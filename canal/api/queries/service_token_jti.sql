-- jti de un solo uso de los tokens que el ERP firma hacia el canal (R2).

-- name: RegisterServiceJti :execrows
-- 1 fila = jti nuevo; 0 = ya se había usado.
INSERT INTO service_token_jti (jti, expires_at) VALUES (@jti, @expires_at)
ON CONFLICT (jti) DO NOTHING;

-- name: PurgeExpiredServiceJti :execrows
DELETE FROM service_token_jti WHERE expires_at < @before;
