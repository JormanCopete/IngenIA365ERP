# Implementation Plan: Recaudo y Tesorería — caja, medios de pago, bancos, pagadurías y giros

**Branch**: `015-recaudo-tesoreria` | **Date**: 2026-10-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-recaudo-tesoreria/spec.md`

## Summary

La feature construye los dos módulos que faltan para que Cartera (013) y Captaciones y aportes (014), ya desplegadas
en producción sin activar, puedan activarse: **Recaudo**, dueño de la plata que entra (caja, medios de pago, recibo
multiproducto, bancos, pagadurías, cheques recibidos, SARLAFT del efectivo), y **Tesorería**, dueña de la plata que
sale y de los bancos (cuentas propias, órdenes de giro, lotes y archivos de pago, cheques girados, remesas,
conciliación). COOFLOPAL sale en vivo el **01/12/2026** con las entregas mínimas **R0 y T0**; R1/T1 completan y R2/T2
amplían. La Tesorería heredada convive hasta T2 y se retira después con guarda destructiva.

**Qué construye R0/T0** (el recorte del dueño):
- **Caja**: puntos, cajas, cajeros, bóveda (o caja principal como bóveda), sesión con base, arqueo ciego por medio,
  diferencias aprobadas, cierre del día y contingencia por talonario.
- **Recibo multiproducto** por los contratos de Cartera y Captaciones, con efectivo y transferencia (cheque por
  parámetro), todo o nada, idempotente, con la elección del deudor de la Ley 2032; anulación y avisos de reverso de los
  módulos; devolución de cheques con reverso por porción.
- **Bancos**: extracto cargado o digitado, identificación manual, partidas por identificar.
- **Pagadurías**: archivo de descuentos, confirmación de envío, respuesta, giro y no aplicados.
- **Tesorería**: cuentas propias con fondo de liquidez restringido, bandeja única de órdenes, lotes por transferencia
  (archivo o portal del banco) y cheque mínimo, aprobación por monto, resultado del giro y regiro, controles de cuenta
  destino y archivo alterado, remesas, conciliación mínima.
- **Transversal**: SARLAFT básico (umbrales con vigencia, declaración, quien efectúa, listas vinculantes que bloquean,
  fraccionamiento), GMF en egresos por cuentas contables, hechos y matrices para Contabilidad, registro como participantes,
  **retiro de los dobles de prueba** con la migración de las e2e de Cartera y Captaciones, seguridad y roles sembrados,
  puesta en marcha con cuadre provisional, lista de go/no-go.

**Cómo**:
- **Sólo por contratos.** Recaudo llama a `IContratoDeObligaciones` y a `IContratoDeCuentas` dentro de **su**
  transacción, un lote por contrato, **obligaciones antes que cuentas** (FR-170); Tesorería consume `OrdenDeGiroEmitida`
  v1/v2 y responde `ResultadoDelGiro` v1. Ningún contrato vigente cambia en R0/T0; la v2 de los dos contratos (partida
  citada) llega en R1 como enmienda.
- **Dos módulos con frontera.** `Receipts` (`REC`, `RCD_`) y `Treasury` (`TES`, `TES_`) se hablan por puertos síncronos
  en la misma unidad de trabajo; ninguno toca las tablas del otro. El extracto bancario se guarda **una vez**, en
  Tesorería, y Recaudo identifica sobre sus líneas.
- **Operación de caja → recibo.** La caja prepara una operación con `PublicId` estable (será el del recibo); confirmar
  pasa por un solo flujo (`ConfirmacionDeRecibo`): listas → validación previa contable → cerrojos en orden fijo →
  numeración → contratos → hechos → un `SaveChanges` (la numeración del recibo va después de los cerrojos propios de Recaudo y **antes** de los contratos (decisión del dueño del 2026-10-23: el contrato guarda el número del recibo en su operación); en la misma transacción, un rechazo no consume número). Las claves de los contratos son por porción
  (`REC:{recibo}:{aplicación}:{clase}`).
- **Procesos no interactivos por línea**: cada línea de un extracto o un giro de pagaduría va en su transacción con su
  clave; un rechazo no tumba las demás. Ningún proceso aplica pagos ni informa resultados.
- **Contabilidad sin cuentas**: dos emisores nuevos en `DestinoContabilidad`, dos matrices (`ACC_ReceiptsPostingRules`,
  `ACC_TreasuryPostingRules`) con el molde de Cartera, y el vínculo cuenta propia ↔ cuenta contable en Contabilidad
  (`ACC_OwnBankAccountLinks`); recibos de caja por lotes resumidos.
- **Plataforma reutilizada**: bandeja, registro de participantes, idempotencia, parámetros con vigencia, aprobaciones
  (presente en caja, diferida en Tesorería), alertas, alcance, plantillas, auditoría encadenada, archivos planos
  (`FlatFileWriter`/`FlatFileReader`, ámbitos nuevos), catálogo de medios y denominaciones de la 012. Lo nuevo de
  plataforma: cuentas bancarias de la persona con doble control (`COR_PersonBankAccounts`) y la lista de go/no-go.

Detalle de cada decisión en [research.md](research.md) (R-01 a R-40). Los nombres canónicos, las decisiones TR1–TR43,
los choques CH1–CH16 y las preguntas Q1–Q10 están en [decisiones-transversales.md](decisiones-transversales.md), que
manda sobre cualquier otra redacción. Las cinco decisiones del dueño del 2026-10-23 (auditoría de las corridas, migraciones
congeladas el 10/11, conciliación única, prefijos por enmienda de la constitución, desviación III aprobada) están en
`## Clarifications` de la spec.

## Technical Context

**Language/Version**: C# / .NET 10.0.5 (API, Domain, Application, Infrastructure). Blazor (Shared, Web, Web.Client y la
app MAUI) con Syncfusion 33.2.8 por paquetes de componente (los de
`Shared.csproj`; si la caja necesita otro componente se agrega su paquete con la misma versión).

