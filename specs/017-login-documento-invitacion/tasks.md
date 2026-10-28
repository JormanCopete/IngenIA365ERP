# Tasks: Ingreso con correo o usuario de documento e invitaciones ancladas al documento

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/api.md](contracts/api.md), [quickstart.md](quickstart.md)

**Tests**: obligatorias, porque las pide el borrador del dueño («Pruebas obligatorias») y research R15.
Las unitarias y de arquitectura van dentro de cada historia. Las de integración (e2e) van **todas al
final**, en la fase 11, como en las features recientes.

**Formato**: `- [ ] Txxx [P] [USn] descripción con ruta`. `[P]` = puede hacerse en paralelo (otro archivo,
sin dependencias pendientes).

**Abreviaturas de rutas**:

| Abreviatura | Ruta |
|---|---|
| `Dom/` | `src/Core/IngenIA365ERP.Domain/` |
| `App/` | `src/Core/IngenIA365ERP.Application/` |
| `Per/` | `src/Infrastructure/IngenIA365ERP.Persistence/` |
| `MigPg/` | `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/` |
| `MigSql/` | `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/` |
| `Idn/` | `src/Infrastructure/IngenIA365ERP.Identity/` |
| `Api/` | `src/Presentation/IngenIA365ERP.API/` |
| `Sh/` | `src/Presentation/IngenIA365ERP.Shared/` |
| `T.Dom/`, `T.App/`, `T.Arq/`, `T.Int/` | `tests/IngenIA365ERP.{Domain,Application,Architecture,API.Integration}Tests/` |

---

## Phase 1: Setup

- [X] T001 Confirmar con el dueño las longitudes mínima y máxima por tipo de documento de research R2 (tabla) y anotarlas en specs/017-login-documento-invitacion/research.md — *confirmadas por el dueño el 2026-10-28 (D-17)*
- [X] T002 [P] Crear las carpetas `Dom/Common/Identidad/`, `App/Identity/Auth/Documento/`, `App/Identity/Correccion/`, `App/Identity/Politica/`, `App/Identity/Auth/CambioDeCorreo/` y `T.Dom/Identidad/`, cada una con su primer archivo según las tareas siguientes (sin archivos vacíos)
- [X] T003 [P] Registrar en specs/017-login-documento-invitacion/research.md (sección «Coordinación») qué ramas en curso (015, 016) tocan `COR_People.TaxId` o importaciones, con `git log origin/015-recaudo-tesoreria --name-only` en modo sólo lectura

---

## Phase 2: Foundational (bloquea a todas las historias)

**Propósito**: la regla del usuario, el esquema y los resolvedores que usan todas las historias.

### Regla del usuario y catálogo (Domain puro)

- [X] T004 [P] Crear el catálogo fijo `TiposDeDocumento` (código de acceso, código `COR_People.IdType`, prefijo, letras, longitudes, si sirve para entrar, código DIAN) con CC, CE, PPT, PEP, TI, PA, RC y NIT según research R2, en Dom/Common/Identidad/TiposDeDocumento.cs
- [X] T005 Crear `UsuarioDeDocumento.Normalizar(tipo, número)` y `UsuarioDeDocumento.Derivar(tipo, númeroNormalizado)`, más `NormalizarIdentificador(texto)` para el ingreso (mayúsculas, sin espacios, puntos ni guiones, quita una `CC` inicial si lo que sigue son dígitos), en Dom/Common/Identidad/UsuarioDeDocumento.cs
- [X] T006 [P] Pruebas en tabla de normalización y derivación: cédula con puntos y espacios, con `CC` escrito, CE, PPT, PEP, TI, PA alfanumérico en minúsculas, CC 123456 frente a CE 123456, tipo sin letras con letras (rechazo), longitudes; en T.Dom/Identidad/UsuarioDeDocumentoTests.cs
- [X] T007 [P] Prueba de arquitectura `LosPrefijosDelUsuarioNoSonPrefijoDeOtro` (ningún prefijo es comienzo de otro y el prefijo vacío sólo admite dígitos) en T.Arq/Identidad/LosPrefijosDelUsuarioNoSonPrefijoDeOtro.cs
- [X] T008 [P] Prueba de arquitectura `ElUsuarioSeDerivaEnUnSoloSitio` (nadie fuera de `UsuarioDeDocumento` concatena tipo y número ni asigna `DocumentUsername`/`Username` de persona) en T.Arq/Identidad/ElUsuarioSeDerivaEnUnSoloSitio.cs

### Entidades y configuración

