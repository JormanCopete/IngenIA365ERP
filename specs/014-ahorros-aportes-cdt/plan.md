# Implementation Plan: Cartera financiera — captaciones: ahorro a la vista, contractual y permanente, CDAT y aportes sociales

**Branch**: `014-ahorros-aportes-cdt` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-ahorros-aportes-cdt/spec.md`

## Summary

La feature agrega las **captaciones** (ahorro a la vista, contractual y permanente, CDAT) y los **aportes
sociales** a la Cartera financiera de la 013, como parte integral del mismo módulo. Retira todo lo
heredado de ahorros, aportes y CDT (38 tablas conservadas y 7 `CDT_*`) y renueva lo que se necesita.
Se construye **cuando la 013 esté completa** y se termina, entera, **antes de la salida de COOFLOPAL**
(Q1, Q3).

**Qué construye**:
- **Línea con naturaleza**: el catálogo de líneas de la Cartera admite colocación, captación y aportes;
  cada naturaleza tiene su versión de condiciones.
- **Cuentas del asociado** (`MemberAccount`) con titularidad, beneficiarios, menores, bloqueos, embargos,
  cargos, débito automático y exención de GMF.
- **Cálculo**: causación diaria por método de saldo y tramos, liquidación con retención (umbral diario
  sobre el total, componente inflacionario) y GMF configurable hasta su eliminación; CDAT con plan
  vencido o anticipado, gracia, prórroga, renovación, redención y tasa negociada; ahorro contractual con
  bonificación; revalorización y excedentes; fondo de liquidez.
- **Procesos**: retiro del asociado completo en la Cartera (solicitud, acta, cruce, devolución),
  fallecimiento, inactividad y no reclamados.
- **Integración**: contrato de cuentas para Recaudo, Tesorería y Nómina; pagadurías configurables por
  convenio; hechos a Contabilidad por la matriz de la Cartera; cargos gravados facturados por Comercial.
- **Puesta en marcha**: migración con cuadre y activación por naturaleza; documentos al titular;
  informes de gestión y regulatorios.

**Cómo** (detalle en [research.md](research.md), cuyos nombres canónicos mandan):
- **Motores puros** en `Domain/Lending/Deposits` sobre los motores comunes ya verificados de la Cartera
  (`EquivalenciaDeTasas`, `ContadorDeDias`, `Causacion`, `RedondeoDeCartera`, `CalendarioHabil`), con
  casos dorados al centavo (R05–R07, R11–R13, R25).
- **Libro propio** con las reglas del libro del crédito: operación como hecho primario, transacción por
  concepto con tres fechas y reverso espejo, posición diaria versionada, marca de agua y escritor único
  `RegistroDeCuentas` (R03, R04).
- **Un solo proceso por cooperativa**: el ciclo diario de la Cartera suma el paso de captaciones y el
  cierre mensual su sección (R10).
- **Un puerto nuevo** `IContratoDeCuentas` en la lista cerrada, con las reglas del contrato de
  obligaciones; los cruces y el débito automático son operaciones internas (R09, R15, R16).
- **Misma matriz contable** con roles y dimensiones agregados al final (R19).
- **Activación por naturaleza** en `LND_Setup`, después de la de créditos (R21).

## Technical Context

**Language/Version**: C# / .NET 10.0.5. Blazor (Shared, Web, Web.Client y MAUI) con Syncfusion 33.2.8 por
paquetes de componente.

**Primary Dependencies**: las de la 013 sin agregar ninguna: Carter, MediatR + FluentValidation, EF Core 10
con migraciones pares PostgreSQL y SQL Server, QuestPDF, ClosedXML/OpenXML, el motor de archivos planos
de la plataforma (`FlatFileWriter`, más un `FlatFileReader` nuevo para las respuestas de pagaduría) y los
layouts regulatorios como dato (`Domain/Common/Layouts`). Domain sin librerías nuevas.

**Storage**:
- Base de cada cooperativa: **46 tablas `LND_*` nuevas** (incluidas las de preparación de la migración y las 2 del convenio de pagaduría)
  y columnas o valores nuevos en 19 tablas existentes (`LND_CreditLines`, `LND_Setup`, `LND_NumberSequences`,
  `ACC_LendingPostingRules`, `COR_BankFileFormats`, los documentos emitidos y otras; ver data-model.md).
- **Se borran** 38 tablas `LND_*` conservadas y 7 `CDT_*` (migración destructiva con guarda, R28).
- Base administrativa: cuatro códigos de valor nacional nuevos (R26, C09), replicados a cada cooperativa.
- Mongo: la auditoría encadenada de la Cartera, sin colecciones nuevas.

**Testing**: xUnit con los proyectos existentes: `Domain.Tests` (motores y casos dorados),
`Application.Tests` (handlers y validadores con NSubstitute), `Architecture.Tests` (enmiendas y pruebas
nuevas de R29), `Shared.Tests` (pantallas) y `API.IntegrationTests` (colección «Captaciones e2e» sobre
`CentralIdentityApiFixture`, en los dos motores, R30).

**Target Platform**: la API y Blazor en el clúster Kubernetes de DEV, QA y producción; MAUI en escritorio y
móvil.

**Project Type**: web-service + aplicación Blazor (monorrepositorio .NET).

**Performance Goals**: causación diaria de 100.000 cuentas en menos de 30 minutos por cooperativa (SC-006);
operación de caja en menos de 1 minuto de punta a punta (SC-004); revisión de una carga de 50.000 cuentas
en menos de 10 minutos (SC-011).

**Constraints**: decimal exacto sin `double` ni `Math.Pow`; ningún valor legal fijo; ninguna operación
fuera del contrato o de las pantallas con permiso; una corrida de la Cartera a la vez por cooperativa;
sin tocar la 013 hasta que esté completa; ningún nombre de tabla heredado reutilizado.

**Scale/Scope**: hasta 100.000 cuentas y 20.000 operaciones diarias por cooperativa. 168 rutas nuevas (74 heredadas retiradas),
unas 20 pantallas, 39 permisos, 21 vistas de informe, 9 tipos de documento, 33 hechos contables (41 mensajes) y
5 entregas (K7 a K11).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | Spec clarificada (2026-10-14) → este plan → tasks |
| II   | Clean Architecture | PASS | Motores en `Domain/Lending/Deposits` sin dependencias; puertos en Application; la Cartera no conoce Contabilidad (`CarteraNoConoceContabilidad` cubre el espacio nuevo) |
| III  | CQRS + MediatR | PASS con desviación | Comandos con validador. Desviación 1 (Complexity Tracking): `IContratoDeCuentas` se llama directo, dentro de la unidad de trabajo del recaudador, no por `ISender` |
| IV   | Multi-tenancy | PASS | Procesos por `IEjecutorEnCooperativa`; valores nacionales replicados; ninguna lectura de `ADM_` en el cálculo |
| V    | Person centralizada | PASS | Titulares, firmantes, representantes y beneficiarios son `COR_People` (I-1); la fecha de nacimiento se lee de Personas |
| VI   | PublicId | PASS | Rutas y DTOs sólo con PublicId y número de cuenta |
| VII  | Soft-delete + auditoría | PASS | Cierre de cuenta es estado (I-3); hechos inmutables con la excepción justificada de la 013 (`IHechoInmutable`) |
| VIII | Validación dual | PASS | Validadores de servidor y pantallas con validación y mensajes con código |
| IX   | Errores visibles | PASS | Errores de corrida con alerta; sin `catch` silenciosos |
| X    | SIPLA/SARLAFT | PASS con desviación | Auditoría encadenada de diez años; origen de fondos sobre umbral. Desviaciones 2 y 3 (Complexity Tracking): causación auditada por resumen de corrida; tablas de preparación de migración sin diff |
| XI   | Inmutabilidad contable | PASS | Libro de cuentas sólo inserción con reverso espejo (I-2) |
| XII  | Migraciones | PASS | Retiro destructivo con guarda, diagnóstico, respaldo y segundo revisor (R28); migraciones pares |
| UI   | Indicador de carga y sistema de diseño | PASS | `LasPantallasDicenQueEstanCargando` suma el módulo (R31) |

**Re-evaluación tras el diseño (Phase 1)**: PASS con las tres desviaciones de Complexity Tracking, que son las
mismas que la 013 justificó y aprobó (T101 y causación por lotes), extendidas a captaciones. Las demás enmiendas son
a pruebas de arquitectura de la 013 (frontera `Lending`, lista cerrada de puertos), no a principios.

## Project Structure

### Documentation (this feature)

```text
specs/014-ahorros-aportes-cdt/
├── spec.md                         # Especificación clarificada
├── investigacion-financiera.md     # Fundamento financiero y diagnóstico
├── plan.md                         # Este archivo
├── research.md                     # Decisiones y nombres canónicos (mandan)
├── data-model.md                   # Tablas, campos, estados
├── quickstart.md                   # Cómo probar cada historia
├── diagnostico-captaciones-heredadas.sql
├── contracts/
│   ├── api.md
│   ├── contrato-de-cuentas.md
│   ├── mensajes.md
│   ├── contabilidad.md
│   ├── plantillas-y-archivos.md
│   └── documentos.md
├── checklists/requirements.md
└── tasks.md                        # /speckit-tasks
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Lending/Deposits/                 # Motores puros (research §0.4)
├── Entities/Lending/Deposits/        # MemberAccount, libro, CDAT, cargos, retiro, asamblea, liquidez
├── Entities/Lending/Configuration/   # CreditLine.Nature, DepositLineVersion, ContributionLineVersion…
├── Enums/Lending/DepositEnums.cs
└── Common/NationalValues/            # Códigos nuevos (R26)

