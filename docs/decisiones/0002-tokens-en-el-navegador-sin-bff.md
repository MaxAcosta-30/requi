# 0002 · Tokens en el navegador (cliente público con PKCE), sin BFF

**Contexto:** Angular vive en Vercel y la API en Render (sitios distintos). Angular necesita un access token para llamar a la API.

**Decisión:** Angular es un cliente **público** con Authorization Code + PKCE. El access token dura 10 minutos y el refresh token (8 h) **rota en cada uso**; si alguien presenta uno ya usado, OpenIddict revoca todos los tokens de la sesión (`SetRefreshTokenReuseLeeway(0)`), y la API revisa en cada petición que el token no esté revocado (`EnableTokenEntryValidation`). Los tokens se guardan en memoria; el refresh token, en `sessionStorage` (se borra al cerrar la pestaña).

**Riesgo aceptado:** un XSS en Angular podría leer los tokens. Mitigaciones: CSP estricta en Vercel, sin `innerHTML` con datos del usuario, vida corta del access token y detección de reutilización del refresh token.

**Por qué no un BFF** (backend que guarda los tokens y da una cookie a Angular): con Vercel y Render en dominios distintos la cookie sería de terceros y los navegadores la bloquean; resolverlo exige un dominio propio para ambos (costo) o un proxy en Vercel (otro servicio que mantener). Para un portafolio con datos de demo, el riesgo restante es aceptable.

**Revisar si:** Requi maneja datos reales o se consigue un dominio propio: entonces BFF con cookie `HttpOnly` `SameSite=Strict`.
