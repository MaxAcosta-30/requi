import { LogLevel, PassedInitialConfig } from 'angular-auth-oidc-client';
import { environment } from '../../../environments/environment';

/**
 * Cliente público con Authorization Code + PKCE contra OpenIddict en la API (ADR 0002).
 * El access token dura 10 min y se renueva con el refresh token, que rota en cada uso.
 * Los tokens viven en sessionStorage: se borran al cerrar la pestaña.
 */
export function configuracionOidc(origen: string): PassedInitialConfig {
  return {
    config: {
      authority: environment.apiUrl,
      clientId: 'requi-web',
      redirectUrl: `${origen}/callback`,
      postLogoutRedirectUri: `${origen}/`,
      scope: 'openid profile email roles offline_access requi-api',
      responseType: 'code',
      useRefreshToken: true,
      silentRenew: true,
      renewTimeBeforeTokenExpiresInSeconds: 60,
      ignoreNonceAfterRefresh: true,
      // OpenIddict no publica /userinfo para este cliente: los datos salen del id_token.
      autoUserInfo: false,
      // El token solo viaja a la API, nunca a otros dominios.
      secureRoutes: [`${environment.apiUrl}/api/`],
      logLevel: LogLevel.Warn,
    },
  };
}
