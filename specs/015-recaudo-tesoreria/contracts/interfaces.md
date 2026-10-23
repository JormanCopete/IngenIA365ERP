# Interfaces síncronas de Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | Molde: `013/contracts/interfaces.md`

Puertos que Recaudo y Tesorería **usan** de otros módulos, los que **se ofrecen entre ellos** y los que **ofrecen** a
Contabilidad. Todos son síncronos, corren en la unidad de trabajo de quien llama (misma base, misma transacción) y
validan y auditan sus escrituras (`LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`, molde de la desviación
T101 de la 013). Viven en `Application/Common/Integration/{Receipts,Treasury,Accounting}`.

## 1. Lo que Recaudo usa de Cartera y Captaciones

### 1.1 Uso del contrato de obligaciones (v1; v2 desde R1)

| Paso de Recaudo | Método | Forma | Notas |
|---|---|---|---|
| Buscar a la persona (FR-037) | `ListarAsync(Collector = REC/Receipts, persona, includeCoDebtor)` | — | lectura sensible: Recaudo audita `Receipts.Person.PayablesRead` |
| Elegir y simular (FR-038) | `SimularAsync` por obligación y modalidad; para el abono extraordinario, dos simulaciones (reducir plazo y cuota) | — | la simulación devuelve `SimulationId`, distribución, avisos, excedente, firmeza |
| Confirmar en caja (FR-042) | `AplicarAsync` con todas las porciones de créditos, `Interactive`, con su `SimulationId` | `Interactive` | `DistributionChanged` → se muestra la nueva y se reconfirma con la misma clave |
| Banco, pagaduría, contingencia | `AplicarAsync` `NonInteractive` con la modalidad del canal (FR-198); pagaduría con `ExternalPaymasterDeduction` y `DeductionFileLinePublicId` | `NonInteractive` | una línea por transacción (R-17) |
| Anular, cheque devuelto | `ReversarAsync(PaymentIdempotencyKey = la porción, Reason, Receipt = la anulación)` | — | obligaciones antes que cuentas (FR-170) |
| Devolver un saldo a favor | `ResolverSaldoAFavorAsync(Form = RefundThroughCollector, Receipt = la orden de pago o el egreso)` | — | R0 por orden de pago; R1 en efectivo |

`ReferenciaDeRecibo`: `Number` = número del recibo (o de la anulación), `Date`, `OfficePublicId` = oficina,
`MeansReference` = referencia del medio, `DocumentKind` = «Recibo», «Recibo de banco», «Recibo de pagaduría»,
«Anulación». `PayerPublicId` = el pagador (FR-040).

### 1.2 Uso del contrato de cuentas (v1; v2 desde R1)

| Paso | Método | Notas |
|---|---|---|
| Cuentas y exigibles | `ConsultarAsync`, `ListarExigiblesAsync` | |
| Simular | `SimularAsync` | devuelve si exige declaración y aprobador, el canje y el GMF |
| Confirmar | `ConsignarAsync` con todas las porciones de cuentas, **después** de las de créditos; `SourceOfFundsAttachmentPublicId` cuando la simulación la exige; `ApprovalPublicId` si hubo aprobador | modalidad `Free`, `Installments`, `SpecificInstallments` o `TermDepositFunding` por el valor exacto (US3-8) |
| Cheque devuelto | `ReversarAsync` aunque deje sobregiro técnico con bloqueo (FR-060) | |
| Retiro en efectivo (R1) | `SimularAsync` → `RetirarAsync(MeansClass = Cash, Signers, Receiver)` → entrega | sin efectivo suficiente se rechaza **antes** de pedir la autorización (US4-3) |

### 1.3 Lo que Recaudo no hace

No lee tablas `LND_` (`LaCarteraSoloSeTocaPorSusInterfaces`), no reversa pagos de Nómina (FR-062) ni de otros recaudadores,
no calcula intereses, firmeza ni GMF de captaciones.

## 2. Puertos entre Recaudo y Tesorería (R-05)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Treasury;

public interface ICuentasBancariasPropias                    // Tesorería → Recaudo (R0)
{
    Task<IReadOnlyList<CuentaBancariaPropiaDto>> DeRecaudoAsync(Guid? branchPublicId, CancellationToken ct); // con alcance
    Task<CuentaBancariaPropiaDto?> PorPublicIdAsync(Guid publicId, CancellationToken ct);
}

