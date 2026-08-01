# Setup local — Pruebas de Identidad Central (Feature 002)

Guía pragmática para levantar el stack local en Windows y dejar la API lista
para ejecutar los flujos del manual de pruebas
(`manual-pruebas-identidad-central.md`).

> Esta guía documenta el setup **real** verificado el 2026-06-02:
> SQL Server y MongoDB corriendo **nativos en Windows**, Redis corriendo
> **dentro de WSL2 (Ubuntu)**, y la API ejecutándose con `dotnet run`. No
> requiere Docker.

---

## 1. Componentes del stack

Hay **dos setups posibles**, ambos usan los mismos puertos y connection
strings (no hay que cambiar nada en `appsettings.Development.json` al
alternar entre uno y otro):

### Setup A — Todo local, sin Docker (recomendado en dev diario)

| Servicio        | Dónde corre                 | Puerto | Cómo se verifica           |
|-----------------|-----------------------------|--------|----------------------------|
| SQL Server      | Servicio Windows nativo     | 1433   | `sqlcmd -S localhost -E -Q "SELECT @@VERSION"` |
| MongoDB         | Servicio Windows nativo     | 27017  | `mongosh --eval "db.runCommand({ping:1})"` |
| Redis           | WSL2 distro **`Ubuntu`**    | 6379   | `wsl -d Ubuntu -- redis-cli ping` → `PONG` |
| smtp4dev (SMTP fake) | `dotnet tool` global   | 1025 SMTP, 8025 UI | `curl http://localhost:8025` |
| API .NET 10     | `dotnet run`                | 5100   | `curl http://localhost:5100/health` |

> El forwarder `svchost` en el puerto 6379 que aparece en `netstat` es **WSL2
> port forwarding** — no es un Redis fantasma. Está bien.

### Setup B — Redis y SMTP en Docker (auxiliar)

Si preferís no depender de WSL para Redis, o si estás en una máquina sin WSL,
levantás Redis + smtp4dev vía `docker-compose.dev.yml`:

```powershell
docker compose -f docker-compose.dev.yml up -d
docker compose -f docker-compose.dev.yml ps
```

Detener sin borrar datos:

```powershell
docker compose -f docker-compose.dev.yml down
```

> Los puertos son idénticos al Setup A (Redis 6379, SMTP 1025, UI 8025). Si ya
> tenés Redis WSL o smtp4dev nativo corriendo, apagálos antes o Docker
> tirará "port in use". SQL Server y Mongo siguen siendo nativos Windows en
> ambos setups.

---

## 2. Arrancar Redis (WSL Ubuntu)

```powershell
# 1. Verificar que el distro Ubuntu está corriendo:
wsl -l -v

# 2. Si Redis no está arriba, iniciarlo:
wsl -d Ubuntu -- sudo service redis-server start

# 3. Verificar PONG:
wsl -d Ubuntu -- redis-cli ping
# Esperado: PONG

# 4. Verificar que Windows lo alcanza:
Test-NetConnection -ComputerName localhost -Port 6379 -InformationLevel Quiet
# Esperado: True
```

**Si `redis-cli` no se encuentra** dentro de WSL:

```bash
# Entrar a WSL:
wsl -d Ubuntu

# Instalar:
sudo apt update && sudo apt install -y redis-server

# Confirmar que escucha en 0.0.0.0 (no solo 127.0.0.1) para que WSL2 lo
# exponga al host Windows:
sudo sed -i 's/^bind 127.0.0.1.*/bind 0.0.0.0/' /etc/redis/redis.conf
sudo service redis-server restart
ss -tlnp | grep 6379    # debe mostrar 0.0.0.0:6379
```

**Detener Redis:**

```powershell
wsl -d Ubuntu -- sudo service redis-server stop
```

### 2.1 Redis vía Docker (alternativa al Setup B)

Si preferís Docker en vez de WSL:

```powershell
docker compose -f docker-compose.dev.yml up -d redis
docker exec ingenia365-dev-redis redis-cli ping     # → PONG
```

