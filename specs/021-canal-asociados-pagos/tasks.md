---
description: "Tareas de la feature 021 — canal digital del asociado y pagos en línea"
---

# Tasks: Canal digital del asociado y pagos en línea

**Input**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/](contracts/), [quickstart.md](quickstart.md), [enmienda-constitucion.md](enmienda-constitucion.md)

**Puerta**: la enmienda de la constitución (C0, T001–T002) quedó aprobada por el dueño y aplicada el 2026-11-08
(v2.1.0); desde ahí se puede construir.

**Tests**:
- Las unitarias y de arquitectura del ERP van dentro de cada historia (lo exigen los Principios II, III y X y las
  pruebas de arquitectura existentes).
- Las del satélite van con su código: `go test` y Vitest (enmienda, regla 9).
- Las de integración del ERP (Testcontainers) y la e2e de Playwright van **todas al final** (fase 18), como pide el
  dueño.
- Compilar el ERP con `-m:1`, y nunca más de dos sesiones compilando a la vez.

**Formato**: `- [ ] Txxx [P] [USn] descripción con ruta`. `[P]` = otro archivo, sin dependencias pendientes.

**Abreviaturas de rutas**:

| Abreviatura | Ruta |
|---|---|
| `Dom/` | `src/Core/IngenIA365ERP.Domain/` |
| `App/` | `src/Core/IngenIA365ERP.Application/` |
| `Per/` | `src/Infrastructure/IngenIA365ERP.Persistence/` |
| `MigPg/`, `MigSql/` | `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/` |
| `Idn/` | `src/Infrastructure/IngenIA365ERP.Identity/` |
| `Ext/` | `src/Infrastructure/IngenIA365ERP.ExternalChannels/` |
| `Api/` | `src/Presentation/IngenIA365ERP.API/` |
| `Sh/` | `src/Presentation/IngenIA365ERP.Shared/` |
| `T.Dom/`, `T.App/`, `T.Arq/`, `T.Int/` | `tests/IngenIA365ERP.{Domain,Application,Architecture,API.Integration}Tests/` |
| `C.api/` | `canal/api/` (Go) |
| `C.web/` | `canal/web/` (React) |
| `C.app/` | `canal/app/` (MAUI) |

---

## Phase 1: Setup (C0 — enmienda y esqueleto)

**Propósito**: aprobar la enmienda y dejar el satélite con CI y despliegue en DEV, sin funcionalidad.

- [X] T001 Llevar specs/021-canal-asociados-pagos/enmienda-constitucion.md a revisión del dueño y de un segundo revisor; anotar en ese archivo los nombres, la fecha y el resultado (aprobada / con cambios / rechazada) — *aprobada sin cambios por el dueño el 2026-11-08*
- [X] T002 Con la enmienda aprobada, escribir la sección «Sistemas satélite» y el prefijo `CDG_` en .specify/memory/constitution.md (versión 2.1.0, Sync Impact Report al inicio), agregar la fila «Satélite (reglas 1–9)» a .specify/templates/plan-template.md y actualizar la referencia de versión en CLAUDE.md
- [X] T003 Enmendar specs/015-recaudo-tesoreria/spec.md: FR-091 (pasarela también en línea por el canal digital) y FR-174 (excepción «el pago que el propio pagador hace en línea se aplica a su nombre»), con referencia a la 021 y fecha
- [X] T004 [P] Crear canal/README.md (qué es el satélite, reglas 1–9 de la enmienda, cómo levantarlo en local) y canal/.gitignore (build de web, binarios, `Resources/Raw/wwwroot` de la app, llaves)
- [X] T005 [P] Inicializar el módulo Go en C.api/go.mod (`github.com/jormancopete/ingenia365erp/canal/api`) con la estructura de carpetas del plan (`cmd/canal`, `internal/{identidad,entidades,conceptos,pagos,pasarela,erp,mensajeria,auditoria,cifrado,trabajos,http}`, `migrations`, `queries`, `web`) y C.api/cmd/canal/main.go que arranca y responde `/salud`
- [X] T006 [P] Inicializar C.web/ con Vite + React 19 + TypeScript (package.json con versiones exactas, tsconfig estricto, ESLint, Vitest), sin pantallas
- [X] T007 [P] Crear canal/docker-compose.yml con PostgreSQL 17 para `ingenia365_canal` (puerto 5434, sin chocar con 5432/5433) y canal/scripts/llaves-dev.sh que genera los pares ES256 del canal y del ERP **fuera del repositorio** (`$HOME/.ingenia365/canal-dev/`)
- [X] T008 Crear C.api/Dockerfile multi-etapa: build de C.web, `go build` con la carpeta `web/dist` embebida, imagen final distroless no root
- [X] T009 Crear .github/workflows/canal.yml filtrado a `canal/**`: `go vet`, `go test ./...`, lint y pruebas de C.web, build de la imagen `ghcr.io/jormancopete/ingenia365erp/canal`, y en `develop`/`release` el digest escrito en `ingenia365-gitops` `workloads/canal/overlays/{dev,qa|pdn}/kustomization.yaml` con `GITOPS_TOKEN`
- [X] T010 Documentar en docs/operaciones/canal-digital-despliegue.md el workload nuevo de GitOps (`workloads/canal/base` + overlays con `images:`, Deployment, Service, IngressRoute de Traefik sólo para `/`, `/api/v1`, `/webhooks`; NetworkPolicy que bloquea `/internal` desde fuera), la base `ingenia365_canal` por clúster con respaldo diario, los Secrets (`canal-llaves`, `canal-kek`, `canal-smtp`) y las rutas del túnel de Cloudflare para `asociados[-dev|-qa].ingenia365.com`; el cambio en el repositorio de GitOps lo hace el dueño o se autoriza aparte

---

## Phase 2: Foundational (bloquea a todas las historias)

**Propósito**: la comunicación autenticada entre el satélite y el ERP, el esquema `CDG_*` y los armazones del canal.

### ERP — actor, auditoría y permisos

- [X] T011 Agregar `ActorKind.Member = 3` y `ExecutionChannel.Channel` en Dom/Enums/Integration/IntegrationEnums.cs, y la fábrica `Actor.AsociadoPorCanal(Guid personPublicId)` en App/Common/Execution/Actor.cs (UserId nulo, PersonPublicId obligatorio)
- [X] T012 [P] Agregar `ModuloDeAuditoria.Channel` en App/Common/Audit/ModuloDeAuditoria.cs y el emisor App/Channel/Common/AuditoriaDelCanal.cs (`AgregarAsync`, `LecturaAsync` con IP y agente del asociado), siguiendo el patrón de App/Receipts/Common/AuditoriaDeRecaudo.cs
- [X] T013 [P] Crear Idn/Seed/ChannelPermissionCatalogSeeder.cs con los nueve permisos de contracts/erp-administracion.md, registrarlo en `PhaseZeroSecuritySeeder` y agregar los permisos a las plantillas de App/Security/Roles/Plantillas/PerfilesSugeridos.cs

### ERP — esquema `CDG_*`

- [X] T014 Crear las entidades en Dom/Entities/Channel/: `ChannelSettings`, `LegalText`, `PaymentDestination`, `PublishedConcept`, `ChannelEnablement`, `ContactUpdate`, `PaymentNotice`, `PaymentNoticeItem` (heredan `AuditableEntity`, con `PublicId`), y sus enums en Dom/Enums/Channel/ (`EnablementPolicy`, `EnablementStatus`, `EnablementSource`, `ConceptKind`, `LegalTextKind`, `SurplusPolicy`, `CredentialsStatus`, `NoticeOutcome`, `Gateway`), según data-model.md §A
- [X] T015 Crear las configuraciones EF en Per/Configurations/Channel/ (tablas `CDG_*`, `HasQueryFilter`, índices únicos: `ChannelEnablements.PersonId` entre vivos, `PublishedConcepts (Kind, LineCode)` entre vivos, `PaymentNotices.Reference`, `ChannelSettings.Version`) y los `DbSet` en Per/DbContext/ApplicationDbContext.cs e `IApplicationDbContext`
- [X] T016 Generar la migración aditiva `CanalDigital` en MigPg/ y MigSql/ (revisar que el scaffold no arrastre cambios ajenos) y verificar que `AutoMigrate`/DbMigrator la aplican en una base local de prueba — *generada y revisada (sólo tablas e índices `CDG_`); su aplicación sobre una base real la cubren las e2e de la fase 18*

