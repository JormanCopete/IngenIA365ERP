// La configuración de UserRole se centraliza ahora en UserConfiguration vía
// `UsingEntity<UserRole>(...)` para que la junction sea EL mapeo de SEC_UserRoles
// y EF use los nombres reales de columna (UserId/RoleId) en lugar de la
// convención implícita (RolesId/UsersId).
//
// Se conserva el archivo vacío como marca histórica — si en el futuro UserRole
// requiere configuración extra independiente del N:N, se reintroduce aquí.
