<!--
SYNC IMPACT REPORT
==================
Version change: (uninitialized template) → 1.0.0
Bump rationale: Initial ratification — all twelve principles, technical standards,
and governance rules established for the first time.

Modified principles: N/A (initial adoption — twelve principles created)
  I.    Spec-First Development                                  [NEW]
  II.   Clean Architecture estricta                             [NEW]
  III.  CQRS + MediatR para Application                         [NEW]
  IV.   Multi-tenancy schema-per-tenant                         [NEW]
  V.    Person como tabla maestra centralizada                  [NEW]
  VI.   PublicId hacia afuera, Id interno hacia adentro         [NEW]
  VII.  Soft-delete + auditoría obligatorias                    [NEW]
  VIII. Validación dual — cliente + servidor                    [NEW]
  IX.   Errores visibles, nunca silenciados                     [NEW]
  X.    Trazabilidad regulatoria financiera (SIPLA / SARLAFT)   [NEW]
  XI.   Inmutabilidad de movimientos contables                  [NEW]
  XII.  Migraciones idempotentes y reversibles                  [NEW]

Added sections:
  - Estándares Técnicos Adicionales (convenciones operativas, no principios)
  - Flujo de Desarrollo y Compuertas de Calidad
  - Governance (enmiendas, versionado, revisión de cumplimiento)

Removed sections: N/A

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check ampliada con
     las doce compuertas (gates) derivadas de los principios.
  ✅ .specify/templates/spec-template.md — sin cambios estructurales (compatible).
  ✅ .specify/templates/tasks-template.md — sin cambios estructurales (compatible).
  ✅ CLAUDE.md — añadida referencia a la constitución.
  ✅ docs/INDICE-DOCUMENTACION.md — constitución listada como entrada de Fase 0.

Follow-up TODOs: ninguno. Todos los placeholders se concretaron.
-->

# IngenIA365ERP Constitution

> ERP financiero SaaS multi-tenant para cooperativas colombianas.
> Stack: .NET 10, Blazor Hybrid (MAUI + Server + WebAssembly), SyncFusion 33.1.44,
> SQL Server (transaccional, schema-per-tenant), MongoDB (auditoría), Redis (caché),
> Carter (Minimal APIs), MediatR (CQRS), QuestPDF (reportes), JWT RS256, ASP.NET Identity.
>
> Idioma: documentación, comentarios de negocio y mensajes al usuario en español;
> código fuente, identificadores y nombres de símbolos en inglés.

## Core Principles

### I. Spec-First Development

Todo cambio que afecte más de una capa de la Clean Architecture o que modifique el
schema de base de datos **MUST** pasar por la secuencia Constitution check →
Specification → Plan → Tasks antes de escribir código de producción.

Excepciones explícitas (no requieren spec previa): bug fixes locales contenidos en
un único archivo o método, ajustes cosméticos de UI sin cambio de comportamiento, y
actualizaciones de dependencias menores (PATCH semver de paquetes existentes).

**Rationale**: los cambios sin spec generan bugs en cascada cuyo costo de
diagnóstico y reversión es siempre mayor que el costo del spec mismo. El spec
obliga a explicitar contratos antes de codificarlos.

### II. Clean Architecture estricta

Cuatro capas con dependencia unidireccional hacia adentro:
`Domain ← Application ← Infrastructure` y `Domain ← Application ← Presentation`.

- El proyecto **Domain NEVER** importa EF Core, Carter, SyncFusion, MongoDB.Driver,
  ASP.NET Core ni ninguna otra dependencia de infraestructura o presentación.
- **Application** solo referencia Domain (más abstracciones puras: MediatR,
  FluentValidation, Result types).
- **Presentation NEVER** referencia el proyecto de Persistence ni proyectos de
  Infrastructure directamente; toda interacción ocurre a través de Application
  (vía `ISender` de MediatR).