- [X] T009 Agregar a `CentralUser` `DocumentType`, `DocumentNumber`, `DocumentUsername`, `FirstName`, `OtherNames`, `LastName`, `SecondLastName` y `EmailVerifiedAt`, más un método `RegistrarDocumento(tipo, número)` que deriva el usuario, en Dom/Entities/Admin/CentralUser.cs; y las mismas propiedades en `CentralUserIdentity` y en su `ToDomain`/`FromDomain` dentro de Per/Identity/CentralUserIdentity.cs e Idn/CentralIdentity/AspNetCoreIdentityProvider.cs
- [X] T010 [P] Agregar `UnderReview = 5` a Dom/Entities/Admin/InvitationStatus.cs. Agregar a `Invitation` `CentralUserId`, documento, nombres, `MobilePhone`, `RolePublicId`, `PositionPublicId`, `DocumentAttempts`, `MaxDocumentAttempts`, `TypedEmail` y los métodos `RegistrarIntentoFallido()` y `PasarARevision()` en Dom/Entities/Admin/Invitation.cs
- [X] T011 [P] Agregar `UnderReview = 4` a Dom/Entities/Admin/MembershipStatus.cs. Agregar `ReviewRequestedAt`, `ReviewReason` y la transición `PasarARevision(motivo)` en Dom/Entities/Admin/TenantMembership.cs
- [X] T012 [P] Agregar `Identifier` e `IdentifierKind` a Dom/Entities/Admin/CentralUserLoginAttempt.cs
- [X] T013 [P] Crear las entidades nuevas `DocumentHistoryEntry` (sólo inserción), `IdentityCase`, `EmailChangeRequest` y `TenantIdentityPolicy` según data-model.md, en Dom/Entities/Admin/{DocumentHistoryEntry,IdentityCase,EmailChangeRequest,TenantIdentityPolicy}.cs
- [X] T014 [P] Agregar `Username` a Dom/Entities/Core/Person.cs y `PositionId` y `CentralUsername` a Dom/Entities/Security/User.cs
- [X] T015 Configurar en Per/Configurations/Admin/CentralUserIdentityConfiguration.cs las columnas nuevas y los índices `UX_ADM_CentralUsers_Document`, `UX_ADM_CentralUsers_DocumentUsername` y `UX_ADM_CentralUsers_NormalizedEmail` (únicos, filtrados, iguales en los dos motores)
- [X] T016 [P] Configurar las columnas nuevas de invitación, membresía e intentos en Per/Configurations/Admin/{InvitationConfiguration,TenantMembershipConfiguration}.cs y en la configuración de `CentralUserLoginAttempt` bajo Per/Configurations/Admin/
- [X] T017 [P] Crear Per/Configurations/Admin/{DocumentHistoryEntryConfiguration,IdentityCaseConfiguration,EmailChangeRequestConfiguration,TenantIdentityPolicyConfiguration}.cs con filtros de borrado y registrar los `DbSet` en el contexto administrativo bajo Per/
- [X] T018 [P] En Per/Configurations/Core/PersonConfiguration.cs, configurar `Username` (24) con `UK_COR_People_Username` único sin filtro y retirar `UK_COR_People_TaxId`. En Per/Configurations/Security/UserConfiguration.cs, configurar `PositionId` (FK a `COR_Positions`) y `CentralUsername` (24)

### Migraciones (Principio XII)

- [X] T019 Generar la migración par `IdentidadConDocumento` (Admin) en MigPg/Admin/ y MigSql/Admin/, más SQL a mano para:
  - normalizar de forma idempotente `Email`/`UserName` (quitar espacios alrededor) y recalcular `NormalizedEmail`/`NormalizedUserName` donde difieran (FR-047, diagnóstico A2);
  - la guarda que falla con la lista de correos duplicados (diagnóstico A1) antes de crear `UX_ADM_CentralUsers_NormalizedEmail`;
  - el relleno idempotente de R4: cuentas no borradas, `Active` y con clave → `EmailConfirmed = true`, `EmailVerifiedAt = now`, sin tocar `SecurityStamp` ni `ConcurrencyStamp`;
  - el `Down` que retira sólo lo agregado.
- [X] T020 Generar la migración par `UsuarioDeLaPersona` (Application) en MigPg/Application/ y MigSql/Application/, más SQL a mano para:
  - agregar la columna nula;
  - rellenar `Username` con la misma regla de T005 (expresión SQL equivalente por motor, comprobada por T023);
  - la guarda que falla con la lista si dos personas chocan (diagnóstico B6);
  - después, `NOT NULL`, crear `UK_COR_People_Username` y retirar `UK_COR_People_TaxId`;
  - el `Down` que vuelve a crear `UK_COR_People_TaxId` y falla con un mensaje claro si ya conviven dos tipos con el mismo número.
- [X] T021 Crear la tarea de una sola vez `identidad-documento-desde-cooperativas` en tools/IngenIA365ERP.DbMigrator/:
  - recorre el directorio de cooperativas y abre la conexión de cada una (Principio IV);
  - si todas las cooperativas de una cuenta tienen la misma persona enlazada con el mismo usuario, sube el documento y los nombres a la cuenta central y registra el historial con motivo `Migration`;
  - si no coinciden, escribe la cuenta en el reporte de conflictos (archivo CSV en la carpeta de salida);
  - correrla dos veces da lo mismo.
- [ ] T022 [P] Prueba de que el SQL de relleno de `Username` y `UsuarioDeDocumento.Derivar` dan lo mismo sobre la tabla de casos de T006, en T.App/Identity/RellenoDeUsuarioEquivaleALaReglaTests.cs o, si el relleno sólo se puede probar contra el motor, como parte de la e2e T091 — *pendiente: va en la e2e de migración (T091)*
- [ ] T023 [P] Prueba de arquitectura de que los `Down` de las dos migraciones no borran columnas con datos previos al despliegue, en T.Arq/Migraciones/LasMigracionesDe017SonAditivas.cs — *pendiente*

### Resolvedores compartidos