---

## 2.5 Arrancar smtp4dev (SMTP fake local)

`smtp4dev` es un servidor SMTP fake que atrapa todo el correo saliente y lo
muestra en un web UI — imprescindible para los flujos de invitación,
password-reset y notificaciones sin mandar correos reales.

### Setup A — como `dotnet tool` global

**Instalación (una vez):**

```powershell
dotnet tool install --global Rnwood.Smtp4dev
```

**Arrancar (en una terminal aparte, o en background):**

```powershell
smtp4dev --smtpport 1025 --urls "http://localhost:8025"
```

Ver los correos: abrí `http://localhost:8025` en el navegador.

> Si la primera vez `smtp4dev` no se encuentra desde una terminal nueva,
> cerrá y reabrí la terminal (dotnet tools agrega `%USERPROFILE%\.dotnet\tools`
> al PATH del usuario en la sesión de instalación). Alternativamente, invocalo
> con la ruta completa:
> ```powershell
> & "$env:USERPROFILE\.dotnet\tools\smtp4dev.cmd" --smtpport 1025 --urls "http://localhost:8025"
> ```

**Detener:** Ctrl+C en la terminal donde corre.

### Setup B — vía Docker

```powershell
docker compose -f docker-compose.dev.yml up -d smtp4dev
```

Mismo UI en `http://localhost:8025`.

---

## 3. Arrancar SQL Server y MongoDB

Ambos corren como servicios Windows. Verificar:

```powershell
Get-Service MSSQLSERVER, MongoDB | Format-Table Name, Status
```

Si alguno está `Stopped`:

```powershell
Start-Service MSSQLSERVER
Start-Service MongoDB
```

> SQL Server usa **Windows Authentication** (`Trusted_Connection=true`).
> Las credenciales `User=erp` del `appsettings.Development.json` ya no se
> usan después del cutover, pero quedan por compatibilidad.

### 3.1 Aplicar migraciones de esquema (primera vez)

Las tablas Phase 0 (`SEC_Users`, `SEC_Roles`, `SEC_Permissions`, `SEC_RolePermissions`,
`SEC_LoginAttempts`, `COR_Notifications`) deben alinearse con las entidades
actuales antes de que arranquen los seeders. Sin esto, verás `Invalid column
name` en cascada y algunos seeders van a fallar (el flujo Feature 002 sigue
funcionando de todos modos, pero los logs quedan sucios y los endpoints Phase 0
van a fallar).

**Migraciones formales (en orden):**

```powershell
cd "D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\database\migration"
foreach ($f in @(
    '18_SEC_Users_BackfillColumns.sql',
    '19_SEC_Roles_UserRoles_BackfillColumns.sql',
    '20_SEC_LoginAttempts_BackfillColumns.sql',
    '21_Roles_Scope_BuiltIn.sql',
    '22_SEC_Permissions_Resource_Action_Plus_Roles_TenantNullable.sql',
    '23_SEC_Roles_TenantId_Nullable.sql')) {
    Write-Host "▶ $f" -ForegroundColor Cyan
    sqlcmd -S localhost -E -C -I -d IngenIA365ERP -i $f
    if ($LASTEXITCODE -ne 0) { Write-Host "❌ Falló $f" -ForegroundColor Red; break }
}
```

**Gaps adicionales que las migraciones oficiales no cubren** (aplicar como
segundo paso — corrigen bugs descubiertos el 2026-07-02 al levantar el stack
por primera vez):

