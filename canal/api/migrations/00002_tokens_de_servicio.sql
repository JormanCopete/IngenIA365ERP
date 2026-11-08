-- Identificadores (jti) de los tokens de servicio que el ERP firma hacia el canal: cada uno vale una sola vez
-- (R2). La tabla es compartida por las réplicas; un jti vencido ya no sirve y lo purga el trabajo purge_service_jti.
-- No son datos de usuario: su purga no contradice la regla «nada se borra en flujo de usuario».

-- +goose Up
CREATE TABLE service_token_jti (
    jti         text        PRIMARY KEY CHECK (length(jti) BETWEEN 1 AND 128),
    expires_at  timestamptz NOT NULL
);

CREATE INDEX ix_service_token_jti_expires_at ON service_token_jti (expires_at);

-- +goose Down
DROP TABLE IF EXISTS service_token_jti;
