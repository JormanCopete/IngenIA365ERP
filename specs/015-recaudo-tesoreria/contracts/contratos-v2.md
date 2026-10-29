# Contratos versionados y enmiendas E-01 a E-06

**Feature**: 015 | **Fecha**: 2026-10-22

Recaudo y Tesorería **adoptan la versión 1** de los contratos y mensajes vigentes sin cambiarlos. Lo que necesitan
distinto va como **enmienda** a la feature dueña y, cuando cambia lo que cruza un contrato, como **versión 2** sin
retirar la 1 (FR-164 a FR-169). Las migraciones de la 013 y la 014 están congeladas (T0802): cada cambio de esquema es
una migración nueva y aditiva.

| Enmienda | Feature | Qué | Entrega |
|---|---|---|---|
| E-01 | 013 Cartera | (a) `ProveedorDeObligaciones` v2 con la partida citada; (b) consulta de la carga aplicada sin activar; (c) mensaje del archivo de desembolsos; (d) recaudos esperados; (e) retiro de los dobles y de la regla que sólo los cuenta fuera de producción | (c), (e) T0/R0; (a), (b) R1; (d) T2 |
| E-02 | 014 Captaciones | (a) `ProveedorDeCuentas` v2 con la línea de pagaduría y la partida; (b) Tesorería no recaudadora; (c) umbral de declaración por módulo; (d) cancelación con entrega; (e) saldo del fondo de liquidez; (f) recaudos esperados; (g) retiro de los dobles | (b), (c), (g) R0; (a), (d) R1; (e), (f) T2 |
| E-03 | 012 Comercial | efectivo del punto de venta; ventas a crédito sin Cartera; facturas de proveedor; lotes de datáfono; remesa del punto de venta | R1, T1, T2 |
| E-04 | 009 Contabilidad | dos emisores y matrices, vínculo, consultas, guardas; la conciliación bancaria de Tesorería reemplaza a la de la 009 (FR-055 a FR-060), cuyas tablas `ACC_Bank*` quedan sin uso | R0, T0 ([contabilidad.md](contabilidad.md)) |
| E-05 | 010 Nómina | dispersión informada a Tesorería | T2 |
| E-06 | Plataforma | códigos de participante, ámbitos de formatos, plantillas genéricas, cuentas bancarias de la persona, alertas, contextos de listas, go/no-go | R0, T0 |

---

## 1. E-01 (a) · `ProveedorDeObligaciones` v2 (R1)

### 1.1 Qué cambia

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Lending;

public interface IContratoDeObligaciones
{
    const int VersionDelContrato = 2;    // el despliegue implementa 1 y 2: ContratosDeIntegracion.VersionesDelContratoDeObligaciones = [1, 2]
    // mismos cinco métodos
}

public sealed record PagoAAplicar(
    /* … los campos de la v1 sin cambio … */
    Guid? DeductionFileLinePublicId = null,
    PartidaBancariaCitada? BankItem = null);          // NUEVO en v2

public sealed record PartidaBancariaCitada(
    Guid BankItemPublicId,                            // la línea del extracto (TES_BankStatementLines.PublicId)
    DateOnly MovementDate,                            // su fecha de movimiento
    Guid OwnBankAccountPublicId);                     // la cuenta bancaria propia donde entró
