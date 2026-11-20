# Etapa 4 (adelanto) · Reglas de estado y de aprobación (casos 3 y 4, dominio)

Escritas con TDD (primero las 22 pruebas en `tests/Requi.UnitTests`, luego el código), sin base de datos ni interfaz.
- `Requisition.Reglas.cs` (clase `partial`: el scaffold no la toca): `Enviar`, `Aprobar`, `Rechazar`, `Cancelar`, `MarcarOrdenGenerada` y `CalcularTotal`.
- Total calculado en el servidor desde los renglones. Hasta $20,000.00 basta 1 aprobación; desde $20,000.01, 2 de aprobadores distintos. Nadie decide sobre lo suyo; nadie decide dos veces; rechazar exige motivo (se guarda sin espacios sobrantes).
- Toda violación lanza `ReglaDeNegocioException` con un código (`transicion_invalida`, `propia`, `ya_decidio`, `motivo_requerido`, `sin_renglones`, `no_es_tuya`); la API la convertirá en 409 con `ProblemDetails` al construir los endpoints.
- Pendiente: endpoints de requisiciones (caso 3) y aprobación (caso 4), y el Outbox (caso 5), después del diseño (Etapa 3).