- [X] T024 Crear `ResolutorDeIdentificador` (texto → cuenta o nada; con `@` exige `EmailConfirmed`; sin `@` busca `DocumentUsername` exacto) en App/Identity/Auth/ResolutorDeIdentificador.cs, con sus métodos de búsqueda por usuario y documento en App/Common/Interfaces/Identity/ICentralIdentityProvider.cs e Idn/CentralIdentity/AspNetCoreIdentityProvider.cs
- [X] T025 [P] Pruebas del resolvedor: correo verificado, correo sin verificar, usuario con y sin `CC`, inexistente; en T.App/Identity/Auth/ResolutorDeIdentificadorTests.cs
- [X] T026 Crear `ResolutorDePersonaPorDocumento` (tipo opcional y número; sin tipo con varias personas responde `Person.DocumentAmbiguous` con los tipos) en App/Common/Personas/ResolutorDePersonaPorDocumento.cs
- [X] T027 En App/Core/People/Services/PersonFactory.cs, derivar `Username` al crear y comparar la colisión por `Username`, conservando `Person.TaxIdDuplicate`/`TaxIdDeleted` y la constante del índice nuevo. Lo mismo al cambiar tipo o número en App/Core/People/Commands/UpdatePerson/UpdatePersonCommand.cs. Normalizar `TaxId` en el servidor sólo para lo nuevo (validador en App/Core/People/Contracts/PersonInput.cs)
- [X] T028 [P] Agregar el código `PT` (PPT) a Sh/Services/Core/CatalogosDePersona.cs, al validador de `IdType` en App/Core/People/Contracts/PersonInput.cs y al mapeo DIAN (`PT → 48`) en App/ElectronicInvoicing/Catalogs/Data/tipos-de-identificacion.json
- [X] T029 [P] Pruebas de `PersonFactory` y del resolvedor de persona: CC y CE con el mismo número conviven, la eliminada bloquea, la ambigüedad pide el tipo; en T.App/Core/People/UsuarioDeLaPersonaTests.cs
- [X] T030 Pasar por `ResolutorDePersonaPorDocumento` las consultas que buscan por número solo:
  - App/Core/People/Queries/GetPersonByDocumentQuery.cs (con `type` opcional en la ruta `/by-document` de Api/Endpoints/Core/PeopleDetailEndpoints.cs);
  - App/Inventory/Sales/SalesDraftCommands.cs, App/Inventory/Sales/CreditoEnLaVenta.cs, App/Inventory/Pos/BorradorDelPos.cs, App/Inventory/Purchasing/PrefillSupplierInvoiceQuery.cs;
  - App/Lending/Payroll/ConveniosDeLibranza.cs, App/Lending/Loans/ListLoansQuery.cs;
  - Per/Seeding/Parametric/ConsumidorFinalSeeder.cs.
- [X] T031 Pasar por el resolvedor, con columna de tipo **opcional** en la plantilla, las importaciones que arman diccionarios por `TaxId`:
  - App/Accounting/Opening/OpeningCommands.cs;
  - App/Inventory/Reports/Vistas/PurchaseSuggestionReportQuery.cs;
  - App/Inventory/Salespeople/Import/ImportSalespeopleCommand.cs;
  - App/Lending/Deposits/Assembly/ImportAssemblyReturnBasesCommand.cs;
  - App/Lending/Deposits/Migration/ImportDepositMigrationPartCommand.cs;
  - App/Lending/Imports/{ImportCreditLinesCommand,ImportRelatedPartiesCommand,PayrollAgreementsTemplate}.cs;
  - App/Lending/Migration/ImportMigrationPartCommand.cs;
  - App/Lending/Origination/CreditLimits/ContributionBalanceCommands.cs;
  - App/Payroll/Novelties/ImportNovelties/ImportNoveltiesCommand.cs.
- [X] T032 [P] Prueba de arquitectura `NadieBuscaPersonasPorNumeroSolo` (prohíbe `TaxId ==` y `ToDictionary/ToLookup/GroupBy` por `TaxId` fuera del resolvedor y de `PersonFactory`) en T.Arq/Identidad/NadieBuscaPersonasPorNumeroSolo.cs
- [X] T033 [P] Agregar los eventos nuevos de contracts/api.md §6 a App/Common/Audit/AuditEventTypes.cs y los códigos de error en un archivo de errores de identidad en App/Identity/IdentityErrors.cs
- [X] T033a [P] Crear el emisor de auditoría de identidad según research R13, en App/Identity/AuditoriaDeIdentidad.cs:
  - en la base global `{prefijo}_Global`, lo que no es de una cooperativa (documento, correo, fusión, rescate);
  - en la de la cooperativa, por su PublicId, lo que sí lo es (invitación, revisión, corrección del administrador, enlace de recuperación).

**Checkpoint**: compila, las migraciones aplican y se revierten en los dos motores, y las pruebas de Domain, Application y Arquitectura de esta fase pasan.

---

## Phase 3: User Story 1 — Quien ya usa el sistema sigue entrando igual (P1) 🎯 MVP

**Goal**: el ingreso pasa por el resolvedor sin que una cuenta existente note diferencia y sin cortar sesiones vivas.

**Independent Test**: quickstart §1. Base de antes del despliegue, migrar, entrar con correo y clave, y renovar una sesión abierta antes.

- [X] T034 [US1] Agregar `Identifier` a App/Identity/Auth/Login/LoginCommand.cs (con `Email` como alias) y exigir formato de correo sólo si lleva `@` en App/Identity/Auth/Login/LoginCommandValidator.cs. En Api/Endpoints/CentralAuthModule.cs, aceptar `identifier` y `email` en `LoginBody`
- [X] T035 [US1] En App/Identity/Auth/Login/LoginCommandHandler.cs:
  - reemplazar `FindByEmailAsync` por `ResolutorDeIdentificador`;
  - en la rama inexistente, verificar contra un hash BCrypt fijo de costo 11 antes de responder;
  - devolver `data.hint` con los prefijos del catálogo;
  - guardar `Identifier` e `IdentifierKind` en el intento.