public interface IMovimientosBancariosParaRecaudo            // Tesorería → Recaudo (R0)
{
    Task<PaginaDto<LineaDeExtractoDto>> AbonosPorIdentificarAsync(FiltroDeAbonos filtro, CancellationToken ct);
    Task MarcarIdentificacionAsync(Guid statementLinePublicId, decimal identificado, CancellationToken ct); // escribe RecaudoStatus
}

public interface IRemesasDeTesoreria                         // Tesorería → Recaudo (T0) y Comercial (T1)
{
    Task<RemesaRegistradaDto> RegistrarAsync(SolicitudDeRemesa solicitud, CancellationToken ct);  // en la transacción del despacho
    Task<EstadoDeRemesaDto> EstadoAsync(Guid remittancePublicId, CancellationToken ct);
}

public interface IOrdenesDePagoDeTesoreria                   // Tesorería → Recaudo (T0)
{
    Task<OrdenDePagoEmitidaDto> EmitirAsync(SolicitudDeOrdenDePago solicitud, CancellationToken ct); // devolución de saldo a favor o partida
    Task<EstadoDeOrdenDto> EstadoAsync(Guid paymentOrderPublicId, CancellationToken ct);
}

namespace IngenIA365ERP.Application.Common.Integration.Receipts;

public interface IRecaudoParaConciliacion                    // Recaudo → Tesorería (T0)
{
    Task<IReadOnlyList<RegistroConciliableDto>> RegistrosAsync(Guid ownBankAccountPublicId, DateOnly desde, DateOnly hasta, CancellationToken ct);
}

public interface IPartidasDesdeConciliacion                  // Recaudo → Tesorería (T0)
{
    Task<PartidaCreadaDto> CrearAsync(Guid statementLinePublicId, string motivo, CancellationToken ct);  // US25-2
}

public interface IResultadoDeOrdenesDeRecaudo                // Recaudo → Tesorería (T0)
{
    Task InformarAsync(Guid paymentOrderPublicId, string outcome, DateOnly occurredOn, string? reason, CancellationToken ct);
}
```

`SolicitudDeOrdenDePago`: `{ IdempotencyKey, OrderKind (PayerCreditBalanceRefund | LendingCreditBalanceRefund |
UnidentifiedItemRefund), BeneficiaryPersonPublicId, PersonBankAccountPublicId?, Form, NetAmount, PayDate, Concept,
SourceDocumentPublicId }`. Cuando la orden de Recaudo queda «rechazada», Recaudo reabre el saldo a favor; «girada», lo
cierra; el hecho contable del giro lo emite Tesorería (`PagoDeOrdenGirado` contra `PuenteDeDevolucion`).

## 3. Contabilidad (E-04)

```csharp
namespace IngenIA365ERP.Application.Common.Integration.Accounting;

public interface IContabilidadParaRecaudo
{
    Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct); // validación previa
    Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct);                         // fecha contable propuesta
    Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct);   // caja por oficina, partidas, saldos a favor (cuadres)
    Task<IReadOnlyList<CuentaPorCobrarComercialDto>> CuentasPorCobrarComercialesAsync(Guid personPublicId, CancellationToken ct); // R1
}

