# Estado

- **Etapa 0:** hecha y fusionada (PR #1).
- **Etapa 1 · despliegue (rama `etapa-1/despliegue`, subida, sin PR):** Dockerfile, CORS por configuración, aviso de arranque en frío y job de despliegue. **Bloqueada por Max:** crear Neon, Render y Vercel y cargar los secretos (ver abajo).
- **Etapa 2 · identidad (rama `etapa-2/identidad`, encadenada sobre la 1):** caso 1 completo: API + login en Angular, probado con 21 pruebas de integración, 11 de Angular y en navegador real (ver `02-identidad.md`). Siguiente: Etapa 3 · diseño.
- **Pendiente con Max (Etapa 1):** secretos `NEON_MIGRATIONS_CONNECTION`, `RENDER_DEPLOY_HOOK`, `VERCEL_DEPLOY_HOOK`; variables `OIDC_REDIRECCIONES` y `OIDC_REDIRECCIONES_SALIDA`; en Render: `ConnectionStrings__Database` (pooler de Neon), `Oidc__Issuer` (URL de Render), `Oidc__CertificadoFirma`, `Oidc__CertificadoCifrado` y `Oidc__ClaveCertificados`, `Cors__Origins`; secreto `DEMO_CLAVE` en GitHub (contraseña de los usuarios de demo, mínimo 10 caracteres). Confirmar horas de Render con Hito y Semáforo.