### ERP — esquema de autenticación `CanalServicio`

- [X] T017 Crear Api/Security/CanalServicio/CanalServicioAuthentication.cs: esquema JwtBearer `CanalServicio` (ES256, `iss = ingenia365-canal`, `aud = ingenia365-erp`, vida ≤ 60 s, llaves por `kid` desde configuración `CanalServicio:LlavesPublicas`), registrado en Idn/DependencyInjection.cs junto al esquema existente, y la política de autorización `CanalServicio`
- [X] T018 Crear Api/Security/CanalServicio/AlmacenDeJti.cs sobre Redis base 0 (`SET NX` con TTL de 120 s) y rechazar un `jti` repetido con 401 `Channel.Service.ReplayedToken`
- [X] T019 Crear Api/Security/CanalServicio/ResolucionDeCooperativaDelCanal.cs: para `/api/canal/v1/tenants/{t}/…` exige `t == sub`, resuelve con `ITenantDirectory` y llena los mismos `HttpContext.Items` que `TenantResolutionMiddleware`; eximir `/api/canal/v1/` en Api/Middleware/TenantResolutionMiddleware.cs; cooperativa inexistente o inactiva → 404, canal apagado → 409 `Channel.Disabled`
- [X] T020 Crear Api/Endpoints/CanalServicio/CanalServicioModule.cs (grupo `/api/canal/v1` con `.RequireAuthorization("CanalServicio")`, filtro que lee `X-Canal-Ip`, `X-Canal-UserAgent`, `X-Canal-Account` al contexto de auditoría) y `GET /tenants` → App/Channel/Service/ListChannelTenantsQuery.cs (recorre el directorio abriendo cada base, Principio IV)
- [X] T021 [P] Pruebas de arquitectura en T.Arq/Principles/CanalServicioTests.cs: el esquema `CanalServicio` sólo aparece en rutas `/api/canal/v1`; ninguna ruta `/api/canal/v1` acepta el JWT de usuarios; `Actor.AsociadoPorCanal` sólo lo construye `ActorDeLaPeticion` para peticiones del canal con persona en la ruta (ningún handler se fabrica un asociado; ajuste de diseño del 2026-11-08)
- [X] T022 [P] Crear el puerto App/Channel/Common/ICanalSatelite.cs (credenciales, intentos, cuentas, reverso, métricas, sincronización) y su implementación Ext/Canal/ClienteDelCanal.cs (HttpClient con token ES256 firmado por el ERP, `iss = ingenia365-erp`, `aud = ingenia365-canal`, llave privada desde Secret)

### Canal Go — armazón

- [X] T023 C.api/internal/config/config.go: configuración por variables de entorno (base, URLs del ERP por ambiente, llaves, KEK por versión, SMTP, host público, frecuencias de trabajos) con validación al arrancar (no arranca si falta algo)
- [X] T024 [P] C.api/internal/db/: pool `pgx/v5`, `sqlc.yaml` en C.api/, migración inicial C.api/migrations/00001_base.sql (goose Up/Down) con `tenants` y `audit_log` según data-model.md §B
- [X] T025 [P] C.api/internal/http/: servidor con `net/http`, middleware de correlación, IP real desde `CF-Connecting-IP`, límite de peticiones por IP y por documento, recuperación de pánicos con log, sobre de error `{code,message,data}` y cabeceras de seguridad (CSP, HSTS, `frame-ancestors 'none'`)
- [X] T026 [P] C.api/internal/auditoria/auditoria.go: `Registrar(ctx, accion, sujeto, datos)` sólo de anexar sobre `audit_log`, sin secretos; logs `slog` JSON con `tenant`, `cuenta`, `intento`, `correlacion`
- [X] T027 [P] C.api/internal/cifrado/kek.go: AES-256-GCM con KEK versionada, `Cifrar`/`Descifrar`, pruebas con vectores conocidos en C.api/internal/cifrado/kek_test.go
- [X] T028 C.api/internal/erp/cliente.go: cliente de `/api/canal/v1` que firma un JWT ES256 por petición (`jti` aleatorio, 60 s, `sub` = cooperativa), reenvía IP, agente y cuenta del asociado, y traduce el sobre de error del ERP; y C.api/internal/erp/verificador.go, que valida el token del ERP en `/internal/v1`
- [X] T029 C.api/internal/trabajos/: River sobre el mismo pool (migraciones de River con goose), registro de trabajadores, y el trabajo `sync_tenants` (cada 5 min) que llena `tenants` desde `GET /api/canal/v1/tenants`
- [X] T030 C.api/internal/http/estaticos.go: sirve el build embebido de C.web con caché inmutable para archivos con huella y `index.html` sin caché (fallback de SPA para rutas del navegador)
- [X] T031 [P] Prueba de contrato C.api/internal/erp/cliente_test.go contra un servidor HTTP de prueba que valida firma, `aud`, `sub` y `jti`

### Canal web — armazón

- [X] T032 [P] C.web/src/servicios/api.ts: cliente `fetch` con cookies, `Idempotency-Key`, sobre de error tipado y mensajes en español; C.web/src/servicios/consultas.ts con TanStack Query (sin persistencia en almacenamiento)
- [X] T033 [P] C.web/src/plataforma.ts: detecta `window.HybridWebView` y expone la misma interfaz en web y app (credencial, biometría, abrir pasarela, push, abrir documento) según contracts/pasarela-y-app.md
- [X] T034 [P] C.web/src/marca/: variables CSS de color, densidad y escala (sin colores literales en pantallas) y `aplicarMarca(brand)` por cooperativa; componente `IndicadorDeCarga` por zona en C.web/src/componentes/IndicadorDeCarga.tsx
- [X] T035 C.web/src/App.tsx y C.web/src/rutas.tsx: React Router con rutas de navegador en la PWA y de hash cuando corre en la app; layout con pie de página (términos, política, preguntas frecuentes) y pantalla «Página no encontrada»
- [X] T036 [P] Configurar vite-plugin-pwa en C.web/vite.config.ts: manifiesto, íconos, sólo el armazón en caché (nunca respuestas de `/api`)

**Checkpoint**: el canal arranca en DEV, lee el directorio de cooperativas del ERP con token de servicio, y el ERP rechaza
cualquier otra forma de llegar a `/api/canal/v1`.

---

## Phase 3: User Story 1 — La entidad configura su canal (P1) 🎯 MVP

**Goal**: el administrador enciende el canal, publica conceptos con destinos, carga credenciales y habilita personas.

**Independent Test**: dos conceptos con destinos distintos, tres personas habilitadas y una inhabilitada; el espejo del
canal refleja exactamente esas habilitaciones (quickstart C1, pasos 1–2).

### ERP — configuración

