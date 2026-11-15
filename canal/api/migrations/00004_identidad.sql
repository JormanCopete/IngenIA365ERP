-- Identidad del canal (feature 021, US2; data-model.md §B; research R13): cuentas, aceptaciones de textos, dispositivos,
-- sesiones, códigos de un solo uso, ceremonias WebAuthn y el freno de ingresos por documento.
-- Nada se borra en flujo de usuario: un dispositivo se revoca, una sesión se termina, un código se consume o vence.

-- +goose Up
CREATE TABLE accounts (
    id                    uuid        PRIMARY KEY,
    -- Único en toda la plataforma (FR-015): una persona, una cuenta, aunque esté en varias cooperativas.
    document_type         text        NOT NULL CHECK (document_type IN ('CC', 'CE', 'PPT', 'PEP', 'TI', 'PA', 'RC', 'NIT')),
    document_number       text        NOT NULL CHECK (document_number ~ '^[A-Z0-9]{1,20}$'),
    -- Los de la entidad validadora al activar.
    first_names           text        NOT NULL CHECK (length(first_names) BETWEEN 1 AND 300),
    last_names            text        NOT NULL DEFAULT '' CHECK (length(last_names) <= 300),
    -- Verificados con un código; nulos hasta verificar.
    email                 text        CHECK (length(email) <= 254),
    mobile                text        CHECK (mobile ~ '^3[0-9]{9}$'),
    password_hash         text        NOT NULL CHECK (length(password_hash) BETWEEN 50 AND 100),
    -- Sin estado «cerrada» (aclaración 5). El bloqueo por fallos vive en login_throttle, igual exista o no la cuenta.
    status                text        NOT NULL CHECK (status IN ('active', 'locked')),
    activated_at          timestamptz NOT NULL,
    validating_tenant_id  uuid        NOT NULL REFERENCES tenants (id),
    password_changed_at   timestamptz NOT NULL,
    created_at            timestamptz NOT NULL DEFAULT now(),
    updated_at            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_accounts_document UNIQUE (document_type, document_number)
);

ALTER TABLE audit_log ADD CONSTRAINT fk_audit_log_account FOREIGN KEY (account_id) REFERENCES accounts (id);

-- Sólo de anexar (FR-020, FR-034): quién aceptó qué versión, cuándo, desde dónde.
CREATE TABLE legal_acceptances (
    id           uuid        PRIMARY KEY,
    account_id   uuid        NOT NULL REFERENCES accounts (id),
    tenant_id    uuid        NOT NULL REFERENCES tenants (id),
    kind         text        NOT NULL CHECK (kind IN ('Terms', 'DataPolicy', 'GatewayTerms')),
    version      integer     NOT NULL CHECK (version > 0),
    accepted_at  timestamptz NOT NULL,
    ip           text        CHECK (length(ip) <= 64),
    user_agent   text        CHECK (length(user_agent) <= 512)
);

CREATE INDEX ix_legal_acceptances_account ON legal_acceptances (account_id, tenant_id, kind);

-- +goose StatementBegin
CREATE FUNCTION legal_acceptances_solo_anexar() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'legal_acceptances es sólo de anexar: % no está permitido', TG_OP
        USING ERRCODE = 'insufficient_privilege';
END;
$$;
-- +goose StatementEnd

CREATE TRIGGER legal_acceptances_sin_cambios
    BEFORE UPDATE OR DELETE ON legal_acceptances
    FOR EACH ROW EXECUTE FUNCTION legal_acceptances_solo_anexar();

CREATE TABLE devices (
    id                   uuid        PRIMARY KEY,
    account_id           uuid        NOT NULL REFERENCES accounts (id),
    kind                 text        NOT NULL CHECK (kind IN ('browser', 'android', 'ios')),
    label                text        NOT NULL CHECK (length(label) BETWEEN 1 AND 120),
    -- SHA-256 de la credencial de dispositivo de confianza (256 bits, R13). Nula = un dispositivo sólo con passkey.
    credential_hash      bytea       CHECK (length(credential_hash) = 32),
    -- Credencial WebAuthn (PWA): id y el JSON que guarda go-webauthn.
    webauthn_id          bytea,
    webauthn_credential  jsonb,
    biometric_enabled    boolean     NOT NULL DEFAULT false,
    push_token           text        CHECK (length(push_token) <= 4096),
    created_at           timestamptz NOT NULL DEFAULT now(),
    last_seen_at         timestamptz NOT NULL,
    revoked_at           timestamptz
);

CREATE INDEX ix_devices_account ON devices (account_id) WHERE revoked_at IS NULL;
CREATE UNIQUE INDEX uk_devices_credential ON devices (credential_hash) WHERE credential_hash IS NOT NULL;
CREATE UNIQUE INDEX uk_devices_webauthn ON devices (webauthn_id) WHERE webauthn_id IS NOT NULL;