**Primary Dependencies**:
- Carter, MediatR + FluentValidation, EF Core 10 con migraciones pares PostgreSQL y SQL Server
  (`tools/scripts/add-migration.ps1`, `check-migration-parity.ps1`).
- Contratos de la 013 y la 014 (`Application/Common/Integration/Lending`), bandeja y registro de participantes
  (`Application/Common/Integration`), aprobaciones, alertas, parámetros, importaciones y auditoría
  (`Application/Common/*`).
- Motor de archivos planos (`Application/Common/BankFiles`) y de layouts (`Domain/Common/Layouts`, R1).
- QuestPDF (recibo en tirilla y carta, actas, cierre, cheque, conciliación); ClosedXML y OpenXML (plantillas,
  exportaciones).
- Sin librerías nuevas. Sin integraciones en línea con bancos ni pasarelas (C2 = A): todo por archivos o portal.

**Storage**:
- **Base de cada cooperativa** (Principio IV): 86 tablas `RCD_` (67 en R0), 42 `TES_` (28 en T0), 3 `COR_`
  (cuentas bancarias de la persona y go/no-go), 7 `ACC_` (matrices, recibos, comprobantes, vínculo). Columnas nuevas en
  `LND_LoanOperations` y `LND_MemberAccountOperations` en R1.
- **Valores**: enumeraciones nuevas en `BankFileScope` (7, 9; 8, 10, 11 después) con el 3 renombrado
  `TreasuryPayments`; `AccountingModules.Receipts = 128`; `ModuloContable.Recaudo = "REC"`.
- **MongoDB** por cooperativa: auditoría encadenada con los módulos `Receipts` y `Treasury` (conservación por
  parámetro, diez años por defecto, nunca menos de cinco).
- **Almacén de objetos** (`IBlobStore`, S3 directo de la 011): soportes de origen de fondos, imágenes de talonarios,
  actas, certificaciones bancarias, archivos de pago y sus respuestas, extractos, documentos emitidos.
- **Retiro** (después de T2): `TRS_Checks`, `TRS_Concepts`, `TRS_Invoices`, por migración destructiva con guarda.
- **Sin uso desde R0/T0**: `ACC_BankReconciliations`, `ACC_BankStatementLines`, `ACC_BankStatementColumnMaps` (la
  conciliación de Tesorería reemplaza a la de la 009, E-04); no se borran en esta feature: su retiro, si se hace, es una
  migración destructiva aparte, pendiente.

**Testing**:
- xUnit + FluentAssertions + NSubstitute.
- **Casos dorados JSON**: imputación de medios, arqueo, señales de efectivo, huella bancaria, cruce de la conciliación,
  transiciones del resultado, GMF, armado de lotes (≈70).
- **Arquitectura**: dieciséis nuevas (`RecaudoYTesoreriaSeHablanPorSusPuertos`, `RecaudoNoConoceContabilidad`,
  `TesoreriaNoConoceContabilidad`, `LosHechosDeRecaudoYTesoreriaNoLlevanCuentas`,
  `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`, `RecaudoYTesoreriaNoTienenValoresLegalesFijos`,
  `LosComandosDeRecaudoYTesoreriaLlevanClave`, `LosCerrojosDeRecaudoYTesoreriaVanEnOrden`,
  `LasCantidadesYMontosDeCajaTienenSuPrecision`, `NoHayDoblesDePrueba`, `NingunProcesoAplicaPagosNiInformaResultados`,
  `LoDeRecaudoYTesoreriaNoSeReversaDesdeComprobantes`, `LaTesoreriaHeredadaNoVuelve`, `TextosDeRecaudoYTesoreriaTests`,
  `LasPantallasDeRecaudoYTesoreriaEstanEnElMenu`, `NadieEscribeLaConciliacionBancariaDeContabilidad`) y catorce ampliadas
  (`ElCatalogoDeMensajesEsUnico`, `SoloElNumeradorNumera`, `LasPantallasDicenQueEstanCargando`,
  `TodoEnlaceDelMenuTieneSuPagina`, `LosEndpointsProtegidosExigenPermiso`, `UnSoloConsumidorPorDestino`,
  `LasCuentasSeTocanSoloPorElContrato`, `LosHechosInmutablesNoSeModifican`, `LaSegregacionNoUsaUserIdDelToken`,
  `LosPagosNoGuardanElNumeroDeTarjeta`, `LaCarteraSoloSeTocaPorSusInterfaces`, `LosComandosDeConsumoNoTienenRuta`,
  `EmisionesDeProcesoPermitidas`, `LasMigracionesCongeladasNoSeRegeneran`); `LosDoblesDePruebaNoSeRegistranEnProduccion`
  se reemplaza por `NoHayDoblesDePrueba`.
- **e2e**: colección «Recaudo y Tesorería e2e» con Testcontainers en los dos motores, cooperativas aisladas para la
  activación; las e2e de Cartera y Captaciones **migran** a los módulos reales antes de retirar los dobles (FR-224). Se
  corren contra base de datos al cierre de R0/T0 (decisión del dueño), con **dos excepciones declaradas**: la e2e de
  humo de la caja al cierre del bloque 2 (T0183c, antes del hito del 04/11, para que el riesgo de la caja aparezca
  pronto) y la puerta de FR-224 (T0301: las e2e de Cartera y Captaciones migradas, en verde en los dos motores antes de
  borrar el código de los dobles). Ninguna otra e2e se corre antes de la fase 22.
- **Rendimiento** con `RUN_PERF_TESTS=1`: SC-010, SC-011, SC-023, SC-031, SC-032.

**Target Platform**: Linux (k3s) para API y Web; navegador y app de Windows/Android (MAUI) para las pantallas; impresora
de tirilla o carta y lector por el navegador (sin integración con datáfonos).

**Project Type**: web-service + SPA existentes. **Sin proyecto nuevo ni servicio desplegable nuevo**: los dos módulos se
agregan a los proyectos actuales; las tareas por `ITareaProgramada` e `IEjecutorEnCooperativa`.

