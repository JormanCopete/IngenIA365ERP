-- Auditoría del canal: sólo de anexar (la base rechaza UPDATE, DELETE y TRUNCATE).

-- name: InsertAuditEntry :exec
INSERT INTO audit_log (
    id, at, account_id, tenant_id, actor, action, subject, ip, user_agent, correlation_id, data
) VALUES (
    @id, @at, @account_id, @tenant_id, @actor, @action, @subject, @ip, @user_agent, @correlation_id, @data
);

-- name: ListAuditEntriesByAction :many
SELECT * FROM audit_log WHERE action = @action ORDER BY at, id LIMIT @max_rows;
