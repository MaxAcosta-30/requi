# Etapa 2 · Identidad (caso 1, backend)

- OpenIddict 7.9 dentro de la API: Authorization Code + PKCE obligatorio, access token de 10 min, refresh token de 8 h rotado en cada uso y sin margen de reutilización.
- Reutilizar un refresh token viejo revoca todos los tokens de la sesión; `EnableTokenEntryValidation` hace que el access token vigente deje de servir al instante (FortressID no lo hacía; ver ADR 0002).
- Login servido por la API (`/cuenta/login`): antiforgery, bloqueo tras 5 intentos, mismo mensaje exista o no la cuenta, sin redirecciones abiertas, límite por IP en login y `/connect/token`.
- Tablas en `0002_identidad.sql` generado una vez (ADR 0001); llaves de Data Protection en PostgreSQL. Llaves de firma: certificados en variables de entorno en producción, efímeras en local y pruebas.
- Semilla (roles + cliente público `requi-web`) en `Requi.DbMigrator`, idempotente, solo si hay `Oidc__Redirecciones`.
- **Pruebas:** 20 de integración contra PostgreSQL real (Testcontainers), con el flujo completo como lo hace el navegador.
- **Falta:** login en Angular (redirigir a `/connect/authorize`, callback, refresco y cierre de sesión).
