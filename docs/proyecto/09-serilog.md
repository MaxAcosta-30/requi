# Serilog (logs en JSON)

- **Qué se registra:** Serilog.AspNetCore 10.0.0 escribe en consola JSON compacto (`RenderedCompactJsonFormatter`), con niveles de `Serilog:MinimumLevel`: Information, y Warning para `Microsoft`, `Microsoft.AspNetCore`, `OpenIddict` y `System`.
- **Por request:** una línea con método, `RequestPath`, estado y duración, más `TraceId`. Los `/health` van a Debug (no salen). Los 5xx van a Error.
- **Errores:** `ManejadorDeErrores` registra los 500 con Error (con excepción, solo en el log) y responde `ProblemDetails` genérico sin traza.
- **Qué NO se registra:** `RequestPath` es `Request.Path`, sin querystring, así que `/connect/authorize?code=...` no deja el `code`. No se registran encabezados, cookies ni cuerpos (`/connect/token`, `/cuenta/login`).
- **Ojo:** `OpenIddict` en Warning es una defensa real: probado con todo en Verbose, OpenIddict escribe parte del refresh token (`ReferenceId`). No bajes ese nivel en producción.
- **Prueba:** `tests/Requi.IntegrationTests/Identidad/LogsSinSecretosTests.cs` captura los eventos con un `ILogEventSink` en memoria; tras login, canje y refresh no aparecen `code`, `code_verifier`, tokens, contraseña ni `?code=`/`?refresh_token=` de una ruta inventada.
- **No probado:** que el 500 del manejador no filtre datos con una excepción real (el código lo evita, sin prueba); `Requi.DbMigrator` conserva su formato propio.
- **Defensa en código:** `Program.cs` fija `OpenIddict` en Warning después de leer la configuración, así ninguna variable de entorno la baja. La prueba `Con_todos_los_niveles_en_Verbose_tampoco_se_filtran_secretos` lo comprueba; sin esa línea, la prueba falla (verificado el 20 nov 2026).
