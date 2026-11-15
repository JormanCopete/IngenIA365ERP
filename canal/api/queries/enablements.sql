-- Espejo de habilitaciones (R8): lo llena el arrastre de GET /api/canal/v1/tenants/{t}/enablements.

-- name: UpsertEnablement :execrows
-- 1 fila = creada o actualizada; 0 = el cambio es más viejo que el guardado (llegó fuera de orden) y se ignora.
INSERT INTO enablements (
    id, tenant_id, person_public_id, document_type, document_number, status, enabled_at, change_sequence
) VALUES (
    @id, @tenant_id, @person_public_id, @document_type, @document_number, @status, @enabled_at, @change_sequence
)
ON CONFLICT (tenant_id, person_public_id) DO UPDATE SET
    document_type   = EXCLUDED.document_type,
    document_number = EXCLUDED.document_number,
    status          = EXCLUDED.status,
    enabled_at      = EXCLUDED.enabled_at,
    change_sequence = EXCLUDED.change_sequence,
    updated_at      = now()
WHERE EXCLUDED.change_sequence >= enablements.change_sequence;

-- name: DisableEnablement :execrows
-- Inhabilitación que llega con un documento ilegible: se refleja el estado sin tocar el documento guardado.
UPDATE enablements
SET status = 'disabled', change_sequence = @change_sequence, updated_at = now()
WHERE tenant_id = @tenant_id AND person_public_id = @person_public_id AND change_sequence <= @change_sequence;

-- name: DisableEnablementsNotListed :many
-- Resincronización completa: lo que el ERP ya no lista queda inhabilitado (nunca se borra). Las filas con una
-- secuencia posterior a la del listado las escribió un arrastre concurrente y no se tocan.
UPDATE enablements
SET status = 'disabled', updated_at = now()
WHERE tenant_id = @tenant_id
  AND status = 'enabled'
  AND change_sequence <= @up_to::bigint
  AND NOT (person_public_id = ANY (@listed::uuid[]))
RETURNING person_public_id;

-- name: ListEnablementsByTenant :many
SELECT * FROM enablements WHERE tenant_id = @tenant_id ORDER BY person_public_id;