**Performance Goals**:
- consulta de lo que puede pagar una persona p95 < 2 s y confirmación de un recibo p95 < 3 s con 20 cajeros simultáneos
  (SC-023); recibo de tres productos y dos medios en < 2 min de operador (SC-003);
- arqueo y cierre de una sesión de 200 recibos en < 10 min (SC-004);
- respuesta de pagaduría de 5.000 líneas procesada y su giro aplicado en < 5 min (SC-010);
- lote de 1.000 órdenes con su archivo en < 1 min (SC-011);
- conciliación mínima de 500 movimientos con ≥ 80 % de cruce automático (SC-031).

**Constraints**:
- Recaudo y Tesorería **nunca** leen ni escriben datos de Cartera, Captaciones, Comercial, Nómina ni Contabilidad, ni
  entre ellos, salvo por sus contratos, puertos y mensajes (FR-003); lo vigilan pruebas de arquitectura.
- Ningún valor legal en el código: umbrales SARLAFT, GMF, plazos de cheques y libranza, conservación son parámetros con
  vigencia y fuente, «pendiente de validar» lo no validado.
- Plazos regulatorios informativos; las **listas vinculantes** y el **fondo de liquidez** bloquean siempre (FR-010).
- Lo inmutable no se edita: recibos, egresos, sesiones cerradas, archivos de pago, resultados, conciliaciones cerradas;
  se corrigen con un documento nuevo enlazado.
- Una transacción explícita por comando; los contratos corren en la de Recaudo; cerrojos en orden fijo; en el recibo, numeración antes de los contratos y en lo demás al
  final.
- Ningún proceso aplica pagos, llama a los contratos ni informa resultados: todo a nombre de una persona (FR-174).
- Fecha local `America/Bogota`, tres fechas por documento.
- Ningún texto del producto nombra al sistema anterior: «lo heredado» y «cifras de referencia».
- Calendario fijo: R0/T0 en producción antes del 28/11, ensayo general del 23/11 al 25/11.

**Scale/Scope**:
- 26 historias, 228 requisitos y 32 criterios de éxito, en seis entregas.
- 138 tablas nuevas (86 `RCD_`, 42 `TES_`, 3 `COR_`, 7 `ACC_`); 105 en R0/T0.
- 13 migraciones pares: 7 en R0/T0 (cuatro que se regeneran hasta el despliegue en QA del 10/11 y ahí se congelan, y
  tres nuevas después: la conciliación mínima, la puesta en marcha y la de datos de los dobles), una por cada entrega
  posterior más la de Cartera y Captaciones en R1, y la destructiva del retiro después de T2.
- Unas 200 rutas nuevas en R0/T0 (`/api/receipts/*`, `/api/treasury/*`, más `core`, `accounting` y `reports`), unos 90
  permisos sembrados completos (los de R1 a T2 incluidos, decisiones-transversales §2.6), siete plantillas de rol.
- 45 pantallas en R0/T0 (`/recaudo/*`, `/tesoreria/*`, contabilidad, plataforma).
- 39 tipos de hecho a Contabilidad (25 de Recaudo, 14 de Tesorería; 25 en R0/T0), 5 mensajes de negocio que emiten (3 en R0/T0) y 7
  tipos que reciben en R0/T0 (más 3 en T2).
- Volumen: hasta 10.000 recibos y 2.000 órdenes diarios, 100 cajas y 50 cuentas bancarias por cooperativa.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec del 2026-10-22 con las aclaraciones del dueño (C1–C3, D2 y ocho decisiones tras la revisión adversarial) → este plan → tasks. Las piezas de Cartera, Captaciones, Comercial, Contabilidad, Nómina y plataforma van como enmiendas E-01 a E-06 en [contracts/contratos-v2.md](contracts/contratos-v2.md). Lo que depende de datos del dueño queda como pendiente con su defecto (P1–P19). |
| II   | Clean Architecture | PASS | Motores puros en `Domain/Receipts/*` y `Domain/Treasury/*` (imputación, arqueo, señales, huella, transiciones, lotes, cruce, GMF) sin IO. Puertos en `Application/Common/Integration/{Receipts,Treasury,Accounting}`. Ningún módulo depende de las reglas internas de otro (FR-003). |
| III  | CQRS + MediatR | PASS con desviación aprobada | Todo comando con validador; los consumidores son comandos sin ruta; los endpoints sólo reenvían. Desviación heredada de la 013 (T101): Recaudo llama los contratos de Cartera y Captaciones, y los puertos entre Recaudo y Tesorería, directo dentro de su unidad de trabajo; cada puerto ejecuta su validador y escribe su evento. **Aprobada formalmente por el dueño el 2026-10-23** (Complexity Tracking; la descripción del PR la deja escrita, T0340b). |
| IV   | Multi-tenancy | PASS | Todo en la base de cada cooperativa: puntos, cajas, cuentas bancarias, recibos, giros, extractos, conciliaciones. Las tareas recorren cooperativas con `IEjecutorEnCooperativa` y arrendamiento por base. Ningún dato cruza cooperativas. |
| V    | Person centralizada | PASS | Pagadores, quienes efectúan, beneficiarios, giradores, pagadurías, bancos-tercero y cajeros son personas del maestro; las cuentas destino viven en la plataforma ligadas a la persona (`COR_PersonBankAccounts`); una partida sin identificar no tiene persona hasta identificarse (I-2). El pagador ocasional se da de alta en un paso mínimo con el buscador común. |
| VI   | PublicId externo | PASS | Rutas, DTO, mensajes y archivos por `PublicId` y número visible; lo que cruza entre módulos se guarda como `PublicId` (sin FK). |
| VII  | Soft-delete + auditoría | PASS | Todo `AuditableEntity` con filtro; catálogos se inactivan; los documentos nunca se dan de baja y llevan la marca H. |
| VIII | Validación dual | PASS | Validadores de FluentValidation; la caja valida antes (total de medios, vueltas, elección del deudor) y el servidor manda; códigos `Receipts.*` / `Treasury.*` con mensaje en español que dice qué hacer. |
| IX   | Errores visibles | PASS | Rechazos de contratos por ítem, fallas de consumo en la bandeja con reproceso, corridas no interactivas con su línea rechazada y motivo, alertas de tareas (sesión olvidada, partidas antiguas, orden sin resultado, remesa sin confirmar, cuadre provisional). Ningún `catch` vacío. |
| X    | Trazabilidad SARLAFT | PASS | Toda operación monetaria y toda lectura sensible a la auditoría encadenada (`Receipts`, `Treasury`); declaración de origen de fondos, quien efectúa y alertas son datos de la operación (I-3). Las corridas no interactivas dejan, por `COR_AuditOutbox` en la cadena de sellos, un evento resumen por corrida con la lista de cada recibo aplicado (`PublicId` y huella `HuellaDeRecibo`), verificable recibo por recibo, y uno por línea rechazada (decisión del dueño del 2026-10-23): se cumple sin desviación. |
| XI   | Inmutabilidad | PASS | Recibos, egresos, sesiones cerradas, archivos de pago, resultados y conciliaciones cerradas no se editan (I-1); se corrigen con anulación, ajuste, reverso o reapertura enlazados. Lo contabilizado se corrige con un hecho espejo, nunca desde Comprobantes. |
| XII  | Migraciones | PASS | Aditivas por entrega en par; los pares de R0/T0 se congelan al desplegarse en QA el 10/11 (T0271) y todo cambio posterior es un par nuevo y aditivo; `RetiroDeLosDoblesDePrueba` es una transformación de datos idempotente que sólo cierra vigencias (`ValidTo`) y no borra filas; `RetiroDeLaTesoreriaHeredada` (después de T2) es destructiva con guarda, diagnóstico, exportación, migración de pendientes, respaldo, segundo revisor, marcador y declarada irreversible, en commit propio. |
| UI   | Indicador de carga; sin colores literales | PASS | Pantallas nuevas en `Shared/Pages/Recaudo` y `Shared/Pages/Tesoreria` con `IndicadorDeCarga` y `EstadoDeCarga` por zona; `Recaudo` y `Tesoreria` entran a `ModulosMigrados`; tokens de `tokens.css`; el menú sólo enlaza páginas con `@page`. |

