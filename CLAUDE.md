# Requi

Sistema de requisiciones de compra: un empleado pide material, un aprobador autoriza según el monto y, al aprobarse, se genera la orden de compra y se avisa al proveedor.

**Frase del proyecto:** "Pide, aprueba y compra sin perder nada en el camino."

## Propósito

Proyecto de portafolio de Max Acosta para aplicar a puestos full stack .NET + Angular. Debe demostrar Minimal API, EF Core con Database First, OIDC con JWT y refresh tokens, el patrón Outbox, Angular moderno con Signals, i18n y pruebas. Presupuesto: $0 al mes. Tiempo: entre 130 y 175 horas.

## Cómo debes trabajar

**Guía general:** sigue también la skill `software-maestro` (copia en `software-maestro(1).md`, fuera de git). Si choca con este archivo, gana este archivo. Sus fases de venta, propuesta, material y SEO local no aplican: Requi no tiene cliente.

1. Trabaja una etapa a la vez. Al terminar cada una, muestra el resultado y **espera la aprobación de Max**.
2. Explica cada decisión importante en una línea: por qué se toma y por qué no otra opción.
3. Antes de programar cada caso, explica el enfoque. Al terminarlo, hazle a Max 3 preguntas de entrevista sobre lo construido y corrige sus respuestas.
4. Usa una rama por caso, commits pequeños, un PR por caso y `main` siempre funcional.
5. Registra las decisiones grandes en `docs/decisiones/` como ADR cortos y el avance por etapa en `docs/proyecto/`.
6. Nunca guardes secretos en el repositorio. No inventes resultados: si algo no se probó, dilo.
7. Antes de crear recursos en la nube, verifica precios y límites vigentes. Si un servicio pide tarjeta o puede generar un cobro, detente y pregunta.
8. Si el proyecto va atrasado, recorta primero los casos "Debería" y avísale a Max.

### Ahorro de tokens

- Responde breve: resultado, decisión y siguiente paso. No resumas lo que hiciste ni repitas lo que dijo Max.
- Busca con grep o glob antes de abrir archivos, y lee solo las partes relevantes.
- No explores todo el repositorio ni releas archivos que no han cambiado.
- Muestra solo los cambios. En salidas largas, muestra solo los errores relevantes.
- Carga una skill solo en la etapa que la necesita. No uses subagentes salvo que Max lo pida.
- Si algo falla dos veces, detente, explica la causa probable y pregunta.
- Un caso por sesión. Al terminar, actualiza `docs/proyecto/` en máximo 10 líneas y sugiere abrir una sesión nueva.
- Excepción: nunca recortes la justificación de decisiones ni las preguntas de entrevista.

### Contra la sobreingeniería

- Antes de agregar una dependencia, capa, servicio o patrón, responde en una línea qué caso "Debe" o qué riesgo real resuelve. Si no hay respuesta, no se agrega.
- Usa primero lo que ya traen .NET y Angular.
- No crees interfaces, genéricos ni abstracciones hasta repetir el mismo patrón 3 veces. Excepción: los repositorios del frontend, que pide la vacante.
- No agregues configuración ni opciones para casos futuros.
- Objetivos de aprendizaje de este proyecto, que sí se justifican: Database First, OIDC, Outbox, i18n, repository pattern en Angular, Tailwind, Vitest y CI/CD.

## Punto de partida

- **Repositorio nuevo:** crea `requi`.
- **FortressID:** úsalo como referencia para OpenIddict. Tiene OpenIddict 7.2 en .NET 10 (Requi usa 7.9, la estable al 17 de noviembre de 2026), con controladores de autorización y tokens. Su única prueba es la plantilla vacía, así que no confíes en que funciona: pruébalo antes de adaptarlo.

Clónalo fuera de la carpeta de `requi`:

```bash
git clone https://github.com/MaxAcosta-30/FortressID.git ../referencias/FortressID
```

## Alcance

### Actores

- **Solicitante:** crea y envía requisiciones.
- **Aprobador:** aprueba o rechaza requisiciones de otros.
- **Compras:** administra productos y proveedores, y consulta órdenes de compra.
- **Proveedor:** no tiene cuenta. Sus avisos llegan a una bandeja visible en la demo.

### Debe