```powershell
sqlcmd -S localhost -E -C -I -d IngenIA365ERP -Q @"
-- Gap 1: mig 18 olvidó agregar PasswordHash y FailedLoginAttempts en SEC_Users
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Users') AND name = 'PasswordHash')
    ALTER TABLE dbo.SEC_Users ADD PasswordHash NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Users') AND name = 'FailedLoginAttempts')
    ALTER TABLE dbo.SEC_Users ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_SEC_Users_FailedLoginAttempts DEFAULT 0;
UPDATE dbo.SEC_Users SET PasswordHash = N'' WHERE PasswordHash IS NULL;

-- Gap 2: audit cols en SEC_Permissions
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'CreatedAt')
    ALTER TABLE dbo.SEC_Permissions ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_Permissions_CreatedAt DEFAULT SYSUTCDATETIME();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'CreatedBy') ALTER TABLE dbo.SEC_Permissions ADD CreatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'UpdatedAt') ALTER TABLE dbo.SEC_Permissions ADD UpdatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'UpdatedBy') ALTER TABLE dbo.SEC_Permissions ADD UpdatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'IsDeleted') ALTER TABLE dbo.SEC_Permissions ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_Permissions_IsDeleted DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'DeletedAt') ALTER TABLE dbo.SEC_Permissions ADD DeletedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'DeletedBy') ALTER TABLE dbo.SEC_Permissions ADD DeletedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_Permissions') AND name = 'RowVersion') ALTER TABLE dbo.SEC_Permissions ADD RowVersion ROWVERSION NOT NULL;

-- Gap 3: drop unique index legacy sobre PermissionCode
--   (el nuevo schema usa (Resource, Action) via UK_SEC_Permissions_ResourceAction).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SEC_Permissions_Code' AND object_id = OBJECT_ID('dbo.SEC_Permissions'))
    DROP INDEX IX_SEC_Permissions_Code ON dbo.SEC_Permissions;

-- Gap 4: SEC_RolePermissions necesita Id INT IDENTITY + PublicId + audit + RowVersion.
--   La tabla legacy tenía PK compuesta (RoleId, PermissionId); la reemplazamos
--   por Id INT y agregamos unique(RoleId, PermissionId) para preservar la
--   integridad.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'PublicId')
    ALTER TABLE dbo.SEC_RolePermissions ADD PublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_RolePermissions_PublicId DEFAULT NEWID();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'CreatedAt')
    ALTER TABLE dbo.SEC_RolePermissions ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_RolePermissions_CreatedAt DEFAULT SYSUTCDATETIME();
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'CreatedBy') ALTER TABLE dbo.SEC_RolePermissions ADD CreatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'UpdatedAt') ALTER TABLE dbo.SEC_RolePermissions ADD UpdatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'UpdatedBy') ALTER TABLE dbo.SEC_RolePermissions ADD UpdatedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'IsDeleted') ALTER TABLE dbo.SEC_RolePermissions ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_RolePermissions_IsDeleted DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'DeletedAt') ALTER TABLE dbo.SEC_RolePermissions ADD DeletedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'DeletedBy') ALTER TABLE dbo.SEC_RolePermissions ADD DeletedBy NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'Id')
BEGIN
    ALTER TABLE dbo.SEC_RolePermissions DROP CONSTRAINT PK_SEC_RolePermissions;
    ALTER TABLE dbo.SEC_RolePermissions ADD Id INT IDENTITY(1,1) NOT NULL;
    ALTER TABLE dbo.SEC_RolePermissions ADD CONSTRAINT PK_SEC_RolePermissions PRIMARY KEY (Id);
    CREATE UNIQUE INDEX UX_SEC_RolePermissions_Role_Permission ON dbo.SEC_RolePermissions(RoleId, PermissionId);
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SEC_RolePermissions') AND name = 'RowVersion')
    ALTER TABLE dbo.SEC_RolePermissions ADD RowVersion ROWVERSION NOT NULL;

-- Gap 5: COR_Notifications tenía schema viejo (Channel string, Status, TemplateId,
--   RecipientPersonId, SentAt). Agregamos las nuevas columnas del refactor.
--   Solo funciona si la tabla está vacía (0 rows) — de lo contrario los NOT NULL
--   con defaults van a marcar todo con TenantId=0 y RecipientUser=empty guid.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'TenantId') ALTER TABLE dbo.COR_Notifications ADD TenantId INT NOT NULL CONSTRAINT DF_COR_Notifications_TenantId DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'RecipientUserPublicId') ALTER TABLE dbo.COR_Notifications ADD RecipientUserPublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_COR_Notifications_RecipientUserPublicId DEFAULT '00000000-0000-0000-0000-000000000000';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'Type') ALTER TABLE dbo.COR_Notifications ADD [Type] NVARCHAR(80) NOT NULL CONSTRAINT DF_COR_Notifications_Type DEFAULT N'';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'ChannelsMask') ALTER TABLE dbo.COR_Notifications ADD ChannelsMask INT NOT NULL CONSTRAINT DF_COR_Notifications_ChannelsMask DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'EmailStatus') ALTER TABLE dbo.COR_Notifications ADD EmailStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_COR_Notifications_EmailStatus DEFAULT N'Pending';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'EmailSentAt') ALTER TABLE dbo.COR_Notifications ADD EmailSentAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'EmailAttemptCount') ALTER TABLE dbo.COR_Notifications ADD EmailAttemptCount INT NOT NULL CONSTRAINT DF_COR_Notifications_EmailAttemptCount DEFAULT 0;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'ReadAt') ALTER TABLE dbo.COR_Notifications ADD ReadAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'ArchivedAt') ALTER TABLE dbo.COR_Notifications ADD ArchivedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.COR_Notifications') AND name = 'NotificationTemplateId') ALTER TABLE dbo.COR_Notifications ADD NotificationTemplateId INT NULL;

PRINT '✔ Gaps de esquema aplicados.';
"@
```

