import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OidcSecurityService, StsConfigHttpLoader, StsConfigLoader } from 'angular-auth-oidc-client';
import { map, take } from 'rxjs';
import { AppService } from '../api';

export const authLoader = {
  provide: StsConfigLoader,
  useFactory: (app: AppService) =>
    new StsConfigHttpLoader(
      app.apiAppGet().pipe(
        map((config) => ({
          authority: config.authority,
          clientId: config.webClientId,
          redirectUrl: `${location.origin}/callback`,
          postLogoutRedirectUri: location.origin,
          scope: 'openid profile email offline_access',
          responseType: 'code',
          silentRenew: true,
          useRefreshToken: true,
          renewTimeBeforeTokenExpiresInSeconds: 30,
          secureRoutes: ['/api/'],
        })),
      ),
    ),
  deps: [AppService],
};

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(OidcSecurityService).isAuthenticated$.pipe(
    take(1),
    map(({ isAuthenticated }) => isAuthenticated || router.createUrlTree(['/login'])),
  );
};

export const guestGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(OidcSecurityService).isAuthenticated$.pipe(
    take(1),
    map(({ isAuthenticated }) => !isAuthenticated || router.createUrlTree(['/'])),
  );
};
