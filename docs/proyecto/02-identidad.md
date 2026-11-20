# Etapa 2 · Identidad (caso 1, backend)

- OpenIddict 7.9 dentro de la API: Authorization Code + PKCE obligatorio, access token de 10 min, refresh token de 8 h rotado en cada uso y sin margen de reutilización.
- Reutilizar un refresh token viejo revoca todos los tokens de la sesión; `EnableTokenEntryValidation` hace que el access token vigente deje de servir al instante (FortressID no lo hacía; ver ADR 0002).
- Login servido por la API (`/cuenta/login`): antiforgery, bloqueo tras 5 intentos, mismo mensaje exista o no la cuenta, sin redirecciones abiertas, límite por IP en login y `/connect/token`.
- Tablas en `0002_identidad.sql` generado una vez (ADR 0001); llaves de Data Protection en PostgreSQL. Llaves de firma: certificados en variables de entorno en producción, efímeras en local y pruebas.
- Semilla (roles + cliente público `requi-web`) en `Requi.DbMigrator`, idempotente, solo si hay `Oidc__Redirecciones`.
- **Pruebas:** 21 de integración contra PostgreSQL real (Testcontainers), con el flujo completo como lo hace el navegador.
- **Angular:** `angular-auth-oidc-client` 22.0.3 (certificada por la OpenID Foundation). No toca OIDC hasta que `/health/live` responde (arranque en frío); tokens en `sessionStorage`; el interceptor solo los manda a `/api/`. 11 pruebas con Vitest.
- **Probado en navegador real (Playwright, local):** login servido por la API → callback → Angular muestra al usuario → cierre de sesión; sin errores en consola. Encontró que el servidor aceptaba PKCE `plain`: ahora solo S256 (con prueba).
- **Demo (caso 9, adelanto):** `Demo__Clave` en el migrador crea un usuario por rol (`<rol>@demo.requi`).