**Resultado: PASS.** Una desviación heredada, justificada y **aprobada por el dueño el 2026-10-23** (III); el Principio X
se cumple sin desviación; los prefijos `RCD_` y `TES_` entran por la enmienda 2.0.1 de la constitución (Estándares,
2026-10-23, revisor el dueño; la referencia queda en el PR, T0340a). Sin violaciones nuevas.

## Project Structure

### Documentation (this feature)

```text
specs/015-recaudo-tesoreria/
├── plan.md                        # este archivo
├── spec.md                        # especificación con las aclaraciones del dueño
├── decisiones-transversales.md    # nombres canónicos (§2), TR1–TR43, choques CH1–CH16, preguntas Q1–Q10
├── research.md                    # Fase 0: R-01–R-40, pendientes del dueño
├── data-model.md                  # Fase 1: tablas, cerrojos, precisión, estados, enums, errores, alertas, parámetros, migraciones
├── quickstart.md                  # Fase 1: pruebas automáticas y recorridos A–L de R0/T0
├── contracts/
│   ├── api.md                     # rutas por grupo, permisos, pantallas
│   ├── mensajes.md                # lo que reciben y emiten, registro de participantes, retiro de los dobles
│   ├── contabilidad.md            # E-04: hechos REC y TES, roles, matriz, lotes, tipos de comprobante
│   ├── contratos-v2.md            # E-01 a E-06: v2 de los contratos, mensajes nuevos, enmiendas
│   ├── interfaces.md              # puertos síncronos con Cartera, Captaciones, entre los dos módulos, Contabilidad, plataforma
│   └── archivos-y-plantillas.md   # ámbitos de formatos, archivo de pago, genéricos, plantillas, reporte, documentos
├── checklists/requirements.md
└── tasks.md                       # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Receipts/{Configuration,Sessions,Vaults,Operations,Banks,Paymasters,Compliance,Activation}   # NUEVO
├── Entities/Treasury/{BankAccounts,Payments,Checkbooks,Remittances,Statements,Reconciliation,Activation} # NUEVO (lo heredado sigue en la raíz hasta T2)
├── Entities/Core/PersonBankAccount.cs, Entities/Core/GoLive/*                                            # E-06
├── Enums/{Receipts,Treasury}/  · Enums/Core/BankFiles.cs (ámbitos)  · Enums/Accounting/AccountingEnums.cs (Receipts = 128)
├── Receipts/{Imputacion,Arqueo,Cumplimiento,Huellas}        # motores puros
├── Treasury/{Resultados,Lotes,Conciliacion,Gmf}             # motores puros
└── Common/Taxation/CalculoDeGmf.cs

src/Core/IngenIA365ERP.Application/
├── Receipts/{Setup,Sessions,Vaults,CashDesk,Voidings,Reversals,CreditBalances,Checks,BankItems,Paymasters,
│             Compliance,Accounting,Activation,Imports,Reports,Integration}
├── Treasury/{BankAccounts,LiquidityFund,Orders,Batches,PaymentFiles,Results,Checkbooks,Remittances,Statements,
│             Reconciliation,Accounting,Activation,Imports,Reports,Integration}
│   └── Checks/ Invoices/ TreasuryConcepts/                  # HEREDADO, sin cambios hasta su retiro después de T2
├── Accounting/{Receipts,Treasury}/{Reglas,Contabilizacion,Lotes,Plantilla}   # E-04
├── Accounting/Integration/DestinoContabilidad.cs            # ramas REC y TES
├── Accounting/Posting/ModuloContable.cs                     # Recaudo = "REC"
├── Common/Integration/{Receipts,Treasury}/                  # puertos entre los dos módulos
├── Common/Integration/Accounting/{IContabilidadParaRecaudo,IContabilidadParaTesoreria,IEstadoOperativoParaElCierre}.cs
├── Common/Integration/Contracts/Lending/ArchivoDeDesembolsos*.cs   # E-01 c
├── Common/Compliance/IListasRestrictivas.cs                 # contextos Collection y Disbursement
├── Common/GoLive/ · Core/PersonBankAccounts/                # E-06
└── Lending/{Activation,Deposits/Activation}/                # E-01 e, E-02 g; se borra Lending/Integration/DoblesDePrueba/

src/Infrastructure/
├── IngenIA365ERP.Persistence/  Configurations/{Receipts,Treasury}, Receipts/CerrojoDeRecaudo.cs,
│                               Treasury/CerrojoDeTesoreria.cs, Seeding/Parametric/* (tipos de documento, conceptos,
│                               causales, formatos genéricos, tipos de alerta, plantillas de rol)
├── IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application/   # 11 pares (data-model §25)
└── IngenIA365ERP.Identity/  Seed/ReceiptsAndTreasuryPermissionCatalogSeeder

src/Presentation/
├── IngenIA365ERP.API/  Endpoints/Receipts/*, Endpoints/Treasury/* (nuevos junto a los heredados),
│                       Endpoints/Reports/{Receipts,Treasury}ReportsEndpoints, Endpoints/Core/{PersonBankAccounts,GoLive}Endpoints,
│                       Endpoints/Accounting/{Receipts,Treasury}IntegrationEndpoints,
│                       Services/{AlcanceDeRecaudoDeLaPeticion,AlcanceDeTesoreriaDeLaPeticion}.cs,
│                       Reports/{DocumentosDeRecaudoReport,DocumentosDeTesoreriaReport}.cs
│                       (se borran Endpoints/Lending/TestDoublesEndpoints.cs e Integration/DoblesDePruebaDeCartera.cs)
└── IngenIA365ERP.Shared/  Pages/Recaudo/*, Pages/Tesoreria/* (nuevas), Pages/Contabilidad/{Recaudo,Tesoreria}/*,
                           Pages/Administracion/GoNoGo.razor, Services/{Recaudo,Tesoreria}/*Client.cs,
                           Layout/NavMenu (grupos «Recaudo», «Tesorería» y «Tesorería · heredada»)
                           (se borra Pages/Cartera/DoblesDePrueba.razor)

tests/
├── IngenIA365ERP.Domain.Tests/{Receipts,Treasury}/Casos/
├── IngenIA365ERP.Application.Tests/{Receipts,Treasury,Accounting/Receipts,Accounting/Treasury}/
├── IngenIA365ERP.Architecture.Tests/Principles/             # las dieciséis nuevas y catorce ampliadas
└── IngenIA365ERP.API.IntegrationTests/{Receipts,Treasury}/   # colección «Recaudo y Tesorería e2e»
    └── Lending/{CarteraE2E,Deposits/CaptacionesE2E}.cs        # migradas a los módulos reales (FR-224)
```