- [X] T036 [US1] Cambiar las llaves de bloqueo a `intentos|bloqueo:{scope}:u:{centralUserId}` o `:i:{normalizado}` en src/Infrastructure/IngenIA365ERP.Caching/Services/Identity/RedisLoginAttemptCounter.cs, con la firma de App/Common/Interfaces/Identity/ILoginAttemptCounter.cs. Usar la misma llave en el MFA verify (App/Identity/Auth/MfaVerify/ o donde esté `MfaVerifyCommandHandler`)
- [X] T037 [US1] Agregar `documentPending`, `documentUsername` y `documentRequiredFrom[]` a App/Identity/Auth/Me/GetMeQuery.cs y App/Identity/Auth/Me/GetMeQueryHandler.cs
- [X] T038 [P] [US1] Pruebas del handler de login: correo existente igual que antes; la rama inexistente llama al verificador; mensaje y código iguales; bloqueo común al alternar correo y usuario; en T.App/Identity/Auth/Login/LoginConIdentificadorTests.cs
- [ ] T039 [P] [US1] Prueba de que el relleno de verificación (T019) no cambia `SecurityStamp` y de que la renovación no exige claims nuevos, en T.App/Identity/Auth/ReglaDeOroTests.cs — *cubierta por la e2e `IngresoConDocumentoE2ETests` (el maestro, cuenta de antes, entra igual) y por las e2e existentes de ingreso; falta la de sesión viva (T097)*
- [X] T040 [US1] Prueba de arquitectura `NadieBuscaCuentasPorCorreoFueraDelResolutor` (`FindByEmailAsync` sólo en el resolvedor, el proveedor y los seeders) en T.Arq/Identidad/NadieBuscaCuentasPorCorreoFueraDelResolutor.cs. Arranca con una lista de excepciones temporales, `DisableUser` y `AdminResetPassword`, para que pase en verde; T073 y T080 las retiran de la lista

**Checkpoint**: una cuenta existente entra y renueva igual (verificado en la e2e T097).

---

## Phase 4: User Story 2 — Invitar a una persona con su documento (P1)

**Goal**: el administrador invita con documento, nombres, celular, rol y cargo; la invitación queda anclada a la identidad y no revela otras cooperativas.

**Independent Test**: quickstart §2, pasos 1, 2 y 7 (mensaje y destino).

- [X] T041 [P] [US2] Crear la política de identidad por cooperativa:
  - lector con defectos cuando no hay fila: App/Identity/Politica/PoliticaDeIdentidad.cs;
  - comandos `GET`/`PUT`: App/Identity/Politica/{GetIdentityPolicyQuery,UpdateIdentityPolicyCommand}.cs;
  - ruta `/api/admin/identity-policy` con el permiso nuevo `Security.IdentityPolicy.Manage`, que siembra la API al arrancar, en Api/Endpoints/ (módulo de usuarios o uno nuevo `IdentityPolicyModule.cs`).
- [X] T042 [US2] Ampliar App/Invitations/IssueTenantInvitation/IssueTenantInvitationCommand.cs y su validador con documento (dos veces), nombres, correo, celular, `RolePublicId` y `PositionPublicId`, y el tipo aceptado por la política.
- [X] T043 [US2] En App/Invitations/IssueTenantInvitation/IssueTenantInvitationCommandHandler.cs:
  - si el correo es de otra identidad, `Invitation.EmailTaken`;
  - buscar la identidad por usuario derivado: crearla `Pending` si no existe, o anclarla si existe;
  - destino = correo de la identidad; guardar `TypedEmail` si difiere;
  - si ya es miembro activo, `Invitation.AlreadyMember`;
  - validar rol y cargo contra la cooperativa resuelta;
  - reintentar ante la carrera del índice único;
  - mismo mensaje en los dos casos;
  - emitir `Invitation.Issued.ByTenantAdmin` explícito.
- [X] T044 [P] [US2] Aplicar lo mismo en App/Invitations/IssueMasterInvitation/IssueMasterInvitationCommandHandler.cs y su comando (más `inviteAsTenantAdmin`). Como el maestro no tiene cooperativa resuelta, rol y cargo se validan abriendo la de la invitación por el único punto autorizado (`IEjecutorEnCooperativa`), nunca con una conexión armada a mano (Principio IV)
- [X] T045 [US2] Crear `CorrectInvitationCommand` (para `Pending` o `UnderReview`: deja la anterior `Superseded`, emite otra y corrige el correo pendiente de la identidad sólo si la creó esta invitación y nunca se activó) en App/Invitations/CorrectInvitation/. Agregar la ruta `PUT /api/tenants/{tenantId}/invitations/{invitationPublicId}` en Api/Endpoints/InvitationsModule.cs y emitir explícitamente `Invitation.Corrected`, `Revoked` y `Superseded` en App/Invitations/GestionInvitaciones/GestionInvitaciones.cs
- [X] T046 [US2] Ampliar el listado de invitaciones (`fullName`, `documentType`, `documentNumberMasked`, `UnderReview`, rol, cargo) en App/Invitations/GestionInvitaciones/GestionInvitaciones.cs
- [ ] T047 [US2] Plantilla de invitación a identidad existente («te invitan a una nueva cooperativa») con el nombre real en vez del correo, en src/Infrastructure/IngenIA365ERP.Storage/Templates/InvitationExistingEmail.html y App/Invitations/Services/InvitationEmailDispatcher.cs — *no se creó plantilla aparte: la invitación usa la general con el nombre de la identidad*
- [X] T048 [US2] Primer administrador con documento: objeto `firstAdmin` (con `firstAdminEmail` como alias) en App/Saas/RegisterTenantWithAdmin/RegisterTenantWithAdminCommand.cs y su handler, más la pantalla Sh/Pages/Security/MasterRegisterTenant.razor
- [X] T049 [US2] Formulario completo de invitación en Sh/Pages/Administracion/Tenants/Invitaciones.razor:
  - campos: tipo (sólo los aceptados), número dos veces, nombres, apellidos, correo, celular, rol (roles de la cooperativa) y cargo (`COR_Positions`, con «crear cargo» si no hay);
  - validación en pantalla y botón «Corregir» en `Pending`/`UnderReview`;
  - filtro «En revisión»;
  - `IndicadorDeCarga` por zona.
