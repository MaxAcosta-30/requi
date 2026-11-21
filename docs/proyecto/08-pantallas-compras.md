# Pantallas de Compras (casos 2, 5 y 6 en interfaz)

- **Rutas:** `/compras/catalogo` (proveedores y productos: alta, edición, desactivar con confirmación en línea, filtro por proveedor), `/compras/ordenes`, `/compras/avisos` (bandeja del proveedor) y `/compras/mensajes` (Outbox, filtro `Failed` por defecto, sin reintento manual porque la API no lo tiene).
- **Permisos:** la API decide (403); en Angular los enlaces solo aparecen con rol Compras y, sin ese rol, la pantalla no pide datos y dice que es solo para Compras.
- **Repositorio:** `repositories/compras.ts` envuelve el cliente generado y traduce los 409 por `codigo` (`duplicado`, `proveedor_invalido`, `referencia`).
- **Estados:** carga, vacío, error con "Reintentar" y sin permisos (`features/compras/estado.ts`). Textos en es y en.
- **Corregido al probar en el navegador:** a 390 px el menú desbordaba (554 px) por `whitespace-nowrap` sin `flex-wrap`; los componentes de sección eran *inline* y `space-y-12` no separaba. Enlaces del menú a 44 px de alto.
- **Verificado en local (API + Postgres + `ng serve`, 390 px):** las 4 pantallas como Compras con datos reales; SKU repetido → 409 con su mensaje; producto válido → 201 y aparece; el solicitante ve "solo para Compras"; axe 0. 29 pruebas de Angular y build sin avisos.
- **Pendiente:** `AvisoDto` no trae el folio de la orden (se muestra el inicio del id).
