-- Códigos de canal de 3 letras (R12): uno por cooperativa, nunca reutilizado.

-- name: GetChannelCodeByTenant :one
SELECT code FROM channel_codes WHERE tenant_public_id = @tenant_public_id;

-- name: ReserveChannelCode :execrows
-- 0 filas = el código ya es de otra cooperativa o esta cooperativa ya tiene uno (carrera): el llamador relee.
INSERT INTO channel_codes (code, tenant_public_id, reserved_at)
VALUES (@code, @tenant_public_id, @reserved_at)
ON CONFLICT DO NOTHING;

-- name: ListTakenChannelCodes :many
-- Ocupados: los reservados y los que ya trae el directorio para otras cooperativas.
SELECT c.code FROM channel_codes AS c
UNION
SELECT t.channel_code FROM tenants AS t WHERE t.tenant_public_id <> @except_tenant::uuid;
