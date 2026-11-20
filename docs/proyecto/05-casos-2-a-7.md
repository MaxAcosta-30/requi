# Etapa 4–5 · Casos 2 a 7 en la API

- **Caso 2 · catálogo** (`/api/proveedores`, `/api/productos`): crear, editar, listar y desactivar. Todos leen; solo Compras escribe. FluentValidation con un filtro de endpoint (400 por campo); SKU normalizado; precio con máximo 2 decimales.
- **Errores:** `ManejadorDeErrores` convierte reglas rotas y duplicados en 409 `ProblemDetails` con `codigo`; lo demás en 500 genérico, sin trazas ni nombres de restricciones.
- **Caso 3 · requisiciones** (`/api/requisiciones`): crear como borrador o enviada; el precio sale del catálogo y el total lo calcula el servidor; cada producto debe ser del proveedor elegido. Vistas `mias`, `por-aprobar` y `todas` según el rol; sin permiso, 404.
- **Caso 4 · aprobar/rechazar:** usa las reglas del dominio (1 o 2 firmas, nada propio, motivo obligatorio).
- **Caso 5 · Outbox:** al completar las firmas, `RequisicionAprobada` entra al Outbox en la misma transacción. `ProcesadorOutbox` toma un mensaje por transacción con `FOR UPDATE SKIP LOCKED`, dentro de la estrategia de reintentos de Npgsql; la orden se crea una sola vez (comprobación + restricción única).
- **Caso 6 · aviso al proveedor:** `OrdenGenerada` deja el aviso en `/api/avisos-proveedor`; los fallos se reintentan (5, 10, 20, 40 s) y al quinto quedan `Failed`, visibles en `/api/outbox?estado=Failed`.
- **Caso 7 · historial:** cada acción guarda quién, qué y cuándo; consecutivo `seq` (script `0003`) para un orden estable; la base impide editar o borrar (trigger).
- **Encontrado al probar:** el dominio ponía `Id` a las aprobaciones y EF las trataba como existentes (500); la transacción manual chocaba con `EnableRetryOnFailure`; el historial se desordenaba con la misma hora.
- **Pruebas:** 40 de integración y 22 unitarias en verde. Una corrida tardó 9 min sin poder reproducirlo (las siguientes, 2 min 24 s): NO VERIFICADA la causa.