- [X] T050 [P] [US2] Pantalla de política de identidad (tipos aceptados, fecha de exigencia informativa, intentos) en Sh/Pages/Security/TenantIdentityPolicyPage.razor, con su enlace en el menú de seguridad (pasa `TodoEnlaceDelMenuTieneSuPagina`)
- [X] T051 [P] [US2] Pruebas de emisión: documento nuevo, identidad existente (destino y mensaje iguales), `EmailTaken`, `AlreadyMember`, tipo no aceptado, carrera de dos cooperativas; en T.App/Invitations/InvitacionConDocumentoTests.cs

**Checkpoint**: se puede invitar y el correo sale al destino correcto.

---

## Phase 5: User Story 3 — Activar la invitación confirmando el documento (P1)

**Goal**: activar exige el número; al agotar los intentos, la invitación queda en revisión; activar enlaza persona, rol y cargo en la cooperativa.

**Independent Test**: quickstart §2, pasos 3 a 8.

- [X] T052 [US3] En App/Invitations/PreviewInvitation/PreviewInvitationQuery.cs y su handler:
  - marcar el correo verificado si el token es válido (`EmailConfirmed` y `EmailVerifiedAt`);
  - devolver el destino enmascarado, `firstName` y `requiresDocument`;
  - nunca devolver el documento.
- [X] T053 [US3] En App/Invitations/AcceptInvitation/AcceptInvitationCommand.cs y App/Invitations/AcceptInvitation/AcceptInvitationCommandHandler.cs:
  - recibir `documentNumber` y compararlo normalizado;
  - con error, sumar el intento y responder `Invitation.DocumentMismatch` con `attemptsLeft`;
  - al agotar los intentos, pasar invitación y membresía a `UnderReview`, avisar al invitador por `IEmailSender` y emitir `Invitation.UnderReview` y `Membership.UnderReview`;
  - una invitación en revisión responde `Invitation.UnderReview`;
  - la rama «con sesión» compara `CentralUserId` en vez del correo;
  - al registrar, poner la clave en la identidad `Pending` creada al invitar y dejarla `Active`;
  - devolver `documentUsername`.
- [X] T054 [US3] En App/Common/Services/TenantUserProvisioner.cs, al activar:
  - buscar la persona de la cooperativa por `Username` o crearla con `PersonFactory` usando los datos de la invitación;
  - escribir `SEC_Users.PersonId`, `PositionId` y `CentralUsername`;
  - asignar el rol de la invitación (o `ReadOnly`/`CompanyAdmin` como hoy si no trae);
  - no escribir `IdentificationNumber`.
- [ ] T055 [US3] Los estados `UnderReview` no dan acceso: no aparecen en el selector ni pasan las puertas de App/Identity/Sessions/SelectTenant/SelectTenantCommandHandler.cs, App/Identity/Sessions/SwitchTenant/SwitchTenantCommandHandler.cs, el auto-select de App/Identity/Auth/Login/LoginCommandHandler.cs y App/Identity/Auth/RefreshToken/RefreshTokenCommandHandler.cs. Revisar que el filtro sea `Active` y no «distinto de revocada». En esas mismas puertas, si `SEC_Users.CentralUsername` difiere del usuario central, actualizarlo en la cooperativa resuelta (D-08; no en cada renovación) — *hecho el filtro (sólo `Active` da acceso); la actualización de `CentralUsername` en las puertas queda para US5/US6, cuando el usuario central puede cambiar*
- [X] T056 [US3] Agregar el paso de documento en Sh/Pages/Security/AcceptInvitation.razor:
  - número, intentos restantes, mensaje de revisión;
  - al final, «Puedes ingresar con tu correo o con tu usuario: **XXXX**».
  Crear la plantilla de bienvenida con usuario en src/Infrastructure/IngenIA365ERP.Storage/Templates/WelcomeWithUsernameEmail.html, enviada por el handler de T053.
- [X] T057 [P] [US3] Plantilla del aviso de revisión al invitador en src/Infrastructure/IngenIA365ERP.Storage/Templates/InvitationUnderReviewEmail.html
- [X] T058 [P] [US3] Pruebas de aceptación:
  - coincide;
  - falla dos veces y luego acierta;
  - falla tres veces y queda en revisión;
  - identidad existente con clave y con sesión;
  - persona de la cooperativa creada o enlazada sin sobrescribir;
  - rol y cargo aplicados.
  En T.App/Invitations/ActivacionConDocumentoTests.cs.

**Checkpoint**: invitar y activar funciona de punta a punta (verificado en la e2e T098).

---

## Phase 6: User Story 4 — Entrar con correo o con usuario de documento (P1)

**Goal**: la pantalla de ingreso acepta correo o usuario y muestra la pista.

**Independent Test**: quickstart §3 (tabla).

