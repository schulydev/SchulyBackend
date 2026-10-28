import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HlmSpinner } from '@spartan-ng/helm/spinner';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { take } from 'rxjs';

@Component({
  selector: 'app-callback',
  imports: [HlmSpinner],
  template: `
    <div class="flex min-h-svh items-center justify-center">
      <hlm-spinner aria-label="Signing in" />
    </div>
  `,
})
export class Callback {
  constructor() {
    const router = inject(Router);
    inject(OidcSecurityService)
      .isAuthenticated$.pipe(take(1))
      .subscribe(({ isAuthenticated }) => router.navigateByUrl(isAuthenticated ? '/' : '/login', { replaceUrl: true }));
  }
}