```

### 1.2 Reglas

- Un recaudador que adoptó v2 puede mandar `BankItem`; uno con v1 que lo mande recibe `Rejected` con
  `Integration.Participant.VersionNotSupported`.
- Con `BankItem` y `ValueDate = MovementDate`, Cartera **exime la ventana** `Pagos.DiasMaximosFechaValor`; no exime el
  corte de la carga (`Lending.Contract.BeforeCutoff`) ni el permiso con monto de un mes cerrado
  (`Lending.Payment.ValueDateBeforeClosing`). `ValueDate ≠ MovementDate` con `BankItem`: `Rejected`
  `Lending.Payment.BankItemDateMismatch` **(nuevo)**.
- Cartera guarda `LND_LoanOperations.BankItemPublicId` (migración `CarteraYCaptacionesConPartidaCitada`) y lo incluye en
  la huella.
- La gramática de claves no cambia (`REC:{recibo:N}:{aplicación:N}:{clase}`).
- La activación de Cartera sigue aceptando un recaudador con sólo v1 (FR-164).

### 1.3 Mientras sólo rija v1 (R0)

La identificación tardía se aplica con la fecha más antigua que admita el contrato; el recibo lo dice
(«fecha valor ajustada a la ventana del contrato») y se alerta `Recaudo.FechaValorTardia` al módulo para que corrija la
mora indebida por su condonación con aprobación (FR-071).

## 2. E-01 (b), (c), (d), (e)

### 2.1 (b) Consulta de la carga aplicada sin activar (R1)

```csharp
public interface IConsultaDeCargaSinActivar            // Cartera la ofrece; Recaudo la usa (FR-046)
{
    Task<RespuestaDelContratoDto<CargaSinActivarDto>> PersonaAsync(Guid personPublicId, CancellationToken ct);
}
public sealed record CargaSinActivarDto(bool InAppliedLoad, IReadOnlyList<ObligacionCargadaDto> Obligations);
public sealed record ObligacionCargadaDto(Guid ObligationPublicId, string Number, string LineName, DateOnly CutoffDate);
```

Sin activar Cartera y con la persona en una carga aplicada, Recaudo deja el valor como recaudo pendiente de aplicar.

### 2.2 (e) Retiro de los dobles (R0/T0)

`RequisitosDeActivacion` deja de admitir `Class = Test` fuera de producción: el recaudador cuenta si es `Receipts` real con
`ProveedorDeObligaciones` adoptado; el destino `Treasury` cuenta si es real con `OrdenDeGiroEmitida` adoptada. El código de
los dobles se borra ([mensajes.md](mensajes.md) §8). `CarteraE2E.RequisitosDeActivacionAsync` pasa a registrar y activar
Recaudo y Tesorería reales.

### 2.3 (c) `ArchivoDeDesembolsosGenerado` v1 y `ArchivoDeDesembolsosAnulado` v1 (`CAR → Treasury`, T0)

Enmienda C10 de la 013 («el archivo no viaja en ningún mensaje»): ahora viaja para que Tesorería lo concilie (D16).
Origen: el archivo de desembolsos de Cartera (`LND_DisbursementFiles`); clave `Confirmation:{archivo:N}` (y
`Voiding:{archivo:N}` el anulado); `related` = las órdenes; tercero = la cooperativa; lo origina la persona que genera o
anula el archivo.

| Campo | Tipo | Oblig. | Qué es |
|---|---|---|---|
| `filePublicId` · `fileNumber` · `version` | Guid · string · int | sí | |
| `file` | objeto | sí | `{ attachmentPublicId, fileName, sha256, sizeBytes, formatCode }` (ámbito `LoanDisbursement` 4) |
| `sourceBankAccount` | objeto | no | `{ bankCode, bankAccountNumber }` de la cuenta de origen elegida al generar (Tesorería la casa con su cuenta propia) |
| `orders` | [objeto] | sí | `{ orderPublicId, orderNumber, netAmount, payeePersonPublicId }` |
| `total` · `lineCount` | decimal · int | sí | |
| `generatedBy` | `UserRefV1` | sí | |
| `reason` | string | sólo anulado | |

Efecto en Tesorería: §5.4 de [mensajes.md](mensajes.md). A futuro (fuera de esta feature), todo archivo de pagos sale de
Tesorería (D16).

### 2.4 (d) Recaudos esperados (T2)

`IRecaudosEsperadosDeCartera.PorFechaAsync(desde, hasta)` → cuotas por vencer por día (sin datos personales), para la
posición de liquidez. Sin respuesta, la posición se muestra sin ese componente y con el aviso (US20-2).

## 3. E-02 · Captaciones

### 3.1 (a) `ProveedorDeCuentas` v2 (R1)

`ConsignacionAAplicar` suma `PartidaBancariaCitada? BankItem` (mismo tipo de §1.1). Reglas: con `BankItem` y
`ValueDate = MovementDate` se exime la ventana; con `DeductionFileLinePublicId` (línea de pagaduría, que Captaciones verifica
por sí misma) también se exime la ventana, **como ya hace Cartera** (C8); `LND_MemberAccountOperations.BankItemPublicId`
(misma migración). `ContratosDeIntegracion.VersionesDelContratoDeCuentas = [1, 2]`. La activación de captaciones y aportes
sigue aceptando v1.

### 3.2 (b) Tesorería no se registra como recaudadora (R0)

Decidido por el dueño (D2): todo dinero de una persona entra por Recaudo. `ClaseDeRecaudador.Treasury = 6`, la gramática
`TES:{documento:N}:{cuenta:N}` y el destino `Treasury` de `ConsignacionReversadaPorCartera` se **conservan sin uso**;
ningún contrato cambia. `RequisitosDeActivacionDeCaptaciones` exige el recaudador `Receipts` real con `ProveedorDeCuentas`.

### 3.3 (c) Umbral de declaración por módulo (R0)

Cada módulo conserva su parámetro. Recaudo exige la declaración desde **su** umbral (`Sarlaft.UmbralDeclaracionOrigenFondos`)
o cuando la simulación de Captaciones la pida, la captura **una vez por recibo** y la manda en
`SourceOfFundsAttachmentPublicId` a cada consignación que la exija (sin cambio de contrato: el campo ya existe en v1).

### 3.4 (d) Cancelación con entrega (R1)

`RetirarAsync` con `CloseAccount = true` deja de responder `Lending.Savings.DeliveryNotAvailable` cuando el recaudador es
`Receipts` real: Captaciones liquida, cobra GMF y cargos, debita todo el disponible y devuelve la autorización; Recaudo
entrega con el mismo egreso (FR-052). Sin cambio de firma; cambia el comportamiento (versión de reglas de Captaciones).

### 3.5 (e) Saldo del fondo de liquidez (T2)

`SaldoDelFondoDeLiquidez` v1 (`TES → Lending`): `{ date, bankAccounts: [{ ownBankAccountPublicId, bankCode, bankAccountNumber,
balance, statementDate }], total }`. Captaciones lo usa para su cálculo del fondo (US20-1).

### 3.6 (f) Recaudos esperados (T2)

`IRecaudosEsperadosDeCaptaciones.PorFechaAsync`: cuotas de aportes y ahorros por vencer.

### 3.7 (g) Retiro de los dobles (R0)

Como §2.2, en `RequisitosDeActivacionDeCaptaciones` y `CaptacionesE2E.RequisitosDeCaptacionesAsync`.

## 4. E-03 · Comercial (012)

| Pieza | Forma | Entrega |
|---|---|---|
| Efectivo recibido y entregado en el punto de venta por persona y mes | puerto `IEfectivoDelPuntoDeVenta.PorPersonaAsync(personPublicId, mes)` y `PorPeriodoAsync` (para el reporte) | R1 |
| Ventas a crédito que no pasan a Cartera (cuentas por cobrar comerciales) | la consulta es de Contabilidad por tercero y factura (E-04); Comercial aporta el número de factura como documento cruce, sin cambio | R1 |
| Facturas de proveedor por pagar y su anulación; aviso del pago | mensajes `FacturaDeProveedorPorPagar` v1, `AnulacionDeFacturaDeProveedor` v1 (`INV → Treasury`), `PagoDeFacturaDeProveedor` v1 (`TES → Inventory`) | T2 |
| Lotes de datáfono y pagos con tarjeta del punto de venta | puerto `ILotesDeDatafonoDelPuntoDeVenta` | T2 |
| Remesa del efectivo del punto de venta | Comercial llama a `IRemesasDeTesoreria` desde su cierre del día | T1 |

## 5. E-05 · Nómina (010)

`DispersionDeNominaGenerada` v1 (`NOM → Treasury`, T2): `{ filePublicId, bankAccount: { bankCode, accountNumber }, total,
lineCount, payDate, runPublicId }`, para la posición y la conciliación; Nómina sigue pagando con su archivo (D27). Los
descuentos de nómina propia siguen en Nómina por los contratos; Recaudo los muestra en el consolidado (R1) por una consulta
de sólo lectura de Nómina (`IRecaudoDeNominaParaConsolidado`).

## 6. E-06 · Plataforma

| Pieza | Qué |
|---|---|
| Participantes | `REC` y `TES` reales (mensajes.md §7); `Test`/`TST` reservados sin uso, rechazados en todo ambiente |
| Formatos | ámbitos `BankStatement = 7`, `CollectionAgreementFile = 8` (R2), `TreasuryPaymentResponse = 9`, `PaymentGatewayFile = 10` (R2), `AcquirerSettlement = 11` (T2); `SupplierPayments = 3` renombrado `TreasuryPayments` (mismo número) |
| Plantillas genéricas | `EXTRACTO-GENERICO`, `PAGOS-GENERICO`, `RESPUESTA-PAGOS-GENERICO`, `RESPUESTA-PAGADURIA-GENERICA` ([archivos-y-plantillas.md](archivos-y-plantillas.md)) |
| Cuentas bancarias de la persona | `COR_PersonBankAccounts` con doble control, certificación y espera ([interfaces.md](interfaces.md) §5) |
| Listas | contextos `Collection` y `Disbursement` en `IListasRestrictivas` |
| Alertas | los tipos de `data-model.md` §22 |
| Go/no-go | `COR_GoLiveChecklists` con `IVerificacionDeSalida` por módulo |

## 7. Lo que NO cambia

`ResultadoDelGiro` v1, `OrdenDeGiroEmitida` v1 y v2, `AnulacionDeOrdenDeGiro` v1, `DescuentosPorPagaduria` v1 y v2,
`EnvioDeDescuentosConfirmado` v1, `ResultadoDeDescuentos` v1 y v2, `PagoReversadoPorCartera` v1 y
`ConsignacionReversadaPorCartera` v1 se adoptan tal como están. Una orden de giro en efectivo exigiría una versión nueva de
la orden y no se hace (D20): el efectivo sale por la caja con el contrato.
