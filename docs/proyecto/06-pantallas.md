# Pantallas de Angular (casos 3, 4 y 8) y demo (caso 9)

- **Cliente generado** con ng-openapi-gen 1.1.0 en `web/src/app/api` (no se edita); el CI lo regenera y falla si hay diferencias. Los componentes usan **repositorios** (`repositories/`) que normalizan números, fechas y errores (`ErrorApi` con el código de la regla).
- **Pantallas** con el diseño "Papel autocopiante": lista por rol (mías, por aprobar, todas), nueva requisición con `FormArray` (sin productos repetidos, cantidad entera > 0, total estimado y aviso de 1 o 2 firmas) y detalle con aprobar/rechazar (motivo obligatorio), cancelar y casillas de firma.
- **Caso 8:** Transloco 8.4 con español e inglés, cambio sin recargar; dinero en MXN y fechas con el formato del idioma; una prueba falla si falta una clave, un texto está vacío o los parámetros `{{x}}` no coinciden.
- **Caso 9:** usuarios de demo por rol más un segundo aprobador (las requisiciones de más de $20,000 necesitan dos).
- **Fuentes** autoalojadas con @fontsource (Public Sans Variable y JetBrains Mono).
- **Verificado en navegador real (local):** solicitante crea y envía una requisición de $22,850 (aviso "2 firmas") → aprobador 1 firma (1 de 2) → cambio a inglés sin recargar → aprobador 2 firma → el Outbox genera la orden OC-000001 y el aviso al proveedor → Compras la ve como "Orden generada". axe 0 en lista, nueva y detalle (claro/oscuro, 1440/390).
- **Pruebas de Angular:** 21 (repositorios con `HttpTestingController`, formulario, traducciones, app).
- **Pendiente:** pantallas de Compras para catálogo, órdenes, bandeja del proveedor y mensajes fallidos (la API ya existe); el e2e de Playwright en el CI (hoy es un script local).