- [X] T037 [P] [US1] App/Channel/Settings/: `GetChannelSettingsQuery`, `UpdateChannelSettingsCommand` (crea versión; rangos de data-model.md: sesión de pago 10–30, inactividad 5–15, aviso 0–15) con validador hermano
- [X] T038 [US1] App/Channel/Settings/EnableChannelCommand.cs y DisableChannelCommand.cs: encender asigna `ChannelCode` de 3 letras único en la plataforma (consulta al canal por `ICanalSatelite`) y materializa la política de habilitación (T046); apagar no toca intentos pendientes — *ajuste del 2026-11-08: no se siembra `RCD_ChannelModalityRules`: cada parte del aviso lleva su modalidad; exige términos y política de datos publicados*
- [X] T039 [P] [US1] App/Channel/Settings/LegalTexts.cs: `PublishLegalTextCommand` (versión nueva, nunca edita) y `ListLegalTextsQuery`; la semilla de `GatewayFaq` es un texto propio de la plataforma, no la copia del documento de la pasarela
- [X] T040 [P] [US1] App/Channel/Destinations/: `CreatePaymentDestinationCommand`/`Update…` (exige convenio `Kind = Gateway` activo de la cooperativa, medios, corte) y `ListPaymentDestinationsQuery`
- [X] T041 [US1] App/Channel/Destinations/SetGatewayCredentialsCommand.cs: reenvía login y secreto a `ICanalSatelite.GuardarCredencialesAsync` sin persistirlos ni auditarlos (el validador redacta el secreto en logs); guarda sólo `CredentialsStatus`, `CheckedAt`, `LoginLast4`
- [X] T042 [P] [US1] App/Channel/Concepts/: `PublishConceptCommand`/`Update…`/`Unpublish…` con las reglas de FR-003/FR-004 (mínimo obligatorio si hay parcial, prepago sólo en créditos, línea existente del tipo) y `ListPublishedConceptsQuery`
- [X] T043 [P] [US1] T.App/Channel/ConfiguracionDelCanalTests.cs: validadores de configuración, conceptos y destinos; credenciales que nunca llegan a la base ni al log

### ERP — habilitaciones

- [X] T044 [US1] App/Channel/Enablements/: `EnablePersonCommand`, `DisablePersonCommand` (motivo obligatorio), `ReEnablePersonCommand` y `ListChannelEnablementsQuery`; `ChangeSequence` desde una secuencia por cooperativa; regla de `EnabledAt` al rehabilitar (data-model.md)
- [X] T045 [US1] App/Channel/Enablements/ImportEnablementsCommand.cs + `GET …/template.xlsx`: carga masiva todo-o-nada con errores `{row, column}` (mecánica de la 016), documentos resueltos con `ResolutorDePersonaPorDocumento`
- [X] T046 [US1] App/Channel/Enablements/EnablementCommands.cs (`PoliticaDeHabilitacion`): al encender con `AllActiveAssociates`/`Both`, habilita a cada asociado con `Status = "A"`; *ajuste del 2026-11-08: en lugar de ganchos en Asociados y de un segundo consumidor del retiro (el bus admite uno por destino), el comando idempotente `ReconcileChannelEnablementsCommand` (`POST /api/canal/v1/tenants/{t}/enablements/reconcile`, que el canal llama antes de arrastrar) habilita a los asociados activos sin habilitación e inhabilita las de la política cuyo asociado se retiró; las manuales y las masivas no se tocan*
- [X] T047 [US1] Api/Endpoints/CanalServicio/: `GET /tenants/{t}/enablements?since=&limit=` → App/Channel/Service/ListEnablementChangesQuery.cs (tipo del catálogo fijo y número normalizado de `COR_People`)
- [X] T048 [P] [US1] T.App/Channel/HabilitacionesTests.cs: transiciones, motivo obligatorio, cursor creciente, retiro inhabilita, carga masiva todo-o-nada

### ERP — endpoints y pantallas de administración

- [X] T049 [US1] Api/Endpoints/Channel/ChannelAdminModule.cs: rutas de configuración, textos, destinos, credenciales, conceptos y habilitaciones de contracts/erp-administracion.md con sus permisos
- [X] T050 [P] [US1] Sh/Pages/Canal/Configuracion.razor (`/canal/configuracion`) con `IndicadorDeCarga`, marca con vista previa y textos legales con historial
- [X] T051 [P] [US1] Sh/Pages/Canal/Destinos.razor (`/canal/destinos`): convenio de pasarela, medios, corte y credenciales con campo de sólo escritura y estado de verificación
- [X] T052 [P] [US1] Sh/Pages/Canal/Conceptos.razor (`/canal/conceptos`)
- [X] T053 [P] [US1] Sh/Pages/Canal/Habilitaciones.razor (`/canal/habilitaciones`) con `PersonSearchPicker`, motivo, carga masiva y pestaña de novedades de contacto
- [X] T054 [US1] Enlaces del menú «Canal digital» en el layout de Shared con `PermissionGate`; agregar «Canal» a `ModulosMigrados` de `LasPantallasDicenQueEstanCargando` (T.Arq/)

### Canal — sitios y espejo

- [X] T055 [US1] C.api/migrations/00002_entidades.sql: `enablements` y `gateway_sites` (data-model.md §B)
- [X] T056 [US1] C.api/internal/entidades/espejo.go: trabajo River `pull_enablements` (cada 60 s por cooperativa con cursor) y `full_resync` (nocturno), con `POST /internal/v1/tenants/{t}/sync` para forzarlo
- [X] T057 [US1] C.api/internal/pasarela/sitios.go + ruta `PUT /internal/v1/tenants/{t}/sites/{s}/credentials`: cifra con la KEK, verifica contra la pasarela (sesión de prueba creada y consultada) y responde estado, fecha y últimos 4 del login; nunca devuelve el secreto
- [X] T058 [P] [US1] C.api/internal/entidades/espejo_test.go (testcontainers): cursor, idempotencia del arrastre, inhabilitación reflejada

**Checkpoint**: la cooperativa queda configurada y el canal conoce a quién habilitó, sin que nadie pueda entrar todavía.

---

## Phase 4: User Story 2 — El asociado activa su cuenta e ingresa (P1)

**Goal**: activación por código a la entidad validadora, ingreso con dos factores, selector de entidad y cambio de contacto.

**Independent Test**: quickstart C1, pasos 3–7.

### ERP

- [X] T059 [US2] App/Channel/Service/GetActivationContactQuery.cs + `GET /tenants/{t}/people/{p}/activation-contact`: nombres, correo y celular con máscara y valor de entrega, autorización de contacto (`IAutorizacionDeDatos`, `ContactChannel`), auditada como lectura sensible
- [X] T060 [US2] App/Channel/ContactUpdates/: `RegisterContactUpdateCommand` (servicio, idempotente por persona, campo y fecha), `ListContactUpdatesQuery`, `AcknowledgeContactUpdateCommand`, y `POST /tenants/{t}/people/{p}/contact-updates`

### Canal — identidad