| # | Caso | Criterio de aceptación |
|---|---|---|
| 1 | Iniciar sesión con OIDC | Authorization Code + PKCE; access token de 10 minutos; refresh token rotado, y si se reutiliza uno viejo se revoca la sesión; una prueba comprueba que un rol no puede usar endpoints de otro |
| 2 | Administrar productos y proveedores | Crear, editar, listar y desactivar; FluentValidation en cada DTO; solo el rol Compras puede escribir |
| 3 | Crear una requisición | Renglones dinámicos con Reactive Forms (`FormArray`); el total se calcula en el servidor; se guarda como borrador o se envía |
| 4 | Aprobar o rechazar | Hasta $20,000 MXN basta 1 aprobación; arriba de $20,000 se necesitan 2 de aprobadores distintos; nadie aprueba su propia requisición; rechazar exige motivo |
| 5 | Generar la orden de compra | Al quedar aprobada, el evento se guarda en el Outbox en la misma transacción; el procesador crea la orden una sola vez aunque el evento se procese dos veces |
| 6 | Avisar al proveedor | La orden genera un segundo evento que deja el aviso en la bandeja del proveedor; los fallos se reintentan y, tras 5 intentos, el mensaje queda marcado como fallido y visible |
| 7 | Consultar el historial | Cada acción guarda quién, qué y cuándo; la API no permite editar ni borrar eventos |
| 8 | Cambiar de idioma | Español e inglés sin recargar la página; moneda y fechas según el idioma; el CI falla si falta una traducción en cualquiera de los dos |
| 9 | Probar la demo | Un usuario de prueba por rol, con datos de ejemplo; un aviso indica que la demo puede tardar hasta un minuto en despertar |

### Debería

- Publicar los eventos del Outbox en RabbitMQ, solo en `compose.yaml` local, con un worker que los consuma.
- Trazas y métricas con OpenTelemetry, visibles en local con el Aspire Dashboard.
- Un `azure-pipelines.yml` equivalente al CI de GitHub Actions, documentado en el README. Antes de ejecutarlo, verifica si Azure DevOps exige solicitar los agentes gratuitos.

### Fuera de alcance

- Inventario y recepción de mercancía.
- Facturación o CFDI.
- Envío real de correos al proveedor.
- Redis: ningún caso lo necesita.
- Microservicios: el Outbox se procesa dentro de la misma API.
- App móvil.

### Reglas de estado

- **Requisición:** `Borrador → Enviada → Aprobada → OrdenGenerada`. Desde `Enviada` también puede pasar a `Rechazada`. Desde `Borrador` o `Enviada` puede pasar a `Cancelada`.
- **Mensaje del Outbox:** `Pendiente → Procesado`. Si falla, se reintenta con espera creciente; tras 5 intentos pasa a `Fallido`.
- Estas reglas viven en el dominio, se escriben primero como pruebas y una transición inválida responde 409 con `ProblemDetails`.

## Arquitectura

```text
Navegador
  └─ Angular en Vercel ──> API .NET en Render (Minimal API + OpenIddict) ──> PostgreSQL en Neon
                                     └─ BackgroundService: procesa el Outbox

GitHub Actions: pruebas → revisión de OpenAPI y scaffold → scripts SQL en Neon → despliegue en Render y Vercel
```

### Decisiones

- **.NET 10 LTS y C# moderno** | Por qué: cumple ".NET 8 o superior (LTS)" e incluye la sintaxis de C# 12, como primary constructors y collection expressions | Por qué no .NET 8: termina su soporte antes.
- **Minimal API con grupos de endpoints y `TypedResults`** | Por qué: lo pide la vacante y los tipos de respuesta quedan en el OpenAPI | Por qué no controladores: no los pide y agregan código.
- **Capas Domain, Application, Infrastructure y Api** | Por qué: las reglas de aprobación y del Outbox se prueban sin base de datos | Por qué no MediatR ni CQRS: no resuelven un problema actual.
- **Database First con scripts SQL y DbUp** | Por qué: la vacante pide Database First; DbUp aplica los scripts en orden y registra cuáles ya corrieron | Por qué no migraciones de EF: serían Code First.
- **Scaffold solo de las tablas de negocio** | Por qué: Identity y OpenIddict traen sus entidades, y scaffoldearlas las duplica | Sus tablas viven en scripts, con su propio contexto y mapeo a `snake_case`.
- **Conexiones a Neon** | `Requi.DbMigrator` usa la conexión directa; la API usa el pooler.
- **EF Core con Npgsql, generado con `dotnet ef dbcontext scaffold`** | Por qué: la base es la fuente de verdad | Personalizaciones en clases `partial` para que regenerar no las borre.
- **OpenIddict dentro de la API** | Por qué: cubre OIDC, JWT y refresh tokens con un solo despliegue, y Max ya lo conoce por FortressID | Por qué no Keycloak: sería otro servicio pesado que gastaría horas gratuitas de Render.
- **FluentValidation con un filtro de endpoint** | Por qué: lo pide la vacante y separa la validación del endpoint.
- **Serilog en JSON y `IExceptionHandler` con `ProblemDetails`** | Por qué: logging estructurado y errores con un formato estándar | Nunca expongas stack traces ni tokens en los logs.
- **Angular 22, standalone, zoneless y Signals** | Por qué: es la versión estable al 17 de noviembre de 2026 y cumple "Angular 17 o superior" | Usa Reactive Forms, no Signal Forms: la vacante pide Reactive Forms y Signal Forms sigue siendo experimental (verificarlo en Angular 22).
- **Estado con Signals en servicios** | Por qué no NgRx: no hay un problema de estado que lo justifique.
- **Cliente generado desde el OpenAPI y envuelto en repositorios** | Por qué: lo pide la vacante y los componentes no dependen del cliente generado. Verifica que el generador elegido funcione con Angular 22.
- **Transloco para i18n** | Por qué: cambia el idioma sin recargar | Por qué no `@angular/localize`: exige un build por idioma. Explica ambos en un ADR.
- **Tailwind CSS v4 y Vitest** | Por qué: los pide la vacante, y Vitest ya viene por defecto en Angular (verificarlo en la 22).

