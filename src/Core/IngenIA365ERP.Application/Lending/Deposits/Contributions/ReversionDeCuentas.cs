using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>Lo que dejó un reverso: el comprobante y si ya estaba reversada. (nuevo)</summary>
public sealed record ReversoDeCuentaAplicado(ComprobanteDeReversoDeCuentaDto Comprobante, bool YaReversada)
{
    /// <summary>US4 (T111): la operación de reverso (para el aviso al recaudador).</summary>
    public MemberAccountOperation? Reverso { get; init; }
}

/// <summary>
/// El reverso de una operación de cuenta (feature 014, US3, T083, T085, T089; US4, T111, T116; contrato-de-cuentas §3.11; FR-002, FR-056, FR-092): la
/// operación de reverso con la clave derivada <c>{clave}:reverso</c>, los espejos de todas las filas de la original —en todas sus cuentas— por
/// <see cref="RegistroDeCuentas"/>, con la fecha valor de la original y la fecha contable de hoy, la marca en la original y el hecho
/// <c>OperacionDeCuentaReversada</c> (que hereda el modo del original). Arrastra a sus dependientes (FR-002): el uso del tope del GMF del mes, los cargos que
/// disparó (cada uno con su propio reverso y, si estaba gravado, la nota crédito a Comercial) y los cargos pendientes que cobró (vuelven a deberse). Un reverso
/// no se reversa (<c>Lending.Savings.ReversalOfReversal</c>); una ya reversada responde el reverso existente. Si el reverso deja una cuenta en negativo (la
/// consignación ya se usó) la cuenta queda con sobregiro técnico y se alerta <c>Cartera.SobregiroTecnico</c> (FR-056). Guarda dentro de la transacción de
/// quien llama: la marca de la original necesita el Id del reverso. (nuevo)
/// </summary>
public sealed class ReversionDeCuentas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj,
    Documents.AnulacionDeDocumentosDeCuentas? documentos = null,
    IAlertas? alertas = null,
    Accrual.AjusteDeCausacionDeCuenta? ajusteDeCausacion = null)
{
    /// <summary>El prefijo del comprobante del reverso de una operación de cuenta.</summary>
    public const string PrefijoReverso = "RVC-";

    /// <summary>El cargo lo cobró una consignación posterior: reversarlo descuadraría lo por cobrar; se condona (con reintegro).</summary>
    public const string CodigoCargoCobradoDespues = "Lending.Savings.ChargeCollectedLater";

    /// <summary>La clave derivada del reverso (contrato-de-cuentas §3.12).</summary>
    public static string ClaveDeReverso(string clave) => $"{clave}:reverso";

    /// <summary>La operación de un recaudador por su clave, con seguimiento (para reversarla).</summary>
    public Task<MemberAccountOperation?> OperacionAsync(string recaudador, string clave, CancellationToken ct) =>
        db.MemberAccountOperations.FirstOrDefaultAsync(o => o.CollectorCode == recaudador && o.IdempotencyKey == clave, ct);

    public async Task<Result<ReversoDeCuentaAplicado>> ReversarAsync(MemberAccountOperation original, string motivo, string iniciadoPor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Kind == MemberAccountOperationKind.Reversal || original.ReversesOperationId is not null)
            return Result.Failure<ReversoDeCuentaAplicado>(ErroresDelLibroDeCuentas.ReversoDeReverso(original.Id));
        if (original.ReversedByOperationId is { } yaId)
        {
            var existente = await db.MemberAccountOperations.AsNoTracking().FirstAsync(o => o.Id == yaId, ct);
            return Result.Success(new ReversoDeCuentaAplicado(await ComprobanteAsync(existente, original, false, [], ct), true) { Reverso = existente });
        }

        // El cargo cuya deuda ya cobró una consignación posterior no se reversa: se condona (su reintegro) o se reversa esa consignación primero.
        var delCargo = original.Kind == MemberAccountOperationKind.Charge
            ? await db.AccountCharges.FirstOrDefaultAsync(c => c.AppliedOperationId == original.Id, ct)
            : null;
        if (delCargo is { CollectedOperationId: not null })
            return Result.Failure<ReversoDeCuentaAplicado>(new ErrorConDatos(CodigoCargoCobradoDespues,
                $"El cargo {delCargo.ChargeCode} ya lo cobró una consignación posterior: condónelo (con reintegro) en lugar de reversarlo.",
                new { chargePublicId = delCargo.PublicId }));

        var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == original.MemberAccountId, ct);
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == original.Id && !t.IsDeleted).OrderBy(t => t.Id)
            .ToListAsync(ct);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, reloj.HoyLocal, ct);
        if (hecho.IsFailure) return Result.Failure<ReversoDeCuentaAplicado>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Reversal, PrefijoReverso, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var texto = string.IsNullOrWhiteSpace(motivo) ? "Reverso por el recaudador" : motivo.Trim();
        var reverso = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            CounterpartMemberAccountId = original.CounterpartMemberAccountId,
            Kind = MemberAccountOperationKind.Reversal,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = original.ValueDate,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = original.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = original.CollectorClass,
            CollectorCode = original.CollectorCode,
            IdempotencyKey = original.IdempotencyKey is { } clave ? ClaveDeReverso(clave) : null,
            ContractVersion = original.ContractVersion,
            PaymentMeansClass = original.PaymentMeansClass,
            ReversesOperationId = original.Id,
            Reason = texto.Length <= 500 ? texto : texto[..500],
            Payload = original.Payload,
            InputsSha256 = original.InputsSha256,
            RulesVersion = original.RulesVersion,
            PrevalidationOutcome = original.PrevalidationOutcome,
        });

        var tipo = TipoDelHecho(original, cuenta.Nature);
        var valores = EmisionDeCartera.ValoresDeCuenta(filas.Where(f => f.MemberAccountId == cuenta.Id), f => -f.Amount);
        var usuario = actor.UserId is null ? null : new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
        var contrapartida = await ContrapartidaDelOriginalAsync(original, delCargo, ct);
        var otras = new List<(HechoDeCuenta, IReadOnlyList<IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.DepositConceptAmountV1>)>();
        foreach (var otraId in filas.Select(f => f.MemberAccountId).Where(id => id != cuenta.Id).Distinct())
        {
            var otra = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == otraId, ct);
            var hechoDeLaOtra = await emision.HechoDeCuentaAsync(otra, reloj.HoyLocal, ct);
            if (hechoDeLaOtra.IsFailure) return Result.Failure<ReversoDeCuentaAplicado>(hechoDeLaOtra.Error);
            otras.Add((hechoDeLaOtra.Value, EmisionDeCartera.ValoresDeCuenta(filas.Where(f => f.MemberAccountId == otraId), f => -f.Amount)));
        }
        var unidad = await emision.OperacionDeCuentaReversadaAsync(new ReversoDeCuentaAEmitir(hecho.Value, reverso, original, tipo,
            ClaseDelOrigen(original), valores, iniciadoPor, texto, usuario)
        {
            ClaseDeRecaudador = contrapartida.Clase, TipoDeOrden = contrapartida.Orden, MotivoDeLaContrapartida = contrapartida.Motivo, OtrasCuentas = otras,
        }, ct);
        var espejos = registro.Reversar(filas, reverso, hecho.Value.FechaContable, unidad.Mensajes.FirstOrDefault()?.PublicId);
        if (espejos.IsFailure) return Result.Failure<ReversoDeCuentaAplicado>(espejos.Error);
        await db.SaveChangesAsync(ct);
        // US5 (T133; FR-062): el reverso lleva la fecha valor de su original; si cae en días ya causados, ajusta la causación de cada cuenta tocada.
        if (ajusteDeCausacion is not null)
        {
            var idsTocadas = filas.Select(f => f.MemberAccountId).Distinct().ToList();
            foreach (var tocada in await db.MemberAccounts.Where(a => idsTocadas.Contains(a.Id)).ToListAsync(ct))
                if (await ajusteDeCausacion.AjustarAsync(tocada, reverso, EmisionDeCartera.ClaseReversoDeCuenta, ct) is { IsFailure: true } ajusteFallido)
                    return Result.Failure<ReversoDeCuentaAplicado>(ajusteFallido.Error);
        }
        RegistroDeCuentas.MarcarReversada(original, reverso);
        // US6 (T173): el pago de un CDAT reversado (su giro rechazado) deja de contar como pagado: el ciclo lo vuelve a pagar.
        foreach (var pagoReversado in await db.InterestSettlements.Where(s => s.OperationId == original.Id && s.ReversedByOperationId == null).ToListAsync(ct))
            pagoReversado.ReversedByOperationId = reverso.Id;

        // FR-002: los dependientes. El uso del tope exento del GMF del mes vuelve (fila espejo).
        var usos = await db.GmfMonthlyUsages.AsNoTracking().Where(u => u.OperationId == original.Id && u.ReversesUsageId == null && !u.IsDeleted).ToListAsync(ct);
        registro.ReversarUsosDeGmf(usos, reverso);
        // Los cargos pendientes que la consignación cobró vuelven a deberse.
        foreach (var cobrado in await db.AccountCharges.Where(c => c.CollectedOperationId == original.Id).ToListAsync(ct))
            cobrado.DevolverAPendiente(original.Id);
        // El cargo de esta operación (si es un cargo) queda reversado; si estaba gravado, Comercial emite la nota crédito.
        if (delCargo is not null)
        {
            delCargo.Reversar(reverso.Id);
            var def = await db.DepositLineCharges.AsNoTracking().FirstOrDefaultAsync(d => d.Id == delCargo.DepositLineChargeId, ct);
            delCargo.ReversalMessagePublicId = await emision.CargoReversadoAComercialAsync(hecho.Value, reverso, original, delCargo, def?.Name ?? delCargo.ChargeCode,
                def?.CommercialItemCode, texto, ct);
        }
        await db.SaveChangesAsync(ct);
        // Los cargos que disparó, cada uno en su propio reverso.
        foreach (var disparado in await db.AccountCharges.AsNoTracking()
                     .Where(c => c.TriggerOperationId == original.Id && (c.Status == AccountChargeStatus.Collected || c.Status == AccountChargeStatus.Pending)
                                 && c.CollectedOperationId == null)
                     .ToListAsync(ct))
        {
            var operacionDelCargo = await db.MemberAccountOperations.FirstAsync(o => o.Id == disparado.AppliedOperationId, ct);
            if (operacionDelCargo.ReversedByOperationId is not null) continue;
            var arrastrado = await ReversarAsync(operacionDelCargo, $"Se reversó la operación {original.Number} que lo disparó: {texto}", iniciadoPor, ct);
            if (arrastrado.IsFailure) return arrastrado;
        }

        // T087 (documentos.md §4): los documentos que vieron la operación y que el reverso deja sin validez, en la misma unidad de trabajo.
        var anulados = documentos is null ? [] : await documentos.AnularAsync(original, reverso, ct);

        // FR-056: si el dinero ya se usó, el reverso deja la cuenta en negativo técnico (bloquea retiros y avisa).
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var negativo = LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos) < 0m;
        var nuevoSobregiro = negativo && cuenta.TechnicalNegativeSince is null;
        if (nuevoSobregiro) cuenta.TechnicalNegativeSince = reloj.HoyLocal;
        await db.SaveChangesAsync(ct);
        if (nuevoSobregiro && alertas is not null)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.SobregiroTecnico, $"Sobregiro técnico en la cuenta {cuenta.Number}",
                $"El reverso {reverso.Number} de la operación {original.Number} ({texto}) dejó la cuenta {cuenta.Number} en negativo: el dinero ya se había usado. "
                + "La cuenta queda bloqueada para retiros hasta que se cubra el saldo.",
                nameof(MemberAccount), cuenta.PublicId, DedupKey: $"{TiposDeAlerta.SobregiroTecnico}:{cuenta.PublicId:N}",
                RecipientPermissions: ["Lending.Accounts.Reverse"], ModuloDeOrigen: "Lending"), ct);
        return Result.Success(new ReversoDeCuentaAplicado(await ComprobanteAsync(reverso, original, negativo, anulados, ct), false) { Reverso = reverso });
    }

    /// <summary>
    /// US8 (T143; research E106): a dónde fue la contrapartida del original, para que Contabilidad arme el espejo sin leer la Cartera: la clase del recaudador
    /// (<c>OwnLoanPayoff</c> en el débito automático y el cruce), el tipo de la orden de giro y la clase del cargo.
    /// </summary>
    private async Task<(string? Clase, string? Orden, string? Motivo)> ContrapartidaDelOriginalAsync(MemberAccountOperation original, AccountCharge? delCargo,
        CancellationToken ct)
    {
        var clase = original.CollectorClass
                    ?? (original.Kind is MemberAccountOperationKind.DirectDebit or MemberAccountOperationKind.InternalOffset
                        ? nameof(ClaseDeRecaudador.OwnLoanPayoff)
                        : null);
        var orden = await db.AccountPayoutOrders.AsNoTracking().FirstOrDefaultAsync(o => o.OperationId == original.Id, ct);
        string? motivo = delCargo?.Kind.ToString();
        // La condonación nombra su cargo en la carga útil ({"chargePublicId":"…N…"}, CargosDeCuentas).
        if (motivo is null && original.Kind == MemberAccountOperationKind.ChargeWaiver && original.Payload is { } carga
            && System.Text.RegularExpressions.Regex.Match(carga, "\"chargePublicId\":\"([0-9a-fA-F-]{32,36})\"") is { Success: true } m
            && Guid.TryParse(m.Groups[1].Value, out var cargoId))
            motivo = (await db.AccountCharges.AsNoTracking().Where(c => c.PublicId == cargoId).Select(c => (ChargeKind?)c.Kind).FirstOrDefaultAsync(ct))?.ToString();
        return (clase, orden is null ? null : EmisionDeCartera.TipoDeOrden(orden), motivo);
    }

    /// <summary>El tipo del hecho principal de la operación original (el que el reverso espeja).</summary>
    public static string TipoDelHecho(MemberAccountOperation original, LineNature naturaleza) => original.Kind switch
    {
        // US10 (T100, T101): las operaciones del retiro del asociado —el cruce (con su pago), la pérdida y la devolución—.
        MemberAccountOperationKind.InternalOffset when original.MembershipWithdrawalId is not null => original.RelatedLoanOperationId is not null
            ? CruceAlRetiroAplicadoV1.Type
            : PerdidaAplicadaAlRetiroV1.Type,
        MemberAccountOperationKind.ContributionRefund or MemberAccountOperationKind.Withdrawal when original.MembershipWithdrawalId is not null =>
            DevolucionOrdenadaV1.Type,
        MemberAccountOperationKind.Deposit => naturaleza == LineNature.Contribution ? AporteRecibidoV1.Type : ConsignacionEnCuentaAplicadaV1.Type,
        MemberAccountOperationKind.Transfer => TrasladoEntreCuentasAplicadoV1.Type,
        MemberAccountOperationKind.ContributionRefund => DevolucionOrdenadaV1.Type,
        MemberAccountOperationKind.Withdrawal or MemberAccountOperationKind.DirectDebit => RetiroDeCuentaAplicadoV1.Type,
        // US4 (T116): el cargo y su condonación.
        MemberAccountOperationKind.Charge => CargoDeCuentaCausadoV1.Type,
        MemberAccountOperationKind.ChargeWaiver => CargoDeCuentaCondonadoV1.Type,
        // US6 (T173): el pago de intereses y la salida de un CDAT (los reversa Tesorería cuando rechaza su giro).
        MemberAccountOperationKind.TermDepositInterestPayment => InteresesDeCdatPagadosV1.Type,
        MemberAccountOperationKind.TermDepositRedemption => original.Payload.Contains("\"cause\":null", StringComparison.Ordinal)
            ? CdatRedimidoV1.Type
            : CdatCanceladoV1.Type,
        _ => original.Kind.ToString(),
    };

    /// <summary>La clase del documento de origen de la operación original (el <c>related</c> del reverso).</summary>
    public static string ClaseDelOrigen(MemberAccountOperation original) => original.MembershipWithdrawalId is not null
        ? EmisionDeCartera.ClaseLiquidacionDeRetiro
        : original.Kind switch
    {
        MemberAccountOperationKind.Deposit => EmisionDeCartera.ClaseConsignacionDeCuenta,
        MemberAccountOperationKind.Transfer => EmisionDeCartera.ClaseTrasladoDeCuenta,
        MemberAccountOperationKind.ContributionRefund => EmisionDeCartera.ClaseDevolucionDeAportes,
        MemberAccountOperationKind.Withdrawal or MemberAccountOperationKind.DirectDebit => EmisionDeCartera.ClaseRetiroDeCuenta,
        MemberAccountOperationKind.Charge or MemberAccountOperationKind.ChargeWaiver => EmisionDeCartera.ClaseCargoDeCuenta,
        MemberAccountOperationKind.TermDepositInterestPayment => EmisionDeCartera.ClasePagoDeInteresesDeCdat,
        MemberAccountOperationKind.TermDepositRedemption => EmisionDeCartera.ClaseRedencionDeCdat,
        _ => original.Kind.ToString(),
    };

    private async Task<ComprobanteDeReversoDeCuentaDto> ComprobanteAsync(MemberAccountOperation reverso, MemberAccountOperation original, bool negativo,
        IReadOnlyList<Guid> anulados, CancellationToken ct)
    {
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == reverso.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var cuentas = filas.Select(f => f.MemberAccountId).Append(reverso.MemberAccountId).Distinct().ToList();
        var publicos = await db.MemberAccounts.AsNoTracking().Where(a => cuentas.Contains(a.Id)).Select(a => a.PublicId).ToListAsync(ct);
        return new ComprobanteDeReversoDeCuentaDto(reverso.PublicId, reverso.Number, original.PublicId, original.Number, publicos, reverso.AccountingDate,
            reverso.RegisteredAt, original.Amount, filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(),
            negativo, anulados);
    }
}
