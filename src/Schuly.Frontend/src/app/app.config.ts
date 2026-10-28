import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { authInterceptor, provideAuth, withAppInitializerAuthCheck } from 'angular-auth-oidc-client';
import { provideApi } from './api';
import { routes } from './app.routes';
import { authLoader } from './core/auth';
import { ThemeStore } from './stores/theme.store';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor()])),
    provideApi(''),
    provideAuth({ loader: authLoader }, withAppInitializerAuthCheck()),
    provideAppInitializer(() => void inject(ThemeStore)),
  ],
};