- [X] T061 [US2] C.api/migrations/00004_identidad.sql (00003 la tomó el espejo en la fase 3; suma `login_throttle` —el freno va por documento, exista o no la cuenta— y `webauthn_ceremonies`): `accounts`, `legal_acceptances`, `devices`, `sessions`, `otp_challenges` (data-model.md §B; único `(document_type, document_number)`)
- [X] T062 [P] [US2] C.api/internal/identidad/validadora.go: entidad validadora = habilitación `enabled` de menor `enabled_at` del documento (D5), con pruebas de los casos borde de la spec
- [X] T063 [P] [US2] C.api/internal/mensajeria/: interfaz `Mensajero` con correo SMTP (plantillas en español, sin la palabra de ninguna marca ajena) y SMS detrás de una interfaz sin proveedor (registra «no disponible»); conteo de SMS por cooperativa en `otp_challenges`
- [X] T064 [US2] C.api/internal/identidad/otp.go: códigos de 6 dígitos hasheados, 5 min, 3 intentos, límite por documento e IP
- [X] T065 [US2] C.api/internal/identidad/activacion.go + rutas `POST /api/v1/activation/{start,send,verify,complete}`: respuesta y tiempo idénticos exista o no la persona (FR-018), «acércate a tu entidad» con su soporte (FR-019), clave bcrypt 12 con política y lista de claves comunes, aceptación de textos con versión, IP y dispositivo (FR-020)
- [X] T066 [US2] C.api/internal/identidad/sesion.go: token opaco en cookie `HttpOnly; Secure; SameSite=Strict`, 5 min de inactividad (o lo que diga la cooperativa, hasta 15) y 8 h máximo desde el ingreso; la rotación no extiende el máximo
- [X] T067 [US2] C.api/internal/identidad/ingreso.go + rutas `POST /api/v1/auth/{login,login/verify,logout}`: bloqueo a los 5 fallos por 15 min con aviso, credencial de dispositivo de confianza (FR-021, FR-022)
- [X] T068 [P] [US2] C.api/internal/identidad/passkeys.go + rutas `/api/v1/auth/passkey/{options,verify}` y registro desde «mis dispositivos» con `go-webauthn/webauthn` (RP ID = host del canal)
- [X] T069 [P] [US2] C.api/internal/identidad/recuperacion.go + rutas `/api/v1/auth/recovery/{start,verify,complete}` (FR-023)
- [X] T070 [US2] C.api/internal/identidad/perfil.go + rutas `GET /api/v1/me`, `POST /api/v1/me/{contact,contact/verify,password}`, `GET /api/v1/me/devices`, `DELETE /api/v1/me/{devices,sessions}/{id}`, `POST /api/v1/session/tenant`; el cambio de contacto encola `send_contact_update` a cada cooperativa habilitada (FR-024–FR-028)
- [X] T071 [US2] C.api/internal/identidad/acotamiento.go (`Servicio.Acotada`; vive con la sesión que extiende): middleware que exige cooperativa elegida en las rutas de cooperativa y vuelve a validar la habilitación en el espejo; la inhabilitación corta en la siguiente petición (FR-026)
- [X] T072 [P] [US2] Pruebas en C.api/internal/identidad/*_test.go (testcontainers): enumeración imposible (mismo cuerpo y tiempo dentro de un margen), bloqueo, vencimiento de códigos, máximo de 8 h, selector con 0, 1 y 2 cooperativas

### Canal web — pantallas

- [X] T073 [P] [US2] C.web/src/pantallas/activacion/: documento, elección de canal del código, código, clave y aceptación de textos; validación con zod y mensajes en español
- [X] T074 [P] [US2] C.web/src/pantallas/ingreso/: ingreso, código en dispositivo nuevo, passkey, recuperar clave
- [X] T075 [P] [US2] C.web/src/pantallas/selector/: selector de cooperativa con su marca; entra directo si hay una sola; «no tienes entidades activas» si no hay ninguna
- [X] T076 [P] [US2] C.web/src/pantallas/perfil/: datos de contacto, clave, dispositivos y sesiones
- [X] T077 [US2] C.web/src/componentes/AvisoDeSesion.tsx: aviso al minuto 4 con «Seguir trabajando» y salida a los 5 hacia el ingreso, conservando la ruta de regreso

**Checkpoint** (cerrado el 2026-11-09; ERP `GET /tenants/{t}/legal-texts` agregado para los textos a aceptar; el ingreso con biometría de la app, `POST /auth/device-unlock`, queda para US11): un asociado habilitado activa su cuenta, entra y elige cooperativa; con eso, C1 en modo sólo consulta está completa al terminar la fase 5.

---

## Phase 5: User Story 3 — El asociado consulta sus conceptos (P1)

**Goal**: ver sus conceptos publicados con los valores del ERP a la fecha.

**Independent Test**: lo que muestra el canal coincide con la caja del ERP para la misma persona y fecha (SC-005).

- [X] T078 [US3] App/Channel/Service/GetChannelPayablesQuery.cs: reutiliza las piezas de `GetPayablesQuery` (`ListarAsync`, `ListarExigiblesAsync`), agrega la próxima fecha con `CuotasDeNominaAsync(TodosLosCanales = true)`, filtra por conceptos publicados y arma el DTO de contracts/erp-api-de-servicio.md; auditoría `Receipts.Person.PayablesRead` con canal digital — hecho 2026-11-09: `ListarAsync` (deudor) + `ObligacionesAsync` (saldo total y mora) + `CuotasDeNominaAsync(TodosLosCanales)` en los 62 días siguientes; cuentas por `ConsultarAsync` + `ListarExigiblesAsync`; publicación de la línea o, si no hay, la de toda la clase; `conceptKey` `LOAN:`/`ACCOUNT:`; modalidades del canal `Due/Partial/Prepayment/Payoff` (`ModosDelCanal`); el evento `EventoConceptosLeidos` pasa a `Receipts.Person.PayablesRead`
- [X] T079 [US3] App/Channel/Service/SimulateChannelPaymentQuery.cs: envuelve `IContratoDeObligaciones.SimularAsync` (con `EleccionDeAbono` y alternativas) e `IContratoDeCuentas.SimularAsync` — hecho: valida concepto visible, modalidad, valor entero y mínimo parcial antes de llamar al módulo
- [X] T080 [US3] Rutas `GET /tenants/{t}/people/{p}/payables` y `POST …/payables/simulate` en Api/Endpoints/CanalServicio/, con la verificación en vivo de la habilitación (404 idéntico a persona inexistente) — hecho (`CanalServicioModule`); la habilitación la verifica el handler (404 `Channel.Person.NotEnabled`)
- [X] T081 [P] [US3] T.App/Channel/ConceptosDelCanalTests.cs: filtro por publicación, línea no publicada oculta, mismos valores que `GetPayablesQuery` — hecho: 11 pruebas (14 casos)
- [X] T082 [US3] C.api/internal/conceptos/: proxies `GET /api/v1/payables` y `POST /api/v1/payables/simulate`, sin guardar nada (FR-030); ERP caído → 503 `Canal.Entidad.NoDisponible` (FR-031) — hecho: `internal/conceptos` + `erp/conceptos.go` (dinero como `json.Number`); 403 `Canal.Cooperativa.NoDisponible` si el ERP dice inhabilitada; rechazos `Channel.Payable.*` → `Canal.Concepto.*`; 6 pruebas
- [X] T083 [P] [US3] C.web/src/pantallas/conceptos/: lista por tipo con fecha de cálculo, detalle por concepto (saldo, vencido, mora, cuota, próxima fecha, exigible), estado «información no disponible» — hecho: `pantallas/conceptos/` reemplaza a `InicioDeCooperativa`; ruta `/cooperativa/conceptos/:clave`; 6 pruebas. La simulación en pantalla va con el pago (US4)

**Checkpoint**: C1 lista para DEV/QA en modo sólo consulta.

---

## Phase 6: User Story 4 — El asociado paga en línea (P1)

**Goal**: carrito, resumen, pasarela, retorno, notificación y consulta periódica, cumpliendo la certificación.

**Independent Test**: quickstart C2, pasos 1–8, con la pasarela simulada y luego con el ambiente de pruebas de Avalpay.

### Canal — pasarela

- [X] T084 [US4] C.api/internal/pasarela/pasarela.go: interfaz `Pasarela`, tipos y normalización de estados de contracts/pasarela-y-app.md — hecho 2026-11-09: `pasarela.go` (tipos, `EstadoDePago`, `ValidarComprador`, errores `ErrNoDisponible`/`ErrRechazo`/`ErrFirmaInvalida`)
- [X] T085 [P] [US4] C.api/internal/pasarela/simulada/: implementación con estado elegible por la prueba (aprobada, pendiente, rechazada, fallida, vencida) y notificación simulada firmada — hecho: `simulada/` en memoria con `Fijar`, notificación firmada SHA-256 y página `/simulada/{requestId}` (sólo con `CANAL_PASARELA=simulada`)
- [X] T086 [US4] C.api/internal/pasarela/placetopay/: `tranKey = Base64(SHA-256(nonce+seed+secret))`, crear sesión, consultar sesión, reversar (`/api/transaction`, `reverse`), verificación de notificación (SHA-256 con prefijo, SHA-1 sin prefijo, comparación en tiempo constante); validación del comprador (tipos de documento, nombres, correo, celular) — hecho: `placetopay/` con `Normalizar` (vencida = `REJECTED` con motivo `EX` o sin intentos tras la expiración, por verificar en pruebas), respuesta guardada sin comprador ni `processorFields`, y `Verificador` de credenciales (crea y consulta una sesión de prueba, R10) que reemplaza al pendiente
- [X] T087 [P] [US4] C.api/internal/pasarela/placetopay/placetopay_test.go: vectores de `tranKey` y de firma, mapeo de estados, respuestas grabadas del ambiente de pruebas **sin** credenciales ni datos reales — hecho: vectores calculados con Python, respuestas grabadas inventadas; 7 pruebas

### Canal — intentos

- [X] T088 [US4] C.api/migrations/00004_pagos.sql: `payment_attempts` (referencia única, restricción de un pendiente por cuenta y concepto) y `attempt_events` — hecho como `00005_pagos.sql` (la 00004 ya era la identidad): `payment_attempts`, `pending_concepts` (un abierto por cuenta y concepto, FR-041), `attempt_events` sólo de anexar, `payment_reference_seq`, `tenants.payment_session_minutes`/`require_otp_to_pay` y el propósito `payment` de los códigos
- [X] T089 [P] [US4] C.api/internal/pagos/referencia.go: `{código 3}{AAMMDD}{secuencia base36 8}{Luhn}` (R12) con pruebas — hecho: dígito de control Luhn módulo 36; 3 pruebas
- [X] T090 [US4] C.api/internal/pagos/crear.go + `POST /api/v1/payments`: `Idempotency-Key` del cliente (doble clic → mismo intento), mismo destino, modalidades y mínimos contra los conceptos vigentes del ERP, pendiente existente → 409 `Canal.Pago.Pendiente` con referencia, valor, autorización y contacto, aceptación de textos si cambió la versión, código si la cooperativa lo exige, total = suma, impuestos si el concepto los trae; crea la sesión en la pasarela con IP y agente del asociado y el vencimiento configurado — hecho (`crear.go`): impuestos vacíos porque ningún concepto de C1 los trae; el ERP publica `paymentSessionMinutes`/`requireOtpToPay` en el directorio y `destinations` con el sitio en los conceptos; código de pago por `identidad/pago.go` (`/payments/otp`, `/send`, `/verify`); los pendientes se refrescan contra la pasarela antes de bloquear
- [X] T091 [US4] C.api/internal/pagos/estado.go + `GET /api/v1/payments/{id}`: consulta la pasarela cuando el intento está pendiente o recién vuelve, aplica la máquina de estados de data-model.md y anexa `attempt_events`; un estado final no cambia salvo `expired → approved` — hecho (`estado.go`): `TransicionValida`, `transicion` con cerrojo, evento sólo cuando cambia; gancho `AlAprobar` para el aviso al ERP (fase 7); además `GET /payments/pending`
- [X] T092 [US4] C.api/internal/http/webhooks.go: `POST /webhooks/placetopay/{siteId}`, firma verificada con el secreto del sitio, 200 inmediato y trabajo encolado; firma inválida → 200 sin efecto + auditoría + alerta si se repite — hecho en `pagos/webhook.go` (no en `http/`): encola `check_payment_attempt` (único por argumentos y minuto); firma inválida o sitio desconocido → 200 + `Canal.Webhook.FirmaInvalida`, y log de ALERTA a partir de 3 en una hora
- [X] T093 [US4] C.api/internal/pagos/sonda.go: trabajo `probe_pending_attempts` (cada 15 min las primeras 24 h, diario hasta 7 días, luego alerta a la cooperativa); frecuencias configurables — hecho: `probe_pending_attempts` cada `CANAL_FRECUENCIA_PENDIENTES`; a los 7 días auditoría `Canal.Pago.PendienteSinResolver` y log de ALERTA una vez — la alerta en la consola de la cooperativa llega con US12
- [X] T094 [P] [US4] C.api/internal/pagos/*_test.go (testcontainers + simulada): doble clic, pendiente bloquea, vencimiento, convergencia de retorno, notificación y sonda sin duplicar, aprobación tras vencimiento — hecho: 9 subpruebas contra PostgreSQL con la simulada (doble clic, pendiente, validaciones, textos y código, convergencia, vencimiento → aprobado, firma inválida, pasarela caída, sonda)

### Canal web — pago

- [X] T095 [P] [US4] C.web/src/pantallas/pago/Carrito.tsx: elegir conceptos del mismo destino, modalidad y valor por concepto, mínimo validado; separación explícita cuando hay dos destinos (FR-032) — hecho (`Carrito.tsx` dentro de `Pagar.tsx`, ruta `/cooperativa/pagar?concepto=`)
- [X] T096 [P] [US4] C.web/src/pantallas/pago/Prepago.tsx: alternativas de reducir plazo o cuota de la simulación, con la preferencia de la cooperativa marcada (FR-032a) — hecho (`Prepago.tsx`, «Ver cómo queda» con la simulación del ERP)
- [X] T097 [US4] C.web/src/pantallas/pago/Resumen.tsx: detalle, impuestos, total, logo de la pasarela, términos y política con aceptación explícita, código si se exige, botón que se deshabilita al primer clic y redirige a `processUrl` (en la app, `AbrirPasarela`) — hecho: el logo es el nombre del destino que configura la cooperativa (no hay recurso gráfico de la pasarela todavía); textos y código se piden cuando el servicio responde 409/403, con la misma llave
- [X] T098 [US4] C.web/src/pantallas/pago/Retorno.tsx (`/pagos/:id/retorno`): consulta el estado y muestra referencia, estado, fecha y hora, valor con moneda, autorización/CUS; aviso de pendiente con el contacto de la cooperativa — hecho: consulta con `?return=1` y vuelve a preguntar cada 10 s mientras siga pendiente
- [X] T099 [P] [US4] C.web/src/pantallas/ayuda/: preguntas frecuentes (de la cooperativa y de la pasarela), términos y política, accesibles desde el pie de página (FR-057) — hecho: `PaginaLegal` lee `GET /api/v1/legal/{terms|policy|faq}` (ruta nueva del canal) con cooperativa elegida; sin ella, dice dónde verlos

**Checkpoint**: el asociado paga y ve el resultado; el ERP aún no lo aplica (fase 7).

---

## Phase 7: User Story 5 — El ERP aplica el pago aprobado (P1)

**Goal**: aviso firmado e idempotente que aplica por Recaudo y los contratos, con comprobante.

**Independent Test**: quickstart C2, pasos 3, 4 y 9; repetir el aviso no cambia nada (SC-004).

- [X] T100 [US5] App/Channel/Service/RegisterChannelPaymentCommand.cs (`IOperacionIdempotente`, `IEscrituraDeRecaudo`, actor `Actor.AsociadoPorCanal`) + validador: total = suma, destino de la cooperativa, conceptos publicados; arma la `LineaNoInteractiva` (`Channel = Online`, `Key = GW:{convenio}:{referencia}`, medio según PSE o tarjeta, cuenta propia del convenio, oficina del convenio, partes por concepto con `ModalidadDePago` y `EleccionDeAbono`) y la aplica con `AplicacionNoInteractiva` en la transacción del comando — hecho 2026-11-13: la ruta es `POST /tenants/{t}/people/{p}/payment-notices` (la persona en la ruta da el actor asociado) y la clave de operación sale de la referencia; `ParteNoInteractiva` ganó `LoanChoice` (aditivo, viaja a la simulación, al pago y a `ReceiptApplication.PrepaymentChoice`); `EmisorDeMensajes` admite al asociado como origen (`OriginActorKind = Member`, sin identidad central) — el consumidor contable lo sigue anotando como proceso porque el sobre v1 no trae el tipo; la publicación no se revalida: un pago aprobado no se rechaza
- [X] T101 [US5] App/Channel/Service/ExcedenteDelAviso.cs: con obligación viva y valor sobrante bajo «exigible» o «parcial», la parte sobrante va como `Advance`; lo demás queda en el saldo a favor que ya crea `AplicacionNoInteractiva` → resultado `InReview` (R6, FR-013, FR-046) — hecho: simula cada concepto; el que ya no admite el pago se retira (queda a favor), el sobrante de exigible o parcial va como `Advance` si la política lo dice; sin ninguna parte aplicable el aviso queda `InReview` sin recibo; alerta nueva `Canal.PagoEnRevision`
- [X] T102 [US5] Persistir `CDG_PaymentNotices` e ítems con el recibo creado; repetición → misma respuesta; `TotalMismatch` → 409 sin aplicar; ruta `POST /tenants/{t}/payment-notices` con `Idempotency-Key` — hecho (la tabla ya existía desde la fase 2): repetición por referencia → misma respuesta; `TotalMismatch` → 409
- [X] T103 [US5] App/Channel/Service/GetChannelReceiptDocumentQuery.cs + `GET /tenants/{t}/people/{p}/receipts/{r}/document`: PDF carta de `GetReceiptDocumentQuery`, sólo recibos del canal de esa persona — hecho: `GetReceiptDocumentQueryHandler.ModeloAsync` compartido con la caja; sólo recibos de avisos de esa persona (`Channel.Receipt.NotFound`)
- [X] T104 [P] [US5] T.App/Channel/RegisterChannelPaymentTests.cs: aplica por prelación, repetición idempotente, obligación cancelada → saldo a favor e `InReview`, prepago con elección, mezcla crédito + aportes (orden FR-170), Recaudo inactivo → 422 — hecho: 10 pruebas (aplica a nombre del asociado, repetición, total, obligación cancelada → saldo a favor y alerta, sin producto aplicable, anticipo del sobrante, elección del abono, crédito antes que cuenta, comprobante sólo de la persona); Recaudo inactivo lo cubre `ModuloActivoBehavior` (la prueba fija que el comando es `IEscrituraDeRecaudo`)
- [X] T105 [US5] Verificar que el aviso corre con el actor asociado que da `ActorDeLaPeticion` (CanalServicioTests ya fija que ningún handler lo fabrica) y verificar que `NingunProcesoAplicaPagosNiInformaResultados` y `Toda_escritura_de_Recaudo_y_Tesoreria_lleva_clave_de_idempotencia` siguen verdes — hecho: Application 7.242 y Architecture 933 en verde, incluidas `NingunProcesoAplicaPagosNiInformaResultados`, la de claves de idempotencia y las del canal
- [X] T106 [US5] C.api/internal/pagos/aviso.go: trabajo `send_payment_notice` al quedar aprobado (encolado en la misma transacción del cambio de estado), reintento con espera creciente, alerta a la cooperativa pasado el umbral, `erp_outcome` y recibo guardados en el intento (FR-047) — hecho (`pagos/aviso.go`): `AlAprobar` por defecto encola `send_payment_notice` en la transacción de la aprobación; espera creciente de 1 min a 1 h; alerta en el intento `UmbralDeAlertaDelAviso` (5); 409/400 del ERP cancela y alerta
- [X] T107 [US5] Ruta `GET /api/v1/payments/{id}/receipt` (proxy del PDF del ERP) y estado «pago recibido, en revisión» en C.web/src/pantallas/pago/Retorno.tsx — hecho: el retorno distingue «registrando», «aplicado» (con «Descargar comprobante») y «pago recibido, en revisión», y sigue preguntando mientras el ERP no responde
- [X] T108 [P] [US5] Prueba de contrato C.api/internal/erp/aviso_test.go: el cuerpo del aviso coincide con contracts/erp-api-de-servicio.md — hecho (`erp/aviso_test.go`): compara las claves del cuerpo con el ejemplo del contrato

**Checkpoint**: C2 completa en DEV con la pasarela simulada; certificación de Avalpay después de la fase 8.

---

## Phase 8: User Story 8 — Historial y comprobantes (P2)

**Goal**: historial por cooperativa con al menos los últimos cinco y comprobante de cada pago aplicado.

**Independent Test**: seis intentos con estados distintos se ven en orden descendente con todos los campos.

- [ ] T109 [US8] C.api/internal/pagos/historial.go + `GET /api/v1/payments?limit=`: por cuenta y cooperativa, descendente, campos de FR-054
- [ ] T110 [P] [US8] C.web/src/pantallas/historial/: lista con fecha y hora, referencia, autorización/CUS, estado y valor `COP`; descarga del comprobante (en la app, `AbrirDocumento`)
- [ ] T111 [US8] Recorrer la lista de certificación de Avalpay en su ambiente de pruebas con PSE y tarjeta (cada estado, doble clic, pendiente, retorno, sonda a 15 min, historial, términos, preguntas frecuentes, logo) y dejar el resultado en docs/operaciones/certificacion-pasarela-avalpay.md, sin credenciales ni texto de la guía confidencial

---

## Phase 9: User Story 6 — Recaudo concilia el canal cada día (P2)

**Goal**: el reporte de la pasarela reconoce lo aplicado en línea y aplica lo aprobado sin aviso, por la misma llave.

**Independent Test**: quickstart C3, paso 1.

- [ ] T112 [US6] En App/Receipts/Agreements/ArchivosDeConvenio.cs, la `Key` de una línea de pasarela pasa a `GW:{convenio}:{referencia}` cuando la referencia tiene la forma del canal (R4, R12); el resto de las líneas no cambia
- [ ] T113 [US6] Índice único `(AgreementId, GatewayReference)` filtrado a no borradas en Per/Configurations/Receipts/ConveniosConfigurations.cs y migración aditiva `CanalDigitalConciliacion` en MigPg/ y MigSql/ (con diagnóstico previo de duplicados en docs/operaciones/canal-digital-despliegue.md)
- [ ] T114 [P] [US6] T.App/Receipts/ConciliacionDelCanalTests.cs: línea ya aplicada en línea → `AlreadyApplied`; aprobada sin aviso → aplicada una vez a nombre del analista; orden aviso/archivo indiferente
- [ ] T115 [US6] Formato de archivo de la pasarela como dato (`COR_BankFileFormats`, ámbito `PaymentGatewayFile`) documentado en docs/manual/canal-digital-conciliacion.md: lo carga el dueño con el reporte real de Avalpay, no va en el repositorio

---

## Phase 10: User Story 7 — Reverso de un pago del canal (P2)

**Goal**: reversar antes del corte por la pasarela y después como devolución por Tesorería.

**Independent Test**: quickstart C3, paso 2.

- [ ] T116 [US7] C.api/internal/pagos/reverso.go + `POST /internal/v1/tenants/{t}/attempts/{reference}/reverse`: verifica el corte de la cooperativa (hora Colombia), reversa en la pasarela, registra el evento y llama `POST /tenants/{t}/payment-notices/{reference}/reversal`; fuera de corte → 409 `Canal.Reverso.FueraDeCorte`
- [ ] T117 [US7] App/Channel/Service/ReverseChannelPaymentNoticeCommand.cs + ruta de servicio: reversa el recibo por el contrato del módulo dueño (asiento espejo) y marca el aviso; idempotente
- [ ] T118 [US7] App/Channel/Payments/ReverseChannelPaymentCommand.cs (usuario, `Channel.Payments.Reverse`, motivo): antes del corte → `ICanalSatelite.ReversarAsync`; después → crea la devolución por Tesorería con el motivo y la referencia
- [ ] T119 [P] [US7] T.App/Channel/ReversoDelCanalTests.cs: antes y después del corte, repetición, estado «reversado» visible para el asociado

---

## Phase 11: User Story 12 — La entidad atiende y supervisa el canal (P2)

**Goal**: búsqueda por documento o referencia, desbloqueo, cierre de sesiones e indicadores.

**Independent Test**: quickstart C3, paso 3.

- [ ] T120 [US12] C.api/internal/http/interno.go: `GET /internal/v1/tenants/{t}/{accounts,attempts,metrics}`, `POST …/accounts/{id}/unlock`, `POST …/accounts/{id}/sessions/revoke`; toda respuesta acotada a personas habilitadas en esa cooperativa (FR-059)
- [ ] T121 [US12] App/Channel/Support/: `SearchChannelSupportQuery` (cuenta + intentos del canal + avisos y recibos del ERP), `UnlockChannelAccountCommand`, `RevokeChannelSessionsCommand`, `GetChannelMetricsQuery`; rutas en Api/Endpoints/Channel/ChannelAdminModule.cs
- [ ] T122 [P] [US12] Sh/Pages/Canal/Atencion.razor (`/canal/atencion`) y Sh/Pages/Canal/Indicadores.razor (`/canal/indicadores`) con `IndicadorDeCarga` y acción de reverso
- [ ] T123 [P] [US12] C.api/internal/http/interno_test.go: una cooperativa no ve cuentas ni intentos de otra

---

## Phase 12: User Story 9 — Extractos y certificados (P2)

**Goal**: documentos publicados descargables, generados por el ERP en el momento.

**Independent Test**: el PDF del canal es igual al del ERP para la misma persona y fecha.

- [ ] T124 [US9] App/Channel/Service/IssueChannelDocumentCommand.cs + `POST /tenants/{t}/people/{p}/documents`: envuelve `IssueLendingDocumentCommand` en modo `Download` para `Statement`, `AccountStatement`, `ContributionCertificate`, `TaxCertificate`, `DepositTaxCertificate`, sólo los publicados; auditado
- [ ] T125 [US9] C.api/internal/conceptos/documentos.go: `GET /api/v1/documents` (los publicados y los productos de la persona) y `POST /api/v1/documents` (proxy del PDF)
- [ ] T126 [P] [US9] C.web/src/pantallas/documentos/: lista y descarga
- [ ] T127 [P] [US9] T.App/Channel/DocumentosDelCanalTests.cs: documento no publicado → no disponible; producto de otra persona → 404

---

## Phase 13: Otros cobros (C2b, extiende US3 y US5)

**Goal**: pagar facturas pendientes de cuentas por cobrar comerciales, incluidos los servicios por vehículo de la 020 si se facturan.

**Independent Test**: una factura pendiente aparece como concepto y su pago queda aplicado contra ella.

- [ ] T128 [US3] Incluir `CommercialReceivable` en GetChannelPayablesQuery.cs con `CuentasPorCobrarDeRecaudo` (facturas pendientes de la persona) cuando el concepto está publicado
- [ ] T129 [US5] Extender App/Receipts/BankItems/AplicacionNoInteractiva.cs para el destino `ApplicationTarget.CommercialReceivable` creando `RCD_CommercialReceivableApplications`, con pruebas en T.App/Receipts/AplicacionNoInteractivaCuentasPorCobrarTests.cs
- [ ] T130 [US5] Permitir el concepto `CommercialReceivable` en RegisterChannelPaymentCommand.cs y en la publicación de conceptos

---

## Phase 14: User Story 10 — Notificaciones (P3)

**Goal**: avisos por correo y push, con preferencias; los de seguridad no se apagan.

**Independent Test**: quickstart C4.

- [ ] T131 [US10] C.api/migrations/00005_avisos.sql: preferencias de aviso por cuenta y `push_subscriptions` (FCM y Web Push)
- [ ] T132 [P] [US10] C.api/internal/mensajeria/push/: FCM HTTP v1 (app) y Web Push con VAPID (PWA instalada); `POST /api/v1/devices/push-token`
- [ ] T133 [US10] C.api/internal/trabajos/avisos.go: `send_notification` (pago aplicado, rechazado, pendiente resuelto, seguridad) y `due_reminders` diario que pide al ERP las cuotas que vencen en la anticipación de cada cooperativa (nueva ruta de servicio `GET /tenants/{t}/due-installments?inDays=` → App/Channel/Service/ListDueInstallmentsQuery.cs)
- [ ] T134 [P] [US10] C.web/src/pantallas/perfil/Avisos.tsx: preferencias, con los de seguridad bloqueados

---

## Phase 15: User Story 11 — App de tiendas (P3)

**Goal**: una sola app con la PWA empaquetada, biometría, push y retorno de la pasarela.

**Independent Test**: quickstart C5.

- [ ] T135 [US11] Crear C.app/IngenIA365Canal.App.csproj (.NET 10 MAUI, Android API 24+, iOS 15+, `ApplicationId` propio del canal) con HybridWebView apuntando a `Resources/Raw/wwwroot`; script canal/scripts/empaquetar-app.sh que copia el build de C.web con rutas de hash
- [ ] T136 [US11] C.app/Puente/PuenteDelCanal.cs (`SetInvokeJavaScriptTarget`): métodos de contracts/pasarela-y-app.md, con `JsonSerializerContext` para el recorte
- [ ] T137 [P] [US11] C.app/Platforms/Android/Biometria.cs (`BiometricPrompt`) y C.app/Platforms/iOS/Biometria.cs (`LAContext`) que liberan la credencial guardada en `SecureStorage`
- [ ] T138 [US11] C.app/Puente/Pasarela.cs: `WebAuthenticator` (Custom Tabs / `ASWebAuthenticationSession`) y App Links / Universal Links para `/pagos/{id}/retorno`; archivos `assetlinks.json` y `apple-app-site-association` servidos por C.api/internal/http/estaticos.go desde configuración
- [ ] T139 [P] [US11] Push con `Plugin.Firebase` en C.app/ (registro del token y apertura por notificación hacia `window.canal.alNotificacion`)
- [ ] T140 [US11] docs/operaciones/canal-digital-app-tiendas.md: firma de Android e iOS **fuera** del CI público, cuentas de desarrollador, publicación manual, versión mínima soportada

---

## Phase 16: User Story 13 — Simulador y solicitud de crédito (P4)

**Goal**: simular con líneas publicadas y enviar una solicitud que llega al ERP con canal de origen.

**Independent Test**: quickstart C6.

- [ ] T141 [US13] Dom/Entities/Lending/Origination/CreditApplication.cs: `IntakeChannel`, `CreatedByUserId` nulable y `CreatedByMemberPublicId`; configuración y migración aditiva `SolicitudDesdeElCanal` en MigPg/ y MigSql/
- [ ] T142 [US13] App/Channel/Service/: `ListPublishedCreditLinesQuery`, `SimulateChannelCreditQuery` (envuelve `SimuladorDeCredito`), `CreateChannelCreditApplicationCommand` (actor asociado, sin notas internas en la respuesta), `ListChannelCreditApplicationsQuery`; rutas de servicio
- [ ] T143 [US13] Verificar que la solicitud de crédito corre con el actor asociado de `ActorDeLaPeticion` y que `CreatedByMemberPublicId` sale de él
- [ ] T144 [US13] C.api/internal/conceptos/credito.go (proxies) y C.web/src/pantallas/credito/ (simulador, solicitud, estado)
- [ ] T145 [P] [US13] T.App/Channel/CreditoEnLineaTests.cs: misma cuota que el simulador del ERP, solicitud con canal de origen

---

## Phase 17: Polish & Cross-Cutting

- [ ] T146 [P] docs/manual/canal-digital.md: configuración para la cooperativa (conceptos, destinos, habilitaciones, textos, atención, reverso, conciliación)
- [ ] T147 [P] docs/operaciones/canal-digital-llaves.md: rotación de la KEK, de las llaves ES256 de servicio (por `kid`) y del secreto de la pasarela; revocación de un dispositivo
- [ ] T148 Sección «Canal digital (feature 021)» en CLAUDE.md con las reglas que no se negocian (satélite sin acceso a bases, aviso idempotente por referencia, actor asociado, conciliación por llave, secreto sólo en el canal) y totales remedidos
- [ ] T149 [P] Revisar que ningún archivo del repositorio contenga credenciales, URLs de sitios de cooperativas, datos de personas ni texto de las guías confidenciales de la pasarela (`git grep` de patrones de llaves y del login de pruebas)
- [ ] T150 [P] Revisión de seguridad del satélite (`/security-review` sobre la rama): cabeceras, cookies, CSRF por `SameSite=Strict` + verificación de origen, límites, enumeración, firma de webhook, `jti`
- [ ] T151 Actualizar la memoria `feature-021-canal-asociados-estado.md` y la nota compartida con el estado final

---

## Phase 18: Pruebas de integración al final

- [ ] T152 T.Int/Canal/CanalServicioTests.cs (fixture `CentralIdentityApiFixture`): token de servicio válido, repetido (`jti`), de otra cooperativa, JWT de usuario rechazado en `/api/canal/v1`
- [ ] T153 T.Int/Canal/AvisoDePagoTests.cs: aviso aplicado contra Cartera y Captaciones reales (Testcontainers), repetición, excedente, reverso; en PostgreSQL y SQL Server
- [ ] T154 T.Int/Canal/ConciliacionDelCanalTests.cs: aviso + archivo de pasarela en los dos órdenes
- [ ] T155 Playwright en C.web/e2e/pago.spec.ts contra el canal con pasarela simulada y un ERP de prueba: activación, ingreso, consulta, pago aprobado, pendiente, historial
- [ ] T156 Correr las suites completas (ERP sin contenedores, integración, `go test ./...`, Vitest, Playwright) y recorrer quickstart.md completo; anotar el resultado con números en specs/021-canal-asociados-pagos/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Fase 1 (Setup)**: T001–T002 son la puerta de todo lo demás. T003–T010 empiezan con la enmienda aprobada.
- **Fase 2 (Foundational)**: depende de la fase 1 y bloquea todas las historias.
- **US1 (fase 3)**: depende de la fase 2.
- **US2 (fase 4)**: depende de US1, porque necesita habilitaciones y el espejo.
- **US3 (fase 5)**: depende de US2, porque necesita sesión y cooperativa elegida.
- **US4 (fase 6)**: depende de US3 (conceptos) y de los destinos de US1.
- **US5 (fase 7)**: depende de US4.
- **US8 (fase 8)**: depende de US5. Es la última pieza antes de la certificación (T111).
- **US6, US7, US12, US9 (fases 9–12)**: dependen de US5 y son independientes entre sí.
- **C2b (fase 13)**: depende de US5.
- **US10 (fase 14)**: depende de US5 para los avisos de pago; los de cuota sólo de US3.
- **US11 (fase 15)**: depende de US4 y US5, porque el flujo de pago debe existir antes de empaquetarlo.
- **US13 (fase 16)**: depende de US3.
- **Polish (fase 17) e integración (fase 18)**: al final.

### Entregas

| Entrega | Fases |
|---|---|
| C0 | 1, 2 |
| C1 | 3, 4, 5 |
| C2 | 6, 7, 8 |
| C2b | 13 |
| C3 | 9, 10, 11 |
| C4 | 12, 14 |
| C5 | 15 |
| C6 | 16 |

### Within Each User Story

- ERP: entidad y configuración, luego comando o consulta con validador, luego endpoint y por último la pantalla.
- Canal: migración, luego dominio y trabajo, luego ruta y por último la pantalla.
- Las pruebas unitarias van junto a su código y las de integración al final.

### Parallel Opportunities

- **Fase 1**: T004–T007 en paralelo.
- **Fase 2**:
  - el ERP (T011–T022) y el canal (T023–T031) avanzan en paralelo, porque se tocan sólo por contrato;
  - el web (T032–T036) también va aparte.
- **US1**: las pantallas T050–T053 van en paralelo entre sí y con T055–T058 del canal.
- **US2**: las pantallas T073–T076 van en paralelo entre sí, y el ERP (T059–T060) en paralelo con el canal.
- **US4**: T085 y T087 van en paralelo con T088–T089, y las pantallas T095, T096 y T099 en paralelo entre sí.
- **Fases 9–12**: pueden repartirse entre personas distintas una vez cerrada la fase 8.
- **Regla de memoria**: como máximo dos sesiones compilando .NET a la vez, con `-m:1`. Go y Node no cuentan para ese límite.

---

## Parallel Example: User Story 1

```text
# ERP y canal a la vez (sólo se tocan por contrato):
T040 App/Channel/Destinations/…            (ERP)
T042 App/Channel/Concepts/…                (ERP)
T055 C.api/migrations/00002_entidades.sql  (canal)
T057 C.api/internal/pasarela/sitios.go     (canal)

# Pantallas a la vez:
T050 Sh/Pages/Canal/Configuracion.razor
T051 Sh/Pages/Canal/Destinos.razor
T052 Sh/Pages/Canal/Conceptos.razor
T053 Sh/Pages/Canal/Habilitaciones.razor
```

## Parallel Example: User Story 4

```text
T085 C.api/internal/pasarela/simulada/
T087 C.api/internal/pasarela/placetopay/placetopay_test.go
T089 C.api/internal/pagos/referencia.go
T095 C.web/src/pantallas/pago/Carrito.tsx
T096 C.web/src/pantallas/pago/Prepago.tsx
T099 C.web/src/pantallas/ayuda/
```

---

## Implementation Strategy

### MVP

El MVP es C0 + C1: las fases 1 a 5, es decir US1, US2 y US3. El asociado entra y consulta sus conceptos, y la cooperativa ya puede medir activaciones. Tiene valor aunque todavía no se pague, y el pago lo agrega C2.

### Incremental Delivery

1. **C0**: con la enmienda aprobada, esqueleto del canal en DEV.
2. **C1**: sólo consulta en DEV y QA; COOFLOPAL puede probarlo después del 01/12.
3. **C2**: pago con la pasarela simulada; certificación con Avalpay (T111); credenciales productivas; producción con autorización expresa del dueño.
4. **C3**: conciliación, reverso y atención. Conviene que estén antes de abrir el pago a todos los asociados.
5. **C4 a C6**: documentos y avisos, la app de tiendas, crédito en línea.

### Producción

Cada paso a producción exige:
- autorización expresa del dueño;
- respaldo de las bases de cooperativa antes de las migraciones `CanalDigital*`, que son aditivas;
- la base `ingenia365_canal` creada;
- los Secrets cargados por el dueño.

## Notes

- Ninguna credencial, URL de sitio de cooperativa, dato de persona ni texto de las guías confidenciales de la pasarela entra al repositorio, que es público.
- El producto nunca nombra el sistema anterior.
- Toda pantalla nueva del ERP lleva `IndicadorDeCarga`.
- Ningún sistema guarda el número de la tarjeta.
- Commit al cerrar cada tarea o grupo lógico. No se usa `git stash`.
