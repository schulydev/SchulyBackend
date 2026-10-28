import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthStore } from '../stores/auth.store';

const withToken = (request: HttpRequest<unknown>, token: string | null) =>
  token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

const needsToken = (url: string) =>
  url.startsWith('/api/') ||
  (url.startsWith('/auth/') &&
    !/^\/auth\/(login|register|refresh|logout|password\/forgot|password\/reset|email\/verify|2fa\/verify)$/.test(
      url,
    ));

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!needsToken(request.url)) return next(request);

  const auth = inject(AuthStore);
  return next(withToken(request, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401)
        return throwError(() => error);
      return auth.refresh().pipe(
        switchMap((token) => {
          if (!token) {
            auth.signedOutElsewhere();
            return throwError(() => error);
          }
          return next(withToken(request, token));
        }),
      );
    }),
  );
};

export const authGuard: CanActivateFn = () =>
  inject(AuthStore).status() === 'signedIn' || inject(Router).createUrlTree(['/login']);

export const guestGuard: CanActivateFn = () =>
  inject(AuthStore).status() !== 'signedIn' || inject(Router).createUrlTree(['/']);
