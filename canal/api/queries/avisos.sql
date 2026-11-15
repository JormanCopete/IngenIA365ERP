-- Avisos al asociado (US10): preferencias, suscripciones de push y a quien avisar.

-- name: ListNotificationPreferences :many
SELECT * FROM notification_preferences WHERE account_id = @account_id;

-- name: UpsertNotificationPreference :exec
INSERT INTO notification_preferences (account_id, kind, by_email, by_push, updated_at)
VALUES (@account_id, @kind, @by_email, @by_push, now())
ON CONFLICT (account_id, kind) DO UPDATE SET by_email = EXCLUDED.by_email, by_push = EXCLUDED.by_push, updated_at = now();

-- name: UpsertPushSubscription :one
-- El mismo endpoint vuelve a la cuenta que lo registra (un telefono que cambia de dueno no sigue avisando al anterior).
INSERT INTO push_subscriptions (id, account_id, kind, endpoint, p256dh, auth, created_at)
VALUES (@id, @account_id, @kind, @endpoint, @p256dh, @auth, now())
ON CONFLICT (endpoint) WHERE revoked_at IS NULL DO UPDATE SET
    account_id = EXCLUDED.account_id, kind = EXCLUDED.kind, p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth
RETURNING *;

-- name: ListPushSubscriptions :many
SELECT * FROM push_subscriptions WHERE account_id = @account_id AND revoked_at IS NULL ORDER BY created_at;

-- name: RevokePushSubscription :exec
UPDATE push_subscriptions SET revoked_at = now() WHERE id = @id AND revoked_at IS NULL;

-- name: TouchPushSubscription :exec
UPDATE push_subscriptions SET last_used_at = now() WHERE id = @id;

-- name: AccountOfTenantPerson :one
-- La cuenta del canal de una persona habilitada en la cooperativa (para el aviso de cuota proxima).
SELECT a.* FROM enablements e
JOIN accounts a ON a.document_type = e.document_type AND a.document_number = e.document_number
WHERE e.tenant_id = @tenant_id AND e.person_public_id = @person_public_id AND e.status = 'enabled';

-- name: ListTenantsWithDueReminders :many
SELECT * FROM tenants WHERE is_enabled AND due_reminder_days > 0 ORDER BY id;
