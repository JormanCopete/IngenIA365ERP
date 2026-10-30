# Implementation Plan: Ingreso con correo o usuario de documento e invitaciones ancladas al documento

**Branch**: `claude/017-login-documento-invitacion-253fe0` | **Date**: 2026-10-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/017-login-documento-invitacion/spec.md`, aprobada por el
dueño el 2026-10-28 (D-01..D-16).

## Summary

La identidad de acceso deja de depender sólo del correo y se ancla al documento.

- **Cuenta central:** la cuenta de la base administrativa gana tipo y número de documento, un **usuario
  derivado** (la cédula sin prefijo; los demás tipos con el suyo: `CE987654`, `PPT…`, `PAAB123`),
  nombres y celular. La regla vive en un solo sitio de Domain y el catálogo de tipos es fijo.
- **Ingreso:** un solo campo «Correo o usuario». Con `@` sólo sirven correos verificados; sin `@`, se
  busca el usuario normalizado. La respuesta no deja distinguir si la cuenta existe: mismo mensaje, mismo
  tiempo, bloqueo por cuenta.
- **Invitación y activación:** la invitación captura documento (dos veces), nombres, celular, rol y
  cargo. Crea o reutiliza la identidad, sale al correo de la identidad y no revela otras cooperativas.
  Activar exige el número; al agotar los intentos, la invitación queda «en revisión».
- **Persona de cada cooperativa:** gana el mismo usuario derivado, que pasa a ser su llave única. Así
  conviven CC y CE con el mismo número. Todo lo que hoy busca personas por el número solo pasa por un
  resolvedor que pide el tipo cuando hay ambigüedad.
- **Lo que se agrega:**
  - completar el documento sin bloquear (salvo la fecha que fije cada cooperativa);
  - corrección por el administrador o el maestro;
  - fusión por el maestro;
  - cambio de correo con clave, segundo factor y código;
  - «enviar enlace» en lugar de «restablecer clave».
- **Usuarios existentes:** no se enteran. No hay claims nuevos ni se rota ningún sello; sus correos se
  dan por verificados.

Decisiones en [research.md](research.md) (R1–R16); esquema en [data-model.md](data-model.md); contratos
en [contracts/api.md](contracts/api.md); validación en [quickstart.md](quickstart.md); punto de partida en
[analisis-sistema-actual.md](analisis-sistema-actual.md) y [diagnostico-de-datos.md](diagnostico-de-datos.md).

## Technical Context

**Language/Version**: C# / .NET 10.0.5; Blazor (Shared, Web, WASM, MAUI).

**Primary Dependencies**: ya presentes. ASP.NET Core Identity (`IdentityUser<Guid>`, BCrypt costo 11),
Carter, MediatR, FluentValidation, EF Core (PostgreSQL y SQL Server), StackExchange.Redis (bloqueo),
AspNetCoreRateLimit (IP), `IEmailSender` (SMTP), auditoría Mongo. **No se agrega ninguna dependencia.**

**Storage**:
- Base administrativa: columnas nuevas en cuatro tablas `ADM_` y cuatro tablas nuevas.
- Cada cooperativa: `COR_People.Username`, `SEC_Users.PositionId` y `SEC_Users.CentralUsername`, y el
  cambio de índice único de personas.
- Redis: llaves de bloqueo por cuenta.

**Testing**: xUnit en Domain, Application, Architecture y Shared, más integración con Testcontainers
(PostgreSQL y SQL Server) al final.

**Target Platform**: API Linux en k3s (DEV, QA, PDN); clientes web y MAUI.

**Project Type**: web service + Blazor.

**Performance Goals**: el ingreso con usuario no es más lento que el de hoy con correo, porque es una
búsqueda por índice. La rama de cuenta inexistente cuesta lo mismo que la de clave errada.

**Constraints**:
- Regla de oro: 0 cuentas afectadas y 0 sesiones vivas cortadas.
- Una sola entrega antes del 01/12/2026.
- Nada de esto se despliega sin el diagnóstico de datos limpio y sin respaldo.

**Scale/Scope**:
- 6 tipos de documento de acceso, 2 más sólo para personas.
- 2 migraciones pares y una tarea de datos.
- Unas 20 rutas nuevas o cambiadas.
- 6 pantallas tocadas (ingreso, aceptar invitación, invitaciones, usuarios, alta de cooperativa, mi
  cuenta) y 2 nuevas (identidades y casos del maestro; política de identidad de la cooperativa).
- 11 importaciones y 8 búsquedas pasan por el resolvedor de documento.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec aprobada; este plan sigue; tasks después |
| II   | Clean Architecture | PASS | la regla del usuario y el catálogo son Domain puro; resolvedores y guardias en Application; Identity, Redis y EF en Infrastructure |
| III  | CQRS + MediatR | PASS | cada acción es un Command con validador; los endpoints sólo reenvían |
| IV   | Multi-tenancy | PASS | la identidad vive sólo en la base administrativa; nada `ADM_` se replica en cooperativas; el enlace es por id; la fusión y la tarea de datos recorren el directorio y abren cada conexión; ningún join entre bases |
| V    | Person centralizada | PASS | `COR_People` sigue siendo la maestra en la cooperativa; `Username` es derivado, no un dato duplicado; `SEC_Users.IdentificationNumber` deja de escribirse. La cuenta central guarda nombres y documento para entrar: es otra base y otro propósito, no una tabla hija |
| VI   | PublicId / Id | PASS | rutas con `PublicId` o con el Guid de la cuenta central (que ya es su identificador público); ningún `int` sale |
| VII  | Soft-delete + auditoría | PASS | las tablas nuevas heredan `AuditableEntity` con filtro; `ADM_DocumentHistory` es de sólo inserción |
| VIII | Validación dual | PASS | doble digitación y formato en pantalla y en el validador |
| IX   | Errores visibles | PASS | códigos de error propios; las guardas de migración fallan con la lista |
| X    | Trazabilidad | PASS | todo cambio de identidad se audita (R13) |
| XI   | Inmutabilidad contable | N/A | no toca movimientos |
| XII  | Migraciones idempotentes y reversibles | PASS con nota | aditivas, rellenos idempotentes, guardas. La reversa de `UsuarioDeLaPersona` vuelve a crear `UK_COR_People_TaxId` y **falla** si ya conviven dos tipos con el mismo número; el runbook lo dice. Respaldo previo obligatorio |

**Re-check post-diseño**: PASS. Ningún FAIL, así que no hay Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/017-login-documento-invitacion/
├── spec.md
├── analisis-sistema-actual.md
├── diagnostico-de-datos.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/api.md
├── checklists/requirements.md
└── tasks.md              # /speckit-tasks
```