src/Core/IngenIA365ERP.Application/
├── Lending/Deposits/                 # Lines, Accounts, Operations, Ledger, Accrual, TermDeposits,
│                                     # Contributions, Holds, Charges, Withdrawals, Assembly, Liquidity,
│                                     # Documents, Reports, Migration, Activation
├── Lending/Integration/              # ContratoDeCuentasDeCartera, ConsultaDeAportesDeCartera
├── Common/Integration/Lending/       # IContratoDeCuentas, IConsultaDeAportes (movido)
├── Common/Integration/Contracts/Lending/Deposits/   # Mensajes *V1
├── Accounting/Lending/               # Roles, dimensiones y hechos nuevos
├── Payroll/                          # DESC_APORTES, DESC_AHORROS (E-02)
└── Common/BankFiles/                 # FlatFileReader (respuestas de pagaduría)

src/Infrastructure/IngenIA365ERP.Persistence/
├── Configurations/Lending/Deposits/
└── (migraciones pares) CaptacionesAportes, CaptacionesAhorro, CaptacionesCdat, CaptacionesAsamblea, CaptacionesLiquidez, RetiroDeCaptacionesHeredadas

src/Presentation/
├── IngenIA365ERP.API/Endpoints/Lending/Deposits/
└── IngenIA365ERP.Shared/Pages/Cartera/Captaciones/

