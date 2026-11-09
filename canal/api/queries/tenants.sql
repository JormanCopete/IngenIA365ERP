-- Directorio de cooperativas: lo llena el arrastre de GET /api/canal/v1/tenants (trabajo sync_tenants).

-- name: UpsertTenant :one
-- payment_session_minutes en 0 toma el valor por defecto (15).
INSERT INTO tenants (
    id, tenant_public_id, channel_code, display_name, brand, support,
    inactivity_minutes, sms_enabled, payment_session_minutes, require_otp_to_pay, is_enabled, last_sync_at
) VALUES (
    @id, @tenant_public_id, @channel_code, @display_name, @brand, @support,
    @inactivity_minutes, @sms_enabled, COALESCE(NULLIF(sqlc.arg(payment_session_minutes)::integer, 0), 15), @require_otp_to_pay, true, @last_sync_at
)
ON CONFLICT (tenant_public_id) DO UPDATE SET
    channel_code       = EXCLUDED.channel_code,
    display_name       = EXCLUDED.display_name,
    brand              = EXCLUDED.brand,
    support            = EXCLUDED.support,
    inactivity_minutes = EXCLUDED.inactivity_minutes,
    sms_enabled        = EXCLUDED.sms_enabled,
    payment_session_minutes = EXCLUDED.payment_session_minutes,
    require_otp_to_pay = EXCLUDED.require_otp_to_pay,
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

-- name: ListEnabledTenants :many
-- Las que tienen el canal encendido: el arrastre de habilitaciones las recorre.
SELECT * FROM tenants WHERE is_enabled ORDER BY id;

-- name: AdvanceEnablementsCursor :exec
-- El cursor sólo avanza.
UPDATE tenants
SET enablements_cursor = @cursor::bigint, updated_at = now()
WHERE id = @id AND enablements_cursor < @cursor::bigint;

-- name: ClaimEnablementsReconcile :execrows
-- 1 fila = le toca conciliar ahora; 0 = otra réplica (o una corrida reciente) ya lo hizo dentro del intervalo.
UPDATE tenants
SET last_reconcile_at = @now::timestamptz
WHERE id = @id AND (last_reconcile_at IS NULL OR last_reconcile_at <= @due_before::timestamptz);
