# Plan de implementación: 026 — Cargos masivos de Cartera

**Rama**: `026-cargos-masivos` · **Fecha**: 2026-11-25 · **Spec**: [spec.md](spec.md)
**Base de código**: `origin/develop` @ `dc7ee87c`. Investigación: [research.md](research.md) · Datos: [data-model.md](data-model.md) ·
API: [contracts/api.md](contracts/api.md) · Guía: [quickstart.md](quickstart.md).

## Resumen

Una corrida de Cartera crea, por asociado, una obligación de una línea marcada, sin solicitud ni giro, y la contabiliza en un solo comprobante.
Reusa el nacimiento sin solicitud de la venta a crédito, el registro único del libro, la corrida de Cartera en segundo plano y el lote resumido
de Contabilidad. Agrega dos hechos contables, un rol de la matriz, un tipo de comprobante (`CO`), dos tablas y cinco columnas en la línea.

**Ajuste a D2 (sólo el código):** `CM` ya es «Causación de interés de mora». El tipo nuevo se llama «Cargos masivos» con el código **`CO`**.

| Orden | Fase | Historias | Tamaño |
|---|---|---|---|
| 1 | Fundamentos: enums, entidades, migración, línea marcada | HU1 | mediano |
| 2 | Contabilidad: hechos, rol, constructor, `CO`, plantilla y completitud | HU1, HU3 | mediano |
| 3 | Corrida: borrador, población, vista previa, confirmación | HU2 | grande |
| 4 | Ejecución en segundo plano y comprobante único | HU2, HU3 | grande |
| 5 | Plano | HU4 | pequeño |
| 6 | Anulación | HU5 | mediano |
| 7 | Centrales, cobranza y textos de origen | HU1 (D4) | pequeño |
| 8 | Pantallas, menú, exportación y manual | HU2–HU6 | grande |
| 9 | Pruebas de punta a punta (una sola vez, al final) | todas | mediano |

## Contexto técnico

- **Lenguaje**: C# / .NET 10.0.5, Minimal APIs con Carter, CQRS con MediatR, FluentValidation; Blazor (Shared) con Syncfusion 33.2.8.
- **Almacenamiento**: base por cooperativa (PostgreSQL y SQL Server), migraciones EF por proveedor posteriores a `EsquemaBase`.
- **Pruebas**: xUnit en Domain, Application, Architecture y Shared; e2e con Testcontainers en los dos motores, al final.
- **Volumen**: 459 asociados activos hoy en COOFLOPAL; diseño para 2.000 por corrida en menos de 15 minutos (SC-002).
- **Restricciones**: un solo escritor del libro (`RegistroDeCartera`); Cartera no conoce cuentas; lo contabilizado no se edita; claves de operación;
  todo trabajo de fondo por `IEjecutorEnCooperativa`; de fábrica nada cambia.

## Constitution Check

| # | Principio | Estado | Nota |
|---|---|---|---|
| I | Spec-First | PASS | spec 026 aprobada (D1–D5), este plan, luego tareas |
| II | Clean Architecture | PASS | reglas puras en Domain (`ReglasDeCargoMasivo`); orquestación en Application; EF en Persistence |
| III | CQRS + MediatR | PASS | comandos con validadores; los endpoints sólo reenvían |
| IV | Multi-tenancy | PASS | todo en la base de la cooperativa; la corrida de fondo por `IEjecutorEnCooperativa` y el conductor de Cartera |
| V | Person centralizada | PASS | las líneas guardan `PersonId`, ningún dato personal |
| VI | PublicId | PASS | rutas y DTOs con `PublicId`; `Id` interno sólo en Application |
| VII | Soft-delete + auditoría | PASS | entidades `AuditableEntity` con filtro; eventos `Lending.BulkCharge.*` |
| VIII | Validación dual | PASS | validadores de comando y la misma validación en la pantalla (rangos de la línea, ciclo y primer pago) |
| IX | Errores visibles | PASS | fallas por asociado con motivo en la línea y `LendingProcessRunErrors`; sin `catch` silencioso |
| X | Trazabilidad | PASS | auditoría de corrida, confirmación y anulación con motivo |
| XI | Inmutabilidad contable | PASS | anular emite un hecho espejo y un comprobante nuevo; nada se edita |
| XII | Migraciones | PASS | un par aditivo `CargosMasivos`; columnas nulas o con valor por defecto; semillas aparte |
| UI | Indicador de carga | PASS | cada zona con `IndicadorDeCarga`; la página entra a `LasPantallasDeCarteraEstanEnElMenu` |
| Satélite | — | N/A | no toca `canal/` |