### Source Code (lo que se toca)

```text
src/Core/IngenIA365ERP.Domain/
├── Common/Identidad/TiposDeDocumento.cs          # catálogo fijo (R2)
├── Common/Identidad/UsuarioDeDocumento.cs        # Normalizar / Derivar: único sitio
├── Entities/Admin/CentralUser.cs                 # + documento, nombres, EmailVerifiedAt
├── Entities/Admin/Invitation.cs, InvitationStatus.cs (+UnderReview)
├── Entities/Admin/TenantMembership.cs, MembershipStatus.cs (+UnderReview)
├── Entities/Admin/{DocumentHistoryEntry, IdentityCase, EmailChangeRequest, TenantIdentityPolicy}.cs
├── Entities/Core/Person.cs                       # + Username
└── Entities/Security/User.cs                     # + PositionId, CentralUsername

src/Core/IngenIA365ERP.Application/
├── Identity/Auth/ResolutorDeIdentificador.cs     # correo verificado | usuario (R5)
├── Identity/Auth/Login/*                         # identificador, tiempo homogéneo, bloqueo por cuenta
├── Identity/Auth/Documento/{RegistrarDocumento, GuardiaDeDocumento}.cs (R9)
├── Identity/Auth/StepUp/*, CambioDeCorreo/*      # R11
├── Identity/Auth/PasswordReset/*                 # forgot por identificador
├── Identity/Correccion/{CorregirDocumento, FusionarIdentidades}/*   # R10
├── Identity/Politica/*                           # ADM_TenantIdentityPolicies
├── Invitations/{IssueTenantInvitation, IssueMasterInvitation, CorrectInvitation,
│                AcceptInvitation, PreviewInvitation}/*              # R7, R8
├── Saas/RegisterTenantWithAdmin/*                # primer administrador con documento
├── Common/Services/TenantUserProvisioner.cs      # PersonId, rol, cargo, CentralUsername
├── Core/People/Services/{PersonFactory, ResolutorDePersonaPorDocumento}.cs  # R3
├── (11 importaciones y 8 consultas de R3)        # pasan por el resolvedor
├── Security/Users/{SendRecoveryLink (nuevo), AdminResetPassword (retirado), DisableUser}/*
└── Common/Audit/AuditEventTypes.cs

src/Infrastructure/
├── IngenIA365ERP.Identity/CentralIdentity/AspNetCoreIdentityProvider.cs   # búsquedas por usuario/documento
├── IngenIA365ERP.Caching/Services/Identity/RedisLoginAttemptCounter.cs    # llaves por cuenta
├── IngenIA365ERP.Persistence/Configurations/{Admin,Core,Security}/*       # columnas e índices
├── IngenIA365ERP.Persistence.Migrations.PostgreSql/{Admin,Application}/   # IdentidadConDocumento, UsuarioDeLaPersona
├── IngenIA365ERP.Persistence.Migrations.SqlServer/{Admin,Application}/    # ídem
├── IngenIA365ERP.Storage/Templates/*.html        # bienvenida con usuario, invitación a identidad existente,
│                                                  # usuario cambiado, código y aviso de cambio de correo, revisión
└── tools/IngenIA365ERP.DbMigrator                # tarea identidad-documento-desde-cooperativas

src/Presentation/
├── IngenIA365ERP.API/Endpoints/{CentralAuthModule, InvitationsModule, UsersModule, SaasAdminModule,
│                                AuthRecoveryModule, IdentidadesModule (nuevo)}.cs
└── IngenIA365ERP.Shared/
    ├── Pages/Security/Login.razor                # «Correo o usuario» + pista
    ├── Pages/Security/AcceptInvitation.razor     # paso de documento, usuario al final
    ├── Pages/Administracion/Tenants/Invitaciones.razor   # formulario completo, corregir, en revisión
    ├── Pages/Security/MasterRegisterTenant.razor # primer administrador con documento
    ├── Pages/Security/Users/{Index, Edit}.razor  # usuario, diferencia, corregir, enviar enlace
    ├── Pages/Security/MiCuenta.razor (nuevo)     # documento, cambio de correo
    ├── Pages/Security/PoliticaDeIdentidad.razor (nuevo)
    ├── Pages/Administracion/Identidades/*.razor (nuevo, maestro)
    ├── Components/Identidad/PasoDeDocumento.razor (aviso posponible en el layout)
    └── Services/Core/CatalogosDePersona.cs       # + PT (PPT)

tests/
├── IngenIA365ERP.Domain.Tests/Identidad/        # tabla de normalización y derivación
├── IngenIA365ERP.Application.Tests/Identity/...
├── IngenIA365ERP.Architecture.Tests/            # 4 pruebas nuevas (R15)
└── IngenIA365ERP.API.IntegrationTests/Identity/ # regla de oro, flujos, migrar dos veces
```