- Las violaciones se detectan automáticamente en `Architecture.Tests` mediante
  ArchUnitNET; un test rojo bloquea el merge.

**Rationale**: testabilidad de Domain y Application sin spin-up de infraestructura,
sustituibilidad de adapters (SQL Server → otro motor, Carter → otro host),
alineación con la documentación de arquitectura del proyecto.

### III. CQRS + MediatR para todo Application

Toda operación en Application se modela como mensaje MediatR:

- **Lectura**: `IRequest<Result<T>>` (Query). Puede usar proyecciones EF, JOINs,
  vistas materializadas — la query es libre de optimizarse para lectura.
- **Escritura**: `IRequest<Result>` o `IRequest<Result<Guid>>` (Command).
  Debe respetar invariantes de Domain.

Reglas adicionales:

- Carter endpoints **SHALL** únicamente reenviar al `ISender` (mapear request →
  command/query, invocar `_sender.Send(...)`, traducir `Result` a respuesta HTTP).
  Ningún endpoint contiene lógica de negocio.
- Los pipeline behaviors `ValidationBehavior`, `AuditBehavior`, `LoggingBehavior`
  y `PerformanceBehavior` se aplican automáticamente y **NEVER** pueden saltarse
  ejecutando handlers directamente.
- Todo `Command` **MUST** tener un `FluentValidation` validator hermano en el
  mismo namespace (mismo archivo o archivo `*Validator.cs` colateral).

**Rationale**: separar lectura/escritura permite optimizar queries sin contaminar
la lógica de comandos; los pipelines garantizan que validación, auditoría,
logging y métricas se apliquen de forma uniforme sin posibilidad de olvido.

### IV. Multi-tenancy schema-per-tenant — inviolable

Toda query EF Core **MUST** ejecutarse en el contexto del tenant resuelto por
`TenantResolutionMiddleware` (header `X-Tenant-Id` o resolución por subdominio).
La base de datos `IngenIA365ERP_Admin` (tablas con prefijo `ADM_`) está separada
del schema operativo de cada cooperativa.

Prohibiciones absolutas:

- **NEVER** cross-tenant joins de ningún tipo.
- **NEVER** queries sin tenant context (incluye background jobs: deben recibir
  el tenant explícitamente).
- **NEVER** `SqlConnection` directo ni Dapper raw que evite los filtros del
  `DbContext` tenant-aware.

**Rationale**: requisito legal — los datos financieros de cooperativas distintas
**NEVER** pueden mezclarse, y la trazabilidad de un fallo de aislamiento sería
catastrófica frente a la Superintendencia de la Economía Solidaria.

### V. Person como tabla maestra centralizada

`COR_People` es la fuente única de verdad para datos personales: nombre,
documento, contacto, demografía, datos fiscales/contables, banca proveedor/cliente
y flags de rol.

Los roles viven en tablas hijas con FK obligatoria a `Person.Id`:

- `COR_Associates` (afiliación + empleo externo + cónyuge laboral + scoring +
  banca depósito)
- `PAY_Employees` (datos laborales internos + banca nómina)
- `COR_Spouses` (datos personales del cónyuge)
- `COR_PeopleFinancial` (scoring crediticio)
- `INV_Salespeople` (rol vendedor)

Reglas:

- **NEVER** duplicar `FirstName`, `LastName`, `TaxId`, `Email`, `Phone` ni
  `Address` en tablas hijas.
- Una persona puede tener múltiples roles simultáneamente.
- Los flags `IsAssociate`, `IsEmployee`, `IsCustomer`, `IsSupplier`, `IsAdvisor`,
  `IsSalesperson`, `IsThirdParty`, `ReceivesInvoice` en `COR_People` **MUST**
  mantenerse en sincronía con la existencia de las filas hijas (la sincronía es
  responsabilidad del handler que crea/elimina la fila hija).

