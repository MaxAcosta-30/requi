# Casos de uso

Estado al 20 de noviembre de 2026. Todos los PR (#2 a #8) siguen en borrador y solo el #1 (etapa 0) está fusionado, así que ningún caso está "cumplido con PR fusionado". El e2e de Playwright (`tests/e2e/`, ver `10-e2e.md`) pasó en verde en local; su job `e2e` del CI aún no se ha ejecutado en GitHub: NO VERIFICADO.

## Caso 1 · Iniciar sesión con OIDC (Debe)
- **Actor:** cualquier usuario · **Permiso:** ninguno para entrar; cada endpoint exige su rol.
- **Para qué:** que Angular obtenga un access token sin guardar contraseñas.
- **Condición previa:** cuenta existente con rol; cliente público `requi-web` sembrado.
- **Flujo:** 1. Angular redirige a `/connect/authorize` con PKCE. 2. La API sirve el login (`/cuenta/login`). 3. Vuelve con un `code` y Angular lo canjea en `/connect/token`.
- **Errores y casos límite:** sin PKCE o con `plain` se rechaza; verificador distinto no canjea; login sin antiforgery se rechaza; bloqueo tras 5 intentos; 429 por límite; refresh token reutilizado revoca la sesión.
- **Criterio de aceptación:** Authorization Code + PKCE; access token de 10 minutos; refresh token rotado, y si se reutiliza uno viejo se revoca la sesión; una prueba comprueba que un rol no puede usar endpoints de otro.
- **Pruebas:** `tests/Requi.IntegrationTests/Identidad/IdentidadTests.cs` (flujo, PKCE, rotación, reutilización, rol contra endpoint ajeno, 429); `tests/Requi.IntegrationTests/Identidad/LogsSinSecretosTests.cs`; `web/src/app/core/auth/sesion.spec.ts`; `web/src/app/app.spec.ts`.
- **Estado:** cumplido en rama, PR #3 en borrador (sin fusionar). E2E: `tests/e2e/flujo.spec.ts` (login real).

## Caso 2 · Administrar productos y proveedores (Debe)
- **Actor:** Compras (escribe); los demás roles leen · **Permiso:** rol Compras para crear, editar y desactivar.
- **Para qué:** mantener el catálogo del que salen los renglones.
- **Condición previa:** sesión iniciada.
- **Flujo:** 1. Compras crea o edita un proveedor o producto. 2. FluentValidation revisa el DTO. 3. Se lista o se desactiva.
- **Errores y casos límite:** 400 por campo; SKU repetido 409 sin detalles internos; precio con más de 2 decimales; no se da de alta un producto de un proveedor desactivado; editar algo inexistente 404; sin sesión 401.
- **Criterio de aceptación:** Crear, editar, listar y desactivar; FluentValidation en cada DTO; solo el rol Compras puede escribir.
- **Pruebas:** `tests/Requi.IntegrationTests/Casos/CatalogoTests.cs`; `web/src/app/features/compras/productos-seccion.spec.ts`; `web/src/app/repositories/compras.spec.ts`.
- **Estado:** cumplido en rama (API PR #6, pantallas PR #8), ambos en borrador.

## Caso 3 · Crear una requisición (Debe)
- **Actor:** Solicitante · **Permiso:** rol Solicitante.
- **Para qué:** pedir material con varios renglones.
- **Condición previa:** sesión iniciada; productos activos en el catálogo.
- **Flujo:** 1. Elige proveedor y agrega renglones (`FormArray`). 2. El servidor toma el precio del catálogo y calcula el total. 3. Guarda como borrador o envía.
- **Errores y casos límite:** producto de otro proveedor se rechaza; el precio no lo pone el cliente; sin renglones no se envía; cantidad 0, negativa o decimal y producto repetido son inválidos en el formulario; otro solicitante no ve ni cancela lo ajeno.
- **Criterio de aceptación:** Renglones dinámicos con Reactive Forms (`FormArray`); el total se calcula en el servidor; se guarda como borrador o se envía.
- **Pruebas:** `tests/Requi.UnitTests/ReglasDeRequisicionTests.cs`; `tests/Requi.IntegrationTests/Casos/RequisicionesTests.cs`; `web/src/app/features/requisiciones/nueva.spec.ts`; `web/src/app/repositories/requisiciones.spec.ts`. E2E: `tests/e2e/flujo.spec.ts`.
- **Estado:** cumplido en rama (dominio PR #4, API PR #6, pantallas PR #7), en borrador.

## Caso 4 · Aprobar o rechazar (Debe)
- **Actor:** Aprobador · **Permiso:** rol Aprobador.
- **Para qué:** autorizar el gasto según su monto.
- **Condición previa:** requisición en estado Enviada.
- **Flujo:** 1. El aprobador abre "por aprobar". 2. Aprueba o rechaza (con motivo). 3. Con el total y las firmas se decide si pasa a Aprobada.
- **Errores y casos límite:** hasta $20,000 basta 1 firma, arriba 2 distintas; el mismo aprobador no cuenta dos veces; nadie decide sobre lo suyo; rechazar sin motivo es 400; transición inválida es 409 con `ProblemDetails`.
- **Criterio de aceptación:** Hasta $20,000 MXN basta 1 aprobación; arriba de $20,000 se necesitan 2 de aprobadores distintos; nadie aprueba su propia requisición; rechazar exige motivo.
- **Pruebas:** `tests/Requi.UnitTests/ReglasDeRequisicionTests.cs`; `tests/Requi.IntegrationTests/Casos/RequisicionesTests.cs`. E2E: `tests/e2e/flujo.spec.ts`.
- **Estado:** cumplido en rama (PR #4, #6 y #7), en borrador.

## Caso 5 · Generar la orden de compra (Debe)
- **Actor:** sistema (procesador del Outbox); Compras la consulta · **Permiso:** Compras para consultar.
- **Para qué:** convertir la aprobación en orden sin perder el evento.
- **Condición previa:** requisición Aprobada.
- **Flujo:** 1. Al aprobar, `RequisicionAprobada` se guarda en el Outbox en la misma transacción. 2. `ProcesadorOutbox` toma el mensaje con `FOR UPDATE SKIP LOCKED`. 3. Crea la orden y marca el mensaje.
- **Errores y casos límite:** procesar dos veces no duplica (comprobación más restricción única); dos procesadores no toman el mismo mensaje.
- **Criterio de aceptación:** Al quedar aprobada, el evento se guarda en el Outbox en la misma transacción; el procesador crea la orden una sola vez aunque el evento se procese dos veces.
- **Pruebas:** `tests/Requi.IntegrationTests/Casos/RequisicionesTests.cs` (idempotencia, dos procesadores, flujo completo); `web/src/app/repositories/compras.spec.ts`. Procesar al arrancar los pendientes: NO VERIFICADO con prueba. E2E: `tests/e2e/flujo.spec.ts`.
- **Estado:** cumplido en rama (PR #6 y #8), en borrador.

## Caso 6 · Avisar al proveedor (Debe)
- **Actor:** sistema; Compras ve la bandeja · **Permiso:** Compras.
- **Para qué:** que el proveedor (sin cuenta) quede avisado en la bandeja de la demo.
- **Condición previa:** orden generada.
- **Flujo:** 1. `OrdenGenerada` crea el segundo evento. 2. Su manejador deja el aviso en `/api/avisos-proveedor`. 3. Si falla, se reintenta (5, 10, 20, 40 s).
- **Errores y casos límite:** al quinto fallo queda `Failed` y visible en `/api/outbox?estado=Failed`; reprocesar no duplica el aviso.
- **Criterio de aceptación:** La orden genera un segundo evento que deja el aviso en la bandeja del proveedor; los fallos se reintentan y, tras 5 intentos, el mensaje queda marcado como fallido y visible.
- **Pruebas:** `tests/Requi.IntegrationTests/Casos/RequisicionesTests.cs`; `web/src/app/repositories/compras.spec.ts` (mensajes con filtro de estado). E2E: `tests/e2e/flujo.spec.ts`.
- **Estado:** cumplido en rama (PR #6 y #8), en borrador.

## Caso 7 · Consultar el historial (Debe)
- **Actor:** usuarios con permiso de ver la requisición · **Permiso:** según rol: NO VERIFICADO el detalle de permisos.
- **Para qué:** saber quién hizo qué y cuándo.
- **Condición previa:** acciones previas sobre la requisición.
- **Flujo:** 1. Cada acción escribe un evento en `audit_events`. 2. La API los lista en orden (`seq`).
- **Errores y casos límite:** la base impide editar o borrar eventos (trigger); mismo instante se ordena por `seq`.
- **Criterio de aceptación:** Cada acción guarda quién, qué y cuándo; la API no permite editar ni borrar eventos.
- **Pruebas:** `tests/Requi.IntegrationTests/Casos/RequisicionesTests.cs` (`El_historial_no_se_puede_editar_ni_borrar_ni_desde_la_base`). Pantalla de historial: NO VERIFICADO.
- **Estado:** cumplido en la API (PR #6, borrador); interfaz NO VERIFICADA.

## Caso 8 · Cambiar de idioma (Debe)
- **Actor:** cualquier usuario · **Permiso:** ninguno.
- **Para qué:** usar la app en español o inglés.
- **Condición previa:** ninguna.
- **Flujo:** 1. El usuario elige el idioma. 2. Transloco cambia los textos sin recargar. 3. Dinero (MXN) y fechas siguen el idioma.
- **Errores y casos límite:** falta de clave, texto vacío o parámetros `{{x}}` distintos entre idiomas hacen fallar la prueba.
- **Criterio de aceptación:** Español e inglés sin recargar la página; moneda y fechas según el idioma; el CI falla si falta una traducción en cualquiera de los dos.
- **Pruebas:** `web/src/app/core/i18n/traducciones.spec.ts` (corre en el job web del CI con `ng test`).
- **Estado:** cumplido en rama (PR #7, borrador). Cambio sin recargar verificado a mano en navegador local; sin e2e en el CI.

## Caso 9 · Probar la demo (Debe)
- **Actor:** visitante · **Permiso:** ninguno.
- **Para qué:** recorrer el flujo sin registrarse.
- **Condición previa:** `Demo__Clave` configurada en el migrador (secreto `DEMO_CLAVE`).
- **Flujo:** 1. Angular llama a `/health/live` y muestra el aviso de arranque en frío. 2. El visitante entra con un usuario `<rol>@demo.requi`. 3. Recorre el flujo.
- **Errores y casos límite:** si la API no despierta, Angular pasa a "no disponible". Datos de ejemplo (requisiciones, productos): NO VERIFICADO; la semilla confirmada solo crea usuarios.
- **Criterio de aceptación:** Un usuario de prueba por rol, con datos de ejemplo; un aviso indica que la demo puede tardar hasta un minuto en despertar.
- **Pruebas:** `web/src/app/app.spec.ts` y `web/src/app/core/api-status.spec.ts` (aviso de despertar). Usuarios de demo: sin prueba automatizada.
- **Estado:** en construcción. Demo pública NO desplegada: Neon, Render y Vercel esperan a Max (ver `estado.md`).

## Debería

- **RabbitMQ para el Outbox (solo `compose.yaml`):** pendiente; no hay rastro en `compose.yaml` ni en los proyectos.
- **OpenTelemetry con Aspire Dashboard:** pendiente; sin paquetes ni configuración.
- **`azure-pipelines.yml`:** pendiente; el archivo no existe.