- [X] T059 [US4] En Sh/Pages/Security/Login.razor:
  - etiqueta «Correo o usuario» sin validación de correo y `autocomplete="username"`;
  - mensaje genérico «Credenciales inválidas» con la pista que llega en `data.hint`;
  - llamada al cliente con `identifier`, en el cliente de autenticación de Sh/Services/ (el que expone `AuthClient.LoginAsync`).
- [ ] T060 [P] [US4] Prueba de componente del formulario de ingreso: acepta un número sin `@` y muestra la pista; en tests/IngenIA365ERP.Shared.Tests/Security/LoginConUsuarioTests.cs — *pendiente: no hay pruebas de componente del ingreso; lo cubre la e2e*
- [X] T061 [P] [US4] Revisar la app MAUI (src/Presentation/IngenIA365ERP.App) por pantallas de ingreso propias que validen formato de correo, y alinearlas con T059

**Checkpoint**: con las fases 3 a 6 está el MVP (alta de COOFLOPAL).

---

## Phase 7: User Story 5 — La persona existente completa su documento (P2)

**Goal**: paso posponible tras el ingreso; desde la fecha de la cooperativa, desafío `document-required`.

**Independent Test**: quickstart §1, paso 5, más una cooperativa con fecha vencida.

- [ ] T062 [US5] Crear `RegistrarDocumentoCommand` (doble digitación, tipo aceptado por todas sus cooperativas activas, `RegistrarDocumento` en la identidad, historial `Registration`, evento `Identity.Document.Registered`; si el documento ya está tomado, `Identity.Document.Unavailable` y un caso `DocumentTaken`) en App/Identity/Auth/Documento/RegistrarDocumentoCommand.cs. Agregar la ruta `POST /api/auth/document` en Api/Endpoints/CentralAuthModule.cs
- [ ] T063 [US5] Crear `GuardiaDeDocumento.Evaluar` con el patrón de App/Identity/Auth/Common/GuardiaDeMetodos.cs, en App/Identity/Auth/Documento/GuardiaDeDocumento.cs. Consultarla en las siete puertas que consultan `GuardiaDeMetodos` y emitir el desafío `document-required` desde la fecha
- [ ] T064 [US5] Crear el componente posponible Sh/Components/Identidad/PasoDeDocumento.razor, montado en Sh/Layout/MainLayout.razor según `documentPending`. Crear la pantalla del desafío `document-required` en Sh/Pages/Security/RegistrarDocumento.razor
- [ ] T065 [P] [US5] Pruebas: registrar libre, tomado (caso sin revelar), antes y después de la fecha, una cooperativa sin fecha nunca bloquea; en T.App/Identity/Auth/Documento/CompletarDocumentoTests.cs

---

## Phase 8: User Story 6 — Corregir, actualizar y fusionar documentos (P2)

**Goal**: el administrador corrige a quien sólo está en su cooperativa, el maestro a los demás, y el maestro fusiona.

**Independent Test**: quickstart §4, pasos 1 y 2.

- [ ] T066 [US6] Crear `CorregirDocumentoCommand` en App/Identity/Correccion/CorregirDocumento/:
  - actor administrador o maestro; decide en el servidor quién puede (`Identity.Document.PlatformOnly`);
  - motivo `Correction`/`Update` obligatorio, notas y soporte;
  - historial, recálculo del usuario, eventos;
  - correo a la persona con el usuario nuevo (plantilla src/Infrastructure/IngenIA365ERP.Storage/Templates/UsernameChangedEmail.html).
- [ ] T067 [US6] Agregar la regla del dueño `CorreccionDeDocumento` (soporte que sube el administrador de la cooperativa) a App/Attachments/Common/AdjuntosDeModulo.cs
- [ ] T068 [US6] Ruta `POST /api/admin/users/{publicId}/correct-document` con el permiso nuevo `Security.Users.CorrectDocument` (sembrado por la API, sólo `CompanyAdmin`) en Api/Endpoints/UsersModule.cs. Agregar `documentUsername`, `documentMismatch` (D-08), `canCorrectDocument` y `position` al listado de usuarios, en el query de App/Security/Users/ que alimenta `GET /api/admin/users`
- [ ] T069 [US6] Crear `FusionarIdentidadesCommand` en App/Identity/Correccion/FusionarIdentidades/:
  - rechaza con `Identity.Merge.SharedTenant` si comparten cooperativa;
  - paso administrativo en una transacción: membresías, `ADM_MfaCredentials`, invitaciones e historial pasan a la correcta; la de origen queda `Disabled` y con el sello rotado; caso `InProgress`;
  - paso por cooperativa: recorre el directorio, abre cada conexión y actualiza `SEC_Users.CentralUserId`; es idempotente y queda pendiente en `Detail` si falla;
  - cierra el caso `Done`.
- [ ] T070 [US6] Crear el módulo del maestro Api/Endpoints/IdentidadesModule.cs (`/api/saas/identities`, buscar, detalle, corregir, fusionar, casos y descartar), sólo maestro, con los queries en App/Identity/Correccion/Consultas/
- [ ] T071 [US6] Pantallas: «Corregir documento» en Sh/Pages/Security/Users/Edit.razor (sólo si `canCorrectDocument`; si no, «Sólo lo puede hacer soporte») y la marca de diferencia en Sh/Pages/Security/Users/Index.razor. Retirar de Edit.razor el campo libre «Cédula» (`IdentificationNumber`), que deja de escribirse. Para el maestro, Sh/Pages/Administracion/Identidades/{Index,Detalle,Casos}.razor, con `IndicadorDeCarga` y enlaces de menú
- [ ] T072 [P] [US6] Pruebas: corrección por el administrador con una sola cooperativa, rechazo con varias, maestro sin límite, historial y usuario recalculado, fusión (rechazo por cooperativa compartida, movimiento de credenciales, reintento idempotente del paso por cooperativa); en T.App/Identity/Correccion/{CorregirDocumentoTests,FusionarIdentidadesTests}.cs

