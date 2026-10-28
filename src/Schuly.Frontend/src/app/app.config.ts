import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideApi } from './api';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth';
import { AuthStore } from './stores/auth.store';
import { ThemeStore } from './stores/theme.store';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideApi(''),
    provideAppInitializer(() => {
      inject(ThemeStore);
      return inject(AuthStore).restore();
    }),
  ],
};
