# Research: Contabilidad NIIF

**Feature**: 009 | **Date**: 2026-09-14

No quedó ningún `NEEDS CLARIFICATION` en el Technical Context: las ocho decisiones del dueño
están en `spec.md › Clarifications`, y lo demás se resolvió leyendo el código (cuatro mapas de
exploración del 2026-09-14) y la norma pública. Cada punto sigue Decision / Rationale /
Alternatives.

## Lo que se encontró en el código (resumen de los mapas)

- **Escritores contables**: 16 lugares escriben `AccountingDocument`/`JournalEntry`, todos en
  Application. **Sólo cuatro escriben líneas** (`CreateDocument`, `DisburseLoan`,
  `ProcessPayment`, `PayrollAccountingPoster`); los otros doce dejan **cabeceras sin líneas**
  (CDT, Cartera causación/clasificación/descuentos/ahorros, Inventario, Tesorería, cierre). Cuatro
  esquemas de numeración (`NextSequenceNumber`, `MAX+1`, `0`, `1`); dos sitios comprueban período
  abierto; sucursal y centro de costo son «el primero por Id» en todas partes; sólo Nómina
  reversa con espejo y guarda en la misma transacción. `VoidInventoryDocument` anula **cualquier**
  documento INV (sin vínculo al de inventario). Ninguna infraestructura ni endpoint escribe: un
  contrato en Application intercepta el 100 %.
- **Parametrizaciones de cuentas**: sólo `PayrollConceptDefinitionAccount` referencia la cuenta
  por FK; el resto (`CreditLineParameter`, `ProductAccount`, `VatAccount`, `SavingsParameter`,
  `CdtParameter`, `Bank`, `VoucherType`) guarda **códigos sueltos**, y varias nunca se leen.
  `TreasuryConcept` no tiene cuentas.
- **Auditoría**: `AuditBehavior` sólo ve tipos `*Command`; hay carril inmediato
  (`IAuditAppendOnlyWriter`, molde `PayrollAuditEmitter`), catálogo `AuditEventTypes`, TTL 5 años
  en `AuditIndexBootstrap`, consola `/admin/auditoria` con export CSV/PDF. `LogAccessAsync` existe
  y **nadie lo llama**; no hay `LocationChanged` en Blazor.
- **Informes**: `TablaExportable` (en `Application.Payroll.Reports`), `ExportadorDeTablas`
  (xlsx/docx/pdf, `es-CO`), `Entregar()` en `PayrollReportsEndpoints` como punto único; el cliente
  descarga con `DescargaDeArchivos` (`eval` + data URI) o `downloadFromStream` (Web.Client).
- **Adjuntos** genéricos por `OwnerEntityType + OwnerEntityPublicId`, cifrados por adjunto;
  `IEmailSender` con adjuntos y molde `PayslipEmailDispatcher`.
- **Permisos**: ninguna ruta de `Endpoints/Accounting` exige permiso; el molde es
  `CorePermissionCatalogSeeder` + patrones de `BuiltInRolesSeeder` (`*.View` para todos;
  `LecturaDeMaestros` para roles personalizados); `RequirePermission` encadenado = AND; sin
  permiso → 404.
- **Persistencia**: 33 tablas `ACC_`, `AuditableEntity/Long`, filtro soft-delete global,
  `RowVersion` por convención (`rowversion` / `xmin`), decimales `18,2` globales,
  `ConcurrencyConflictException` **no la atrapa nadie** (sale como 500 en vez del 409 prometido).
  Migraciones en par; guarda `MIGRACION-DESTRUCTIVA-APROBADA` para `Drop*`. Semillas en arreglos
  C# (sin recursos embebidos); `ChartOfAccountsSeeder` deja sólo las 9 clases.
- **Sucursales**: `COR_Branches` es la sucursal operativa (la usan `JournalEntry`, `Employee`);
  `SEC_UserBranchAssignments.BranchId` apunta a `ADM_Branches` (otra base) y **nada lo hace
  cumplir**; el claim `branch_id` no se emite.
