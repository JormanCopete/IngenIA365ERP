# Contract — Configuración `Database` (Feature 004)

Contrato operativo de la sección de configuración que gobierna el multi-motor.
La forma completa y las reglas de validación viven en [data-model.md §1](../data-model.md).

## Claves

| Clave | Tipo | Obligatoria | Default Dev/QA | Default Production |
|---|---|---|---|---|
| `Database:Provider` | `"PostgreSQL" \| "SqlServer"` (case-insensitive) | Sí | `PostgreSQL` | — (explícita) |
| `Database:ConnectionStrings:PostgreSQL` | string | Solo si Provider=PostgreSQL | ejemplo local | secreto |
| `Database:ConnectionStrings:SqlServer` | string | Solo si Provider=SqlServer | ejemplo local | secreto |
| `Database:AdminConnectionStrings:*` | string | No (deriva `_Admin`) | — | opcional |
| `Database:AutoMigrate` | bool | No | `true` | `false` |
| `Database:Seed:RunParametricSeed` | bool | No | `true` | `true` |
| `Database:Seed:RunTestSeed` | bool | No | `true` | `false` (opt-in auditado) |
| `Database:Startup:RetryWindowSeconds` | int ≥ 0 | No | `60` | `60` |
| `Database:Startup:RetryIntervalSeconds` | int ≥ 1 | No | `5` | `5` |

## Variables de entorno (precedencia sobre appsettings)

```bash
Database__Provider=PostgreSQL
Database__ConnectionStrings__PostgreSQL="Host=...;Database=...;Username=...;Password=..."
Database__ConnectionStrings__SqlServer="Server=...;Database=...;User Id=...;Password=..."
Database__AutoMigrate=false
Database__Seed__RunTestSeed=true
```

## Errores de validación (fail-fast, arranque)

| Código | Condición | Mensaje (español, accionable) |
|---|---|---|
| `Database.InvalidProvider` | Provider fuera de {PostgreSQL, SqlServer} | "Proveedor de base de datos '<x>' no soportado. Valores válidos: PostgreSQL, SqlServer." |
| `Database.ConnectionStringMissing` | Falta la cadena del provider activo | "Falta la cadena de conexión para el proveedor '<x>' (Database:ConnectionStrings:<x>)." |
| `Database.InvalidStartupOptions` | Retry inválido | "Startup:RetryWindowSeconds debe ser ≥ 0 y RetryIntervalSeconds ≥ 1." |
| `Database.Unreachable` | BD inaccesible agotada la ventana | "No fue posible conectar a la base de datos (<host/db enmascarado>) tras <n>s." |
| `Database.MigrationsPending` | `AutoMigrate=false` + pendientes | "Hay <n> migraciones pendientes: <lista>. Aplique los scripts de DBA o habilite Database:AutoMigrate." |
| `Database.MigrationFailed` | Error aplicando una migración | "La migración '<nombre>' falló: <causa>." |

Reglas transversales: la cadena de conexión citada en cualquier log/error pasa por el
enmascarador (host y database visibles; usuario/contraseña ocultos). El proceso que
falla la validación termina con exit code ≠ 0 y **no** queda escuchando (FR-003).

## Configuraciones de ejemplo entregadas

- `appsettings.json` — estructura completa con placeholders.
- `appsettings.Development.json` — PG default (contenedor local), AutoMigrate on, ambos seeds on.
- `appsettings.QA.json` — PG, AutoMigrate on, paramétrico on, demo on.
- `appsettings.Production.json` — provider explícito, AutoMigrate off (scripts DBA), paramétrico on, demo off; cadenas vía variables de entorno.