**Gap 6: `ADM_Tenants` (base de datos `IngenIA365ERP_Admin`) — la tabla
legacy no tiene las columnas del refactor de tenants con perfil comercial.
Aplicar contra la base admin, no la principal:**

```powershell
sqlcmd -S localhost -E -C -I -d IngenIA365ERP_Admin -Q @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'Subdomain') ALTER TABLE dbo.ADM_Tenants ADD Subdomain NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'PlanType') ALTER TABLE dbo.ADM_Tenants ADD PlanType NVARCHAR(50) NOT NULL CONSTRAINT DF_ADM_Tenants_PlanType DEFAULT N'Basic';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'StorageLimitMb') ALTER TABLE dbo.ADM_Tenants ADD StorageLimitMb BIGINT NOT NULL CONSTRAINT DF_ADM_Tenants_StorageLimitMb DEFAULT 5120;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'DatabaseName') ALTER TABLE dbo.ADM_Tenants ADD DatabaseName NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'ContactEmail') ALTER TABLE dbo.ADM_Tenants ADD ContactEmail NVARCHAR(200) NOT NULL CONSTRAINT DF_ADM_Tenants_ContactEmail DEFAULT N'';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'ContactPhone') ALTER TABLE dbo.ADM_Tenants ADD ContactPhone NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'ActivatedAt') ALTER TABLE dbo.ADM_Tenants ADD ActivatedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'SuspendedAt') ALTER TABLE dbo.ADM_Tenants ADD SuspendedAt DATETIME2(0) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ADM_Tenants') AND name = 'RowVersion') ALTER TABLE dbo.ADM_Tenants ADD RowVersion ROWVERSION NOT NULL;

-- Legacy: Identifier + LicenseType eran NOT NULL pero ya no se mapean en la
-- entidad Tenant. Drop de la unique + volverlas nullable para que los INSERT
-- del handler RegisterTenantWithAdmin no fallen.
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ADM_Tenants_Identifier')
    ALTER TABLE dbo.ADM_Tenants DROP CONSTRAINT UQ_ADM_Tenants_Identifier;
ALTER TABLE dbo.ADM_Tenants ALTER COLUMN Identifier NVARCHAR(100) NULL;
ALTER TABLE dbo.ADM_Tenants ALTER COLUMN LicenseType NVARCHAR(50) NULL;

PRINT '+ ADM_Tenants alineada';
"@
```