**Structure Decision**: se extienden los proyectos existentes con el molde de la 012, la 013 y la 014.

- **Sin proyecto nuevo**: no hay proveedores externos en línea (C2 = A); los bancos se integran por archivos.
- **Carpetas ya fijadas por pruebas**: `Application/Receipts`, `Domain/Entities/Receipts`, `Application/Treasury`,
  `Domain/Entities/Treasury` y `API/Endpoints/Treasury` ya las nombra `LasCuentasSeTocanSoloPorElContrato`.
- **Convivencia**: lo heredado de Tesorería queda en la raíz de sus carpetas y en sus rutas
  (`/api/treasury/{checks,concepts,invoices,cash-flow}`) hasta T2; lo nuevo, en subcarpetas y subrutas propias.
- **Prefijos**: `RCD_`, `TES_`, `ACC_Receipts*`, `ACC_Treasury*`, `COR_`. No se reutiliza `TRS_`. `RCD_` y `TES_`
  están en la constitución desde la enmienda 2.0.1 (Estándares Técnicos Adicionales, 2026-10-23).

## Complexity Tracking

| Punto | Por qué | Alternativa descartada |
|---|---|---|
| **Desviación III** (heredada, T101 de la 013): los contratos de Cartera y Captaciones y los puertos entre Recaudo y Tesorería se llaman directo dentro de la unidad de trabajo del llamador. **Aprobada formalmente por el dueño (Jorman Copete) el 2026-10-23**; la descripción del PR de R0/T0 la deja escrita (T0340b) | El recibo es «todo o nada» con sus aplicaciones (FR-042) y la orden de pago de una devolución debe nacer en la misma transacción que la baja del saldo a favor. Por `ISender` un reintento anidado vacía el `ChangeTracker`. Mitigación, condición de la aprobación: cada puerto ejecuta su validador y escribe su evento (`LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`). | Mensajes por la bandeja entre Recaudo y Tesorería: estados intermedios visibles y devoluciones sin orden. |
| **La conciliación de Tesorería reemplaza a la de Contabilidad** (enmienda E-04 a la 009, FR-055 a FR-060; decisión del dueño del 2026-10-23) | Un solo extracto y una sola conciliación por cuenta: dos conciliaciones del mismo banco divergirían. `ReopenPeriodCommand` (`Application/Accounting/Periods/PeriodCommands.cs`), que hoy marca `Outdated` las `ACC_BankReconciliations`, pasa a marcar las de Tesorería por `IConciliacionBancariaParaContabilidad`; las tres tablas `ACC_` quedan sin uso (`NadieEscribeLaConciliacionBancariaDeContabilidad`) y su retiro es una migración destructiva aparte, pendiente. | Dos conciliaciones con sincronización entre ellas. |
| **Dos módulos en una feature** con frontera vigilada | Comparten caja, medios, cuentas bancarias y remesas (D1), pero tienen permisos, participantes y emisores distintos. | Un solo módulo: mezclaría el recaudador y el destino de giros, y el registro de participantes los distingue. |
| **El extracto vive en Tesorería** y Recaudo lo usa por puerto | Un solo almacén garantiza «nada se identifica ni se concilia dos veces» por construcción (FR-066, FR-197). | Un extracto por módulo con deduplicación cruzada. |
| **Pieza de plataforma nueva**: `COR_PersonBankAccounts` | FR-210 exige doble control, certificación y espera sobre la cuenta destino; hoy sólo hay campos sueltos sin historia. | Validar contra los campos sueltos del maestro: sin doble control. |
| **Pieza de plataforma nueva**: lista de go/no-go | FR-227 la pide verificable en el sistema; sirve a Cartera y Captaciones. | Una hoja fuera del sistema. |
| **Migración de datos** `RetiroDeLosDoblesDePrueba` | Retira las filas de los dobles en DEV y QA conservando la historia; en producción no hace nada. Va con `WHERE` idempotente. | Retirarlas a mano en cada cooperativa (FR-148 lo prohíbe). |
| **Renombrar** `BankFileScope.SupplierPayments` a `TreasuryPayments` (mismo número) | El ámbito 3 se reservó en la 010 para esto y no tiene filas. | Un ámbito nuevo y el 3 huérfano para siempre. |
| **Convivencia con la Tesorería heredada** hasta T2 | Decisión del dueño: COOFLOPAL la usa para proveedores; sus egresos se ven como movimientos contables de la cuenta (FR-209). | Retirarla en R0/T0: dejaría a COOFLOPAL sin cuentas por pagar el 01/12. |
| **Migración destructiva** del heredado después de T2 | Decisión del dueño (C3 = A) con migración previa de pendientes. | Conservarla de sólo lectura: dos fuentes de cheques y conceptos. |

