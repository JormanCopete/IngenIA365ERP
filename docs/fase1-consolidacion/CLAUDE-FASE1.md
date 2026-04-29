# CLAUDE.md — IngenIA365ERP (antes SOLIDO ERP)

## Estado General del Proyecto
- Fase 1 COMPLETADA: 33 proyectos VB.NET → ERP.Core consolidado (C# .NET 10)
- Fase 2A COMPLETADA: Rediseño de BD (272 tablas nuevas, DDL, mapeo, migración)
- Fase 2B EN CURSO: Creación de solución Clean Architecture + Frontend

## Historial de Migración
1. Se migraron 33 proyectos de clases (Plugins/) de VB.NET a C# con IA
2. Se consolidaron en ERP.Core (231 archivos, 149K líneas, 9 módulos)
3. Se rediseñó la BD completa: 270 tablas viejas → 272 tablas nuevas en inglés
4. Se generaron scripts de migración de datos (SQL + .NET DataMigrator)

## Fuentes Originales
- Solución VB.NET: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\solido.sln
- Formularios VB.NET: Proyecto "SOLIDO" (272 formularios WinForms)
- Plugins VB.NET: D:\...\SOLIDO\Plugins\ (33 proyectos, ya migrados a ERP.Core)
- Lógica migrada: ERP.Core (C# .NET 10, 231 archivos, 9 módulos)
- Esquema BD original: DBDefinicion.sql (270 tablas, 121 vistas, 8 funciones, SQL Server)
- Base frontend: FlitApp (D:\OneDrive - INGENIA 365\AOS\Proyectos\FLIT\Code\FlitApp)

## Documentación de Referencia
- CONTEXTO-SOLUCION-ORIGINAL.md → Arquitectura completa del sistema original
- MAPA-DE-MIGRACION.md → Equivalencias namespace viejo → nuevo en ERP.Core
- REPORTE-FINAL.md → Resumen de la Fase 1
- PROPUESTA-REDISENO-BD.md → Análisis y rediseño de 270 tablas
- MAPEO-BD-VIEJO-NUEVO.md → Tabla vieja → tabla nueva (270 mapeos)
- IngenIA365ERP_Schema_01 al _12 → DDL completo del nuevo esquema
- ANALISIS-FORMULARIOS.md → Análisis completo de 490 formularios WinForms (tipo, prioridad, mapeo Blazor)
- tools/Migration/SQL/ → 10 scripts de migración de datos
- tools/IngenIA365ERP.DataMigrator/ → Migrador .NET para transformaciones complejas

---

## Decisiones Arquitectónicas Confirmadas

### Nombre y producto
- Nombre: IngenIA365ERP (antes SOLIDO)
- Tipo: ERP Financiero SaaS para cooperativas colombianas
- Multi-tenancy: Schema-per-tenant en SQL Server

### Stack Tecnológico
- Backend: .NET 10.0.5, C# 13
- API: Minimal APIs con Carter
- ORM: EF Core 10 + Dapper (queries complejas/reportes)
- BD Transaccional: SQL Server
- BD Auditoría: MongoDB (bajo costo, alta escritura)
- Caché: Redis
- Mensajería: MassTransit + RabbitMQ (futuro, entre módulos)
- Frontend: Blazor Hybrid MAUI + Blazor Web (patrón FlitApp)
- Componentes UI: SyncFusion Blazor 33.1.44
- CQRS: MediatR
- Validación: FluentValidation
- Mapping: Mapster
- Auth: ASP.NET Core Identity + JWT Bearer (RS256)
- Logging: Serilog → MongoDB
- Observabilidad: OpenTelemetry
- Testing: xUnit + FluentAssertions + ArchUnitNET

### Estrategia de Llaves Primarias (Híbrido)
- Tablas MAESTRAS: INT IDENTITY(1,1) como PK clustered
- Tablas TRANSACCIONALES: BIGINT IDENTITY(1,1) como PK clustered
- TODA tabla: PublicId UNIQUEIDENTIFIER DEFAULT NEWID() con UNIQUE INDEX
- FKs: Siempre al Id interno (INT o BIGINT), NUNCA al PublicId
- API/Frontend: Solo exponen PublicId (GUID), nunca el Id interno
- PKs compuestas originales: Convertidas a UNIQUE INDEX

### Base de Datos Nueva (272 tablas)
- Schema: [dbo] con prefijos por módulo
- Prefijos: COR_, ACC_, LND_, PAY_, INV_, CDT_, DEB_, TRS_, SEC_, AUD_, WEB_, ADM_
- Multi-tenant: Cada tenant tiene su propio schema (tenant_001, tenant_002)
- Columnas estándar en toda tabla: Id, PublicId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy
- NVARCHAR en vez de VARCHAR (Unicode)
- DATETIME2 en vez de SMALLDATETIME
- Centralización de personas: COR_People como tabla maestra con flags IsAssociate, IsEmployee, etc.

### Frontend (Patrón FlitApp)
- 4 proyectos: App (MAUI), Web (Blazor Server), Web.Client (WASM), Shared (componentes)
- Todo el código de UI vive en Shared (una vez, 3 plataformas)
- ISecureStorage: 3 implementaciones (MAUI nativo, Web memoria, WASM)
- IFormFactor: Detecta plataforma
- CustomAuthStateProvider: Compartido
- Mock services para desarrollo sin backend

### ERP.Core (Legacy)
- Se mantiene vía proyecto IngenIA365ERP.Legacy con Adapters
- Adapters traducen entidades viejas (español) → nuevas (inglés, GUIDs)
- Absorción gradual: módulo por módulo se reescribe en Application
- Cuando un módulo está 100% reescrito, se elimina su adapter
- Al final ERP.Core se elimina completamente de la solución

### Seguridad (Nivel Financiero)
- JWT con firma RS256 (asimétrica)
- Access token: 15 min, Refresh token: 7 días con rotación
- Passwords: BCrypt con factor 12
- Datos sensibles (NIT, cuentas banco): AES-256 en reposo
- Row-Level Security (RLS) en SQL Server por tenant
- Rate limiting por tenant e IP
- Security headers (HSTS, CSP, X-Frame-Options)
- MFA opcional por tenant
- Bloqueo de cuenta después de 5 intentos fallidos
- OWASP Top 10 compliance

### Auditoría
- Toda operación CUD se registra en MongoDB
- Campos: TenantId, UserId, Action, EntityType, EntityId, OldValues, NewValues
- Pipeline MediatR: AuditBehavior intercepta todos los Commands
- EF Core Interceptor: AuditableEntityInterceptor llena campos de auditoría
- TTL configurable (default 5 años para financiero)
- No impacta BD transaccional

---

## Estructura de la Solución

```
IngenIA365ERP.sln
│
├── src/
│   ├── Core/
│   │   ├── IngenIA365ERP.Domain/           → Entidades, Value Objects, Eventos
│   │   └── IngenIA365ERP.Application/      → CQRS, Validación, Interfaces
│   │
│   ├── Infrastructure/
│   │   ├── IngenIA365ERP.Persistence/      → EF Core + SQL Server
│   │   ├── IngenIA365ERP.Identity/         → Auth + JWT + Permisos
│   │   ├── IngenIA365ERP.Audit/            → MongoDB
│   │   ├── IngenIA365ERP.Caching/          → Redis
│   │   └── IngenIA365ERP.Legacy/           → Puente a ERP.Core (temporal)
│   │
│   ├── Presentation/
│   │   ├── IngenIA365ERP.API/              → Minimal APIs (backend)
│   │   ├── IngenIA365ERP.App/             → MAUI Hybrid (desktop/móvil)
│   │   ├── IngenIA365ERP.Web/             → Blazor Server
│   │   └── IngenIA365ERP.Web.Client/      → Blazor WASM
│   │
│   └── IngenIA365ERP.Shared/              → Componentes Blazor + SyncFusion
│
├── legacy/
│   └── ERP.Core/                           → Lógica migrada de VB.NET
│
├── tools/
│   ├── IngenIA365ERP.DataMigrator/         → Migración de datos viejo → nuevo
│   └── IngenIA365ERP.DbMigrator/           → Crear schemas por tenant
│
└── tests/
    ├── IngenIA365ERP.Domain.Tests/
    ├── IngenIA365ERP.Application.Tests/
    ├── IngenIA365ERP.API.IntegrationTests/
    └── IngenIA365ERP.Architecture.Tests/
```

## Regla de Dependencias (Clean Architecture)
- Domain → NO depende de nada
- Application → depende solo de Domain
- Persistence, Identity, Audit, Caching, Legacy → dependen de Application + Domain
- API → depende de Application (NO de Persistence directamente)
- Shared → NO depende de backend (solo DTOs propios)
- Web, Web.Client, App → dependen de Shared

## Convenciones de Código
- Minimal APIs con Carter (no controllers)
- CQRS con MediatR: Commands (escritura), Queries (lectura)
- Records para DTOs y Commands/Queries
- Result<T> pattern (no excepciones para flujo de control)
- FluentValidation para toda validación
- Async en todo I/O
- Español para nombres de dominio de negocio, inglés para infraestructura
- Entidades con PascalCase mapeadas a tablas del DDL nuevo
- BaseEntity (INT Id) para maestras, BaseEntityLong (BIGINT Id) para transaccionales

## Mapeo de Controles WinForms → SyncFusion Blazor
- DataGridView → SfGrid
- TextBox → SfTextBox
- ComboBox → SfDropDownList / SfComboBox
- DateTimePicker → SfDatePicker
- NumericUpDown → SfNumericTextBox
- TabControl → SfTab
- TreeView → SfTreeView
- ListView → SfListView
- Chart → SfChart
- MessageBox → SfDialog
- ToolStrip → SfToolbar
- MenuStrip → SfMenu
- StatusStrip → Barra CSS
- Crystal Reports → QuestPDF / Bold Reports
- MDI Forms → SfSidebar + routing Blazor
- Form.Show() → NavigationManager.NavigateTo()
- Form.ShowDialog() → SfDialog con IsModal=true
- PrintDocument → QuestPDF + SfPdfViewer

## Prefijos de Tablas BD
| Prefijo | Módulo | Tablas |
|---|---|---|
| COR_ | Core/Sistema | 42 |
| ACC_ | Contabilidad | 33 |
| LND_ | Cartera Financiera | 94 |
| PAY_ | Nómina | 27 |
| INV_ | Inventario | 24 |
| CDT_ | Certificados | 7 |
| DEB_ | Tarjetas Débito | 7 |
| TRS_ | Tesorería | 3 |
| SEC_ | Seguridad | 11 |
| AUD_ | Auditoría | 14 |
| WEB_ | Portal Web | 6 |
| ADM_ | Admin Multi-tenant | 3 |

---

## Progreso

### Fase 1: Consolidación VB.NET → C# (COMPLETADA)
- [x] 33 proyectos VB.NET → C#
- [x] 33 proyectos C# → ERP.Core consolidado
- [x] 7 ciclos circulares resueltos
- [x] 6,500 líneas duplicadas eliminadas
- [x] Crystal Reports aislado con #if CRYSTAL_LEGACY
- [x] 9 archivos de documentación generados
- [x] 0 errores de namespace/referencia

### Fase 2A: Rediseño de Base de Datos (COMPLETADA)
- [x] Análisis de 270 tablas originales
- [x] Propuesta de rediseño aprobada (PROPUESTA-REDISENO-BD.md)
- [x] DDL nuevo: 12 archivos SQL, 272 tablas, 11,296 líneas
- [x] Mapeo viejo→nuevo: 1,547 líneas (MAPEO-BD-VIEJO-NUEVO.md)
- [x] Scripts migración SQL: 10 archivos, 8,677 líneas
- [x] DataMigrator .NET: 2,051 líneas

### Fase 2B: Estructura Clean Architecture (EN CURSO)
- [ ] Crear solución IngenIA365ERP.sln (~13 proyectos)
- [ ] Generar entidades Domain desde DDL nuevo
- [ ] Generar EF Core Configurations
- [ ] Configurar SyncFusion 33.1.44

### Fase 2C: Infraestructura (COMPLETADA)
- [x] Multi-tenancy schema-per-tenant (Finbuckle, TenantResolutionMiddleware, schema routing)
- [x] Seguridad financiera: ASP.NET Core Identity + JWT RS256 + BCrypt + AES-256 encriptación
  - 14 archivos en Identity/ (Models, Services, Policies, Seed)
  - ErpIdentityDbContext con tablas SEC_
  - JwtService (RS256, claims: PublicId, TenantId, Permissions)
  - AuthenticationService (login lockout 5/30min, refresh rotation)
  - PermissionService (Redis cache, 112 permisos, 8 roles)
  - EncryptionService (Data Protection API)
  - PermissionAuthorizationHandler + PermissionRequirement
  - SecurityHeadersMiddleware (HSTS, CSP, X-Frame-Options)
  - Rate limiting (login 10/min, general 1000/min)
  - 5 endpoints auth: login, logout, refresh, change-password, me
- [x] Auditoría MongoDB
  - MongoAuditService con ConcurrentQueue batching + flush timer
  - AuditLog + AccessLog (colecciones por tenant: audit_{tenantId}, access_{tenantId})
  - MongoDbInitializer (índices: timestamp, entity, user, module + TTL 5 años/90 días)
  - AuditableEntityInterceptor (captura OldValues/NewValues automático en SaveChanges)
  - AuditBehavior (intercepta Commands MediatR, infiere Action/EntityType/Module)
  - Fallback a archivo local si MongoDB no disponible
  - 4 endpoints audit: logs (paginado+filtros), entity history, user activity, access logs
  - Retención configurable: 1825 días audit, 90 días access

### Fase 2D: Frontend SyncFusion (COMPLETADA)
- [x] Layout profesional con SyncFusion (SfSidebar Push + SfToolbar + SfToast)
  - MainLayout: sidebar colapsable, toolbar con usuario/tenant/logout, footer, loading overlay
  - NavMenu: jerárquico con NavMenuGroup colapsable, 9 secciones, 30+ items de navegación
  - MinimalLayout: para login (sin sidebar/toolbar)
  - Tema: fluent2-dark.css, paleta oscura profesional con accent #00bcd4
- [x] Login mejorado: SfDropDownList tenant, SfTextBox email/password, spinner, diseño centrado oscuro
- [x] Dashboard con SyncFusion: 4 KPIs (SfCard), 2 SfChart (barras+línea), 2 SfGrid (movimientos+vencimientos)
- [x] Componentes compartidos reutilizables (5):
  - PersonSearchDialog: SfDialog+SfGrid búsqueda por NIT/nombre (reemplazo terceros SOLIDO)
  - AccountSearchDialog: SfDialog+SfGrid búsqueda plan cuentas por código/nombre
  - DateRangeSelector: 2 SfDatePicker con presets (Hoy, Semana, Mes, Año)
  - ConfirmDialog: SfDialog modal (reemplazo MessageBox.Show WinForms)
  - LoadingOverlay: SfSpinner pantalla completa con servicio inyectable
- [x] Servicios UI: NotificationService (Success/Error/Warning/Info → SfToast), LoadingService
- [x] 25 páginas placeholder por módulo con [Authorize] y SfProgressBar
- [x] _Imports.razor con 20 namespaces SyncFusion
- [x] 3 Program.cs actualizados: SyncfusionBlazor + license + servicios
- [x] CSS tema fluent2-dark en hosts HTML (Web App.razor + MAUI index.html)
- [x] Análisis de 490 formularios completado (ANALISIS-FORMULARIOS.md, 983 líneas)
- [ ] PdfViewer para reportes (pendiente)

### Fase 2E: Migración de Formularios (EN CURSO)
- [x] Sub-grupo P1.1: 14 Maestros Core (CQRS + API + Blazor)
- [x] Sub-grupo P1.2: 22 Maestros Contabilidad (13) + Cartera (9) — CQRS + API + Blazor
- [x] Sub-grupo P1.3: 38 Maestros Nomina (11) + Inventario (15) + CDT (2) + Debito (3) + Tesoreria (1) + Cartera extras (6)
- [x] Grupo P1 COMPLETADO: 74 maestros migrados, 375+ rutas REST, 106 DbSets
- [x] Grupo P2: Contabilidad transaccional — Comprobantes (CRUD+void+post), Movimientos, Saldos, Conciliacion, Presupuestos
- [x] Grupo P3.1: Cartera Creditos — Solicitudes (CRUD+approve+reject+disburse), Cartera, Recaudos, Mora+Cobro, Ficha Asociado
- [x] Grupo P3.2: Cartera Ahorros+Aportes+CDT+Retiros — Cuentas ahorro (open+deposit+withdraw+close), Aportes, CDT (create+renew+cancel), Retiro asociado
- [x] Grupo P3 COMPLETADO: 9 modulos transaccionales de cartera financiera
- [x] Grupo P4: Inventario (documentos+facturacion+kardex) + Nomina (registro+liquidacion+novedades) + Debito (tarjetas+transacciones) + Tesoreria (cheques+facturas+flujo caja)
- [x] Grupo P5: Procesos batch — Cierre periodo, Causacion intereses, Calificacion cartera SFC, Liquidacion CDT/Ahorro, Descuento nomina, Extractos, Cert. retencion + ProcessExecutor component
- [x] Grupo P6: 16 Reportes PDF (QuestPDF) — Balance General, Estado Resultados, Libro Mayor, Comprobante, Cert. Retencion, Extracto Credito/Ahorro, Cartera Edades/Calificacion, CDT, Nomina, Inventario, Factura

### Fase 2E COMPLETADA: 382 archivos CQRS, 113 endpoints, 15 reportes PDF, 136 paginas Blazor, 0 errores

### Fase 3: Infraestructura y Deployment (COMPLETADA)
- [x] Docker Compose: sqlserver, mongodb, redis, rabbitmq, api, web (6 servicios)
- [x] Dockerfiles multi-stage: API + Web (SDK build → aspnet runtime)
- [x] docker-compose.override.yml para desarrollo con hot reload
- [x] .dockerignore configurado
- [x] Scripts BD: init-database.sql (3 tablas ADM_*), init-tenant.sql (seed departamentos, ciudades, bancos), init-dev.ps1 (orquestador)
- [x] CI/CD: GitHub Actions (build+test+docker push a GHCR)
- [x] Tests expandidos: 17 Domain + 28 Application = 45 tests pasando
- [x] Validators tests: CreateDocument (partida doble), CreateLoanApplication (monto/plazo), ProcessPayment
- [x] appsettings: Development (debug), Production (CORS restrictivo, swagger off), base completo
- [x] README.md con inicio rapido, estructura, modulos
- [x] 0 referencias a "Solido" o "FlitApp" en src/
- [ ] Ejecutar DataMigrator con datos reales (pendiente datos de produccion)
- [ ] Testing integral end-to-end
