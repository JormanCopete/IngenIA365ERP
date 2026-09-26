# Research: Inventario comercial renovado — catálogo, existencias, compras, ventas, POS y facturación electrónica

**Feature**: 012 | **Fecha**: 2026-09-24 | **Rama**: `012-inventario-comercial` | **Spec**: [spec.md](spec.md)

Fase 0 del plan. Las decisiones del dueño están en `spec.md › Clarifications` (sesión 2026-09-24):

- **Q1 A**: COOFLOPAL sale con I1 a I4. Está obligada a facturar electrónicamente, y esa obligación
  es un parámetro de cada cooperativa. Ninguna fecha queda fija en estos artefactos.
- **Q2 B**: mensajes asíncronos con validación previa y modo de paso a contabilidad en línea, por
  lotes o no pasa. Enmienda la 009 para Inventario.
- **Q3 C**: proveedor tecnológico externo primero y software propio por el servicio central de la
  010 después, detrás del mismo adaptador.
- La entrega IC (comunicación con Cartera) queda pendiente, con crédito provisional mientras tanto.
- Los medios de pago son dinámicos, múltiples y configurables, y se contabilizan solos por la matriz;
  el cierre de caja es por punto y por medio (sección K).
- Las 20 decisiones por defecto de los Supuestos, todas aceptadas.

Lo demás se resolvió con siete informes de investigación del 2026-09-24: contabilidad, mensajería,
núcleo, ventas y POS, DIAN, seguridad, y compras, impuestos y cartera. Cada uno trae sus hechos
verificados en el código y sus decisiones. Un documento de armonización,
[decisiones-transversales.md](decisiones-transversales.md) en esta misma carpeta (nombres canónicos en
su §2 y decisiones T1 a T52 en su §3), fija los nombres y **gana sobre cualquier informe** cuando se
contradicen. Lo que
un informe propuso y la armonización cambió aparece aquí como alternativa descartada, para que nadie
lo vuelva a introducir.

Cada decisión sigue Decisión / Por qué / Alternativas descartadas / Riesgos / Spec. Los nombres de
tablas, entidades, enumeraciones, comandos, rutas, permisos, parámetros, mensajes, alertas y pruebas
son los canónicos, y `data-model.md`, `contracts/` y `quickstart.md` los repiten sin variantes. Nada
de lo que sigue es un valor legal escrito en el programa: tarifas, bases, plazos y códigos DIAN son
datos con vigencia y norma (FR-012, FR-013).

**Convención de citas.** Las rutas son del repositorio en la rama (`src/…`, `tests/…`). Los URL son de
la sesión de investigación. Las normas sin URL se citan por número, tal como las trae el informe: su
texto no se cotejó en esta fase y lo confirma la contadora de COOFLOPAL (Rafaela Lastra España) antes
del ensayo. Los siete informes fueron material de trabajo de la sesión y no se publican: lo que de
ellos se usó está en este documento y en `decisiones-transversales.md`.

## Índice de decisiones

| R | Tema | Entrega |
|---|---|---|
| R1 | Integración con Contabilidad y enmienda de la 009 | I2 |
| R2 | Validación previa y modo de paso a contabilidad | I1 (sello) · I2 (efecto) |
| R3 | Mensajería: bandeja de salida, entrega garantizada, orden y lotes | I1 · I2 |
| R4 | Idempotencia de las operaciones | I1 |
| R5 | Procesamiento por cooperativa, actor y trabajos de fondo | I1 |
| R6 | Modelo de documentos: clases fijas y tipos parametrizables | I1 |
| R7 | Kardex, proyecciones y hechos inmutables | I1 |
| R8 | Concurrencia de existencias | I1 |
| R9 | Numeración | I1 · I4 |
| R10 | Costeo | I1 · I5 |
| R11 | Precisión decimal, redondeo y fecha de operación | I1 |
| R12 | Parámetros con vigencia | I1 |
| R13 | Aprobaciones multinivel | I1 |
| R14 | Permisos, alcance por bodega y punto de venta, y montos máximos | I1 · I3 |
| R15 | Auditoría verificable | I1 |
| R16 | Alertas | I1 |
| R17 | Catálogo, búsqueda de productos e importación | I1 |
| R18 | Bodegas, tránsito, traslados y conteos | I1 |
| R19 | Períodos, saldo inicial, activación por bodega y convivencia con SOLIDO | I1 · I2 |
| R20 | Compras y cruce | I1 · I4 · I5 |
| R21 | Impuestos y retenciones | I1 |
| R22 | Personas: copia fiscal, consumidor final y autorización de datos | I1 · I3 |
| R23 | Ventas, precios, descuentos y promociones | I3 · I6 |
| R24 | Punto de venta | I3 |
| R25 | Medios de pago y cierre de caja | I3 |
| R26 | Crédito provisional y frontera con Cartera | I3 · IC |
| R27 | DIAN: adaptador, canónico y canales | I4 |
| R28 | DIAN: numeración fiscal, resoluciones, estados y procesamiento | I4 |
| R29 | DIAN: contingencias y documentos rechazados | I4 |
| R30 | DIAN: obligación, cambio de canal, representación gráfica, artefactos y RADIAN | I1 · I3 · I4 · I5 |
| R31 | Reportes y tablero | I1 a I6 |
| R32 | Pantallas, menú y clientes | I1 a I6 |
| R33 | Retiro del módulo actual y migraciones | I1 a I6 |
| R34 | Pruebas | I1 a I6 |

## Hechos verificados del código

Estado del repositorio el 2026-09-24 (`develop` en `04bc649`, más este worktree).

### El módulo actual de inventario

- **24 entidades** en `src/Core/IngenIA365ERP.Domain/Entities/Inventory/*.cs`, mapeadas a 24 tablas
  `INV_*` (`Persistence/Configurations/Inventory/*Configuration.cs`). Están en la base de toda
  cooperativa y en los dos motores.
  - Todas sus llaves foráneas salen de otras `INV_`; ninguna tabla de otro módulo apunta a ellas
    (snapshot de PostgreSQL, ~l. 36440-36620).
  - `INV_Salespeople` sólo apunta a `COR_People`.
- **La existencia es `int` y está en dos sitios** que nadie reconcilia: `InventoryTransaction.Quantity`
  y `Product.CurrentStock`.
  - `CreateInventoryDocumentCommand.cs:108` numera con `txType.SequenceNumber + 1`, y en las líneas
    179 y 205 hace `product.CurrentStock += signedQty`. Ninguna de las dos está protegida ante
    concurrencia.
  - Sus tres escritores contables llevan el marcador `E3 (feature 009)`
    (`CreateInventoryDocumentCommand.cs:209`, `CreateInvoiceCommand.cs:169`,
    `VoidInventoryDocumentCommand.cs:65`).
- **API, Application y pantallas**:
  - 17 archivos en `API/Endpoints/Inventory/` con 84 rutas, más 3 en
    `Endpoints/Reports/InventoryReportsEndpoints.cs`: 87 en total. `grep RequirePermission` sobre
    ellos da 0.
  - 66 archivos en 17 carpetas de `Application/Inventory`.
  - 20 páginas: 19 en `Shared/Pages/Inventario/` y `Pages/Reportes/InventarioValorizado.razor`, sin
    cliente tipado. Las enlazan o describen `NavMenu.razor` (94-111), `CentroReportes.razor:175,183`
    y `ManualCatalogo.cs` (~759-808), que describe Facturación como si funcionara.
- **Permisos**: los de inventario sólo están en `Identity/Seed/IdentitySeedData.cs:110-125`, una
  semilla retirada, y en el glob `Inventory.*` del rol `InventoryManager` (l. 182). Nadie los ejecuta.
- **Vendedores**:
  - `CreateSalespersonCommand` y `DeleteSalespersonCommand` ya escriben la fila y la marca
    `IsSalesperson` juntas, como exige la 008.
  - `UK_INV_Salespeople_PersonId` es único **sin filtro**: volver a asignar el rol a una persona
    retirada da un 500.
- **Heredados que confunden y nadie usa**:
  - `COR_PaymentMethods` (`sys_forpago`): desglose de pago por comprobante con columnas fijas.
  - `COR_Sequences` (`sys_consecu`).
  - `DEB_PosTerminals`: datáfonos del emisor de la tarjeta débito propia.
  - `COR_Companies.Dian*`: una sola resolución (`Company.cs:48-59`).
  - `InvoicePrintReport.cs`: sin CUFE ni QR.
- **SOLIDO**:
  - No factura electrónicamente: el grep de cufe y de nombres de proveedores en sus `.vb` no
    encuentra nada.
  - Guarda los pagos en columnas fijas (`inv_docs`, `sys_forpago`).
  - Su cierre de caja no arquea (`clsmsgtiket.cs`), e `Inv_frmforpag.cs` admite una sola clase de
    pago por venta.

### Plataforma: ejecución fuera de HTTP, identidad y auditoría

- **No hay bandeja de salida, despachador, bus ni `INotificationHandler<>`**.
  - `BaseEntity.DomainEvents` existe y nadie lo usa.
  - `HabeasDataRevokedEvent` se publica después del commit, sin durabilidad
    (`RevokeConsentCommand.cs:92`).
- **Trabajos de fondo**:
  - `NotificationEmailDispatcher` (Storage; sondeo de 15 s, `MaxAttempts` 4) recorre
    `ITenantDirectory.ListActiveAsync` y abre `ITenantDbContextFactory.Abrir(...)`. Es un DbContext
    suelto, sin MediatR, sin actor y sin candado: con las 2 réplicas de PDN puede mandar dos veces el
    mismo correo.
  - Los handlers del contenedor no ven el contexto que arma `TenantDbContextFactory`.
- **Sin `HttpContext`, todo cae en silencio en otro sitio**:
  - la fábrica de `ErpTenantInfo` (`Persistence/DependencyInjection.cs`) devuelve la base de
    **plantilla**;
  - `TenantContextAccessor` y `CurrentUserService` son singletons que sólo leen `HttpContext`;
  - `MongoAuditService` toma `TenantId ?? SufijoGlobal`, así que lo que corre en segundo plano cae en
    `IngenIA365ERP_Audit_Global`, y `AccountingAuditEmitter` hace lo mismo.
- **`ICurrentUserService.UserId` es siempre nulo con la identidad central**: `CentralJwtIssuer.cs:133-144`
  emite `sub` = Guid y ningún `uid`. Las consecuencias:
  - `AccountingPoster` sella `RegisteredByUserId = 0`.
  - Con `FourEyes` activo, `DocumentCommands.cs:281` compara `0 == 0` y bloquearía a todos. La e2e sólo
    prueba el caso negativo (`ContabilidadNiifTests.cs:290-305`).
  - `ListMyNotificationsQuery` responde `Auth.Unauthorized`.
  - La auditoría guarda `system`.

  Sí resuelven bien `SEC_Users.Id` `QuienLlamaEnLaCooperativa` (restablecer MFA) y
  `PermisosDeLaPeticion.ResolverUsuarioYCooperativaAsync`.
- **Pipeline**: Validation → Logging → Audit → ReintentoPorConcurrencia → Performance
  (`Application/DependencyInjection.cs`).
  - `AuditBehavior` audita sólo `*Command`, guarda la petición entera y registra 200 aunque el
    resultado falle. No llena IP, User-Agent, antes/después ni motivo.
  - Infiere el módulo del espacio de nombres (`.Inventory` → «Inventory»; `AuditBehavior.cs:108`,
    `AuditableEntityInterceptor.cs:217`).
  - `AuditableEntityInterceptor` sí captura antes, después y campos cambiados de toda entidad, en el
    mismo flujo asíncrono.
- **Auditoría en Mongo**:
  - `MongoAuditService` es una cola en memoria con volcado por temporizador y respaldo a `Logs/`: la
    entrega no está garantizada.
  - `AuditRetention` da 10 años sólo a `Accounting` y `Navigation`, y el índice TTL `ttl_expiresAt`
    borra documentos.
  - El rol `audit_appendOnly` de `database/migration/15_Audit_Mongodb_Bootstrap.json` apunta a la base
    única `IngenIA365ERP_Audit`, no a las `IngenIA365ERP_Audit_{tenant}`.
  - `AuditSignatureSettings` trae por defecto la clave `dev-v1`, escrita en el código, y ningún
    appsettings del repositorio la reemplaza.
  - No hay sello por evento ni cadena.
- **Origen de la petición**: `IIpAddressAccessor` existe (CF-Connecting-IP → X-Forwarded-For →
  RemoteIp) y `AuditBehavior` no lo usa. No hay accesor de User-Agent ni cabecera de canal.
  `RenovacionDeSesionHandler` clona todas las cabeceras al reintentar.
- **Fecha**: `IDateTimeService` sólo da `UtcNow` y `TodayUtc` (`API/Services/DateTimeService.cs`), y
  después de las 19:00 de Bogotá `TodayUtc` ya es el día siguiente. No hay `TimeZoneInfo` ni
  `America/Bogota` en `src`.
- **Candados**: `IDistributedLock` (Redis, base 0, sin renovación de TTL) lo usan nómina e
  invitaciones. `IDbProviderConfigurator` ya abstrae `sp_getapplock`/`pg_advisory_lock` para el
  arranque.

### Transacciones, concurrencia y numeración

- **Concurrencia optimista**:
  - `BaseEntity.RowVersion` se mapea a `rowversion` o `xmin` (`ProviderModelConventions.ApplyPortableRowVersion`).
  - `DbUpdateConcurrencyException` se traduce a `ConcurrencyConflictException`.
  - `ReintentoPorConcurrenciaBehavior` reintenta hasta 5 veces los `IReintentableAnteConcurrencia` y
    llama `DescartarCambios()`, que vacía todo el ChangeTracker del ámbito.
- **Transacciones explícitas**:
  - La única está en `Payroll/Settlements/Settlement/TransaccionDeLiquidacion.cs`
    (`CreateExecutionStrategy` + `BeginTransactionAsync`). Su comentario documenta que un comando
    reintentable anidado vació el tracker del externo.
  - Los dos motores corren con `EnableRetryOnFailure(3)`.
  - No hay `SELECT … FOR UPDATE`, `UPDLOCK` ni candado de aplicación en el código de negocio, y
    ninguna base usa `READ_COMMITTED_SNAPSHOT`.
- **Numeración contable**: `AccountingPoster` numera con `VoucherType.NextNumber` en memoria más
  `RowVersion` (009 R3). La prueba `NingunModuloEscribeMovimientosFueraDelContrato` marca **cualquier**
  `.NextNumber =|++|+=` fuera de `Accounting/Posting`; por eso la 010 usó `LastIssuedNumber`.
- **Índices**:
  - Precedente de índice único con número nulo: `UK_ACC_Documents_Type_Number` con
    `HasFilter("[Number] IS NOT NULL")`.
  - `ApplyPortableIndexFilters` traduce los filtros escritos en T-SQL.
  - Una colisión de índice único sólo se traduce, por nombre, en `PersonFactory.EsColisionDeDocumento`
    (`Core/People/Services/PersonFactory.cs:117-135`).
- **Decimales**: la convención global es (18,2) (`ApplicationDbContext.ConfigureConventions`, l. 440),
  y PostgreSQL redondea en silencio al insertar.

### Contabilidad (feature 009)

- **`AccountingPoster`** (`Application/Accounting/Posting`):
  - `PrepareAsync` valida, numera y agrega sin guardar.
  - `ValidarAsync` es el dry-run (el mismo `AnalizarAsync`, reglas 1 a 11), pero deja `VoucherType` y
    `ChartOfAccounts` rastreados en el contexto de quien llama.
  - `PrepareReversalAsync` crea `Kind = Reversal`, marca el original `Reversed` y corre la fecha al
    primer período abierto.
  - El alcance de sucursal sólo se aplica si el origen es `CNT`.
  - `PostingRequest` no tiene campo para un usuario de origen distinto del actor.
- **Reglas de cuenta**: `AccountLineRules.Evaluar` es puro. Su regla 10 compara base ×
  `TarifaVigenteDe(cuenta, fecha)` —una tarifa por cuenta y fecha, `AccountTaxRate.Rate` fracción
  (9,4)— contra `AccountingSetup.TaxTolerance`. Ni un impuesto por unidad ni una cuenta con varias
  tarifas caben en esa regla.
- **Parametrizaciones de otros módulos**: usan `AccountEligibility` (cuenta de movimiento, activa y
  habilitada para el módulo; FR-016 de la 009). Hay dos precedentes:
  - `PAY_ConceptDefinitionAccounts`: concepto por texto, sin vigencia;
  - `AddPolicyVersionCommand`: vigencia sin cruces, cierra la anterior la víspera.
- **Tipos de comprobante**:
  - `voucher-types.json` trae `FV`, `EI` y `SI`, reservados a INV.
  - `VoucherTypesSeeder` (Order 60) salta en silencio un código que ya existe con otro uso.
  - `cross-document-types.json` trae FV, FC, CC, NC, ND, CT, PG y OT.
  - «FP» es de TES.
- **Cierre y reversión**:
  - `ClosePeriodCommand` sólo bloquea por borradores. El precedente de «confirmar para seguir» es
    `GeneratePilaCommand.AcknowledgeWarnings`.
  - `ReverseDocumentCommand` rechaza lo que nació en un módulo (`Accounting.Document.ModuleOwned`).
  - `IX_ACC_Documents_Origin_Source` no es único: hoy la única defensa contra comprobantes duplicados
    es la atomicidad módulo + comprobante.
- **Lectura del libro**:
  - `MovimientosContables` es el único punto de lectura y siempre aplica el alcance de sucursal del
    usuario.
  - `PendingDocumentsQuery` (vista `pending-documents` de la E2) da los saldos por cuenta, tercero y
    documento cruce.
- **Piezas que la 012 toca**:
  - `AccountReferenceFinder` todavía busca en `INV_ProductAccounts` e `INV_VatAccounts`.
  - `EnlacesDeOrigen` existe dos veces y sólo mapea Nómina.
  - `ListInvalidParameterizationsQuery` sólo revisa Nómina.
  - Los textos de la 009 que la 012 enmienda están verificados línea a línea: spec FR-016, FR-030,
    FR-036 a FR-041, FR-070, US4 y SC-004; research R2 y R16; contrato §4 y §5; data-model; tasks
    T120, T125, T128 y T129; plan; manual.

### Parámetros, catálogos e importación

- **Vigencia en nómina**:
  - la tabla `PAY_CompanyPolicies` (`CompanyPolicy.cs`: Key 60, Value 400, ValidFrom/ValidTo, Notes);
  - el catálogo cerrado `CompanyPolicyKeys` y el lector único `PayrollPolicyReader`;
  - el alta `AddPolicyVersionCommand` y la semilla de Order 75.

  No tiene ámbito. `COR_SystemSettings` no tiene vigencia ni historial.
- **Códigos y semillas**:
  - `CodigoDeCatalogo` (`Application/Common/Catalogos`): 10 o 20 caracteres, en mayúsculas, con
    `Catalogo.CodigoDuplicado`.
  - `BuscarCodigoDeCatalogoQuery` resuelve por slug.
  - Las semillas paramétricas de cada cooperativa van por `IDataSeeder.Order`; el último usado es 76
    (`BankFileFormatsSeeder`).
- **Plantillas de importación**:
  - `PlantillaDeImportacion` pone los encabezados en la fila 1 y las instrucciones en otra hoja, pero
    formatea toda columna decimal como `0.00`.
  - `ImportAccountsCommand` es todo o nada, con `ErrorDeFila(Row, Column, Code, Message)`
    (`Accounting/Setup/SetupDtos.cs:35`).
  - Ningún importador tiene revisión previa sin guardar.
- **Motores puros con casos dorados en JSON**: `Domain/Payroll/{Calculation,Settlements,Pila,Withholding}`,
  con casos en `tests/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/*.json` que el csproj copia.
  `LaNominaNoTieneValoresLegalesFijos` es el modelo de prueba sin valores legales.
- **Catálogo transversal en Core con vigencia**: `COR_BankFileFormats` (D-42 de la 010), ligado a
  `COR_Banks` (con `TransferCode` ACH y `PersonId`).

### Seguridad y permisos

- **Autorización de rutas**:
  - `.RequirePermission` (`PermissionAuthorizationFilter`, semántica AND) responde el mismo 404 que
    una ruta inexistente.
  - `RequirePermissionWhenExporting` exige un segundo permiso sólo con `?format=xlsx|pdf|docx`.
  - Los permisos se resuelven por petición (`PermisosDeLaPeticion` → `UserPermissionResolver`, caché
    de 30 min).
  - `PermisosDelHandler` devuelve la lista vacía sin `HttpContext`.
- **Catálogo y roles**:
  - Los seeders de permisos son estáticos y de sólo inserción (`Domain`, `Payroll`, `Core`,
    `Accounting`). Los invoca `PhaseZeroSecuritySeeder` antes de `BuiltInRolesSeeder`.
  - Hay cuatro roles integrados, definidos por globs: `*.View` llega a Operator, ReadOnly y Auditor, y
    los vínculos se reinsertan en cada arranque.
  - `SEC_RolePermissions` es sólo (RoleId, PermissionId): no admite montos. No existen límites por
    monto ni aprobaciones multinivel.
- **Alcance**: `IUserBranchScope`/`AlcanceDeSucursales` va por oficina (`SEC_UserBranchAssignments` →
  `COR_Branches.TenantBranchPublicId`). Sin asignaciones no restringe, y sólo limita la digitación
  contable.
- **Segregación**: hoy hay tres variantes:
  - `FourEyes`, que compara el `UserId` nulo;
  - `AllowSameUserApproval`, que compara correos;
  - la doble aprobación de MFA, que compara `SEC_Users.Id`.

  `IWebAuthnService` ofrece `CrearOpcionesDeIngreso` y `VerificarIngresoAsync`, reutilizables para
  aprobar en el terminal.
- **Notificaciones**:
  - `COR_Notifications` es una fila por destinatario, sin «atendida por».
  - `NotificationType` es un enum cerrado.
  - El empuje por SignalR no llega: el hub usa `SEC_Users.PublicId` y la identidad es el `sub` central.
  - No existe la consulta «usuarios que tienen el permiso X».
- **Habeas Data**:
  - `HabeasDataPolicyVersion` y `HabeasDataConsent` sólo tienen las acciones `Accepted` y `Revoked`.
  - `HabeasDataModule` recibe `personId:int`, un Id interno.
  - `AcceptConsentCommand` exige `Compliance.HabeasData.RecordConsent`, que Operator no tiene.
  - `PersonaDialog` no tiene paso de consentimiento.
- **Pruebas de arquitectura que hay que ampliar**:
  - `LosEndpointsProtegidosExigenPermiso` no cubre Inventory.
  - `LasPantallasDicenQueEstanCargando` sólo cubre `ModulosMigrados` = Maestros, Nomina, Asociados y
    Contabilidad.
  - `PrincipioXI_ContableImmutable` protege Accounting, Lending y Payroll `/Transactions`, y sólo
    detecta `Remove`.
  - `PrincipioXII_MigracionesDestructivas` exige el marcador ante `DropTable`, `DropColumn`,
    `DropForeignKey` o SQL destructivo; `DropIndex` no la dispara.
  - `Feature004_MigrationParity` sólo compara nombres lógicos.
  - `TodoEnlaceDelMenuTieneSuPagina` y `ManualCatalogoTests`.

### Impuestos, personas y municipios

- **No hay catálogo de impuestos fuera de Contabilidad**. `ChartOfAccount.TaxKind` = {None,
  Withholding, Vat, Ica, Gmf, IncomeTax}: no hay INC ni tipos propios para ReteIVA y ReteICA.
  `WithholdingCertificate` y `TaxForm`, de la E4 de la 009, no tienen handlers.
- **Datos tributarios de la persona** (`Person.cs:144-170`):
  - existen `WithholdingExempt`, `IcaWithholdingExempt`, `TaxRegime` (2), `IcaType`,
    `IsLargeContributor`, `IcaRate`, `SourceWithholding`, `CiiuCode`, `PersonType`, `DaneCityCode` y el
    `IdType` heredado («C»);
  - ninguno está en `PersonInput` ni lo escribe Application;
  - faltan responsable de IVA, autorretenedor, agente retenedor de IVA, régimen simple, declarante y
    obligado a facturar, y las responsabilidades DIAN.

  La dispersión ya traduce «C» → «CC» a mano (`PayrollDisbursementLines.cs:130`).
- **La cooperativa**: su régimen es texto libre (`Tenant.TaxRegime`, 50), y su identidad fiscal está
  repetida en `COR_Companies` y en `ElectronicPayrollSettings`.
- **Municipios**: `COR_Cities` no tiene código DANE ni semilla. `COR_Branches` y `TenantBranch` no
  tienen municipio. Nómina guarda el DANE como texto de 5 dígitos, sin catálogo.
- **UVT**: vive en `PAY_LegalParameters` (código `UVT`, 52.374, con fuente «Resolución DIAN 000238 del
  15-12-2025»; `PayrollLegalParametersSeeder.cs:120`, Order 71, sembrada en toda cooperativa). Cada
  cargador de nómina la lee por su cuenta.

### Cartera

- **No ofrece consultas ni recibe mensajes**.
  - `LoanPortfolio` guarda saldos corrientes, sin vínculo a documentos comerciales ni historia a la
    fecha.
  - La única vía que crea obligaciones es `DisburseLoanCommand`: exige `LoanApplication`, numera con
    max+1 y amortiza con `double`.
- **Otros módulos leen sus tablas directamente**: Nómina (`SettlementInputLoader.cs:639`,
  `ApproveSettlementCommand.cs:136`) y la ficha de persona (`GetPersonDetailQuery.cs:146-170`).
- **El asociado en el maestro** trae, heredados de SOLIDO, `Status`, `AssociateClass` (texto de 4, sin
  catálogo), `CreditLimit`, `PosCardLimit`, `IsInLegalCollection` y `WithdrawalDate`.

### Presentación

- **Dónde corre el front**: en el cliente (`@rendermode InteractiveWebAssembly`), y la app es un
  `BlazorWebView`.
  - Los JS globales se cargan con `@Assets` en `App.razor` y a mano en `App/wwwroot/index.html`.
  - `MauiProgram.cs` no registra `PermisosDelUsuario`, `PersonasClient`, `NominaClient`,
    `ContabilidadClient` ni `DescargaDeArchivos`: toda pantalla con `PermissionGate` o con cliente
    tipado falla en la app.
- **Informes**:
  - `TablaExportable` admite columnas ocultas que empiezan con `_`.
  - `EntregaDeInformes.EntregarAsync` entrega json, xlsx, pdf o docx.
  - `AccountingReportsEndpoints` es el patrón de una ruta por vista.
  - Todos los QuestPDF son tamaño carta; no hay tirilla de 80 mm.
  - QRCoder sólo lo referencia `IngenIA365ERP.Identity`.
- **Búsqueda de texto**: `string.Contains` o `EF.Functions.Like('%x%')`, sin `pg_trgm`, columna
  normalizada ni full-text. En PostgreSQL, `LIKE` distingue mayúsculas y tildes.
- **Teclado y layouts**: los precedentes de captura por teclado son `LineasDeComprobante.razor` y
  `BuscadorDeCuenta.razor`. `MainLayout` lleva `AvisoDeVencimientoDeSesion` y `RegistroDeAccesos`;
  `MinimalLayout` no lleva ninguno.

### Facturación electrónica y adjuntos

- **No existe nada de factura electrónica**: el grep de cufe, cude, cuds y TechnicalKey en `src` no da
  resultados.
- **La nómina electrónica de la 010 tiene esquema sin lógica**: `ElectronicPayrollSettings`,
  `ElectronicPayrollDocument` y los enums `ElectronicPayrollMode` y `DianEnvironment` en `Enums/Payroll`.
  - El servicio central sin estado **no existe**: no hay `src/Servicios`, y las tareas N3 T108-T115
    están sin empezar.
  - Su contrato fija JWT RS256 de 5 min, secretos por nombre, «el servicio nunca reintenta» y «el
    estado vive en la base de la cooperativa».
  - La 010 dejó manual la consulta de estado (D-11) porque no había trabajo de fondo por cooperativa.
- **Adjuntos**:
  - `UploadAttachmentCommand` guarda lo que genera un módulo (`Direct`, `Available`), pero hace
    `int.Parse` de `ICurrentUserService.TenantId`, así que falla fuera de una petición.
  - `AdjuntosDeModulo.DeModulo` es el único sitio que decide leer, subir y borrar, con **un** permiso de
    lectura por tipo de dueño.
  - `AttachmentPolicy.AllowedMimeTypes` no admite `application/xml` ni `application/zip`.
  - `IEmailSender` ya admite adjuntos.
- **Secretos**: `crear-secreto-smtp.ps1` arma el Secret por STDIN sobre SSH, y la API lo lee al
  arrancar. No existe `crear-secreto-nomina-electronica.ps1`.
- **Aislar una librería**: el precedente es `LaCriptografiaDeWebAuthnNoSeFiltra`.

## Precisiones que la investigación hace a la spec

Cada una es una **precisión aplicada a la spec** (spec.md queda enmendada antes de generar tasks.md).
Ninguna cambia una decisión del dueño; las que esperan su confirmación nombran la pregunta de «Lo que
falta del dueño» y aplican la propuesta por defecto mientras tanto.

