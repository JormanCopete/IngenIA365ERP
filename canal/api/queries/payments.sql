-- Intentos de pago (US4; data-model.md sec.B). Ninguna consulta toca datos de tarjeta: no existen.

-- name: NextPaymentReferenceSequence :one
SELECT nextval('payment_reference_seq')::bigint AS seq;

-- name: InsertPaymentAttempt :exec
INSERT INTO payment_attempts (
    id, reference, tenant_id, account_id, person_public_id, site_id, destination_public_id, idempotency_key,
    items, total, status, expires_at, buyer_ip, buyer_user_agent, created_at
) VALUES (
    @id, @reference, @tenant_id, @account_id, @person_public_id, @site_id, @destination_public_id, @idempotency_key,
    @items, @total, 'created', @expires_at, @buyer_ip, @buyer_user_agent, @created_at
);

-- name: GetPaymentAttempt :one
SELECT * FROM payment_attempts WHERE id = @id;

-- name: GetPaymentAttemptForUpdate :one
SELECT * FROM payment_attempts WHERE id = @id FOR UPDATE;

-- name: GetPaymentAttemptByIdempotency :one
SELECT * FROM payment_attempts WHERE account_id = @account_id AND idempotency_key = @idempotency_key;

-- name: GetPaymentAttemptByRequest :one
SELECT * FROM payment_attempts WHERE site_id = @site_id AND gateway_request_id = @gateway_request_id;

-- name: ClaimPendingConcept :execrows
-- 0 filas = el concepto ya tiene un intento abierto de la cuenta (FR-041).
INSERT INTO pending_concepts (account_id, tenant_id, concept_key, attempt_id)
VALUES (@account_id, @tenant_id, @concept_key, @attempt_id)
ON CONFLICT DO NOTHING;

-- name: OpenAttemptsForConcepts :many
-- Los intentos abiertos de la cuenta en la cooperativa que tienen alguno de los conceptos.
SELECT DISTINCT a.*
FROM pending_concepts p
JOIN payment_attempts a ON a.id = p.attempt_id
WHERE p.account_id = @account_id AND p.tenant_id = @tenant_id AND p.concept_key = ANY (@concept_keys::text[])
ORDER BY a.created_at;

-- name: OpenConceptKeys :many
-- Los conceptos con intento abierto de la cuenta en la cooperativa, con la referencia (para la lista de conceptos).
SELECT p.concept_key, a.id AS attempt_id, a.reference, a.total, a.status, a.approval, a.created_at
FROM pending_concepts p
JOIN payment_attempts a ON a.id = p.attempt_id
WHERE p.account_id = @account_id AND p.tenant_id = @tenant_id;

-- name: ReleasePendingConcepts :exec
DELETE FROM pending_concepts WHERE attempt_id = @attempt_id;

-- name: SetAttemptSession :exec
UPDATE payment_attempts
SET status = 'pending', gateway_request_id = @gateway_request_id, process_url = @process_url, updated_at = now()
WHERE id = @id AND status = 'created';

-- name: SetAttemptStatus :exec
-- Lo usa la maquina de estados (pagos.Transicion); aqui no se valida la transicion.
UPDATE payment_attempts
SET status = @status, approval = COALESCE(@approval, approval), status_message = @status_message,
    final_at = @final_at, last_checked_at = @checked_at, updated_at = now()
WHERE id = @id;

-- name: TouchAttemptChecked :exec
UPDATE payment_attempts SET last_checked_at = @checked_at, updated_at = now() WHERE id = @id;

-- name: MarkAttemptStaleAlerted :exec
UPDATE payment_attempts SET stale_alerted_at = @at::timestamptz, updated_at = now() WHERE id = @id AND stale_alerted_at IS NULL;

-- name: InsertAttemptEvent :exec
INSERT INTO attempt_events (id, attempt_id, source, gateway_status, status, raw, at)
VALUES (@id, @attempt_id, @source, @gateway_status, @status, @raw, @at);

-- name: ListAttemptEvents :many
SELECT * FROM attempt_events WHERE attempt_id = @attempt_id ORDER BY at, id;

-- name: ListAttemptsToProbe :many
-- Pendientes que tocan consulta (FR-040): los creados despues de @recent_since (las primeras 24 h) si su ultima consulta
-- es anterior a @frequent_before; los demas dentro de la ventana (@window_since, 7 dias) si es anterior a @daily_before.
-- Tambien los que nunca se consultaron. Los mas viejos primero.
SELECT * FROM payment_attempts
WHERE status IN ('created', 'pending')
  AND created_at > sqlc.arg(window_since)::timestamptz
  AND (
        last_checked_at IS NULL
     OR (created_at > sqlc.arg(recent_since)::timestamptz AND last_checked_at <= sqlc.arg(frequent_before)::timestamptz)
     OR (created_at <= sqlc.arg(recent_since)::timestamptz AND last_checked_at <= sqlc.arg(daily_before)::timestamptz)
  )
ORDER BY created_at
LIMIT sqlc.arg(max_rows)::integer;

-- name: ListStaleAttempts :many
-- Pendientes creados antes de @window_since (7 dias) que todavia no se alertaron.
SELECT * FROM payment_attempts
WHERE status IN ('created', 'pending') AND created_at <= sqlc.arg(window_since)::timestamptz AND stale_alerted_at IS NULL
ORDER BY created_at
LIMIT 200;

-- name: LatestLegalAcceptances :many
-- La ultima version aceptada de cada texto por la cuenta en la cooperativa (FR-034).
SELECT kind, max(version)::integer AS version
FROM legal_acceptances
WHERE account_id = @account_id AND tenant_id = @tenant_id
GROUP BY kind;

-- name: CountRecentAuditForSubject :one
-- Repeticiones recientes de una accion sobre un sujeto (alerta de firmas invalidas repetidas).
SELECT count(*) FROM audit_log WHERE action = @action AND subject = @subject AND at > @since::timestamptz;

-- name: SetAttemptErpOutcome :exec
-- Resultado del aviso al ERP (FR-047). Un intento aplicado o en revision no vuelve atras.
UPDATE payment_attempts
SET erp_outcome = @erp_outcome, erp_receipt_public_id = COALESCE(sqlc.narg(erp_receipt_public_id)::uuid, erp_receipt_public_id), updated_at = now()
WHERE id = @id AND erp_outcome NOT IN ('applied', 'in_review');

-- name: ListAccountAttempts :many
-- Historial del asociado en la cooperativa (FR-054): los mas recientes primero.
SELECT * FROM payment_attempts
WHERE account_id = @account_id AND tenant_id = @tenant_id
ORDER BY created_at DESC, id DESC
LIMIT sqlc.arg(max_rows)::integer;