## Fase 1 — Fundamentos (HU1)

**Domain**
- Enums: `LoanOrigin.BulkCharge = 4`, `DisbursementForm.Charge = 5`, `TermsVersionCause.BulkCharge = 13`, `LendingProcessKind.BulkCharge = 9` y
  `BulkChargeVoid = 10`, y los nuevos `BulkChargeRunStatus`, `BulkChargeLineStatus`, `BulkChargeSource`, `BulkChargeChannelMode`,
  `BulkChargeCounterpart`.
- Entidades `BulkChargeRun` y `BulkChargeLine` (`Domain/Entities/Lending/BulkCharges/`) con sus transiciones (`Confirmar`, `Encolar`,
  `Terminar`, `Descartar`, `IniciarAnulacion`) y `Loan.BulkChargeLineId`.
- `CreditLineVersion`: las cinco columnas, la huella con `.Si(AllowsBulkCharge, …)` y `Validar` (`Lending.CreditLine.BulkChargeIncompatible`):
  sin codeudores ni garantías, sin documentos, sin `RequiresEmployment` ni `RequiresBureauQuery`, valor por defecto dentro del rango, cuotas por
  defecto ≥ 1 y ≤ `MaxInstallments`. Borrador (`BorradoresDeLinea`) y su copia desde la vigente.
- `Domain/Lending/BulkCharges/ReglasDeCargoMasivo` (puro): exclusión de un asociado (activo, fallecido, retiro vivo), forma de pago resultante,
  primer pago dentro del ciclo, `HoldsPeriod`. Casos en `Domain.Tests/Lending/BulkCharges`.

**Persistence**
- Configuraciones `BulkChargeRunConfiguration`, `BulkChargeLineConfiguration` (`ComoEntidadDeCartera`), índice único filtrado de R6, las columnas
  de la línea con `HasDefaultValue(false)`, `DbSet` en `IApplicationDbContext`.
- Migración `CargosMasivos` con `tools/scripts/add-migration.ps1 -Name CargosMasivos -Context Application` y `check-migration-parity.ps1`.

**Application y API de la línea**
- `CreditLineVersionInput` / DTO, comandos de crear y agregar versión, `CreditLineQueries`; plantilla `lending.credit-lines` con las columnas
  `admiteCargoMasivo`, `valorCargoMasivo`, `cuotasCargoMasivo`, `cargoReportaACentrales`, `cargoEntraACobranza`.

**Pruebas**: validación de la versión, huella estable de versiones viejas, reglas puras.

## Fase 2 — Contabilidad (HU1, HU3)

- Contratos `CargoMasivoRegistradoV1` y `CargoMasivoAnuladoV1` (`Common/Integration/Contracts/Lending`) y su entrada en
  `CatalogoDeMensajesDeCarteraV1` (`CMS`, `CMA`).
- `RolesDeCuentaDeCartera`: `ContrapartidaDelCargo` (crédito, puente, opcionales `[Lin, Suc]`, tercero `Beneficiario`); `RolesDeCredito = 29`.
- `OperacionesDeCartera`: `new(CargoMasivoRegistradoV1.Type, "CMS", false, [R.Capital], [R.ContrapartidaDelCargo], PorLotes, null, "CO", K1, Ninguno)` y
  el anulado con los lados invertidos y `HeredaElModo` falso (la anulación sella el suyo). `LendingFactTypesSeeder.Datos` igual.
- `ConstructorDeLineasDeCartera`: casos de los dos hechos (R2).
- Semillas: `CO` en `voucher-types.json`; dos mapeos en `lending-voucher-mappings.json`.
- `GetLendingRulesTemplateQuery.Sugeridas`: una fila `ContrapartidaDelCargo` por línea con `AllowsBulkCharge` vigente. Completitud: la
  combinación en uso de `IDimensionesDeCartera` incluye las líneas marcadas aunque todavía no tengan créditos.
- `ValidacionPreviaContableDeCartera`: hecho de muestra para la corrida (`HechoDeCargoParaValidar`) y origen `OrigenDeValidacionPrevia.CargoMasivo`
  (bloquea en pantalla como `Pantalla`).
- **Pruebas**: la prueba cruzada de hechos (tres declaraciones), casos contables del constructor (débito con deudor; crédito con deudor, con
  proveedor y sin tercero) y del resumen de un grupo de tres créditos en un comprobante `CO`.

