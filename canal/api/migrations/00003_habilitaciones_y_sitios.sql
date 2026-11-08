-- Espejo de habilitaciones (R8), sitios de pasarela con su secreto cifrado (R10) y reserva de códigos de canal (R12).
-- data-model.md §B. Nada se borra en flujo de usuario: una habilitación que el ERP deja de listar queda 'disabled'.

-- +goose Up
CREATE TABLE enablements (
    id                uuid        PRIMARY KEY,
    tenant_id         uuid        NOT NULL REFERENCES tenants (id),
    -- PublicId de la persona en la base de la cooperativa.
    person_public_id  uuid        NOT NULL,
    -- Catálogo fijo de tipos del ERP (TiposDeDocumento) y número normalizado.
    document_type     text        NOT NULL CHECK (document_type IN ('CC', 'CE', 'PPT', 'PEP', 'TI', 'PA', 'RC', 'NIT')),
    document_number   text        NOT NULL CHECK (document_number ~ '^[A-Z0-9]{1,20}$'),
    status            text        NOT NULL CHECK (status IN ('enabled', 'disabled')),
    -- Decide la entidad validadora (D5): la 'enabled' de menor enabled_at para el documento.
    enabled_at        timestamptz NOT NULL,
    -- ChangeSequence del ERP (ticks UTC); un cambio más viejo que el guardado no lo pisa.
    change_sequence   bigint      NOT NULL,
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uk_enablements_tenant_person UNIQUE (tenant_id, person_public_id)
);

-- Búsqueda por documento al activar y al ingresar.
CREATE INDEX ix_enablements_document ON enablements (document_type, document_number);

CREATE TABLE gateway_sites (
    -- El SitePublicId que asigna el ERP al destino de pago.
    id                 uuid        PRIMARY KEY,
    -- Sin FK a tenants: el destino puede configurarse antes de que la cooperativa aparezca en el directorio.
    tenant_public_id   uuid        NOT NULL,
    gateway            text        NOT NULL CHECK (gateway IN ('placetopay')),
    base_url           text        NOT NULL CHECK (length(base_url) BETWEEN 1 AND 500),
    login              text        NOT NULL CHECK (length(login) BETWEEN 1 AND 100),
    -- AES-256-GCM con la KEK de kek_version y el id del sitio como datos asociados. Nunca en claro.
    secret_ciphertext  bytea       NOT NULL CHECK (length(secret_ciphertext) > 28),
    kek_version        integer     NOT NULL CHECK (kek_version > 0),
    payment_methods    text[]      NOT NULL DEFAULT '{}',
    status             text        NOT NULL CHECK (status IN ('active', 'failed', 'pending_verification')),
    status_reason      text        CHECK (length(status_reason) <= 500),
    checked_at         timestamptz,
    created_at         timestamptz NOT NULL DEFAULT now(),
    updated_at         timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_gateway_sites_tenant ON gateway_sites (tenant_public_id);

-- Código de 3 letras de cada cooperativa, prefijo de sus referencias de pago (R12). Una vez reservado no cambia ni se
-- reutiliza.
CREATE TABLE channel_codes (
    code              text        PRIMARY KEY CHECK (code ~ '^[A-Z]{3}$'),
    tenant_public_id  uuid        NOT NULL,
    reserved_at       timestamptz NOT NULL,
    CONSTRAINT uk_channel_codes_tenant UNIQUE (tenant_public_id)
);

-- Última conciliación de la política de habilitación pedida al ERP (POST …/enablements/reconcile). En la base y no
-- en memoria: con varias réplicas, el límite de frecuencia es uno solo.
ALTER TABLE tenants ADD COLUMN last_reconcile_at timestamptz;

-- +goose Down
ALTER TABLE tenants DROP COLUMN IF EXISTS last_reconcile_at;
DROP TABLE IF EXISTS channel_codes;
DROP TABLE IF EXISTS gateway_sites;
DROP TABLE IF EXISTS enablements;
