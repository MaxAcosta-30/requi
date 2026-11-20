import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';
import { environment } from '../environments/environment';
import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { configuracionOidc } from './core/auth/auth.config';
import { proveedoresI18n } from './core/i18n/i18n';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor()])),
    provideRouter(routes, withComponentInputBinding()),
    provideAuth(configuracionOidc(window.location.origin)),
    provideApiConfiguration(environment.apiUrl),
    proveedoresI18n(),
  ],
};