## Decisiones

Se adoptan TR1–TR43 de [decisiones-transversales.md](decisiones-transversales.md) y R-01–R-40 de
[research.md](research.md). Resumen por tema:

| Tema | Decisión del plan |
|---|---|
| **Módulos y frontera** (TR1–TR4) | `Receipts`/`REC`/`RCD_` y `Treasury`/`TES`/`TES_`; puertos síncronos entre ellos; extracto único en Tesorería; lo heredado intacto hasta T2. |
| **Caja y recibo** (TR5–TR13) | Operación de caja con `PublicId` estable; `ConfirmacionDeRecibo` único; un lote por contrato, obligaciones antes que cuentas; clave por porción; imputación pura; cerrojos en orden fijo; numeración al final (en el recibo, antes de los contratos); sesión con valores sellados; bóveda en Recaudo y remesa en Tesorería. |
| **Bancos y pagadurías** (TR14–TR16) | Consumo de `DescuentosPorPagaduria` v1/v2, envío y resultado sin cambios; línea por transacción; ningún proceso aplica. |
| **Tesorería** (TR17–TR22) | Bandeja única con validación de destino, banco y listas; `ResultadoDelGiro` v1 sin cambios con transiciones fijas; hecho de pago al confirmar el banco o al emitir el cheque; archivo por el motor común; cuentas destino con doble control; fondo de liquidez exclusivo. |
| **Contabilidad** (TR23–TR26) | Dos emisores, dos matrices, vínculo en Contabilidad; validación previa que bloquea en pantalla; recibos por lotes; puesta en marcha sin hechos con cuadre provisional y guarda de cierre. |
| **Cumplimiento y GMF** (TR27–TR30) | Parámetros SARLAFT separados con vigencia; registro propio de efectivo; listas vinculantes bloquean; GMF con parámetros de Tesorería. |
| **Participantes** (TR31–TR34) | Registro real en la activación; retiro de los dobles en tres piezas con las e2e migradas antes; v2 de los contratos en R1; go/no-go de plataforma. |
| **Seguridad y uso** (TR35–TR40) | Alcance que falla cerrado; aprobaciones de la 012; auditoría encadenada (corridas con evento resumen que lista cada recibo con su huella); precisión por alias; caja en tres pasos con atajos; migraciones de R0/T0 congeladas al desplegarse en QA el 10/11 y, después, sólo pares nuevos. |
| **Después del analyze** (TR41–TR43) | Conciliación bancaria única, de Tesorería (E-04); activación mínima en el bloque 2 para los hitos del 04/11 y del 10/11; hora de corte como restricción de configuración comprobada (FR-185). |

## Entregas y fases

R0 y T0 van **juntas** (la activación de Cartera y Captaciones exige las dos) y son el recorte mínimo para el 01/12;
después R1 y T1 juntas; después R2 y T2. Cada entrega incluye todo lo anterior de su módulo.

| Entrega | Contenido | Tablas | Migraciones |
|---|---|---|---|
| **R0** | US1 (parcial), US2, US3 (parcial), US5, US6 (parcial), US7, US8, US9, US12, US13, US14 (parcial), US15 (parcial), US24 | 67 `RCD_` (64 en `RecaudoNucleo`, 3 en la puesta en marcha) | `RecaudoNucleo` |
| **T0** | US8, US9, US10, US11, US12, US13, US19 (parcial), US25, US26 (parcial) | 28 `TES_` (22 en `TesoreriaNucleo`, 3 en la conciliación, 3 en la puesta en marcha) | `TesoreriaNucleo`, `TesoreriaConciliacionMinima` |
| **R0 + T0 común** | plataforma (cuentas de la persona, go/no-go, formatos genéricos, alertas), puesta en marcha, retiro de los dobles, contabilidad | 3 `COR_`, 7 `ACC_` | `PlataformaParaRecaudoYTesoreria`, `IntegracionContableDeRecaudoYTesoreria`, `RecaudoYTesoreriaPuestaEnMarcha`, `RetiroDeLosDoblesDePrueba` |
| **R1** | resto de medios, egresos en efectivo, identificación por reglas, consignación reportada, recaudo pendiente de aplicar, v2 de los contratos, cuentas por cobrar comerciales, registro único de efectivo con el punto de venta, **reporte de transacciones** (en producción antes del primer vencimiento de enero de 2027), calendario de pagadurías, consolidado | 10 `RCD_` + columnas `LND_` | `RecaudoCompleto`, `CarteraYCaptacionesConPartidaCitada` |
| **T1** | órdenes manuales, traslados, remesa del punto de venta, firmantes y tokens, comprobante de pago, cheques al cierre del mes, aviso al cierre contable | 6 `TES_` | `TesoreriaCompleta` |
| **R2** | canje completo, convenios con referencia y corresponsales, pasarelas, aseguradoras | 9 `RCD_` | `RecaudoCanales` |
| **T2** | conciliación completa y notas bancarias, proveedores y cuentas por pagar, adquirentes, liquidez y flujo, exógena | 8 `TES_` | `TesoreriaAmpliada` |
| **Después de T2** | retiro de la Tesorería heredada con migración de pendientes | −3 `TRS_` | `RetiroDeLaTesoreriaHeredada` (destructiva, commit propio) |

