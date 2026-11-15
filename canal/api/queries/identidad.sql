-- Identidad del canal (US2): cuentas, aceptaciones, dispositivos, sesiones, códigos, freno de ingresos y passkeys.

-- name: ValidatingEnablement :one
-- La entidad validadora (D5, FR-017): la habilitación 'enabled' de menor enabled_at del documento, en una cooperativa con
-- el canal encendido.
SELECT e.tenant_id, e.person_public_id, e.enabled_at, t.tenant_public_id, t.display_name, t.support, t.sms_enabled
FROM enablements e
JOIN tenants t ON t.id = e.tenant_id
WHERE e.document_type = @document_type AND e.document_number = @document_number
  AND e.status = 'enabled' AND t.is_enabled
ORDER BY e.enabled_at, e.tenant_id
LIMIT 1;

-- name: EnabledTenantsForDocument :many
-- Las cooperativas donde el documento está habilitado y el canal encendido (FR-025), por nombre.
SELECT t.id, t.tenant_public_id, t.display_name, t.brand, t.support, t.inactivity_minutes, t.sms_enabled, e.person_public_id
FROM enablements e
JOIN tenants t ON t.id = e.tenant_id
WHERE e.document_type = @document_type AND e.document_number = @document_number
  AND e.status = 'enabled' AND t.is_enabled
ORDER BY t.display_name, t.id;

-- name: EnablementForSession :one
-- La habilitación vigente de la cuenta en la cooperativa de la sesión (acotamiento, FR-026).
SELECT e.person_public_id, t.tenant_public_id, t.inactivity_minutes
FROM enablements e
JOIN tenants t ON t.id = e.tenant_id
WHERE e.tenant_id = @tenant_id AND e.document_type = @document_type AND e.document_number = @document_number
  AND e.status = 'enabled' AND t.is_enabled;

-- name: GetTenantByID :one
SELECT * FROM tenants WHERE id = @id;

-- name: GetAccountByDocument :one
SELECT * FROM accounts WHERE document_type = @document_type AND document_number = @document_number;

-- name: GetAccount :one
SELECT * FROM accounts WHERE id = @id;

-- name: InsertAccount :exec
INSERT INTO accounts (
    id, document_type, document_number, first_names, last_names, email, mobile, password_hash, status, activated_at,
    validating_tenant_id, password_changed_at
) VALUES (
    @id, @document_type, @document_number, @first_names, @last_names, @email, @mobile, @password_hash, 'active', @activated_at,
    @validating_tenant_id, @activated_at
);

-- name: UpdateAccountPassword :exec
UPDATE accounts SET password_hash = @password_hash, password_changed_at = @changed_at, updated_at = now() WHERE id = @id;

-- name: UpdateAccountEmail :exec
UPDATE accounts SET email = @email, updated_at = now() WHERE id = @id;

-- name: UpdateAccountMobile :exec
UPDATE accounts SET mobile = @mobile, updated_at = now() WHERE id = @id;

-- name: InsertLegalAcceptance :exec
INSERT INTO legal_acceptances (id, account_id, tenant_id, kind, version, accepted_at, ip, user_agent)
VALUES (@id, @account_id, @tenant_id, @kind, @version, @accepted_at, @ip, @user_agent);

-- name: InsertDevice :exec
INSERT INTO devices (id, account_id, kind, label, credential_hash, webauthn_id, webauthn_credential, last_seen_at)
VALUES (@id, @account_id, @kind, @label, @credential_hash, @webauthn_id, @webauthn_credential, @last_seen_at);

-- name: DeviceByCredential :one
SELECT * FROM devices WHERE credential_hash = @credential_hash AND revoked_at IS NULL;

-- name: DeviceByWebauthnID :one
SELECT * FROM devices WHERE webauthn_id = @webauthn_id AND revoked_at IS NULL;

-- name: ListDevices :many
SELECT * FROM devices WHERE account_id = @account_id AND revoked_at IS NULL ORDER BY last_seen_at DESC;

-- name: ListWebauthnDevices :many
SELECT * FROM devices WHERE account_id = @account_id AND revoked_at IS NULL AND webauthn_id IS NOT NULL;

-- name: TouchDevice :exec
UPDATE devices SET last_seen_at = @at::timestamptz WHERE id = @id;

-- name: SetDeviceCredential :exec
UPDATE devices SET credential_hash = @credential_hash, last_seen_at = @at::timestamptz WHERE id = @id AND account_id = @account_id;

-- name: UpdateWebauthnCredential :exec
UPDATE devices SET webauthn_credential = @webauthn_credential, last_seen_at = @at::timestamptz WHERE id = @id;

-- name: RevokeDevice :execrows
UPDATE devices SET revoked_at = @at::timestamptz WHERE id = @id AND account_id = @account_id AND revoked_at IS NULL;

-- name: InsertSession :exec
INSERT INTO sessions (id, account_id, device_id, token_hash, tenant_id, inactivity_minutes, created_at, last_activity_at, expires_at, ip, user_agent)
VALUES (@id, @account_id, @device_id, @token_hash, NULL, @inactivity_minutes, @created_at, @created_at, @expires_at, @ip, @user_agent);

-- name: SessionByToken :one
SELECT s.*, a.document_type, a.document_number, a.status AS account_status
FROM sessions s
JOIN accounts a ON a.id = s.account_id
WHERE s.token_hash = @token_hash;