CREATE TABLE sessions (
    id                  uuid        PRIMARY KEY,
    account_id          uuid        NOT NULL REFERENCES accounts (id),
    device_id           uuid        REFERENCES devices (id),
    -- SHA-256 del token opaco; el token sólo existe en la cookie o en el almacén seguro de la app.
    token_hash          bytea       NOT NULL CHECK (length(token_hash) = 32),
    -- La cooperativa elegida; nula antes del selector (FR-025).
    tenant_id           uuid        REFERENCES tenants (id),
    -- 5 por defecto; la cooperativa elegida puede subirla hasta 15 (FR-027).
    inactivity_minutes  integer     NOT NULL CHECK (inactivity_minutes BETWEEN 1 AND 15),
    created_at          timestamptz NOT NULL,
    last_activity_at    timestamptz NOT NULL,
    -- Máximo 8 h desde el ingreso; nada lo corre.
    expires_at          timestamptz NOT NULL,
    ended_at            timestamptz,
    end_reason          text        CHECK (end_reason IN ('logout', 'inactivity', 'expired', 'revoked', 'password_changed')),
    ip                  text        CHECK (length(ip) <= 64),
    user_agent          text        CHECK (length(user_agent) <= 512),
    CONSTRAINT ck_sessions_maximo CHECK (expires_at <= created_at + interval '8 hours')
);

CREATE UNIQUE INDEX uk_sessions_token ON sessions (token_hash);
CREATE INDEX ix_sessions_account ON sessions (account_id) WHERE ended_at IS NULL;

-- Un desafío de código de un solo uso. Para activar y recuperar nace antes de saber si hay persona: los documentos que no
-- existen dejan un desafío «señuelo» que se comporta igual y nunca se verifica (FR-018).
CREATE TABLE otp_challenges (
    id                  uuid        PRIMARY KEY,
    purpose             text        NOT NULL CHECK (purpose IN ('activation', 'new_device', 'recovery', 'contact_change')),
    account_id          uuid        REFERENCES accounts (id),
    -- SHA-256 del tipo y número de documento: para el límite por documento sin guardar el documento en claro.
    document_key        bytea       NOT NULL CHECK (length(document_key) = 32),
    -- En activación: el documento y la persona en la entidad validadora (para crear la cuenta al terminar).
    document_type       text,
    document_number     text,
    tenant_id           uuid        REFERENCES tenants (id),
    person_public_id    uuid,
    decoy               boolean     NOT NULL DEFAULT false,
    -- Cambio de contacto: el campo y el valor nuevo (se guardan sólo hasta verificar).
    contact_field       text        CHECK (contact_field IN ('email', 'mobile')),
    contact_value       text        CHECK (length(contact_value) <= 254),
    channel             text        CHECK (channel IN ('email', 'sms')),
    destination_hash    bytea,
    code_hash           bytea,
    sends               integer     NOT NULL DEFAULT 0 CHECK (sends BETWEEN 0 AND 5),
    attempts            integer     NOT NULL DEFAULT 0 CHECK (attempts BETWEEN 0 AND 3),
    created_at          timestamptz NOT NULL,
    -- 5 minutos desde el último envío.
    expires_at          timestamptz NOT NULL,
    consumed_at         timestamptz,
    -- Tras verificar el código: el permiso de un solo uso para terminar (activar, recuperar).
    grant_hash          bytea,
    grant_expires_at    timestamptz,
    grant_used_at       timestamptz,
    ip                  text        CHECK (length(ip) <= 64)
);

CREATE INDEX ix_otp_challenges_document ON otp_challenges (document_key, created_at);
CREATE INDEX ix_otp_challenges_ip ON otp_challenges (ip, created_at);
CREATE UNIQUE INDEX uk_otp_challenges_grant ON otp_challenges (grant_hash) WHERE grant_hash IS NOT NULL;
-- Conteo de SMS por cooperativa (FR-011a).
CREATE INDEX ix_otp_challenges_sms ON otp_challenges (tenant_id, created_at) WHERE channel = 'sms';

-- Fallos de ingreso por documento, exista o no la cuenta (FR-018, FR-022): a los 5 fallos, 15 minutos de bloqueo.
CREATE TABLE login_throttle (
    document_key     bytea       PRIMARY KEY CHECK (length(document_key) = 32),
    failures         integer     NOT NULL DEFAULT 0,
    first_failure_at timestamptz NOT NULL,
    locked_until     timestamptz,
    updated_at       timestamptz NOT NULL DEFAULT now()
);

-- Ceremonias WebAuthn en curso (registro e ingreso con passkey). Vencen en 5 minutos.
CREATE TABLE webauthn_ceremonies (
    id           uuid        PRIMARY KEY,
    kind         text        NOT NULL CHECK (kind IN ('register', 'login')),
    account_id   uuid        REFERENCES accounts (id),
    session_data jsonb       NOT NULL,
    expires_at   timestamptz NOT NULL,
    used_at      timestamptz
);

-- +goose Down
DROP TABLE IF EXISTS webauthn_ceremonies;
DROP TABLE IF EXISTS login_throttle;
DROP TABLE IF EXISTS otp_challenges;
DROP TABLE IF EXISTS sessions;
DROP TABLE IF EXISTS devices;
DROP TRIGGER IF EXISTS legal_acceptances_sin_cambios ON legal_acceptances;
DROP FUNCTION IF EXISTS legal_acceptances_solo_anexar();
DROP TABLE IF EXISTS legal_acceptances;
ALTER TABLE audit_log DROP CONSTRAINT IF EXISTS fk_audit_log_account;
DROP TABLE IF EXISTS accounts;
