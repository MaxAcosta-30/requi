# Etapa 3 · Diseño

Prototipo navegable: `docs/diseno/prototipo.html` (Tailwind 4.3.5, la misma versión que la app). Capturas: `docs/diseno/claro-1440.png`, `oscuro-1440.png`, `celular-390.png`.

## Casos → pantallas

| Pantalla | Casos | Quién |
|---|---|---|
| Mis requisiciones | 3, 4, 5 | Solicitante: qué pidió y en qué paso va |
| Nueva requisición (renglones con `FormArray`) | 3 | Solicitante |
| Por aprobar → aprobar o rechazar | 4 | Aprobador |
| Productos y proveedores | 2 | Compras (tabla + formulario; mismo sistema) |
| Órdenes y bandeja del proveedor | 5, 6 | Compras y la demo del proveedor |
| Historial de una requisición | 7 | Todos (línea de tiempo de solo lectura) |

## Brief (5 líneas)
- **Para quién:** solicitante, aprobador y Compras de una empresa mediana.
- **Qué debe sentir en 3 s:** orden: "sé dónde está mi pedido".
- **Personalidad:** oficinista confiable.
- **Lo que debe recordar:** las casillas de firma.
- **Contenido más fuerte:** la regla de los $20,000 (1 o 2 firmas) → se vuelve la firma visual.

## Tres direcciones (elegida: A)

| | A · Papel autocopiante | B · Andén de carga | C · Libro mayor |
|---|---|---|---|
| Referencia | Talonario de requisiciones en triplicado | Almacén y etiquetas de tarima | Contabilidad en papel verde |
| Tipografía | Public Sans + JetBrains Mono | Oswald + Roboto Mono | Libre Caslon + Inconsolata |
| Paleta | Papel `#FBFAF6`, carbón `#27357E`, copias canario/menta/cielo/rosa | Amarillo `#F2C200`, negro `#141414` | Verde `#E8F0E3`, tinta roja `#B3261E` |
| Firma | Casillas de firma según el monto | Etiqueta de tarima por pedido | Columnas debe/haber |

**Por qué A** (`software-maestro` §7.4, sin Max presente): convierte la regla central (1 o 2 firmas) en lo que se ve primero, y el azul del carbón sale del mundo del producto en lugar de ser el azul SaaS de siempre. B grita sin informar; C confunde un pedido con contabilidad. Cambiarla es cambiar los tokens.

## Tokens (`web/src/styles.css`, `@theme inline`)
- **Superficies:** `papel` (hojas), `mesa` (fondo y campos), `renglon` (líneas). Oscuro `#1B1D22 / #121317 / #2C2F37`.
- **Texto:** `tinta`, `tinta-2`, `tinta-3` (metadatos). **Acción y cifras:** `carbon` (único color de acción: botón primario, montos, foco); `carbon-suave` para la navegación activa.
- **Estado = la copia del talonario:** Borrador = original (borde punteado), En firma = `canario`, Aprobada = `menta`, Orden generada = `cielo`, Rechazada = `rosa`; siempre con el texto del estado. `rojo` solo para errores y rechazos en texto.
- **Tipografía:** Public Sans (400–800; titulares 800 con tracking negativo) y JetBrains Mono con cifras tabulares para folios, cantidades y dinero. Escala 1.25 sobre 15 px.
- **Profundidad:** la hoja (`hoja`: anillo de 1 px + dos sombras suaves), porque son papeles sobre una mesa; nada más lleva sombra. Radios 4 px (controles) y 6 px (hojas).

## Componentes
- **Casillas de firma (firma visual):** 1 casilla hasta $20,000 y 2 arriba. Firmada = trazo de firma en carbón + nombre y hora; pendiente = recuadro punteado; rechazada = ✕ en rojo. Cada grupo lleva `aria-label` ("1 de 2 firmas").
- **Folio:** franja del color de la copia a la izquierda, folio en mono, título, proveedor, renglones, estado, casillas y total.
- **Renglón del formulario:** producto, cantidad (error junto al campo con `aria-invalid` y `aria-describedby`), precio del catálogo, importe y quitar (44 px). El total se muestra como **estimado**: el definitivo lo calcula el servidor.
- **Botones:** `boton-primario` (carbón), `boton-secundario` (contorno). 44 px; `:active` a 0.97.
- **Rechazo:** el motivo es obligatorio y el error se explica ("lo leerá quien la pidió").

## Estados (en el prototipo)
Carga (esqueletos con `motion-reduce`), vacío con acción, error con "Reintentar", arranque en frío ("Despertando la demo"), éxito (aviso tras enviar, que dice cuántas firmas necesita) y propia ("No puedes aprobar tu propia requisición", botones desactivados).

## Verificado
- axe: 0 violaciones en claro y oscuro, a 1440 y 390 px (incluye contraste de cada texto); sin desplazamiento horizontal.
- Autocrítica: se corrigió la casilla de una rechazada que parecía pendiente, "1 renglones" y el encabezado apretado en celular.
- `ng build` y las 11 pruebas siguen en verde con los tokens nuevos.

## Para la construcción (Etapa 4)
Fuentes autoalojadas con `@fontsource` (gratis, sin depender de Google en producción y compatible con una CSP estricta). Componentes standalone; `hoja`, `boton-*` y `campo` como utilidades de Tailwind.