-- name: TouchSession :exec
UPDATE sessions SET last_activity_at = @at::timestamptz WHERE id = @id AND ended_at IS NULL;

-- name: SetSessionTenant :exec
UPDATE sessions SET tenant_id = @tenant_id, inactivity_minutes = @inactivity_minutes, last_activity_at = @at::timestamptz
WHERE id = @id AND ended_at IS NULL;

-- name: ClearSessionTenant :exec
UPDATE sessions SET tenant_id = NULL, inactivity_minutes = 5 WHERE id = @id;

-- name: EndSession :execrows
UPDATE sessions SET ended_at = @at::timestamptz, end_reason = @end_reason WHERE id = @id AND ended_at IS NULL;

-- name: EndAccountSession :execrows
UPDATE sessions SET ended_at = @at::timestamptz, end_reason = @end_reason WHERE id = @id AND account_id = @account_id AND ended_at IS NULL;

-- name: EndOtherSessions :execrows
UPDATE sessions SET ended_at = @at::timestamptz, end_reason = @end_reason
WHERE account_id = @account_id AND ended_at IS NULL AND id <> @keep_id;

-- name: EndDeviceSessions :execrows
UPDATE sessions SET ended_at = @at::timestamptz, end_reason = 'revoked'
WHERE account_id = @account_id AND device_id = @device_id AND ended_at IS NULL;

-- name: ListOpenSessions :many
SELECT * FROM sessions
WHERE account_id = @account_id AND ended_at IS NULL AND expires_at > @at::timestamptz
ORDER BY last_activity_at DESC;

-- name: InsertChallenge :exec
INSERT INTO otp_challenges (
    id, purpose, account_id, document_key, document_type, document_number, tenant_id, person_public_id, decoy,
    contact_field, contact_value, created_at, expires_at, ip
) VALUES (
    @id, @purpose, @account_id, @document_key, @document_type, @document_number, @tenant_id, @person_public_id, @decoy,
    @contact_field, @contact_value, @created_at, @expires_at, @ip
);

-- name: GetChallenge :one
SELECT * FROM otp_challenges WHERE id = @id;

-- name: GetChallengeForUpdate :one
SELECT * FROM otp_challenges WHERE id = @id FOR UPDATE;

-- name: SetChallengeCode :exec
UPDATE otp_challenges
SET channel = @channel, destination_hash = @destination_hash, code_hash = @code_hash, sends = sends + 1, expires_at = @expires_at
WHERE id = @id;

-- name: FailChallengeAttempt :exec
UPDATE otp_challenges SET attempts = attempts + 1 WHERE id = @id;

-- name: ConsumeChallenge :exec
UPDATE otp_challenges
SET consumed_at = @at::timestamptz, attempts = attempts + 1, grant_hash = @grant_hash, grant_expires_at = @grant_expires_at
WHERE id = @id;

-- name: ChallengeByGrant :one
SELECT * FROM otp_challenges WHERE grant_hash = @grant_hash FOR UPDATE;

-- name: UseGrant :exec
UPDATE otp_challenges SET grant_used_at = @at::timestamptz WHERE id = @id;

-- name: CountChallengesByDocument :one
SELECT count(*) FROM otp_challenges WHERE document_key = @document_key AND created_at > @since;

-- name: CountChallengesByIP :one
SELECT count(*) FROM otp_challenges WHERE ip = @ip AND created_at > @since;

-- name: CountSmsByTenant :one
-- Uso de SMS por cooperativa (FR-011a), para facturárselo.
SELECT coalesce(sum(sends), 0)::bigint FROM otp_challenges
WHERE tenant_id = @tenant_id AND channel = 'sms' AND created_at >= @desde AND created_at < @hasta;

-- name: GetThrottle :one
SELECT * FROM login_throttle WHERE document_key = @document_key;

-- name: RecordLoginFailure :one
-- Suma un fallo; la ventana se reinicia si el primer fallo es más viejo que @window_start.
INSERT INTO login_throttle (document_key, failures, first_failure_at, updated_at)
VALUES (@document_key, 1, @at::timestamptz, @at::timestamptz)
ON CONFLICT (document_key) DO UPDATE SET
    failures         = CASE WHEN login_throttle.first_failure_at < @window_start THEN 1 ELSE login_throttle.failures + 1 END,
    first_failure_at = CASE WHEN login_throttle.first_failure_at < @window_start THEN @at::timestamptz ELSE login_throttle.first_failure_at END,
    updated_at       = @at::timestamptz
RETURNING *;

-- name: LockThrottle :exec
UPDATE login_throttle SET locked_until = @locked_until::timestamptz, failures = 0, updated_at = now() WHERE document_key = @document_key;

-- name: ClearThrottle :exec
DELETE FROM login_throttle WHERE document_key = @document_key;

-- name: InsertCeremony :exec
INSERT INTO webauthn_ceremonies (id, kind, account_id, session_data, expires_at) VALUES (@id, @kind, @account_id, @session_data, @expires_at);

-- name: TakeCeremony :one
UPDATE webauthn_ceremonies SET used_at = @at::timestamptz
WHERE id = @id AND kind = @kind AND used_at IS NULL AND expires_at > @at::timestamptz
RETURNING *;