**Rationale**: prevenir inconsistencia de datos personales — si un asociado
cambia su email en `/maestros/personas`, debe verse fresco al instante en las
vistas de empleados, asociados, vendedores y proveedores sin reconciliación
manual.

### VI. PublicId hacia afuera, Id interno hacia adentro

Toda entity expone:

- `int Id` — PK interna autoincremental. **NEVER** aparece en JSON, URLs, logs
  públicos, MongoDB de auditoría externa, ni en el frontend.
- `Guid PublicId` — **NEVER** ausente; es lo único que viaja en HTTP, cookies,
  logs operativos, MongoDB y URLs del frontend.

Reglas estrictas:

- Los endpoints **NEVER** aceptan `int Id` como parámetro de ruta, query o body.
- Los DTOs **NEVER** devuelven `int Id`.
- Los handlers traducen `PublicId` (recibido) a `Id` interno mediante lookup
  contra el `DbContext`.

**Rationale**: ofuscación contra ataques de enumeración secuencial, portabilidad
entre BDs (un `Id` 17 en SQL Server puede ser 23 tras migración; el `PublicId` no
cambia), y consistencia de identificación frontend ↔ backend ↔ auditoría.

### VII. Soft-delete + auditoría obligatorias

Toda entity **MUST** heredar de `AuditableEntity` y registrar:
`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `DeletedAt`,
`DeletedBy`.

- El DELETE en código de producción es siempre soft: `IsDeleted = true` +
  `DeletedAt = UtcNow` + `DeletedBy = currentUser`.
- Toda EF Core `Configuration` **MUST** declarar
  `builder.HasQueryFilter(e => !e.IsDeleted)`.
- **NEVER** `DELETE FROM` directo en código de producción (ni Dapper, ni
  `context.Database.ExecuteSqlRaw`, ni stored procedure hard-delete).
- **NEVER** entities sin `AuditableEntity`, salvo lookup catalogs muy simples
  cuya inmutabilidad y ausencia de auditoría se justifique explícitamente en la
  spec de la feature que los introduce.

**Rationale**: trazabilidad regulatoria, recuperación ante error humano (un
asociado borrado por equivocación puede restaurarse sin restore de backup).

### VIII. Validación dual — cliente + servidor

Toda operación de escritura tiene dos capas de validación:

1. **Frontend Blazor**: `EditForm` con `DataAnnotations` o validación manual con
   toasts. Su objetivo es UX — bloquear envíos inválidos antes del round-trip.
2. **Backend Application**: `FluentValidation` en el validator del Command, vía
   `ValidationBehavior`. Es la **fuente de verdad legal** — lo que el backend
   rechaza, el frontend debió haberlo bloqueado antes.

Reglas para mensajes:

- En español, claros y accionables ("El documento de identidad ya está
  registrado por otra persona", no "TaxId duplicate").
- Códigos de error con namespace (ej. `Person.TaxIdDuplicate`,
  `Loan.AmountExceedsLimit`).
- Los errores del backend se desempaquetan en el frontend vía
  `Notification.ShowErrorAsync(res, fallback)`. **NEVER** mostrar toast genérico
  tipo "Error 400" o "Algo salió mal".

**Rationale**: defense-in-depth (un cliente malicioso puede saltarse la
validación frontend) y mejor UX (no esperar al servidor para errores triviales).

### IX. Errores visibles, nunca silenciados

El patrón `try { ... } catch { _items = []; }` (o equivalentes que oculten la
excepción sin registrar ni notificar) está **PROHIBIDO** en código nuevo.

Toda excepción **MUST**:

1. Loguearse mediante Serilog con contexto suficiente (tenant, usuario,
   operación, parámetros relevantes — sin PII innecesaria).
2. Mostrarse al usuario con mensaje accionable
   (`Notification.ErrorAsync(ex.Message)` o equivalente).
3. Dejar el sistema en estado conocido (sin colecciones a medio poblar, sin
   formularios en estado intermedio).

Única excepción aceptable: `TaskCanceledException` proveniente de un debounce
controlado (puede ignorarse silenciosamente — es flujo normal).

**Rationale**: los errores silenciados causan bugs invisibles que cuestan días
diagnosticar — el incidente histórico del helper paginado ("no veo registros"
sin error visible) es la lección aprendida.

### X. Trazabilidad regulatoria financiera (SIPLA / SARLAFT)

Toda operación monetaria, alta/baja de asociado, modificación de salario,
exoneración de SIPLA y registro de beneficiario final **MUST** quedar registrada
en MongoDB de auditoría vía `AuditBehavior`, con:

- `Timestamp` en UTC.
- `UserId` y `Username` del actor.
- `TenantId` (cooperativa).
- Tipo de operación (Command type).
- Entidad afectada (tipo + `PublicId`).
- Valores antes/después (diff de los campos modificados).
- IP de origen y User-Agent.

Retención TTL **mínima de 5 años** por norma SARLAFT (la colección de auditoría
configura el TTL en consecuencia).

**NEVER** comandos que escriban datos sensibles fuera del pipeline MediatR — eso
saltaría el `AuditBehavior` y perdería la traza.

**Rationale**: compliance con la Superintendencia de la Economía Solidaria de
Colombia y con SARLAFT; la auditoría es la única defensa frente a un
requerimiento regulatorio posterior.

### XI. Inmutabilidad de movimientos contables

Las transacciones contables ya asentadas son inmutables:

- `ACC_JournalEntries`
- `ACC_AccountBalances`
- `LND_LoanTransactions`
- `PAY_PayrollTransactions`

Reglas:

- **NEVER** editar ni borrar (ni soft, ni hard) un movimiento contable asentado
  como flujo de usuario.
- Para corregir un error → asentar un **movimiento reverso** con referencia al
  original (`ReversesEntryId` / `ReversesTransactionId`). Saldos y reportes
  reflejan siempre la suma neta.
- El soft-delete de estas tablas solo se permite por **mantenimiento técnico
  autorizado** (script de recuperación documentado, ejecutado por administrador
  con justificación escrita), **NEVER** como flujo de usuario o de aplicación.

**Rationale**: trazabilidad fiscal y auditoría externa; la contabilidad no
admite "cambiar de opinión" — el principio universal de partida doble exige que
todo asiento quede para la historia.

### XII. Migraciones idempotentes y reversibles

Toda migración SQL ubicada en `database/migration/` **MUST**:

1. Ser **idempotente** — ejecutable múltiples veces sin error:
   `IF NOT EXISTS`, `IF EXISTS`, `MERGE`, comprobaciones previas.
2. Estar **documentada** con header que indique contexto, problema que resuelve
   y modo de ejecución.
3. Ser **reversible** o declararse explícitamente irreversible. Las migraciones
   destructivas **MUST** declarar que requieren backup previo.
4. Estar **numerada secuencialmente** (`13_X.sql`, `13b_Y.sql` para
   complementarias dentro del mismo lote).
5. **NEVER** mezclar DDL con datos — schema (`database/schema/`) y seeds
   (`database/seed/`) son archivos separados; migración de datos
   (`database/migration/`) tampoco mezcla DDL con DML en el mismo archivo si
   ello impide la idempotencia.

Antes de ejecutar migraciones destructivas en BD productiva: **backup
completo + revisión de un segundo desarrollador**, ambos documentados.

**Rationale**: las migraciones 11, 12, 13a, 13b, 13c demostraron en producción
el valor de la idempotencia — un script idempotente se puede reintentar tras
un fallo parcial sin temor a duplicar o corromper datos.

## Estándares Técnicos Adicionales

Convenciones operativas vinculantes (no son principios; un cambio aquí no exige
bump MAJOR pero sí PR y revisión):

- **Entities**: PascalCase singular (`Person`, `Employee`, `JournalEntry`).
- **Tablas SQL**: prefijo de módulo: `COR_`, `ACC_`, `LND_`, `PAY_`, `INV_`,
  `CDT_`, `DEB_`, `TRS_`, `SEC_`, `AUD_`, `WEB_`, `ADM_`.
- **Endpoints REST**: `/api/{module}/{plural-kebab}`
  (ej. `/api/core/people`, `/api/payroll/employees`).
- **Commands / Queries**: verbo en infinitivo + entidad
  (`RegisterEmployeeCommand`, `ListEmployeesQuery`, `GetPersonByPublicIdQuery`).
- **DTOs**: sufijo `Dto`, `record` con `init` properties.
- **Razor pages**: español si UI de usuario final; inglés si pantalla de
  administración o herramienta interna.
- **FKs en DTOs**: salida expone `PublicId`; entrada recibe `PublicId` y el
  handler lo resuelve a `Id` interno.
- **Buscador de personas**: usar siempre `<PersonSearchPicker>` (componente
  compartido); **NEVER** reinventar buscadores ad-hoc.
- **Anti doble-click**: flag `_saving` en cada formulario de escritura.
- **Seguridad**: toda ruta endpoint **MUST** declarar
  `.RequireAuthorization(...)`. HTTPS obligatorio. Passwords almacenadas con
  BCrypt cost ≥ 11.
- **Tests**:
  - `Domain.Tests` — pruebas unitarias puras de Domain.
  - `Application.Tests` — validators y handlers con NSubstitute.
  - `API.IntegrationTests` — endpoints contra BD efímera.
  - `Architecture.Tests` — ArchUnitNET valida el Principio II.

## Flujo de Desarrollo y Compuertas de Calidad

- Toda feature significativa pasa por: Constitution check → `/speckit-specify`
  → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`.
