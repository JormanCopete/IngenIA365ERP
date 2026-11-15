-- Atencion e indicadores para la cooperativa (feature 021, US12; FR-058 a FR-060). Toda consulta va acotada a la
-- cooperativa: una cuenta solo se ve si su documento esta habilitado (o lo estuvo) en ella (FR-059).

-- name: EnablementOfTenantForDocument :one
SELECT * FROM enablements WHERE tenant_id = @tenant_id AND document_type = @document_type AND document_number = @document_number;

-- name: LastLoginOfAccount :one
SELECT created_at FROM sessions WHERE account_id = @account_id ORDER BY created_at DESC LIMIT 1;

-- name: CountOpenSessionsOfAccount :one
SELECT count(*) FROM sessions WHERE account_id = @account_id AND ended_at IS NULL AND expires_at > @at::timestamptz;

-- name: EndAllAccountSessions :execrows
UPDATE sessions SET ended_at = @at::timestamptz, end_reason = 'revoked' WHERE account_id = @account_id AND ended_at IS NULL;

-- name: CountEnabledOfTenant :one
SELECT count(*) FROM enablements WHERE tenant_id = @tenant_id AND status = 'enabled';

-- name: CountActiveAccountsOfTenant :one
-- Personas habilitadas que ya activaron su cuenta.
SELECT count(*) FROM enablements e
JOIN accounts a ON a.document_type = e.document_type AND a.document_number = e.document_number
WHERE e.tenant_id = @tenant_id AND e.status = 'enabled';

-- name: CountLoginsOfTenant :one
-- Ingresos a la cooperativa: sesiones que la eligieron, por fecha de ingreso.
SELECT count(*) FROM sessions WHERE tenant_id = @tenant_id AND created_at >= @desde::timestamptz AND created_at < @hasta::timestamptz;

-- name: AttemptStatsOfTenant :many
SELECT status, (reversed_at IS NOT NULL)::boolean AS reversed, count(*) AS cantidad, coalesce(sum(total), 0)::bigint AS valor
FROM payment_attempts
WHERE tenant_id = @tenant_id AND created_at >= @desde::timestamptz AND created_at < @hasta::timestamptz
GROUP BY status, (reversed_at IS NOT NULL);

-- name: RejectionReasonsOfTenant :many
SELECT coalesce(nullif(status_message, ''), 'Sin motivo')::text AS motivo, count(*) AS cantidad
FROM payment_attempts
WHERE tenant_id = @tenant_id AND status IN ('rejected', 'failed') AND created_at >= @desde::timestamptz AND created_at < @hasta::timestamptz
GROUP BY 1 ORDER BY 2 DESC, 1 LIMIT 10;