## Fase 3 — La corrida: borrador y vista previa (HU2)

- `Application/Lending/BulkCharges/`:
  - `PoblacionDeCargoMasivo`: asociados activos con los filtros (R7), exclusiones (D3) y unicidad (R6); resuelve forma de pago y convenio (R8).
  - `VistaPreviaDeCargoMasivo`: escribe las líneas del borrador (`Pending`/`Skipped`), totales, la huella y la evaluación contable.
  - Comandos `CreateBulkChargeRunCommand`, `UpdateBulkChargeRunCommand`, `RecalculateBulkChargeRunCommand`, `DiscardBulkChargeRunCommand` y
    `ConfirmBulkChargeRunCommand` (vuelve a calcular; si la huella cambió, `PreviewChanged`; segregación `Segregacion.CargoMasivoPorOtraPersona`;
    mes cerrado de Cartera o Contabilidad → `PeriodClosed`; encola `LendingProcessRun` con `RequestLendingProcessRunCommand`).
  - Consultas: opciones, lista, detalle, líneas.
- Parámetro `Segregacion.CargoMasivoPorOtraPersona` en `ParametrosDeCartera.Definiciones`.
- Permisos `Lending.BulkCharges.Read/Run/Void` en `LendingPermissionCatalogSeeder` y `PerfilesSugeridos`; contadores de permisos en las pruebas.
- Endpoints `API/Endpoints/Lending/BulkChargesEndpoints.cs` según [contracts/api.md](contracts/api.md); entra a `LosComandosDeCarteraLlevanClave`.
- **Pruebas** (Application): población y exclusiones, forma de pago «de cada asociado», unicidad, huella, segregación, mes cerrado.

## Fase 4 — Ejecución y comprobante único (HU2, HU3)

- `NacimientoDeCargoMasivo` (R1): crea la obligación de una línea, con el vínculo de nómina o pagaduría, y emite `CargoMasivoRegistrado` con
  `ModoDeEntrega.Sellado(Batch, "CAR.CMS|CargoMasivo||PorDia", "CargoMasivo:{run}")` (o `NoPasa`, R3) y la decisión de validación previa sellada.
- `EjecutorDeCargoMasivo : IEjecutorDeCorridaDeCartera` (clase `BulkCharge`): toma 50 líneas `Pending` por `Id` > cursor; por cada una,
  `TransaccionExplicita` → cerrojo de setup compartido y del deudor → vuelve a evaluar exclusiones y unicidad → nace o queda `Skipped`/`Failed`
  con motivo y `LendingProcessRunErrors`. Al no quedar pendientes: totales, `Completed`, lote contable con las entregas de su alcance
  (`LotesDeIntegracion.Nuevo` + `Asignar`, como `CierreMensualDeCartera.LoteAsync`) y auditoría `Lending.BulkCharge.Completed`.
- `ConductorDeCorridasDeCartera.Clases` incluye `BulkCharge` y `BulkChargeVoid`; registro en `DependencyInjection`.
- **Pruebas**: una corrida de tres asociados deja tres créditos con plan, posición y vínculo; una línea que falla no detiene el resto; reanudar
  tras interrumpir en la mitad da el mismo resultado; las entregas quedan en un solo lote.

## Fase 5 — Plano (HU4)

- Plantilla `lending.bulk-charges` en `CatalogoDePlantillasDeCartera` (tipo y número de documento; valor, cuotas, primer pago y forma de pago
  opcionales) y `ImportBulkChargeLinesCommand` sobre `EjecutorDeImportacion`: revisa con `ResolutorDePersonaPorDocumento`, marca errores de fila
  (inexistente, ambigua, no asociada, repetida, fuera de rango) y, al aplicar, reemplaza las líneas del borrador en modo `File`. Rutas con
  `MapPlantilla` bajo `/{id}`.
- **Pruebas**: errores por fila, omitidos que no bloquean, reemplazo de las líneas.

## Fase 6 — Anulación (HU5)

- `AnulacionDeCargoMasivo` (R9): comprueba que el libro sólo tenga el desembolso, registra `DisbursementReversal` con los espejos, pasa a
  `Voided`, marca la línea `Voided` con `HoldsPeriod = false` y emite `CargoMasivoAnulado`.
- `VoidBulkChargeLineCommand` (síncrono, sello `Batch` con alcance propio y lote al guardar) y `VoidBulkChargeRunCommand` (encola `BulkChargeVoid`;
  el ejecutor anula por tandas y deja `HasMovements` a los que no se pueden; al final, un lote → un comprobante de reverso).
