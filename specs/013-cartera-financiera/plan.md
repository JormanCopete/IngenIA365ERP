# Implementation Plan: Cartera financiera — líneas, originación, causación, pagos, riesgo y cobranza de créditos

**Branch**: `013-cartera-financiera` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013-cartera-financiera/spec.md`

## Summary

La feature reemplaza la cartera de crédito heredada, un traslado a medias de 93 tablas que mezcla crédito con
ahorros, aportes y caja, por un módulo de Cartera nuevo. Lo que se conserva de ahorros y aportes se muda sin
cambios al espacio de nombres `Savings`.

**Qué construye**:
- **Producto y originación**: líneas parametrizables con vigencia y control diario de usura; originación con
  validaciones, puntaje, atribuciones, sesiones con acta y garantías; desembolso con orden de giro para Tesorería.
- **Cálculo**: causación diaria, liquidación reproducible a cualquier fecha y un **contrato de obligaciones** que
  Recaudo, Nómina y Aportes usan para pagar.
- **Riesgo**: calificación SARC con arrastre, deterioro (Anexo 1 en K1, pérdida esperada en K6a) y cierre mensual
  inmutable.
- **Ciclo de vida**: libranza, novedades, castigo, cobranza con la Ley 2300, documentos al deudor e informes
  regulatorios.
- **Puesta en marcha**: migración con cuadre y activación.
- **Otros módulos**: las adaptaciones de Comercial, Contabilidad, Nómina y Personas que el desacoplamiento exige
  (E-01 a E-05).

**Cómo**:
- **Motor financiero puro en Domain.** Los hechos son inmutables: `LND_LoanOperations` es el hecho primario y
  `LND_LoanTransactions` el libro por concepto y cuota, con tres fechas.
  - Saldos, cuotas, mora y calificación salen de **reconstruir** el crédito a una fecha D con la información hasta un
    instante R (`ReconstruccionDeCredito`).
  - Las posiciones diarias son instantáneas versionadas que nunca se reescriben (I-6).
  - La causación es idempotente por diferencia de acumulados.
  - La concurrencia se resuelve con un cerrojo por deudor y por crédito en orden fijo (`CerrojoDeCartera`), y un
    único escritor del libro (`RegistroDeCartera`).
- **Mensajes**: la bandeja de la 012 pasa a tener **varios emisores**, y Cartera emite sus hechos y respuestas.
  - El proceso automático puede originar mensajes sólo en los tipos marcados (pregunta B1).
  - El origen de cada mensaje es la operación de Cartera, nunca el crédito; no hay cadena por crédito hacia
    Contabilidad.
  - Los destinos se habilitan por un **registro de participantes por cooperativa**, con su clase y las versiones que
    adoptan. Ese registro también define cuándo Recaudo y Tesorería están «disponibles» para activar.
- **Contratos síncronos**: el de obligaciones y las reservas de número corren **dentro de la unidad de trabajo de
  quien llama** (I-4). Ejecutan su validador y escriben su evento de auditoría (desviación T101).
- **Contabilidad**: del lado contable se construyen la matriz de Cartera, el consumidor, el comprobante resumido de
  la causación, la reclasificación al cierre, la validación previa y la conciliación. Cartera nunca conoce una
  cuenta (E-01, enmienda de la 009).
- **Valores nacionales**: usura, índices, SMMLV, festivos y documentos normativos se registran **una vez** en la base
  administrativa. Se **replican** con bitemporalidad a cada cooperativa, también en el alta (I-3), y Nómina toma de
  ahí el SMMLV, la UVT y los festivos (A7).
- **Adaptadores**: pagaré desmaterializado, centrales, listas restrictivas, SMS y WhatsApp, cada uno con su **canal
  simulado**, en un proyecto de infraestructura nuevo (`IngenIA365ERP.ExternalChannels`), con el molde de la DIAN de
  la 012.
- **Recaudo y Tesorería** (Q1): en producción, Cartera no se activa sin ellos. En DEV y QA se ejercita con un
  recaudador y una tesorería de prueba que no pueden registrarse en un despliegue de producción.

Detalle de cada decisión en [research.md](research.md). Los nombres canónicos, las decisiones transversales
T1–T102, los choques entre frentes CH1–CH50, las preguntas al dueño A1–I17 y la conciliación de los artefactos
(C01–C102, §7) están en [decisiones-transversales.md](decisiones-transversales.md), que manda sobre cualquier otra
redacción de nombres.

## Technical Context

**Language/Version**: C# / .NET 10.0.5 (API, Domain, Application, Infrastructure). Blazor (Shared, Web, Web.Client y
la app MAUI) con Syncfusion 33.2.8 por paquetes de componente.

**Primary Dependencies**:
- Carter, MediatR + FluentValidation, y EF Core 10 con migraciones pares PostgreSQL y SQL Server.
- QuestPDF para documentos al deudor y actas; ClosedXML y OpenXML para plantillas y exportación.
- El motor común de archivos planos (`FlatFileWriter`, `COR_BankFileFormats`) y el de layouts regulatorios
  (`Domain/Common/Layouts`).
- Los SDK o clientes HTTP de proveedores externos viven **sólo** en el proyecto nuevo
  `Infrastructure/IngenIA365ERP.ExternalChannels`.
- No hay librerías nuevas en Domain. La aritmética de tasas (potencias, raíces y TIR) es `decimal` propia
  (`AritmeticaDecimal`).

**Storage**:
- **Base de cada cooperativa**: 113 tablas `LND_*` de negocio más 17 de preparación de la migración (81 nacen en K1),
  4 `ACC_*` del lado contable y 11 `COR_*` de plataforma (réplica de valores nacionales, perfil regulatorio, registro de
  participantes, canales externos, listas restrictivas, solicitudes del titular).
- **Otros módulos**: `PAY_PayrollLoanDeductions` e `INV_LendingObligationResponses`.
- **Columnas nuevas** en `COR_IntegrationMessages`, `COR_NotificationTemplates`, `CMP_HabeasData*`,
  `PAY_SettlementDeductions`, `PAY_Novelties`, `PAY_PayrollRuns`, `INV_DocumentPayments` y `COR_PaymentMeans`.
- **Base administrativa**: `ADM_NationalValues`, `ADM_RegulatoryDocuments` y el contador `ADM_NationalValueCounters`.
- **MongoDB** por cooperativa para la auditoría encadenada, con los módulos `Lending`, `Compliance` y
  `NationalValues` a diez años.
- **Almacén de objetos** (`IBlobStore`) para documentos de solicitudes, evidencias, pagarés, archivos de desembolso,
  de descuentos y regulatorios, y documentos emitidos.
- **Credenciales de canales externos**: Secrets montados en archivo, nunca en la base.
- **Retiro**: 55 tablas `LND_*` de crédito, por migración destructiva con guarda. Las 38 que se conservan no cambian
  de tabla.

**Testing**:
- xUnit + FluentAssertions + NSubstitute.
- **Casos dorados JSON**:
  - motor financiero (~28 en K1): amortización, convenciones, usura, causación, suspensión, prelación, anticipos,
    abono extraordinario, fecha valor, reversos con pagos posteriores, día no hábil;
  - riesgo: calificación, arrastre, deterioro y, en K6a, pérdida esperada;
  - cobranza: ventana de la Ley 2300.
- **Pruebas de propiedad**: suma del capital del plan, reproducibilidad a (D, R), reverso «como si no hubiera
  existido» y causación idempotente.
- **Arquitectura**: unas 30 nuevas y 15 ampliadas (§2.25 de las transversales). Entre ellas:
  - `LaCarteraSoloSeTocaPorSusInterfaces`;
  - `CarteraNoConoceContabilidad`;
  - `NadieEscribeElLibroDeCarteraFueraDelRegistro`;
  - `LosDoblesDePruebaNoSeRegistranEnProduccion`;
  - `LaCarteraNoTieneValoresLegalesFijos`;
  - `LosCierresDeCarteraNoSeReescriben`.
- **e2e**: colección «Cartera e2e» con Testcontainers en los dos motores, cooperativas aisladas para lo que activa
  Cartera, recaudador y tesorería de prueba, y canales simulados.
- **Rendimiento** con `RUN_PERF_TESTS=1`: SC-007, SC-008 y SC-016.

**Target Platform**: Linux (k3s) para API y Web; navegador y app de Windows/Android para las pantallas.

**Project Type**: web-service + SPA existentes, más **un proyecto de infraestructura nuevo**
(`IngenIA365ERP.ExternalChannels`), sin servicio desplegable nuevo. Lo pesado del ciclo diario corre en un servicio
de fondo propio dentro de la API (`ProcesadorDeCartera`), con arrendamiento por cooperativa.

**Performance Goals**:
- causación diaria de 50.000 créditos vivos en menos de 30 minutos por cooperativa, sin afectar a las demás (SC-007);
  el motor solo, en menos de 60 s;
- consulta de cupo y estado en menos de 1 s y simulación en menos de 2 s, percentil 95 (SC-008);
- venta a crédito visible en Cartera en menos de 5 s, en modo en línea y sin cola (SC-005);
- archivo de descuentos de 5.000 obligaciones en menos de 1 minuto (SC-016).

**Constraints**:
- Cartera **nunca** lee ni escribe datos de otro módulo, ni otro módulo los suyos, salvo por la lista cerrada de
  interfaces (FR-008). Lo vigila una prueba de arquitectura.
- Ningún valor legal en el código: todos son valores nacionales o parámetros con vigencia, piso o techo y fuente.
- Decimal exacto, con alias de precisión (`PrecisionDeCartera`). Sin aproximaciones intermedias.
- Lo inmutable no se edita por ninguna vía: operaciones, libro, posiciones, cierres, actas, votos y mensajes
  emitidos.
- Una transacción explícita por comando. Los contratos síncronos corren en la del recaudador.
- Fecha local `America/Bogota` (`HoyLocal`), y tres fechas en cada transacción.
- Los trabajos de fondo recorren las cooperativas una por una, con arrendamiento en la base de cada una.
- Ninguna credencial de proveedor toca la base. Los dobles de prueba no existen en producción.

**Scale/Scope**:
- 17 historias, 149 requisitos y 23 criterios de éxito.
- 150 tablas nuevas (130 `LND_`, 11 `COR_`, 4 `ACC_`, 3 `ADM_`, 1 `PAY_`, 1 `INV_`), más columnas en unas doce
  tablas existentes. Retiro de 55 tablas heredadas. Unas 156 enumeraciones.
- 14 migraciones pares: una destructiva con guarda, una de la base administrativa y doce aditivas.
- Volumen esperado en una cooperativa grande: unos 40 millones de filas de libro y posiciones al año.
- 380 rutas nuevas (305 `/api/lending/*`, de ellas 10 de los dobles de prueba; 21 de informes; 54 en `core`, `saas`,
  `compliance`, `accounting` y `payroll`), 123 permisos `Lending.*` y 20 de otros módulos, trece plantillas de rol,
  62 pantallas `/cartera/*` y 9 más en Contabilidad, administración y maestros.
- 47 tipos de mensaje: 41 que emite Cartera (35 hechos contables) y 6 que recibe.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec → aclaraciones del dueño (Q1 B, Q2 A completo y configurable, Q3 y D29, COOFLOPAL sin ahorros) → seis rondas de verificación → research de ocho frentes → decisiones transversales con crítica doble → este plan → tasks. Las piezas de Comercial, Contabilidad, Nómina, Personas y plataforma se especifican aquí como enmiendas E-01 a E-05. Las enmiendas a la spec que propone el diseño (A1–A11) quedan para el dueño. |
| II   | Clean Architecture | PASS | Motores puros en Domain (`Domain/Lending/*`, `Domain/Common/{Calendar, NationalValues, Layouts, Compliance}`) sin IO. Puertos en Application (`Common/Integration/Lending`, `Common/Integration/Accounting`, `Common/Integration/Savings`). Los proveedores externos sólo en `Infrastructure/IngenIA365ERP.ExternalChannels`, con prueba de arquitectura. |
| III  | CQRS + MediatR | PASS con desviación | Todo comando tiene validador; los consumidores son comandos sin ruta y los endpoints sólo reenvían. Desviación: los métodos de escritura de `IContratoDeObligaciones` e `IReservasDeCartera` se llaman directo dentro de la unidad de trabajo del recaudador. Ejecutan su validador y escriben su evento (T101, Complexity Tracking). |
| IV   | Multi-tenancy | PASS | Todo lo de Cartera vive en la base de la cooperativa. Los valores nacionales se registran en la base administrativa y se **replican**; ningún cálculo los lee de una base compartida (I-3). `ProcesadorDeCartera` y las tareas recorren el directorio con `IEjecutorEnCooperativa`. Los arrendamientos están en cada base. |
| V    | Person centralizada | PASS | Deudores, codeudores, pagadurías, aseguradoras, abogados y miembros de órganos son personas del maestro. Los documentos emitidos y las actas guardan la copia de la identificación tal como se emitió (I-1). Las altas usan el diálogo único. |
| VI   | PublicId externo | PASS | Rutas, DTO, mensajes y archivos por `PublicId` o por el número del crédito como dato de búsqueda, nunca como identificador de ruta. |
| VII  | Soft-delete + auditoría | PASS | Todo es `AuditableEntity` con filtro. Los hechos (H) no admiten cambios. Las proyecciones son instantáneas versionadas o se calculan al leer (I-6). |
| VIII | Validación dual | PASS | Validadores de FluentValidation; las pantallas validan antes y el servidor manda. Los errores van con código `Lending.*` y mensaje en español que dice qué hacer. |
| IX   | Errores visibles | PASS | Alertas con destinatario por permiso: mensaje rechazado, proceso sin correr, usura o índice faltante, descuadre, giro sin resultado, no aplicado, garantía por vencer, coincidencia en listas, plazo regulatorio. Ningún `catch` vacío. |
| X    | Trazabilidad | PASS con desviación | Cartera, Cumplimiento y Valores nacionales entran a la auditoría encadenada, a diez años. Se auditan también las lecturas sensibles y el intento y desenlace de cada llamada al contrato. Desviación: la causación del proceso se audita con un evento resumen por tanda, con huella, conteos y totales, porque el libro es inmutable y verificable contra la reconstrucción (T101). |
| XI   | Inmutabilidad | PASS | `LND_LoanTransactions` es el objeto literal del Principio XI (I-5). Los reversos son filas espejo con `ReversesTransactionId`; las correcciones contables, comprobantes nuevos. Los cierres no se reescriben: la reapertura es otro hecho. |
| XII  | Migraciones | PASS | `RetiroDeLaCarteraHeredada` es destructiva con guarda: se niega si hay filas, salvo la aprobación por base con respaldo y segundo revisor. Lleva el marcador `MIGRACION-DESTRUCTIVA-APROBADA` y va en su propio commit. Las demás son aditivas. Las semillas son idempotentes. `NominaRecaudaPorElContrato` copia `PublicId` y número desde `LND_LoanPortfolios` a `PAY_SettlementDeductions` y `PAY_Novelties`: no es semilla sino una transformación idempotente de datos existentes, necesaria antes del retiro (C73). |
| UI   | Indicador de carga; sin colores literales | PASS | Pantallas nuevas en `Shared/Pages/Cartera` con `IndicadorDeCarga` y `EstadoDeCarga` por zona. `Cartera` entra a `ModulosMigrados`, y el menú apunta sólo a páginas con `@page`. |

## Project Structure

### Documentation (this feature)

```text
specs/013-cartera-financiera/
├── plan.md                        # este archivo
├── spec.md                        # especificación con las aclaraciones del dueño
├── decisiones-transversales.md    # nombres canónicos (§2), T1–T102, choques CH1–CH50, preguntas A1–I17, conciliación C01–C102 (§7)
├── research.md                    # Fase 0: hechos del código por frente, decisiones y riesgos
├── data-model.md                  # Fase 1: tablas, columnas, índices, estados, enums, errores, retiro
├── quickstart.md                  # Fase 1: pruebas automáticas y ensayo por entrega
├── contracts/
│   ├── api.md                     # convenciones, permisos y rutas de todo el módulo
│   ├── mensajes.md                # catálogo de mensajes, sobre, destinos, registro de participantes
│   ├── interfaces.md              # contrato de obligaciones, orden de giro y puertos con otros módulos
│   ├── contabilidad.md            # enmienda E-01: matriz, consumidor, cierre, conciliación
│   ├── adaptadores-y-archivos.md  # canales externos, Ley 2300, archivos planos, layouts, documentos
│   └── plantillas.md              # plantillas de importación de Cartera
├── checklists/requirements.md
└── tasks.md                       # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Lending/{Configuration,Origination,Governance,Loans,Schedules,Operations,Transactions,
│                     Projections,Integration,Risk,Payroll,Collections,Documents,Migration}
├── Entities/Savings/                     # las 38 entidades heredadas que se conservan, sin cambiar su tabla
├── Enums/Lending/
├── Lending/{Rates,DayCount,Schedules,Accrual,Delinquency,Payments,Replay,Risk,Risk/Normativa,
│            Attributions,Sessions,Segregation,Collections,Payroll,Documents}   # motores puros
└── Common/{Calendar,NationalValues,Layouts,Compliance,VigenciaBitemporal}

src/Core/IngenIA365ERP.Application/
├── Lending/{Engine,Ledger,Accrual,Settlement,Payments,Common,Integration,Origination,Sessions,Attributions,
│            Segregation,Disbursements,Risk,Payroll,Collections,Documents,Regulatory,Reports,Imports,
│            Migration,Activation,Adapters}
├── Accounting/Lending/{Reglas,Contabilizacion,Lotes,Consultas}        # enmienda E-01 de la 009
├── Common/Integration/{Lending,Accounting,Savings,Contracts/Lending}   # puertos y contratos de mensaje
├── Common/{NationalValues,Calendar,Messaging,Compliance}
├── Saas/NationalValues/                                               # registro central (maestro)
└── Savings/                                                           # handlers heredados que se conservan

src/Infrastructure/
├── IngenIA365ERP.Persistence/  Configurations/{Lending,Savings,Accounting,Core,Admin}, Lending/CerrojoDeCartera.cs,
│                               Seeding/Parametric/* (tipos de hecho, comprobantes, plantillas de rol, documentos normativos)
├── IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/   # 14 pares (decisiones-transversales §2.26)
├── IngenIA365ERP.ExternalChannels/  # PROYECTO NUEVO: Messaging, Securities, CreditBureaus, RestrictiveLists,
│                                    # Credentials, Circuit, con canal SIMULADO en cada uno
└── IngenIA365ERP.Identity/  Seed/LendingPermissionCatalogSeeder

src/Presentation/
├── IngenIA365ERP.API/  Endpoints/Lending/*, Endpoints/Savings/*, Endpoints/Reports/LendingReportsEndpoints,
│                       Endpoints/Accounting/LendingIntegrationEndpoints, Endpoints/Saas/NationalValuesEndpoints,
│                       Integration/ProcesadorDeCartera, Reports/DocumentosDeCarteraReport
└── IngenIA365ERP.Shared/  Pages/Cartera/*, Pages/AhorrosYAportes/* (lo heredado que se conserva),
                           Pages/Contabilidad/Cartera/*, Services/Cartera/*Client*.cs

tests/
├── IngenIA365ERP.Domain.Tests/Lending/{Casos,Risk/Casos,Collections/Casos}/
├── IngenIA365ERP.Application.Tests/{Lending,Accounting/Lending,Common/NationalValues,Payroll,Inventory}/
├── IngenIA365ERP.Architecture.Tests/Principles/   # las de decisiones-transversales §2.25
└── IngenIA365ERP.API.IntegrationTests/Lending/    # colección «Cartera e2e»
```

**Structure Decision**: se extienden los proyectos existentes con el molde de las features 009, 010 y 012.

- **Un solo proyecto nuevo**, `Infrastructure/IngenIA365ERP.ExternalChannels`: es el único lugar donde pueden vivir
  los clientes de los proveedores externos (depósito de valores, centrales, SMS, WhatsApp, listas) sin que Domain,
  Application ni la API los conozcan.
- **Lo compartido va a `Common`** para que otros módulos lo reutilicen: valores nacionales, calendario hábil,
  layouts regulatorios, mensajería a personas y cumplimiento.
- **Prefijos**: crédito en `LND_`, lado contable en `ACC_Lending*`, plataforma en `COR_`, valores nacionales en
  `ADM_`. No se crean prefijos nuevos.
- **Lo heredado que se conserva** (ahorros, aportes, retiros, caja, SIPLA) se muda de espacio de nombres a `Savings`,
  sin cambiar tablas ni rutas de pantalla (enmienda A10).

## Complexity Tracking

| Punto | Por qué | Alternativa descartada |
|---|---|---|
| **Desviación III y X**: el contrato de obligaciones y las reservas se llaman directo, fuera del pipeline de MediatR (T101) | Corren dentro de la unidad de trabajo del recaudador (I-4): Nómina aprueba la definitiva «todo o nada». Por `ISender`, un reintento anidado vacía el `ChangeTracker` de quien llama. Precedente: `RecaudoDeCredito`. Mitigación: cada método ejecuta su validador y escribe su evento; lo fija `LosPuertosDeCarteraValidanYAuditanSusEscrituras`. | Pasar por `ISender`: rompe el «todo o nada» o exige transacciones distribuidas entre módulos de la misma base. |
| **Desviación X**: la causación del proceso se audita con un evento resumen por tanda (T101) | Unas 150.000 filas al día con 50.000 créditos saturarían la auditoría. El libro es inmutable y `VerifyLendingLedgerIntegrityQuery` lo compara con la reconstrucción. | Un evento por fila: volumen inmanejable y sin valor de trazabilidad adicional. |
| **Desviación X ampliada**: `[SinDiffDeAuditoria]` también en las filas de control (`LoanControl`, `DebtorControl`), las partes de hechos, el historial de mora, las líneas de archivos de descuento y regulatorios y las 17 tablas de preparación, cada una con su evento resumen por corrida, archivo o importación (C46) | Son filas de control o de volumen sin decisión humana propia; una migración de 50.000 créditos dejaría ≈1,5 millones de diffs. El evento resumen lleva huella, conteos y totales. | Auditar fila por fila: volumen inmanejable; filas de control sin datos de negocio. |
| **Activación en una transacción con guardados por tandas** (C60) | La activación de 50.000 créditos y ≈1,5 millones de cuotas no cabe en un solo `SaveChanges`. Una transacción explícita con guardados de 1.000 créditos y `ChangeTracker.Clear()` conserva el «todo o nada» y acota la memoria. Se mide bajo `RUN_PERF_TESTS`. | Una corrida `Activation` por tandas con transacciones separadas: deja estados intermedios visibles y obliga a deshacer filas inmutables si falla. |
| **Convención de nombres**: motores, puertos, DTO de puerto, mensajes, parámetros y pruebas en español, y DTO de entrada de puertos sin sufijo `Dto` (T101) | Precedente vivo de la 012. Un solo criterio en el código. | Renombrar todo a inglés: dos criterios conviviendo, o reescribir la 012. La alternativa limpia es una enmienda constitucional. |
| Proyecto nuevo `IngenIA365ERP.ExternalChannels` | Los proveedores son intercambiables y su cliente no debe contaminar Application ni la API. | Ponerlos en la API: una prueba de arquitectura no podría aislarlos. |
| Piezas de plataforma nuevas: valores nacionales con réplica, registro de participantes, bandeja con varios emisores, mensajería a personas, layouts | La spec las exige (FR-015, FR-142, FR-149) y hoy no existen. Se construyen reutilizables en `Common`. | Resolverlas dentro de Cartera: Nómina, Comercial y Recaudo tendrían que duplicarlas. |
| El proceso automático origina mensajes (pregunta B1, T11) | La causación diaria, el cierre y el consumidor de Comercial corren como proceso; sin esto Cartera no puede emitir ningún hecho automático. Se limita a los tipos de Cartera marcados, con el usuario de origen como dato. | Mantener la regla de la 012 «sólo la persona emite»: deja sin hechos la causación y el cierre. |
| Decisiones de crédito en un modelo propio de sesiones y actas, no en el motor `COR_Approval*` de la 012 (pregunta F1) | Quórum, votos, 4/5 de la composición para vinculados y exclusión de quien radicó no caben en el motor de aprobaciones. Se reutilizan sus primitivas: límites por permiso y segregación por persona. | Generalizar el motor de la 012: cambio de plataforma de alto riesgo para una sola necesidad. |
| Migración destructiva de 55 tablas | El dueño decidió crear Cartera desde cero; las tablas heredadas de crédito no tienen datos (a confirmar con el diagnóstico). | Convivir con ellas: dos fuentes de cartera y llaves cruzadas desde Nómina. |

## Decisiones

Se adoptan las decisiones T1–T102 de [decisiones-transversales.md](decisiones-transversales.md) y las de
[research.md](research.md). Resumen por tema:

| Tema | Decisión del plan |
|---|---|
| **Nombres y módulo** (T1–T4) | Inglés para entidades, tablas, rutas, permisos y errores; español para motores, puertos, mensajes, parámetros y pruebas (desviación registrada). Módulo `Lending`; rutas `/api/lending/*`; lado contable `/api/accounting/lending/*`; valores nacionales `/api/saas/*`. |
| **Ejecución y actor** (T5–T9) | `IEjecutorEnCooperativa` con el actor del proceso de la cooperativa (`Actor.ProcesoDeIntegracion`, sin IP); una orden manual encola y la corrida firma con quien la ordenó. `ProcesadorDeCartera` con arrendamiento por cooperativa para el ciclo diario y el cierre; tareas livianas por `ITareaProgramada`. |
| **Mensajes** (T10–T18) | Catálogo agregado con varios emisores (INV, CAR, TES, REC). El origen es el documento u operación de Cartera, nunca el crédito. Las dependencias apuntan sólo al original. No hay cadena por crédito hacia Contabilidad; la cadena de orden existe sólo en las respuestas a Comercial, con la venta como raíz. Los hechos masivos se emiten por partes. `VentaACreditoRegistrada` pasa a v2 con el número reservado. Un solo consumidor por destino; el destino Accounting usa un enrutador para sus dos emisores. |
| **Idempotencia** | `Idempotency-Key` en toda escritura de pantalla. En el contrato, la clave es por clase de recaudador. La causación es idempotente por crédito y fecha. |
| **Motor financiero** | Hechos primarios y libro inmutables. La liquidación se reconstruye a (D, R). Causación por diferencia de acumulados, tope de usura del día y suspensión. Plan versionado con `RulesVersion`. Prelación de cuota completa. Simulación con huella y sin estado, con dos formas de aplicar. Reverso con reconstrucción de los pagos posteriores. |
| **Fechas y valores nacionales** | Tres fechas. Registro central en la base administrativa y réplica bitemporal. Lectores únicos `IValoresNacionales`, `ICalendarioHabil` e `INormativaVigente`. Pisos y techos legales como valores nacionales. |
| **Riesgo y cierres** | Documentos normativos tipados `SarcAnexo1` y `SarcAnexo2`, más la política SARC de la cooperativa, más estricta. Calificación en dos fases con arrastre por modalidad. Cierre mensual inmutable con R sellado, hechos por diferencia y reapertura del último. Cierre base de la migración. Pérdida esperada en K6a. |
| **Contabilidad** (E-01) | `ACC_LendingPostingRules` con dimensiones por código y especificidad; comprobantes `CN` y los sembrados de Cartera; comprobante resumido de la causación que conserva dimensiones; reclasificación completa al cierre; validación previa (A8); conciliación y cuadre de activación; puente de Nómina para `DESC_CARTERA` (C1). |
| **Seguridad y actas** | 123 permisos `Lending.*` y trece plantillas de rol. Alcance por oficina y por línea que falla cerrado. Atribuciones con matriz versionada y enrutador puro. Sesiones y actas propias. Segregación por persona con registro de intervenciones. |
| **Habeas data y auditoría** | Autorizaciones por finalidad en la plataforma; reclamos y suplantación en Cartera. Cartera, Cumplimiento y Valores nacionales en la cadena de auditoría, más las lecturas sensibles. |
| **Adaptadores y mensajería** | Puerto por proveedor, registro por código y canal simulado con guardia de efecto legal. Mensajería saliente a personas en la plataforma; cola con `VentanaLegalDeContacto` en Cartera. Aviso previo por episodio con constancia. |
| **Archivos y documentos** | Archivo de desembolsos y archivos de pagaduría con el motor común (ámbitos nuevos). Layouts SICSES y de centrales como JSON con cotejo por campo. Documentos con código de verificación y estado calculado. |
| **Migración y activación** | Carga por partes con un cuadre y una conciliación de ventas; cobros provisionales por plantilla. Activación el día siguiente al corte, con requisitos verificables contra el registro de participantes. Antes de activar, las interfaces responden sin error: las consultas de Comercial con «Cartera no habilitada» y el contrato de obligaciones rechazando con «Cartera no activada». |
| **Retiro de lo heredado** | Se retiran 55 tablas, sus rutas y pantallas de crédito, y el proyecto `IngenIA365ERP.Legacy`. Las 38 conservadas se mudan a `Savings`. Los consumidores pasan antes a los puertos «sin activar». |
| **Enmiendas a otros módulos** | Comercial: consultas reales, reservas, v2 y consumidor de respuestas. Nómina: recauda por el contrato, la definitiva «todo o nada», la ordinaria como pagaduría interna, y el SMMLV y los festivos desde la réplica. Personas: consulta de obligaciones. |
| **Conciliación de los artefactos** (§7, C01–C102) | Cada artefacto cerró con sus puntos abiertos y se decidieron en un solo sitio. Las que cambian el diseño: la unidad de emisión es la operación con su modo (C01); la orden pagada por archivo nace al desembolsar con `PaidByFile` (C10); `Answered` en el resultado del contrato (C28); cuatro tablas `LND_` nuevas, entre ellas `LND_LoanPayrollLinks` y `LND_ProvisionalCollections` (C44); reverso de un tramo sólo por su capital (C49); activación en una transacción con guardados por tandas (C60); `recordedAsOf` para el instante R (C83); el permiso `Lending.PrepaymentPreferences.Manage` (C85); la plantilla `cartera.administrador` sin decisiones de crédito (C86). |

## Entregas

Sin fechas: las define el dueño. **K1 es lo mínimo para salir en vivo.** Además, en producción hacen falta Recaudo y
Tesorería reales, y K6a si la cooperativa estuviera obligada (COOFLOPAL no lo está). Todas las entregas son parte
de esta feature.

| Entrega | Contenido | Migraciones |
|---|---|---|
| **K1 · Núcleo y riesgo básico** | Retiro de lo heredado, con los consumidores primero en los puertos «sin activar». Plataforma: valores nacionales y réplica, registro de participantes, bandeja con varios emisores, canales externos simulados, mensajería a personas, listas, solicitudes del titular. Líneas, originación con actas, garantías, cupos y reservas. Desembolso y orden de giro. Motor completo y ciclo diario. Contrato de obligaciones. Comercial, Nómina y Personas por las interfaces. Hechos contables y matriz. Calificación y deterioro del Anexo 1, cierre mensual y conciliación. Migración, cierre base y activación. Seguridad y auditoría. | `NominaRecaudaPorElContrato`, `ComercioConCartera`, `RetiroDeLaCarteraHeredada` (destructiva, commit propio), `ValoresNacionales` (Admin), `PlataformaParaCartera`, `CarteraNucleo`, `CarteraRiesgo`, `IntegracionContableDeCartera` |
| **K2 · Libranza** | Pagadurías y convenios, prelación, archivos de ida y vuelta, pendientes de giro, no aplicados, traslado, convenio de la nómina propia. | `CarteraLibranza` |
| **K3 · Novedades** | Novedades por causa, consentimiento de garantes, condonación, insolvencia, castigo, evaluación de cartera. | `CarteraNovedades` |
| **K4 · Cobranza** | Etapas, gestores, gestiones, promesas, acuerdos, cobro jurídico, campañas, aviso previo con constancia, reclamos, SMS y WhatsApp reales con contrato. | `CarteraCobranza` |
| **K5 · Informes** | Extracto y certificado tributario, reportes de gestión, cosechas, concentración, tablero, SICSES y centrales. El plan de pagos, el paz y salvo y el certificado de saldo se adelantan a K1 (pregunta I5). | `CarteraInformes` |
| **K6a · Pérdida esperada** | Modelo de referencia completo y configurable, adopción y desistimiento voluntarios, carga complementaria del historial. Se adelanta antes de la primera cooperativa obligada. | `CarteraPerdidaEsperada` |
| **K6b · Vivienda** | Ley 546, tope de vivienda y UVR. | `CarteraVivienda` (sólo si hace falta) |

Orden de migraciones en K1:
1. `NominaRecaudaPorElContrato` y `ComercioConCartera`, con los consumidores ya en los puertos «sin activar».
2. `RetiroDeLaCarteraHeredada`, con modelo intermedio.
3. `ValoresNacionales`.
4. `PlataformaParaCartera`.
5. `CarteraNucleo`.
6. `CarteraRiesgo`.
7. `IntegracionContableDeCartera`.

## Riesgos y lo que falta del dueño

El detalle está en decisiones-transversales.md §5 y §7.7: 100 preguntas, cada una con su propuesta por defecto. Si
el dueño no responde, **rige la propuesta**. Las que más pesan:

- **Enmiendas a la spec (A1–A11)** que el diseño necesita. Se plantean al dueño junto con este plan. Entre ellas:
  - la lista cerrada de interfaces ampliada (A1);
  - el dato de Comercial para vender servicios a crédito sin Cartera (A6);
  - Nómina con proyección de valores nacionales (A7);
  - el bloqueo por falta de regla contable sólo en pantallas (A8);
  - la exención de fecha valor para los pagos sin interacción (A9);
  - la mudanza de lo conservado a `Savings` (A10).
- **Antes de cerrar K1**:
  - que el proceso pueda originar mensajes (B1);
  - qué es un «despliegue de producción», y que QA no corra como Production (B4);
  - el puente contable de Nómina para `DESC_CARTERA` (C1);
  - las actas en un modelo propio (F1).
- **Para el retiro**: el diagnóstico en cada ambiente, el segundo revisor y la aprobación por base (H7).
- **Para la salida de COOFLOPAL**: si el primer vencimiento del formato de cartera del SICSES llega antes que K5, se
  adelanta a K1 (E11).
- **Semillas que valida la contadora**:
  - la tasa de mora como factor de la corriente y los defectos de parámetros (cupo y bloqueo por mora);
  - el centro de costo de la matriz, que se asigna y no se compara (C77);
  - tablas del Anexo 1;
  - catálogo de modalidades con los sub-segmentos de microcrédito y las productivas (E4, E10);
  - tipos de comprobante;
  - cuentas de la matriz;
  - el concepto tributario del interés de las líneas de origen comercial (C11, D20).
- **Para salir en vivo**: las features de **Recaudo y Tesorería**. Hoy no tienen especificación y son el mayor riesgo
  para el calendario de COOFLOPAL.

Riesgos principales:
- **Tamaño de K1**: carga la plataforma, el motor, el riesgo básico, la contabilidad, la migración, el retiro y tres
  enmiendas a módulos en producción.
- **Volumen y rendimiento** del libro y de las posiciones diarias. Se mide pronto con `RUN_PERF_TESTS`.
- **Cambios en Nómina en producción**: recaudo por el contrato síncrono y la ordinaria en transacción explícita.
- **Valores nacionales como infraestructura nueva**: la reproducibilidad del motor depende de su bitemporalidad.
- **Exactitud de los casos dorados** de tasas en decimal, y del cotejo de las tablas normativas transcritas.

## Re-evaluación de la constitución tras el diseño

Re-evaluada el 2026-10-05 sobre los artefactos de la fase 1 ya conciliados (§7 de las transversales, C01–C102).
**Resultado: PASS**, con las desviaciones del Complexity Tracking y ninguna violación nueva sin justificar.

| #    | Principio | Estado tras el diseño | Qué lo sostiene en los artefactos |
|------|-----------|-----------------------|-----------------------------------|
| I    | Spec-First | PASS | Todo lo que el diseño agrega a la spec está como enmienda (A1–A11) o pregunta con propuesta por defecto (100 en total, §5 y §7.7). Ningún artefacto deja «puntos para conciliar» abiertos: los 186 se decidieron en la §7 (C01–C102). |
| II   | Clean Architecture | PASS | Los cinco puertos ofrecidos viven en `Common.Integration.Lending` (C27). `LaCarteraSoloSeTocaPorSusInterfaces` vigila las dos direcciones (C34). Contabilidad elige las cuentas de pérdidas por clase y naturaleza, no por código (C78). |
| III  | CQRS + MediatR | PASS con desviación | Se mantiene la desviación del contrato síncrono. El contrato verifica el permiso y el límite del usuario que actúa (C41). La reapertura del cierre se encola como corrida (C58). |
| IV   | Multi-tenancy | PASS | La corrección de un valor nacional se dispara desde la réplica en cada base (C05). La alerta al maestro por valores faltantes no lee cooperativas (C69). `COR_Alerts` suma oficina y línea para el alcance (C68). |
| V    | Person centralizada | PASS | Pagadurías, miembros de órganos y personas simuladas son personas del maestro. Bajo códigos `View` sólo se muestran nombre, cargo y contacto institucional (C84). |
| VI   | PublicId externo | PASS | Las claves de mensaje y de recaudador van por `PublicId` (C02, C12, C35). La orden pagada por archivo no expone el archivo en ningún mensaje (C10). |
| VII  | Soft-delete + auditoría | PASS | La marca H se amplió a toda versión y evidencia (C42). Los votos se corrigen con un voto de reemplazo, no editando (C75). |
| VIII | Validación dual | PASS | Los códigos de error son literales y únicos: `data-model.md` §10 manda (C71, C98) y `api.md` usa los mismos. |
| IX   | Errores visibles | PASS | Hay alerta para la venta no incorporada (C99) y para el hecho no contabilizable (C21). El mensaje no convertible queda visible y reprocesable (C95). |
| X    | Trazabilidad | PASS con desviación | Se amplió el evento resumen a las filas de control y de volumen (C46, Complexity Tracking). Las lecturas sensibles tienen una lista cerrada (C89). |
| XI   | Inmutabilidad | PASS | El reverso de un tramo no duplica filas (C49), el destino del mensaje se copia en el evento (C57), los cobros provisionales son `[EscrituraUnica]` (C62) y la base administrativa pasa a tener guarda de inmutabilidad (C68). |
| XII  | Migraciones | PASS | La copia de datos de `NominaRecaudaPorElContrato` es una transformación idempotente, declarada (C73). La activación es atómica: una transacción con guardados por tandas (C60). Los arreglos a tablas existentes (`COR_NotificationTemplates`, `CMP_HabeasDataPolicyVersions`) van en migraciones aditivas con índices rehechos, sin pérdida de filas. |
| UI   | Indicador de carga; sin colores literales | PASS | Sin cambios respecto del chequeo inicial. Son 62 pantallas `/cartera/*` con `IndicadorDeCarga`. |

**Lo que el diseño deja para `/speckit-tasks`**:
- el paso de despliegue de K4 que registra la etapa de cobranza migrada (C64);
- la prueba de volumen de la activación con 50.000 créditos (C60);
- la prueba de volumen del POS con el orden de cerrojos (C24);
- las enmiendas a la 012 (T97, C16, C18), que tienen que ir antes de las tareas de mensajes de K1.

**Antes de cerrar K1 sigue haciendo falta la respuesta del dueño** a B1, B4, C1, F1 y H7. Mientras no responda, rige
la propuesta por defecto.
