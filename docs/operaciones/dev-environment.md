# Entorno de desarrollo — Fase 0 (Cimientos técnicos)

Este documento describe cómo levantar el entorno local para trabajar las
historias US1–US7 de `specs/001-cimientos-tecnicos/`. Solo cubre las
dependencias de infraestructura; la API y el panel Blazor se ejecutan con
`dotnet run` desde el IDE.

## Prerrequisitos

| Herramienta        | Versión mínima | Notas |
|--------------------|----------------|-------|
| .NET SDK           | 10.0.300       | `dotnet --version` debe imprimir `10.0.300` o superior |
| Docker Desktop     | 4.31+          | Engine + Compose v2 |
| Git                | 2.40+          | Necesario para los hooks `.specify/extensions.yml` |
| (opcional) VS Code o JetBrains Rider | última | Para la experiencia integrada de Blazor + EF Core |

## Levantar la infraestructura

```bash
docker compose -f docker/dev.yml up -d
```

Servicios expuestos:

| Servicio   | Puerto host | Credenciales / Notas |
|------------|-------------|-----------------------|
| SQL Server | `1433`      | `sa` / `IngenIA365_Dev2026!` (Developer Edition) |
| MongoDB    | `27017`     | Sin auth en local; base `IngenIA365ERP_Audit` |
| Redis      | `6379`      | Sin auth en local |
| smtp4dev   | `5025` SMTP, `5080` UI | http://localhost:5080 para inspeccionar correos |

Verificar:

```bash
docker compose -f docker/dev.yml ps
```

Detener (sin borrar volúmenes):

```bash
docker compose -f docker/dev.yml down
```

Limpiar todo, incluyendo datos:

```bash
docker compose -f docker/dev.yml down -v
```

## Variables de entorno

La API/Web leen estas variables desde `appsettings.Development.json` o desde el
entorno. Los valores por defecto coinciden con el stack de `docker/dev.yml`:

```bash
# Conexiones a datos
export ConnectionStrings__SqlServer="Server=localhost,1433;Database=IngenIA365ERP;User=sa;Password=IngenIA365_Dev2026!;TrustServerCertificate=true"
export MongoDb__ConnectionString="mongodb://localhost:27017"
export MongoDb__DatabaseName="IngenIA365ERP_Audit"
export ConnectionStrings__Redis="localhost:6379,abortConnect=false"

# Correo (smtp4dev)
export Smtp__Host="localhost"
export Smtp__Port="5025"
export Smtp__From="no-reply@ingenia365.dev"

# DataProtection — claves de cifrado para refresh tokens, secretos MFA y adjuntos
# En dev, se almacenan en disco. En prod, se rotan por DPAPI / KMS gestionado.
export DataProtection__KeyRingPath="./.dataprotection-keys"
export DataProtection__ApplicationName="IngenIA365ERP-Dev"

# JWT RS256 — el par de claves se genera al primer arranque si no existe.
export JwtSettings__PrivateKeyPath="Keys/dev_private.pem"
export JwtSettings__PublicKeyPath="Keys/dev_public.pem"
export JwtSettings__Issuer="IngenIA365ERP-Dev"
export JwtSettings__Audience="IngenIA365ERP-Clients"
```

> **Importante**: las claves `dev_private.pem` y `dev_public.pem` que aparecen
> en este documento son **solo para desarrollo**. No deben copiarse a otros
> entornos. En staging/prod se generan vía el HSM o el KMS correspondiente.

## Flujo recomendado

```bash
# 1. Arrancar infraestructura
docker compose -f docker/dev.yml up -d

# 2. Restaurar y compilar
dotnet restore IngenIA365ERP.slnx
dotnet build IngenIA365ERP.slnx -c Release

# 3. Migraciones SQL (tras T032)
dotnet run --project tools/IngenIA365ERP.DbMigrator -c Release

# 4. API
dotnet run --project src/Presentation/IngenIA365ERP.API -c Release

# 5. Web (otro terminal)
dotnet run --project src/Presentation/IngenIA365ERP.Web -c Release
```

## Diagnóstico rápido

| Síntoma | Posible causa | Acción |
|---------|---------------|--------|
| `Login failed for user 'sa'` | SQL Server aún arrancando | Esperar 30 s y reintentar; `docker compose -f docker/dev.yml logs sqlserver` |
| `MongoServerSelectionTimeout` | MongoDB no expone 27017 | `docker compose -f docker/dev.yml ps`; revisar firewall local |
| `StackExchange.Redis.RedisConnectionException` | Redis caído | `docker compose -f docker/dev.yml restart redis` |
| Correos no llegan a smtp4dev | Puerto SMTP del cliente incorrecto | Confirmar `Smtp__Port=5025` (host) — *no* 25 |
| `IDX10500: Signature validation failed` | Claves RSA no cargadas o desincronizadas | Borrar `Keys/dev_*.pem` y reiniciar la API |

## Referencias

- Plan de la fase: [`specs/001-cimientos-tecnicos/plan.md`](../../specs/001-cimientos-tecnicos/plan.md)
- Tareas: [`specs/001-cimientos-tecnicos/tasks.md`](../../specs/001-cimientos-tecnicos/tasks.md)
- Quickstart funcional (post Sprint D): [`specs/001-cimientos-tecnicos/quickstart.md`](../../specs/001-cimientos-tecnicos/quickstart.md)