**Structure Decision**: la estructura de siempre, en 4 capas; nada nuevo a nivel de proyecto.

## Orden de construcción (una sola entrega, D-16)

1. **Base**: `TiposDeDocumento` + `UsuarioDeDocumento` con sus casos. Migraciones pares con guardas y
   rellenos. Configuración EF. Tarea de datos.
2. **Regla de oro**: el resolvedor del identificador, tiempo homogéneo y bloqueo por cuenta. Ingreso
   con `identifier`. `me` con `documentPending`. Prueba de regresión.
3. **Persona de cooperativa**: `Username` en `PersonFactory` y `UpdatePerson`, el resolvedor de
   documento y los 19 consumidores de R3, el código `PT`.
4. **Invitación y activación**: emitir, corregir, vista previa, aceptar con intentos, provisionador
   (persona, rol, cargo), primer administrador. Pantallas.
5. **Ingreso por usuario en pantalla** y pista.
6. **Completar documento**: el paso posponible, la guardia y la política por cooperativa.
7. **Administración**: enviar enlace (retira `reset-password`), corrección por el administrador,
   identidades y casos del maestro, fusión, cambio de correo con step-up, arreglos de `DisableUser`.
8. **Auditoría y correos**: eventos explícitos y plantillas.
9. **Cierre**: e2e en los dos motores, documentación (manual y runbook) y totales de `CLAUDE.md`.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Correos o documentos duplicados en un ambiente frenan la migración | Diagnóstico previo obligatorio; guardas con la lista; se resuelve con el dueño antes de desplegar |
| Algún lector del documento solo no se migra y lanza ante CC/CE iguales | Resolvedor único y prueba `NadieBuscaPersonasPorNumeroSolo`; revisar 015 y 016 al integrar |
| Un cliente MAUI viejo manda `email` | El login y `forgot` siguen aceptándolo |
| Un bloqueo en curso se libera al desplegar | Aceptado (máximo una hora, una vez) |
| Calendario de 4 semanas con todo el alcance | Orden por ruta crítica; si no alcanza, proponer al dueño mover fusión y cambio de correo (R16) |
| La reversa de `UsuarioDeLaPersona` falla si ya conviven dos tipos con el mismo número | Runbook explícito; el respaldo previo es la reversa real |
