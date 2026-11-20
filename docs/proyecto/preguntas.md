# Preguntas de entrevista

## Caso 1 · Identidad
1. ¿Qué problema resuelve PKCE en un cliente público como Angular? Recorre `code_verifier` y `code_challenge`.
2. ¿Qué pasa exactamente cuando alguien reutiliza un refresh token ya usado? ¿Por qué hizo falta `EnableTokenEntryValidation` para que el access token también muera?
3. ¿Por qué el login lo sirve la API y no Angular, y por qué la cookie es `SameSite=Lax`?

## Reglas de aprobación (caso 4)
1. ¿Por qué el total se recalcula en el servidor aunque Angular ya lo muestre?
2. Una requisición de $30,000 tiene una aprobación y el segundo aprobador la rechaza. ¿En qué estado queda y por qué?
3. ¿Por qué las reglas viven en una clase `partial` y no en el archivo de la entidad?

## Outbox (casos 5 y 6)
1. ¿Qué garantiza que la orden de compra no se pierda si la API se apaga justo después de aprobar?
2. ¿Qué hace `FOR UPDATE SKIP LOCKED` y qué pasaría sin `SKIP LOCKED` con dos instancias?
3. El mismo mensaje se procesa dos veces. ¿Por qué no se duplica la orden? Nombra las dos defensas.