### Fases de construcción de R0/T0 (propuesta de fechas, R-38)

| Fase | Fechas | Contenido | Sale con |
|---|---|---|---|
| **1 · Plataforma y esqueleto** | 22/10–27/10 | E-06 (`COR_PersonBankAccounts`, ámbitos, formatos genéricos, contextos de listas, tipos de alerta, go/no-go); módulos `Receipts` y `Treasury` vacíos con permisos, roles, grupos de rutas y de menú; `ModuloContable`/`AccountingModules`; `CerrojoDeRecaudo`, `CerrojoDeTesoreria`, numeradores; puertos entre los dos módulos; pruebas de arquitectura nuevas en rojo controlado; migraciones `PlataformaParaRecaudoYTesoreria` y esqueleto de `RecaudoNucleo`/`TesoreriaNucleo` | arquitectura en verde |
| **2 · Caja y giros** | 28/10–04/11 | R0: configuración (puntos, cajas, cajeros, medios, tipos, conceptos), sesión, arqueo, bóveda, operación de caja, `ConfirmacionDeRecibo` por los dos contratos, recibo y su documento, anulación, avisos de reverso, saldos a favor por orden de pago. T0: cuentas bancarias con vínculo contable, fondo de liquidez, bandeja de órdenes (`DestinoTesoreria`), validación de destino, lotes por archivo y portal, aprobación, archivo y descarga, respuesta y cotejo, resultado y regiro. R0/T0: **activación mínima** de Recaudo y Tesorería (requisitos básicos y registro de los participantes, TR42) y **e2e de humo de la caja** (excepción declarada) | 04/11: un recibo y un giro de punta a punta en DEV, con los participantes registrados por la activación mínima |
| **3 · Bancos, pagadurías, cheques y contabilidad** | 05/11–10/11 | R0: extractos (carga única de Tesorería) e identificación manual, partidas, `AplicacionNoInteractiva`, pagaduría completa (`DestinoRecaudo`), cheque recibido por parámetro y devolución por porción, contingencia, SARLAFT básico. T0: chequera mínima y cheques girados, remesas. E-04: ramas `REC`/`TES` de `DestinoContabilidad`, matrices, plantilla, completitud, validación previa, lotes resumidos, `IntegracionContableDeRecaudoYTesoreria`. E-01 (c): mensaje del archivo de desembolsos | 10/11: versión al paralelo en QA (caja, recibo, extracto, pagaduría, giro), activada con la activación mínima; **los cuatro pares de R0/T0 que existen se congelan** con sus huellas |
| **4 · Conciliación, salida y dobles** | 11/11–19/11 | T0: conciliación mínima (par nuevo `TesoreriaConciliacionMinima`) que reemplaza a la de Contabilidad (E-04, reapertura del período por puerto). R0/T0: plantillas de puesta en marcha, cifras de referencia, cuadre definitivo y provisional (par nuevo `RecaudoYTesoreriaPuestaEnMarcha`), activación completa con requisitos y registro de participantes, guarda de cierre (E-04), lista de go/no-go, informes R0/T0, manuales (FR-225). E-01 (e)/E-02 (g): e2e de Cartera y Captaciones migradas a los módulos reales y en verde; después, `RetiroDeLosDoblesDePrueba` y borrado del código de los dobles | 19/11: R0 y T0 completas en QA |
| **5 · Verificación y congelamiento** | 20/11–22/11 | e2e de la colección en los dos motores (una sola corrida al final, decisión del dueño, salvo las dos excepciones declaradas), rendimiento, QA por rol, huellas de las migraciones nuevas posteriores al 10/11 (no se regenera nada) | 22/11: todas las migraciones de R0/T0 con huella |
| Ensayo general | 23/11–25/11 | recorridos A–L de [quickstart.md](quickstart.md) en QA (SC-002) | go/no-go del ensayo |
| Producción | 26/11–27/11 | correcciones; respaldo por base; despliegue de R0/T0 (las migraciones aditivas y la de los dobles, que en producción no hace nada) | 27/11 |
| Carga y salida | 28/11 · 01/12 | go/no-go y carga definitiva sobre producción limpia; activación de Recaudo y Tesorería y después Cartera y Aportes | 01/12 |

Lo que el diseño deja para `/speckit-tasks`: el orden fino dentro de cada fase, la prueba de volumen de 20 cajeros, el
cotejo de los tipos de comprobante sugeridos contra los vigentes, el diagnóstico SQL del heredado (antes del retiro) y las
tareas de manuales por entrega.

## Riesgos para llegar a R0/T0 el 01/12

- **Plazo**: cinco semanas de construcción para 105 tablas, unas 200 rutas y 45 pantallas, más las enmiendas a módulos
  en producción. Mitigación: fases con hitos verificables, recorte estricto (lo de R1/T1 no entra), reutilización de
  plataforma, paralelo en QA con la versión del 10/11 aunque falte la conciliación, la e2e de humo de la caja al cierre
  del bloque 2 y las **líneas de recorte** de abajo.
