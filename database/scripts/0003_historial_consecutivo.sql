-- 0003 · El historial necesita un orden estable: dos acciones de la misma transacción
-- (crear y enviar) comparten la hora. Un consecutivo generado por la base lo resuelve.
-- Agregar una columna identity reescribe la tabla; no dispara el trigger de solo lectura.
ALTER TABLE audit_events ADD COLUMN seq bigint GENERATED ALWAYS AS IDENTITY;
ALTER TABLE audit_events ADD CONSTRAINT uq_audit_events_seq UNIQUE (seq);
CREATE INDEX ix_audit_events_entity_seq ON audit_events (entity_type, entity_id, seq);