- El `plan-template.md` incluye una sección Constitution Check con doce
  compuertas (una por principio); cualquier violación se documenta en
  `Complexity Tracking` con justificación o se rechaza el plan.
- En cada PR significativo el reviewer **MUST** verificar adhesión a los doce
  principios.
- Una violación deliberada de un principio requiere justificación explícita en
  la descripción del PR **Y** aprobación del líder técnico.

## Governance

**Procedimiento de enmienda**

- Las enmiendas se proponen vía issue con título
  `[Constitution] Propuesta: <resumen>`, incluyendo:
  1. Motivación (qué problema resuelve la enmienda).
  2. Impacto (qué código existente queda afectado).
  3. Plan de migración (cómo se actualiza lo existente).
- Se aprueban por consenso de **mínimo dos revisores**. Discrepancias se
  elevan al líder técnico, cuya decisión es definitiva.

**Versionado del documento (Semantic Versioning)**

- **MAJOR**: eliminación o redefinición de un principio que rompe código o
  invalida prácticas vigentes.
- **MINOR**: nuevo principio o expansión material de uno existente (nuevas
  reglas declarativas que añaden obligaciones).
- **PATCH**: clarificaciones, correcciones de redacción o errata, sin cambio
  semántico.

**Revisión de cumplimiento**

- En cada PR significativo el reviewer verifica adhesión a los doce principios
  (lista en la PR template).
- **Revisión trimestral** del estado de cumplimiento para detectar drift, a
  cargo del líder técnico, con informe breve en `docs/`.

**Relación con otros documentos**

- `CLAUDE.md` (raíz) describe el estado del proyecto y **MUST** referenciar
  esta constitución.
- `docs/INDICE-DOCUMENTACION.md` lista esta constitución como entrada de
  **Fase 0 — Fundamentos**.
- En caso de contradicción entre cualquier documento del repositorio y esta
  constitución, **gana esta constitución** hasta que sea formalmente
  enmendada por el procedimiento descrito arriba.

**Version**: 1.0.0 | **Ratified**: 2026-05-03 | **Last Amended**: 2026-05-03
