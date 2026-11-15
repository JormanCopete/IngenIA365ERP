-- Avisos al asociado (feature 021, US10; FR-012, FR-056; research R15): por correo y por notificación (FCM en la app, Web Push en la
-- PWA instalada). La persona apaga los que quiera salvo los de seguridad, que no tienen preferencia: siempre salen.

-- +goose Up

-- Días de anticipación del aviso de cuota próxima, que el directorio trae del ERP (0 = sin aviso).
ALTER TABLE tenants
    ADD COLUMN due_reminder_days integer NOT NULL DEFAULT 0 CHECK (due_reminder_days BETWEEN 0 AND 15);

-- Sin fila = encendido. Sólo los avisos que se pueden apagar.
CREATE TABLE notification_preferences (
    account_id  uuid        NOT NULL REFERENCES accounts (id),
    kind        text        NOT NULL CHECK (kind IN ('due_reminder', 'payment_applied', 'payment_rejected', 'pending_resolved')),
    by_email    boolean     NOT NULL,
    by_push     boolean     NOT NULL,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (account_id, kind)
);

-- Una suscripción por navegador o teléfono. FCM: el token en endpoint. Web Push: el endpoint del navegador y sus llaves.
CREATE TABLE push_subscriptions (
    id            uuid        PRIMARY KEY,
    account_id    uuid        NOT NULL REFERENCES accounts (id),
    kind          text        NOT NULL CHECK (kind IN ('fcm', 'webpush')),
    endpoint      text        NOT NULL CHECK (length(endpoint) BETWEEN 1 AND 2048),
    p256dh        text        CHECK (length(p256dh) <= 200),
    auth          text        CHECK (length(auth) <= 100),
    created_at    timestamptz NOT NULL DEFAULT now(),
    last_used_at  timestamptz,
    revoked_at    timestamptz,
    CONSTRAINT ck_push_subscriptions_llaves CHECK (kind = 'fcm' OR (p256dh IS NOT NULL AND auth IS NOT NULL))
);

CREATE UNIQUE INDEX uk_push_subscriptions_endpoint ON push_subscriptions (endpoint) WHERE revoked_at IS NULL;
CREATE INDEX ix_push_subscriptions_account ON push_subscriptions (account_id) WHERE revoked_at IS NULL;

-- +goose Down
DROP TABLE IF EXISTS push_subscriptions;
DROP TABLE IF EXISTS notification_preferences;
ALTER TABLE tenants DROP COLUMN IF EXISTS due_reminder_days;
