-- Catálogo de demo SOLO para el e2e (el migrador no siembra catálogo). Idempotente.
INSERT INTO suppliers (name, email) VALUES ('Papelera E2E', 'ventas@papelera-e2e.test')
ON CONFLICT ON CONSTRAINT uq_suppliers_name DO NOTHING;

INSERT INTO products (supplier_id, sku, name, unit, unit_price)
SELECT s.id, v.sku, v.name, 'pieza', v.price
FROM suppliers s,
     (VALUES ('E2E-001', 'Impresora láser E2E', 12000.00),
             ('E2E-002', 'Escritorio ejecutivo E2E', 8500.00)) AS v(sku, name, price)
WHERE s.name = 'Papelera E2E'
ON CONFLICT ON CONSTRAINT uq_products_sku DO NOTHING;
