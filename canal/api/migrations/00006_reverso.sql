-- El reverso de un pago del canal (feature 021, US7; FR-052, FR-053). El intento aprobado guarda cómo se reversó: por la
-- pasarela antes del corte, o como devolución por Tesorería después. reversed_at y reversal_kind van juntos.

-- +goose Up
ALTER TABLE payment_attempts
    ADD COLUMN reversal_kind text CHECK (reversal_kind IN ('gateway', 'treasury')),
    ADD CONSTRAINT ck_payment_attempts_reversal CHECK ((reversed_at IS NULL) = (reversal_kind IS NULL)),
    ADD CONSTRAINT ck_payment_attempts_reversal_approved CHECK (reversed_at IS NULL OR status = 'approved');

-- +goose Down
ALTER TABLE payment_attempts
    DROP CONSTRAINT IF EXISTS ck_payment_attempts_reversal_approved,
    DROP CONSTRAINT IF EXISTS ck_payment_attempts_reversal,
    DROP COLUMN IF EXISTS reversal_kind;