public interface IContabilidadParaTesoreria
{
    Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct);
    Task<IReadOnlyList<CuentaBancariaDelPlanDto>> CuentasBancariasDelPlanAsync(Guid? bankPublicId, CancellationToken ct);
    Task VincularCuentaBancariaAsync(Guid ownBankAccountPublicId, Guid chartAccountPublicId, DateOnly desde, CancellationToken ct); // escribe ACC_OwnBankAccountLinks
    Task<VinculoDto?> VinculoAsync(Guid ownBankAccountPublicId, CancellationToken ct);
    Task<decimal> SaldoEnLibrosAsync(Guid ownBankAccountPublicId, DateOnly fecha, CancellationToken ct);
    Task<IReadOnlyList<MovimientoContableDeCuentaDto>> MovimientosDeCuentaBancariaAsync(Guid ownBankAccountPublicId, DateOnly desde, DateOnly hasta, CancellationToken ct); // conciliación mínima, FR-209
    Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct);
    Task<IReadOnlyList<CuentaPorPagarDto>> CuentasPorPagarAsync(FiltroDeCuentasPorPagar filtro, CancellationToken ct); // T2
}
```

`MovimientoContableDeCuentaDto`: `{ journalEntryPublicId, voucherNumber, date, debit, credit, description, sourceModule }`
(con `sourceModule` el conciliador distingue lo registrado en Contabilidad o en lo heredado de lo que ya es de Recaudo y
Tesorería).

Lo que **Recaudo y Tesorería ofrecen** a Contabilidad:

```csharp
public interface IDimensionesDeRecaudo       { /* códigos y nombres de puntos, cajas, medios y conceptos para la matriz */ }
public interface IDimensionesDeTesoreria     { /* cuentas propias, clases de orden */ }
public interface IEstadoOperativoParaElCierre
{
    Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct);
    // cuadre provisional pendiente del mes de la activación (bloquea, FR-228);
    // T1: conciliaciones abiertas y sesiones sin cerrar (aviso; bloquea si Contabilidad.CierreExigeConciliaciones, FR-132)
}
```

`PeriodCommands` (cierre de mes) consulta `IEstadoOperativoParaElCierre` igual que hoy consulta `CarteraEnElCierre`.

## 4. Comercial, Nómina y plataforma

| Puerto | Ofrece | Usa | Entrega |
|---|---|---|---|
| `IEfectivoDelPuntoDeVenta` | Comercial | Recaudo (registro único, FR-204) | R1 |
| `ILotesDeDatafonoDelPuntoDeVenta` | Comercial | Tesorería (adquirentes) | T2 |
| `IRecaudoDeNominaParaConsolidado` | Nómina | Recaudo (consolidado de sólo lectura) | R1 |
| `IListasRestrictivas` (contextos `Collection`, `Disbursement`) | plataforma | Recaudo, Tesorería | R0, T0 |
| `IAutorizacionDeDatos` | plataforma | Recaudo (avisos por correo, FR-020; pagador ocasional, FR-016) | R0 (alta), R1 (avisos) |
| `ICalendarioHabil` | plataforma | Recaudo (firmeza mostrada, plazos de cheques y libranza), Tesorería (días sin resultado) | R0, T0 |
| `IMotorDeAprobaciones`, `IDesafiosDePresencia` | plataforma | los dos | R0, T0 |
| `ICuentasBancariasDePersona` | plataforma (E-06) | Tesorería (validar destino), Recaudo (devoluciones) | T0 |
| `IVerificacionDeSalida` | cada módulo | la lista de go/no-go | R0, T0 |

## 5. `ICuentasBancariasDePersona` (E-06)

```csharp
public interface ICuentasBancariasDePersona
{
    Task<IReadOnlyList<CuentaBancariaDePersonaDto>> ActivasAsync(Guid personPublicId, CancellationToken ct);
    Task<CoincidenciaDeCuentaDto> VerificarAsync(Guid personPublicId, string bankCode, string accountNumber, CancellationToken ct);
    // devuelve: registrada y activa, PayableFrom, cambiada hace N días, compartida con otros beneficiarios, igual a la de un usuario interno, importada sin certificación
}
```

La escriben sólo los comandos de `/api/core/people/{id}/bank-accounts` con doble control; leer es lectura sensible
auditada (`Core.PersonBankAccount.Read`).

## 6. Pruebas que fijan estas interfaces

`RecaudoYTesoreriaSeHablanPorSusPuertos`, `RecaudoNoConoceContabilidad`, `TesoreriaNoConoceContabilidad`,
`LaCarteraSoloSeTocaPorSusInterfaces` (ampliada con `Receipts` y `Treasury` como llamadores),
`LasCuentasSeTocanSoloPorElContrato` (ya vigila `Receipts` y `Treasury`), `LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras`,
`LosCerrojosDeRecaudoYTesoreriaVanEnOrden`; Application: el recibo con tres aplicaciones y un rechazo en la tercera no
deja nada (SC-006); e2e de concurrencia con dos cajas sobre la misma persona (SC-032).