tests/
├── IngenIA365ERP.Domain.Tests/Lending/Deposits/(Casos/)
├── IngenIA365ERP.Application.Tests/Lending/Deposits/
├── IngenIA365ERP.Architecture.Tests/Principles/   # Enmiendas y pruebas nuevas (R29)
└── IngenIA365ERP.API.IntegrationTests/Lending/Deposits/   # «Captaciones e2e»

Se retiran: Domain/Entities/{Savings,CDT}, Application/{Savings,CDT}, Endpoints/{Savings,CDT},
Shared/Pages/AhorrosYAportes, Configurations/{Savings,CDT}.
```

**Structure Decision**: las captaciones viven dentro del espacio `Lending` de la Cartera, en subcarpetas
`Deposits` de cada capa, para que el módulo sea uno (pedido del dueño) y las fronteras de la 013 lo cubran
sin excepciones nuevas.

## Orden de construcción y entregas

Todas después de terminar la 013 y todas antes de la salida de COOFLOPAL. Cada entrega deja verde todo lo
anterior.

| Entrega | Bloques (orden sugerido para `/speckit-tasks`) |
|---|---|
| Fundacional | Retiro de lo heredado (diagnóstico, migración destructiva con guarda `RetiroDeCaptacionesHeredadas` y retiro del código conservado, R28), enmiendas de frontera y puertos (R29), `CreditLine.Nature` con colocación por defecto y 32 casos dorados de crédito verdes, enums, valores nacionales y parámetros (R26), libro de cuentas y escritor único, cerrojo, paso vacío del ciclo diario, sección vacía del cierre, activación por naturaleza sin cuentas, permisos sembrados, diagnóstico de lo heredado |
| K7 · Aportes | Línea de aportes, cuenta, cuotas, contrato de cuentas (aportes), consulta de aportes v2, DESC_APORTES, pagadurías configurables (E-05), hechos y roles, migración de aportes, retiro del asociado con acta y cruce, constancias |
| K8 · Ahorro | Líneas de captación, apertura con titulares y menores, contrato completo, motores de interés, retención y GMF, liquidación, débito automático, bloqueos, embargos, cargos y facturación por Comercial, extractos y certificados, migración y activación de captaciones |
| K9 · CDAT y contractual | Plan de CDAT, constitución, gracia, prórroga, renovación, redención, tasa negociada, título; contractual con bonificación |
| K10 · Asamblea y ciclo de vida | Revalorización y excedentes, inactividad, no reclamados, fallecimiento |
| K11 · Liquidez e informes | Fondo de liquidez, depósitos asegurados, SICSES, exógena, informes de gestión |

## Riesgos

- **La 013 cambia antes de empezar**: lo nombrado de la 013 (clave de la matriz, ciclo diario, enums) se
  verifica al empezar; si cambió, se enmienda `research.md` antes de las tareas.
- **K2 de la 013**: las pagadurías configurables (E-05) amplían el convenio de K2; si K2 no está terminado
  cuando empiece la 014, E-05 se construye dentro de K2.
- **Validaciones tributarias abiertas** (P2, P3, P26): no bloquean; los parámetros quedan «pendiente de
  validar».
- **Calendario de COOFLOPAL**: con Q1 y Q3, el 01/12/2026 depende de terminar la 013, esta feature y
  Recaudo y Tesorería, que aún no están especificadas.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 1 · Principio III: `IContratoDeCuentas` no pasa por `ISender`; ejecuta su validador y escribe su evento de auditoría dentro del contrato (como `IContratoDeObligaciones`, T101 de la 013) | El recaudador (Recaudo, Nómina, la carga de pagadurías) necesita que la consignación y su propia operación queden en **una sola transacción**; un `ISender.Send` abre su propio ámbito y rompería la atomicidad | Mensajería asíncrona: el cajero no sabría al instante si el retiro se aplicó, y un fallo dejaría dinero entregado sin registro |
| 2 · Principio X: la causación diaria de captaciones se audita con **un evento por corrida y cuenta afectada** (resumen con totales), no un diff por transacción | 100.000 cuentas × 365 días producirían decenas de millones de eventos sin valor de auditoría adicional; el libro inmutable ya es la traza (SC-006) | Diff por transacción: imposible de sostener en volumen y no agrega información sobre lo que ya guarda el libro |
| 3 · Principio X: las tablas de preparación de migración (`LND_Migration*Row`) llevan `[SinDiffDeAuditoria]` | Son datos de carga revisables y reemplazables; la carga, su revisión, su cuadre y su activación sí se auditan como operaciones | Diff por fila de carga: duplicaría el archivo cargado en la auditoría sin trazabilidad nueva |
