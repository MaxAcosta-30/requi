# 0001 · Tablas de identidad en scripts SQL, generadas una sola vez

**Contexto:** Requi es Database First: la base es la fuente de verdad y DbUp aplica scripts en orden. Identity, OpenIddict y Data Protection traen sus propias entidades; escribir su SQL a mano es propenso a errores y *scaffoldearlas* las duplicaría.

**Decisión:** `IdentidadDbContext` mapea esas entidades a `snake_case` (tablas `users`, `roles`, `oidc_*`, `data_protection_keys`). Su SQL se generó **una vez** con una fábrica de diseño y una migración temporales (`dotnet ef migrations script`), y se guardó como `database/scripts/0002_identidad.sql` sin `__EFMigrationsHistory` ni la transacción (DbUp abre una por script). La migración temporal se borró. El scaffold sigue con su lista explícita de tablas de negocio.

**Por qué no migraciones de EF para estas tablas:** habría dos mecanismos de esquema conviviendo.
**Por qué no escribirlo a mano:** índices y longitudes de OpenIddict cambian entre versiones.

**Revisar si:** se actualiza OpenIddict o Identity de versión mayor: regenerar el SQL de la misma forma y escribir el **diferencial** como script nuevo (`000N_...`), nunca editar `0002`.
