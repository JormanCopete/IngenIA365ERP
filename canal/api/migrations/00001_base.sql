-- Base del canal (feature 021, data-model.md §B): directorio de cooperativas y auditoría.
-- Identificadores UUID v7 generados en Go. Nada se borra en flujo de usuario: estados y fechas.

-- +goose Up
CREATE TABLE tenants (
    id                  uuid        PRIMARY KEY,
    -- PublicId de la cooperativa en el ERP.
    tenant_public_id    uuid        NOT NULL,
    -- Prefijo de las referencias de pago (R12); lo asigna la plataforma al encender el canal.
    channel_code        text        NOT NULL CHECK (channel_code ~ '^[A-Z]{3}$'),
    display_name        text        NOT NULL CHECK (length(display_name) BETWEEN 1 AND 200),
    -- {logoUrl, primary, secondary}
    brand               jsonb       NOT NULL DEFAULT '{}'::jsonb,
    -- {phone, email, hours}
    support             jsonb       NOT NULL DEFAULT '{}'::jsonb,
    inactivity_minutes  integer     NOT NULL DEFAULT 5 CHECK (inactivity_minutes BETWEEN 1 AND 60),
    sms_enabled         boolean     NOT NULL DEFAULT false,
    -- Nulo = la URL común del ERP (CANAL_ERP_URL).
    erp_base_url        text,
    -- false cuando el ERP deja de listarla (canal apagado); nunca se borra la fila.
    is_enabled          boolean     NOT NULL DEFAULT true,
    -- Cursor del arrastre de habilitaciones (R8).
    enablements_cursor  bigint      NOT NULL DEFAULT 0,
    last_sync_at        timestamptz NOT NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_tenants_public_id UNIQUE (tenant_public_id),
    CONSTRAINT uk_tenants_channel_code UNIQUE (channel_code)
);

CREATE TABLE audit_log (
    id              uuid        PRIMARY KEY,
    at              timestamptz NOT NULL DEFAULT now(),
    -- La FK a accounts se agrega en la migración que crea esa tabla (C1).
    account_id      uuid,
    tenant_id       uuid        REFERENCES tenants (id),
    actor           text        NOT NULL CHECK (actor IN ('member', 'erp_staff', 'system')),
    action          text        NOT NULL CHECK (length(action) BETWEEN 1 AND 100),
    subject         text        NOT NULL DEFAULT '' CHECK (length(subject) <= 200),
    ip              text        CHECK (length(ip) <= 64),
    user_agent      text        CHECK (length(user_agent) <= 512),
    correlation_id  text        CHECK (length(correlation_id) <= 64),
    -- Sin secretos ni datos de tarjeta (lo valida auditoria.Registrar).
    data            jsonb       NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX ix_audit_log_at ON audit_log (at);
CREATE INDEX ix_audit_log_account ON audit_log (account_id, at) WHERE account_id IS NOT NULL;
CREATE INDEX ix_audit_log_tenant ON audit_log (tenant_id, at) WHERE tenant_id IS NOT NULL;
CREATE INDEX ix_audit_log_action ON audit_log (action, at);

-- Sólo de anexar (regla 6 de «Sistemas satélite»): la base rechaza UPDATE, DELETE y TRUNCATE.
-- La depuración por retención (≥ 5 años) será una migración o un procedimiento de soporte aparte.
-- +goose StatementBegin
CREATE FUNCTION audit_log_solo_anexar() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_log es sólo de anexar: % no está permitido', TG_OP
        USING ERRCODE = 'insufficient_privilege';
END;
$$;
-- +goose StatementEnd

CREATE TRIGGER audit_log_sin_cambios
    BEFORE UPDATE OR DELETE ON audit_log
    FOR EACH ROW EXECUTE FUNCTION audit_log_solo_anexar();

CREATE TRIGGER audit_log_sin_truncar
    BEFORE TRUNCATE ON audit_log
    FOR EACH STATEMENT EXECUTE FUNCTION audit_log_solo_anexar();

-- +goose Down
DROP TRIGGER IF EXISTS audit_log_sin_truncar ON audit_log;
DROP TRIGGER IF EXISTS audit_log_sin_cambios ON audit_log;
DROP FUNCTION IF EXISTS audit_log_solo_anexar();
DROP TABLE IF EXISTS audit_log;
DROP TABLE IF EXISTS tenants;
