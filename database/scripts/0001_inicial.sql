-- 0001 · Tablas de negocio.
-- Las columnas *_id que apuntan a usuarios (requester_id, approver_id, actor_id) reciben su FK
-- en el script de identidad (Etapa 2), cuando exista la tabla de usuarios.
-- Los estados se guardan en inglés; la interfaz los traduce con i18n.

CREATE TABLE suppliers (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name        text NOT NULL,
    email       text NOT NULL,
    phone       text,
    is_active   boolean NOT NULL DEFAULT true,
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_suppliers_name UNIQUE (name)
);

CREATE TABLE products (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    supplier_id  uuid NOT NULL REFERENCES suppliers (id),
    sku          text NOT NULL,
    name         text NOT NULL,
    unit         text NOT NULL,
    unit_price   numeric(18,2) NOT NULL CHECK (unit_price > 0),
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL DEFAULT now(),
    updated_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_products_sku UNIQUE (sku)
);
CREATE INDEX ix_products_supplier_id ON products (supplier_id);

CREATE TABLE requisitions (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    number            bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
    requester_id      uuid NOT NULL,
    supplier_id       uuid NOT NULL REFERENCES suppliers (id),
    status            text NOT NULL DEFAULT 'Draft'
                      CHECK (status IN ('Draft', 'Submitted', 'Approved', 'OrderGenerated', 'Rejected', 'Cancelled')),
    total             numeric(18,2) NOT NULL DEFAULT 0 CHECK (total >= 0),
    notes             text,
    rejection_reason  text,
    created_at        timestamptz NOT NULL DEFAULT now(),
    submitted_at      timestamptz,
    updated_at        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_requisitions_rejection_reason
        CHECK (status <> 'Rejected' OR length(trim(rejection_reason)) > 0)
);
CREATE INDEX ix_requisitions_requester_id ON requisitions (requester_id);
CREATE INDEX ix_requisitions_status ON requisitions (status);

CREATE TABLE requisition_lines (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    requisition_id  uuid NOT NULL REFERENCES requisitions (id) ON DELETE CASCADE,
    product_id      uuid NOT NULL REFERENCES products (id),
    quantity        integer NOT NULL CHECK (quantity > 0),
    unit_price      numeric(18,2) NOT NULL CHECK (unit_price > 0),
    CONSTRAINT uq_requisition_lines_product UNIQUE (requisition_id, product_id)
);

-- Una fila por aprobador y requisición: dos aprobaciones deben ser de personas distintas.
CREATE TABLE approvals (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    requisition_id  uuid NOT NULL REFERENCES requisitions (id),
    approver_id     uuid NOT NULL,
    decision        text NOT NULL CHECK (decision IN ('Approved', 'Rejected')),
    reason          text,
    decided_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_approvals_approver UNIQUE (requisition_id, approver_id),
    CONSTRAINT ck_approvals_reason CHECK (decision <> 'Rejected' OR length(trim(reason)) > 0)
);

-- uq_purchase_orders_requisition hace idempotente la creación de la orden (caso 5).
CREATE TABLE purchase_orders (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    number          bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
    requisition_id  uuid NOT NULL REFERENCES requisitions (id),
    supplier_id     uuid NOT NULL REFERENCES suppliers (id),
    total           numeric(18,2) NOT NULL CHECK (total > 0),
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_purchase_orders_requisition UNIQUE (requisition_id)
);

CREATE TABLE purchase_order_lines (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    purchase_order_id  uuid NOT NULL REFERENCES purchase_orders (id) ON DELETE CASCADE,
    product_id         uuid NOT NULL REFERENCES products (id),
    quantity           integer NOT NULL CHECK (quantity > 0),
    unit_price         numeric(18,2) NOT NULL CHECK (unit_price > 0)
);

-- uq_supplier_notifications_order hace idempotente el aviso (caso 6).
CREATE TABLE supplier_notifications (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    supplier_id        uuid NOT NULL REFERENCES suppliers (id),
    purchase_order_id  uuid NOT NULL REFERENCES purchase_orders (id),
    subject            text NOT NULL,
    body               text NOT NULL,
    created_at         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_supplier_notifications_order UNIQUE (purchase_order_id)
);

CREATE TABLE outbox_messages (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    type             text NOT NULL,
    payload          jsonb NOT NULL,
    occurred_at      timestamptz NOT NULL DEFAULT now(),
    status           text NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Processed', 'Failed')),
    attempts         integer NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    next_attempt_at  timestamptz NOT NULL DEFAULT now(),
    last_error       text,
    processed_at     timestamptz
);
-- El procesador solo busca pendientes listos para reintento.
CREATE INDEX ix_outbox_messages_pending ON outbox_messages (next_attempt_at) WHERE status = 'Pending';

CREATE TABLE audit_events (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at  timestamptz NOT NULL DEFAULT now(),
    actor_id     uuid NOT NULL,
    action       text NOT NULL,
    entity_type  text NOT NULL,
    entity_id    uuid NOT NULL,
    data         jsonb
);
CREATE INDEX ix_audit_events_entity ON audit_events (entity_type, entity_id, occurred_at);

-- El historial es de solo inserción también en la base, no solo en la API (caso 7).
CREATE FUNCTION audit_events_read_only() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_events no admite %', TG_OP;
END;
$$;

CREATE TRIGGER trg_audit_events_read_only
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW EXECUTE FUNCTION audit_events_read_only();