### Modelo de datos inicial

Los scripts viven en `database/scripts/` con nombres como `0001_inicial.sql` y usan `snake_case`.

- **Tablas de negocio:** `products`, `suppliers`, `requisitions`, `requisition_lines`, `approvals`, `purchase_orders`, `purchase_order_lines`, `supplier_notifications`, `outbox_messages`, `audit_events`.
- **Identidad:** las tablas de Identity y OpenIddict también van en scripts. Genera su SQL una sola vez desde sus modelos y documéntalo en un ADR.
- **Dinero:** `numeric(18,2)` en la base y `decimal` en C#, nunca `float`.
- **Idempotencia:** `purchase_orders` tiene una restricción única sobre `requisition_id`, para que una orden no se duplique.
- **Outbox:** `outbox_messages` guarda tipo, payload `jsonb`, fecha, intentos, último error y fecha de procesado.

### Outbox

1. Aprobar la requisición e insertar el mensaje ocurren en la misma transacción.
2. El `BackgroundService` lee lotes pequeños cada 5 segundos con `FOR UPDATE SKIP LOCKED`, para que dos instancias no tomen el mismo mensaje.
3. Cada manejador es idempotente. Prueba qué pasa si la API se cae después de crear la orden y antes de marcar el mensaje como procesado.
4. Al arrancar, procesa los mensajes pendientes. Así se demuestra que nada se pierde aunque Render apague la API.

### Estructura

```text
requi/
├── database/scripts/
├── src/
│   ├── Requi.Domain/
│   ├── Requi.Application/
│   ├── Requi.Infrastructure/
│   ├── Requi.Api/
│   └── Requi.DbMigrator/
├── web/                         Angular
│   └── src/app/
│       ├── core/                auth, i18n, interceptores
│       ├── api/                 cliente generado, no se edita
│       ├── repositories/
│       └── features/
├── tests/
│   ├── Requi.UnitTests/
│   ├── Requi.IntegrationTests/
│   └── e2e/
├── docs/
│   ├── decisiones/
│   └── proyecto/
├── .github/workflows/
├── compose.yaml
└── README.md
```

### Seguridad mínima

- **Permisos:** se validan en el servidor con políticas por rol, en cada endpoint.
- **Login:** la página de login la sirve la API en Render, no Angular | Por qué: Vercel y Render son sitios distintos y el navegador bloquearía la cookie de sesión como de terceros. Angular solo redirige a `/connect/authorize`.
- **Data Protection:** las llaves se guardan en PostgreSQL | Por qué: Render reinicia el contenedor y, con llaves en memoria, cada reinicio invalida las sesiones.
- **Tokens:** el cliente Angular es público y usa PKCE. El access token dura 10 minutos y el refresh token se rota en cada uso. Documenta en un ADR el riesgo de guardar tokens en el navegador y por qué no se hace un BFF en este proyecto.
- **CORS:** solo acepta el dominio de Vercel y `localhost`.
- **Límites de intentos:** en el login y en el endpoint de tokens.
- **Validación:** FluentValidation en todas las entradas y montos siempre positivos.
- **Secretos:** en variables de entorno de Render y Vercel, y en los secretos de GitHub Actions. Los certificados de firma de OpenIddict nunca van al repositorio.

## Infraestructura a $0

Datos revisados el 11 de noviembre de 2026. Verifícalos otra vez antes de crear recursos.

**Regla de costo:** usa solo planes gratuitos y no registres ninguna tarjeta.

