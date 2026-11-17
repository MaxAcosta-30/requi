# Etapa 0 · Preparación

**FortressID (referencia, commit 22074ad):** se reutiliza la forma de `AuthorizationController`, `TokenController`, el login con `SignInManager` y lockout, y el alta de clientes con `IOpenIddictApplicationManager`. No sirve tal cual (NO VERIFICADO que funcione; nunca se probó):
- SQL Server → Npgsql; `EnsureCreatedAsync` al arrancar → se quita (la API no toca el esquema).
- No llama a `SetScopes`: sin `offline_access` concedido no se emite refresh token. Tampoco agrega el claim de rol.
- Cliente confidencial de Postman → cliente público con PKCE para Angular.
- Certificados de desarrollo, transporte HTTP permitido y antiforgery apagado globalmente → solo en Development.
- Duración por defecto del access token (1 h) → 10 min. La revocación al reusar un refresh token hay que probarla.

**Herramientas:** Node 24.21 (compatible con Angular 22), Docker 29.9, Compose 5.7, Git 2.56, gh 2.97 con sesión. Faltan .NET 10 SDK (hay 9.0.122) y Angular CLI.