**Gap 7: `ADM_PasswordResetTokens.Id` era `BIGINT IDENTITY` (según el DDL 15e)
pero la entidad `PasswordResetToken : AuditableEntity` espera `int` — mismo
defecto que el Gap 4. Descubierto el 2026-07-31 al ejecutar el paso 4.2 del
manual (forgot password → `InvalidCastException: Unable to cast Int64 to
Int32`). Aplicar contra la base admin:**

```powershell
sqlcmd -S localhost -E -C -I -d IngenIA365ERP_Admin -Q @"
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
           WHERE c.object_id = OBJECT_ID('dbo.ADM_PasswordResetTokens') AND c.name = 'Id' AND t.name = 'bigint')
BEGIN
    DELETE FROM dbo.ADM_PasswordResetTokens;  -- filas huérfanas de inserts fallidos
    DECLARE @pk sysname = (SELECT name FROM sys.key_constraints
                           WHERE parent_object_id = OBJECT_ID('dbo.ADM_PasswordResetTokens') AND type = 'PK');
    IF @pk IS NOT NULL EXEC('ALTER TABLE dbo.ADM_PasswordResetTokens DROP CONSTRAINT [' + @pk + ']');
    ALTER TABLE dbo.ADM_PasswordResetTokens DROP COLUMN Id;
    ALTER TABLE dbo.ADM_PasswordResetTokens ADD Id INT IDENTITY(1,1) NOT NULL;
    ALTER TABLE dbo.ADM_PasswordResetTokens ADD CONSTRAINT PK_ADM_PasswordResetTokens PRIMARY KEY (Id);
    PRINT '+ Id recreado como INT IDENTITY';
END
ELSE PRINT '= Id ya es INT — sin cambios';
"@
```

> Nota: el Gap 7 iba acompañado de un bug de código ya corregido en el repo —
> `AdminDbContext.OnModelCreating` no aplicaba `PasswordResetTokenConfiguration`,
> por lo que EF buscaba `dbo.PasswordResetTokens` en vez de
> `ADM_PasswordResetTokens` (`Invalid object name`).

> **✔ Formalizados (2026-08-01)**: los 7 gaps ya viven como migraciones
> oficiales idempotentes — `database/migration/26_Backfill_Gaps_Tenant.sql`
> (gaps 1–5, BD `IngenIA365ERP`) y `26b_Backfill_Gaps_Admin.sql` (gaps 6–7,
> BD `IngenIA365ERP_Admin`). El SQL inline de esta sección queda como
> referencia histórica; para entornos nuevos ejecutá los scripts formales.

---

## 4. Variables de entorno y connection strings

La API lee todo desde `src/Presentation/IngenIA365ERP.API/appsettings.Development.json`.
**No** hay que setear variables de entorno para correr local — basta con que
`ASPNETCORE_ENVIRONMENT=Development` esté activo (es el default cuando se usa
`launchSettings.json`).

Connection strings activas:

| Key                                    | Valor                                                                  |
|----------------------------------------|------------------------------------------------------------------------|
| `ConnectionStrings:SqlServer`          | `Server=localhost;Database=IngenIA365ERP;Trusted_Connection=true;...`  |
| `ConnectionStrings:SqlServerAdmin`     | `Server=localhost;Database=IngenIA365ERP_Admin;Trusted_Connection=true;...` |
| `ConnectionStrings:MongoDB`            | `mongodb://localhost:27017/IngenIA365ERP_Audit`                        |
| `ConnectionStrings:Redis`              | `localhost:6379,abortConnect=false`                                    |
| `EmailSender:Smtp:Host` / `Port`       | `localhost:1025` (MailHog opcional)                                    |

---

## 5. Arrancar la API

### Forma corta (recomendada)

```powershell
cd "D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP"
dotnet run --project src/Presentation/IngenIA365ERP.API
```

Esperar el banner:

```
[HH:mm:ss INF] Now listening on: http://localhost:5100
[HH:mm:ss INF] Application started. Press Ctrl+C to shut down.
[HH:mm:ss INF] Hosting environment: Development
```