- **Blazor**: menú estático sin permisos; no existe ninguna grilla editable ni navegación por
  teclado en el repo; `Syncfusion.Blazor.TreeGrid` no está referenciado; `PrincipioVIII` excluye
  `Accounting` explícitamente; `LasPantallasDicenQueEstanCargando` no incluye `Contabilidad`;
  `NominaE2E.PrepararAsync` deja una cooperativa con plan mínimo, 12 períodos 2026 y cuentas por
  concepto.

## Norma (fuentes públicas)

- **PUC Comercial**: Decreto 2650 de 1993 — clases, grupos, cuentas y subcuentas obligatorias;
  auxiliares desde el séptimo dígito; con la Ley 1314 de 2009 cada entidad define su catálogo,
  pero el 2650 sigue siendo la referencia de codificación ([niif.com.co](https://niif.com.co/decreto-2650-1993/),
  [nexiamya](https://nexiamya.com.co/vigencia-decreto-2650-de-1993-puc/)).
- **Catálogo solidario NIIF**: Resolución 2015110009615 del 13 de noviembre de 2015 de la
  Supersolidaria, «Catálogo Único de Información Financiera con Fines de Supervisión», con
  actualizaciones posteriores publicadas en SICSES
  ([accounter.co](https://accounter.co/niif/resolucion-2015110009615-puc-catalogo-unico-de-informacion-financiera-con-fines-de-supervision-niif-sector-solidario.html),
  [Supersolidaria](https://www.supersolidaria.gov.co/es/content/conoce-mas-acerca-de-las-niif)).
- **Exógena**: Resolución Única DIAN 000227 de 2025 (año gravable 2025, 69 formatos), modificada
  por las Resoluciones 000012 y 000021 de 2026; archivos XML validados por el XSD de cada
  formato, ISO 8859-1, nombre `Dmuisca_…` de 33 caracteres; el prevalidador es la herramienta de
  validación ([Siigo](https://www.siigo.com/blog/obligaciones-fiscales/informacion-exogena/),
  [siemprealdia](https://siemprealdia.co/colombia/impuestos/resolucion-000012-de-2026-cambios-en-exogena/),
  [DIAN — preguntas frecuentes](https://www.dian.gov.co/Transaccional/GuaServiciosLinea/Preguntas_Exogena.pdf)).
  La resolución **del año gravable 2026** normalmente se expide a fin de 2026: la semilla arranca
  con la 000227/2025 vigente y se actualiza como dato (FR-071).

## R1. Reemplazar el núcleo contable, no adaptarlo

- **Decision**: una migración en par `ContabilidadNiif` que **elimina las 33 tablas `ACC_`** y crea
  el esquema nuevo (data-model.md), conservando los nombres de entidad que otros módulos
  referencian por FK (`ChartOfAccount`, `AccountingDocument`, `VoucherType`, `AccountingPeriod`)
  para que `PAY_PayrollRuns.AccountingDocumentId` y
  `PAY_PayrollConceptDefinitionAccounts.Debit/CreditAccountId` sigan válidos. `Up()` empieza con
  una **guarda**: si `ACC_JournalEntries` o `ACC_Documents` tienen filas, la migración falla con
  un mensaje (`RAISE EXCEPTION` / `THROW`) y no borra nada. Cabecera con
  `MIGRACION-DESTRUCTIVA-APROBADA`, referencia del respaldo y segundo revisor (Principio XII).
- **Rationale**: los libros están vacíos en los cuatro ambientes (verificado el 2026-09-14);
  adaptar 33 tablas heredadas con más de cincuenta columnas muertas cada una costaría más que
  crearlas bien y dejaría el modelo de saldos por columnas-mes que la spec prohíbe (FR-046). La
  guarda protege producción si alguien contabiliza antes del despliegue.
- **Alternatives considered**: (a) alterar columna a columna: migración de 800 líneas ilegible y
  el `Down` imposible; (b) crear tablas nuevas con otro nombre y dejar las viejas: dos libros y
  las FK de Nómina apuntando al viejo; (c) migrar datos: no hay datos.

## R2. Un contrato de contabilización que agrega sin guardar

- **Decision**: `Application/Accounting/Posting/AccountingPoster` (Scoped) con
  `PrepareAsync(PostingRequest, ct) → Result<AccountingDocument>` y
  `PrepareReversalAsync(AccountingDocument original, DateOnly fecha, string motivo, ct)`. Valida
  todo (FR-014, FR-020, FR-021), construye documento y líneas, asigna número y **los agrega al
  contexto sin guardar**; el handler que llama (Nómina, Cartera, digitación…) cambia su propio
  estado y hace **un** `SaveChangesAsync`. Las reglas de línea viven en **una** clase,
  `AccountLineRules`, que usan `AccountingPoster` y la validación de borradores.
- **Rationale**: es exactamente el molde de `PayrollAccountingPoster` (que «deliberadamente no
  guarda») y de las fábricas de la 008 (`PersonFactory`), y da la atomicidad de FR-036 sin
  transacción explícita (`IApplicationDbContext` no la expone). Una sola clase de reglas es lo que
  garantiza «las mismas reglas» de FR-014 por construcción, no por disciplina.
- **Alternatives considered**: (a) `sender.Send(CreateDocumentCommand)` anidado: guarda y audita
  aparte, rompe la atomicidad; (b) eventos de dominio procesados después: el módulo quedaría
  aprobado sin comprobante si el evento falla; (c) exponer `BeginTransaction`: contrato transversal
  nuevo para un caso ya resuelto por navegaciones EF.

## R3. Numeración consecutiva bajo concurrencia

- **Decision**: `VoucherType.NextNumber` se lee con seguimiento, se incrementa en memoria y se
  guarda en el mismo `SaveChangesAsync` que el documento; el índice único
  `(VoucherTypeId, Number)` (filtrado a `Number IS NOT NULL`) hace imposible el duplicado, y el
  `RowVersion` del tipo de comprobante convierte la carrera en `ConcurrencyConflictException`.
  Nuevo `ReintentoPorConcurrenciaBehavior` (MediatR) reintenta hasta 5 veces, con `jitter`, los
  requests marcados con `IReintentableAnteConcurrencia`, tras `IApplicationDbContext.DescartarCambios()`
  (método nuevo, `ChangeTracker.Clear()`); las excepciones que sobreviven se mapean a
  `Concurrency.StaleRowVersion` → 409 en el manejador global (cierra el hueco encontrado: hoy
  salen como 500). Los borradores no tienen número; se asigna al contabilizar (FR-020).
- **Rationale**: sin transacciones explícitas, la única forma de «sin huecos» es asignar el
  número en la misma escritura que lo consume; el reintento en servidor cumple el escenario 9 de
  US3 sin que el usuario vea un error. El comportamiento es genérico y sirve también a
  `ApprovePayrollRun`/`ReversePayrollRun`.
- **Alternatives considered**: (a) `SEQUENCE` por tipo de comprobante: DDL por proveedor desde un
  handler y huecos si la operación falla después de tomar el número; (b) `ExecuteUpdate` sobre una
  tabla de contadores: se confirma fuera del `SaveChanges` (huecos en fallo); (c) devolver 409 y
  que el usuario reintente: viola el escenario 9.

## R4. Saldos derivados, sin tablas de saldos

- **Decision**: no existen `ACC_AccountBalances`, `ACC_ThirdPartyAccounts`, `ACC_AuxiliaryDocuments`
  ni `ACC_RiskCategories`. Todo saldo es `SUM(Debit) - SUM(Credit)` sobre `ACC_JournalEntries`
  filtrado por `IsPosted`, con índices `(AccountId, Date)`, `(PersonId, AccountId, Date)`,
  `(BranchId, Date)`, `(CrossDocumentTypeId, CrossDocumentNumber, PersonId)` y `(DocumentId)`. La
  línea desnormaliza `Date` e `IsPosted` del documento (los escribe el mismo handler al
  contabilizar) para agregar sin `JOIN`. La jerarquía se consolida en memoria (el plan tiene miles
  de cuentas, no millones). Un resumen mensual materializado queda **diferido** hasta que una
  medición real supere SC-008; la prueba `ElBalanceDePruebaCuadra` (integración) vigila la
  invariante en cualquier caso.
- **Rationale**: FR-046 y SC-006 exigen que ningún saldo pueda divergir del libro; el modelo
  heredado (columna por mes y por día) es exactamente lo que diverge. PostgreSQL agrega un millón
  de filas indexadas por cuenta y rango en menos de un segundo.
- **Alternatives considered**: tablas de saldos mantenidas por el poster (doble escritura, deriva
  ante cualquier fallo parcial); vistas materializadas (refresco y bloqueo por proveedor).

## R5. Catálogos como recursos embebidos JSON e importador propio

- **Decision**: `puc-comercial.json` y `puc-solidario.json` (código, nombre, nivel, naturaleza,
  rubro NIIF) como `EmbeddedResource` en `IngenIA365ERP.Persistence/Seeding/Parametric/Data/`,
  cargados por `AccountCatalogsSeeder` a `ACC_AccountCatalogs`/`ACC_AccountCatalogEntries` de
  **cada base de cooperativa** (un `SELECT` de claves existentes + un `SaveChanges`, patrón de
  `PayrollConceptDefinitionsSeeder`). Una prueba de Persistencia valida cada JSON (padre
  existente, longitudes 1/2/4/6, naturaleza válida, rubro existente). El importador
  (`ImportAccountCatalogCommand`) recibe el archivo tabular ya leído (R13), aplica las mismas
  reglas fila por fila y crea un catálogo `Source = Imported`. Nueva convención documentada en
  `docs/manual/semillas-json.md`.
- **Rationale**: ~1.500 filas × 2 catálogos en arreglos C# no las revisa ningún contador; el JSON
  se abre en cualquier editor y la validación del contador en QA (FR-001) es sobre ese archivo.
  Sembrar en cada base respeta el Principio IV (no hay base compartida ni cruce).
- **Alternatives considered**: arreglos C# (ilegibles a esa escala); tabla global en la base
  administrativa (cruzaría bases; prohibido); leer el JSON desde disco en tiempo de ejecución
  (frágil en contenedor).

## R6. Rubros NIIF y estados financieros como datos

- **Decision**: `ACC_FinancialStatementItems` sembrado desde `rubros-niif.json` por grupo NIIF
  (estado, sección, orden, signo); cada entrada de catálogo lleva `NiifItemCode`; las auxiliares lo
  heredan. Los cuatro estados (ESF, ERI, cambios en el patrimonio, flujo de efectivo indirecto) se
  arman recorriendo rubros → cuentas → saldos; ningún rubro está en código.
- **Rationale**: FR-047 y la regla de la casa de no escribir valores de negocio en código (la
  prueba `LaNominaNoTieneValoresLegalesFijos` es el precedente).
- **Alternatives considered**: prefijos de cuenta en código (`StartsWith("36")`, como el cierre
  heredado): se rompe con el catálogo solidario, cuyos códigos difieren.

## R7. Sucursal contable y alcance por usuario

- **Decision**: la sucursal contable es `COR_Branches` (tenant). `AccountingSetup.MainBranchId` es
  la principal; toda línea lleva `BranchId` (FR-014). Para FR-035, `COR_Branches` gana
  `TenantBranchPublicId` (Guid?, único filtrado) que la enlaza con la oficina registrada en
  `ADM_Branches`; `IUserBranchScope` (Application) lo implementa la API leyendo
  `SEC_UserBranchAssignments` del usuario y traduciendo por ese vínculo; sin asignaciones =
  sin restricción; con asignaciones sin vínculo = sin sucursales en alcance (falla cerrado). La
  propuesta de sucursal en la digitación es la `IsDefault` del usuario, si existe, o la principal.
- **Rationale**: no puede haber FK entre bases (Principio IV); la asignación existente apunta a la
  base administrativa y hoy no la aplica nadie; un vínculo por `PublicId` es un dato, no código.
- **Alternatives considered**: reapuntar `SEC_UserBranchAssignments` a `COR_Branches` (rompe
  `AssignBranch` y la ficha de usuario); duplicar la asignación en una tabla nueva (dos verdades).

## R8. Terceros institucionales

- **Decision**: `PersonId?` (FK `COR_People`) en `PAY_HealthInsuranceProviders`,
  `PAY_WorkRiskProviders`, `PAY_PensionProviders`, `PAY_SeveranceProviders`,
  `PAY_FamilyCompensationFunds` y `COR_Banks`; los diálogos de esos catálogos muestran un
  `PersonSearchPicker` («Persona que la representa como tercero», con creación en línea). La
  parametrización de cuentas de nómina valida el vínculo (FR-088); `PayrollAccountingPoster`
  envía `PersonId` en las líneas de aportes y retenciones (hoy no manda tercero).
- **Rationale**: decisión del dueño (Clarifications) y Principio V: el tercero es la persona.
- **Alternatives considered**: crear la persona automáticamente al crear la entidad (opción C
  descartada por el dueño); no exigir tercero en esas cuentas (opción B descartada).

## R9. Cuatro ojos

- **Decision**: `AccountingDocument.RegisteredByUserId` (`ICurrentUserService.UserId`, `int?` del
  usuario de la cooperativa) y `PostedByUserId`; `PostDocumentCommandHandler` rechaza con
  `Accounting.Document.FourEyes` cuando `AccountingSetup.FourEyes` y ambos coinciden. La
  anulación (reversión) se contabiliza en el acto con `Accounting.Vouchers.Void`; no le aplica.
- **Rationale**: comparar identidad de usuario, no nombre; el flag es por empresa y auditado.

## R10. Auditoría del ingreso a cada opción

- **Decision**: `RegisterOptionAccessCommand(string Ruta, string Titulo)` (Application/Audit) con
  validador, sin efecto en SQL: su única traza es el evento que `AuditBehavior` escribe por ser
  un Command (`Action = "RegisterOptionAccess"`, módulo `Navigation`: el módulo lo infiere `AuditBehavior` del
  namespace, así que se agrega ese mapeo en `InferModuleFromNamespace`). Endpoint
  `POST /api/audit/access` (sesión + cooperativa). En el cliente, `RegistroDeAccesos` (Scoped,
  Shared) se suscribe a `NavigationManager.LocationChanged` en `MainLayout`, deduplica la misma
  ruta consecutiva, encola y envía con reintento (3 intentos, `backoff`); si agota, registra con
  `ILogger` y muestra **un** aviso por sesión. La consulta es la consola existente
  (`/admin/auditoria`) con el filtro `Module=Navigation`, que ya exporta.
- **Rationale**: es el único carril que pasa por el pipeline sin código nuevo de infraestructura;
  reutiliza consola, permisos (`AuditLog.View/Export`) y TTL. `LogAccessAsync` (colección
  `access_log`) no tiene consulta ni índice: quedaría como segundo carril muerto.
- **Alternatives considered**: middleware de servidor por ruta HTTP (no ve la navegación de una
  SPA); `LogAccessAsync` (sin consulta, sin TTL).

## R11. Informes y exportación

- **Decision**: `TablaExportable`, `ColumnaExportable`, `FilaExportable` y `TipoDeColumna` se
  mueven a `Application/Common/Reports` (los `using` de nómina se actualizan; los nombres no
  cambian). `Entregar()` sale de `PayrollReportsEndpoints` a `API/Reports/EntregaDeInformes` y lo
  usan nómina y contabilidad. Cada query de informe contable lleva `Formato` y, cuando no es
  `json`, su handler emite `Accounting.Report.Exported` por `AccountingAuditEmitter` (molde
  `PayrollAuditEmitter`, precedente `ExportRunQuery`). Los estados y libros son
  `IRequest<Result<TablaExportable>>` en `Application/Accounting/Reports`.
- **Rationale**: FR-043/FR-045/FR-050 con el exportador ya probado; el evento en el handler es
  la única forma de auditar una query.
- **Alternatives considered**: un comando de exportación aparte (duplica la consulta); auditar en
  el endpoint (sin `ISender` ni emisor en esa capa).

## R12. Interfaz: árbol y grilla de líneas

- **Decision**: se agrega `Syncfusion.Blazor.TreeGrid` 33.2.8 a `Shared.csproj` (regla de la
  casa: paquete por componente, misma versión) para el plan de cuentas y para el libro auxiliar
  con profundización por demanda (`SfTreeGrid` con carga de hijos al expandir). La grilla de
  líneas del comprobante es un componente propio, `LineasDeComprobante.razor`: tabla `.data-grid`
  con `SfComboBox`/`SfNumericTextBox`/`PersonSearchPicker Compacto` por celda, `@onkeydown`
  (Tab, Enter, Ctrl+D duplicar, Ctrl+B balancear, F2 ayuda) y foco por `ElementReference`;
  sienta el precedente (no existe ninguna grilla editable en el repo). `ContabilidadClient`
  (partial por área) sigue el molde de `NominaClient`.
- **Rationale**: FR-031 (teclado de principio a fin) no lo da un `SfGrid` en modo edición sin
  pelear con su ciclo de vida; el `TreeGrid` sí resuelve exactamente FR-042.
- **Alternatives considered**: `SfGrid` editable (sin precedente, ciclo de foco opaco);
  `SfTreeView` (sin columnas ni totales).

## R13. Archivos tabulares de entrada

- **Decision**: `ITabularFileReader` en `Application/Common/Interfaces/Files` (`LeerAsync(bytes,
  nombre) → TablaLeida` con filas de celdas texto); implementación `ClosedXmlTabularFileReader` en
  `API/Reports/Importadores` (ClosedXML ya está en API) registrada como `IPayslipPdfRenderer`; CSV
  y texto separado los lee la misma clase con BCL. La usan el importador de catálogo (R5), la
  apertura (FR-085) y el extracto bancario (FR-056).
- **Rationale**: Application no referencia ClosedXML (Principio II); tres importadores, un lector.

## R14. Exógena

- **Decision**: formatos, conceptos y su mapeo a cuentas son datos por año
  (`ACC_ExogenousFormats/Concepts/ConceptAccounts`), sembrados desde `exogena-2026.json` (todos
  los formatos de la Resolución 000227/2025 vigente, editable; los que no aplican se marcan). La
  generación produce `ACC_ExogenousRuns` + líneas con instantánea de identificación del tercero
  e inconsistencias; el XML se arma con `System.Xml` (BCL, en Application) según la estructura
  `Dmuisca` y se guarda como adjunto del run (`OwnerEntityType = "ExogenousRun"`); el XSD **no se
  valida en el ERP** (lo hace el prevalidador, SC-012 se verifica en QA). Excel por
  `ExportadorDeTablas`.
- **Rationale**: FR-071..075 y la decisión del dueño (semilla completa); las estructuras cambian
  cada año y deben ser dato.

## R15. Activos fijos y diferidos

- **Decision**: método de línea recta con valor residual; calendario de cuotas
  (`ACC_FixedAssetInstallments`) generado al registrar y **regenerado sólo hacia adelante** al
  cambiar vida útil o residual; la corrida mensual es un documento con origen `ACT` y
  `ACC_AssetRuns(PeriodId único)` garantiza una por período; reversión por `PrepareReversalAsync`
  desde la misma opción; baja con comprobante propio.
- **Rationale**: FR-076..082; la unicidad por período es una restricción de base, no una
  comprobación.

## R16. Los módulos que hoy «contabilizan»

- **Decision**: cada módulo pasa por `AccountingPoster` con la parametrización **que ya tiene**:
  Nómina (`PayrollConceptDefinitionAccount`, ya por FK), Cartera (`CreditLineParameter.*` y
  `SavingsParameter.TreasuryAccount`, por código → se resuelven a `AccountId` y se valida
  elegibilidad al guardar la parametrización), Inventario (`ProductAccount`/`VatAccount` por
  código, hoy nunca leídos), CDT (`CdtParameter.TreasuryAccount`), Tesorería (**`TreasuryConcept`
  gana `DebitAccountId`/`CreditAccountId`**, hoy no tiene cuentas), bancos (`COR_Banks.AccountingAccountCode`
  → `AccountId`). Cuando falta parametrización, la operación falla con
  `Accounting.Parameterization.Missing` nombrando qué configurar (molde
  `Payroll.ConceptWithoutAccounts`); **nunca se omite en silencio** (hoy `DisburseLoan` salta si
  falta el tipo `EG`). Los documentos de módulo guardan `SourceType/SourcePublicId`, y la
  anulación del módulo reversa por ese vínculo (corrige el bug de `VoidInventoryDocument`). Las
  cuentas «11100501» cableadas desaparecen: banco por parametrización.
- **Rationale**: FR-036..041; no se inventa contabilidad para módulos que no la tienen definida
  (sería decidir política contable por ellos): se conecta lo que existe y se hace visible lo que
  falta.
- **Alternatives considered**: diseñar la contabilidad completa de Cartera/Inventario/Tesorería
  aquí (cada una es una feature); dejar los escritores directos (viola FR-039 y la prueba
  `NingunModuloEscribeMovimientosFueraDelContrato`).

## R17. Entrega por etapas dentro de la feature

- **Decision**: cuatro entregas, cada una mergeable a `develop` y desplegable a QA:
  **E1 núcleo** (US1, US2, US3, US4-Nómina, US7, migración, permisos, auditoría de accesos);
  **E2 consultas, cierres y apertura** (US5, US6, US13); **E3 módulos restantes** (US4.7:
  Cartera, Inventario, Tesorería, CDT); **E4 satélites** (US8–US12). `tasks.md` las refleja como
  fases; las ramas son `009-contabilidad-niif` (E1) y `009-e2…`, `009-e3…`, `009-e4…` desde
  `develop`.
- **Rationale**: 13 historias y 88 requisitos no caben en un PR revisable; Nómina necesita
  contabilidad real cuanto antes (es lo que está en producción).

## R18. Pruebas de arquitectura y alcance

- **Decision**: `PrincipioVIII_DualValidation.InScopeNamespacePrefixes` +=
  `IngenIA365ERP.Application.Accounting` (y `Audit` para el comando de accesos);
  `LosMaestrosDePersonaExigenPermiso.Archivos` += todos los `Endpoints/Accounting/*.cs` (renombrar
  la prueba a `LosEndpointsProtegidosExigenPermiso` para que el nombre sea cierto);
  `LasPantallasDicenQueEstanCargando.ModulosMigrados` += `Contabilidad` **en E1**, porque las 22
  pantallas heredadas se retiran en esa misma entrega (FR-083); nueva
  `NingunModuloEscribeMovimientosFueraDelContrato` (ningún archivo fuera de
  `Application/Accounting/Posting` hace `AccountingDocuments.Add`/`JournalEntries.Add`/`new
  AccountingDocument`/`new JournalEntry`); nueva `LaContabilidadNoTieneCuentasEnCodigo` (ningún
  literal de código de cuenta en `Application/**` salvo semillas y pruebas); `PrincipioXI`
  cubre `Entities.Accounting.Transactions` por convención de namespace: documento y líneas viven
  ahí.

## R19. Ejercicio, períodos, apertura y cierre

- **Decision**: `ACC_FiscalYears` (año calendario) con doce `ACC_AccountingPeriods`; se crean al
  iniciar la contabilidad y al abrir cada ejercicio (`OpenFiscalYearCommand`, exige el anterior
  cerrado o ser el primero). La apertura es `Kind = Opening`, sin período, fechada el día anterior
  al primer período, única (`AccountingSetup.OpeningDocumentId`). El cierre anual es `Kind =
  Closing` (tipo reservado `CIE`), fechado el último día, y `EstadoDeResultadosQuery` lo excluye
  salvo `IncluirCierre`. Reabrir el ejercicio reversa el cierre.
- **Rationale**: FR-021..024, FR-084..087; un ejercicio explícito evita el `PeriodCode` numérico
  heredado y el «cierre número 1» que colisionaba cada año.

## R20. Certificados desactualizados

- **Decision**: al expedir, el certificado guarda `LedgerFingerprint` = hash de (conteo, suma de
  bases, suma de valores) de las líneas que lo alimentan; una consulta compara con el libro
  actual y marca `Outdated`; reexpedir crea uno nuevo con consecutivo nuevo y deja el anterior
  `Voided` con referencia.
- **Rationale**: FR-068 sin recorrer la auditoría; determinista y barato.