- **Base de datos: Neon Free.** Ofrece 1 GB y 100 CU-horas por proyecto al mes, y se apaga tras 5 minutos sin uso. Fija el cómputo en 0.25 CU, usa la cadena de conexión con pooler y configura `EnableRetryOnFailure`. No uses Supabase, porque sus 2 proyectos gratuitos ya están ocupados.
- **API: Render Free.** Las 750 horas al mes se comparten entre todos los servicios de la cuenta, y si se acaban, Render suspende todos hasta el mes siguiente. **Antes de crear el servicio**, confirma con Max que Semáforo revisa Hito solo una vez por hora. Antes de crearlo, confirma también que el plan gratuito acepte el Dockerfile.
- **Frontend: Vercel Hobby**, como sitio estático. Su uso es personal y no comercial.
- **Issuer de OIDC:** usa la URL de Render como issuer y configura CORS. Si eliges otra opción, documenta por qué en un ADR.
- **Arranque en frío:** antes de redirigir al login, Angular llama a `/health/live`, muestra "Despertando la demo, puede tardar hasta un minuto" y reintenta.

### Despliegue

- **Scripts SQL:** `Requi.DbMigrator` los aplica en Neon desde GitHub Actions antes del despliegue. La API nunca modifica la base al arrancar.
- **Revisión de scaffold:** el CI levanta PostgreSQL, aplica los scripts, regenera el `DbContext` y falla si hay diferencias con el código.
- **Revisión de OpenAPI:** el CI genera el documento OpenAPI, regenera el cliente de Angular y falla si hay diferencias.
- **Publicación:** al terminar, llama al deploy hook de Render y despliega el frontend en Vercel.

## Pruebas

- **TDD pragmático:** escribe primero la prueba de las reglas de aprobación, las transiciones de estado y el procesador del Outbox. Lo demás se prueba después.
- **Unitarias (xUnit):** dominio, validadores y manejadores del Outbox.
- **Integración:** API contra PostgreSQL real con Testcontainers, aplicando los mismos scripts SQL. Incluye permisos por rol, rotación de refresh tokens e idempotencia del Outbox.
- **Frontend (Vitest):** repositorios con `HttpTestingController`, validaciones del formulario y existencia de cada traducción en los dos idiomas.
- **E2E (Playwright):** un flujo: el solicitante crea una requisición → el aprobador la aprueba → Compras ve la orden y el proveedor recibe el aviso.

## Etapas

Detente y espera aprobación al final de cada una.

### Etapa 0: preparación

1. Clona FortressID y resume en 10 líneas qué se reutiliza y qué hay que probar.
2. Verifica .NET 10, Node LTS, Angular CLI, Docker, Git y GitHub CLI. Dile a Max qué falta.
3. Crea la solución, `compose.yaml` con PostgreSQL, el primer script SQL, `Requi.DbMigrator` y el primer scaffold.
4. Crea la API con `/health/live`, `/health/ready` y el documento OpenAPI; el proyecto Angular; y `ci.yml` con compilación y pruebas de ambos.

### Etapa 1: despliegue temprano

1. Guía a Max para crear las cuentas de Neon, Render y Vercel. No debe registrar tarjeta ni pegarte contraseñas.
2. Publica la API mínima en Render conectada a Neon, y el frontend mínimo en Vercel conectado a la API.
3. Confirma en los tres paneles que todo esté en plan gratuito.

### Etapa 2: identidad

1. Implementa el caso 1 con OpenIddict y el login en Angular.
2. Prueba la rotación y la reutilización de refresh tokens antes de seguir.

### Etapa 3: diseño

1. Mapea los casos Debe a pantallas.
2. Usa `interface-design` si está disponible para definir la dirección visual con Tailwind. Presenta 2 o 3 opciones.
3. Guarda las reglas en `docs/proyecto/07-diseno.md` y diseña la lista de requisiciones, el formulario con renglones y la vista de aprobación, con estados de carga, vacío, error y éxito.

### Etapa 4: casos 2 a 5

Construye un caso completo a la vez, de la interfaz a la base de datos.

### Etapa 5: casos 6 a 9

Termina el aviso al proveedor, el historial, el i18n y la demo.

### Etapa 6: calidad

1. Completa las pruebas.
2. Revisa accesibilidad, rendimiento y seguridad. Usa `code-review` si está disponible.

### Etapa 7: presentación

1. Escribe el README: problema, demo, capturas, arquitectura, decisiones, cómo correrlo y límites conocidos. Incluye un diagrama del flujo del Outbox.
2. Escribe 3 viñetas para el CV que digan solo lo que el repositorio demuestra.
3. Haz con Max una entrevista simulada de 10 preguntas, incluidas Outbox, Database First, PKCE y rotación de refresh tokens.

## Definición de terminado

- [ ] Los 9 casos Debe cumplen su criterio.
- [ ] La demo pública funciona desde una ventana privada, después de un arranque en frío.
- [ ] El CI pasa, incluidas las revisiones de scaffold y de OpenAPI, y el despliegue es automático.
- [ ] El costo mensual es $0 y ningún servicio tiene tarjeta registrada.
- [ ] El README permite entender el proyecto en menos de 2 minutos.
- [ ] Max puede explicar cada decisión sin ayuda.