---

## Phase 9: User Story 8 — Retirar a alguien de una cooperativa (P2)

**Goal**: retirar corta esa cooperativa y no las demás, y el autor del retiro es quien retira.

**Independent Test**: quickstart §4, paso 4.

- [ ] T073 [US8] En App/Security/Users/DisableUser/DisableUserCommandHandler.cs, resolver la cuenta central por `SEC_Users.CentralUserId` (por correo sólo si no hay vínculo) y anotar como autor al actor, no al retirado
- [ ] T074 [P] [US8] Aplicar la misma corrección del autor y de la búsqueda en App/Memberships/RevokeMembership/RevokeMembershipCommandHandler.cs
- [ ] T075 [P] [US8] Pruebas: autor correcto, búsqueda por id y la renovación rechazada tras el retiro; en T.App/Security/Users/RetiroDeCooperativaTests.cs

---

## Phase 10: User Story 7 — Cambiar el correo, y clave sin administrador (P3)

**Goal**: sólo la persona cambia su correo (clave, segundo factor y código); el maestro rescata; el administrador ya no fija claves.

**Independent Test**: quickstart §4, pasos 3 y 5.

- [ ] T076 [US7] Crear el desafío `step-up` (clave más segundo factor → token de 5 minutos, mismo verificador del ingreso) en App/Identity/Auth/StepUp/ y la ruta `POST /api/auth/step-up` en Api/Endpoints/CentralAuthModule.cs
- [ ] T077 [US7] Crear el cambio de correo en App/Identity/Auth/CambioDeCorreo/{SolicitarCambioDeCorreoCommand,ConfirmarCambioDeCorreoCommand}.cs:
  - código hasheado, 30 minutos, 5 intentos;
  - si el correo está tomado, `Identity.Email.Taken`;
  - al confirmar, reemplazar `Email`, `NormalizedEmail`, `UserName` y `NormalizedUserName`, rotar el sello y avisar al anterior;
  - rutas `POST /api/auth/email-change` y `/confirm`.
- [ ] T078 [P] [US7] Plantillas del código y del aviso al correo anterior en src/Infrastructure/IngenIA365ERP.Storage/Templates/{EmailChangeCodeEmail,EmailChangedNoticeEmail}.html
- [ ] T079 [US7] Crear el rescate del maestro `POST /api/saas/identities/{id}/change-email` (motivo, avisos a los dos correos, evento `Identity.Email.ChangedByMaster`) en App/Identity/Correccion/CambiarCorreoPorMaestro/ y en Api/Endpoints/IdentidadesModule.cs
- [ ] T080 [US7] Retirar `POST /api/admin/users/{id}/reset-password` sin alias (borrar App/Security/Users/AdminResetPassword/ y su ruta en Api/Endpoints/UsersModule.cs). Crear `SendRecoveryLinkCommand`, que reutiliza el token de App/Identity/Profile/RequestPasswordReset/, en App/Security/Users/SendRecoveryLink/, con la ruta `POST /api/admin/users/{publicId}/send-recovery-link` y el evento `Identity.RecoveryLinkSent`
- [ ] T081 [US7] Hacer que `forgot` acepte `identifier` (con `email` como alias) por `ResolutorDeIdentificador` en App/Identity/Profile/RequestPasswordReset/RequestPasswordResetCommandHandler.cs y Api/Endpoints/AuthRecoveryModule.cs. Introducir `ICanalDeRecuperacion` con una sola implementación, la de correo, en App/Common/Interfaces/Identity/ICanalDeRecuperacion.cs. Exigir correo verificado también para la recuperación del segundo factor por correo (FR-041) en App/Identity/Auth/Recuperacion/GuardiaDeRecuperacionPorCorreo.cs
- [ ] T082 [US7] Pantallas:
  - Sh/Pages/Security/MiCuenta.razor (nueva): documento y usuario, y cambio de correo con step-up y código; enlace en el menú del perfil;
  - Sh/Pages/Security/Users/Edit.razor: «Enviar enlace de recuperación» en lugar de «Restablecer clave»;
  - Sh/Pages/Security/ResetPassword.razor: el campo de olvido pasa a «Correo o usuario».
- [ ] T083 [P] [US7] Retirar del manual las referencias a «Restablecer clave» del administrador en Sh/Services/Manual/ManualCatalogo.cs
- [ ] T084 [P] [US7] Pruebas: step-up obligatorio, correo tomado, código vencido o agotado, sello rotado, aviso al anterior, rescate del maestro, `send-recovery-link` sólo al correo verificado y `forgot` por usuario; en T.App/Identity/Auth/CambioDeCorreoYClaveTests.cs

---

## Phase 11: Polish & cierre

- [ ] T085 [P] Explicar en specs/017-login-documento-invitacion/diagnostico-de-datos.md cómo leer el reporte de conflictos de T021 y enlazarlo desde el runbook
- [ ] T086 [P] Runbook de despliegue en docs/operaciones/identidad-con-documento.md:
  - diagnóstico previo, respaldo, orden de las migraciones, tarea de datos, verificación;
  - reversa y su límite (CC y CE ya conviviendo);
  - el bloqueo que se libera una vez;
  - la consulta de control de SC-004: identidades y personas cuyo usuario no coincide con `Derivar(tipo, número)`, que debe dar 0.