- **Datos del dueño que bloquean** (research «Pendientes»): roles, alcances y segundo factor (P4, 13/11); umbrales y
  listas SARLAFT (P7, 20/11); matrices con la contadora al 100 % (P8/P12, 20/11); escenarios de no-go parcial (P15,
  20/11); pagadurías (P6, 06/11, bloquea sólo la pagaduría). El formato de pagos de AV Villas (P1) sólo bloquea si no se
  acepta el genérico o el portal.
- **Retiro de los dobles**: las e2e de Cartera y Captaciones (la colección de Lending) hoy dependen de los
  dobles; migrarlas antes de borrar el código está en la ruta crítica de la fase 4.
- **Cambios en módulos en producción**: requisitos de activación de Cartera y Captaciones, el mensaje del archivo de
  desembolsos y las ramas nuevas de `DestinoContabilidad`. Se cubren con las e2e migradas y la conciliación de puentes.
- **Concurrencia en caja**: el orden de cerrojos con los contratos es nuevo en un recibo que toca créditos y cuentas;
  se mide pronto con `DosCajasMismaPersonaTests` y la prueba de 20 cajeros.
- **El 15/12**: la quincena exige el archivo de descuentos hacia el 10/12; la pagaduría debe salir completa en R0.
- **Primer reporte UIAF** en enero de 2027 (si la suspensión no se prorroga): R1 debe llegar a producción antes.
- **Cierre de diciembre** (también cierre del ejercicio): exige el cuadre definitivo y la conciliación mínima; un cuadre
  provisional vencido escala al dueño y al contador.

## Líneas de recorte

Si un hito (04/11, 10/11, 19/11) llega con atraso, sale de R0/T0 **en este orden**, y lo que sale pasa a una entrega
inmediata posterior al 01/12 con su propia migración nueva. Cada recorte lo decide el dueño en el hito y queda en
research.md («Pendientes del dueño»); no hay recorte sin esa decisión registrada.

1. **Informes de R0/T0 que no exige nadie el primer día** (US21 parte R0/T0): quedan `daily-collection`, `session-close`,
   `day-close`, `unidentified-items`, `orders-by-state` y `parallel-comparison`; el resto, después.
2. **Cheque recibido por parámetro** (US15 en R0): `Caja.AdmiteCheque` ya arranca apagado (D37); si P2 confirma que
   COOFLOPAL no recibe cheques en ventanilla, sale entero.
3. **Contingencia por talonario** (FR-186): mientras no llegue, la caída de la caja se cubre con recibos con fecha valor
   anterior aprobados (FR-192) dentro de la ventana del contrato.
4. **Chequera mínima y cheques girados** (US19, parte cheques): los giros salen por transferencia (archivo o portal del
   banco); los cheques de proveedores siguen en lo heredado.
5. **Remesas como documento de Tesorería** (FR-105): la bóveda registra la remesa y la consignación entra como abono
   identificado del extracto.
6. **Conciliación mínima** (US25): último recorte posible; debe estar en producción antes del cierre de diciembre
   (Clarifications, FR-228).

**Nunca salen**: caja y recibo multiproducto por los dos contratos, extracto e identificación, pagaduría (la quincena del
15/12), giros por transferencia con su resultado, contabilidad por las matrices, SARLAFT básico con listas vinculantes,
fondo de liquidez, activación con cuadre provisional y su guarda, retiro de los dobles con la puerta de FR-224, go/no-go.

## Re-evaluación de la constitución tras el diseño

Re-evaluada el 2026-10-22 sobre los artefactos de la fase 1 y otra vez el 2026-10-23 con las decisiones del dueño sobre
el `/speckit-analyze`. **Resultado: PASS**, con la desviación III aprobada por el dueño, el Principio X cumplido sin
desviación y ninguna violación nueva.

| #    | Principio | Estado tras el diseño | Qué lo sostiene en los artefactos |
|------|-----------|-----------------------|-----------------------------------|
| I    | Spec-First | PASS | Las enmiendas a otras features están escritas (contratos-v2.md); las preguntas nuevas (Q1–Q10) tienen propuesta por defecto; ningún artefacto deja puntos abiertos. |
| II   | Clean Architecture | PASS | Ocho motores puros con casos dorados; ningún FK entre `RCD_` y `TES_` ni hacia otros módulos (data-model §0.1, §14); vínculo contable en Contabilidad (§17.3). |
| III  | CQRS + MediatR | PASS con desviación aprobada (2026-10-23) | La desviación se limita a los contratos y a los puertos entre los dos módulos, cada uno con validador y evento; los consumidores son comandos sin ruta. |
| IV   | Multi-tenancy | PASS | Tareas por `IEjecutorEnCooperativa`; registro de participantes y activación por cooperativa; nada en la base administrativa. |
| V    | Person centralizada | PASS | `COR_PersonBankAccounts` cuelga de la persona; los documentos guardan la persona por FK al maestro. |
| VI   | PublicId externo | PASS | api.md §1.1; lo que cruza por `PublicId` (data-model §14); claves de contrato por `PublicId`. |
| VII  | Soft-delete + auditoría | PASS | Marca H en documentos (data-model §0.2); catálogos con UK filtrado; documentos sin baja. |
| VIII | Validación dual | PASS | Códigos literales y únicos en data-model §21; api.md los usa. |
| IX   | Errores visibles | PASS | 37 tipos de alerta (data-model §22) con destinatario por permiso; corridas con su línea rechazada. |
| X    | Trazabilidad | PASS | Lecturas sensibles con lista cerrada (R-32); declaración, quien efectúa y señales con la versión del parámetro (data-model §7); corridas con evento resumen que lista cada recibo con su huella (R-32, T0189a). |
| XI   | Inmutabilidad | PASS | Transiciones de §19; correcciones por documento nuevo; resultados del giro como hecho único por orden y resultado. |
| XII  | Migraciones | PASS | data-model §25 y §24: aditivas, congeladas las de R0/T0 al 10/11 y pares nuevos después, una de datos idempotente, la destructiva con guarda después de T2. |
| UI   | Indicador de carga; sin colores literales | PASS | api.md §18: pantallas con `IndicadorDeCarga`; `ModulosMigrados` ampliada. |
