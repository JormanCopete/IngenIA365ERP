-- Directorio de cooperativas: lo llena el arrastre de GET /api/canal/v1/tenants (trabajo sync_tenants).

-- name: UpsertTenant :one
INSERT INTO tenants (
    id, tenant_public_id, channel_code, display_name, brand, support,
    inactivity_minutes, sms_enabled, is_enabled, last_sync_at
) VALUES (
    @id, @tenant_public_id, @channel_code, @display_name, @brand, @support,
    @inactivity_minutes, @sms_enabled, true, @last_sync_at
)
ON CONFLICT (tenant_public_id) DO UPDATE SET
    channel_code       = EXCLUDED.channel_code,
    display_name       = EXCLUDED.display_name,
    brand              = EXCLUDED.brand,
    support            = EXCLUDED.support,
    inactivity_minutes = EXCLUDED.inactivity_minutes,
    sms_enabled        = EXCLUDED.sms_enabled,
    is_enabled         = true,
    last_sync_at       = EXCLUDED.last_sync_at,
    updated_at         = now()
RETURNING *;

-- name: DisableTenantsNotListed :many
-- Las que el ERP ya no lista (canal apagado) quedan inhabilitadas; la fila no se borra.
UPDATE tenants
SET is_enabled = false, last_sync_at = @last_sync_at, updated_at = now()
WHERE is_enabled AND NOT (tenant_public_id = ANY (@listed::uuid[]))
RETURNING tenant_public_id;

-- name: ListTenants :many
SELECT * FROM tenants ORDER BY display_name, id;

-- name: GetTenantByPublicID :one
SELECT * FROM tenants WHERE tenant_public_id = @tenant_public_id;