### Forma sin launchSettings

Si arrancás con `--no-launch-profile` (por ejemplo desde scripts), **es
obligatorio** setear el environment explícitamente — de lo contrario no se
carga `appsettings.Development.json` y la API falla con
`Connection string 'DefaultConnection' not found`:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/Presentation/IngenIA365ERP.API --no-launch-profile
```

---

## 6. Credenciales del master admin

Tras ejecutar `database/migration/16_Seed_Default_GlobalMasterAdmin.sql` con
las variables `MASTER_ADMIN_EMAIL` y `MASTER_ADMIN_PASSWORD_HASH` (ver
sección 7 abajo), el master queda creado.

**Master admin actual (entorno local):**

- Email: `master@ingenia.dev`
- Password: `MasterDev2026!Strong`
- CentralUserId: `c3cc31c1-a855-43bb-aec8-c751cfff4526`

**Probar login:**

```powershell
curl -s -X POST http://localhost:5100/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{"email":"master@ingenia.dev","password":"MasterDev2026!Strong"}'
```

Respuesta esperada en el primer login (sin tenants asignados):

```json
{
  "challenge": "NoActiveMembership",
  "centralUserId": "c3cc31c1-a855-43bb-aec8-c751cfff4526",
  "isGlobalMasterAdmin": true,
  "message": "No tienes acceso a ninguna empresa. Solicita una invitación."
}
```

Eso significa: **handler vivo, hash BCrypt validado, Redis OK**. El próximo
paso es seguir el manual de pruebas para registrar el primer tenant.

---

## 7. Resetear la contraseña del master (si la perdiste)

El seed script (`16_Seed_Default_GlobalMasterAdmin.sql`) es idempotente: si
ya existe un master no inserta otro. Para cambiar la contraseña tenés dos
opciones:

### Opción A — UPDATE directo en SQL

```powershell
# 1. Generar hash BCrypt cost 11 con un proyecto temporal:
mkdir /tmp/bcrypt-gen; cd /tmp/bcrypt-gen
@'
Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(args[0], 11));
'@ | Out-File -Encoding utf8 Program.cs
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
  </ItemGroup>
</Project>
'@ | Out-File -Encoding utf8 bcrypt-gen.csproj
dotnet run --verbosity quiet -- 'TuPasswordNuevo2026!'
# Copiar el hash $2a$11$... que imprime