- **Pruebas**: con pagos se rechaza; anular libera el período; la anulación entera deja un solo lote.

## Fase 7 — Centrales, cobranza y origen (D4)

- `MotivosDeExclusionDeCentral.BulkChargeNotReported` y la regla en `ProyeccionCentralDeRiesgo` (R10), con su texto en la pantalla de excluidos.
- `CobranzaDelCicloDiario.ExcluidosAsync`, reparto manual y agenda: fuera los de origen `BulkCharge` cuya versión no entra a cobranza.
- `TextosDeCreditos` («Cargo masivo», causa «Cargo masivo»), filtro de origen en Créditos, `CreditoDetalle.razor` (enlace a la corrida).
- **Pruebas**: exclusión de centrales con su motivo; fuera de cobranza; con las opciones en Sí, entran.

## Fase 8 — Pantallas, menú, exportación y manual (HU2–HU6)

- `Shared/Services/Cartera/CargosMasivosClient.cs` y DTOs (registrado en Web, Web.Client y MAUI).
- Páginas `Pages/Cartera/CargosMasivos.razor` (`/cartera/cargos-masivos`: lista con filtros y «Nueva») y `CargoMasivoDetalle.razor`
  (`/cartera/cargos-masivos/{Id:guid}`: pasos datos → a quién (filtros o plano con `ImportarPlantilla`) → vista previa → confirmar; en curso, el
  avance; terminada, líneas con filtro por estado, comprobante, exportar, anular corrida o línea). `IndicadorDeCarga` por zona.
- Editor de la versión de línea: casilla «Admite cargo masivo» y sus campos; resumen de la versión.
- Menú: «Cargos masivos» en Cartera → Procesos e informes, con `PermissionGate`; `generar-mapa-del-menu.py`; `LasPantallasDeCarteraEstanEnElMenu`.
- Exportación con `TablaExportable` (Excel y PDF).
- Manual `docs/manual/cartera-cargos-masivos.md`, entrada en `ManualCatalogo` y en `docs/manual/cartera.md`; CLAUDE.md con la regla nueva.

## Fase 9 — Pruebas de punta a punta (al final, una sola vez)

- `tests/IngenIA365ERP.API.IntegrationTests/Lending/CargosMasivosTests.cs` en PostgreSQL y SQL Server: línea marcada + regla, corrida de tres
  asociados (uno retirado), comprobante `CO` cuadrado, repetir sin duplicar, pago en caja, anular uno y volver a cargar, plano.
- Suites sin contenedores completas (Domain, Application, Architecture, Shared).

## Riesgos

- **Fecha contable única**: si la fecha del cargo cae en un día que `FechaContable` mueve (festivo), todas se mueven igual; un solo comprobante.
- **Lote resumido y fallas**: si Contabilidad rechaza una unidad, el comprobante sale sin ella y queda en reproceso con alerta (comportamiento de
  siempre).
- **Actor de proceso**: los mensajes los emite el conductor; las operaciones llevan a la persona que confirmó.
- **Huella de la línea**: las cinco columnas sólo entran a la huella si la marca está encendida, para no cambiar la de versiones guardadas.

## Estructura

```text
src/Core/IngenIA365ERP.Domain/
  Entities/Lending/BulkCharges/{BulkChargeRun,BulkChargeLine}.cs
  Lending/BulkCharges/ReglasDeCargoMasivo.cs
src/Core/IngenIA365ERP.Application/
  Lending/BulkCharges/  (población, vista previa, comandos, consultas, nacimiento, ejecutor, anulación, plano)
  Common/Integration/Contracts/Lending/{CargoMasivoRegistradoV1,CargoMasivoAnuladoV1}.cs
  Accounting/Lending/Reglas/ (rol, operaciones), Contabilizacion/ConstructorDeLineasDeCartera.cs
src/Infrastructure/IngenIA365ERP.Persistence/
  Configurations/Lending/BulkCharges/*.cs, Seeding/Parametric/Data/*.json
  …Migrations.PostgreSql|SqlServer/Application/*_CargosMasivos.cs
src/Presentation/IngenIA365ERP.API/Endpoints/Lending/BulkChargesEndpoints.cs
src/Presentation/IngenIA365ERP.Shared/Pages/Cartera/{CargosMasivos,CargoMasivoDetalle}.razor
docs/manual/cartera-cargos-masivos.md
```

## Complexity Tracking

Sin violaciones.