1. **Norma DIAN.** FR-063 cita la Res. 000165 de 2023 y la 000167 de 2021. Las dos están compiladas en
   la **Resolución Única 000227 del 23-09-2025, Título 5, anexo T5.1**
   (https://www.dian.gov.co/normatividad/Paginas/Resolucion-000227-del-23092025.aspx), como ya citó la
   010. El anexo técnico vigente de la factura es el 1.9, y el del documento equivalente, el 1.0 (R27).
   FR-063 cita «Resolución Única 000227 de 2025, Título 5 (compila la 000165 de 2023 y la 000167 de
   2021)».
2. **Documento equivalente POS.**
   - No tiene el tope de 5 UVT: ese tope es del tiquete POS tradicional (ET art. 616-1 par. 2).
   - Se valida **antes** de entregarse, como la factura (Sovos, R28).
   - Si el comprador pide factura después de emitido, hace falta una nota de ajuste más la factura
     (Actualícese). La spec lo adoptó en FR-063 (I4); contrato en `contracts/api.md` §18.3.1.
3. **Contingencia del facturador (tipo 03).** La norma y los proveedores dicen transmitir dentro de las
   48 h siguientes a superar el inconveniente; la página de inconvenientes de la DIAN todavía dice «30
   días». El plazo es un parámetro con norma (`Dian.PlazoContingenciaHoras`), no una constante (R29).
4. **«Total a pagar» (FR-056).** Si el comprador es agente retenedor, paga el total menos sus
   retenciones. La igualdad «suma de pagos = total» se evalúa contra `AmountDue` (R21). FR-056 dice:
   «La suma de los pagos MUST igualar el total a pagar: el total menos las retenciones que practica el
   comprador agente retenedor».
5. **Nombres de mensaje sin tildes.** Los tipos se guardan como `DevolucionRegistrada`,
   `NotaCreditoEmitida` y `NotaDebitoEmitida`; la pantalla los muestra con tilde.
6. **Impuestos saludables.** La Ley 2277 de 2022 creó el ICUI y el IBUA, que la spec no nombra. El
   modelo de impuesto por unidad con factor los admite (pregunta A7).
7. **XML de la factura del proveedor.** La spec dice que se lee y no se guarda. La obligación de
   conservar soportes (ET art. 632) podría exigir guardarlo (pregunta E10; por defecto, no se guarda).
8. **Reemplazo de un rechazado (FR-066 b).** Exige que, desde I1, el índice único de `INV_Documents`
   excluya el número liberado (`FiscalNumberReleased`), aunque la emisión llegue en I4 (R9).
9. **Bodegas que siguen en SOLIDO en la conciliación (FR-081, SC-005).** «La diferencia no las
   cuenta» se lee «la diferencia no se les atribuye: entran con sus cifras importadas». Las bodegas no
   activas que comparten cuentas con las activas **suman al valorizado del conjunto** con sus cifras
   de SOLIDO a la fecha de corte, como en FR-090, y se muestran aparte para identificarlas. Si
   quedaran fuera, la diferencia sería su valor y SC-005 (diferencia cero) no se cumpliría durante la
   transición (R19).
10. **Retroactivos que no dependen del parámetro (FR-045).** «Los ajustes que el sistema genera desde
    un conteo aprobado, y el saldo inicial de una bodega todavía no activa (con su anulación), no
    dependen de `Costeo.RetroactivosPermitidos`». El motor los inserta en su lugar (`OperationDate`,
    `Id`), recalcula las salidas posteriores cuando cambia su costo y registra
    `AjusteDeCostoReconocido` por documento afectado; ese retroactivo mínimo se entrega en I1 (R10,
    R19; preguntas D8 y D9).
11. **Permisos que dependen del cuerpo (FR-009).** Cuando el recurso ya es visible y el permiso que
    falta depende del cuerpo (costo indicado, aceptar la diferencia de activación, remisiones sin
    facturar, tipo fiscal sin paso, clave de parámetro), la respuesta es **422 con código propio**,
    no 404, porque no revela existencia (R14; pregunta C10).
12. **Medición de SC-004 cuando el canal no responde.** Una venta del POS que agota
    `Dian.EsperaMaximaPosSegundos` sin respuesta cuenta como falla para `CircuitoDeCanal`. Pasado
    `Dian.UmbralFallasCircuito`, las ventas nuevas abren la contingencia 03 y reciben en el acto su
    representación de papel, que es «el documento de contingencia» de SC-004. Las que quedaron con
    resultado ambiguo antes de abrirse el circuito se entregan al validarse y no se renumeran (R28,
    R29).

## R1. Integración con Contabilidad y enmienda de la 009

**Decisión**: Inventario nunca registra comprobantes ni conoce cuentas: emite mensajes (R3), y
Contabilidad los consume con piezas nuevas de su lado, que se construyen en esta feature (I2) como
enmienda de la 009 (D-01).

- **Unidad de contabilización.** Son los mensajes de un mismo evento de origen (`OriginPublicId` +
  `OriginEventKey`) con el mismo destino, y dan **un comprobante por documento**: `VentaFacturada` y
  `CostoDeVentaReconocido` salen en un solo `FV` (FR-077).
  - La procesa `PostInventoryMessagesCommand(MessagePublicIds, BatchPublicId?)`, en
    `Application/Accounting/Inventory/Contabilizacion`: es `IReintentableAnteConcurrencia`, tiene su
    validador y no tiene ruta ni permisos.
  - Lee el contenido por `IMensajesEntrantes` y verifica la versión aceptada (`VersionesAceptadas`), la
    moneda COP con tasa 1 y que el original ya esté contabilizado. Si el original no lo está, responde
    `Retry` con `Accounting.InventoryMessage.WaitingForOriginal`.
  - Arma las líneas con `ConstructorDeLineasDeInventario`, llama `AccountingPoster.PrepareAsync` y agrega
    `ACC_InventoryPostings` (UK `MessagePublicId`), todo en **un** `SaveChanges`. Una colisión del
    índice se traduce a `AlreadyProcessed` (molde `PersonFactory.EsColisionDeDocumento`).
  - Un mensaje informativo deja su fila sin comprobante; uno de valor cero, una fila «sin comprobante
    (valor cero)».
  - El consumidor no toca tablas de la plataforma: el despachador, en otro ámbito, envía
    `RegisterDeliveryResultCommand` (R3).
  - Origen del comprobante: `AccountingOrigin("INV", "InventoryDocument", documento.PublicId)`, o
    `AccountingOrigin("INV", "InventoryPostingBatch", lote.PublicId)` si es resumido. Fecha: la de
    operación.
- **Matriz de reglas.** Una sola tabla, `ACC_InventoryPostingRules`, con `Operation` (catálogo fijo
  `OperacionesDeInventario`, 20 operaciones) y `Role` (`RolesDeCuenta`, 17 roles).
  - Las dimensiones del módulo y de Core van **por código** (`AccountingGroupCode`, `WarehouseCode`,
    `PointOfSaleCode`, `PaymentMeansCode`, `TaxRateCode` + `TaxRate`, `ReasonCode`). Sólo hay FK a
    `COR_Branches` y `COR_CostCenters`.
  - `DimensionKey` se normaliza con `*` y lleva UK filtrado `(DimensionKey, ValidFrom)`.
  - `ResolutorDeReglas` resuelve por especificidad, con pesos 16 (bodega o punto), 8 (centro de costo),
    4 (sucursal) y 2 (grupo).
  - La vigencia sigue el molde de `AddPolicyVersionCommand`: sin cruces
    (`Accounting.InventoryRule.Overlaps`) y sin versiones que empiecen antes de lo ya contabilizado
    (`Accounting.InventoryRule.RetroactiveOverPosted`).
  - Cada cuenta pasa por `AccountEligibility.Verificar(…, ModuloContable.Inventario)`, y la de impuesto
    debe ser de un `TaxKind` compatible.
  - Se administra con `Accounting.InventoryRules.View/Manage`, en la pantalla
    `/contabilidad/inventario/matriz` y con su plantilla (`ImportInventoryPostingRulesCommand`,
    `GetInventoryRulesTemplateQuery`, mecánica de `ImportAccountsCommand`).
- **Tipos de comprobante.**
  - Se conservan `FV`, `EI` y `SI`, y se siembran `NV` (notas de venta), `CP` (factura del proveedor,
    documento soporte y sus notas), `TR` (traslados), `AC` (ajustes de costo y reclasificaciones) y `CJ`
    (caja del POS). Todos con `Usage = Module` y `ModuleCode = INV`.
  - El mapeo es dato, en `ACC_InventoryVoucherMappings`: operación (y, opcionalmente, código de tipo de
    documento de Inventario) → tipo de comprobante y documento cruce. Lo siembra
    `InventoryVoucherMappingsSeeder` (Order 84).
  - El tipo de la unidad lo da su **mensaje principal**, el comercial antes que el de costo.
    `DocumentoAnulado` usa el de su original.
  - Cruces por defecto: `FV` en ventas, `NC`/`ND` en notas y `FC` en compras.
  - `VoucherTypesSeeder` registra una colisión en el log en vez de saltarla en silencio.
- **Las correcciones son comprobantes nuevos** (C7).
  - Nada de Inventario usa `PrepareReversalAsync`: el poster lo rechaza con
    `Accounting.Document.InventoryCorrectsWithNewVoucher`.
  - Anulación, nota, devolución o ajuste son cada uno un comprobante `Kind = Regular`, de su propio
    mensaje y con su propia fecha.
  - `DocumentoAnulado` espeja las cuentas del original, resolviendo la matriz **a la fecha del
    original**. Si su fecha cae en un período cerrado, se rechaza a la bandeja: la fecha nunca se mueve.
  - El vínculo vive en `ACC_InventoryPostings.RelatedDocumentPublicId`, y el comprobante original nunca
    se marca `Reversed`.
  - `ReverseDocumentCommand` conserva `ModuleOwned`, con un mensaje propio para INV.
- **Frontera.** Dos puertos, en `Application/Common/Integration/Accounting`:
  - `IContabilidadParaInventario` declara **exactamente** `EvaluarAsync`, `SaldosDeCuentasMapeadasAsync`,
    `CompletitudAsync` y `PrevisualizarLoteAsync` (las cuatro consultas de FR-014), con
    `MensajeContableDto`, `ResultadoDeContabilizacionDto` y `ConjuntoDeCuentasDto`. Lo implementa el
    adaptador `ContabilidadParaInventario`.
  - `IDimensionesDeInventario` lo implementa Inventario, y lo consumen la matriz y la completitud.

  Lo vigila `InventarioNoConoceContabilidadNiCartera` (R34).
- **Consultas.**
  - `InventoryAccountBalancesQuery` arma los conjuntos de cuentas como componentes conexos grupo ↔
    cuenta (roles `Inventario` y `Transito`). Lee los saldos sólo por `MovimientosContables`, con una
    sobrecarga de `PrepararAsync` de alcance explícito (`SinRestriccion`): la cifra se compara con el
    valorizado completo.
  - `InventoryRulesCompletenessQuery` recibe de Inventario las combinaciones en uso y devuelve lo que
    falta (FR-082, incluido C8).
  - `PreviewInventoryBatchQuery` arma y valida sin agregar.
  - Se amplían `ListInvalidParameterizationsQuery` y `AccountReferenceFinder`, que suma la matriz.
- **Cierre contable.** `ClosePeriodCommand` gana `AcknowledgeInventoryPending`.
  - Si hay entregas a Contabilidad pendientes, en lote o rechazadas, fechadas en el mes
    (`PendingInventoryMessagesQuery`), y no vino el reconocimiento, falla con
    `Accounting.Period.InventoryPending`.
  - La pantalla de Períodos ofrece «Procesar ahora» o «Cerrar de todos modos», y cerrar queda auditado.
  - Después, esos mensajes se rechazan por período cerrado y se recuperan reabriendo el mes, según la 009.
- **Enmiendas a la 009**, en texto, código y pruebas, dentro de I2.
  - Textos:
    - spec: FR-016, FR-030, FR-036 a FR-041, FR-070, US4 (la frase «si rechaza, la operación del
      módulo falla completa» y los escenarios 5 y 7), SC-004, la entrada y el alcance;
    - research: una nota en R2 y el reemplazo de R16;
    - `contracts/contabilizacion.md` §4 y §5, data-model, plan, el manual del contrato y CLAUDE.md;
    - tasks: T120 (parte Inventario), T125, T128 y T129, marcadas «reemplazadas por la 012».
  - Código:
    - `AccountingPoster`: `ValidarVariosAsync` (sin seguimiento) y la guarda INV en
      `PrepareReversalAsync`;
    - `PostingRequest.RegistradoPor` (`UsuarioDeOrigen?`): el usuario del documento como dato, nunca
      como actor;
    - `ClosePeriodCommand.AcknowledgeInventoryPending`;
    - `EnlacesDeOrigen` (`InventoryDocument`, `InventoryPostingBatch`), en Application y en Shared;
    - `ACC_AccountTaxRates.Rate` pasa de (9,4) a (9,6);
    - `AccountingPermissionCatalogSeeder` suma `Accounting.InventoryRules.View/Manage` y
      `Accounting.InventoryBatches.View/Run`;
    - `AccountingAuditEmitter` deja de caer a la base global.
  - Pruebas: `NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioXI_ContableImmutable` y
    `LaContabilidadNoTieneCuentasEnCodigo` quedan sin cambios y verdes.

**Por qué**:
- Es el molde de la 009 R2 (el módulo arma las líneas; el contrato valida, numera y agrega sin
  guardar; quien llama guarda todo junto), sumado al de Nómina (`PayrollAccountingPoster`). El camino
  al libro sigue siendo uno.
- Guardar comprobante y recibo en el mismo `SaveChanges` reemplaza la atomicidad «operación +
  comprobante» de la 009: en Inventario pasa a ser «documento + mensaje», y en Contabilidad, «mensaje +
  comprobante».
- El índice único por `MessagePublicId` es lo único que resiste a dos réplicas procesando el mismo
  mensaje: el índice de origen de `ACC_Documents` no es único, y un resumido reúne muchos documentos.
- Una tabla con rol da un solo motor de resolución, una plantilla, una completitud y un punto de
  validación. Los códigos (no FK a `INV_`) respetan R6 en los dos sentidos y sobreviven al retiro del
  módulo actual.
- Espejar el original a su fecha deja en cero las cuentas de balance aunque la matriz cambie, que es
  lo que la conciliación necesita.
- El comprobante nuevo con fecha propia es lo que el dueño aceptó en C7: la anulación afecta el
  período en que ocurre, y un resumido no se reversa por un documento.
- Los componentes conexos son la única forma correcta de conciliar cuando dos grupos comparten cuenta.

**Alternativas descartadas**:
- (a) Que Inventario llame `AccountingPoster` dentro de su comando (el contrato sincrónico de la 009):
  lo descartó el dueño (C1, Q2 B).
- (b) Un `INotificationHandler` de MediatR: no devuelve `Result` ni pasa por auditoría ni reintento.
- (c) Un comando por mensaje: parte la factura en dos comprobantes.
- (d) Una tabla ancha con una columna por rol, o tres tablas (producto, impuestos, medios): con la
  ancha, cada rol nuevo sería una migración; con tres, habría tres motores para una misma regla.
- (e) FK a `INV_AccountingGroups` o `INV_Warehouses`: acopla los esquemas.
- (f) Un índice único con columnas nulas: SQL Server y PostgreSQL tratan `NULL` de forma distinta.
- (g) `PrepareReversalAsync` sobre el original: reversa todo el resumido, corre la fecha y marca
  `Reversed`.
- (h) Una columna `CorrectsDocumentId` en `ACC_Documents`: no cubre el n:1 del resumido.
- (i) Resolver la anulación con las reglas de su propia fecha: deja saldo huérfano en la cuenta vieja.
- (j) `FC` como tipo de comprobante de la factura del proveedor (informe de compras): `FC` es un
  documento cruce, no un tipo de comprobante. Se usa `CP`.
- (k) `ACC_IntegrationReceipts` (mensajería) y `ContabilizarUnidadDeInventarioCommand` (contabilidad):
  se unifican en `ACC_InventoryPostings` y `PostInventoryMessagesCommand` (T1, T11).
- (l) Dejar la 009 intacta y documentar la excepción sólo aquí: el Principio I exige que la spec
  vigente diga lo que hace el código.

**Riesgos**:
- Todo el consumidor depende de la plataforma de R3 y R5, que no existe.
- SC-020 (5.000 documentos en menos de 10 min) exige `ValidarVariosAsync` con carga en bloque, y hay
  que medirlo.
- Los impuestos por unidad no cumplen la regla 10 de la 009: su cuenta va sin «exige base gravable»
  (pregunta E11).
- Un resumido de un día de POS trae miles de líneas de impuesto con detalle (pregunta G8).
- Si una cooperativa ya creó `NV`, `CP`, `TR`, `AC` o `CJ` con otro uso, la contabilización falla con
  `NotAllowedForModule` hasta remapear.
- Con la conciliación sin alcance, un usuario limitado por sucursal ve los totales de todas las
  sucursales (sólo agregados; pregunta G5).
- La 009 sólo reabre el último ejercicio cerrado, así que un mensaje rechazado de un año anterior puede
  quedar sin camino.
- Hoy inactivar una cuenta de la matriz no avisa, y detiene las ventas en la validación previa
  (pregunta G7).
- El retiro de I1 debe quitar de `AccountReferenceFinder` las referencias a
  `ProductAccounts`/`VatAccounts` en el mismo cambio, o no compila.
- `ACC_InventoryPostings` crece del orden de 3 a 4 millones de filas al año por cooperativa (Id
  `long`, índices por documento, comprobante y fecha).

**Spec**: FR-014, FR-069, FR-073, FR-076, FR-077, FR-079, FR-081, FR-082, FR-083, FR-090, FR-098, US7,
SC-003, SC-005, SC-011, SC-020, SC-024; C1, C2, C3, C7, C8; D-01; Supuestos 4 y 5.

## R2. Validación previa y modo de paso a contabilidad

**Decisión**:
- **Validación previa.** `IContabilidadParaInventario.EvaluarAsync` → `EvaluateInventoryPostingQuery`.
  - Arma las líneas con el **mismo** `ConstructorDeLineasDeInventario` del consumidor y las somete a
    `AccountingPoster.ValidarVariosAsync`: las mismas reglas 1 a 11 de la 009, con carga en bloque, sin
    seguimiento y sin numerar.
  - Suma las comprobaciones de la matriz: regla faltante (`Accounting.InventoryRule.Missing`); tarifa
    del catálogo distinta de la de la cuenta a esa fecha (`Accounting.InventoryRule.TaxRateMismatch`,
    que nombra el impuesto, la cuenta y las dos tarifas: C8); tipo de comprobante sin mapear o
    inactivo; descuadre; más de 2 decimales.
  - Responde `IsPostable`, más errores y avisos con `lineNumber`, cuenta, regla y
    `WhoFixes { module, page, permission }`. Quién corrige:
    - la matriz: `Accounting.InventoryRules.Manage`;
    - una regla de cuenta: `Accounting.Accounts.Manage`, o el propio documento si le falta un dato;
    - el período: `Accounting.Periods.*`;
    - el tipo de comprobante: `Accounting.VoucherTypes.Manage`.
  - Si no es contabilizable, Inventario responde `Inventory.Prevalidation.NotPostable` con
    `data.errors[] {lineNumber, account, rule, whoFixes}`.
- **Dónde corre.**
  - En proceso y **fuera del cerrojo** de existencias: es el paso 5 del flujo canónico, antes del
    cerrojo del paso 6.
  - Con tiempo máximo `Contabilidad.ValidacionPreviaSegundos` (3 s).
  - Si no responde (excepción o tiempo agotado), aplica `Contabilidad.PoliticaSinRespuesta`:
    `ConfirmarConPendiente` por defecto (R6), o `Bloquear`.
  - El resultado se sella en `IntegrationMessage.PrevalidationOutcome` (`Postable`, `NoResponse`,
    `NotApplicable`) para medir SC-021.
  - Los montos de costo que usa son provisionales (el promedio leído sin bloqueo). Las reglas de
    cuenta no dependen de ellos, salvo el caso de valor cero.
  - Sólo se evalúa si el modo que se sellará es distinto de `NotPosted`. El saldo inicial y los
    informativos no se evalúan.
- **Modo de paso.** Parámetro `Contabilidad.ModoDePaso`: `EnLinea` por defecto, `PorLotes` o `NoPasa`.
  - Tiene un valor general y una excepción por tipo de documento **declarada por cadena**
    (`PostingChain` { Purchases, Sales, Transfers }, según `ClasesDeDocumento`).
  - Cambiar un tipo sin los demás de su cadena se rechaza con `Inventory.PostingMode.ChainMismatch`.
  - Dejar sin paso un tipo fiscal, o cambiar el valor general cuando un tipo fiscal lo hereda, exige
    `Inventory.DocumentTypes.DisableFiscalPosting` y una confirmación explícita que nombre esos tipos
    (`Inventory.PostingMode.FiscalRequiresConfirmation`).
  - El documento **sella** `PostingMode` al confirmar. La entrega a Contabilidad nace `Pending` (Online),
    `InBatch` con `ScheduleKey` y `BatchScopeKey` (Batch) o `NotApplicable` (NotPosted).
  - La granularidad (`Contabilidad.Granularidad`: `PorDocumento`, `Resumido`), el disparador
    (`Contabilidad.DisparadorDeLote`: `HoraDiaria`, `CierreDeTurno`, `CierreDePeriodo`) y la hora
    (`Contabilidad.HoraDeLote`, hora de Colombia, 23:00 por defecto) tienen los mismos ámbitos.
- **Herencia.** Estos documentos y mensajes **no leen el parámetro**: copian el modo y la `ScheduleKey`
  de la entrega de su original u origen, también el horario de lote (pregunta G9):
  - los derivados: factura desde remisiones; factura del proveedor, documento soporte o devolución
    contra una recepción; recepción de traslado;
  - los relacionados: anulación y notas;
  - cada parte de `AjusteDeCostoReconocido`.

  Un derivado que reúna orígenes con destinos distintos se rechaza. `GrupoContableReclasificado` usa
  el valor general. Los mensajes a Cartera y los informativos van con `DeliveryMode.Always`.
- **Enviar después lo que no pasó** (FR-078): `SendNotApplicableMessagesCommand`
  (`Inventory.Messages.SendNotApplicable`, con motivo y clave de idempotencia).
  - Crea un lote `SendNotApplicable` y cambia en bloque `NotApplicable` → `InBatch` (con ese lote) en
    toda la clausura de dependencias de los documentos del rango, con la persona como actor.
  - Si un documento cae en un período contable cerrado, su envío se rechaza completo a la bandeja.

**Por qué**:
- Usar el mismo constructor y el mismo `AnalizarAsync` hace verdadera SC-021 por construcción, no por
  disciplina, como la 009 garantizó «las mismas reglas» con una sola `AccountLineRules`.
- El dueño aceptó aplicar las reglas de cuenta completas, no sólo «¿hay regla?» (Supuesto 6).
- Correr fuera del cerrojo evita que una consulta contable alargue el bloqueo de las filas de
  existencia que comparten 30 cajas (T15).
- Sellar el modo en la entrega, no en el mensaje inmutable, cumple FR-075 sin tocar lo emitido.
- La herencia impide que una factura pase sin el costo de su remisión (Supuesto 19).
- `ValidarAsync` ya existe y no consume numeración; la versión en bloque evita cargar la configuración
  contable documento por documento.

**Alternativas descartadas**:
- (a) Sólo verificar que exista una regla: el dueño pidió las mismas reglas de la 009.
- (b) Validar dentro de Inventario leyendo `ChartOfAccounts`: viola FR-014.
- (c) Llamar `PrepareAsync` y descartar el contexto: consume `NextNumber` en memoria y ensucia el
  tracker del comando de Inventario.
- (d) Validar dentro del cerrojo: serializa las cajas mientras responde Contabilidad.
- (e) Resolver el modo en el despachador al entregar: un cambio de parámetro alteraría documentos ya
  confirmados.
- (f) Validar por HTTP interno: agrega latencia sin aislar más (es el mismo proceso y la misma base).

**Riesgos**:
- La evaluación usa costos provisionales, así que un documento de valor cero puede evaluarse distinto
  de como se contabiliza. El riesgo se acota a ese caso.
- Con la política por defecto, un mes contable cerrado o una cuenta inactivada detiene las ventas del
  POS en la validación. El reporte de completitud y el aviso del cierre lo anticipan.
- SC-019 depende de que la validación quepa en 3 s con 30 cajas: hay que medirlo.
- La granularidad por cadena obliga a que la plantilla de tipos de documento (FR-095) lo explique, para
  que COOFLOPAL no cargue cadenas mezcladas.

**Spec**: FR-012, FR-014, FR-015, FR-074, FR-075, FR-078, FR-079, FR-082, US7 esc. 1 a 4 y 8 a 10,
SC-010, SC-021; Supuestos 3, 6, 18 y 19; C8; pregunta D7.

## R3. Mensajería: bandeja de salida, entrega garantizada, orden y lotes

**Decisión**:
- **Tablas en la base de cada cooperativa** (T7).
  - En **I1**:
    - `COR_IntegrationMessages` (`IntegrationMessage`): hecho inmutable. `PublicId` = MessageId, y el
      Id `bigint` identity es el orden. Lleva `Type`, `Version`, `Kind` (Business | Informational),
      origen, relacionado, `ChainRootPublicId`, fecha de operación, sucursal, persona, moneda y tasa,
      `PayloadJson` en texto, `PayloadSha256`, `PrevalidationOutcome` y usuario de origen. UK
      `(OriginPublicId, Type, OriginEventKey)`; `OriginEventKey` separa los mensajes del mismo tipo de un
      origen (`Confirmation`, `Confirmation:{affectedDocumentPublicId:N}`,
      `Confirmation:{paymentPublicId:N}`, `Close:{closingVersion}`, `Reopen:{closingVersion}`,
      `Reclassification`; contracts/mensajes.md §10.1). El Id `bigint` no sale del módulo: ninguna
      respuesta HTTP lo expone.
    - `COR_IntegrationMessageDependencies`: arista inmutable «este mensaje espera a aquél».
    - `COR_IntegrationMessageDeliveries`: estado por destino, mutable con `RowVersion` (`Destination`,
      `Mode`, `Status`, `ScheduleKey`, `BatchScopeKey`, `BatchId`, `Attempts`, `NextAttemptAt`, último
      error, `ProcessedAt`, `ResultReference`, `ResultVoucherTypeCode`, `ResultVoucherNumber`). UK
      `(MessageId, Destination)`. El consumidor devuelve el comprobante en
      `ResultadoDeConsumo.Processed` (`AccountingDocumentPublicId`, `VoucherTypeCode`, `VoucherNumber`)
      y `RegisterDeliveryResultCommand` lo guarda aquí, así la bandeja lo muestra sin leer `ACC_`.
  - En **I2**: `COR_IntegrationDeliveryAttempts` (bitácora de intentos), `COR_IntegrationBatches` y
    `COR_IntegrationBatchCounters` (`NextValue`).
- **Emisión.**
  - `EmisorDeMensajes` agrega mensaje, entregas y dependencias **sin guardar**, dentro del
    `SaveChanges` del documento (molde `AccountingPoster`).
  - Desde I1, todo documento confirmado nace con sus mensajes. Sus entregas a Contabilidad quedan
    `Pending` sin consumidor hasta I2: el despachador salta un destino sin consumidor registrado, sin
    contar intentos ni alertar.
  - Contratos: records en `Application/Common/Integration/Contracts/Inventory/<Type>V1.cs`, sobre
    `IntegrationEnvelopeV1`. Propiedades en inglés, JSON camelCase, enums como texto, decimales exactos
    y tarifas como **fracción**.
  - El contrato de cada tipo y versión está en `contracts/mensajes.md`.
  - Un cambio incompatible crea `V2`. El destino declara lo que acepta
    (`IDestinoDeMensajes.Acepta(type, version)`), y una versión no aceptada se rechaza con
    `Integration.VersionNotAccepted`. Lo emitido nunca se reescribe.
- **Los veinte tipos** (FR-069). El detalle de clases emisoras y contenido está en
  `contracts/mensajes.md`.

  | `Type` | Destino · `Kind` | Operación de la matriz | Comprobante |
  |---|---|---|---|
  | `VentaFacturada` | Accounting · Business | `Venta` | FV |
  | `CostoDeVentaReconocido` | Accounting · Business | `CostoDeVenta` | FV (SI si es remisión) |
  | `CompraRecibida` | Accounting · Business | `Compra` | EI |
  | `FacturaProveedorRegistrada` | Accounting · Business | `FacturaProveedor` | CP |
  | `AjusteInventarioAprobado` | Accounting · Business | `AjustePositivo`, `AjusteNegativo`, `ConsumoInterno`, `RetiroGravado`, `Baja`, `Ensamble` | EI / SI |
  | `TrasladoDespachado` · `TrasladoRecibido` | Accounting · Business | `DespachoTraslado` · `RecepcionTraslado` | TR |
  | `DevolucionRegistrada` | Accounting · Business | `DevolucionAProveedor` / `DevolucionDeCliente` | SI / NV |
  | `DocumentoAnulado` | Accounting · el `Kind` del original | las del original, con reglas a su fecha | el del original |
  | `AjusteDeCostoReconocido` | Accounting · Business (uno por documento afectado) | `AjusteDeCosto` | AC |
  | `NotaCreditoEmitida` · `NotaDebitoEmitida` | Accounting · Business | `NotaCredito` · `NotaDebito` | NV |
  | `GrupoContableReclasificado` | Accounting · Business (modo general) | `Reclasificacion` | AC |
  | `MovimientoDeCajaRegistrado` | Accounting · Business | `MovimientoDeCaja` | CJ |
  | `DiferenciaDeArqueoAprobada` | Accounting · Business | `DiferenciaDeArqueo` | CJ |
  | `SaldoInicialCargado` | Accounting · Informational | — | — |
  | `PeriodoInventarioCerrado` · `PeriodoInventarioReabierto` | Accounting · Informational | — | — |
  | `VentaACreditoRegistrada` · `AjusteDeVentaACredito` | Lending · Business (`Always`) | — | — |

  Un documento fiscal validado nunca emite `DocumentoAnulado` (FR-066). `CostoDeVentaReconocido` y
  `DevolucionRegistrada` no llevan base ni impuestos, y los de venta no llevan costo (FR-036).
- **Orden y dependencias** (T9).
  - Al emitir, se registran aristas hacia el último mensaje de cada cadena de la que el nuevo depende:
    su propio documento, el original que anula o corrige, los orígenes de un derivado y cada documento
    afectado por un ajuste de costo.
  - Un mensaje es elegible si su entrega está `Pending`, su `NextAttemptAt` ya venció y ninguna
    dependencia tiene entrega al mismo destino en {`Pending`, `InBatch`, `Rejected`}. Los estados
    {`Processed`, `NotApplicable`, `ValidationFailed`} no bloquean.
  - Se procesa por Id ascendente; nunca se ordena por Guid. La bandeja calcula «espera al mensaje X».
- **Despachador** (T10). `DespachadorDeMensajes` (API, `BackgroundService`, registrado sólo en
  `API/Program.cs`):
  - recorre `ITenantDirectory.ListActiveAsync` y toma el arrendamiento `integration.dispatch` en
    `COR_BackgroundLeases`, en la base de cada cooperativa (`UPDATE … WHERE LeaseUntil < ahora OR Owner
    = yo`, TTL de 120 s renovado entre tandas);
  - sondea cada 5 s y, además, lo despierta la señal en proceso `ISenalDeMensajes` (`Channel<Guid>`);
  - trabaja en tandas de 100 por Id, con un presupuesto de 60 s por cooperativa;
  - por cada mensaje, abre un ámbito DI nuevo con `IEjecutorEnCooperativa` (R5), envía el comando del
    destino y recibe `ResultadoDeConsumo` (Processed | AlreadyProcessed | Rejected | Retry); en otro
    ámbito envía `RegisterDeliveryResultCommand`, que actualiza el estado y agrega el intento.

  La exactitud no depende del arrendamiento: la garantizan el `RowVersion` de la entrega y el recibo
  único del destino.
- **Reintentos.**
  - Los transitorios reintentan sin límite, con espera `min(15 s·2^(n−1), 15 min)` + jitter. A los 3
    intentos o a los 15 min se levanta `Integracion.MensajeSinEntregar`.
  - Los de negocio quedan `Rejected`, sin reintento, con la alerta `Integracion.MensajeRechazado` para
    Inventario y para Contabilidad.
  - Se reprocesan con `ReprocessMessagesCommand` (`Inventory.Messages.Reprocess`, con motivo y la
    persona como actor), que crea un lote con `Trigger = Reprocess`, pasa las entregas de `Rejected` a
    `InBatch` con ese lote y arrastra a los dependientes. Lo mismo hace
    `SendNotApplicableMessagesCommand` con un lote `SendNotApplicable` (`NotApplicable` → `InBatch`).
  - No hay estado «en proceso» persistido.
  - Los valores técnicos van en `appsettings` (`Integration:Dispatcher`, `Integration:Retries`). La
    fixture apaga el despachador y conduce el ciclo a mano.
- **Lotes** (T12). El lote es de la plataforma (`COR_IntegrationBatches`, con número tomado de
  `COR_IntegrationBatchCounters`), así Inventario lo muestra sin leer `ACC_`.
  - Contabilidad **planea** los grupos (`IDestinoDeMensajes.PlanearLote` → `AgrupadorDeResumidos`) y
    valida documento a documento con `ValidarVariosAsync`: excluye los que fallan, con sus relacionados.
  - En resumido (`PostInventorySummaryGroupCommand`), suma débitos y créditos por clave sin netear y
    conserva el detalle de las líneas cuya cuenta exige tercero, documento cruce o base.
  - Disparadores (`BatchTrigger`):
    - `Scheduled`: a la hora local. Un UK filtrado `(ScheduleKey, ScheduledFor)` impide duplicar entre
      réplicas.
    - `CashSessionClose`: lo crea `CloseCashSessionCommand` en su propio `SaveChanges`.
    - `PeriodClose`: lo crea `CloseInventoryPeriodCommand`.
    - `Manual`: `OrderIntegrationBatchCommand`, con el `cutoffMessagePublicId` (el `PublicId` del
      último mensaje listado) de `PreviewIntegrationBatchQuery`. El handler lo traduce al Id interno y
      `COR_IntegrationBatches.CutoffMessageId` (`bigint`) no sale de la base. Responde 202 y exige
      `Accounting.InventoryBatches.Run`.
    - `Reprocess` y `SendNotApplicable`.
  - Un ámbito DI por unidad o grupo. Un lote vacío queda `Empty`, que es la prueba de que corrió. Un
    lote programado vencido más la tolerancia técnica alerta `Integracion.LoteNoCorrio`.
- **Bandeja.** `ListIntegrationMessagesQuery` en `/api/inventory/messages` (`Inventory.Messages.View`),
  con la pantalla `/inventario/bandeja-de-mensajes` y la vista `messages`. Lista en orden de emisión
  (con `emittedAt`), sin el Id interno, y el resultado (comprobante y número) se lee sólo de
  `COR_IntegrationMessageDeliveries`: Inventario no consulta tablas `ACC_`. Los lotes se ven en
  `/contabilidad/inventario/lotes` y en la vista `accounting-batches`.

**Por qué**:
- FR-071 exige que no haya documento sin mensaje ni mensaje sin documento. Sin transacción
  distribuida, la única forma es escribir el mensaje en la misma unidad de trabajo, y el código ya
  resuelve así la atomicidad.
- Separar el mensaje inmutable de la entrega mutable cumple a la vez «un mensaje emitido no se modifica
  nunca» y «estado por destino».
- El Id identity es un orden topológico válido porque un relacionado sólo se confirma después de su
  original. Una sola clave de orden no bastaría: un derivado reúne varios orígenes.
- Emitir desde I1 evita documentos de ensayo sin mensajes.
- El arrendamiento en SQL, en la base de la cooperativa, cumple el Principio IV (la base 0 de Redis es
  para lo que no pertenece a ninguna cooperativa), no depende de Redis y queda como evidencia.
- El lote en la plataforma deja que Inventario lo muestre sin leer tablas contables. Un ámbito por
  unidad evita que el `DescartarCambios` de un reintento borre el estado del lote.
- Un sondeo de 5 s más la señal cumple SC-011 sin broker.

**Alternativas descartadas**:
- (a) Prefijo `INT_` (mensajería): la constitución no lo lista; va `COR_` (T2).
- (b) Eventos de dominio convertidos en filas por un interceptor: magia implícita, y consultas dentro
  del interceptor.
- (c) Publicar un `INotification` después del commit, como `HabeasDataRevokedEvent`: se pierde si el
  proceso cae.
- (d) Guardar el estado de entrega dentro del mensaje: rompe la inmutabilidad.
- (e) `jsonb`: diverge de SQL Server.
- (f) Candado Redis en la base 0 con `ExtendAsync` (mensajería), o `IDistributedLock` para la cadena de
  auditoría (seguridad): se reemplazan por `COR_BackgroundLeases` (T10).
- (g) Hangfire, Quartz, MassTransit o RabbitMQ: o guardan en un almacén compartido, contra el Principio
  IV, o agregan un broker en un clúster de un nodo sin ganancia. Se pueden adoptar después sobre las
  mismas tablas. `LISTEN/NOTIFY` sólo existe en PostgreSQL.
- (h) Contabilizar en la misma petición, después del commit: sube la latencia del POS, y el actor
  sería el cajero.
- (i) Un máximo de intentos que marque «fallido» como estado final: contradice la entrega garantizada.
  Tampoco se reintentan solos los rechazos de negocio: repetiría errores deterministas.
- (j) `ACC_InventoryPostingBatches` y `ACC_InventoryIntegrationSettings.NextBatchNumber` (informe de
  contabilidad): el lote pasa a la plataforma (T12).
- (k) Orden global estricto por cooperativa: un rechazo detendría todo.

**Riesgos**:
- El Id identity se asigna al insertar, no al confirmar. El orden sólo vale dentro de cadenas causales,
  y una regla futura que emita un relacionado en la misma transacción que su original lo rompe. Se fija
  con una prueba.
- Al vencer el arrendamiento, dos réplicas podrían desordenar una cadena. Se mitiga releyendo las
  dependencias antes de cada entrega.
- Volumen de auditoría: unos 15.000 mensajes al día por cooperativa, conservados 10 años. Las entregas
  y los intentos se excluyen del diff (R15, pregunta B7).
- Los mensajes a Cartera pueden esperar meses con v1. Si D-02 pide datos que v1 no trae, habrá que
  convertir en el consumidor.
- Un despliegue reinicia pods con entregas en vuelo; el recibo único lo absorbe.

**Spec**: FR-014, FR-015, FR-016, FR-018, FR-069 a FR-072, FR-076, FR-077, FR-080, FR-083, US7, SC-002,
SC-010, SC-011, SC-013, SC-020, SC-023; D-03; Supuesto 4.

## R4. Idempotencia de las operaciones

**Decisión** (T13, T14):
- **Una sola mecánica** para toda operación de pantalla: `COR_OperationKeys` + `IdempotencyBehavior`.
  - `OperationKey` lleva `Key` (único), `Operation`, `RequestSha256`, `CentralUserId`, `ActorName` y
    `ResultJson`. Nunca se borra.
- **La clave la genera el cliente**: un UUID al iniciar la operación (abrir el diálogo, cargar el
  borrador), que manda en la cabecera `Idempotency-Key`.
  - Lo conserva en los reintentos: `RenovacionDeSesionHandler` ya clona las cabeceras.
  - Lo renueva sólo tras un éxito o si cambia el contenido.
  - La ruta lo mapea a `OperationKey` del comando (`IOperacionIdempotente`).
- **El behavior** abre `TransaccionExplicita`, inserta la fila y ejecuta el handler.
  - Con éxito, guarda el `Result` serializado en la misma transacción.
  - Con fallo, revierte, y la clave no queda.
- **Respuestas**:
  - repetición: el mismo resultado, con la cabecera `Idempotent-Replayed: true` y el evento de auditoría
    `Operation.Replayed`;
  - la misma clave con otro contenido: 422 `Operation.KeyReused`;
  - clave ausente: 400 `Operation.KeyRequired`.

  Un duplicado concurrente espera en el índice y choca; la colisión se detecta por el nombre del índice
  (molde `PersonFactory`).
- **Alcance**: todo comando con ruta de `Application/Inventory`, `Application/ElectronicInvoicing` y
  `Application/Core/{Taxes,PaymentMeans}`, y los de la plataforma que se invocan desde una pantalla
  (parámetros, aprobaciones, alertas, alcances, montos, reproceso, lotes, envío posterior). Lo vigila
  `LosComandosDeInventarioLlevanClave`.
- **Otras claves**: los mensajes usan su `MessageId` (recibo único, R1), y la emisión DIAN, la clave
  `{tenantPublicId}:{ambiente}:{prefijo}{consecutivo}:v{versión}` (R28).
- **`TransaccionExplicita.EjecutarAsync`** generaliza `TransaccionDeLiquidacion`: estrategia de ejecución
  y transacción; en cada intento, `DescartarCambios()` y relectura de todo; una llamada anidada se une a
  la transacción en curso.
- **Pipeline**: Validation → Logging → **Idempotency** → Audit → ReintentoPorConcurrencia → Performance.
  Ningún handler envía por `ISender` un comando reintentable dentro de su transacción.
- **Retención**: indefinida (pregunta B3).

**Por qué**:
- FR-016 y SC-002 (tres repeticiones sin duplicar) cubren el doble clic y el reintento tras perder la
  conexión justo al confirmar: es el caso que más duplica ventas en un POS.
- Una tabla de claves cubre también transiciones (aprobar, anular, cerrar) que una columna por tabla no
  cubre.
- La clave es atómica con el efecto, aunque `ReintentoPorConcurrencia` descarte el tracker.
- Guardar sólo los éxitos evita fijar como respuesta un error transitorio.

**Alternativas descartadas**:
- (a) Una columna `ClientOperationId` con índice único por tabla (ventas), o `INV_Documents.IdempotencyKey`
  (núcleo): no cubren las transiciones. `INV_Documents` no lleva clave (T13).
- (b) Agregar la fila al tracker sin transacción: `DescartarCambios` la pierde.
- (c) La clave en Redis: queda fuera de la transacción y fuera de la base de la cooperativa.
- (d) Una clave derivada del contenido: dos ventas idénticas legítimas colisionarían.
- (e) Sólo deshabilitar el botón: no cubre los reintentos de la red.

**Riesgos**:
- La transacción del behavior se anida mal con un handler que abra la suya. Por eso todo pasa a
  `TransaccionExplicita`.
- Los puntos de guardado de EF con reintento, y los interbloqueos, sólo se prueban con Testcontainers en
  los dos motores: InMemory ignora las transacciones.
- Si se conservan indefinidamente, `COR_OperationKeys` crece sin límite: una fila por operación de
  pantalla, del orden de decenas de miles al día por cooperativa.

**Spec**: FR-016, SC-002, SC-001 (indirecto), Edge Cases «Concurrencia»; D-03.

## R5. Procesamiento por cooperativa, actor y trabajos de fondo

**Decisión**:
- **Contexto de ejecución** (T5): `ContextoAmbiental` (AsyncLocal, en `Application/Common/Execution`)
  más `IEjecutorEnCooperativa.EjecutarAsync(TenantDirectoryEntry, Actor, origen, trabajo)`, implementado
  en `API/Integration/EjecutorEnCooperativa`.
  - Abre un `AsyncServiceScope` nuevo por mensaje o por comando, y restaura el contexto anterior al
    terminar.
  - Lanza si hay `HttpContext` (las órdenes manuales sólo encolan) o si la entrada no trae base.
  - Salta la cooperativa con base desactualizada sin detener a las demás.
  - Cuando no hay `HttpContext`, leen el ambiental: `TenantContextAccessor`, `CurrentUserService` (sólo
    `UserName` y `TenantId`), `CurrentCentralUserContextAccessor`, `IpAddressAccessor`,
    `CentralIdentityLogEnricher` y la fábrica de `ErpTenantInfo`.
  - `MongoAuditService` lanza, y registra Critical, si hay contexto ambiental sin cooperativa. Nunca
    escribe en `_Global`.
- **Actor** (T6): una sola interfaz nueva de lectura, `IActorActual`, que devuelve el record `Actor`.
  - `Actor` lleva `Kind` (Person | Process), `UserId?` (`SEC_Users.Id`), `UserPublicId?`,
    `CentralUserId?`, `Name`, `Email?`, `Channel`, `Origin`, `Ip?` y `Reason?`.
  - La implementan `ActorDeLaPeticion` (reusa `PermisosDeLaPeticion.ResolverUsuarioYCooperativaAsync` y
    se memoriza por petición) y el ambiental en segundo plano.
  - El proceso automático es `Kind = Process`, `Name = "Proceso de integración"`, sin IP, con canal
    `Process` y origen `Mensaje:{id}`, `Lote:{número}` o `Tarea:{nombre}`.
  - Los lotes manuales, los reprocesos y los envíos posteriores corren con la persona que los ordenó,
    capturada en la orden.
  - El usuario de origen viaja en el mensaje y en `PostingRequest.RegistradoPor`. Nunca es actor ni
    presta sus permisos.
  - No se crea un usuario técnico en `SEC_Users`.
  - La segregación y las aprobaciones comparan el `SEC_Users.Id` de `IActorActual`, y los documentos
    guardan `CreatedByUserId` y `ConfirmedByUserId`.
  - `ICurrentUserService.UserId` **no se cambia** en esta feature (pregunta B1).
- **Trabajos de fondo** (T47). Todo `BackgroundService`:
  - se registra sólo en `API/Program.cs` (el DbMigrator nunca los arranca) y espera
    `DatabaseReadiness.IsReady`;
  - recorre `ITenantDirectory.ListActiveAsync` y toma su arrendamiento en `COR_BackgroundLeases`
    (`integration.dispatch`, `audit.forward`, `einvoicing.process`, `scheduled.tasks`, `email.dispatch`);
  - ejecuta por `IEjecutorEnCooperativa`;
  - se apaga por configuración en la fixture.

  Los trabajos son:
  - `AuditOutboxForwarder` (I1);
  - `ProgramadorDeTareas`: en I1, reorden, quiebre, eventos RADIAN faltantes y la verificación de
    integridad nocturna; en I4, las alertas DIAN; en I6, liberar reservas vencidas;
  - `NotificationEmailDispatcher`, con arrendamiento (I1);
  - `DespachadorDeMensajes` (I2);
  - `ProcesadorDeDocumentosElectronicos` (I4).
- **Dos arreglos mínimos de plataforma entran en I1**, porque sin ellos el módulo no cumple:
  `ListMyNotificationsQuery` y sus hermanas resuelven al usuario por `IActorActual`, y
  `NotificationEmailDispatcher` toma el arrendamiento por cooperativa.

**Por qué**:
- FR-083 y D-03 exigen el mismo contexto que una petición y el camino común (Principio III): todo por
  `ISender`, con el pipeline completo.
- `MongoAuditService`, `TenantContextAccessor` y `CurrentUserService` son singletons, y la cola de
  auditoría tiene que serlo. Un holder de ámbito no les llega; `AsyncLocal` es el mismo mecanismo de
  `IHttpContextAccessor`.
- Un ámbito por mensaje aísla el tracker (lección de `TransaccionDeLiquidacion`).
- `IActorActual` arregla lo que esta feature necesita sin cambiar el cuatro ojos ni «mis borradores» del
  resto de la plataforma.

**Alternativas descartadas**:
- (a) Fabricar un `DefaultHttpContext` falso: engaña a los filtros de permiso, al alcance y a la IP.
- (b) Un holder de ámbito: no llega a los singletons.
- (c) Seguir con `ITenantDbContextFactory`, como el despachador de correo: los handlers no ven ese
  contexto.
- (d) Corregir `ICurrentUserService.UserId` en toda la plataforma (informe de seguridad, opción b):
  cambia el comportamiento de módulos en producción, con documentos viejos que tienen
  `RegisteredByUserId = 0`. Queda como tarea aparte del dueño.
- (e) `IContextoDeEjecucion` (mensajería) y «fijar `ICurrentUserService` a la identidad del proceso»
  (contabilidad): se funden en `IActorActual` (T6).
- (f) Un usuario técnico en `SEC_Users`: sería una cuenta sin persona a la que alguien podría darle
  permisos.
- (g) Un proyecto Worker aparte: otra imagen para un clúster de un nodo. Se puede extraer después sin
  cambiar el diseño.
- (h) Un CronJob de Kubernetes: corre fuera del proceso y sin el contexto por cooperativa.

**Riesgos**:
- Filtración del `AsyncLocal`: una operación de fondo escribiendo en otra cooperativa. Se mitiga con un
  `using` que restaura, la negativa ante `HttpContext`, un ámbito por mensaje y la e2e de dos
  cooperativas.
- Sin `HttpContext` ni ambiental (arranque, sembrado, CLI), la fábrica de `ErpTenantInfo` sigue cayendo
  a la plantilla. Un trabajo futuro que olvide el ejecutor escribiría en la plantilla sin error.
  Endurecer la fábrica puede romper el inicializador, el sembrado y el DbMigrator, así que se decide
  aparte; mientras tanto lo acota `NingunTrabajoDeFondoOperaSinCooperativa`.
- `UploadAttachmentCommand` hace `int.Parse` de `TenantId`, así que el procesador DIAN depende de que
  el ambiental lo llene.
- El defecto de `UserId` sigue vivo fuera del módulo.

**Spec**: FR-007, FR-010, FR-083, US7 esc. 3, SC-012; D-03; Principios III, IV y X; Supuesto 7.

## R6. Modelo de documentos: clases fijas y tipos parametrizables

**Decisión** (T17):
- **Una cabecera y una línea para las 34 clases** de `DocumentClass`: `INV_Documents`
  (`InventoryDocument`) e `INV_DocumentLines`. Satélites:
  - vínculos: `INV_DocumentLinks` e `INV_DocumentLineLinks` (este último con `QuantityBase`). Los
    pendientes se calculan, no se guardan;
  - de toda venta y compra: `INV_DocumentPartySnapshots` e `INV_DocumentTaxLines`;
  - de compras: `INV_SupplierInvoiceDetails`;
  - de traslados y conteos: `INV_TransferDiscrepancies`, `INV_CountSnapshotLines` e `INV_CountCaptures`;
  - de ventas y caja: `INV_DocumentLineDiscounts`, `INV_DocumentPayments`, `INV_CashMovementDetails` e
    `INV_CashDocumentLines`.
- **Qué es una clase**:
  - El movimiento de caja y el arqueo con diferencia **son clases**.
  - La anulación es la clase `Voiding` (`VoidsDocumentId`/`VoidedByDocumentId`).
  - El documento electrónico queda aparte, en `COR_` (R28).
- **El comportamiento de cada clase vive en código**: efecto, si es fiscal, mensajes, cadena, grupo y
  bodegas admitidas, en `Domain/Inventory/Documents/ClasesDeDocumento`, más una estrategia por clase en
  `Application/Inventory/Documents/Efectos`.
- **Los tipos sólo eligen su clase y parametrizan** (`INV_DocumentTypes`, `INV_DocumentTypeWarehouses`):
  consecutivo, aprobación, modo de paso, campos obligatorios, bodegas y canal (FR-037).
- **`DocumentClassGroup` decide la ruta y la familia de permisos**: Purchases, Adjustments, Transfers,
  Counts, Sales, Cash, OpeningBalance y Costing. `Voiding` toma el grupo del original.
- **Estados** (`DocumentStatus`): `Draft`, `PendingApproval`, `Confirmed`, `Voided` y `Discarded` (el
  descarte queda registrado).
- **Un confirmado implementa `IInmutableTrasConfirmar`**: sólo cambian `Status` (→ `Voided`),
  `VoidedByDocumentId`, `FiscalNumberReleased` y la auditoría.
- **Flujo canónico de confirmación**: `ConfirmInventoryDocumentCommand(DocumentPublicId,
  ExpectedGroup)` en doce pasos, que `data-model.md` y `plan.md` reproducen:
  1. idempotencia;
  2. relectura del borrador y reglas de la clase;
  3. aprobaciones;
  4. guardia fiscal;
  5. validación previa, fuera del cerrojo;
  6. cerrojo;
  7. costeo y kardex;
  8. numeración;
  9. mensajes;
  10. documento electrónico pendiente;
  11. un solo `SaveChanges`;
  12. señal al despachador y emisión en línea.
- **Anular** (FR-006) es `VoidInventoryDocumentCommand`:
  - fecha propia, en un período abierto;
  - efecto al costo del original;
  - una sola vez;
  - con dependientes vigentes responde `Inventory.Document.HasDependents` (`data.dependents`);
  - sobre un documento fiscal validado responde `Inventory.Document.FiscalUseCorrection` (R29).

**Por qué**:
- Numeración, estados, aprobación, anulación, período, alcance, idempotencia, bandeja y conciliación son
  iguales para todas las clases. Una tabla permite una sola implementación y una sola consulta para el
  cierre (borradores, tránsitos y remisiones del período).
- Hay precedentes: `ACC_Documents.Kind` (009) y `PAY_PayrollRuns.Kind` (010, un ciclo común para cinco
  tipos).
- Tener las clases en código impide que un tipo invente un efecto que rompa los invariantes (FR-036).

**Alternativas descartadas**:
- (a) Una tabla por clase, unas 30: la lógica transversal duplicada y un cierre que une todas.
- (b) Una tabla TPH con todas las columnas: cientos de columnas nulas.
- (c) Cabeceras propias para compras (`INV_SupplierInvoices`), movimientos de caja (`SLS_CashMovements`)
  y documento electrónico (`FEL_Documents`): duplican numeración, estados y auditoría. La factura del
  proveedor queda como 1:1 (`INV_SupplierInvoiceDetails`), y el electrónico en `COR_` por su ciclo
  fiscal propio.
- (d) `SourceLineId` en la línea (núcleo): no admite muchos a muchos, como una factura contra varias
  recepciones. Los vínculos van en `INV_DocumentLineLinks`.
- (e) Contadores de pendiente guardados en la línea de la orden: se desvían con anulaciones
  concurrentes.

**Riesgos**:
- Las tablas de documentos y de líneas concentran todo el volumen del módulo: millones de líneas al año
  a la escala de referencia. Llevan índices por clase, fecha, bodega y contraparte.
- Una estrategia mal registrada deja una clase sin efecto. Una prueba recorre `DocumentClass` y exige
  la estrategia de cada una.
- La ruta por grupo exige que el handler verifique clase ↔ ruta (`ExpectedGroup`); si no, una clase de
  ventas podría confirmarse por la ruta de ajustes, con los permisos equivocados.

**Spec**: FR-001, FR-005, FR-006, FR-011, FR-018, FR-036, FR-037, FR-038, US2, SC-013.

## R7. Kardex, proyecciones y hechos inmutables

**Decisión** (T18):
- **El kardex es un hecho**: `INV_KardexEntries` (`KardexEntry`, `AuditableEntityLong`, sólo
  inserción). Cada línea lleva:
  - documento y línea, producto, bodega, ubicación y lote o serie. La ubicación está siempre, porque
    cada bodega tiene una por defecto;
  - `OperationDate` y `RegisteredAt`;
  - `Kind` (`Entry`, `Exit`, `CostAdjustment`) y `Reason` (`Normal`, `Retroactive`, `PriceDifference`,
    `LandedCost`, `NegativeRegularization`, `VoidDifference`, `RoundingResidue`, `MethodChange`);
  - `QuantityBase` con signo (0 en los ajustes de costo), `UnitCost` y `TotalCost`;
  - `CostScopeWarehouseId` (0 = cooperativa) y el `CostMethod` sellado;
  - `ReversesEntryId` y `AffectsEntryId`.

  El orden del kardex es `(OperationDate, Id)`.
- **Proyecciones reconstruibles**, fuera del Principio XI, con índices únicos **sin filtro** porque
  nunca se borran:
  - `INV_StockBalances`: físico y reservado por producto y bodega;
  - `INV_StockDetails`: por ubicación y lote;
  - `INV_CostStates`: costo por producto y ámbito (cantidad, valor, promedio, último costo);
  - `INV_CostLayers`: capas PEPS (I5).

  `INV_LayerConsumptions` sí es un hecho.
- **Valor por bodega** = cantidad × promedio del ámbito, nunca la suma de `TotalCost` por bodega.
- **Un solo escritor**: `RegistroDeKardex` agrega kardex, proyecciones y consumos sin guardar. Lo
  vigila `NadieEscribeElKardexFueraDelRegistro`.
- **Inmutabilidad en el contexto**: `ApplicationDbContext.SaveChangesAsync` rechaza los `Modified` y
  `Deleted` no autorizados de:
  - todo `IHechoInmutable`: kardex, consumos de capa, mensajes, dependencias, intentos, decisiones de
    aprobación, `InventoryPosting`, `DocumentPartySnapshot`, `DocumentTaxLine`, versiones y transmisiones
    electrónicas, y anclas;
  - todo `IInmutableTrasConfirmar`.
- **Verificación y reconstrucción**:
  - `VerificacionDeIntegridad` compara las sumas del kardex con cada proyección y levanta
    `Inventario.IncidenteDeIntegridad`.
  - Corre de noche en `ProgramadorDeTareas` y a pedido (`VerifyInventoryIntegrityQuery`,
    `/api/inventory/integrity/verify`, `Inventory.Integrity.Verify`).
  - `RebuildInventoryProjectionsCommand` exige `Inventory.Integrity.Rebuild`.

**Por qué**:
- Es la idea de la 009 R4 (no hay saldos guardados; todo es una suma sobre el libro), con proyecciones
  materializadas, porque:
  - la regla del stock negativo necesita una fila que proteger en cada escritura;
  - SC-009, SC-017 y SC-019 piden consultas rápidas sobre 50.000 productos.
- Como cantidad y valor se obtienen sumando, la proyección no depende del orden de reconstrucción, y
  FR-003 y SC-006 se verifican con un `GROUP BY`.
- La prueba actual del Principio XI sólo detecta `Remove()`: cambiar una propiedad de una línea no la
  dispara. Por eso la guardia vive en el contexto.

**Alternativas descartadas**:
- (a) Sin proyecciones, sumando el kardex en cada venta: no hay fila que bloquear, y el POS sumaría
  millones de filas.
- (b) El saldo en el producto, como hoy (`CurrentStock`): es el defecto que la spec describe.
- (c) Vistas materializadas: el refresco es distinto en cada motor y no es transaccional.
- (d) Sólo pruebas por regex: no ven la mutación de una propiedad. Triggers: dos implementaciones, y
  fuera de la arquitectura limpia.

**Riesgos**:
- `INV_KardexEntries` crece unos 9 millones de filas al año a la escala de referencia. SC-017
  (valorizado a una fecha pasada en menos de 30 s) depende de `INV_PeriodClosingBalances` y de los
  índices `(ProductId, WarehouseId, OperationDate)` y `(OperationDate)`.
- Reconstruir las proyecciones con operación en curso necesita el cerrojo completo de la cooperativa o
  una ventana sin movimientos. Se documenta en el runbook.
- `IInmutableTrasConfirmar` debe admitir exactamente las columnas autorizadas, o bloquea la anulación y
  el caso b de FR-066.

**Spec**: FR-001, FR-002, FR-003, FR-033, FR-086 (kardex), US2, SC-006, SC-013, SC-017; Principios VII y
XI.

## R8. Concurrencia de existencias

**Decisión** (T15): bloqueo pesimista en orden canónico, dentro de `TransaccionExplicita`.
- **Dónde**: sólo en `Persistence/Inventory/CerrojoDeInventario` (`ICerrojoDeInventario` en
  `Application/Inventory/Common`), con el SQL de cada motor ejecutado por el propio
  `ApplicationDbContext`.
- **Dos pasos**:
  1. Asegura que existan las filas de proyección: en PostgreSQL, `INSERT … ON CONFLICT DO NOTHING`; en
     SQL Server, `INSERT … WHERE NOT EXISTS … WITH (UPDLOCK, HOLDLOCK)`. Ese SQL no pasa por EF ni por
     `AuditableEntityInterceptor`, así que el INSERT del cerrojo escribe él mismo `PublicId`
     (`gen_random_uuid()` / `NEWID()`), `CreatedAt` = UTC, `CreatedBy` = nombre del actor
     (`IActorActual`), `IsDeleted` = 0 y ceros en las cantidades (Principios VI y VII); lo prueba
     `ConcurrenciaDeExistenciasTests` en los dos motores.
  2. Las bloquea: en PostgreSQL, `SELECT … WHERE "Id" = ANY(@ids) ORDER BY "Id" FOR UPDATE`; en SQL
     Server, `WITH (UPDLOCK, ROWLOCK, HOLDLOCK) … ORDER BY Id`.
- **Orden fijo de adquisición**:
  1. `INV_Setup` (compartido);
  2. `INV_Warehouses` (compartido, por Id);
  3. `INV_CostStates`, `INV_StockBalances` e `INV_StockDetails` (exclusivos, por Id);
  4. al final, la fila de numeración (`INV_DocumentSequences` o `COR_DianNumberingResolutions`).
- **Con las filas bloqueadas**, el handler:
  - las lee por EF;
  - valida que disponible = físico − reservado alcance la cantidad; si no alcanza, responde
    `Inventory.Stock.Insufficient` con `data.available`, salvo que `Existencias.StockNegativoPermitido`
    lo admita;
  - pide los costos a `MotorDeCosteo`;
  - agrega kardex y proyecciones;
  - hace un solo `SaveChanges`.

  `RowVersion` queda como segunda defensa.
- **Reintentos**: `ConfirmInventoryDocumentCommand` **no** es `IReintentableAnteConcurrencia`. A una
  víctima de interbloqueo (PostgreSQL 40P01 o 40001, SQL Server 1205) la repite entera la estrategia de
  ejecución.
- **Fuera del cerrojo**: la validación previa contable y la llamada al canal DIAN.
- **Saldos iniciales grandes**: se parten en documentos de hasta 4.000 líneas por bodega.
- **Bonos**: el de número único no se bloquea; lo protege el UK filtrado de `INV_VoucherRedemptions`
  (R25).

**Por qué**:
- SC-001 exige exactamente 10 confirmadas y 40 rechazadas con 50 ventas simultáneas.
- Con la concurrencia optimista de la 009 R3, las 50 releen y gana una por ronda: con 5 reintentos
  ganan unas 5, y el resto sale con 409 aunque quede existencia. La correctitud se cumple; la vivacidad,
  no.
- Con el bloqueo, las que pierden esperan milisegundos y leen el valor ya confirmado. Lo mismo sirve a
  las 30 cajas que comparten un consecutivo (SC-019).
- El orden canónico evita interbloqueos, y los que ocurran los absorbe `EnableRetryOnFailure`.

**Alternativas descartadas**:
- (a) Sólo `RowVersion` + `ReintentoPorConcurrenciaBehavior`: vivacidad probabilística, que falla
  SC-001.
- (b) `SERIALIZABLE`: semántica distinta por motor y más abortos.
- (c) Un `UPDATE` condicional atómico: sirve para la cantidad, pero el promedio exige leer, modificar y
  escribir el estado de costo, que igual hay que bloquear.
- (d) Candados de aplicación por producto (`pg_advisory_xact_lock`/`sp_getapplock`): son invisibles para
  el resto del código, y sus espacios de clave chocan entre módulos. Quedan como plan B para la carrera
  en el alta de filas.

**Riesgos**:
- SQL Server escala el bloqueo de fila a tabla por encima de unas 5.000 filas bloqueadas. De ahí el
  corte de 4.000 líneas; un conteo total grande tiene el mismo riesgo.
- Cada intento de la estrategia debe limpiar el tracker y releer todo.
- InMemory no ejercita los bloqueos: la concurrencia sólo se verifica con Testcontainers, en PostgreSQL
  y en SQL Server.
- SC-001 al pie de la letra (1.000 × 50) es pesado para CI: va detrás de `RUN_PERF_TESTS`, con Skip
  explícito, nunca con un `return` que cuente como aprobada.

**Spec**: FR-004, FR-034, FR-038, US2 esc. 2, SC-001, SC-019; Edge Cases «Concurrencia».

## R9. Numeración

**Decisión** (T16): un solo mecanismo, el cerrojo. La fila de numeración es la última del orden
canónico y se incrementa por EF dentro de la transacción de confirmación. Sólo `Numerador` y
`NumeradorFiscal` escriben esas columnas (`SoloElNumeradorNumera`).
- **No fiscales y notas** (`CreditNote`, `DebitNote`, `PosAdjustmentNote`,
  `SupportDocumentAdjustmentNote`, `NonElectronic*`): `INV_DocumentSequences` (`DocumentTypeId`,
  `Prefix`, `NextValue`, vigencia). Un cambio de prefijo es una fila nueva.
- **Fiscales con resolución** (factura, documento equivalente POS, documento soporte, contingencia 03):
  `COR_DianNumberingResolutions.LastIssuedNumber` de la resolución vigente, buscada por (`Kind`,
  `Prefix`, `Environment`) y asociada al canal sellado. El tipo declara su prefijo, sin FK a la
  resolución.
- **Cuándo**: al confirmar. El borrador no consume número.
- **Unicidad**:
  - `INV_Documents` lleva desde I1 el UK `(DocumentTypeId, Prefix, Number)` filtrado
    `[Number] IS NOT NULL AND [FiscalNumberReleased] = 0`, porque el caso b de FR-066 reusa el número;
  - la unicidad fiscal vive en `COR_ElectronicDocuments (Environment, Prefix, Consecutive)`, **sin
    filtro**.
- **Nombres**: las propiedades nunca se llaman `NextNumber`.
- **Errores**: una resolución vencida o agotada responde `Inventory.Numbering.ResolutionUnavailable`
  (con `ElectronicInvoicing.Resolution.Expired` o `.Exhausted`).

**Por qué**:
- FR-038 pide consecutivos sin huecos ni repetidos, seguros ante concurrencia y compartidos por todas
  las cajas. Con la fila al final del cerrojo, 30 cajas esperan milisegundos en vez de reintentar.
- Asignar al confirmar, y no al crear el borrador, evita huecos por descartes.
- Los nombres `NextValue` y `LastIssuedNumber` esquivan la regex de
  `NingunModuloEscribeMovimientosFueraDelContrato`, como hizo la 010.

**Alternativas descartadas**:
- (a) `RowVersion` + reintento, como la 009: muchos reintentos con 30 cajas sobre la misma fila.
- (b) El incremento atómico `UPDATE … RETURNING/OUTPUT` de la resolución (informe DIAN): es correcto,
  pero sería un segundo mecanismo, y el cerrojo ya tiene la fila tomada.
- (c) Una `SEQUENCE` nativa: deja huecos y exige DDL por proveedor (009 R3).
- (d) Que el proveedor asigne el número, como Factus: rompe FR-038, FR-064 y el reintento sin duplicar
  (R27).

**Riesgos**:
- La fila de la resolución es la más caliente del POS: cualquier trabajo lento después de tomarla
  (validación contable, llamada DIAN) serializa las cajas. Por eso los dos quedan fuera.
- Si el prefijo del tipo no coincide con ninguna resolución vigente asociada al canal, la confirmación
  fiscal se bloquea. Lo anticipa `GetDianReadinessQuery`.
- El reuso del número rechazado es la regla inversa de la nómina electrónica de la 010, donde un rechazo
  genera un documento nuevo. Hay que dejarlo escrito para que nadie copie la 010.

**Spec**: FR-038, FR-065, FR-066, US5, US8 esc. 3 y 6, SC-001, SC-019.

## R10. Costeo

**Decisión**:
- **Motor**: `Domain/Inventory/Costing/MotorDeCosteo`, puro, determinista y sin IO (molde
  `PayrollCalculationEngine`), con `PromedioPonderado`, `Peps` (I5), `Retroactivo`, `Prorrateo`,
  `CostoDeEntrada`, `Redondeo` y `ExplicacionDeCosto`.
  - Entradas: el estado de costo del ámbito (y las capas, si es PEPS); el movimiento (clase, cantidad
    en unidad base, costo de entrada neto ya calculado con las reglas de FR-044, y la línea de origen si
    se valora a su costo); y los parámetros (método y redondeo).
  - Salidas: líneas de kardex, consumos de capa, el nuevo estado y la explicación.
- **Método y ámbito.** Promedio ponderado por defecto (`Costeo.Metodo = PromedioPonderado`), con ámbito
  cooperativa (`Costeo.Ambito = Cooperativa`) o bodega. Método y ámbito sólo cambian al inicio de un
  período sin movimientos posteriores (`Parameters.RequiresPeriodStart`).
- **Reglas de las entradas y devoluciones**:
  - devolución de cliente: al costo con que salió;
  - devolución a proveedor: al costo de entrada, con la diferencia contra el promedio como ajuste;
  - ajuste positivo: al costo vigente, o al indicado con `Inventory.Adjustments.SetUnitCost`;
  - saldo inicial: al costo cargado.
- **Casos límite**:
  - con existencia cero se conserva `LastUnitCost`;
  - con negativo permitido, la salida va al último costo y se regulariza al llegar la entrada;
  - anular una entrada ya consumida registra la diferencia (`VoidDifference`);
  - al agotar, el residuo de redondeo deja valor = 0 cuando cantidad = 0.
- **Traslados**: en ámbito cooperativa salen y entran al promedio (neutros). En ámbito bodega, la bodega
  de tránsito tiene su propio estado pero **sale siempre al costo de la línea de despacho**, así el
  tránsito queda en cero exacto al resolverse.
- **Nada se reescribe.**
  - Retroactivos, diferencias de precio, prorrateos, negativos regularizados y diferencias de anulación
    agregan líneas `Kind = CostAdjustment` con `AffectsEntryId`.
  - El ajuste de un retroactivo se fecha en la salida afectada, con el `RegisteredAt` real.
  - `AjusteDeCostoReconocido` sale **uno por documento afectado**.
  - El retroactivo se admite sólo con `Costeo.RetroactivosPermitidos`, dentro de
    `Costeo.RetroactivosDiasMaximos` y en un período abierto. Antes de confirmar, `SimularImpacto` corre
    el mismo motor sin guardar (I5; `POST /api/inventory/documents/{id}/cost-impact`).
- **Excepción de puesta en marcha y retroactivo mínimo en I1** (preguntas D8 y D9; precisión aplicada
  a la spec en FR-045). Dos clases de documento **no** dependen de `Costeo.RetroactivosPermitidos`:
  - el `OpeningBalance` de una bodega todavía `NotActivated`, y su `Voiding`. Con el ámbito por
    defecto (`Cooperativa`), la segunda bodega y las siguientes cargan su saldo fechado en su propio
    corte, cuando las ya activas tienen movimientos posteriores de los mismos productos; sin la
    excepción, ninguna bodega después de la primera podría salir en I1–I4 (FR-089, FR-091, SC-016);
  - los ajustes que el sistema genera desde un conteo aprobado, fechados en la foto (FR-041). En
    promedio ponderado, un ajuste valorado al promedio de su fecha no cambia el costo de las salidas
    posteriores; basta insertarlo en su lugar y comprobar que no aparece negativo.

  Para esas dos clases, `Retroactivo` se entrega **en I1**: el motor inserta las líneas en orden
  (`OperationDate`, `Id`), recalcula el costo de las salidas posteriores cuando cambia (el saldo
  inicial entra al costo cargado y mueve el promedio de la cooperativa), agrega líneas
  `CostAdjustment` sin reescribir nada y emite `AjusteDeCostoReconocido` por documento afectado. Los
  documentos retroactivos en general (compras, ajustes digitados, prorrateos) siguen siendo de I5.
- **Cambio de método** (I5): el valorizado por los dos métodos, por grupo contable (vista
  `method-change-valuation`), para la NIC 8 o la Sección 10.
- **Casos dorados** en `tests/IngenIA365ERP.Domain.Tests/Inventory/Costing/Casos/*.json`:
  - 01: promedio 10 @ 1.000 + 10 @ 1.300 → 1.150;
  - 02: devolución de cliente a 1.150 después de que cambió el promedio;
  - 03: devolución a proveedor;
  - 04 y 05: traslado en los dos ámbitos;
  - 06: existencia cero;
  - 07: negativo y regularización;
  - 08: anular una entrada consumida;
  - 09: compra retroactiva antes de tres ventas;
  - 10: prorrateo con parte vendida;
  - 11: PEPS con dos capas, venta de 15 → 16.500;
  - 12: residuo de redondeo;
  - 13: caja × 12 con conversión no exacta;
  - 14: IVA no descontable e INC al costo;
  - 15: ajuste de conteo al promedio de la fecha de la foto, con salidas posteriores en otra bodega
    del ámbito cooperativa (entra en su lugar; el costo de esas salidas no cambia);
  - 16: valorizado por los dos métodos;
  - 17: segunda bodega activada después de ventas en la primera, ámbito cooperativa (el saldo inicial
    entra en su fecha de corte, se recalculan las salidas posteriores y sale un
    `AjusteDeCostoReconocido` por documento afectado; I1).

  Además, `PropiedadesDelKardexTests` genera secuencias aleatorias y comprueba que Σ kardex = estado y
  que nunca queda negativo.

**Por qué**:
- SC-007 pide coincidir al peso con cálculos a mano, y el patrón de nómina (un motor puro con JSON que
  la contadora puede leer y firmar) ya está probado en producción.
- Sin IO, el retroactivo y PEPS se prueban sin base de datos, y Application sólo carga el historial y
  persiste.
- Con ámbito cooperativa, sumar `TotalCost` por bodega deriva: una bodega que recibió a 1.000 y vende a
  1.150 queda con valor negativo. Por eso el valor por bodega es cantidad × promedio.

**Alternativas descartadas**:
- El costeo en el handler, con EF: no se prueba sin base y se mezcla con el bloqueo.
- Procedimientos almacenados: dos implementaciones, contra el Principio II.
- Casos en C#: la contadora no los revisa.
- Recalcular y reescribir las salidas posteriores: viola FR-002.
- Un único ajuste global fechado hoy: rompe la división por documento afectado y el valorizado a
  fechas intermedias.
- Que el tránsito salga a su propio promedio: mezcla despachos de orígenes distintos.
- UEPS: FR-021 lo excluye.

**Riesgos**:
- En ámbito cooperativa, si la contadora mapea bodegas del mismo grupo a cuentas de inventario
  distintas, la conciliación descuadra. La completitud exige una cuenta de inventario por grupo en ese
  ámbito (pregunta D2).
- Los retroactivos con PEPS obligan a corregir consumos de capa con asignaciones negativas y nuevas
  (pregunta D6; propuesta: sólo promedio ponderado).
- El prorrateo con promedio necesita una regla para separar lo vendido de lo existente (pregunta D5).
- El flete facturado aparte no tiene prorrateo hasta I5, así que el costo de los meses de ensayo y
  salida puede quedar subvaluado (pregunta E7).

**Spec**: FR-021, FR-042 a FR-046, FR-006, FR-041, FR-089, FR-091, US3, US16, SC-007, SC-016; NIC 2 o Sección 13 y NIC 8 o
Sección 10 de la NIIF para PYMES (FR-021, FR-043).

## R11. Precisión decimal, redondeo y fecha de operación

**Decisión** (T19, T20):
- **Precisión.** La convención global (18,2) no cambia.
  `Persistence/Configurations/Inventory/PrecisionDeInventario` la fija propiedad por propiedad, y la
  prueba `LasCantidadesYCostosTienenSuPrecision` lo exige:
  - `Cantidad` (18,4);
  - `Factor` (18,6);
  - `CostoUnitario` (18,6);
  - `PrecioUnitario` (18,6);
  - `Monto` (18,2);
  - `Tarifa` (9,6).

  Casos puntuales: el precio de lista `INV_PriceListItems.Price` es (18,2); el precio de línea, (18,6);
  el impuesto por unidad `AmountPerUnit`, (18,2).
- **Tarifas.** **Toda tarifa es una fracción (9,6)**: 0,19; 0,00966 para 9,66 ‰. Aplica a
  `COR_TaxRates.Rate` y, por enmienda aditiva de la 009 en I2, a `ACC_AccountTaxRates.Rate`.
- **Redondeo.**
  - `MidpointRounding.AwayFromZero`, según `Redondeo.Montos` (`Centavo` o `Peso`).
  - El residuo se asigna según `Redondeo.Residuo` (`MayorValor` o `UltimaLinea`) y queda visible.
  - Cada unidad declara `AllowedDecimals` (0 a 4). Una conversión que no da exacta deja la diferencia
    en `RoundingQuantity` de la misma línea; si la unidad no admite los decimales necesarios, se
    rechaza con `Inventory.Unit.DecimalsNotAllowed`.
- **Fecha de operación local.** `IDateTimeService` gana `AhoraLocal` (`DateTimeOffset`) y `HoyLocal`
  (`DateOnly`).
  - Usan la zona de `Plataforma:ZonaHoraria`: `America/Bogota` por defecto; si la imagen no la trae,
    −05:00 fijo, con un aviso único en el log.
  - Los instantes se guardan en UTC; la fecha de operación es `HoyLocal`.
  - Quiénes la usan:
    - Inventario: fecha propuesta, «fecha ≤ hoy» y período;
    - POS: la `OperatingDate` de la sesión es la fecha local de apertura, y la fecha fiscal, la fecha
      local de la venta;
    - los lotes programados;
    - DIAN: hora con −05:00.
  - Los demás módulos siguen con `TodayUtc` hasta adoptarla.

**Por qué**:
- Son los números de FR-017, y los montos en (18,2) cuadran con la contabilidad de la 009.
- PostgreSQL redondea `numeric(18,2)` en silencio al insertar: un `HasPrecision` olvidado truncaría
  cantidades y costos sin error. Por eso la prueba de modelo.
- Las tarifas de ICA por mil no caben en 4 decimales de fracción, y C8 compara la tarifa del catálogo
  con la de la cuenta: las dos tienen que usar la misma unidad y la misma escala.
- Con `TodayUtc`, una venta del POS después de las 19:00 queda con fecha del día siguiente, y la noche
  del último día de un mes cae en el mes siguiente: otro período y otro documento fiscal.

**Alternativas descartadas**:
- Cambiar la convención global: afecta a todo el ERP.
- Tipos de valor con conversores de EF: los agregados no se traducen bien.
- Montos del kardex con 4 decimales: no cuadran con la contabilidad en pesos.
- Tarifas porcentuales (7,4) (núcleo), o fracción (9,4) como la contabilidad de hoy: no caben las
  tarifas por mil y descuadran C8.
- Zona horaria por cooperativa: no en esta feature.
- Arreglar ya la fecha de Contabilidad y Nómina (pregunta B2).

**Riesgos**:
- El ajuste de `ACC_AccountTaxRates.Rate` es una ampliación, no destructiva, sobre una tabla en
  producción.
- La imagen del contenedor puede no traer `tzdata`; de ahí el respaldo −05:00.
- Hasta que la plataforma adopte `HoyLocal`, conviven dos relojes (Inventario local, Contabilidad UTC),
  y un informe que cruce los dos puede dar fechas distintas para una misma noche.
- `PlantillaDeImportacion` formatea hoy los decimales como `0.00`: se agregan `0.0000` y `0.000000` a
  `TipoDeColumna` (R17).

**Spec**: FR-017, FR-018, FR-025, FR-006, FR-047, FR-058, SC-007; C8.

## R12. Parámetros con vigencia

**Decisión** (T21):
- **Almacén**: `COR_ParameterVersions` (`ParameterVersion`: `Module`, `Key`, `ScopeKind`, `ScopeId` (0 =
  general), `Value` en texto, `ValidFrom`, `ValidTo`, `Reason` obligatorio y `LegalSource` opcional; UK
  filtrado por no borrados).
- **Lector único**: `LectorDeParametros` resuelve el valor a una fecha con caída ámbito → general →
  defecto seguro. Un valor no admitido es el error `Parameters.ValueNotAllowed`, nunca el defecto.
- **Alta**: `AddParameterVersionCommand`.
  - Sin cruces (`Parameters.Overlaps`); cierra la anterior la víspera.
  - Motivo obligatorio (`IConMotivo`) y auditado con diferencia.
  - Reglas por clave: el método y el ámbito de costeo sólo cambian al inicio de un período sin
    movimientos posteriores; el modo de paso, por cadena y con confirmación para los tipos fiscales.
- **Definiciones**: `DefinicionDeParametro` lleva clave, tipo, valores admitidos, defecto, ámbitos
  (`ParameterScopeKind`: None, Warehouse, DocumentType, PointOfSale, CashRegister, ThirdPartyKind),
  permiso y si se sella.
  - Viven en catálogos cerrados por módulo: `ParametrosDeInventario` (`INV`), `ParametrosTributarios`
    (`TAX`) y `ParametrosDeFacturacionElectronica` (`EINV`).
  - Las más de cincuenta claves, con sus valores, defecto, ámbitos y entrega, son las de la tabla de
    `data-model.md` (§4.2), de `Costeo.Metodo` a `DocumentoSoporte.Generacion`, incluida
    `Informes.TopeFaltantesPorcentaje` (tope de la vista `shrinkage-cap`: fracción, defecto 0, exige
    `LegalSource`, I6).
  - «Sellado» quiere decir que se fija en el documento al confirmar: modo de paso, emisión, numeración y
    `Cartera.CuentaPorCobrarRegistradaPor`.
- **Con vigencia pero fuera de esta tabla** (FR-012):
  - prefijos y consecutivos (`INV_DocumentSequences`);
  - políticas de aprobación (`COR_ApprovalPolicies`) y montos máximos (`SEC_PermissionAmountLimits`);
  - impuestos (`COR_TaxRates`);
  - listas de precios y topes de descuento;
  - tolerancia de arqueo y comisión esperada por medio (`COR_PaymentMeans.ToleranceAmount`,
    `ExpectedCommission*`): el `PUT` del medio exige motivo (`IConMotivo`) y queda auditado con
    diferencia, y los dos valores se **copian** a cada línea de arqueo (`INV_CashCountLines`) o pago que
    los usa, así un cambio sólo afecta lo que se cierre después;
  - tipos de alerta;
  - configuración de emisión.

  La UVT sigue en `PAY_LegalParameters` (R21). Los valores **técnicos** (sondeo, espera, tandas) van en
  `appsettings`.
- **Rutas y pantalla**: `/api/inventory/parameters` (`Inventory.Parameters.View/Manage`) y
  `/inventario/parametros`. Lo vigila `LosParametrosSeLeenEnUnSoloSitio`.

**Por qué**:
- D-03 pide generalizar el patrón de nómina, y FR-012 y FR-034 necesitan ámbito (por bodega, por tipo),
  que `PAY_CompanyPolicies` no tiene.
- Una tabla en Core sirve a Inventario, al tributario y a la facturación electrónica, y mañana a Cartera
  y Tesorería.
- No tocar nómina evita riesgo sobre un módulo en producción.
- Un valor no admitido que cayera al defecto escondería un error de configuración (Principio IX).

**Alternativas descartadas**:
- `INV_Parameters`, copia de la de nómina: un tercer patrón paralelo.
- Migrar `PAY_CompanyPolicies` ahora: riesgo en producción sin beneficio.
- `COR_SystemSettings`: sin vigencia ni historial.
- Un bool en `COR_Companies` para la obligación de facturar: sin vigencia.
- `FacturacionElectronica.Obligada`, `DS.Generacion` e `Integracion.Cartera.HabilitadaDesde` como nombres
  de clave (informes DIAN y compras): quedan `Dian.ObligadaAFacturar`, `DocumentoSoporte.Generacion` y
  `Cartera.IntegracionHabilitadaDesde` (T1).

**Riesgos**:
- El valor en texto obliga a parsear y validar por tipo en el lector: hay casos de prueba por clave.
- Un parámetro con el ámbito mal elegido (por ejemplo, modo de paso por tipo sin respetar la cadena) se
  rechaza, y la plantilla de tipos debe explicarlo.
- `Value` sin tipo en la base impide agregar por valor en SQL; no hace falta.

**Spec**: FR-012, FR-013, FR-034, FR-042, FR-043, FR-045, FR-063, FR-067, FR-075, US12 esc. 3; D-03.

## R13. Aprobaciones multinivel

**Decisión** (T33):
- **Motor de plataforma**:
  - `Domain/Approvals/EvaluadorDePolitica`, puro y con casos dorados;
  - `Application/Common/Approvals/MotorDeAprobaciones`, detrás de `IMotorDeAprobaciones`
    (`EvaluarAsync`, `SolicitarAsync`, `DecidirAsync`, `PendientesParaMiAsync`).
- **Tablas**:
  - `COR_ApprovalPolicies`: por módulo (`Module = Inventory`), sujeto (`Subject`:
    `DocumentConfirmation`, `DiscountOverCap`, `ProvisionalCredit`, `TransferDiscrepancy`,
    `PurchaseMatchException`) y tipo de documento (opcional: nulo = todos los tipos del sujeto), con
    versión y vigencia sin cruces;
  - `COR_ApprovalPolicyLevels`: `Order`, `Threshold` y `PermissionCode`;
  - `COR_ApprovalRequests`: política sellada, monto, creador, solicitante, excluidos, `Status`,
    `CurrentLevel` y `ContentSha256`;
  - `COR_ApprovalDecisions`: hecho, con UK `(RequestId, Level)` entre aprobaciones.
- **Flujo**:
  - Al confirmar se evalúa con la política vigente en la fecha de operación.
  - Si tiene niveles, el documento queda `PendingApproval`, congelado y sin número.
  - La última aprobación confirma en su misma transacción; ahí se numera.
  - Rechazar exige motivo y devuelve el documento a `Draft`.
- **Segregación fija**. Se excluyen:
  - el creador y el solicitante;
  - los participantes que declare el documento: quien abrió o capturó el conteo, y el cajero en las
    diferencias de arqueo y en el crédito;
  - quien ya aprobó otro nivel del mismo documento.

  Una aprobación prohibida responde `Approvals.SelfApprovalForbidden`. La identidad es el `SEC_Users.Id`
  de `IActorActual`, nunca el entero del token ni el correo.
- **Huella**: `ContentSha256` invalida la aprobación si cambia lo aprobado (descuentos).
- **Métodos** (`ApprovalMethod`), nunca con contraseña:
  - `OwnSession`: desde la bandeja `/inventario/aprobaciones`; el POS consulta cada 2 s;
  - `InPersonPasskey`: `IWebAuthnService`, con las credenciales del aprobador;
  - `InPersonTotp`: de un solo uso.
- **Quién lo usa**: tipos de documento, descuentos sobre el tope, diferencias de arqueo, movimientos de
  caja, faltantes y sobrantes de traslado, ajustes de conteo, saldo inicial, crédito provisional y
  excepciones del cruce (I5).
- **Rutas y comandos**:
  - `/api/inventory/approval-policies`, `/api/inventory/approvals` y `/api/inventory/approvals/{id}/decide`;
  - permisos `Inventory.ApprovalPolicies.*` e `Inventory.Approvals.*`;
  - `DecideApprovalCommand`, `ListMyPendingApprovalsQuery` y `SaveApprovalPolicyCommand`;
  - alerta `Aprobaciones.Pendiente`.
- **Lo que no se migra**: `FourEyes` de Contabilidad y `AllowSameUserApproval` de Nómina.

**Por qué**:
- D-03 pide aprobaciones en varios niveles y por montos, reutilizables.
- FR-010 exige niveles, umbrales, permiso por nivel y una segregación que no se parametriza.
- El precedente que funciona con la identidad central es `QuienLlamaEnLaCooperativa`.
- Las decisiones de sólo inserción y la política sellada cumplen R2 y R3, y evitan que el monto cambie
  a mitad de la aprobación.
- La vía presencial evita que el cajero espere a que el supervisor abra su equipo (SC-004), y el
  Supuesto «Aprobaciones» prohíbe pedir la contraseña de otra persona.

**Alternativas descartadas**:
- La lógica dentro de cada documento: duplicada en unas diez clases.
- Extender `FourEyes`: es booleano, por empresa, y compara el `UserId` nulo.
- Un motor de flujos genérico (Elsa u otro): desproporcionado.
- La contraseña del supervisor: prohibida.
- Comparar por correo, como Nómina: el correo cambia.

**Riesgos**:
- Si el primer umbral es mayor que 0 y el usuario supera su monto máximo, FR-009 fuerza el nivel 1
  aunque no se alcance su umbral. Es fácil de implementar mal: va en los casos dorados.
- Sin SignalR, la aprobación remota depende del sondeo, y una venta con aprobación puede pasar de 30 s
  si el supervisor no está a mano.
- WebAuthn dentro del WebView de MAUI no está verificado; se prueba a mano.
- El TOTP en el terminal debe impedir que un código se use dos veces.

**Spec**: FR-009, FR-010, FR-041, FR-054, FR-061, FR-089, FR-099, FR-100, US12 esc. 2 y 6, SC-004; D-03;
Supuesto «Aprobaciones».

## R14. Permisos, alcance por bodega y punto de venta, y montos máximos

**Decisión**:
- **Permisos** (T48).
  - Los siembra la API al arrancar, nunca el DbMigrator: `PhaseZeroSecuritySeeder` invoca
    `InventoryPermissionCatalogSeeder`, `ElectronicInvoicingPermissionCatalogSeeder` y los códigos nuevos
    de `Core`, `Accounting` y `DomainPermissionCatalogSeeder`, antes de `BuiltInRolesSeeder`.
  - La lista exacta es la canónica, que reproduce `data-model.md`: la familia `Inventory.*`, más
    `Core.Taxes.*`, `Core.PaymentMeans.*`, `Accounting.InventoryRules.*`, `Accounting.InventoryBatches.*`,
    `ElectronicInvoicing.*` y `AuditLog.VerifyIntegrity`.
  - El permiso de cada ruta es fijo y sale de `DocumentClassGroup`: no hay permisos dinámicos por tipo
    de documento.
  - `Create`, `Confirm` y `Approve` van separados.
  - Sin el permiso de la ruta, la respuesta es el mismo 404 que para algo inexistente (FR-009). Los
    permisos que revisa el handler porque dependen del cuerpo, sobre un recurso que quien llama ya ve
    (`Adjustments.SetUnitCost`, `Warehouses.AcceptActivationDifference`,
    `Periods.AcceptUnbilledShipments`, `DocumentTypes.DisableFiscalPosting` y el permiso de una clave
    de parámetro), responden **422 con código propio**: no revelan existencia (precisión 11 a la spec;
    pregunta C10).
  - Las lecturas sensibles usan una acción distinta de `View` (`Costs.Read`, `Reports.ExportPersonalData`,
    `Scope.AllWarehouses`), porque `*.View` llega solo a Operator, ReadOnly y Auditor.
  - **Perfiles sugeridos** como plantillas (`PerfilesSugeridos`): `inventario.administrador`, `.jefe`,
    `.bodeguero`, `.comprador`, `.cajero`, `.aprobador`, `.contador` y `.auditor`.
    - Se sirven en `GET /api/admin/roles/templates?module=Inventory`.
    - `CreateRoleFromTemplateCommand` (`Security.Roles.Create`) los convierte en roles editables.
    - A los roles integrados no se les agrega escritura de inventario.
- **Alcance** (T35).
  - Tablas `INV_UserWarehouseScopes` (I1) e `INV_UserPointOfSaleScopes` (I3), con `IsDefault`.
  - **Falla cerrado**, salvo con `Inventory.Scope.AllWarehouses` o `Inventory.Scope.AllPointsOfSale`.
  - `IAlcanceDeInventario` (record `AlcanceDeInventario`) se memoriza por petición en
    `AlcanceDeInventarioDeLaPeticion`. En segundo plano, el alcance es total.
  - `FiltroDeAlcance` va en toda consulta:
    - documentos, por bodega de origen **o** de destino;
    - kardex y existencias, por bodega;
    - sesiones, por punto.
  - Todo comando valida cada bodega y cada punto que toca, y responde el 404 genérico si alguno está
    fuera. El aprobador también necesita alcance.
  - La bodega de tránsito se ve a través de los traslados de las bodegas del alcance. Su existencia
    propia sólo se ve con alcance total o asignación explícita.
  - Ruta `/api/inventory/scopes/users/{userPublicId}` (`Inventory.Scopes.Manage`) y pestaña «Alcance
    comercial» en `/admin/usuarios`.
- **Montos máximos** (T34).
  - Tabla `SEC_PermissionAmountLimits`: rol, código validado contra el catálogo, monto, moneda,
    vigencia y motivo.
  - `ILimitesPorPermiso.MontoMaximoAsync(permiso, fecha)`.
  - El límite efectivo es el mayor entre los roles activos que conceden el permiso; un rol que lo
    concede sin fila de límite es «sin límite».
  - Por encima del monto: el nivel 1 de la política del tipo más los niveles cuyo umbral se alcanza. Sin
    política, `Inventory.Approval.AmountExceedsLimit` con `data.maxAmount`.
  - Aplica a `Purchases.Confirm`, `Adjustments.Confirm`, `Sales.SellOnCredit`, notas y órdenes.
  - Se administra con `Inventory.ApprovalPolicies.Manage` (`SetPermissionAmountLimitCommand`,
    `/api/inventory/amount-limits`).

**Por qué**:
- Es la convención de los cuatro seeders existentes. El permiso va por grupo de documento porque
  `SEC_Permissions` es un catálogo estático y `.RequirePermission` es fijo por ruta.
- Plantillas y no roles integrados, porque:
  - `BuiltInRolesSeeder` reinserta los vínculos en cada arranque, así que un rol integrado no se puede
    ajustar por cooperativa;
  - se sembrarían ocho roles también en las cooperativas que no usan comercio.
- El alcance sigue el patrón probado de la 009 (servicio más filtros explícitos), pero más fino:
  bodega y caja, no oficina. Falla cerrado para cumplir SC-014 con un valor por defecto seguro.
- El límite pertenece al par rol-permiso (US12 esc. 6). Como tabla aparte, no toca `SEC_RolePermissions`
  ni su caché.

**Alternativas descartadas**:
- Un permiso por tipo de documento, dinámico: sembrado y caché por cooperativa, y la ruta no puede
  declararlo.
- Un único `Inventory.Documents.*`: impide la segregación.
- Reusar los códigos heredados de `IdentitySeedData`: no separan confirmar de aprobar.
- `Inventory.Taxes.*` e `Inventory.PaymentMethods.*` (informe de seguridad): esos catálogos son de Core
  (T22, T25).
- `Sales.*`, `Sales.ElectronicDocuments.*` y `/api/sales/*` (informes de ventas y DIAN): hay un solo
  módulo `Inventory`, y la facturación electrónica es de plataforma (T3).
- Ocho roles integrados, o ampliar los globs de Operator.
- Reusar `SEC_UserBranchAssignments`: no distingue bodegas ni cajas.
- Una tabla genérica `SEC_UserScopeAssignments`: sin FK.
- Un filtro global de EF como mecanismo principal: choca con el filtro de borrado lógico, con el
  procesador de fondo y con la verificación del kardex.
- Sin filas, sin restricción, como en la 009: falla SC-014.
- Una columna `MaxAmount` en `SEC_RolePermissions`; un límite por usuario; el menor entre los roles.

**Riesgos**:
- Un `Inventory.*.View` nombrado por descuido filtra datos sensibles a todos los roles integrados.
- Resolver permisos, alcance y límites en cada petición del POS suma consultas: se memorizan por
  petición y se cachean por usuario, con invalidación.
- Un usuario sin asignaciones no opera nada. Hay que decirlo en la puesta en marcha (pregunta C5).
- `AuditLog.VerifyIntegrity` llega por glob a Auditor y a CompanyAdmin.

**Spec**: FR-009, FR-012, FR-032, FR-083, FR-087, FR-093, US12 esc. 1 y 6, SC-014.

## R15. Auditoría verificable

**Decisión**:
- **Origen, motivo, rechazos y diferencias** (T36). Se **amplía** `AuditableEntityInterceptor`; no se
  crea otro.
  - Exclusión por entidad con `[SinDiffDeAuditoria]`: `IntegrationMessageDelivery`,
    `IntegrationDeliveryAttempt`, `BackgroundLease`, `OperationKey`, las proyecciones, `KardexEntry` (el
    documento ya es la referencia), `AuditOutboxEntry`, `AuditChainHead` y `AuditAnchor`.
  - Exclusión por propiedad con `[NoAuditar]`: `TechnicalKey`, `CredentialKey` y los secretos.
  - `InferModule` (del interceptor y de `AuditBehavior`) reconoce, antes de `.Core`:
    `.ElectronicInvoicing`, `.Integration`, `.Approvals`, `.Alerts`, `.Parameters`, `.Core.Taxes` →
    `Taxes`, y `.Core.Payments` o `.Core.PaymentMeans` → `PaymentMeans`.
  - `IOrigenDeLaPeticion` da la IP (del `IIpAddressAccessor` existente), el User-Agent, el canal y el
    endpoint. El canal es:
    - `web` o `app`, por la cabecera `X-Canal` que pone `CanalDeOrigenHandler` en los tres anfitriones;
    - `pos`, cuando el comando implementa `IOperacionDePuntoDeVenta`;
    - `proceso`, en segundo plano.
  - `IConMotivo.Reason` se copia al evento.
  - Un `Result.IsFailure` se registra como `Rejected`, con el `Error.Code`.
  - La clave de idempotencia y el `ActorKind` van en la metadata.
- **Entrega garantizada** (T37). Para los módulos encadenados, el evento se escribe en `COR_AuditOutbox`
  en la misma transacción del cambio.
  - El interceptor agrega las diferencias en `SavingChanges`, y para esos módulos deja de mandarlas a
    Mongo.
  - `AuditBehavior` agrega el evento del comando dentro de la transacción de `IdempotencyBehavior`, o de
    una `TransaccionExplicita` propia.
  - Los rechazos se escriben por un contexto aparte (`ITenantDbContextFactory`), para que sobrevivan al
    rollback. Exportar, imprimir e ingresar insertan su propia fila.
  - `AuditOutboxForwarder` (arrendamiento `audit.forward`) reenvía a Mongo en orden de Id, con
    `_id = EventId` (un duplicado cuenta como hecho).
  - Después de reenviar, un `UPDATE` vacía `PayloadJson`: queda una fila delgada (`Seq`, `Hash`), nunca
    un `DELETE`.
  - Los módulos no encadenados siguen por `MongoAuditService`, con su guarda (R5).
- **Sello de integridad** (T38).
  - Un flujo por cooperativa y clase de retención: `Stream = "{tenantPublicId:N}:10y"`.
  - Los módulos de `AuditoriaEncadenada.Modulos` son `Inventory`, `ElectronicInvoicing`, `Integration`,
    `Approvals`, `Alerts`, `Parameters`, `Taxes`, `PaymentMeans` y `Navigation`. **Accounting queda
    fuera** (pregunta C1).
  - El reenviador asigna `Seq` y `Hash = SHA-256(prevHash || JSON canónico)`, y actualiza
    `COR_AuditChainHeads` con `RowVersion`: la cadena no se bifurca aunque coincidan dos réplicas.
  - La canonicalización está en un solo sitio, `SelloDeIntegridad`: claves ordenadas, UTC en ISO,
    decimales invariantes, versión `v` y casos dorados.
  - Anclas cada 1.000 eventos y diarias en `COR_AuditAnchors`, con HMAC de `AuditSignature` en una
    versión propia, y un ancla génesis al activar.
  - `VerifyAuditIntegrityQuery` (`POST /api/audit/integrity/verify`, `AuditLog.VerifyIntegrity`, pestaña
    «Integridad» en `/admin/auditoria`) informa alterado, eliminado, intercalado, ancla inválida y
    «purgado por retención». La verificación misma se audita como `AuditLog.IntegrityVerified`. Es una
    consulta sin clave de idempotencia: cuerpo `{ from, to, stream? }` (por defecto, el flujo de 10 años
    de la cooperativa); respuesta `{ stream, fromSeq, toSeq, checked, anchorsChecked, incidents[] }` con
    `kind` = `Altered`, `Deleted`, `Interleaved`, `AnchorInvalid` o `PurgedByRetention`; un rango de
    más de 10 años responde 400 `Validation.Invalid`.
- **Retención**: `AuditRetention.ModulosDeDiezAnios` suma `Inventory`, `ElectronicInvoicing`,
  `Integration`, `Approvals`, `Alerts`, `Parameters`, `Taxes` y `PaymentMeans`.

**Por qué**:
- El Principio X ya exige IP, User-Agent y antes/después, y hoy no se registran.
- El interceptor existente ya captura las diferencias de toda entidad: es el punto natural, y un
  segundo interceptor duplicaría el bucle.
- No hay transacción distribuida entre SQL y Mongo. El outbox es el patrón estándar, cumple SC-012
  (100 %) y deja la cadena fuera del camino caliente del POS.
- Encadenar por clase de retención hace que el TTL borre siempre un prefijo, sin abrir huecos.
- El ancla con HMAC, cuya clave está fuera de las bases, obliga a un atacante a alterar Mongo y SQL y
  además conseguir la clave. Compensa que el rol append-only de Mongo no esté aplicado a las bases por
  cooperativa.
- La fila delgada conserva el testigo sin `DELETE` (Principio VII).

**Alternativas descartadas**:
- Un interceptor nuevo con activación explícita `IAuditarCambios` y un `ColectorDeCambios` (informe de
  seguridad): se amplía el existente con exclusiones (T36).
- Escribir en Mongo de forma sincrónica y fallar si Mongo falla: no es atómico y pone Mongo en el camino
  del POS.
- La auditoría del módulo en SQL, con triggers: rompe la consola única.
- Calcular la cadena al escribir en Mongo: serializa todas las escrituras entre réplicas.
- Firmar cada evento sin encadenar: no detecta eliminaciones ni intercalados.
- Un árbol de Merkle o un ledger externo: desproporcionado. La copia externa del ancla queda abierta
  (pregunta C2).
- El candado Redis `lock:audit-chain:{tenant}:{stream}`: se reemplaza por `COR_BackgroundLeases` y la
  cabeza de cadena con `RowVersion`.
- Purgar el outbox ya reenviado: sería una excepción al Principio VII (pregunta C3).

**Riesgos**:
- La canonicalización es frágil: el orden de claves, la zona horaria, los decimales o un cambio del
  driver dan falsos positivos. Se mitiga con un solo sitio, la versión `v` y los casos dorados.
- La clave `dev-v1`, escrita en el código, debe estar reemplazada en producción antes de I1, y las
  anclas usan una versión nueva (pregunta A2).
- Hay que verificar en los tres clústeres con qué usuario de Mongo entra la API (pregunta A2).
- A 30.000 eventos diarios, el outbox crece del orden de 110 millones de filas delgadas en 10 años por
  cooperativa, unos 10 GB.
- Los cambios en `AuditBehavior` y en el interceptor afectan a todos los módulos: tienen que ser
  aditivos, con regresión en Nómina y Contabilidad.
- Una cadena que empieza con un ancla génesis deja fuera los eventos anteriores. Se informa, no se
  oculta.

**Spec**: FR-007, FR-008, FR-083, US12 esc. 4 y 5, SC-012, SC-013; D-03; Principios VII y X; Supuesto
«Retención».

## R16. Alertas

**Decisión** (T39):
- **Tablas**:
  - `COR_AlertTypes`: configuración por cooperativa, con vigencia. Lleva `TypeCode`,
    `RecipientPermissions`, `Channels` (`InApp`, `Email`), umbrales en JSON y motivo. Cambiarla exige
    `Inventory.Alerts.Manage`.
  - `COR_Alerts`: el estado compartido. Lleva tipo, módulo, severidad, asunto, cuerpo, entidad, un
    `DedupKey` único mientras está pendiente, `Status` (Pending | Attended), quién la atendió y su nota,
    y `WithoutRecipient`.
- **Comandos**: `RaiseAlertCommand`, `AttendAlertCommand` y `SaveAlertTypeCommand`.
- **Destinatarios**: por permiso **y alcance**, con `IDestinatariosPorPermiso`: los usuarios activos que
  tienen alguno de los permisos y alcance sobre la bodega o el punto de la alerta.
- **Entrega**: por `SendNotificationCommand`, con un nuevo `NotificationType.Alert`, y el correo por
  `NotificationEmailDispatcher`.
- **Tipos**: los 19 de `data-model.md`, sembrados con sus permisos destinatarios por defecto por
  `AlertTypesSeeder` (Order 83):
  - de Inventario: `Inventario.Reorden`, `Inventario.Quiebre`, `Inventario.IncidenteDeIntegridad`,
    `Inventario.VentaBajoCosto`, `Inventario.ProximoAVencer` e `Inventario.RemisionSinFacturar`;
  - de compras y aprobaciones: `Compras.EventosRadianFaltantes` y `Aprobaciones.Pendiente`;
  - de integración: `Integracion.MensajeSinEntregar`, `Integracion.MensajeRechazado`,
    `Integracion.LoteNoCorrio` e `Integracion.ValidacionFallida`. `Integracion.LoteNoCorrio` llega a
    Contabilidad y a Inventario (`Accounting.InventoryBatches.Run` e `Inventory.Messages.Reprocess`,
    InApp + Email, Warning), como pide FR-080;
  - de personas: `Personas.SinPoliticaDeDatos`, cuando se da de alta una persona sin política de
    Habeas Data publicada (R22): destinatarios `Compliance.HabeasData.RecordConsent`, InApp + Email,
    Warning, I3;
  - los seis `Dian.*`.
- **Sin destinatario activo**: la alerta se enruta a los titulares de `CompanyAdmin` y se marca
  `WithoutRecipient` (SC-022). El reporte de completitud lista los tipos cuyo permiso no tiene ningún
  usuario activo.
- **Alertas de fondo**: las levanta `ProgramadorDeTareas`, o el procesador de cada cooperativa con el
  contexto de R5.
- **Bandeja y rutas**: `/inventario/alertas`; `/api/inventory/alerts`, `/api/inventory/alerts/{id}/attend`
  y `/api/inventory/alert-types` (`Inventory.Alerts.View/Attend/Manage`).
- **Empuje en tiempo real**: SignalR no se arregla; las pantallas consultan.

**Por qué**:
- FR-022 pide destinatarios por permiso, canal, estado pendiente o atendida y quién la atendió. Ese
  estado compartido no lo modela `COR_Notifications`, que es una fila por destinatario.
- Reutilizar las notificaciones para la entrega evita un segundo despachador.
- D-03 la pide como bandeja de plataforma.

**Alternativas descartadas**:
- Sólo `COR_Notifications`: cada destinatario la atendería por separado.
- Alertas calculadas al vuelo: sin historial ni quién atendió.
- `INV_Alerts`: D-03 la quiere de plataforma, para Contabilidad y Cartera.
- Rutas con `Notifications.ManageOwn`: Auditor y ReadOnly no lo tienen.

**Riesgos**:
- La bandeja en la aplicación depende del arreglo de `ListMyNotificationsQuery` (R5).
- Con dos réplicas, el correo se duplica si el despachador no toma su arrendamiento.
- Una alerta de alto volumen (reorden sobre 50.000 productos) necesita `DedupKey` y agrupación por
  corrida para no inundar la bandeja.

**Spec**: FR-022, FR-035, FR-050, FR-061, FR-063, FR-065, FR-067, FR-080, FR-088, SC-022; Principio IX;
D-03.

## R17. Catálogo, búsqueda de productos e importación

**Decisión**:
- **Catálogo** (I1):
  - `INV_UnitsOfMeasure`, con `AllowedDecimals` (0 a 4) y `DianUnitCode` (Rec. 20);
  - `INV_ProductCategories` (`ParentId`, `Level` ≤ 5) e `INV_Brands`;
  - `INV_AccountingGroups`;
  - `INV_Products`: `Code` (20), `Kind` (`ProductKind`), categoría, marca, unidad base, grupo contable,
    `TracksLot`/`TracksSerial`/`TracksExpiry`, `Status` (Active | Inactive | Blocked), `VatSaleTreatment`,
    `WithholdingConceptId`, `Reference`, `Weight`, `Volume` y `SearchText`;
  - `INV_ProductUnits`: `Factor` (18,6), de compra o de venta;
  - `INV_ProductBarcodes`: único en la cooperativa, con el `ProductUnitId` del empaque. Un duplicado
    responde `Inventory.Barcode.Duplicate` nombrando al producto que lo tiene;
  - `INV_ProductTaxes` e `INV_ProductAccountingGroupChanges`;
  - en I6: componentes, variantes, lotes y series.
- **Reglas del catálogo**:
  - La unidad base de un producto con movimientos no cambia, y un producto con historia no se borra.
  - Cambiar el grupo contable de un producto con existencia es `ChangeProductAccountingGroupCommand`
    (`Inventory.Catalog.ReclassifyAccountingGroup`, con motivo). Afecta sólo lo futuro y emite
    `GrupoContableReclasificado`.
  - Los códigos siguen `CodigoDeCatalogo`: 20 caracteres el de producto, 10 el de bodega y los demás.
  - Como la matriz los referencia por código, **los códigos no cambian una vez creados**: grupo, bodega,
    punto, medio de pago, tarifa y causa.
- **Adjuntos del catálogo** (D-04), en `AdjuntosDeModulo`:
  - imágenes, con dueño `InventoryProduct`: sube quien tiene `Inventory.Catalog.Manage`, y el borrado se
    audita;
  - soportes de bajas y ajustes negativos, con dueño `InventoryAdjustmentSupport`: sube quien crea el
    documento, y no se borran después de confirmar.
- **Búsqueda** (T43). Se adelanta a I1 porque FR-020 rige en toda pantalla.
  - **Lectura exacta**, por igualdad: el código de barras (UK filtrado entre vivos, que devuelve la
    unidad y el factor del empaque) o el código del producto. Ruta `/api/inventory/pos/lookup`
    (`LookupPosProductQuery`).
  - **Búsqueda mientras se escribe**: `/api/inventory/products/search` (`SearchProductsQuery`) sobre
    `SearchText`.
    - `SearchText` se normaliza en C# con `NormalizadorDeBusqueda`: mayúsculas, sin tildes y espacios
      colapsados. Reúne código, nombre, referencia y marca.
    - La consulta usa `EF.Functions.Like` por cada término escapado, hasta 5, y todos deben aparecer.
    - Orden: código exacto, luego prefijo, luego nombre. Devuelve 20, con la existencia de la bodega.
  - **Índices**: en PostgreSQL, `pg_trgm` más un índice GIN (en la migración
    `InventarioComercialNucleo`); en SQL Server, un índice no agrupado estrecho con `INCLUDE`.
  - **Cliente**: espera 200 ms, pide al menos 2 caracteres y cancela la petición anterior.
  - `Components/Inventario/BuscadorDeProducto` es reutilizable y acepta lector: lo usan compras,
    traslados, conteos y POS.
- **Importación** (T49).
  - Usa `PlantillaDeImportacion` e `ITabularFileReader`.
  - `ErrorDeFila` se mueve a `Application/Common/Imports` y se reexporta desde contabilidad.
  - Todo comando de importación recibe `ModoDeImportacion { Review, Apply }`: la revisión previa corre
    sin guardar y muestra lo que se creará o actualizará (FR-030).
  - Se agregan los formatos `0.0000` y `0.000000` a `TipoDeColumna`.
  - Es todo o nada, y comparte las reglas con el alta unitaria.
  - **Orden de carga**: impuestos y retenciones, grupos contables, unidades, marcas, categorías,
    productos, bodegas y ubicaciones, tipos de documento y vendedores. Los vendedores se citan por el
    documento de la persona: si la persona no existe, la fila se rechaza, porque la importación no crea
    personas.
  - **Todas** las plantillas de FR-095 se publican en I1: las de puntos y cajas, medios de pago, listas
    de precios y topes publican su `GET template.xlsx` en I1 y su importación llega en I3. La de la
    matriz llega en I2, y la de cifras de SOLIDO, en I1.
  - El modelo de cada catálogo se congela antes de publicar su plantilla.
  - Rutas: `GET …/template.xlsx` (con `?withData=true` para bajar lo cargado) y
    `POST …/import?mode=review|apply`, junto a cada catálogo. Pantalla `/inventario/plantillas`.
  - La fuente única de la mecánica, el orden completo (16 plantillas), las hojas, las columnas, el
    `ImportResultDto` y los códigos `Import.*` (`Import.Invalid`, `Import.Cell.Required`,
    `Import.Cell.PermissionRequired`) es `contracts/plantillas.md` (§0 y §1 a §16).
- **Vendedores** (FR-031).
  - `INV_Salespeople` se conserva.
  - `CreateSalespersonCommand` restaura o crea, y `UK_INV_Salespeople_PersonId` pasa a filtrado
    `[IsDeleted] = 0`.
  - Permisos `Inventory.Salespeople.View/Manage`, pantalla `/ventas/vendedores` e importación por
    plantilla (`ImportSalespeopleCommand`).
  - Rutas en `/api/inventory/salespeople`: `GET`, `POST` (`CreateSalespersonCommand`: restaura la fila
    retirada de esa persona con el mismo `PublicId`, o crea una, y escribe `IsSalesperson` en el mismo
    `SaveChanges`), `POST /{id}/retire` con motivo (`DeleteSalespersonCommand`: baja lógica y apaga la
    marca) y `template.xlsx` · `import`.
  - La operación se muda y se endurece **antes** del retiro del módulo actual.

**Por qué**:
- COOFLOPAL prepara su parametrización en estas plantillas (FR-095), y la mecánica es la de la 009 E2.
  Sin la revisión previa no se cumple FR-030, y con `0.00` las cantidades de 4 decimales se ven
  redondeadas.
- La lectura, que es lo crítico de SC-004, es una búsqueda por igualdad: O(log n) en los dos motores.
- La búsqueda por texto cumple SC-009 sin depender de extensiones de SQL Server, y normalizar en C#
  da el mismo resultado en los dos motores sin `unaccent` ni collations.
- `Like` explícito en vez de `Contains`, porque Npgsql puede traducir `Contains` a `strpos()`, que el
  índice trigram no usa.

**Alternativas descartadas**:
- Un importador propio de inventario: dos mecánicas para el mismo usuario.
- Full-Text Search de SQL Server: no está en todas las ediciones ni en la imagen de contenedor, busca por
  palabra y se puebla de forma asíncrona.
- `tsvector`: no encuentra códigos parciales y no tiene par en SQL Server.
- Todo el catálogo en la memoria del navegador: varios MB, precios viejos y memoria en Android.
- `Contains` o `ILIKE` sin normalizar.
- Un código de producto de 10 caracteres: no caben las referencias de proveedor (pregunta D1).

**Riesgos**:
- `pg_trgm` debe poder crearse en las bases de los tres clústeres. Es una extensión «trusted» desde
  PostgreSQL 13, pero hay que verificar la versión y los privilegios del rol `ingenia`.
  **Verificado el 2026-09-25 (T006)**:
  - DEV, QA y PDN corren PostgreSQL 17.7 con `pg_trgm` 1.6 disponible (`pg_available_extensions`);
    todavía no está instalada en ninguna base.
  - Al ser «trusted», basta el privilegio `CREATE` sobre la base, y `ingenia` es dueño de las bases
    que crea. Así `CREATE EXTENSION IF NOT EXISTS pg_trgm` va en `InventarioComercialNucleo` sin
    superusuario.
  - La imagen `postgres:17` de las e2e trae los módulos contrib, `pg_trgm` incluido.
  - SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`) no instala la búsqueda de texto
    completo por defecto, así que rige el recorrido del índice cubriente sobre `SearchText` con
    `INCLUDE (Code, Name, Status)`.
  - La alternativa, si T1009 no cumple SC-009, exige una imagen con el componente
    `mssql-server-fts` en pruebas y en los servidores, más `CREATE FULLTEXT CATALOG/INDEX` en la
    migración de SQL Server.
- En SQL Server, el `LIKE` con comodín inicial recorre el índice: hay que medir SC-009 con 50.000
  productos y 30 búsquedas concurrentes (Skip explícito si no corre).
- Una plantilla publicada amarra el modelo: cambiar columnas después invalida lo que COOFLOPAL ya
  diligenció.

**Spec**: FR-011, FR-020, FR-023 a FR-031, FR-095, US1, SC-004, SC-008, SC-009; D-04, D-07.

## R18. Bodegas, tránsito, traslados y conteos

**Decisión**:
- **Estructura**: cooperativa → sucursal contable (`COR_Branches`) → bodega (`INV_Warehouses`) →
  ubicación (`INV_WarehouseLocations`, con una por defecto).
  - `INV_WarehouseTypes` es parametrizable, pero cada tipo tiene un comportamiento fijo:
    `WarehouseBehavior` { `Operational`, `Transit` }.
  - La bodega de tránsito de una sucursal se crea con la primera bodega operativa de esa sucursal
    (Supuesto 15), y no se crea sola fuera de ese momento. Su código lo propone el sistema (`TR` + código
    de la sucursal) y lo puede fijar quien crea: campo opcional `transitWarehouse { code, name }` en
    `POST /api/inventory/warehouses`, o una fila de tipo tránsito en la plantilla de bodegas. Crear a
    mano otra bodega de tipo tránsito responde `Inventory.WarehouseType.TransitIsSystem`. Como la
    matriz la cita por código (T27), ese código es permanente. Esa bodega:
    - no se inactiva mientras tenga existencia;
    - no es origen de ventas ni de despachos (regla de clase);
    - cuenta en el valorizado, pero no en la existencia física de ninguna bodega operativa.
  - Reorden por producto y bodega, en `INV_ReorderPolicies`. La posición es disponible + en tránsito +
    por recibir (0 hasta I5). Las alertas `Inventario.Reorden` e `Inventario.Quiebre` las levanta
    `ProgramadorDeTareas`.
  - Stock negativo: `Existencias.StockNegativoPermitido`, con valor general y excepción por bodega.
    Prohibido por defecto.
- **Traslado en dos pasos**: `DispatchTransferCommand` y `ReceiveTransferCommand`, en
  `/api/inventory/transfers`.
  - El **despacho** saca del origen y entra al tránsito de la sucursal de **origen**, al costo de
    origen (`TrasladoDespachado`).
  - La **recepción** saca del tránsito, como máximo lo despachado (vínculo de línea), al costo de la
    línea de despacho, y hereda el destino de su mensaje (`TrasladoRecibido`).
  - El **faltante** queda pendiente en `INV_TransferDiscrepancies` y se resuelve con aprobación
    (`ResolveTransferDiscrepancyCommand`): `ReturnToOrigin`, `WriteOffFromTransit` (con su causa) o
    `LateReceipt`.
  - El **sobrante** queda fuera de la existencia hasta que un ajuste aprobado lo entra al costo vigente
    (`SurplusAdjustment`, que emite `AjusteInventarioAprobado`).
  - Anular un despacho no recibido devuelve la mercancía al origen.
  - La existencia en tránsito hacia una bodega se consulta sobre los despachos abiertos con ese destino.
  - El movimiento entre ubicaciones es `LocationMove`: un paso, sin costo y sin mensaje.
- **Conteos**: `OpenPhysicalCountCommand`, `CapturePhysicalCountCommand` y `ClosePhysicalCountCommand`,
  en `/api/inventory/counts`.
  - El conteo es un documento de clase `PhysicalCount`.
  - Al abrir se congela la foto de la existencia teórica (`INV_CountSnapshotLines`), **sin número**:
    el conteo sigue en `Draft` con su `CountSnapshotAt` y se numera al cerrarse (`Confirmed`), así un
    conteo abierto que se descarta (`POST /{id}/discard`, con motivo) no deja hueco (FR-038). Anular uno
    cerrado (`POST /{id}/void`) crea un documento `Voiding` que lo referencia, sin kardex ni mensajes
    (FR-006).
  - La captura admite lector, varios contadores y conteo ciego (`INV_CountCaptures`).
  - Una diferencia por encima de `Conteo.ToleranciaReconteoPorcentaje` o
    `Conteo.ToleranciaReconteoUnidades` exige reconteo.
  - Con `Conteo.BloquearMovimientos` (el defecto), los productos del conteo no admiten movimientos
    (`Inventory.Count.ProductsLocked`).
  - El ajuste se genera sólo después de aprobado, y no aprueba quien abrió o capturó el conteo.
  - El ajuste se fecha según `Conteo.FechaDelAjuste` (`Foto` por defecto), o el primer día abierto si ese
    período ya se cerró, y se valora al promedio vigente en esa fecha. Aunque queden salidas
    posteriores en otras bodegas del ámbito, no depende de `Costeo.RetroactivosPermitidos`: el motor lo
    inserta en su lugar con el retroactivo mínimo de I1 (R10, caso dorado 15; pregunta D9).
  - El conteo por clase ABC llega en I6.

**Por qué**:
- La bodega de tránsito mantiene la mercancía en camino en el kardex y en el valorizado (US3-2, US10).
- Que el comportamiento sea fijo impide que un tipo parametrizado convierta una bodega de ventas en
  tránsito.
- Salir del tránsito al costo del despacho (identificación específica) lo deja en cero exacto en los
  dos ámbitos de costeo.
- Congelar la foto del conteo y aprobar aparte de la captura son las reglas de FR-040 y FR-041.

**Alternativas descartadas**:
- Un estado «en tránsito» en el documento, sin bodega virtual: la mercancía desaparecería del kardex
  entre el despacho y la recepción.
- El tránsito de la sucursal de destino: la spec eligió el de origen.
- Aceptar una recepción mayor que lo despachado como entrada directa: el sobrante sería existencia sin
  aprobación.

**Riesgos**:
- Un conteo total grande bloquea muchas filas de proyección: el mismo límite de escalamiento de SQL
  Server de R8.
- Los faltantes de traslado sin resolver avisan en el cierre pero no lo bloquean, y pueden acumularse
  en tránsito.
- El alcance de un bodeguero de destino debe dejarle ver el despacho que viene hacia su bodega sin ver
  la existencia de origen.

**Spec**: FR-032 a FR-035, FR-039 a FR-041, US1, US10, US11, SC-008; Supuesto 15.

## R19. Períodos, saldo inicial, activación por bodega y convivencia con SOLIDO

**Decisión**:
- **Período** (informe de núcleo, decisión 9).
  - Tablas:
    - `INV_Setup`, fila única: `StartDate` y `LastClosedDate`. La crea el primer registro de una fecha
      de corte (o `ImportOpeningBalanceCommand` si no existe), con `StartDate` = primer día del mes del
      primer corte; un documento anterior responde `Inventory.Document.DateBeforeCutoff`;
    - `INV_Periods`: mes, estado, cierre, reapertura, motivo y remisiones sin facturar aceptadas;
    - `INV_PeriodClosingBalances`: el valorizado fijado por producto × bodega, con su grupo y su versión.
      Reabrir marca la versión como `Superseded`.
  - Un documento con `OperationDate ≤ LastClosedDate` se rechaza con `Inventory.Period.Closed`,
    nombrando el período. Los períodos cierran en orden, y sólo se reabre el último.
  - Al confirmar se toma un bloqueo **compartido** sobre `INV_Setup`; el cierre lo toma **exclusivo**:
    espera a las confirmaciones en vuelo y bloquea las nuevas mientras dura.
  - El cierre (`CloseInventoryPeriodCommand`):
    - se bloquea si hay conteos con foto en el período;
    - avisa de borradores, tránsitos sin resolver y mensajes;
    - exige que alguien con `Inventory.Periods.AcceptUnbilledShipments` acepte, con motivo, las
      remisiones sin facturar (I6);
    - fija el valorizado;
    - emite `PeriodoInventarioCerrado` (informativo) y crea el lote `PeriodClose`.
  - Reabrir es `ReopenInventoryPeriodCommand` (`Inventory.Periods.Reopen`, con motivo), y emite
    `PeriodoInventarioReabierto`.
- **Saldo inicial**: `ImportOpeningBalanceCommand`, en `/api/inventory/opening-balances`
  (`Inventory.OpeningBalance.Load/Approve`).
  - Por plantilla, con revisión previa y todo o nada.
  - Un documento `OpeningBalance` por bodega, partido en documentos de hasta 4.000 líneas, fechado en su
    corte y confirmado con aprobación.
  - Emite `SaldoInicialCargado` **informativo** (Supuesto 2: su valor ya está en los libros por la
    apertura de la 009). No pasa por la validación previa ni por el modo de paso.
  - Su anulación también es informativa.
  - **Excepción de puesta en marcha** (R10; pregunta D8): el `OpeningBalance` de una bodega todavía
    `NotActivated`, y su `Voiding`, no dependen de `Costeo.RetroactivosPermitidos`. En ámbito
    cooperativa, la segunda bodega y las siguientes cargan su saldo en su propio corte cuando las ya
    activas tienen movimientos posteriores de los mismos productos: el motor lo inserta en orden
    (`OperationDate`, `Id`), recalcula el costo de esas salidas posteriores (el saldo entra al costo
    cargado y mueve el promedio) y registra `AjusteDeCostoReconocido` por documento afectado. Se
    entrega en I1 (caso dorado 17). La alternativa, si el dueño la prefiere, es exigir
    `Costeo.Ambito = Bodega` mientras haya bodegas no activas.
- **Activación**: `ActivateWarehouseCommand`, en `/api/inventory/warehouses/{id}/activation`, con la
  pantalla `/inventario/activacion` (I2).
  - `INV_Warehouses` lleva `ActivationStatus` (`NotActivated`/`Active`), `CutoffDate` y `ActivatedAt/By`.
  - Una bodega no activa sólo admite su saldo inicial y su anulación: es una regla de clase en el
    confirmador.
  - Antes de activar se compara, a la fecha de corte y por grupo y conjunto de cuentas:
    - el valorizado de las bodegas activas y de la que se activa, más las cifras de SOLIDO de las no
      activas;
    - contra el saldo contable (`IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync`).
  - La comparación se guarda en `INV_WarehouseActivations`.
  - Activar exige cuadre, o aceptar la diferencia con `Inventory.Warehouses.AcceptActivationDifference`
    y motivo.
  - Antes de I2 no hay matriz ni consulta de saldos, así que no hay comparación. Ese camino (activar
    aceptando la diferencia) **sólo existe fuera de producción**: en producción el `POST` responde 422
    `Inventory.Activation.AccountingUnavailable` mientras la consulta de saldos no exista (FR-090).
  - La bodega de tránsito se considera activa cuando lo está alguna bodega de su sucursal.
  - El corte debe caer en un período abierto.
- **Convivencia con SOLIDO**.
  - Las cifras de SOLIDO van en `INV_LegacyFigures`, con `ImportLegacyFiguresCommand`
    (`Inventory.LegacyFigures.Import`) y la pantalla `/inventario/cifras-solido`. Guarda fecha, códigos
    tal como vienen, los Ids resueltos si existen, cantidad, valor y lote de importación.
  - Vistas: `legacy-comparison-kardex` y `legacy-comparison-valuation` (I1), y la conciliación
    `reconciliation` (I2). En la conciliación, las bodegas no activas que comparten cuentas entran con
    sus cifras de SOLIDO a la fecha de corte y **suman al valorizado del conjunto**, como en la
    activación (FR-090); se muestran aparte sólo para identificarlas (precisión 9 a la spec).
  - No hay sincronización automática con SOLIDO: SOLIDO sigue registrando las bodegas no activas.
- **Ensayo y salida** (Supuesto 1, FR-095).
  - El ensayo corre en una cooperativa de ensayo, fuera de producción, con la parametrización cargada
    desde las plantillas y un saldo inicial provisional. Allí los tipos pasan a una contabilidad de
    ensayo o quedan sin paso.
  - En producción, la salida es bodega por bodega, cuando el saldo de cada una queda cuadrado.
  - **Ninguna fecha queda fija en estos artefactos**: las define el dueño cuando la aplicación esté lista.

**Por qué**:
- Que un período cerrado sea un prefijo de fechas reduce la comprobación a un solo valor.
- El bloqueo compartido/exclusivo cierra la carrera entre cerrar y confirmar sin nada fuera de la
  transacción.
- Guardar el valorizado fijado por producto × bodega cubre FR-047, acelera SC-017 y deja verificarlo
  contra el kardex.
- La activación por bodega, con su comparación guardada, da la trazabilidad de FR-090 y SC-018.
- Contar las no activas con sus cifras de SOLIDO evita que la primera bodega que se activa muestre como
  diferencia el inventario de las que siguen en SOLIDO.

**Alternativas descartadas**:
- Comprobar el período sin bloquearlo: un documento entraría a un mes que se está cerrando.
- Reutilizar `ACC_AccountingPeriods`: viola FR-014, y el período de inventario es otro.
- Guardar el valorizado sólo por grupo y bodega: ni acelera ni permite verificar.
- Contabilizar el saldo inicial: lo contaría dos veces.
- Migrar los movimientos históricos de SOLIDO: fuera de alcance; se entra con saldos.
- Ensayar en la cooperativa de producción: una bodega no activa sólo admite su saldo inicial.

**Riesgos**:
- La activación necesita la matriz y la consulta de saldos (I2), y las cifras de SOLIDO que COOFLOPAL
  entregue en el formato de la plantilla.
- El conteo físico antes de la carga, y los movimientos entre el conteo y el corte, son trabajo
  operativo de la cooperativa (D-07).
- Un saldo inicial de 20.000 líneas en una bodega se parte en cinco documentos para no escalar el
  bloqueo, y la aprobación debe tratarlos juntos.
- La conciliación por conjunto de cuentas depende de que la contadora mapee las cuentas antes del
  ensayo.

**Spec**: FR-047, FR-081, FR-089 a FR-091, FR-095, US3 esc. 3 a 5, US4, SC-005, SC-016, SC-017, SC-018;
Supuestos 1 y 2; D-07.

## R20. Compras y cruce

**Decisión**:
- **Clases**, sobre el modelo común (R6):
  - I1: `PurchaseReceipt` (compra directa), `SupplierInvoice`, `SupplierNote` y `SupplierReturn`;
  - I4: `SupportDocument` y `SupportDocumentAdjustmentNote` quedan operables;
  - I5: `PurchaseRequest`, `PurchaseOrder`, `LandedCost` y la recepción contra orden.
- **Datos de la factura del proveedor**: `INV_SupplierInvoiceDetails`, 1:1 con la factura o nota del
  proveedor. Lleva prefijo, número, CUFE, fechas de emisión y vencimiento, forma de pago y si es
  electrónica, con UK filtrados entre no anuladas por (proveedor, prefijo, número) y por CUFE.
- **Vínculos**: `INV_DocumentLineLinks` enlaza orden → recepción, recepción → factura, recepción →
  devolución, factura → nota y recepción → costos adicionales, con la cantidad en unidad base. Lo
  pendiente se calcula.
- **Compra directa**: `ConfirmDirectPurchaseCommand`, en `/api/inventory/purchases/direct` y
  `/compras/compra-directa`. Confirma recepción y factura en una sola transacción: dos documentos de la
  misma cadena de modo de paso, para cumplir los 3 pasos de FR-019.
- **Dos vías (I1)**:
  - La cantidad facturada no puede superar lo recibido y aún no facturado.
  - Una diferencia de precio sólo ajusta el costo (`AjusteDeCostoReconocido`, partido entre existencia
    y vendido, por documento afectado), sin retención ni aprobación (pregunta E6).
  - La recepción entra al kardex a precio neto de descuentos no condicionados, más los impuestos que van
    al costo (R21).
  - `FacturaProveedorRegistrada` separa el IVA descontable del IVA al costo, para que la matriz cancele
    la cuenta puente de mercancía recibida por el mismo valor que cargó `CompraRecibida`.
- **Devolución a proveedor**: al costo de la línea de recepción enlazada, con la diferencia contra el
  promedio como ajuste. Si la compra se soportó con documento soporte, emite su nota de ajuste (I4).
- **Tres vías (I5)**: el motor puro `Domain/Inventory/Purchasing/CruceDeCompra` compara por línea lo
  ordenado, lo recibido y lo facturado, en cantidad y en precio.
  - Tolerancias: `Compras.Tolerancia*` y `Compras.ReglaDeTolerancia` (`AmbasCondiciones` por defecto).
  - Lo que excede queda retenido en `INV_PurchaseMatchLines` y pasa por el motor de aprobaciones, como
    tipo «Diferencia de cruce».
  - Aprobada, la diferencia ajusta el costo; rechazada, la factura vuelve a borrador.
  - Casos dorados: US13-1 a US13-3.
- **Costos adicionales (I5)**: la clase `LandedCost` referencia la factura del flete o el seguro (otra
  `SupplierInvoice`, de servicio, con su retención por transporte) y una o varias recepciones.
  - `Domain/Inventory/Costing/Prorrateo` reparte por valor, cantidad, peso, volumen o a mano, con suma
    exacta por residuo mayor y la diferencia visible (`INV_LandedCostAllocations`).
  - Antes de I5, el flete facturado aparte va al gasto; si viene en la misma factura, al costo de la
    recepción (pregunta E7).
- **Eventos RADIAN**: se registran desde I1 y se emiten desde I5 (R30).
- **Prellenado opcional** desde el XML de la factura electrónica del proveedor: se lee y no se guarda
  (pregunta E10).

**Por qué**:
- Recepción y factura son muchas a muchas (FR-050: «contra las recepciones»).
- Con vínculos por línea, el cruce, la devolución al costo de entrada y el cruce a tres vías reusan la
  misma estructura.
- La unicidad por proveedor, prefijo y número evita registrar dos veces la misma factura.
- Un solo motor de cruce sirve a las dos entregas, y la aprobación reutiliza FR-010.
- Separar el IVA descontable del IVA al costo sigue la NIC 2 párr. 11 (los impuestos no recuperables
  forman parte del costo) y evita que los mensajes se superpongan (FR-036).

**Alternativas descartadas**:
- Cabeceras propias por clase de compra.
- La factura como atributo de la recepción: no admite varias recepciones ni facturas parciales.
- Contadores de pendiente guardados.
- Cruce por encabezado: la spec lo pide por línea.
- Tolerancia sólo en porcentaje.
- Retener también en I1 (pregunta E6).
- Meter el flete a mano en el costo unitario: sólo como puente, si el dueño lo acepta.
- Prorratear en Contabilidad: el kardex quedaría sin el costo real.
- `INV_SupplierInvoices` como nombre (informe de compras): queda `INV_SupplierInvoiceDetails`.

**Riesgos**:
- Sin prorrateo hasta I5, el costo de los meses de ensayo y salida puede quedar subvaluado.
- En I1, una diferencia de precio ajusta el costo sin aprobación: un error de digitación en la factura
  pasa al promedio. El motivo y la auditoría lo dejan rastreable.
- La unicidad entre no anuladas exige que la anulación libere la combinación proveedor-prefijo-número.

**Spec**: FR-019, FR-036, FR-044, FR-046, FR-048 a FR-051, FR-075, US9, US13, SC-007, SC-008;
Supuesto 11.

## R21. Impuestos y retenciones

**Decisión**:
- **Catálogo en Core** (T22, I1):
  - `COR_TaxDefinitions`: `Code`, `Name`, `Kind` (`TaxKind`: Iva, Inc, ReteFuente, ReteIva, ReteIca, Ica,
    Other), `CalculationForm` (PercentOfBase; PercentOfTax, con `TaxedOnDefinitionId`, para ReteIVA;
    AmountPerUnit), `IsWithholding` y `DianTaxCode`.
  - `COR_TaxRates`: tarifa con vigencia. Lleva `Rate` (fracción, 9,6) o `AmountPerUnit`; concepto de
    retención; municipio DANE y CIIU, o `*`; base mínima en UVT o en pesos; condiciones **tipadas** sobre
    las partes; `AppliesTo`; `Priority`; y `LegalSource`.
    - Dos vigencias de la misma clave no se cruzan.
    - Una tarifa ya usada no se edita: se cierra y se crea otra.
  - `COR_WithholdingConcepts`.
  - Permisos `Core.Taxes.View/Manage`, pantalla `/maestros/impuestos`, rutas
    `/api/core/{taxes, tax-rates, withholding-concepts}` (`TaxesEndpoints.cs`, I1) y la plantilla
    `ImportTaxCatalogCommand` (`GET /api/core/taxes/template.xlsx`, `POST /api/core/taxes/import`).
    Una vigencia nueva es otra fila; `POST /tax-rates/{id}/close` la cierra (`validTo`, motivo) y
    `POST /tax-rates/{id}/review` quita `ReviewPending` con motivo. Errores: `Core.Tax.NotFound`,
    `Core.TaxRate.{NotFound, Overlaps, InEffect, Ambiguous}`, `Core.WithholdingConcept.NotFound` y
    `Catalogo.CodigoDuplicado`. El contrato está en `contracts/api.md`.
  - Semilla `TaxCatalogSeeder` (Order 81, `Data/impuestos-co.json`): IVA 19 %, 5 %, exento y excluido;
    INC; bolsas por unidad; ReteFuente por concepto en UVT; ReteIVA. ReteICA va sin semilla porque es
    municipal. Todo marcado «pendiente de validar por la contadora».
- **Motor puro**: `Domain/Taxes/MotorTributario`, en el molde de nómina.
  - Recibe:
    - fecha y perspectiva: en compras retiene la cooperativa; en ventas, el comprador agente retenedor;
    - líneas con base neta de descuentos no condicionados;
    - los perfiles de las dos partes (`PerfilTributario`), el municipio y la actividad;
    - la foto del catálogo (`TaxCatalogSnapshot`, que arma `LectorDeCatalogoTributario`);
    - la UVT de la fecha y los parámetros.
  - Devuelve:
    - impuestos por línea, redondeados por línea (el total es la suma, como exige UBL);
    - retenciones por documento y por (impuesto, concepto, municipio), cuando la base es **igual o
      mayor** que el mínimo convertido a pesos (`ConversionUvt`, `Tributario.RedondeoUvtAPesos`);
    - el tratamiento de cada renglón: `Generated`, `Deductible`, `AddedToCost`, `WithholdingApplied` o
      `WithholdingSuffered`;
    - una `Explanation` por paso.
  - Notas y devoluciones usan la foto del original y no vuelven a probar el mínimo (pregunta E9).
  - `INV_DocumentTaxLines` guarda la foto.
  - Casos dorados en `Domain.Tests/Taxes/Casos`: base exacta en el umbral, gran contribuyente o no,
    autorretenedor, declarante o no, ReteIVA sobre el IVA, ReteICA con la fila general, bolsas por
    unidad, exento contra excluido y nota parcial.
- **IVA descontable**: `Domain/Taxes/IvaDescontable.Determinar`, en el orden de FR-044:
  1. si la cooperativa no es responsable de IVA a la fecha (`Tributario.ResponsableIva`), todo IVA va al
     costo;
  2. si el tipo de compra está marcado `VatNonDeductible` (consumo interno, actividades excluidas), va al
     costo;
  3. si el producto se vende excluido, va al costo.

  El INC siempre va al costo. El prorrateo del IVA en costos comunes (ET art. 490) queda en
  Contabilidad.
- **UVT** (T23): un solo lector, `Application/Common/Taxation/LectorDeUvt` (`IValorUvt`).
  - Lee el código `UVT` de `PAY_LegalParameters` a la fecha.
  - Si no hay vigencia, falla visible: «No hay UVT vigente al {fecha}; regístrela en Parámetros
    legales».
  - Lo vigila `LaUvtSeLeeEnUnSoloSitio`. No se crea otra UVT.
- **Perfil tributario** (T24). Es una enmienda de la 008, en I1:
  - seis marcas nuevas en `COR_People`: `IsVatResponsible`, `IsSelfWithholder`, `IsVatWithholdingAgent`,
    `IsSimpleTaxRegime`, `IsIncomeTaxFiler` e `IsObligatedToInvoice`;
  - junto con las que ya existen (`IsLargeContributor`, `WithholdingExempt`, `IcaWithholdingExempt`,
    `CiiuCode`), entran a `PersonInput`, a su validador y a una sección «Datos tributarios» de
    `PersonaDialog`;
  - las responsabilidades DIAN (O-13, O-15, O-23, O-47, R-99-PN), el tributo y el tipo de identificación
    DIAN se **derivan** en `CatalogoDian`. El `IdType` heredado se traduce por tabla.

  El perfil de la cooperativa son los parámetros `TAX` con vigencia: `Tributario.ResponsableIva`,
  `.GranContribuyente`, `.AgenteRetencionIva`, `.Autorretenedor` y `.RegimenTributarioEspecial`.
- **Municipio**:
  - `COR_Cities.DaneCode`, con la semilla DIVIPOLA (`DivipolaSeeder`, Order 82);
  - `COR_Branches.MunicipalityDaneCode`, que escriben las rutas existentes de la 009
    (`POST/PUT /api/accounting/branches`, campo `municipalityDaneCode`, validado contra
    `COR_Cities.DaneCode`: 422 `Branch.MunicipalityUnknown`) y que la carga de plantillas exige antes;
  - el documento de compra lleva `OperationMunicipalityDaneCode`, propuesto desde la sucursal de la
    bodega que recibe.

  ReteICA se busca por (municipio, CIIU del proveedor, concepto) y cae a la fila `*` del municipio. El
  `IcaRate` por persona no se usa.
- **Total a pagar** (T26):
  - `Total` = base − descuentos + impuestos;
  - `AmountDue` = `Total` − retenciones sufridas (`WithholdingSuffered`).

  La igualdad de FR-056 se evalúa contra `AmountDue`. La retención viaja en `VentaFacturada` como una
  línea de retención (rol `Retencion`), nunca como medio de pago.
- **Impuestos por unidad**: su cuenta va sin «exige base gravable»; no se enmienda la regla 10 de la 009
  (pregunta E11).

**Por qué**:
- El Principio V pone los datos fiscales en `COR`, y FR-013 admite «del módulo (o de Core)».
- El precedente D-42 de la 010 puso los formatos bancarios en Core, y Tesorería, Cartera
  (`CreditLineParameter.VatPercentage`) y Activos necesitarán el mismo catálogo.
- Las condiciones tipadas, sin fórmulas libres, siguen el patrón de nómina y se importan con errores
  por fila y columna.
- Redondear por línea evita descuadres contra la DIAN.
- La foto del original evita que un cambio de tarifa altere la nota de un documento viejo.
- Un código DANE mal digitado no encontraría tarifa y dejaría de retener en silencio (Principio IX). Por
  eso el catálogo DIVIPOLA.
- Una sola UVT evita dos valores que se desalineen, y no toca el motor de nómina en producción.

**Alternativas descartadas**:
- Tablas `INV_` para el catálogo: moverlas después sería destructivo, y Tesorería tendría que leer
  Inventario.
- Reutilizar `ChartOfAccount.TaxKind` y `AccountTaxRates`: viola FR-014, y una tarifa por cuenta no
  expresa concepto, municipio ni base mínima.
- `Inventory.Taxes.*` y `/inventario/impuestos` (informes de seguridad y ventas): el catálogo es de Core.
- Expresiones libres, o condiciones en un JSON.
- Otra UVT en el catálogo tributario, o mover la UVT a Core ya.
- Una tabla `INV_SupplierTaxProfiles`: viola el Principio V.
- Reutilizar `TaxRegime` y `SourceWithholding` heredados: son ambiguos.
- El DANE como texto, sin catálogo.
- La tarifa ICA por persona: la fija el municipio.
- Tratar la retención sufrida como un medio de pago.
- Enmendar la regla 10 de la 009 para los impuestos por unidad.

**Riesgos**:
- Sin el mapeo de `TaxRegime`, `SourceWithholding` e `IcaType` de SOLIDO, las retenciones del primer día
  pueden salir mal (pregunta E3).
- La DIVIPOLA debe estar sembrada antes de I1, y las ciudades existentes conciliadas por `LegacyCode`
  (pregunta E4).
- ReteFuente con tarifas distintas según el proveedor declare o no, y cada tarifa de ICA, exigen una
  auxiliar por tarifa (C8, pregunta E11).
- La UVT la mantiene quien tiene el permiso de parámetros legales de nómina, también en una cooperativa
  que no usa nómina (pregunta E2).
- Redondear la base de UVT a pesos puede cambiar el resultado justo en el umbral.
- Si la contadora no valida la semilla tributaria, el cálculo puede salir mal desde el primer día
  (pregunta A8).

**Spec**: FR-011, FR-013, FR-014, FR-017, FR-027, FR-044, FR-050, FR-056, FR-063, FR-082, US9 esc. 2,
SC-007; C8; Supuesto 17; D-06. Normas, sin cotejar en esta fase: NIC 2 párr. 11; ET arts. 490 y 632; Ley
2277 de 2022 (ICUI e IBUA); la UVT 2026 de la Res. DIAN 000238 del 15-12-2025, que siembra nómina.

## R22. Personas: copia fiscal, consumidor final y autorización de datos

**Decisión**:
- **Copia fiscal** (T52): `INV_DocumentPartySnapshots` (`DocumentPartySnapshot`, sólo inserción).
  - La versión 1 se escribe al confirmar: nombre o razón social, documento, dirección, régimen y lo que
    pida el canónico DIAN.
  - El caso a de FR-066 agrega la versión siguiente, con antes y después auditados; también queda en la
    versión del documento electrónico.
  - Reimpresiones y representación gráfica usan la copia vigente (la de mayor `Version`) o el archivo
    firmado.
  - `INV_Documents` no copia la identificación en columnas.
- **Consumidor final**: `ConsumidorFinalSeeder` (Order 87, I3) crea la persona genérica del maestro, con
  la identificación que indique `CatalogoDian`. La Res. 202 de 2025 redujo los datos obligatorios del
  adquirente y admite «consumidor final» (R30). Una venta a crédito nunca es a consumidor final (R26).
- **Autorización de datos** (T46):
  - `PersonaDialog` sigue siendo el único que escribe personas, con un `[Parameter] CapturarAutorizacion`
    y un modo compacto para el POS y Compras.
  - `GET /api/compliance/habeas-data/policies/current`, con `Core.People.Create`.
  - `AutorizacionAlCrear { Decision, PolicyVersionPublicId, Channel }`, opcional en `CreatePersonCommand`
    y en los compuestos `with-person`, se escribe en el mismo `SaveChanges`. Al cajero no se le exige
    `Compliance.HabeasData.RecordConsent`.
  - Una acción nueva, `Declined` (columna de texto, sin migración), que los lectores tratan como sin
    autorización.
  - `IAutorizacionDeDatos.VigenteAsync(personPublicId)` sirve a promociones y contacto comercial.
  - Todo por `PublicId`.
  - Sin política publicada, se permite el alta dejando «sin política vigente», con la alerta
    `Personas.SinPoliticaDeDatos` (R16) a quien tiene `Compliance.HabeasData.RecordConsent`
    (pregunta C8).
- **Exportación**: exportar datos personales exige `Inventory.Reports.ExportPersonalData` (R31).

**Por qué**:
- FR-011 exige una copia inmutable de lo emitido, y una autorización registrada en el registro de
  consentimientos que ya existe.
- Hacerlo dentro del alta evita que un cajero necesite permisos de Cumplimiento, y respeta el escritor
  único de la 008 (Principio V, D-06).
- La copia no es una duplicación de las que prohíbe el Principio V: un documento no es una tabla de rol.

**Alternativas descartadas**:
- La copia fiscal en columnas de `INV_Documents`: no admite la versión del caso a.
- Llamar `POST /consents/accept` después de crear: son dos transacciones, exige `RecordConsent` y usa
  un Id interno.
- Una bandera `AutorizaDatos` en la persona: pierde la versión y el historial.
- Un diálogo propio del POS: viola el escritor único.

**Riesgos**:
- El cambio a `CreatePersonCommand` y `PersonaDialog` toca la regla de escritor único:
  `LaPersonaSeEscribeEnUnSoloSitio` se amplía, no se relaja.
- `HabeasDataModule` sigue exponiendo `personId:int` (Principio VI). El módulo no lo usa.
- La fase 2 de la 008 (alta de clientes y proveedores desde el diálogo) es una dependencia (D-06).

**Spec**: FR-011, FR-059, FR-066 (a), FR-087, US5 esc. 6, SC-012; D-06; Ley 1581 de 2012; Principios V
y VI.

## R23. Ventas, precios, descuentos y promociones

**Decisión**:
- **Documentos de venta**:
  - I3: `SalesInvoice`, `PosEquivalentDocument`, `CreditNote` y `PosAdjustmentNote`. Sólo confirman si
    `GuardiaDeEmisionFiscal` responde `Electronic`, es decir, con I4 en una cooperativa obligada.
  - I3: `NonElectronicSalesReceipt` y `NonElectronicSalesNote`, sólo con `Dian.ObligadaAFacturar`
    apagado (Supuesto 20).
  - Las devoluciones de cliente entran al costo con que salieron.
  - I6: `SalesQuote`, `SalesOrder` (reserva en `INV_Reservations`, que `ProgramadorDeTareas` libera a
    los `Ventas.ReservaDiasVencimiento`), `Shipment`, `SalesInvoiceFromShipments` y `DebitNote`.
  - Rutas: `/api/inventory/sales/{documents, invoices, credit-notes}` (I3) y
    `/api/inventory/sales/{quotes, orders, shipments, debit-notes}` (I6).
  - El retiro gravado del consumo interno se habilita en I3: la base es el precio de la lista general y
    el IVA el del producto; su tipo lo marca `IsTaxableWithdrawal`.
- **Listas de precios** (T51):
  - `INV_PriceLists`: ámbito con dimensiones anulables (persona, segmento, canal, sucursal),
    `IncludesTaxes`, vigencia y `ScopeKey` (texto normalizado del ámbito, que da una unicidad portable y
    sin vigencias cruzadas).
  - `INV_PriceListItems`: (lista, producto, unidad) → `Price` (18,2).
  - Resolución pura en `Domain/Sales/Pricing/ResolutorDeListaDePrecios`, con casos en
    `Domain.Tests/Sales/Pricing/Casos`:
    - candidatas: las listas vigentes cuyas dimensiones no nulas coinciden;
    - primero las que coinciden en más dimensiones;
    - en empate: cliente > segmento > canal > sucursal;
    - la general, al final;
    - con **respaldo por producto** en la siguiente lista aplicable (pregunta F8).
  - La línea guarda la lista usada (`PriceListId`, `ListPrice`).
  - El segmento es `Associate.AssociateClass`, validado contra los valores existentes (pregunta F10).
  - Vender bajo el costo alerta o bloquea según `Ventas.BajoCosto` (alerta `Inventario.VentaBajoCosto`).
  - `ResolvePriceQuery`, en `/api/inventory/prices/resolve`.
- **Descuentos**:
  - Topes por rol en `INV_DiscountCaps`, por línea y por documento, con vigencia. El tope de un usuario
    es el mayor de sus roles; sin fila, es 0.
  - Cambiar el precio de lista cuenta como un descuento sobre ese precio.
  - Sobre el tope se pide aprobación (R13), ligada a la línea y al valor por `ContentSha256`.
  - `INV_DocumentLineDiscounts` guarda `Source` (Manual | Promotion), porcentaje, valor, aprobación y
    `PromotionId`.
  - El descuento por total se prorratea a las líneas, para bajar la base gravable, con el ajuste de
    redondeo visible.
  - Permisos `Inventory.Discounts.Authorize` e `Inventory.DiscountCaps.Manage`.
- **Promociones** (I6):
  - Tablas `INV_Promotions`, `INV_PromotionScopes` e `INV_PromotionTiers`, y `PromotionKind` { Percent,
    Amount, BuyNPayM, QuantityPrice, BundlePrice }.
  - `Domain/Sales/Promotions/MotorDePromociones`, puro, trabaja sobre las líneas ya precificadas.
  - Se aplican como descuentos no condicionados, nunca como líneas a precio cero: en un «3×2», las tres
    unidades llevan un descuento igual al precio de una.
  - No son acumulables, y gana la de mayor descuento. El descuento manual no se suma (pregunta F9).
  - El esquema de descuentos por línea existe desde I3, para no migrar en I6.
- **Vendedor** en cada venta (`SalespersonId`, una persona con rol de vendedor). El margen por vendedor
  es un informe de I6.

**Por qué**:
- Es la regla literal de FR-053 y US5 esc. 7, y la función pura con casos dorados sigue el patrón de
  nómina.
- `ScopeKey` hace portable la unicidad: en un índice único, SQL Server trata los `NULL` como iguales y
  PostgreSQL como distintos.
- El respaldo por producto evita copiar toda la lista general en cada lista especial.
- Los topes por rol y la aprobación con identidad propia cumplen FR-054 y el Supuesto «Aprobaciones».
- Las promociones como descuentos no condicionados reducen la base gravable (Supuesto 13).

**Alternativas descartadas**:
- Una prioridad numérica escrita a mano: contradice la regla fija.
- Sin respaldo por producto.
- Unicidad sobre las columnas anulables.
- Tope por usuario.
- Pedir la contraseña del supervisor.
- Líneas a precio cero.
- Construir el cálculo de promociones en I3: fuera del alcance mínimo de la salida.
- Prefijo `SLS_` para ventas (T2): todo es `INV_`.

**Riesgos**:
- `AssociateClass` es texto libre de 4 caracteres: una lista por segmento puede no aplicar por
  diferencias de escritura.
- La aprobación sobre el tope, sin empuje en tiempo real, depende del sondeo.
- Al cambiar el cliente de una venta en curso se recalculan los precios, y el cajero tiene que verlo.
- El retiro gravado depende de que exista una lista general vigente.

**Spec**: FR-037, FR-052 a FR-055, FR-057, FR-063, US5 esc. 3 y 7, US14; Supuestos 13, 14 y 20.

## R24. Punto de venta

**Decisión**:
- **El borrador vive en el servidor** (T50). La venta del POS es el documento en `Draft`, guardado
  desde la primera lectura y ligado a `CashSessionId`. No consume número.
  - Operaciones, en `/api/inventory/pos/`:
    - `lookup`;
    - `drafts`: crear el borrador;
    - `drafts/{id}/lines`: agregar con el código leído y la cantidad. Devuelve la línea precificada, la
      lista aplicada, el impuesto, la existencia y la alerta de venta bajo costo;
    - `lines/{lineId}`: cambiar la cantidad o el descuento, o quitar la línea (auditado);
    - `suspend` (con un rótulo), `resume` y `discard` (queda registrado);
    - `checkout`: con los pagos y la clave de idempotencia.
  - Reimpresión en `/api/inventory/documents/{id}/reprint` (`Inventory.Documents.Reprint`), con la marca
    «COPIA» y auditada.
  - Comandos: `CreatePosDraftCommand`, `AddPosLineCommand`, `UpdatePosLineCommand`,
    `RemovePosLineCommand`, `SuspendPosDraftCommand`, `ResumePosDraftCommand`, `DiscardPosDraftCommand`,
    `CheckoutPosDraftCommand` y `ReprintDocumentCommand`, todos con `IOperacionDePuntoDeVenta`.
  - Se auditan las acciones de riesgo (quitar una línea, descuento, precio manual, suspender, recuperar,
    descartar, cobrar, reimprimir), no cada lectura (pregunta F13).
  - No se cierra una sesión con ventas suspendidas (`Inventory.CashSession.HasOpenDrafts`). Sin una
    sesión abierta no se agregan líneas ni se cobra (`Inventory.CashSession.NotOpen`).
  - Cobrar pasa por el flujo canónico de R6. Si el documento es electrónico, intenta la emisión en línea
    hasta `Dian.EsperaMaximaPosSegundos` (R28).
- **Pantalla** (T45): `Pages/Pos/PuntoDeVenta.razor` (`/pos`), con `PosLayout`: pantalla completa, sin
  barra lateral, con `AvisoDeVencimientoDeSesion` y `RegistroDeAccesos`.
  - Una tabla liviana, sin SfGrid.
  - Un campo de lectura siempre enfocado: Enter resuelve el código exacto; «3*» multiplica la cantidad;
    leer dos veces el mismo producto suma; si el código no existe, abre la búsqueda.
  - Atajos visibles:
    - F2 buscar, F3 cantidad, F4 cliente, F6 vendedor, F7 descuento;
    - F8 suspender, F9 recuperar, F10 cobrar;
    - Supr quitar línea, Esc volver a la lectura;
    - Ctrl+Alt+M, C, R y D: movimiento de caja, cierre, reimpresión y devolución.

    Se evitan F5, F11, F12 y Ctrl+W.
  - En el cobro, la tecla rápida de cada medio agrega un pago por lo que falta, y la pantalla muestra lo
    que falta, lo que sobra y las vueltas.
  - Componentes en `Components/Pos/*`: líneas, cobro, atajos, cliente (con `PersonSearchPicker` compacto,
    que abre `PersonaDialog`), aprobación, arqueo, denominaciones, lotes de datáfono y tirillas.
  - `wwwroot/js/pos.js`, cargado con `@Assets` y en `App/wwwroot/index.html`:
    - intercepta las teclas reservadas con `preventDefault` mientras el POS está montado;
    - detecta las ráfagas del lector (caracteres a menos de 30 ms que terminan en Enter);
    - imprime en un iframe oculto.
  - La caja del equipo se elige una vez y se recuerda en `localStorage`, con try/catch. Decide el
    servidor.
- **Impresión**:
  - La tirilla de 58 u 80 mm es un componente HTML con `@media print`, impreso por `pos.js`.
  - La carta sale de QuestPDF: `SalesDocumentReport` para el comprobante no electrónico (I3) y
    `RepresentacionGraficaReport` para los electrónicos (I4).
  - En la app, `IImpresionDeDocumentos` tiene una implementación nativa, porque el WebView de Android no
    admite `window.print`.
  - Recomendación operativa: PC en modo kiosco de Chrome o Edge, con `--kiosk-printing` (pregunta A6).

**Por qué**:
- Quitar líneas sin cobrar es el fraude clásico del mostrador. Para auditarlo de verdad, cada retiro
  tiene que llegar al servidor en el momento.
- Un borrador en el servidor da gratis suspender y recuperar, recuperarse de una caída del navegador y
  retomar la venta en otra caja.
- La lectura necesita de todos modos una llamada para precificar: guardar la línea no agrega viajes.
- `InteractiveWebAssembly` hace locales el teclado y el render.
- Una página en Shared cumple FR-094: web y app.

**Alternativas descartadas**:
- Un carrito sólo en el navegador: las líneas quitadas se pierden, y suspender quedaría en un
  almacenamiento local que no es confiable.
- Auditar cada línea agregada: unos 50.000 eventos al día sin valor de control.
- SfGrid para las líneas: le quita el foco al campo de lectura.
- Una aplicación POS aparte o nativa: rompe FR-094 y duplica el código.
- `@onkeydown:preventDefault` estático: no se puede aplicar sólo a algunas teclas.
- La tirilla en PDF: lenta y sin visor en Android.
- ESC/POS directo: exige controladores.
- Rutas `/api/sales/pos/*` (informe de ventas): van en `/api/inventory/pos` (T3).

**Riesgos**:
- La sesión de 30 minutos de inactividad y el segundo factor obligatorio expulsan al cajero en la hora
  valle. Mitigación: passkeys para los cajeros. Relajar la política exige una decisión del dueño.
- Sin POS fuera de línea, una caída del ERP o de la red detiene la caja, y la contingencia DIAN no lo
  cubre (pregunta A10).
- Sin modo kiosco, cada tirilla abre el diálogo de impresión.
- La app MAUI no arranca estas pantallas hasta que registre los clientes que le faltan (R32).
- SC-004 (30 s con 6 lecturas y pago mixto) suma la validación previa, el cerrojo, la numeración y la
  espera DIAN: hay que medirlo.

**Spec**: FR-005, FR-007, FR-019, FR-020, FR-038, FR-058, FR-059, FR-094, US5, SC-004, SC-008, SC-019;
Supuesto «Hardware».

## R25. Medios de pago y cierre de caja

**Decisión**:
- **Catálogo en Core** (T25, I3).
  - `COR_PaymentMeans`:
    - código, nombre, orden y tecla rápida;
    - `Class` fija (`PaymentMeansClass`: Cash, CreditCard, DebitCard, AssociateCredit, CustomerCredit,
      BankDeposit, Transfer, Voucher, Check, Other);
    - para tarjetas, la red y el adquirente; para consignación y transferencia, el banco destino y la
      cuenta **como dato**;
    - reglas de captura: `RequiresReference`, `ReferenceKind` y largo; vueltas sólo en efectivo; pago
      parcial; `UniqueReference` (por defecto en bonos);
    - `CountMethod` (`PhysicalCount`, `VoucherTotal`, `ByReference`, `None`), con defecto según la clase;
    - `ToleranceAmount` y la comisión esperada, informativa (FR-101). Cambiarlas exige motivo, y se
      copian a cada línea de arqueo o pago que las usa (R12);
    - `DianPaymentMeansCode` como dato, sembrado con una sugerencia por clase (10, 48, 49, 42, 47, 20,
      71…) que valida la contadora;
    - vigencia. Un medio con pagos no se borra: se inactiva.
  - `COR_CardNetworks` (con `CardKind`), `COR_CardAcquirers` (con `PersonId` para el tercero),
    `COR_CardTerminals` («Datáfonos de cobro», que no son `DEB_PosTerminals`) y `COR_CashDenominations`
    (sembradas, con `Kind` = `CashDenominationKind`: `Bill` 1 · `Coin` 2).
  - Permisos `Core.PaymentMeans.View/Manage`; rutas `/api/core/{payment-means, card-networks,
    card-acquirers, card-terminals, cash-denominations}`; pantalla `/maestros/medios-de-pago`, con
    pestañas.
  - Semillas: `CashDenominationsSeeder` (85) y `DefaultPaymentMeansSeeder` (86, que sólo crea
    `EFECTIVO`).
  - Dónde se ofrece cada medio vive en el módulo: `INV_PaymentMeansPointsOfSale`,
    `INV_PaymentMeansChannels` e `INV_PaymentMeansDocumentTypes`, con la opción «todos» en el medio.
  - La regla pura `Domain/Sales/Payments/DisponibilidadDeMedio` decide si se ofrece: activo y vigente;
    punto, canal y tipo; el crédito sólo con cliente identificado y, si es de asociado, con el asociado
    activo.
- **Pagos**: `INV_DocumentPayments`.
  - Lleva medio, valor, `Direction` (Received | Refunded), entregado y vueltas (sólo en efectivo),
    referencia normalizada, autorización, datáfono, lote, copias del medio, `Last4` (nunca el número de
    la tarjeta), `CashSessionId`, condiciones de crédito y `PendingValidation`.
  - `ValidadorDePagos` exige:
    - que lo aplicado sume exactamente `AmountDue` (si no, `Payments.TotalMismatch`);
    - vueltas sólo donde se admiten;
    - la referencia que pida el medio;
    - que el mismo medio pueda repetirse con referencias distintas.
  - **Bonos**: `INV_VoucherRedemptions`, con UK filtrado `(PaymentMeansId, NormalizedNumber)` y
    `Status = Active`. Un número repetido se rechaza con `Payments.VoucherAlreadyUsed`, nombrando la venta
    que lo usó. Anular o devolver con reintegro agrega la liberación; nunca se borra.
  - La devolución de dinero sale por el mismo medio de la venta. Otro medio exige
    `Inventory.Sales.RefundOtherMeans` y queda auditado.
- **Contabilización automática**: cada pago viaja en `VentaFacturada` o `NotaCreditoEmitida` con su
  medio, y la matriz asigna la cuenta por `PaymentMeansCode` (y, opcionalmente, por `PointOfSaleCode`):
  - la caja del punto, para el efectivo;
  - la cuenta por cobrar a la red o al adquirente, con el tercero = la persona del adquirente;
  - el banco, para consignaciones y transferencias;
  - la cartera o la cuenta por cobrar provisional, para los créditos;
  - la cuenta del bono.

  Un medio sin cuenta vigente detiene la validación previa y aparece en la completitud (SC-024).
- **Punto, caja y sesión** (T50):
  - `INV_PointsOfSale`: sucursal, canal, `PosEnabled` y bodega por defecto;
  - `INV_CashRegisters`: bodega e impresión, con sus tipos de documento por rol
    (`INV_CashRegisterDocumentTypes`, UK `(CashRegisterId, Role)`; `CashRegisterDocumentRole`:
    `PosSale`, `InvoiceOnRequest`, `PosAdjustmentNote`, `InvoiceCreditNote`, `PosSaleContingency`,
    `InvoiceContingency`). Las notas no electrónicas van en `PosAdjustmentNote`. `PosSaleContingency`
    exige una resolución con `BacksUpKind = PosEquivalent` e `InvoiceContingency`, una con
    `BacksUpKind = Invoice`: así una caja que emite documento equivalente y factura a pedido tiene su
    nota y su contingencia para cada uno (FR-058, FR-067);
  - `INV_CashSessions`: caja, cajero (`SEC_Users.Id` más copia de la persona), `OperatingDate` (fecha
    local de apertura), base y estado. Dos UK filtrados con `Status = Open`: uno por caja y otro por
    cajero (`Caja.UnaSesionPorCajero`).
  - La base sigue `Caja.BaseModo`: con `FondoFijo` (el defecto) la sesión hereda la base; con base del
    día, o si la base cambia, se registra un ingreso de base desde la caja fuerte.
  - Todo pago cuyo medio se arquea pertenece a una sesión abierta del usuario, también el cobro de
    oficina (pregunta F14). Una devolución en efectivo sale de la sesión donde se devuelve.
- **Movimientos de caja**: clase `CashMovement`, con `INV_CashMovementDetails`, en
  `/api/inventory/cash-movements`.
  - Tipos: retiro a caja fuerte; retiro a otra caja (un solo documento, que resta en el origen y suma en
    la sesión abierta del destino); retiro para consignar; ingreso de base; y **reclasificación entre
    medios**, que corrige un pago registrado con el medio equivocado sin tocar la venta.
  - Siguen la política de aprobación de su tipo.
  - Emiten `MovimientoDeCajaRegistrado`.
  - Imprimen un comprobante con firmas (`CashMovementReceiptReport`).
- **Arqueo y cierre de la sesión**. `CloseCashSessionCommand` es inmediato: la caja puede abrir otra
  sesión enseguida.
  - La función pura `CalculadoraDeEsperado` calcula el esperado por medio: base (sólo efectivo) + ventas
    − devoluciones ± movimientos confirmados ± reclasificaciones.
  - `EvaluadorDeArqueo` lo compara con lo contado (`INV_CashCounts`, `INV_CashCountLines`,
    `INV_CashCountDenominations`), según el método del medio:
    - efectivo: por denominaciones o por total;
    - tarjetas: por total de comprobantes o por lote de cada datáfono (`INV_CashCountTerminalBatches`);
    - consignaciones, transferencias y bonos: por referencia (`INV_CashCountReferenceChecks`);
    - créditos: sin arqueo.
  - El arqueo ciego es opcional (`Caja.ArqueoCiego`, apagado por defecto).
  - Una diferencia distinta de cero crea el documento `CashCountDifference` (`INV_CashDocumentLines`):
    - dentro de la tolerancia del medio: pide motivo, sin aprobación;
    - por encima: pide los niveles de aprobación, y nunca aprueba el cajero.
  - Al confirmarse, emite **un solo** `DiferenciaDeArqueoAprobada`, con una línea por medio: signo,
    valor y tratamiento (`CashDifferenceTreatment`, según `Caja.TratamientoFaltante`: `Gasto` por defecto,
    o `CargoAlCajero`, que exige `SEC_Users.PersonId`). El sobrante va a su cuenta. **Incluye las
    diferencias aceptadas dentro de la tolerancia.**
  - Cerrar la sesión crea además el lote `CashSessionClose` para los tipos por lotes con ese disparador.
  - Informe `CashCountReport`, para firma.
- **Cierre del día**: `ExecuteDayCloseCommand`, con `INV_DayCloses` (UK `(PointOfSaleId, OperatingDate)`)
  e `INV_DayCloseLines` por medio, adquirente y datáfono.
  - Exige que todas las sesiones del día estén cerradas.
  - Después, no se abren sesiones con esa fecha en ese punto.
  - No emite mensajes.
  - Se reabre con `Inventory.DayClose.Reopen` y motivo (pregunta F12).
- **Informes** (I3): `sales-by-session`, `sales-by-register`, `sales-by-payment-means`, `cash-session`,
  `day-close`, `card-payments`, `cash-movements`, `cash-differences` y `voucher-redemptions`. La
  conciliación de lo que abonan los adquirentes es de Tesorería (FR-101); el módulo la deja facilitada.

**Por qué**:
- La clase es lo único que el código conoce: vueltas, crédito, forma de pago DIAN y arqueo por defecto.
  Lo demás es dato, como pidió el dueño.
- Core, porque Cartera y Tesorería van a reutilizar el catálogo (en SOLIDO, `sys_forpago` ya era común),
  y porque la matriz lo referencia sin leer Inventario.
- N pagos por documento reemplazan las columnas fijas de SOLIDO, y las copias protegen la historia si el
  medio cambia.
- `Last4` y la autorización bastan para cotejar con la red sin entrar al alcance de PCI-DSS.
- El UK filtrado de los bonos garantiza la unicidad bajo concurrencia sin bloquear filas.
- Separar el punto (configuración) de la sesión (estado del día) corrige lo que SOLIDO mezclaba en
  `inv_puntos`, y la base de datos garantiza una sesión por caja.
- Cerrar sin esperar la aprobación evita dejar la caja bloqueada.
- Contabilizar también lo aceptado dentro de la tolerancia es lo único que deja cuadrada la caja del
  punto.

**Alternativas descartadas**:
- El catálogo dentro del módulo: la matriz leería Inventario, y Cartera y Tesorería lo duplicarían.
- Reutilizar `COR_PaymentMethods`: es justo el desglose por comprobante con columnas fijas que el dueño
  pidió abandonar.
- El nombre `PaymentMethod`: choca con el heredado.
- Un medio de tarjeta genérico con la red elegida en cada pago: la matriz necesita una cuenta por red o
  adquirente.
- Clases configurables.
- Reutilizar `DEB_PosTerminals`.
- Columnas por clase, como SOLIDO.
- Los pagos como documento aparte: parte la atomicidad venta-pago.
- Guardar la cuenta contable en el medio: es el error del módulo actual.
- Un mapa fijo de códigos DIAN en el código.
- Seguir con `INV_SalesPoints` y su PK (punto, estado, fecha).
- Una sesión por punto.
- Editar el medio de un pago confirmado: viola FR-005.
- Bloquear la caja hasta la aprobación.
- Un mensaje por medio.
- El cierre del día como un informe calculado, sin entidad.
- Tablas `SLS_*` e `INV_Shifts` (T2).

**Riesgos**:
- El faltante a cargo del cajero exige vincular `SEC_Users.PersonId` en la base de cada cooperativa, y
  hoy es opcional (pregunta F4).
- El cierre de una sesión con 300 ventas y 6 medios tiene que caber en el tiempo de SC-025: el esperado
  se calcula en Domain, y la consulta agrega por sesión.
- Los códigos DIAN sembrados para los medios de pago deben validarse contra el anexo vigente (pregunta
  A8).
- La reclasificación entre medios es un documento nuevo, y la contadora tiene que mapearla (roles
  `MedioDePago` y `CajaDestino`).

**Spec**: FR-010, FR-056, FR-058, FR-069, FR-096 a FR-101, US5 esc. 1, 2, 4, 9 y 10, SC-004, SC-019, SC-024,
SC-025; D-42 de la 010 (precedente).

## R26. Crédito provisional y frontera con Cartera

**Decisión** (T32):
- **Mensajes a Cartera**. Toda venta con un pago de clase `AssociateCredit` o `CustomerCredit`, y sus
  notas, devoluciones y anulaciones (incluidos los casos b y c de FR-066), emiten
  `VentaACreditoRegistrada` o `AjusteDeVentaACredito`.
  - La entrega va con `Destination = Lending`, `Mode = Always` y `Status = Pending`, y con dependencias
    hacia el original, en el `SaveChanges` de la venta.
  - El contrato v1 de `VentaACreditoRegistrada` lleva:
    - persona y tipo de tercero;
    - el pago de crédito, su medio y su valor;
    - plazo, cuotas, periodicidad y vencimientos (la factura electrónica los necesita);
    - la línea sugerida;
    - `PendingValidation` y `Origin` (`ProvisionalCredit`, `LendingNoResponse`, `Validated`);
    - la aprobación;
    - `AccountsReceivableRecordedBy`, sellado.
  - El de `AjusteDeVentaACredito` lleva `AdjustmentClass` { CreditNote, DebitNote, Return, Voiding,
    VoidingByDianRejection, Replacement }, el valor con signo y `OriginalMessageId`.
  - `PersonPublicId` va en el mensaje, por si D-02 exige orden por persona.
- **Mientras IC esté pendiente**. Si no hay un `IDestinoDeMensajes` «Lending» registrado, **o** si
  `Cartera.IntegracionHabilitadaDesde` está vacío, el despachador salta esas entregas, sin contar
  intentos ni alertar. La bandeja las muestra como «Pendiente — destino aún no disponible (IC)».
- **Frontera**: `Application/Common/Integration/Lending/IConsultasDeCartera`, con
  `EstadoCrediticioAsync` y `EstadoDeValidacionAsync`, y tiempo máximo `Cartera.ConsultaSegundos`.
  - Mientras IC esté pendiente, la implementa `CarteraNoHabilitada`, que responde «no habilitado» y
    activa el flujo provisional.
  - Inventario nunca referencia `Domain.Entities.Lending` ni sus DbSet
    (`InventarioNoConoceContabilidadNiCartera`).
- **Flujo provisional** (Clarifications, F2):
  1. La persona debe estar identificada (no consumidor final) y activa en el maestro: un asociado, con
     `IsAssociate` y sin retiro; un cliente, con `IsCustomer`.
  2. La venta se aprueba con el motor, por alguien con monto suficiente, y nunca el cajero: es la
     política «permitir con aprobación» de FR-061. La solicitud lleva `Subject = ProvisionalCredit` y
     `SourceType = DocumentPayment`; el monto suficiente es el límite de `Inventory.Sales.SellOnCredit`
     del aprobador (R14).
  3. El pago queda `PendingValidation`.
  4. `VentaFacturada` lleva el pago de crédito por su medio, y la matriz lo envía a una cuenta que exige
     tercero y documento cruce `FV`. Así, la vista `pending-documents` de la E2 sirve de cartera
     provisional por persona.
  5. `Cartera.CuentaPorCobrarRegistradaPor = Contabilidad` queda sellado; mientras IC esté pendiente,
     ese valor es forzado.
  6. Plazo, cuotas y línea sugerida son datos del medio de pago, sin llave a `LND_*`.

  El recaudo va por fuera, con comprobantes de la 009 contra el mismo tercero y documento cruce
  (Supuestos).
- **Cuando llegue IC** (D-02):
  - se registran el destino y el parámetro;
  - el despachador entrega lo acumulado, por Id y respetando las dependencias;
  - Cartera deduplica por `MessageId`;
  - `ValidationFailed` lo fija IC consultando `EstadoDeValidacionAsync`, y levanta
    `Integracion.ValidacionFallida`. Nada se anula solo.
- **Recomendación para D-02** (pregunta H2): la cuenta por cobrar del crédito comercial a clientes, en
  Contabilidad; la del crédito a asociados por línea con tasa, en Cartera, con una **reclasificación**
  de la cuenta provisional por tercero y documento cruce, sin volver a contabilizar el ingreso.
- **Lo que la spec de Cartera debe cubrir** (pregunta H4):
  - consultas de estado, cupo y líneas, y el estado de validación por `MessageId`;
  - recepción idempotente, en orden por cadena;
  - obligaciones creadas desde documentos comerciales, sin `LoanApplication`, con numeración segura y
    amortización en decimal;
  - ajustes, y el crédito comercial a no asociados;
  - quién registra la cuenta por cobrar;
  - validación a la fecha de cada venta, con historia punto a punto;
  - recaudo, e incorporación de lo recaudado por fuera;
  - el IVA sobre los intereses de financiación (ET art. 447);
  - permisos, alertas y conciliación.

**Por qué**:
- Es exactamente lo que decidió el dueño (crédito provisional, IC pendiente), y usa la misma entrega
  garantizada sin nada específico de Cartera hoy (SC-023: 0 perdidos, 0 repetidos).
- La regla tercero + documento cruce reutiliza la E2 y deja cada venta rastreable.
- El sello de quién registra la cuenta por cobrar hace determinista el paso a Cartera y evita contarla
  dos veces (FR-062: un solo lado).
- La implementación nula deja listo el punto por donde entra IC, sin tocar el POS.
- Una interfaz en el mismo proceso basta, porque R6 prohíbe leer tablas, no llamar a un contrato.

**Alternativas descartadas**:
- Marcar las entregas `NotApplicable` y reemitirlas después: lo emitido es inmutable, y reemitir arriesga
  duplicados.
- Una tabla aparte «para Cartera»: otra cola.
- Rechazarlas por destino ausente: alertas sin una causa que alguien pueda corregir.
- Usar `Associate.CreditLimit` o `PosCardLimit` como tope provisional: el cupo es de Cartera, y la spec
  dice «sin consultar cupo».
- Guardar las ventas a crédito en una cartera provisional de Inventario: FR-062 lo prohíbe.
- HTTP interno.
- Leer `LND_LoanPortfolios`, como hace Nómina (C4).
- Construir las piezas de Cartera dentro de la 012: el dueño decidió que IC espere su propia spec.
- Records en `Application/Integracion/Contratos/Cartera` (informe de compras): van en
  `Application/Common/Integration/Contracts/Inventory` (T8).

**Riesgos**:
- Toda venta a crédito exige que una segunda persona apruebe desde su propia sesión, y eso puede frenar
  la caja.
- Si el recaudo por fuera no usa tercero y documento cruce en la cuenta de la matriz, después no se
  puede conciliar.
- Las libranzas no descuentan estas ventas, porque Cartera no las conoce.
- Si D-02 elige que Cartera registre la cuenta por cobrar sin reclasificar, se cuenta dos veces.
- IC es un trabajo grande sobre el código heredado de Lending: numeración max+1, `double`, obligaciones
  sólo desde solicitud y saldos sin historia.
- v1 puede no traer todo lo que D-02 pida.

**Spec**: FR-014, FR-015, FR-056, FR-060 a FR-062, FR-069, FR-084, FR-085, FR-097, FR-098, US6, SC-003,
SC-010, SC-023; C4; D-02; Supuestos 16 y «Recaudo del crédito provisional».

## R27. DIAN: adaptador, canónico y canales

**Decisión** (T40):
- **Puerto**: `ICanalDeEmisionElectronica`, en `Application/ElectronicInvoicing/Channels`.
  - El registro `ICanalesDeEmision.Resolver(código)` devuelve la implementación del canal **sellado en
    el documento**, no la de la configuración de hoy.
  - Operaciones:
    - emitir: la emisión normal y la transmisión diferida de las contingencias 03 y 04;
    - consultar el estado;
    - emitir un evento RADIAN;
    - descargar un artefacto: XML firmado, ApplicationResponse o AttachedDocument;
    - probar credenciales y salida.
  - Otras son opcionales, según `CapacidadesDelCanal`: tipos admitidos, si acepta el número del ERP
    (eliminatorio), si es asíncrono, si da PDF o correo, rangos y adquirente.
  - Todas devuelven un `ResultadoDeCanal` uniforme: `ChannelOutcome` (Validated, ValidatedWithNotices,
    Rejected, InProcess, NotFound, DianUnavailable, ChannelUnavailable, InvalidData), código único y su
    tipo, QR, fecha de validación, mensajes con su traducción, artefactos, referencia externa, códigos y
    duración.
  - El adaptador traduce los códigos de cada proveedor y **nunca lanza** por un rechazo:
    `ChannelUnavailable` si la petición no salió; `InProcess` si pudo haber llegado.
- **Canónico**: `DocumentoElectronicoCanonico` v1, en `Application/ElectronicInvoicing/Canonical`.
  - Es independiente del proveedor y trae los códigos DIAN ya resueltos:
    - tipo de documento y de operación; prefijo y consecutivo; ambiente; fecha y hora con −05:00;
    - forma y medios de pago;
    - emisor, y adquirente tomado de la **copia fiscal** (o consumidor final);
    - líneas con unidad Rec. 20, descuentos, cargos e impuestos por línea; retenciones informativas;
      totales;
    - referencias al documento corregido, con su código de concepto;
    - bloque de contingencia; bloque POS, con caja, cajero y software; bloque DS.
  - Lo arma **un solo** `ConstructorDelCanonico`, a partir de una entrada neutral que entrega el módulo
    fuente por `IFuenteDeDocumentoElectronico`. Inventario lo implementa con
    `FuenteDeEmisionDeInventario`, así la plataforma no lee tablas `INV_`.
  - Se guarda como artefacto de cada versión, con su SHA-256.
  - Los catálogos DIAN (tipos, identificación, responsabilidades, tributos, Rec. 20, medios y formas de
    pago, conceptos de corrección, tipos de caja) son JSON embebidos y versionados, que lee
    `CatalogoDian`.
  - Si falta un dato, la confirmación se bloquea diciendo qué falta y dónde completarlo.
- **Proyecto nuevo**: `src/Infrastructure/IngenIA365ERP.ElectronicInvoicing` (I4), con:
  - `Channels/Simulado/CanalSimulado`, obligatorio para CI, las e2e y el ensayo. Según los datos del
    documento, responde validado, con notificaciones, rechazado, timeout, contingencia DIAN o canal
    caído;
  - `Channels/<Proveedor>`, cuando se contrate;
  - `Channels/ServicioCentral/CanalServicioCentral`, cuando exista el servicio de la 010;
  - `Credentials/CredencialesEnArchivo`, `Processor/ProcesadorDeDocumentosElectronicos` y
    `Circuit/CircuitoDeCanal`.

  Los HttpClient tipados llevan un timeout corto y **ningún reintento automático en la emisión**: sólo en
  consultas y descargas, que son idempotentes. `ElProveedorTecnologicoSoloLoConoceSuAdaptador` impide
  que Domain y Application referencien el proyecto `IngenIA365ERP.ElectronicInvoicing` o un SDK de
  proveedor; la API lo referencia sólo como raíz de composición: únicamente `Program.cs` llama su
  extensión `AddElectronicInvoicing()`, y ningún otro archivo de la API usa tipos de
  `IngenIA365ERP.ElectronicInvoicing.*`.
- **Credenciales**, fuera de la base y del repositorio (FR-064).
  - Un Secret por ambiente, `erp-fe-credenciales`, con una clave `{tenantPublicId}.{channelCode}.json`
    por cooperativa y canal.
  - Se monta como directorio, sin `subPath`, en `/secrets/facturacion-electronica/`: el kubelet lo
    refresca sin reiniciar la API.
  - `CredencialesEnArchivo` deriva la ruta **sólo** de la cooperativa resuelta y del canal sellado, con
    caché de 5 min.
  - La base guarda sólo `CredentialKey` y `CredentialVerifiedAt`. Si `CredentialKey` no coincide con la
    ruta derivada, falla con `ElectronicInvoicing.CredentialMismatch`.
  - Guion `tools/scripts/crear-secreto-facturacion-electronica.ps1 -Ambiente -Tenant -Canal`, con el
    patrón de `crear-secreto-smtp.ps1`: `kubectl patch` de una sola clave, y confirmación «PRODUCCION» en
    producción.
  - Lo vigila `LasCredencialesDeFacturacionNoTocanLaBase`.
- **Software propio** (después).
  - `CanalServicioCentral` convierte el canónico en UBL 2.1 sin firmar, con constructores puros y casos
    dorados byte a byte.
  - Llama al servicio central con el sobre del contrato de la 010: JWT RS256 de 5 min, secretos por
    nombre y `X-Idempotency-Key`.
  - El contrato se amplía con las rutas de facturación: validar, firmar y transmitir, consultar estado,
    eventos, rangos y adquirente.
  - El servicio calcula el CUFE, CUDE o CUDS, firma en XAdES-EPES, transmite, devuelve los artefactos y
    nunca reintenta.
  - Todo sin cambiar tablas, estados, pantallas ni comandos.
- **Proveedor**: lo deciden el dueño y COOFLOPAL (D-05, D-07).
  - Criterios eliminatorios:
    - autorizado por la DIAN, con API documentada y sandbox;
    - acepta el número que asigna el ERP y varias resoluciones por NIT;
    - cubre 01, 91, el DEE POS y su nota, el DS y su nota, las contingencias 03 y 04 con código
      explícito, y la recepción con los eventos 030 y 032;
    - responde síncrono con código único y QR, y permite consultar el estado y descargar los artefactos;
    - usa credenciales por empresa.
  - Criterios de puntuación: p95 de 5 s o menos con 30 cajas en sandbox; precio a unos 150.000
    documentos al mes por cooperativa; ISO 27001; PDF y correo que se puedan desactivar; salida de datos
    al terminar.
  - Lista corta para una prueba pagada: The Factory HKA y Dataico (pregunta A3). Mientras tanto se
    desarrolla y se ensaya con `CanalSimulado`.

**Por qué**:
- FR-064 exige dos modos detrás del mismo adaptador, un proveedor intercambiable y que cambiar de canal
  no cambie documentos ni pantallas. La frontera en Application, con tipos propios, repite lo que hizo la
  010 (Principio II).
- Separar «no salió» de «pudo haber llegado» es lo que permite reintentar sin duplicar: lo segundo sólo
  se resuelve consultando.
- Cada proveedor revisado recibe su propio formato, y ninguno acepta UBL sin firmar como entrada
  general. En modo proveedor, el CUFE lo calcula quien tiene la clave técnica y firma.
- Un Secret con una clave por cooperativa se amplía sin tocar el Deployment, y derivar la ruta de la
  cooperativa resuelta impide usar credenciales ajenas aunque alguien altere la base.

**Alternativas descartadas**:
- Un método por tipo de documento.
- Exponer en Application el cliente HTTP de un proveedor.
- Webhooks como vía principal de estado: no son uniformes y exigen una ruta pública por cooperativa.
  Pueden sumarse como aviso, nunca como fuente de verdad.
- Generar UBL para todos los canales.
- Usar el JSON de un proveedor como canónico.
- Mandar al servicio central un JSON para que arme el UBL: la 010 ya lo descartó.
- Los adaptadores en Storage o en la API.
- `Microsoft.Extensions.Http.Resilience` en la emisión.
- Credenciales cifradas en la base: FR-064 lo prohíbe.
- Un Secret por cooperativa, montado aparte: cambia el Deployment.
- AWS Secrets Manager con el rol de la 011: es por ambiente, no por cooperativa. Queda como evolución.
- Leer los secretos por la API de Kubernetes.
- Firmar dentro de la API del ERP.
- Un servicio central distinto para facturación.
- Ingenia365 como proveedor tecnológico: fuera de alcance (C6).
- Siigo, Alegra o Loggro como canal: exigen tener clientes y productos en su propio sistema.
- Factus, salvo que admita el número del ERP.
- Tablas `FEL_*` y rutas `/api/electronic-documents` y `/api/dian/*` (informe DIAN): van en `COR_` y
  `/api/electronic-invoicing/*` (T2, T3).

**Riesgos**:
- I4 depende sólo de un contrato externo, porque el servicio central no existe (N3 sin empezar), y la
  habilitación, la asociación de prefijos, la resolución de contingencia y el set de pruebas tienen
  trámites ajenos al desarrollo.
- Un Secret compartido por todas las cooperativas de un ambiente descansa en derivar bien la ruta: prueba
  unitaria, prueba de arquitectura y `CredentialMismatch`.
- Si se contrata como socio con una llave maestra, una sola credencial podría emitir a nombre de cualquier
  NIT: se exigen credenciales por empresa.
- Si cambian el anexo o la API del proveedor sin versionar el canónico y el adaptador, puede haber
  rechazos masivos.
- Una fuente secundaria (Actualícese) sugiere que el DEE sólo se emite con software propio. Lo
  contradicen Sovos y la oferta de los proveedores; hay que confirmarlo con el proveedor elegido.

**Spec**: FR-011, FR-016, FR-063, FR-064, FR-095, US8 esc. 1, 7 y 8, SC-002, SC-004, SC-019; C6; D-05,
D-07; Supuesto 10.

**Normas y fuentes**:
- Res. Única 000227 del 23-09-2025, Título 5:
  https://www.dian.gov.co/normatividad/Paginas/Resolucion-000227-del-23092025.aspx
- Anexo técnico de la factura 1.9:
  https://www.dian.gov.co/impuestos/factura-electronica/Documents/Anexo-Tecnico-Factura-Electronica-de-Venta-vr-1-9.pdf
- Anexo del documento equivalente 1.0:
  https://www.dian.gov.co/impuestos/factura-electronica/Documents/Anexo-Tecnico-Documento-Equivalente-Electronico-V1-0-final.pdf
- Servicio web de la DIAN (`WcfDianCustomerServices`, SOAP 1.2 con WS-Security), con las operaciones
  listadas en https://github.com/movaltech/cofacture-php
- Proveedores:
  - The Factory HKA: https://felcowiki.thefactoryhka.com.co/index.php/Manual_de_usuario_API_IntTfhkaFel21
    y https://felcowiki.thefactoryhka.com.co/index.php/Manual_DLL_hkafact21_-_Emisi%C3%B3n_V4
  - Dataico: https://portaldelcliente.dataico.com/es/knowledge/documentaci%C3%B3n-t%C3%A9cnica-de-la-api-de-dataico-factura-electr%C3%B3nica
  - Factus: https://factusapi-v2.halltec.co/facturas/descripcion-de-campos/
  - Facturatech: https://webservice.facturatech.co/v2/BETA/WSV2DEMO.asmx
  - Siigo: https://developers.siigo.com/docs/siigoapi/invoice/1-create-invoice/

## R28. DIAN: numeración fiscal, resoluciones, estados y procesamiento

**Decisión**:
- **Tablas** (`COR_`, I4):
  - `COR_ElectronicEmissionSettings`: configuración con vigencia. Lleva `Mode` (`EmissionMode`:
    TechnologyProvider, OwnSoftware), `ChannelCode`, `Environment`, `SoftwareId`, `TestSetId`,
    `CredentialKey`, `CredentialVerifiedAt`, `EmailDeliveryBy`, `IsEnabled` y `Reason`.
  - `COR_DianNumberingResolutions`: `Kind` (`ResolutionKind`: Invoice, PosEquivalent, SupportDocument,
    Contingency), `BacksUpKind`, número y fecha, `Prefix` (hasta 4 caracteres), rango, vigencia,
    `Environment`, `LastIssuedNumber` y `RowVersion`.
  - `COR_DianResolutionChannels`: prefijo ↔ canal o software, desde una fecha. `TechnicalKey` sólo en
    las de factura, con `[NoAuditar]` y enmascarada.
  - `COR_ElectronicDocuments`: uno por número fiscal. Lleva módulo y documento fuente, `Kind`
    (`ElectronicDocumentKind`), código DIAN, resolución (nula en las notas), prefijo, consecutivo,
    ambiente, canal sellado, código único, QR, `Status`, `ContingencyType`, evento de contingencia,
    fechas, versión vigente, `CorrectsDocumentId`, `RejectedBy`, intentos, `NextAttemptAt`, `LeaseUntil`,
    `LeaseOwner` y `TransmissionDeadline`. UK `(Environment, Prefix, Consecutive)` **sin filtro**.
  - `COR_ElectronicDocumentVersions`: hecho. Número de versión, documento fuente, `Reason`
    (Initial | CaseA | CaseB), canónico, XML firmado, AttachedDocument y PDF.
  - `COR_ElectronicDocumentTransmissions`: hecho. Un intento: operación, canal, solicitante, duración,
    clave de idempotencia, resultado, códigos, mensajes crudos y traducidos, referencia externa y
    ApplicationResponse.
  - `DianEnvironment` se traslada a `Domain/Enums/Dian`, con los mismos valores.
- **Numeración**: la de R9, más:
  - Las notas llevan su propio consecutivo, sin resolución, y pueden referirse a una factura cuya
    resolución ya venció.
  - La resolución de contingencia es una fila `Kind = Contingency`, ligada al tipo que respalda. Es el
    tipo «de contingencia» de la caja.
  - `ConsultarRangos` (GetNumberingRange), donde el canal lo ofrezca, sólo verifica y propone la clave
    técnica; nunca crea resoluciones.
  - Avisos `Dian.ResolucionPorAgotar` y `Dian.ResolucionPorVencer`, según
    `Dian.AvisoResolucionPorcentaje` (0,90) y `Dian.AvisoResolucionDias` (30).
  - Rutas `/api/electronic-invoicing/resolutions` (`RegisterNumberingResolutionCommand`,
    `LinkResolutionToChannelCommand`) y pantalla `/maestros/resoluciones-dian`.
- **Estados** (`ElectronicDocumentStatus`):
  - `Pending`: numerado, sin constancia de que haya llegado;
  - `Sent`: el canal lo recibió o pudo haberlo recibido. Sólo se consulta;
  - `Validated` y `ValidatedWithNotices`: finales;
  - `Rejected`: rechazo confirmado, con `RejectedBy` (Dian | Channel);
  - `IssuerContingency` (03) y `DianContingency` (04);
  - `CancelledWithoutReplacement`: el caso c. Final.

  `ContingencyType` se conserva aunque el documento se valide después.
- **Máquina de estados**: `Domain/ElectronicInvoicing/TransicionesDelDocumentoElectronico`, pura, con
  una prueba de todas las transiciones prohibidas. Invariantes:
  - nunca `Validated` sin código único **y** respuesta de validación;
  - un `Sent` no se corrige ni se anula hasta tener respuesta (`ElectronicInvoicing.Document.AwaitingResponse`);
  - las notificaciones solas no rechazan.
- **Procesamiento**. La llamada al canal **nunca** va dentro de la transacción de confirmación:
  1. se confirma, se numera y se crea el `COR_ElectronicDocuments` en `Pending`, con la versión 1 y el
     SHA-256 del canónico;
  2. commit;
  3. se sube el artefacto y se llama `EmitElectronicDocumentCommand`, fuera de la transacción;
  4. otra transacción registra la transmisión, los artefactos y el estado.
- **En el POS**, la petición espera hasta `Dian.EsperaMaximaPosSegundos` (15 s). Si no hay respuesta, la
  venta queda confirmada y el documento pasa a «pendientes de entrega» de la caja. Esa espera agotada
  **cuenta como falla** para `CircuitoDeCanal`: pasado `Dian.UmbralFallasCircuito`, las ventas nuevas
  abren la contingencia 03 y reciben en el acto su representación de papel (R29). Así se mide SC-004
  cuando el canal no responde (precisión 12 a la spec).
- **Ante un resultado ambiguo** (timeout, 5xx, corte después de enviar), **siempre se consulta primero**
  (`QueryElectronicDocumentStatusCommand`).
  - Sólo si el canal responde `NotFound` se reenvía la misma versión con el mismo número.
  - «Ya existe» se traduce a una consulta, nunca a un error.
  - Clave de idempotencia: `{tenantPublicId}:{ambiente}:{prefijo}{consecutivo}:v{versión}`.
- **Espera entre intentos**: 15 s, 1, 2, 5, 15, 30 y 60 min, y después cada hora hasta el plazo.
- **Procesador**: `ProcesadorDeDocumentosElectronicos` corre por `IEjecutorEnCooperativa`.
  - Toma el arrendamiento `einvoicing.process` **y** un arrendamiento por fila (`LeaseUntil`/`LeaseOwner`,
    con `UPDATE … WHERE (LeaseUntil IS NULL OR LeaseUntil < ahora) AND NextAttemptAt <= ahora`), porque el
    intento en línea del POS y el procesador pueden coincidir.
  - Alertas: `Dian.DocumentoSinValidar` (a los `Dian.MinutosAlertaSinValidar`) y `Dian.DocumentoRechazado`.
- **Bandeja**: `/ventas/documentos-electronicos` (`ListElectronicDocumentsQuery`; reintentar y consultar
  con `ElectronicInvoicing.Documents.Transmit`) y la vista `dian-documents`.

**Por qué**:
- La 010 dejó en `PAY_ElectronicPayroll*` la forma: configuración, rangos, documentos y transmisiones
  inmutables, en la base de la cooperativa, y un servicio central sin estado.
- La diferencia es que en factura un número rechazado **no** se consume: se reenvía con el mismo número.
  Por eso la identidad es el número, y las versiones van en otra tabla.
- `COR_` deja que otros módulos (Cartera, servicios) facturen por el mismo camino sin depender de `INV_`.
- `Pending` y `Sent` tienen políticas opuestas: reenviar frente a sólo consultar.
- Una llamada externa dentro de la transacción mantendría tomada la fila de la resolución y serializaría
  las 30 cajas (SC-019).
- «Consultar antes de reenviar» es la regla que evita duplicados ante la DIAN.
- La 010 dejó la consulta manual (D-11) porque no había trabajo de fondo. Aquí FR-063 pide reintentos
  automáticos y FR-067 exige transmitir dentro del plazo.

**Alternativas descartadas**:
- Prefijo `FEL_` y rutas `/api/electronic-documents` y `/api/dian/*` (informe DIAN): van en `COR_` y
  `/api/electronic-invoicing/*` (T2, T3).
- Reusar `PAY_ElectronicPayroll*`: son de nómina.
- Columnas DIAN en `INV_Documents`: mezclan el ciclo comercial con el fiscal y atarían a otros emisores a
  Inventario.
- Una fila por versión con UK filtrado a la vigente: frágil.
- Los estados en MongoDB.
- Copiar los estados de la 010: no tiene contingencias ni el caso c.
- Un solo estado de contingencia: la 03 y la 04 difieren en numeración, entrega y transmisión.
- Emitir dentro de la transacción.
- Emitir sólo desde la bandeja de mensajes: agrega latencia al POS.
- Un candado Redis por documento: el candado de fila es transaccional y queda como evidencia.
- La consulta manual, como en la 010.
- El incremento atómico `UPDATE … RETURNING` de `LastIssuedNumber`: se unifica con el cerrojo (R9).

**Riesgos**:
- SC-004 y SC-019 dependen de la latencia del proveedor y de la DIAN: hay que medirlas en sandbox antes
  de contratar.
- Reenviar sin consultar, o renumerar, un resultado ambiguo produce duplicados ante la DIAN. Se fija con
  pruebas.
- Sin el arrendamiento por fila, dos réplicas procesarían el mismo documento y reenviarían sin consultar.
- El volumen de artefactos (unos 150 KB por documento: 0,75 GB al día y 270 GB al año por cooperativa a
  5.000 documentos diarios, más unas 20.000 filas diarias en `COR_Attachments`) exige transición a
  almacenamiento frío (R30).
- Faltan datos DIAN en el maestro (responsabilidades, tributo, tipo de identificación, unidad Rec. 20,
  código DIAN del medio de pago), y sin ellos no se confirma.

**Spec**: FR-016, FR-038, FR-063, FR-065, FR-066, FR-083, US8 esc. 2, 3 y 6, SC-002, SC-004, SC-013,
SC-015, SC-019.

**Fuentes**:
- Tipos de documento del anexo 1.9 (01, 02, 03, 04, 05, 91, 92, 95, 96):
  https://felcowiki.thefactoryhka.com.co/index.php/Tablas_de_c%C3%B3digos_de_propiedades_para_emisi%C3%B3n_de_documentos_V1.9-Cambios_Integraci%C3%B3n_Anexo_V_1.9.
  Los del DEE y su nota (20 y 94) salen del anexo 1.0 y hay que cotejarlos.
- Prefijo de hasta 4 caracteres: Res. 165 art. 11 num. 4, compilada en la 227.
- El DEE se valida antes de entregarse:
  https://sovos.com/es/iva/como-emitir-documentos-equivalentes-electronicos-en-colombia/

## R29. DIAN: contingencias y documentos rechazados

**Decisión**:
- **Contingencia de la DIAN (04)**. Sólo la declara el canal: `DianUnavailable`, junto con el documento
  generado y firmado, su código y su XML. El ERP no la supone por un timeout propio.
  - El documento queda en `DianContingency`, con su numeración **normal**.
  - Se abre un `COR_DianContingencyEvents` de tipo `Dian04`, o se une al que esté abierto.
  - Se entrega con la representación marcada «pendiente de validación de la DIAN».
  - El procesador lo transmite cuando el canal informa que la DIAN volvió, o en el siguiente intento que
    prospere.
- **Contingencia del facturador (03)**.
  - Se abre por `CircuitoDeCanal`, por cooperativa y canal: fallas de conexión definitivas **y las
    esperas del POS agotadas** (`Dian.EsperaMaximaPosSegundos` sin respuesta), y el sondeo `Probar`,
    sobre `Dian.UmbralFallasCircuito`. Contar el timeout del POS es lo que permite que, tras el umbral,
    la venta siguiente salga en papel en el acto (SC-004, precisión 12).
  - También a mano: `OpenContingencyCommand`, con `ElectronicInvoicing.Contingencies.Declare` y motivo.
  - Mientras está abierta, las ventas fiscales **nuevas** toman el consecutivo de la resolución de
    contingencia del rol de contingencia que respalda al rol de la venta en la caja
    (`PosSaleContingency` o `InvoiceContingency`, R25), y el ERP imprime su representación de papel, sin CUFE y con la
    leyenda de la norma.
  - Al cerrarse (`CloseContingencyCommand`, o el sondeo que vuelve a responder), se transmiten en orden
    como tipo 03, referidos al número de papel.
  - Un documento ya numerado con la numeración normal y con resultado ambiguo **nunca** se renumera a
    contingencia: sigue en `Sent` o `Pending` y se resuelve consultando.
- **Plazo**: `TransmissionDeadline` = cierre del evento + `Dian.PlazoContingenciaHoras` (48, con
  `LegalSource`).
  - Se cuenta desde que se supera el inconveniente en la factura y el DEE, y desde el día siguiente en el
    documento soporte (`PlazoDeContingencia`, puro).
  - Alertas: `Dian.PlazoDeContingencia`, `Dian.AlertaHorasAntesDelPlazo` horas antes del vencimiento, y
    `Dian.ContingenciaAbierta`.
  - El evento guarda inicio, fin, causa y evidencias como adjuntos, para la constancia ante la DIAN
    (dueño `DianContingencyEvent`: se lee con `ElectronicInvoicing.Contingencies.View`, se sube con
    `.Declare` y no se borra).
  - Pantalla `/ventas/contingencias-dian`.
- **Rechazados** (FR-066). Los tres casos aplican sólo con `Status = Rejected` **confirmado**, por una
  respuesta definitiva o por la consulta. Los errores de validación del proveedor que nunca llegaron a la
  DIAN se tratan igual, con `RejectedBy = Channel`.
  - **(a)** `CorrectRejectedDocumentCommand`. La regla pura `ReglaDeCorreccionFiscal` compara la «huella
    económica» del canónico viejo y del nuevo: identificación del tercero; líneas (producto, cantidad,
    base, descuentos, impuestos y retenciones); totales.
    - Si coincide: crea la versión n+1; actualiza la copia fiscal si cambió (nombre, dirección, contacto,
      responsabilidades), con antes y después auditados; vuelve a `Pending`; y reemite por el **mismo**
      canal con el **mismo** número, sin mensajes de negocio.
    - Si difiere, responde qué campo económico cambió
      (`ElectronicInvoicing.Document.EconomicFootprintChanged`, `data.fields[]`) y remite al caso b.
  - **(b)** `ReplaceRejectedDocumentCommand`.
    - Anula el documento del ERP con un `Voiding` sin efecto fiscal, que emite sus mensajes de anulación
      a existencias, Contabilidad y Cartera, y marca `FiscalNumberReleased`.
    - Crea el documento de reemplazo, precargado, y lo confirma con el **mismo** número fiscal, por una
      vía de numeración exclusiva de este comando, sin consumir consecutivo.
    - Crea la versión n+1, con el documento de reemplazo como fuente.
  - **(c)** `CancelRejectedDocumentCommand`: como b, pero sin reemplazo. Deja
    `CancelledWithoutReplacement`, con motivo y responsable. El número queda ligado a su documento anulado
    y no cuenta como hueco.

  Ningún caso aplica a un `Sent`, a un `Validated` ni a un `Pending` sin respuesta
  (`ElectronicInvoicing.Document.NotRejected`). Permiso: `ElectronicInvoicing.Documents.Correct`.
- **Documento validado** (FR-066). «Anular» emite el documento de corrección de su tipo, nunca
  `DocumentoAnulado`:
  - factura: nota crédito total;
  - DEE: nota de ajuste;
  - documento soporte: nota de ajuste.

  `VoidInventoryDocumentCommand` sobre un fiscal validado responde `Inventory.Document.FiscalUseCorrection`.
  - Un documento expedido en contingencia se corrige con su documento de corrección, que se transmite
    después del original y referido a él.
  - Si al transmitirlo lo rechazan, rigen los casos a a c, y su corrección en cola se refiere al documento
    que quede validado con ese número.
  - SC-003: anular una factura validada, con una sola acción, emite su nota crédito total.

**Por qué**:
- La norma y los proveedores coinciden en que el 04 usa la numeración normal y lleva CUFE, y quien sabe si
  la DIAN está caída es el canal. Si el ERP lo dedujera de un timeout, podría declarar contingencia con un
  documento que sí llegó.
- El circuito evita declarar el 03 por un solo timeout, y no detiene la venta.
- Renumerar un documento que quizá llegó produciría dos documentos para la misma venta.
- La huella económica vuelve verificable la frontera entre a y b, en vez de dejarla al criterio de quien
  opera. Como el caso a no emite mensajes, elegirlo mal escondería un cambio económico.
- Un rechazado no está expedido; por eso su número no se consume.

**Alternativas descartadas**:
- Que el ERP decida el 04 con un sondeo propio: en modo proveedor el ERP no habla con la DIAN.
- Numerar el 04 con la resolución de contingencia: es incorrecto según la norma.
- Activar el 03 sólo a mano: frena la caja. Se conserva como complemento.
- Pasar a contingencia el documento en curso.
- Una sola resolución de contingencia para todos los tipos: el modelo admite una por tipo respaldado.
- Emitir un documento nuevo con número nuevo, como la nómina de la 010: contradice FR-066 y FR-038.
- Dejar que el usuario diga si es caso a o caso b.
- Una nota crédito sobre un rechazado: el rechazado no está expedido.

**Riesgos**:
- Hay que confirmar con el proveedor o con la contadora: la contingencia del DEE POS (¿numeración de
  contingencia o transmisión diferida?), los códigos 20 y 94, la numeración de las notas de ajuste del DS
  y el contenido exacto del QR.
- La página de inconvenientes de la DIAN dice «30 días» para el 03. El plazo es un parámetro con norma.
- Confundir un rechazo del proveedor con uno de la DIAN aplicaría b o c indebidamente. Por eso exigen un
  rechazo confirmado.
- El caso b, y el índice de `INV_Documents` con `FiscalNumberReleased`, deben existir desde I1, aunque la
  emisión llegue en I4.

**Spec**: FR-005, FR-006, FR-011, FR-038, FR-065, FR-066, FR-067, US8 esc. 3, 4 y 5, SC-003, SC-013,
SC-015; Supuestos 8 y 9.

**Fuentes**:
- Contingencias tipo 03 y 04, y el plazo de 48 h: ET art. 616-1, citado en la Res. 165
  (https://normograma.dian.gov.co/dian/compilacion/docs/resolucion_dian_0165_2023.htm);
  https://felcowiki.thefactoryhka.com.co/index.php/Contingencia_de_factura_electr%C3%B3nica_un_aliado_para_su_negocio;
  https://micrositios.dian.gov.co/sistema-de-facturacion-electronica/inconvenientes-tecnologicos/ (esta
  última todavía dice 30 días).
- Documento soporte, Res. 167/2021 (https://normograma.dian.gov.co/dian//compilacion/docs/resolucion_dian_0167_2021.htm):
  48 h desde el día siguiente en las contingencias (arts. 11 y 12); nota de ajuste que referencia el CUDS
  (art. 5); el rechazado se corrige hasta quedar validado (art. 13).

## R30. DIAN: obligación, cambio de canal, representación gráfica, artefactos y RADIAN

**Decisión**:
- **Obligación de facturar**: parámetro `Dian.ObligadaAFacturar`, del módulo `EINV`, con vigencia e
  historial. Es **sí** por defecto, y apagarlo exige permiso y motivo.
  - La decisión se toma en un solo sitio: `GuardiaDeEmisionFiscal.Evaluar(fecha, tipo, caja)`, con el
    patrón de `GuardiaDeMetodos.Evaluar`.
  - Responde:
    - `Electronic`, con el canal y la resolución;
    - `NonElectronic`: comprobante no electrónico (FR-036);
    - `Blocked`, con sus motivos: I4 no activo, sin configuración vigente, credencial no verificada, sin
      resolución vigente asociada al canal, set de pruebas pendiente en producción, o sin resolución de
      contingencia estando en contingencia 03.
  - La consultan tres sitios:
    - la confirmación, en el servidor (paso 4 del flujo canónico);
    - la apertura de la caja, como aviso temprano;
    - `GET /api/electronic-invoicing/readiness` (`GetDianReadinessQuery`), con la lista de lo que falta,
      al estilo de `legal-parameters/missing`.

    Si bloquea, responde `ElectronicInvoicing.NotReady` (`data.missing[]`).
  - El parámetro sólo gobierna la venta. El documento soporte sigue su propia norma
    (`DocumentoSoporte.Generacion`: `PorOperacion` por defecto, o `Semanal`, el último día hábil) y exige
    un canal configurado (pregunta I6).
- **Configuración y cambio de canal**.
  - `ConfigureEmissionCommand` crea una fila nueva de `COR_ElectronicEmissionSettings` y cierra la
    vigencia de la anterior, sin cruces, con motivo, y auditada sin `CredentialKey` en el diff.
  - Sólo admite el canal nuevo si hay al menos una resolución vigente de cada tipo usado cuyo prefijo
    esté asociado a ese canal o software (`LinkResolutionToChannelCommand`).
  - Cada documento sella el canal al numerar, y sus reintentos, sus correcciones a y b y su transmisión
    desde contingencia salen por ese canal.
  - Si ese canal se retira, `TransmitByCurrentChannelCommand`
    (`ElectronicInvoicing.Documents.TransmitByCurrentChannel`) lo manda por el vigente, sólo si la
    resolución con que se numeró está asociada a él. Queda auditado.
  - Notas y eventos son documentos nuevos: salen por el canal vigente.
  - `VerifyChannelCredentialCommand` prueba la credencial.
  - Pantalla `/admin/facturacion-electronica` (`ElectronicInvoicing.Settings.*`).
- **Representación gráfica**: siempre la del ERP.
  - `IRepresentacionGraficaRenderer` en Application, implementado en `RepresentacionGraficaReport`
    (QuestPDF y QRCoder, que se agrega a `IngenIA365ERP.API.csproj`).
  - Formatos: carta para factura, notas y DS; 80 mm para el POS.
  - Usa la copia fiscal y el QR que devuelve el canal, tal cual. En modo propio lo arma el constructor,
    según el anexo.
  - Leyendas: tipo 04, «pendiente de validación de la DIAN»; papel 03, sin CUFE y con el texto de la
    norma; ambiente de pruebas, «SIN VALIDEZ FISCAL».
  - Entrega: en operación normal, sólo después de `Validated` o `ValidatedWithNotices`; en contingencia,
    en el acto.
  - Correo por el ERP, con `IEmailSender`: el ZIP del AttachedDocument y el PDF, al correo de recepción de
    la copia fiscal. Lo decide `Dian.EntregaCorreo`: `Erp` por defecto, o `Canal` si el proveedor lo
    ofrece, sin enviarlo dos veces.
- **Artefactos** (T41, D-04). `AdjuntosDeModulo.DeModulo` suma tres tipos de dueño, todos con
  `Borrable = false`:
  - `ElectronicSalesDocument`, que se lee con `Inventory.Sales.View`, sin subidas;
  - `ElectronicPurchaseDocument`, que se lee con `Inventory.Purchases.View`, sin subidas;
  - `DianContingencyEvent` (constancias y evidencias del evento 03/04), que se lee con
    `ElectronicInvoicing.Contingencies.View` y admite subidas con `ElectronicInvoicing.Contingencies.Declare`.

  El enlace de descarga (`POST /api/electronic-invoicing/documents/{id}/download-link`) exige sólo el
  permiso del dueño del adjunto; sin él, 404.

  Se guardan con `UploadAttachmentCommand`, por versión y por intento: el canónico (`application/json`),
  el XML firmado, el ApplicationResponse y el AttachedDocument (`application/xml` o `application/zip`), y
  el PDF.
  - `AttachmentPolicy.ModuleGeneratedMimeTypes` (+ xml, zip, json) sólo aplica a lo que genera un módulo;
    las subidas de personas no cambian.
  - Conservación legal: 5 años (ET art. 632). Transición a almacenamiento frío a los 90 días, que no es
    un borrado.
  - El PDF del proveedor no se guarda (pregunta I3).
- **Eventos RADIAN** (T42).
  - Desde **I1**, `INV_SupplierInvoiceEvents` guarda el estado del 030 (acuse de recibo) y del 032
    (recibo del bien) de cada factura de proveedor a crédito: `SupplierInvoiceEventStatus` (Pending,
    RegisteredExternally, Emitted, Rejected, NotApplicable), quién, cuándo, fuente y CUDE.
    - `RegisterExternalRadianEventCommand` (`Inventory.Purchases.RegisterRadianEvent`) registra lo que la
      cooperativa emitió en el portal de su proveedor o de la DIAN.
    - La alerta `Compras.EventosRadianFaltantes` sale a los `Compras.DiasAlertaEventosRadian`, y se cierra
      sola cuando están los dos eventos.
    - Las facturas de contado quedan `NotApplicable`.
  - Desde **I5**, `EmitRadianEventCommand` (`Inventory.Purchases.EmitRadianEvent`,
    `POST /api/inventory/purchases/supplier-invoices/{id}/radian-events/emit` con `{ eventCodes }`, 202)
    emite por el mismo puerto, como un `COR_ElectronicDocuments` de `Kind = RadianEvent030` o `RadianEvent032`, con la misma
    máquina de estados y enlazado por `ElectronicDocumentPublicId`. Se dispara cuando la factura queda
    enlazada a recepciones confirmadas.
  - Los eventos 031, 033 y 034 quedan fuera.
  - Criterio al contratar: que el canal ofrezca recepción y emisión de eventos para el mismo NIT.

**Por qué**:
- US8 esc. 9 y el Supuesto 20: una cooperativa obligada no confirma ventas fiscales sin I4 ni
  resolución.
- El defecto «sí» evita que una cooperativa nueva expida documentos no electrónicos por descuido, lo cual
  es sancionable.
- Una sola decisión impide que la pantalla, el POS y el servidor digan cosas distintas (Principio VIII).
- Las reglas del cambio de canal son las de FR-064, al pie de la letra.
- Una sola plantilla del ERP cumple «cambiar de proveedor no cambia pantallas» y funciona igual en modo
  propio, que no tiene PDF de proveedor.
- El correo por el ERP es necesario en modo propio y deja un solo registro de entrega.
- Los artefactos usan el mecanismo que ya usan PILA, la dispersión y la definitiva. Separar los tipos MIME
  conserva la protección de la 011 sobre lo que suben las personas.
- Sin el 030 y el 032, una factura a crédito no soporta costo ni IVA descontable (Supuesto 11).
  Registrarlos desde I1 deja la alerta y la trazabilidad de quién afirmó haberlos emitido.

**Alternativas descartadas**:
- `FacturacionElectronica.Obligada` como un bool en `COR_Companies`, o deducida de «hay configuración»:
  confunde «no configurado» con «no obligado».
- El defecto «no».
- Canal por tipo de documento: el Supuesto 10 dice que es por cooperativa.
- Reenviar lo pendiente por el canal nuevo automáticamente.
- Usar el PDF de cada proveedor.
- Imprimir en el POS antes de validar.
- Que el correo lo mande siempre el proveedor.
- Un tipo de dueño con un permiso compuesto: exigiría cambiar `Regla`.
- El XML en columnas.
- Agregar `application/xml` a `AllowedMimeTypes`.
- `FEL_RadianEvents` (informe DIAN), o dos banderas en la factura: se pierde quién y cuándo.
- Emitir los eventos en I1: no hay adaptador.

**Riesgos**:
- El volumen de artefactos y su costo en S3 (R28).
- Si el proveedor elegido no emite eventos RADIAN, I5 no se puede cumplir con él.
- Hasta I5, el ERP sólo registra lo que la cooperativa afirma haber emitido.
- Los datos DIAN que faltan en el maestro bloquean la confirmación.

**Spec**: FR-012, FR-036, FR-050, FR-059, FR-063, FR-064, FR-067, FR-068, US8 esc. 7, 8 y 9, US9 esc. 4,
US13 esc. 4, SC-015; D-04, D-05; Supuestos 10, 11 y 20.

**Fuentes**:
- RADIAN, Res. 85/2022
  (https://www.dian.gov.co/normatividad/Normatividad/Resoluci%C3%B3n%20000085%20de%2008-04-2022.pdf): los
  eventos 030 a 034 son ApplicationResponse firmados por el adquirente, y se transmiten con
  `SendEventUpdateStatus`.
- El DEE identificado da derecho a costos e impuestos descontables:
  https://incp.org.co/publicaciones/boletin-virtual/contenido-de-interes-profesional-boletin-virtual/tributario/2024/12/el-documento-equivalente-electronico-otorga-derecho-a-costos-deducciones-e-impuestos-descontables/
- El DEE lleva datos de la caja, del cajero y del software con su fabricante:
  https://actualicese.com/elementos-clave-que-debes-conocer-del-nuevo-pos-electronico/
- Res. 202/2025, autollenado del adquirente y consumidor final:
  https://incp.org.co/sin-categoria/2025/04/nuevas-herramientas-y-mayor-flexibilidad-para-la-facturacion-electronica/
- Documento soporte por operación o semanal: Res. 167/2021, art. 2 y art. 10 par. 2.
- Conservación de soportes por 5 años: ET art. 632.

## R31. Reportes y tablero

**Decisión** (T44):
- **Mecánica**: `TablaExportable` + `EntregaDeInformes.EntregarAsync`, con **una ruta por vista** en
  `/api/reports/inventory/{vista}?format=json|xlsx|pdf|docx`. `Endpoints/Reports/InventoryReportsEndpoints.cs`
  se reescribe.
- **Vistas por entrega**:
  - I1: `kardex`; `valuation` (a cualquier fecha de corte, con el tránsito, desde
    `INV_PeriodClosingBalances` más el kardex); `stock`; `documents`; `count-differences`;
    `reorder-alerts`; `radian-events`; `legacy-comparison-kardex`; `legacy-comparison-valuation`.
  - I2: `messages`, `accounting-batches` y `reconciliation`.
  - I3: `sales-by-session`, `sales-by-register`, `sales-by-payment-means`, `cash-session`, `day-close`,
    `card-payments`, `cash-movements`, `cash-differences`, `voucher-redemptions`, `discount-approvals` e
    `impairment`.
  - I4: `dian-documents`.
  - I5: `purchase-matches` y `method-change-valuation`.
  - I6: `margin?by=product|category|salesperson|customer|point`, `turnover`, `abc`, `no-movement`,
    `expiring`, `purchase-suggestion` y `shrinkage-cap` (opcional, para el régimen ordinario).
- **Columnas ocultas**: `_documento`, `_producto`, `_bodega`, `_mensaje`, `_lote` y `_sesion`.
- **Permisos**:
  - `Inventory.Reports.View` para json e `Inventory.Reports.Export` para archivo;
  - además, `Inventory.Reports.ExportPersonalData` en las vistas con datos de clientes
    (`margin?by=customer`, `card-payments`, las `sales-by-*` con cliente, `voucher-redemptions`);
  - `reconciliation` exige también `Inventory.Reconciliation.View`;
  - `cash-session` y `day-close` de otros cajeros exigen `Inventory.CashSessions.ViewAll`.
- **Auditoría y alcance**: cada exportación se audita desde el handler con `InventoryAuditEmitter` (por el
  PublicId de la cooperativa y la bandeja de auditoría), y el alcance se aplica en la consulta.
- **QuestPDF** sólo para documentos que se firman o se entregan: `RepresentacionGraficaReport`,
  `CashMovementReceiptReport`, `CashCountReport` y `SalesDocumentReport`.
- **Pantallas**: `/inventario/informes?vista=` y `/ventas/informes?vista=`, más tarjetas nuevas en
  `CentroReportes`.
- **Indicios de deterioro** (`impairment`):
  - productos cuyo costo es mayor que el precio de la lista general menos
    `Informes.DeterioroPorcentajeGastosVenta`;
  - vencidos o próximos a vencer;
  - sin movimiento.

  El deterioro lo registra Contabilidad.
- **Tablero** (I6, `/inventario/tablero`, `Inventory.Dashboard.View`), por sucursal y bodega y con un clic
  hasta el detalle:
  - valor del inventario, rotación, días de inventario y margen;
  - ventas del día y del mes, contra el período anterior;
  - reorden, quiebres y próximos a vencer;
  - mensajes pendientes o rechazados, lotes que no corrieron y documentos DIAN pendientes o rechazados;
  - tipos fiscales sin paso a contabilidad;
  - alertas pendientes.

**Por qué**:
- Es el camino de nómina y contabilidad: un exportador, un sobre de error, columnas ocultas para
  profundizar y un permiso de exportación aparte (FR-087).
- Una ruta por vista deja que `LosEndpointsProtegidosExigenPermiso` revise cada una.
- El emisor por PublicId evita el defecto que destaparon las e2e de la 009: la auditoría escrita en la base
  Mongo equivocada.

**Alternativas descartadas**:
- Una sola ruta `/{vista}` con un switch: la prueba de permisos no podría revisar cada tramo.
- Reportes QuestPDF a medida por vista: duplican el exportador.
- `Inventory.Reports.PersonalData` (informe de ventas): el código es `Inventory.Reports.ExportPersonalData`.

**Riesgos**:
- SC-017 (el valorizado a una fecha pasada, con 50.000 productos en 50 bodegas, en menos de 30 s)
  depende de los cierres fijados y de los índices.
- El margen por cliente expone datos personales y exige su permiso.
- Los informes que cruzan con Contabilidad (`reconciliation`) ignoran el alcance de sucursal contable
  (pregunta G5).

**Spec**: FR-011 (exportación de datos personales), FR-021, FR-086 a FR-088, FR-099, FR-101, US17, SC-017.

## R32. Pantallas, menú y clientes

**Decisión** (T45):
- **Carpetas**: `Pages/Inventario`, `Pages/Compras`, `Pages/Ventas` y `Pages/Pos`. Las cuatro entran a
  `ModulosMigrados` de `LasPantallasDicenQueEstanCargando`.
- **Menú**: cuatro `NavMenuGroup` nuevos, **Inventario**, **Compras**, **Ventas** y **Punto de venta**,
  más entradas en:
  - Maestros: `/maestros/impuestos`, `/maestros/medios-de-pago` y `/maestros/resoluciones-dian`;
  - Contabilidad: `/contabilidad/inventario/*`;
  - Administración: `/admin/facturacion-electronica`, la pestaña «Integridad» en `/admin/auditoria`, la
    pestaña «Alcance comercial» en `/admin/usuarios` y el botón «Crear desde perfil sugerido» en
    `/admin/roles`.

  Las rutas por entrega son las de la tabla de `data-model.md` y `plan.md`.
- **Convenciones**: toda página usa `PermissionGate` e `IndicadorDeCarga`, y ninguna declara colores
  literales ni un `<style>` propio.
- **Clientes tipados**: `InventarioClient` (con sus partes), `ComprasClient`, `VentasClient` (`.Pos`,
  `.Caja`, `.Precios`, `.Documentos`), `ImpuestosClient`, `MediosDePagoClient`,
  `FacturacionElectronicaClient` e `IntegracionContableClient`.
  - Se registran en `Web/Program.cs`, `Web.Client/Program.cs` y `App/MauiProgram.cs`.
  - De paso, `MauiProgram` registra `PermisosDelUsuario`, `PersonasClient`, `NominaClient`,
    `ContabilidadClient` y `DescargaDeArchivos`, que hoy le faltan.
- **Handlers**: `CanalDeOrigenHandler` va **después** de `RenovacionDeSesionHandler`, que sigue primero.
- **Códigos**: slugs nuevos en `BuscarCodigoDeCatalogoQuery` para los catálogos del módulo.
- **Retiro de lo viejo**: las 20 páginas del módulo actual, sus temas de `ManualCatalogo`, las tarjetas
  de `CentroReportes` y los enlaces del grupo Cartera se retiran **en el mismo cambio** que publica cada
  reemplazo.

**Por qué**:
- Separar Inventario, Compras, Ventas y Punto de venta sigue a los actores de la spec y a la forma del
  menú actual. Un solo grupo «Comercial» tendría más de 40 enlaces.
- Blazor falla con rutas ambiguas al ejecutar, no al compilar: cada reemplazo borra la página vieja en el
  mismo cambio. `TodoEnlaceDelMenuTieneSuPagina` y `ManualCatalogoTests` fuerzan la coherencia.
- Los errores de registro de servicios sólo aparecen al ejecutar, y el repositorio no tiene pruebas de
  navegador. Registrar en los tres anfitriones es lo único que cumple FR-094.

**Alternativas descartadas**:
- Un solo grupo «Comercial».
- Las rutas viejas conviviendo con un prefijo `/v2`: pantallas muertas visibles, contra FR-092.
- Registrar los clientes sólo en `Web.Client`: rompe la app.
- `/configuracion/facturacion-electronica` (informe DIAN): va en `/admin/facturacion-electronica`.
- `/inventario/impuestos` (informe de seguridad): va en `/maestros/impuestos`.

**Riesgos**:
- El manual en línea, que hoy describe Facturación como si funcionara, hay que reescribirlo a mano.
- WebAuthn y la impresión dentro del WebView de MAUI se prueban a mano.
- Son más de 80 páginas nuevas en seis entregas: la prueba de carga y la de permisos se amplían en la
  primera entrega, no al final.

**Spec**: FR-019, FR-020, FR-092 a FR-094, US1 a US17 (pantallas), SC-008, SC-014.

## R33. Retiro del módulo actual y migraciones

**Decisión** (T4):
- **El retiro va primero**, en su propio commit y su propia migración par: `RetiroDelInventarioHeredado`,
  destructiva con guarda.
  - Suelta las 23 tablas heredadas (todas menos `INV_Salespeople`), y deja un estado intermedio del modelo
    sin las 23 entidades y todavía sin las nuevas.
  - **La guarda** cuenta las filas de cada tabla y se niega nombrando tabla y cantidad: en PostgreSQL con
    `DO $$ … RAISE EXCEPTION`, en SQL Server con `THROW 50012`.
    - Pasa sólo si la base tiene `COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'`, con
      respaldo, revisor y fecha, insertada a mano después del `pg_dump`.
    - Cabecera `MIGRACION-DESTRUCTIVA-APROBADA`.
    - `Down()` recrea vacías las 23 tablas.
  - **Antes de fusionar**, se corre `specs/012-inventario-comercial/diagnostico-inventario-heredado.sql`
    (sólo lectura, conteo por tabla) en DEV, QA y PDN.
  - **Con las tablas salen**:
    - sus 23 entidades y 65 archivos de `Application/Inventory` (salvo `Salespeople`);
    - 17 archivos de `Endpoints/Inventory`, `InventoryReportsEndpoints` e `InventoryValuationReport`;
    - 20 páginas, `NavMenu` 94-111, las tarjetas de `CentroReportes`, los temas de `ManualCatalogo` y el uso
      en `IdentitySeedData`.

    Además, `AccountReferenceFinder` deja de mirar `ProductAccounts` y `VatAccounts`, y se actualizan
    `TestDbContextFactory` y `PersonasTestData`.
  - **Antes del retiro**, la operación de vendedores se muda y se endurece (R17).
  - Las tareas de la 009 T120 (parte Inventario), T125, T128 y T129 (Inventario) quedan «reemplazadas por
    la 012».
- **Migraciones aditivas**, en pares PostgreSQL y SQL Server, con `tools/scripts/add-migration.ps1` y la
  paridad revisada con `check-migration-parity.ps1`. El contenido de cada una está en `data-model.md`.
  - I1: `PlataformaParaInventario` (parámetros, claves, arrendamientos, aprobaciones, alertas, outbox y
    cadena de auditoría, montos, catálogo tributario, columnas de `COR_People`, `COR_Cities` y
    `COR_Branches`, mensajes, dependencias y entregas).
  - I1: `InventarioComercialNucleo` (tablas `INV_` de I1, `UK_INV_Salespeople_PersonId` filtrado, `pg_trgm`
    con índice GIN en PostgreSQL e índice con `INCLUDE` en SQL Server).
  - I2: `IntegracionContableDeInventario`.
  - I3: `VentasYPuntoDeVenta`.
  - I4: `DocumentosElectronicos`.
  - I5: `ComprasYCosteoAvanzado`.
  - I6: `ComercioAmpliado`.
- **Semillas** paramétricas por cooperativa, Order 77 a 87: unidades, tipos de bodega, causas de ajuste,
  tipos de documento por clase con su secuencia, catálogo tributario, DIVIPOLA, tipos de alerta, mapeo de
  comprobantes, denominaciones, `EFECTIVO` y consumidor final. Además, `voucher-types.json` suma `NV`, `CP`,
  `TR`, `AC` y `CJ` en la semilla existente de Order 60.
  - Los permisos no son semilla paramétrica: los siembra la API.
  - Los catálogos DIAN son JSON embebidos versionados, no semillas.
- **No se tocan ni se usan**: `COR_PaymentMethods`, `COR_Sequences`, `DEB_PosTerminals` y
  `COR_Companies.Dian*`. Su retiro no es de esta feature.

**Por qué**:
- Lo exigen el Principio XII y FR-092.
- Separar lo destructivo de lo aditivo hace que la guarda se revise sola.
- Con nombres de tabla reutilizados (`INV_Documents`, `INV_Products`, `INV_Warehouses`), el generador de
  EF haría `AlterColumn` sobre las heredadas si todo fuera una sola migración; la 009 tuvo que resolver eso
  a mano.
- La fila de aprobación por base concreta el «salvo el marcador» de FR-092, cooperativa por cooperativa.
  Una cabecera de código, igual para todas, no puede hacerlo.
- Una migración aditiva por entrega mantiene reversible cada despliegue.

**Alternativas descartadas**:
- Una sola migración que suelta y crea, como `ContabilidadNiif`: exige escribir a mano los drop de las
  tablas que conservan el nombre, y mezcla lo destructivo con lo aditivo.
- Una guarda estricta, sin aprobación posible: si COOFLOPAL alcanzó a parametrizar en las pantallas
  actuales, obligaría a un vaciado manual aparte, que sería otra operación destructiva.
- Dejar las tablas viejas y crear otras con otro nombre: dos inventarios y deuda permanente.
- Un prefijo nuevo (`IVC_`, `COM_`, `SLS_`): rompe la regla módulo = prefijo y deja huérfana a
  `INV_Salespeople`.
- Migrar los datos del módulo actual: FR-095 dice que se retira sin migrar.

**Riesgos**:
- La guarda puede detener el despliegue en producción si alguien parametrizó en las pantallas actuales;
  el plan de implantación de COOFLOPAL lo preveía desde el 2026-09-26. Diagnóstico y decisión antes de
  fusionar (pregunta A1).
- El estado intermedio del modelo exige un commit propio, y revisar la paridad en los dos pasos.
- `CREATE EXTENSION pg_trgm` exige privilegios en los tres clústeres.
- `Feature004_MigrationParity` compara nombres lógicos: el SQL propio de un motor (la extensión, el
  índice GIN) no rompe la paridad, pero hay que revisarlo a mano.
- Retirar las rutas y pantallas en I1 deja a COOFLOPAL sin el módulo actual antes de tener I3. No lo usaba
  en producción.

**Spec**: FR-031, FR-092, FR-095, US1; C2; Principio XII.

## R34. Pruebas

**Decisión**:
- **Pruebas de arquitectura nuevas** (`tests/IngenIA365ERP.Architecture.Tests/Principles`):
  - `InventarioNoConoceContabilidadNiCartera`, en cuatro partes:
    1. ArchUnit: `Application.Inventory*` y `Domain.Entities.Inventory*` no dependen de `*.Accounting*` ni
       de `*.Lending*`, salvo `Application.Common.Integration*`;
    2. una regex sobre el fuente, contra los DbSets contables y de cartera, `"ACC_"`, `"LND_"` y
       `AccountingPoster`;
    3. por reflexión, `IContabilidadParaInventario` declara exactamente sus cuatro métodos e
       `IConsultasDeCartera` sus dos: es la «comprobación automática» de FR-014;
    4. la inversa: `Application/Accounting/Inventory` no depende de `Domain.Entities.Inventory`.
  - Plataforma y contratos: `NingunTrabajoDeFondoOperaSinCooperativa`, `LosComandosDeConsumoNoTienenRuta`,
    `LosComandosDeInventarioLlevanClave` y `LosHechosInmutablesNoSeModifican`.
  - Kardex, numeración y precisión: `NadieEscribeElKardexFueraDelRegistro`, `SoloElNumeradorNumera` y
    `LasCantidadesYCostosTienenSuPrecision`.
  - Sin valores legales: `ElComercioNoTieneValoresLegalesFijos`, sobre `Domain/Inventory`,
    `Application/Inventory`, `Domain/Taxes`, `Application/Core/Taxes`, `Domain/ElectronicInvoicing` y
    `Application/ElectronicInvoicing`.
  - Contabilidad, UVT y parámetros: `LoDeInventarioNoSeReversa`, `LaUvtSeLeeEnUnSoloSitio` y
    `LosParametrosSeLeenEnUnSoloSitio`.
  - Pagos y facturación: `LosPagosNoGuardanElNumeroDeTarjeta`,
    `ElProveedorTecnologicoSoloLoConoceSuAdaptador` y `LasCredencialesDeFacturacionNoTocanLaBase`.
  - Seguridad: `LasConsultasDeInventarioRespetanElAlcance` y `LaSegregacionNoUsaUserIdDelToken`.
- **Pruebas de arquitectura ampliadas**:
  - `LosEndpointsProtegidosExigenPermiso`, con `Endpoints/Inventory/*.cs`, `Endpoints/ElectronicInvoicing/*.cs`,
    `Endpoints/Core/Taxes*.cs`, `Endpoints/Core/PaymentMeans*.cs` y `Endpoints/Reports/Inventory*.cs`;
  - `LasPantallasDicenQueEstanCargando`, con Inventario, Compras, Ventas y Pos;
  - `PrincipioXI_ContableImmutable`, con `Entities/{Inventory,Integration,Approvals,ElectronicInvoicing}/Transactions`;
  - `LaPersonaSeEscribeEnUnSoloSitio`, `TodoEnlaceDelMenuTieneSuPagina`, `ManualCatalogoTests`,
    `PrincipioXII_MigracionesDestructivas` y `Feature004_MigrationParity`.

  `NingunModuloEscribeMovimientosFueraDelContrato` y `LaContabilidadNoTieneCuentasEnCodigo` quedan **sin
  cambios y verdes**.
- **Casos dorados**:
  - en Domain.Tests: `Inventory/Costing/Casos/*.json` (los 17 de R10), `Taxes/Casos`, `Sales/Pricing/Casos`,
    `Sales/Cash/Casos`, `Approvals/Casos`, `Inventory/Purchasing/Casos` (US13-1 a US13-3) e
    `Inventory/Costing/Prorrateo`, más la prueba de propiedades `PropiedadesDelKardexTests`;
  - en Application: `ConstructorDeLineasDeInventario` por operación, `ResolutorDeReglas`,
    `AgrupadorDeResumidos`, la completitud con C8 y los saldos con componentes conexos;
  - las transiciones prohibidas de `TransicionesDelDocumentoElectronico`, `ReglaDeCorreccionFiscal` y
    `SelloDeIntegridad`.
- **Integración**, con Testcontainers en **PostgreSQL y SQL Server**:
  - `Integration/`: `ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests`, `EntregaGarantizadaTests`,
    `IdempotenciaDeOperacionesTests` y `LotesProgramadosTests`;
  - `Inventory/`: `ConcurrenciaDeExistenciasTests`, `CompraDirectaTests`, `TrasladoEnDosPasosTests`,
    `ConteoYAjusteTests`, `CierreDePeriodoTests` y `SaldoInicialYActivacionTests`;
  - `Accounting/ContabilizacionPorMensajesTests`: en línea; lote resumido; reenvío; anulación sobre un
    resumido; período cerrado → rechazo → reapertura → reproceso con la fecha original; «huérfanos cero» en
    los dos sentidos; y `ElBalanceDePruebaCuadra`;
  - `Ventas/`: `VentaPosCompletaTests`, `BonoUnicoConcurrenteTests`, `UnaSesionPorCajaTests`,
    `BusquedaDeProductos50kTests` y `CreditoProvisionalTests`;
  - `Security/`: `AprobacionMultinivelTests`, `AlcancePorBodegaTests` e `IntegridadDeAuditoriaTests`;
  - `ElectronicInvoicing/DocumentosElectronicosTests`, con `CanalSimulado`.

  Las que cambian todo el libro corren en cooperativas aisladas del mismo host (patrón
  `ContabilidadE2E.CooperativaAisladaAsync`), en la colección «Inventario e2e». La fixture apaga los
  trabajos de fondo y conduce a mano el despachador, el reenviador y el procesador DIAN.
- **Volumen**: SC-001 al pie de la letra (1.000 × 50), los 50.000 productos (SC-009) y SC-020 reportan un
  **Skip explícito** sin `RUN_PERF_TESTS=1`, nunca un `return` que cuente como aprobada (CLAUDE.md
  documenta siete pruebas que «pasan» sin correr). Siempre corre una versión reducida de SC-001: 50 ventas
  simultáneas, una vez.
- **A mano**: el interop del navegador (`pos.js`, la impresión, WebAuthn presencial) y la app MAUI, porque
  el repositorio no tiene pruebas de navegador.

**Por qué**:
- Una ruta nueva sin permiso, un `Add` al libro o un `.NextNumber++` no rompen ninguna prueba de unidad: es
  la lección de la 008 y de la 009.
- Las transacciones, los bloqueos y los interbloqueos sólo existen en un motor real (InMemory los ignora),
  y el comportamiento difiere entre PostgreSQL y SQL Server: filtros de índice, `NULL` en índices únicos,
  escalamiento de bloqueos.
- Los casos dorados en JSON son los que la contadora puede leer y firmar (SC-007).

**Alternativas descartadas**:
- Revisar a mano la frontera y los permisos: no escala, y no es la comprobación automática de FR-014 y
  SC-014.
- Probar la concurrencia con InMemory, o con un solo motor.
- Tres pruebas de frontera con nombres distintos (`InventarioNoTocaContabilidadNiCartera`,
  `InventarioNoConoceLaContabilidad`, `InventarioNoLeeContabilidadNiCartera`): queda una sola (T31).
- `SoloLaNumeracionDeInventarioNumera`, `ElInventarioNoTieneValoresLegalesFijos`,
  `LosImpuestosNoTienenValoresLegalesFijos`, `LaFacturacionNoTieneValoresLegalesFijos` y
  `LosMensajesEmitidosNoSeModifican`: se funden en `SoloElNumeradorNumera`,
  `ElComercioNoTieneValoresLegalesFijos` y `LosHechosInmutablesNoSeModifican`.

**Riesgos**:
- El costo de CI crece con los dos motores y las colecciones e2e nuevas: se paraleliza por colección, como
  «Nomina e2e» y «Contabilidad e2e».
- `ElComercioNoTieneValoresLegalesFijos`, como la de nómina, cae con literales dentro de GUID o de
  comentarios: la lista de permitidos es acotada y explícita.
- Las pruebas de rendimiento que no corren en CI deben correrse antes del ensayo, en un ambiente con
  volumen: SC-001, SC-009, SC-019, SC-020 y SC-025.

**Spec**: SC-001 a SC-025 (verificación), FR-014 (comprobación automática), SC-013, SC-014; Principio I.

## Línea base de pruebas

Medida el 2026-09-25 (T004) sobre la rama `012-inventario-comercial`, en develop `57392f5` más los
esqueletos de la fase 1. Cada entrega compara contra esto, no contra las cifras de CLAUDE.md.

| Proyecto | Pruebas | Estado |
|---|---|---|
| Domain | 225 | todas pasan |
| Application | 1.319 | todas pasan |
| Architecture | 151 (134 previas + 17 esqueletos de §2.18) | todas pasan |
| Shared | 106 | todas pasan |
| Load | 2 | todas pasan |

La suite de integración no forma parte de esta línea base: necesita Docker. Sus colecciones se corren
por entrega.

## Lo que falta del dueño

La armonización consolidó 78 preguntas, y la revisión del plan agregó tres (C10, D8 y D9), cada una con
una propuesta por defecto coherente con la spec y la constitución. **Si el dueño no responde, rige la propuesta.** La columna «Bloquea» dice qué no puede salir
sin una respuesta explícita: una entrega, el ensayo (D-07) o la salida en producción. Ninguna pregunta fija
fechas: el ensayo, el conteo, la carga y la salida los define el dueño cuando la aplicación esté lista.

Además de responder, el dueño y COOFLOPAL aportan (D-05, D-07):
- el contrato con el proveedor tecnológico y su habilitación;
- las resoluciones de numeración (también la de contingencia y la del documento equivalente POS), el
  software asociado y las credenciales por empresa;
- las plantillas de parametrización, diligenciadas desde ya y no en las pantallas del módulo actual;
- la matriz de reglas, diligenciada con la contadora, dejando vacío el reporte de completitud antes del
  ensayo;
- el conteo físico, antes de la carga del saldo inicial;
- las cifras de SOLIDO, para el ensayo, la conciliación y la marcha paralela;
- el nombre del segundo revisor de la migración destructiva.

### A. Salida de COOFLOPAL y operación

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| A1 | Si el diagnóstico encuentra filas en las tablas heredadas de alguna cooperativa, ¿se aprueba el retiro por base (fila `INV.RetiroHeredado.Aprobado`, con `pg_dump` y segundo revisor)? ¿Quién es el segundo revisor? | Aprobación por base, con respaldo y un revisor que nombra el dueño | El despliegue de I1 en el ambiente donde haya filas |
| A2 | ¿Se verifican antes de I1, en los tres clústeres, el usuario de Mongo de la API (rol de sólo inserción sobre las bases por cooperativa) y que `AuditSignature` de producción no sea la clave `dev-v1`? | Sí, como tarea operativa; clave de anclas en una versión nueva | I1 en producción |
| A3 | ¿Qué proveedor tecnológico contrata COOFLOPAL (o Ingenia365 como integrador)? ¿Se acepta la lista corta The Factory HKA / Dataico para una prueba pagada en sandbox (p95 ≤ 5 s con 30 cajas; precio por ~150.000 documentos al mes), con credenciales por empresa y no de socio? | Sí; mientras tanto se desarrolla y se ensaya con `CanalSimulado` | El adaptador real de I4 y la salida |
| A4 | ¿Cómo factura electrónicamente COOFLOPAL hoy? ¿Qué resoluciones, prefijos y software asociado tiene? ¿Tiene resolución de contingencia y numeración del documento equivalente POS? | Se registran como dato en `/maestros/resoluciones-dian` | El ensayo de I4 |
| A5 | El correo al comprador, ¿desde qué buzón y dominio (SPF/DKIM del relay actual)? | Lo envía el ERP (`Dian.EntregaCorreo = Erp`) | La salida (I4) |
| A6 | Impresión: ¿PC con Chrome o Edge en modo kiosco (`--kiosk-printing`) o la app de Windows? ¿Impresoras de 58 u 80 mm? | Kiosco y 80 mm, con guía operativa | La salida (operativa), no el desarrollo |
| A7 | ¿COOFLOPAL vende productos con INC, impuesto de bolsas, ICUI o IBUA? | Semilla con IVA 19/5, exento, excluido, INC y bolsas por unidad; ICUI e IBUA como tarifas por unidad si aplican | El ensayo (semilla tributaria validada) |
| A8 | Antes del ensayo, ¿valida la contadora los tipos de comprobante (`FV`, `EI`, `SI`, `NV`, `CP`, `TR`, `AC`, `CJ`) y los cruces, los códigos DIAN sugeridos por clase de medio de pago (10, 48, 49, 42, 47, 20, 71…), las cuentas por medio y la semilla tributaria? | Se siembran marcados «pendiente de validar por la contadora» | El ensayo |
| A9 | ¿Vende COOFLOPAL productos pesados con códigos de balanza (EAN-13 con precio o peso)? | Fuera de alcance | No |
| A10 | ¿Acepta el dueño que, sin POS fuera de línea, una caída del ERP o de la red detenga la caja (la contingencia DIAN no la cubre)? | Sí (fuera de alcance por la spec) | No |

### B. Plataforma

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| B1 | ¿Se corrige en toda la plataforma `ICurrentUserService.UserId` (identidad central → `SEC_Users.Id`)? Arregla el cuatro ojos de Contabilidad y la auditoría «system», pero cambia comportamientos existentes. | No en esta feature: `IActorActual` (R5); el arreglo, como tarea aparte con prueba del cuatro ojos | No |
| B2 | Zona horaria: ¿`America/Bogota` para toda la plataforma, o por cooperativa? ¿Pasan ya Contabilidad y Nómina a la fecha local? | `America/Bogota` por configuración de plataforma; los demás módulos la adoptan aparte | No |
| B3 | ¿Cuánto se conservan las claves de idempotencia (`COR_OperationKeys`)? | Indefinidamente (nada se borra solo) | No |
| B4 | Candado del despachador: ¿Redis base 0, ranura Redis de la cooperativa o la base SQL? | Arrendamiento en la base de la cooperativa (R3) | No |
| B5 | ¿Pasa el despachador de correo al arrendamiento por cooperativa, para evitar correos dobles con 2 réplicas? | Sí, en I1 | No |
| B6 | ¿El lote manual responde 202 y corre en segundo plano, con el avance en la bandeja? | Sí | No |
| B7 | ¿Se excluyen del diff de auditoría los cambios técnicos de estado de las entregas (quedan la bitácora de intentos y el evento del comando)? | Sí | No |

### C. Auditoría y seguridad

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| C1 | ¿La cadena de sellos se extiende a Contabilidad, que tiene la misma retención de 10 años? | No por ahora: Inventario, sus piezas de plataforma y Navegación (R15) | No |
| C2 | ¿Basta el anclaje en SQL con HMAC, o se copia cada ancla a un almacén externo inmutable (S3 Object Lock)? | SQL + HMAC; la copia externa, después | No |
| C3 | ¿Se conserva indefinidamente la fila delgada (`Seq`, `Hash`) del outbox de auditoría, o se justifica una purga técnica? | Fila delgada indefinida | No |
| C4 | Monto máximo con varios roles: ¿el mayor o el menor? ¿Un rol sin fila de límite es «sin límite»? | El mayor; sin fila, sin límite | No |
| C5 | ¿Un usuario sin bodegas ni cajas asignadas no opera ninguna (falla cerrado), salvo con alcance total? | Sí | No |
| C6 | Niveles altos de aprobación: ¿dos permisos genéricos o cualquier código del catálogo? | Cualquier código del catálogo; `Approvals.Supervisor` y `Approvals.Management` como sugeridos | No |
| C7 | ¿Perfiles sugeridos como plantillas que se vuelven roles editables, o roles integrados fijos? | Plantillas | No |
| C8 | Alta desde el POS sin política de Habeas Data publicada: ¿se bloquea, o se permite con constancia y alerta? | Se permite, con la constancia «sin política vigente» y la alerta `Personas.SinPoliticaDeDatos` a quien tiene `Compliance.HabeasData.RecordConsent` | No |
| C9 | Aprobación de descuentos sobre el tope: ¿sólo remota, o también presencial con la passkey o el TOTP del supervisor? | Ambas, nunca con contraseña; WebAuthn en MAUI se prueba a mano | No |
| C10 | FR-009 dice que sin permiso la respuesta es la misma que para algo inexistente. Cuando el recurso ya es visible y el permiso que falta depende del cuerpo (costo indicado, aceptar la diferencia de activación, remisiones sin facturar, tipo fiscal sin paso, clave de parámetro), ¿422 con código propio o 404? | 422 con código propio: no revela existencia y le dice a quien opera qué le falta (precisión 11) | No |

### D. Catálogo, costeo y existencias

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| D1 | Largo del código de producto y del de bodega (códigos de SOLIDO, referencias de proveedor). | Producto 20 (`LargoLargo`); bodega y demás catálogos 10 | La publicación de las plantillas (I1) |
| D2 | Ámbito de costeo cooperativa cuando la contadora mapea bodegas del mismo grupo a cuentas de inventario distintas. | Ámbito cooperativa; la completitud avisa y exige una cuenta de inventario por grupo en ese ámbito | No |
| D3 | Residuo de redondeo: ¿a la línea o bodega de mayor valor, o a la última? | Mayor valor | No |
| D4 | Precio unitario de venta con 6 decimales o con 2. | Lista en pesos (18,2); precio de línea (18,6) | No |
| D5 | Prorrateo con promedio ponderado: la regla para separar lo vendido de lo existente. | Proporción = mín(1, existencia actual / cantidad recibida), validada por la contadora | I5 |
| D6 | Retroactivos con PEPS: ¿se admiten, o se restringen al promedio ponderado? | Sólo promedio ponderado | I5 |
| D7 | ¿El documento equivalente POS pertenece a la cadena de ventas de FR-075, y por eso comparte el modo de paso con la factura de oficina? | Sí (lectura literal de FR-075) | No |
| D8 | Puesta en marcha bodega por bodega con ámbito de costeo cooperativa: el saldo inicial de la segunda bodega queda antes de movimientos ya registrados en la primera. ¿Se exime ese saldo (y su anulación) de `Costeo.RetroactivosPermitidos`, o se exige `Costeo.Ambito = Bodega` mientras haya bodegas no activas? | Excepción de puesta en marcha: el motor lo inserta en su fecha, recalcula las salidas posteriores y registra `AjusteDeCostoReconocido` por documento afectado; entregado en I1 (R10, R19; caso dorado 17) | La salida de la segunda bodega |
| D9 | Los ajustes que genera un conteo aprobado, fechados en la foto, ¿dependen de `Costeo.RetroactivosPermitidos`? | No: se insertan en su lugar al promedio de su fecha, con el mismo retroactivo mínimo de I1 (R10, R18; precisión 10) | No |

### E. Compras e impuestos

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| E1 | ¿El catálogo de impuestos y retenciones queda en Core? | Sí (R21) | No |
| E2 | ¿Se lee la UVT de `PAY_LegalParameters` con un solo lector? ¿Quién la mantiene si la cooperativa no usa nómina? | Sí; la mantiene quien tenga el permiso de parámetros legales | No |
| E3 | Enmienda de la 008: ¿se agregan las marcas tributarias al maestro y al diálogo único? ¿Cómo se traducen `TaxRegime`, `SourceWithholding` e `IcaType` de SOLIDO? | Sí; la traducción la define la contadora | El ensayo (cargue de personas con retenciones) |
| E4 | ¿Código DANE en `COR_Cities` (con DIVIPOLA) y municipio en `COR_Branches`, como municipio por defecto de la operación? | Sí; las ciudades existentes se concilian por `LegacyCode` | No |
| E5 | Tolerancias del cruce: ¿porcentaje **y** valor, o cualquiera de los dos? ¿Excepción por proveedor? | Ambas condiciones; una sola tolerancia por cooperativa | No (I5) |
| E6 | En I1 (dos vías), ¿una diferencia de precio sólo ajusta el costo, o también se retiene? | Sólo ajusta el costo | No |
| E7 | Antes de I5, ¿cómo se trata el flete facturado aparte? | Al gasto; si viene en la misma factura, al costo de la recepción | No |
| E8 | Factura a crédito sin 030 y 032: ¿sólo alerta, o IVA «descontable condicionado»? | Sólo alerta | No |
| E9 | Notas parciales: ¿retenciones en proporción, con las tarifas del original y sin volver a probar la base mínima? | Sí | No |
| E10 | ¿Se guarda el XML de la factura electrónica del proveedor (ET art. 632), aunque la spec diga que sólo se lee? | No se guarda | No |
| E11 | Impuestos por unidad y varias tarifas de retención: ¿cuenta sin «exige base gravable», o enmienda de la regla 10 de la 009? ¿Una auxiliar por tarifa de ReteFuente y ReteICA? | Cuenta sin «exige base» para los de valor por unidad; una auxiliar por tarifa | No |

### F. Ventas, POS, caja y medios de pago

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| F1 | ¿Medios de pago, franquicias, adquirentes y datáfonos en Core, y en el POS sólo dónde se ofrecen? | Sí (R25) | No |
| F2 | Base de caja: ¿fondo fijo heredado, o base del día desde la caja fuerte? | Fondo fijo | No |
| F3 | ¿Las diferencias dentro de la tolerancia también se contabilizan? | Sí | No |
| F4 | Faltante: ¿a cargo del cajero (exige persona vinculada) o al gasto? ¿El sobrante siempre a su cuenta? | Gasto; el sobrante, a su cuenta | No |
| F5 | ¿La reclasificación entre medios es un movimiento de caja? | Sí | No |
| F6 | ¿Arqueo ciego opcional? | Parámetro, apagado | No |
| F7 | ¿Un cajero tiene una sola sesión abierta en toda la cooperativa? | Sí (parámetro) | No |
| F8 | Si la lista más específica no trae un producto, ¿se toma de la siguiente aplicable? | Sí | No |
| F9 | Promociones en conflicto: ¿gana la de mayor descuento? ¿Un descuento manual se suma a una promoción? | Mayor descuento; el manual no se suma | No (I6) |
| F10 | Segmento del asociado: ¿el texto de la clase, o un catálogo nuevo? | El texto de `AssociateClass`, validado contra los valores existentes | No |
| F11 | Bonos: ¿unicidad por medio? ¿Vuelven a quedar disponibles al anular o devolver? | Sí y sí | No |
| F12 | ¿El cierre del día se puede reabrir con permiso y motivo? | Sí | No |
| F13 | Auditoría del POS: ¿las acciones de riesgo, o cada lectura? | Las acciones de riesgo | No |
| F14 | ¿Un cobro en efectivo desde la oficina exige una sesión de caja abierta? | Sí | No |

### G. Contabilidad

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| G1 | ¿Un comprobante por documento con la venta y el costo, o dos? | Uno | No |
| G2 | La anulación, ¿refleja las cuentas del original (reglas a su fecha) aunque la matriz haya cambiado? | Sí | No |
| G3 | ¿Se bloquea una versión de regla con vigencia anterior a lo ya contabilizado? | Sí; los errores pasados se corrigen con un CG manual | No |
| G4 | ¿Quién ordena el lote manual y quién reprocesa? | Lote: `Accounting.InventoryBatches.Run`; reproceso y envío posterior: Inventario | No |
| G5 | ¿La conciliación ignora el alcance de sucursal contable del usuario? | Sí (sólo agregados por cuenta) | No |
| G6 | Cierre de mes con mensajes pendientes: ¿avisar o bloquear? | Avisar y exigir reconocimiento, con «Procesar ahora» | No |
| G7 | ¿Inactivar una cuenta usada por la matriz avisa o bloquea? | Avisa; la completitud la lista | No |
| G8 | Resumidos: ¿acepta la contadora el detalle por documento de las líneas de impuesto? | Sí (FR-077 lo exige) | No |
| G9 | Los relacionados de un documento por lotes, ¿siguen también su horario? | Sí | No |

### H. Cartera (IC y D-02)

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| H1 | ¿Basta el orden por cadena de documento, o la entrega a Cartera debe ir en orden estricto por persona? | Por cadena; `PersonPublicId` va en el mensaje | IC |
| H2 | ¿Quién registra la cuenta por cobrar, según la clase de crédito? | Comercial: Contabilidad; asociados: Cartera, con reclasificación de la provisional | IC |
| H3 | Crédito provisional: ¿plazos y cuotas por medio? ¿Asociado activo en el maestro? ¿Cliente con `IsCustomer`? | Plazos y cuotas como datos del medio; se exigen las dos marcas | No |
| H4 | ¿Acepta el dueño la lista de lo que debe cubrir la spec de Cartera (R26: consultas, recepción idempotente, obligaciones desde documentos comerciales, ajustes, crédito comercial, cuenta por cobrar, validación a la fecha, recaudo, IVA sobre la financiación, permisos y conciliación)? | Sí, como insumo de D-02 | IC |

### I. DIAN

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| I1 | ¿Cuántos segundos espera la caja la validación? Sin respuesta, ¿basta con «pendiente de entrega»? | 15 s; pendiente de entrega, sin comprobante provisional. La espera agotada cuenta como falla para `CircuitoDeCanal`: pasado `Dian.UmbralFallasCircuito`, las ventas nuevas abren la contingencia 03 y salen en papel en el acto (precisión 12) | No |
| I2 | Contingencia 03: ¿automática por circuito y manual, o sólo manual? ¿Quién presenta la constancia ante la DIAN? | Ambas; la cooperativa | No |
| I3 | ¿Se guarda el PDF del proveedor? ¿Almacenamiento frío a los 90 días para lo que se conserva 5 años? | No; sí | No |
| I4 | Clave técnica: ¿en la base de la cooperativa, enmascarada, o en el Secret? | En la base, enmascarada y fuera del diff | No |
| I5 | ¿«Obligada a facturar electrónicamente» = sí por defecto para toda cooperativa nueva? | Sí | No |
| I6 | Documento soporte: ¿por operación o semanal? ¿Y una cooperativa no obligada, con compras a no obligados? | Por operación; el DS sigue su propia norma, independiente del parámetro de venta | No |
| I7 | Si el comprador pide factura después del DEE POS, ¿se agrega el flujo nota de ajuste + factura? | **Resuelta en la spec** (FR-063): sí, en I4; contrato en `contracts/api.md` §18.3.1 | No |

### US4 · Regla de cantidad del saldo inicial (T325, pendiente)

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| T325 | La cantidad del saldo inicial de cada bodega (plantilla 14, FR-089), ¿es la del **conteo físico más o menos los movimientos que la bodega tuvo en SOLIDO entre el conteo y la fecha de corte**, o la bodega **deja de operar en SOLIDO desde el conteo hasta su activación** (bodega congelada) y la cantidad es la del conteo sin más? | Conteo ± movimientos de SOLIDO hasta el corte: la bodega sigue vendiendo en SOLIDO hasta la víspera de su activación y el archivo trae la cantidad ya corregida. La importación no distingue los dos casos (recibe la cantidad final); la regla la aplica quien arma el archivo. Registrada el 2026-09-26 en la implementación de US4, sin respuesta del dueño todavía: **rige la propuesta**. | No (la carga funciona con cualquiera de las dos; cambia el procedimiento de conteo de la guía de COOFLOPAL) |

### US11 · Tolerancia de reconteo y fecha del ajuste (T406, pendiente)

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| T406 | Para COOFLOPAL: ¿desde qué diferencia se exige reconteo —`Conteo.ToleranciaReconteoPorcentaje` y `Conteo.ToleranciaReconteoUnidades`, general o por bodega— y en qué fecha va el ajuste de un conteo aprobado —`Conteo.FechaDelAjuste`: la de la **foto** o la de la **aprobación**—? | Tolerancia 0 y 0 (toda diferencia se recuenta) y `Foto`. Registrada el 2026-09-26 en la implementación de US11, sin respuesta del dueño todavía: **rige la propuesta**. Los ajustes fechados en la foto sin depender de `Costeo.RetroactivosPermitidos` (D9) se preguntan en la fase de Polish, no aquí. | No (son parámetros con vigencia: se cambian sin desplegar) |

**Cómo se lee la tolerancia (decisión de la implementación, 2026-09-26).** Una diferencia se **tolera** si cabe en
cualquiera de las dos: hasta `ToleranciaReconteoUnidades` en valor absoluto, **o** hasta `ToleranciaReconteoPorcentaje` % del
teórico de la línea. Con las dos en cero toda diferencia distinta de cero exige reconteo. Así «tolerancia de 1 unidad» funciona
sola, sin que el porcentaje en cero la anule. Una línea de la foto que nadie contó cuenta 0 y también exige reconteo; la ronda 2
sólo admite las líneas que la ronda 1 —ya capturada— dejó fuera de tolerancia, y después de ella vale lo recontado aunque siga
fuera. Lo fija `ComparacionDeConteo` (casos `Domain.Tests/Inventory/Counts/Casos`).

## Riesgos

- **Ruta crítica de la salida.** COOFLOPAL necesita I1 → I2 → I3 → I4, y I4 depende sólo de un contrato
  con un proveedor externo, porque el servicio central de la 010 no existe (N3 sin empezar). La
  habilitación, la asociación de prefijos, la resolución de contingencia, la numeración del DEE y el set
  de pruebas son trámites ajenos al desarrollo. Mitigación: I4 se construye en paralelo sobre
  `CanalSimulado`, y la guardia fiscal dice qué falta (`readiness`).
- **Mucha plataforma antes del módulo.** I1 construye piezas que la plataforma no tiene y que el resto del
  ERP usará: contexto por cooperativa fuera de HTTP, actor, idempotencia, outbox, auditoría garantizada y
  sellada, parámetros, aprobaciones y alertas. Algunos cambios tocan módulos en producción: `AuditBehavior`,
  el interceptor, `CreatePersonCommand`/`PersonaDialog`, las notificaciones y el despachador de correo.
  Todo aditivo, con regresión en Nómina y Contabilidad y con la e2e de dos cooperativas.
- **Fallas silenciosas de contexto.** Una filtración del `AsyncLocal`, o un trabajo de fondo que olvide el
  ejecutor, escribiría en otra cooperativa o en la plantilla. Lo acotan `NingunTrabajoDeFondoOperaSinCooperativa`,
  la guarda de `MongoAuditService` y la e2e de dos cooperativas; endurecer la fábrica de `ErpTenantInfo` se
  decide aparte.
- **Rendimiento sin medir.** SC-001, SC-004, SC-009, SC-019, SC-020 y SC-025 dependen del cerrojo, de la
  validación previa (3 s), de la numeración compartida, de la latencia del proveedor y de los índices en
  los dos motores. SQL Server escala bloqueos a tabla por encima de unas 5.000 filas. Hay que medir en
  Testcontainers y en sandbox antes del ensayo, con Skip explícito en CI.
- **Guarda del retiro.** Si alguien parametrizó en las pantallas actuales, la migración destructiva se
  niega en ese ambiente. Diagnóstico en DEV, QA y PDN antes de fusionar, y decisión del dueño (A1).
- **Datos maestros incompletos.** Faltan el perfil tributario de las personas, las responsabilidades DIAN,
  la DIVIPOLA, el municipio de las sucursales, la persona vinculada a cada cajero y un segmento limpio en
  `AssociateClass`. Sin ellos, las retenciones salen mal o las ventas fiscales no se confirman. Completarlos
  es trabajo de la cooperativa antes del ensayo, y la guardia y la completitud dicen qué falta.
- **Semillas sin validar.** Los valores tributarios, los tipos de comprobante, los códigos DIAN de los
  medios de pago y los catálogos DIAN (códigos 20 y 94 del DEE, QR, notas de ajuste del DS) van marcados
  «pendiente de validar». Si se usan sin validar, el primer día sale mal.
- **Dos fuentes de tarifas (C8).** Las ReteFuente con tarifas distintas y el ICA por municipio exigen una
  auxiliar por tarifa; los impuestos por unidad, cuentas sin «exige base». La completitud lo muestra, pero la
  contadora tiene que decidirlo antes del ensayo.
- **Conciliación.** En ámbito de costeo cooperativa, las bodegas del mismo grupo mapeadas a cuentas
  distintas descuadran; un cambio de matriz dentro del período crea diferencias que sólo se explican por
  reclasificación; los mensajes rechazados de un ejercicio ya cerrado pueden quedar sin camino.
- **Crédito provisional.** La aprobación de cada venta a crédito puede frenar la caja; el recaudo va por
  fuera; las libranzas no descuentan estas ventas; si D-02 elige mal quién registra la cuenta por cobrar,
  se cuenta dos veces. IC es un trabajo grande sobre el código heredado de Lending.
- **Operación del POS.** No hay POS fuera de línea: una caída del ERP o de la red detiene la caja. La sesión
  de 30 minutos y el segundo factor obligatorio expulsan al cajero en la hora valle. Sin modo kiosco, cada
  tirilla abre el diálogo de impresión. La app MAUI (impresión nativa, WebAuthn en el WebView) no está
  verificada.
- **Seguridad de la auditoría.** La clave `dev-v1` escrita en el código y el usuario de Mongo de la API
  deben verificarse en los tres clústeres antes de I1 (A2). Un `*.View` mal nombrado filtra datos a todos los
  roles integrados. El defecto de `ICurrentUserService.UserId` sigue vivo fuera del módulo.
- **Volumen y costo**, por cooperativa a la escala de referencia:
  - `INV_KardexEntries`: unos 9 millones de filas al año;
  - `ACC_InventoryPostings`: de 3 a 4 millones al año;
  - el outbox de auditoría: del orden de 110 millones de filas delgadas en 10 años;
  - los artefactos DIAN: unos 270 GB al año, más unas 20.000 filas diarias de adjuntos;
  - la auditoría en Mongo, con retención de 10 años.

  Mitigaciones: índices por fecha y documento, cierres fijados, exclusiones de diff y almacenamiento frío.
- **Incertidumbre normativa.** Hay que confirmar con el proveedor o la contadora:
  - la contingencia del DEE POS;
  - si el DEE sólo se emite con software propio (Actualícese frente a Sovos);
  - la página de la DIAN que todavía dice 30 días para el 03;
  - el comprador que pide factura después del DEE;
  - el ICUI y el IBUA;
  - la conservación del XML del proveedor (art. 632).

  Todo plazo y todo código es dato con norma, para cambiarlo sin desplegar.
- **Enmienda de la 009 en la misma entrega.** Si los textos, el código y las pruebas de la 009 no se
  enmiendan con I2, la spec vigente guía mal al próximo módulo. Y si el retiro de I1 no quita en el mismo
  cambio las referencias de `AccountReferenceFinder` a `ProductAccounts` y `VatAccounts`, no compila.
- **Plantillas primero.** Publicar las plantillas en I1 amarra el modelo de cada catálogo: cambiar columnas
  después invalida lo que COOFLOPAL ya diligenció.
- **Alcance de la feature.** Son 101 FR, seis entregas y una pendiente (IC). I5 e I6 van después de la
  salida, y el riesgo es que su trabajo se cuele en la ruta crítica. La tabla de entregas de `plan.md` es la
  que manda.
- **Dos relojes.** Hasta que la plataforma adopte `HoyLocal`, Inventario usa la fecha local y Contabilidad
  y Nómina, `TodayUtc`. Un informe que cruce los dos puede dar fechas distintas para la misma noche.
- **Pruebas que pasan sin correr.** Las de volumen deben reportar un Skip explícito. CLAUDE.md documenta
  siete que hoy «pasan» con un `return` al principio.
