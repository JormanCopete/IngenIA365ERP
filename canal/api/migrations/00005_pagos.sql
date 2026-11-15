-- Intentos de pago en línea (feature 021, US4; data-model.md §B «payment_attempts», «attempt_events»; research R11, R12).
-- El canal no guarda saldos ni movimientos (FR-030): un intento es lo que el asociado pidió pagar y lo que respondió la
-- pasarela. Nunca hay datos de tarjeta (FR-042). Nada se borra: el estado final no cambia salvo vencido → aprobado y la
-- marca del reverso.

-- +goose Up

-- La configuración de pago de la cooperativa que el directorio trae del ERP (FR-008, FR-009).
ALTER TABLE tenants
    ADD COLUMN payment_session_minutes integer NOT NULL DEFAULT 15 CHECK (payment_session_minutes BETWEEN 10 AND 30),
    ADD COLUMN require_otp_to_pay      boolean NOT NULL DEFAULT false;

-- El código para confirmar un pago (FR-009) es otro propósito de los desafíos.
ALTER TABLE otp_challenges DROP CONSTRAINT otp_challenges_purpose_check;
ALTER TABLE otp_challenges ADD CONSTRAINT otp_challenges_purpose_check
    CHECK (purpose IN ('activation', 'new_device', 'recovery', 'contact_change', 'payment'));

-- Secuencia de las referencias (R12): la parte base 36 de 8 caracteres. Nunca se reinicia.
CREATE SEQUENCE payment_reference_seq AS bigint START WITH 1 MAXVALUE 2821109907455 NO CYCLE;

CREATE TABLE payment_attempts (
    id                     uuid        PRIMARY KEY,
    -- {código 3}{AAMMDD}{secuencia base 36 (8)}{dígito de control}: 18 caracteres, única y nunca reutilizada (FR-035).
    reference              text        NOT NULL CHECK (reference ~ '^[A-Z]{3}[0-9]{6}[0-9A-Z]{9}$'),
    tenant_id              uuid        NOT NULL REFERENCES tenants (id),
    account_id             uuid        NOT NULL REFERENCES accounts (id),
    person_public_id       uuid        NOT NULL,
    site_id                uuid        NOT NULL REFERENCES gateway_sites (id),
    destination_public_id  uuid        NOT NULL,
    -- Llave del cliente: confirmar dos veces devuelve el mismo intento (FR-036).
    idempotency_key        text        NOT NULL CHECK (length(idempotency_key) BETWEEN 8 AND 100),
    -- [{conceptKey, productPublicId, kind, modality, amount, prepaymentChoice, description, tax}]
    items                  jsonb       NOT NULL,
    total                  bigint      NOT NULL CHECK (total > 0),
    currency               text        NOT NULL DEFAULT 'COP' CHECK (currency = 'COP'),
    status                 text        NOT NULL CHECK (status IN ('created', 'pending', 'approved', 'rejected', 'failed', 'expired')),
    gateway_request_id     bigint,
    process_url            text        CHECK (length(process_url) <= 1000),
    expires_at             timestamptz NOT NULL,
    -- {internalReference, authorization, receipt, franchise, paymentMethodName, issuerName, approvedAt}; sin tarjeta.
    approval               jsonb,
    -- Lo que la pasarela dijo del rechazo o la falla, para mostrarlo.
    status_message         text        CHECK (length(status_message) <= 500),
    buyer_ip               text        CHECK (length(buyer_ip) <= 64),
    buyer_user_agent       text        CHECK (length(buyer_user_agent) <= 512),
    erp_outcome            text        NOT NULL DEFAULT 'not_sent'
                                       CHECK (erp_outcome IN ('not_sent', 'sending', 'applied', 'in_review', 'failed_retrying')),
    erp_receipt_public_id  uuid,
    reversed_at            timestamptz,
    -- Consulta periódica (FR-040): la última consulta a la pasarela y la alerta de un pendiente sin resolver.
    last_checked_at        timestamptz,
    stale_alerted_at       timestamptz,
    final_at               timestamptz,
    created_at             timestamptz NOT NULL,
    updated_at             timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_payment_attempts_reference UNIQUE (reference),
    CONSTRAINT uk_payment_attempts_idempotency UNIQUE (account_id, idempotency_key),
    CONSTRAINT ck_payment_attempts_final CHECK ((status IN ('created', 'pending')) = (final_at IS NULL))
);

CREATE UNIQUE INDEX uk_payment_attempts_request ON payment_attempts (site_id, gateway_request_id) WHERE gateway_request_id IS NOT NULL;
CREATE INDEX ix_payment_attempts_account ON payment_attempts (account_id, tenant_id, created_at DESC);
-- La consulta periódica recorre sólo los pendientes.
CREATE INDEX ix_payment_attempts_pending ON payment_attempts (created_at) WHERE status IN ('created', 'pending');

-- A lo sumo un intento abierto por cuenta y concepto (FR-041): una fila por concepto mientras el intento está
-- 'created' o 'pending'; se retira cuando el intento llega a un estado final.
CREATE TABLE pending_concepts (
    account_id   uuid NOT NULL REFERENCES accounts (id),
    tenant_id    uuid NOT NULL REFERENCES tenants (id),
    concept_key  text NOT NULL CHECK (length(concept_key) BETWEEN 1 AND 100),
    attempt_id   uuid NOT NULL REFERENCES payment_attempts (id),
    PRIMARY KEY (account_id, tenant_id, concept_key)
);

CREATE INDEX ix_pending_concepts_attempt ON pending_concepts (attempt_id);

-- Cada cambio de estado y cada consulta que trajo novedad, con su fuente (FR-043). Sólo se anexa.
CREATE TABLE attempt_events (
    id              uuid        PRIMARY KEY,
    attempt_id      uuid        NOT NULL REFERENCES payment_attempts (id),
    source          text        NOT NULL CHECK (source IN ('create', 'return', 'webhook', 'probe', 'reconciliation', 'reversal')),
    gateway_status  text        CHECK (length(gateway_status) <= 40),
    status          text        NOT NULL,
    -- La respuesta de la pasarela sin datos de tarjeta.
    raw             jsonb       NOT NULL DEFAULT '{}'::jsonb,
    at              timestamptz NOT NULL
);

CREATE INDEX ix_attempt_events_attempt ON attempt_events (attempt_id, at);

-- +goose StatementBegin
CREATE FUNCTION attempt_events_solo_anexar() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'attempt_events es sólo de anexar: % no está permitido', TG_OP
        USING ERRCODE = 'insufficient_privilege';
END;
$$;
-- +goose StatementEnd

CREATE TRIGGER attempt_events_sin_cambios
    BEFORE UPDATE OR DELETE ON attempt_events
    FOR EACH ROW EXECUTE FUNCTION attempt_events_solo_anexar();

-- +goose Down
DROP TRIGGER IF EXISTS attempt_events_sin_cambios ON attempt_events;
DROP FUNCTION IF EXISTS attempt_events_solo_anexar();
DROP TABLE IF EXISTS attempt_events;
DROP TABLE IF EXISTS pending_concepts;
DROP TABLE IF EXISTS payment_attempts;
DROP SEQUENCE IF EXISTS payment_reference_seq;
ALTER TABLE otp_challenges DROP CONSTRAINT otp_challenges_purpose_check;
ALTER TABLE otp_challenges ADD CONSTRAINT otp_challenges_purpose_check
    CHECK (purpose IN ('activation', 'new_device', 'recovery', 'contact_change'));
ALTER TABLE tenants DROP COLUMN IF EXISTS require_otp_to_pay, DROP COLUMN IF EXISTS payment_session_minutes;