# 2. UPDATE en SQL (el flag -I es OBLIGATORIO por los filtered indexes):
sqlcmd -S localhost -E -C -I -d IngenIA365ERP_Admin -Q "UPDATE ADM_CentralUsers SET PasswordHash = N'$2a$11$...PEGAR_HASH...', SecurityStamp = LOWER(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', '')) WHERE IsGlobalMasterAdmin = 1;"
```

### Opción B — Re-ejecutar el seed sobre BD limpia

Solo si podés borrar todo el esquema admin. No recomendado en pruebas
en curso.

---

## 8. Errores y soluciones rápidas

| Síntoma | Causa | Solución |
|---------|-------|----------|
| `RedisConnectionException: It was not possible to connect` | Redis WSL apagado o bindeado a `127.0.0.1` | `wsl -d Ubuntu -- sudo service redis-server start`; verificar `bind 0.0.0.0` en `/etc/redis/redis.conf` |
| `RedisConnectionException: SocketFailure ConnectionReset` con Redis WSL respondiendo `PONG` adentro | Relay de port-forwarding WSL2 (`svchost` en 6379) quedó zombie: acepta el TCP pero resetea al primer byte | `wsl --shutdown` y volver a arrancar Redis — el relay se recrea; la API se reconecta sola (`AbortOnConnectFail=false`) |
| `Connection string 'DefaultConnection' not found` | Falta `ASPNETCORE_ENVIRONMENT=Development` | Quitar `--no-launch-profile` o setear el env var |
| `Unable to resolve service for type 'IDistributedLock'` (y similares) | `AddCachingServices` comentado en `Program.cs` | Verificar que la línea `builder.Services.AddCachingServices(builder.Configuration);` esté activa |
| `Login failed for user 'sa'` | SQL Server usa Windows Auth, no SA | Usar `sqlcmd -E` (Windows) en vez de `-U sa -P` |
| `UPDATE failed because ... 'QUOTED_IDENTIFIER'` | Filtered indexes requieren QI ON | Agregar el flag `-I` a `sqlcmd` |
| `Invalid column name 'EmailStatus'` en logs cada 15s | `COR_Notifications` no tiene columnas del refactor | Aplicar los gaps de la sección 3.1 (Gap 5) |
| `Invalid column name 'PasswordHash'` / `'FailedLoginAttempts'` | mig 18 olvidó agregarlas | Aplicar Gap 1 de sección 3.1 |
| `Invalid column name 'CreatedAt', 'IsDeleted', ...` en `SEC_Permissions`/`SEC_RolePermissions` | Audit cols faltan en las tablas de permisos | Aplicar Gap 2 y Gap 4 de sección 3.1 |
| `Cannot insert duplicate key row in ... IX_SEC_Permissions_Code`. `duplicate key value is ()` | Índice legacy sobre `PermissionCode` (empty) bloquea inserts nuevos | Aplicar Gap 3 (drop del índice) |
| `SqlNullValueException: Data is Null` en `DomainSecuritySeedData` | Filas legacy en `SEC_Users` tienen `PasswordHash = NULL` | Aplicar `UPDATE SEC_Users SET PasswordHash = N'' WHERE PasswordHash IS NULL;` (incluido en Gap 1) |
| `InvalidCastException: Unable to cast 'System.Int64' to 'System.Int32'` en un seeder Security | `SEC_RolePermissions.Id` es `BIGINT` pero la entidad `RolePermission : AuditableEntity` espera `int` | Aplicar Gap 4 (recrea `Id` como `INT IDENTITY`) |
| `InvalidCastException: Unable to cast 'System.Int64' to 'System.Int32'` (HTTP 500) en `POST /api/auth/password/forgot` | `ADM_PasswordResetTokens.Id` es `BIGINT` pero la entidad `PasswordResetToken : AuditableEntity` espera `int` | Aplicar Gap 7 (recrea `Id` como `INT IDENTITY`) |
| Compilación falla con `El archivo se ha bloqueado por: "IngenIA365ERP.API (PID)"` | Instancia previa de la API sigue viva | `Get-Process IngenIA365ERP.API \| Stop-Process -Force` |
| `Failed to bind to address http://127.0.0.1:5100: address already in use` | Otra instancia de la API ocupa el puerto (típicamente después de un Ctrl+C mal cerrado o VS con debug activo) | Ver comando arriba, o `Get-NetTCPConnection -LocalPort 5100 -State Listen \| ForEach { Stop-Process -Id $_.OwningProcess -Force }` |
| Master responde `Credenciales inválidas` | Hash sembrado distinto al password que probás | Resetear hash con sección 7, Opción A |

---

## 9. Apagar todo

```powershell
# Detener API: Ctrl+C en la terminal donde corre dotnet run

# Detener Redis (opcional, los servicios pueden quedar prendidos):
wsl -d Ubuntu -- sudo service redis-server stop

# SQL Server y MongoDB son servicios Windows — generalmente se dejan
# encendidos. Para detenerlos:
Stop-Service MSSQLSERVER
Stop-Service MongoDB
```

---

## 10. Referencias

- Manual técnico de pruebas: [`manual-pruebas-identidad-central.md`](manual-pruebas-identidad-central.md)
- Manual funcional usuario final: [`manual-funcional-usuario-final.md`](manual-funcional-usuario-final.md)
- Setup Phase 0 con Docker (deprecado para este flujo): [`dev-environment.md`](dev-environment.md)
- Spec del feature: [`../../specs/002-identidad-central-federada/`](../../specs/002-identidad-central-federada/)
