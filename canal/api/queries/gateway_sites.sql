-- Sitios de pasarela (R10). El secreto va siempre cifrado; ninguna consulta lo devuelve fuera del canal.

-- name: GetGatewaySite :one
SELECT * FROM gateway_sites WHERE id = @id;

-- name: UpsertGatewaySite :one
-- Sin fila devuelta = el sitio existe y es de otra cooperativa: no se toca.
INSERT INTO gateway_sites (
    id, tenant_public_id, gateway, base_url, login, secret_ciphertext, kek_version, payment_methods,
    status, status_reason, checked_at
) VALUES (
    @id, @tenant_public_id, @gateway, @base_url, @login, @secret_ciphertext, @kek_version, @payment_methods,
    @status, @status_reason, @checked_at
)
ON CONFLICT (id) DO UPDATE SET
    gateway           = EXCLUDED.gateway,
    base_url          = EXCLUDED.base_url,
    login             = EXCLUDED.login,
    secret_ciphertext = EXCLUDED.secret_ciphertext,
    kek_version       = EXCLUDED.kek_version,
    payment_methods   = EXCLUDED.payment_methods,
    status            = EXCLUDED.status,
    status_reason     = EXCLUDED.status_reason,
    checked_at        = EXCLUDED.checked_at,
    updated_at        = now()
WHERE gateway_sites.tenant_public_id = EXCLUDED.tenant_public_id
RETURNING *;