- [ ] T087 [P] Manual del administrador (invitar, revisión, corregir, política) y de la persona (ingresar con usuario, completar documento, cambiar correo) en docs/manual/usuarios-e-identidad.md, sin mencionar el sistema anterior
- [ ] T088 [P] Ampliar `ModulosMigrados` si hace falta, para que `LasPantallasDicenQueEstanCargando` cubra las pantallas nuevas de seguridad, en T.Arq/
- [ ] T089 Actualizar el bloque **Auth/Sesión** y los Totales de CLAUDE.md con lo construido (usuario de documento, resolvedor, guardia de documento, rutas retiradas), remidiendo con los comandos de la tabla
- [ ] T090 Correr y dejar verdes `dotnet build IngenIA365ERP.slnx` y las suites sin contenedores (Domain, Application, Architecture, Shared)

### Integración (e2e, al final, en PostgreSQL y SQL Server)

- [ ] T091 [P] e2e: migraciones aplicadas dos veces sin cambios; la tarea de datos dos veces da el mismo reporte; la reversa de `UsuarioDeLaPersona` con y sin convivencia; en T.Int/Identity/Documento/MigracionIdempotenteTests.cs
- [ ] T092 [P] e2e: el ingreso con usuario cubre la tabla del borrador (cédula con y sin puntos y `CC`; CE, PPT y PA en mayúsculas y minúsculas; CC y CE del mismo número; correo sin verificar rechazado); en T.Int/Identity/Documento/IngresoConUsuarioTests.cs
- [ ] T093 [P] e2e: tiempo homogéneo; las medianas de inexistente y clave errada están dentro de un margen; sólo con `RUN_PERF_TESTS=1`; en T.Int/Identity/Documento/TiempoHomogeneoTests.cs
- [ ] T094 [P] e2e: invitación a documento nuevo, a documento de otra cooperativa (destino y mensaje) y con correo ajeno; en T.Int/Identity/Documento/InvitacionConDocumentoE2ETests.cs
- [ ] T095 [P] e2e: activación que coincide, que no coincide hasta quedar en revisión, y corrección por el administrador; en T.Int/Identity/Documento/ActivacionE2ETests.cs
- [ ] T096 [P] e2e: corrección y actualización conservan el historial; la fusión conserva membresías y segundo factor; el administrador no ve otras cooperativas ni cambia claves ni correos; en T.Int/Identity/Documento/CorreccionYFusionE2ETests.cs
- [ ] T097 e2e **regla de oro** en T.Int/Identity/Documento/ReglaDeOroTests.cs:
  - cuentas creadas con el flujo de hoy y una sesión abierta antes de aplicar las migraciones nuevas;
  - entrar con correo y clave da el mismo segundo factor, cooperativas y permisos;
  - la sesión vieja renueva.
- [ ] T098 e2e: retiro de una cooperativa con sesión abierta en dos, y cambio de correo de punta a punta; en T.Int/Identity/Documento/RetiroYCambioDeCorreoE2ETests.cs
- [ ] T099 Recorrer quickstart.md a mano en local (web y MAUI) y anotar el resultado en specs/017-login-documento-invitacion/quickstart.md

---

## Dependencies & Execution Order

- **Setup (F1)** → **Foundational (F2)**, que bloquea todo. Dentro de F2:
  - T004 y T005 van antes de todo lo demás;
  - T009–T018 antes de T019–T021;
  - T024 antes de US1;
  - T026 y T027 antes de T030, T031 y US3.
- **US1 (F3)** depende sólo de F2.
- **US2 (F4)** depende de F2. **US3 (F5)** depende de US2 (necesita invitaciones con documento).
- **US4 (F6)** depende de US1 (resolvedor en el login) y, para probarse con usuarios nuevos, de US3.
- **US5 (F7)**, **US6 (F8)**, **US8 (F9)** y **US7 (F10)** dependen de F2 y US1. Son independientes entre
  sí, salvo que US7-T079 y US6-T070 comparten Api/Endpoints/IdentidadesModule.cs: van en orden.
- **Polish (F11)** al final. Las e2e T091–T098 se corren una sola vez con todo terminado.

### Paralelo dentro de cada fase

- **F2**: T004 ∥ T006 ∥ T007 ∥ T008, luego T010 ∥ T011 ∥ T012 ∥ T013 ∥ T014, luego T016 ∥ T017 ∥ T018,
  luego T028 ∥ T029 ∥ T032 ∥ T033.
- **US2**: T041 ∥ T044 ∥ T050 ∥ T051 una vez hecho T042–T043.
- **US3**: T057 ∥ T058 tras T053.
- **Entre historias**: US5, US6, US8 y US7 pueden repartirse entre personas distintas una vez cerrada
  US1.

## Implementation Strategy

**MVP** = F1 + F2 + US1 + US2 + US3 + US4 (las cuatro P1): las cuentas existentes intactas, invitar y
activar con documento y entrar con usuario. Es lo que COOFLOPAL necesita el 01/12.

La entrega es **una sola** (D-16), así que el MVP no se despliega por separado. Sirve de corte de
control: si al cerrar US4 el calendario no da para todo, se le propone al dueño mover US6 (fusión) y
US7 (cambio de correo), como dice research R16. Quitar «Restablecer clave» (T080) es pequeño y
conviene adelantarlo si se mueve US7.

**Orden sugerido**: F2 → US1 → US2 → US3 → US4 → US8 → US5 → US6 → US7 → Polish.
