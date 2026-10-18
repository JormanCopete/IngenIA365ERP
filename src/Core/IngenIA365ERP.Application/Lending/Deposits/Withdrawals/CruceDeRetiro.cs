using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Withdrawals;

/// <summary>El débito de una cuenta que pagó un crédito en el cruce: la operación de cuenta, el pago del crédito y lo que salió por concepto. (nuevo)</summary>
public sealed record CruceDeCuentaAplicado(int CuentaId, int CreditoId, MemberAccountOperation Operacion, long OperacionDelCredito,
    IReadOnlyList<DebitoDeCuentaEnElRetiro> Debitos);

/// <summary>
/// El cruce interno al retiro del asociado (feature 014, US10, T100; data-model §6.1; FR-077, D14, R15): aplica los aportes y ahorros a los créditos vivos
/// de la persona <b>con la aplicación de pagos del crédito</b> —<see cref="IContratoDeObligaciones.AplicarAsync"/> con el recaudador <c>CAR</c> de clase
/// <c>OwnLoanPayoff</c>, medio <c>InternalOffset</c> y la prelación de los estatutos— y, por cada cuenta que paga cada crédito, una operación
/// <c>InternalOffset</c> que la debita, nombra el pago (<c>RelatedLoanOperationId</c>) y el retiro, con <c>CruceAlRetiroAplicado</c>. Todo en la
/// transacción y bajo el cerrojo de quien llama (crédito → cuenta): si un pago no se aplica, falla y nada queda. También el descuento por pérdidas
/// (<c>PerdidaAplicadaAlRetiro</c>, FR-078), en su propia operación (research E68). Llama al contrato directo, nunca por <c>ISender</c>. (nuevo)
/// </summary>
public sealed class CruceDeRetiro(
    IApplicationDbContext db,
    IContratoDeObligaciones contrato,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    /// <summary>El prefijo del comprobante del cruce de una cuenta.</summary>
    public const string PrefijoCruce = "CRA-";

    /// <summary>El prefijo del comprobante del descuento por pérdidas.</summary>
    public const string PrefijoPerdida = "PDA-";

    /// <summary>El recaudador del cruce: la Cartera misma (<c>OwnLoanPayoff</c>, exenta del registro de participantes, T91).</summary>
    public static Recaudador Recaudador => RecogidaEnElDesembolso.Recaudador;

    /// <summary>La clave del pago de <paramref name="credito"/> en la liquidación <paramref name="liquidacion"/> (<c>CAR:{liquidación:N}:{crédito:N}</c>).</summary>
    public static string Clave(Guid liquidacion, Guid credito) => RecogidaEnElDesembolso.Clave(liquidacion, credito);

    /// <summary>Aplica los cruces (ver la clase). Las cuentas y los créditos ya están bloqueados.</summary>
    public async Task<Result<IReadOnlyList<CruceDeCuentaAplicado>>> AplicarAsync(MembershipWithdrawal retiro, Guid liquidacion,
        IReadOnlyList<CruceDeRetiroCalculado> cruces, DateOnly fecha, Guid deudor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retiro);
        if (cruces.Count == 0) return Result.Success<IReadOnlyList<CruceDeCuentaAplicado>>([]);
        var creditos = await db.Loans.AsNoTracking().Where(l => cruces.Select(c => c.Credito.CreditoId).Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new { l.PublicId, l.Number }, ct);
        var pagos = cruces.Select(c => new PagoAAplicar(
            Clave(liquidacion, creditos[c.Credito.CreditoId].PublicId),
            creditos[c.Credito.CreditoId].PublicId,
            c.Total ? ModalidadDePago.FullPayoff : ModalidadDePago.FreeAmount,
            c.Monto,
            fecha,
            ClaseDeMedioDeRecaudo.InternalOffset,
            deudor,
            new ReferenciaDeRecibo(retiro.Number, fecha, null, null, nameof(MembershipWithdrawal)))).ToList();
        var respuesta = await contrato.AplicarAsync(new SolicitudDeAplicacion(Recaudador, FormaDeAplicacion.NonInteractive, pagos, []), ct);

        var actor = await actorActual.ObtenerAsync(ct);
        var aplicados = new List<CruceDeCuentaAplicado>();
        for (var i = 0; i < cruces.Count; i++)
        {
            var cruce = cruces[i];
            var credito = creditos[cruce.Credito.CreditoId];
            var item = i < respuesta.Payments.Count ? respuesta.Payments[i] : null;
            if (item is null || item.Result is not (ResultadoDelContrato.Applied or ResultadoDelContrato.AlreadyApplied) || item.Value is null)
                return Result.Failure<IReadOnlyList<CruceDeCuentaAplicado>>(ErroresDeRetiro.CruceRechazado(credito.Number,
                    item?.Code ?? "Lending.Contract.NoResponse", item?.Message ?? "Cartera no respondió por el crédito."));
            var pago = await db.LoanOperations.AsNoTracking().Where(o => o.PublicId == item.Value.OperationPublicId).Select(o => o.Id).FirstAsync(ct);

            foreach (var grupo in cruce.Debitos.GroupBy(d => d.CuentaId))
            {
                var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == grupo.Key, ct);
                var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
                if (hecho.IsFailure) return Result.Failure<IReadOnlyList<CruceDeCuentaAplicado>>(hecho.Error);
                var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
                var monto = grupo.Sum(d => d.Monto);
                var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.InternalOffset, PrefijoCruce, ct);
                var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
                {
                    MemberAccountId = cuenta.Id,
                    Kind = MemberAccountOperationKind.InternalOffset,
                    Number = numero,
                    Sequence = sello.Sequence,
                    ValueDate = fecha,
                    AccountingDate = hecho.Value.FechaContable,
                    RegisteredAt = sello.RegisteredAt,
                    Amount = monto,
                    ActorKind = actor.Kind,
                    ActorUserId = actor.UserId,
                    OriginUserCentralId = actor.CentralUserId,
                    PaymentMeansClass = nameof(ClaseDeMedioDeRecaudo.InternalOffset),
                    RelatedLoanOperationId = pago,
                    MembershipWithdrawalId = retiro.Id,
                    Reason = $"Cruce del retiro {retiro.Number} contra el crédito {credito.Number}",
                    Payload = string.Create(CultureInfo.InvariantCulture,
                        $"{{\"withdrawalPublicId\":\"{retiro.PublicId:N}\",\"loanPublicId\":\"{credito.PublicId:N}\",\"priority\":{cruce.Prelacion},\"payoff\":{cruce.Credito.ParaCancelar}}}"),
                    InputsSha256 = new HuellaDeCartera().Identificador("withdrawal", retiro.PublicId).Identificador("loan", credito.PublicId)
                        .Decimal("amount", monto, 2).Calcular(),
                    RulesVersion = VersionesDeReglas.Vigente,
                });
                await db.SaveChangesAsync(ct);
                var unidad = await emision.CruceAlRetiroAsync(new CruceAlRetiroAEmitir(hecho.Value, operacion, retiro, Valores(grupo, "MembershipWithdrawalOffset"),
                    [new WithdrawalOffsetV1
                    {
                        Loan = new LoanRefV1 { LoanPublicId = credito.PublicId, LoanNumber = credito.Number },
                        PaymentOperationPublicId = item.Value.OperationPublicId,
                        Amount = monto,
                    }]), ct);
                if (unidad.IsFailure) return Result.Failure<IReadOnlyList<CruceDeCuentaAplicado>>(unidad.Error);
                registro.RegistrarTransacciones(Filas(cuenta.Id, operacion, grupo, MemberAccountTransactionKind.InternalOffset, fecha, hecho.Value.FechaContable,
                    unidad.Value.De(CruceAlRetiroAplicadoV1.Type)));
                await db.SaveChangesAsync(ct);
                aplicados.Add(new CruceDeCuentaAplicado(cuenta.Id, cruce.Credito.CreditoId, operacion, pago, grupo.ToList()));
            }
        }
        return Result.Success<IReadOnlyList<CruceDeCuentaAplicado>>(aplicados);
    }

    /// <summary>
    /// El descuento por pérdidas (FR-078) en la cuenta de aportes: una operación <c>InternalOffset</c> sin crédito con <c>PerdidaAplicadaAlRetiro</c>. La
    /// cuenta ya está bloqueada.
    /// </summary>
    public async Task<Result<MemberAccountOperation?>> DescontarPerdidaAsync(MembershipWithdrawal retiro, IReadOnlyList<DebitoDeCuentaEnElRetiro> debitos,
        WithdrawalLossV1 perdida, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(retiro);
        if (debitos.Count == 0) return Result.Success<MemberAccountOperation?>(null);
        var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == debitos[0].CuentaId, ct);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<MemberAccountOperation?>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var monto = debitos.Sum(d => d.Monto);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.InternalOffset, PrefijoPerdida, ct);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.InternalOffset,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = monto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            PaymentMeansClass = nameof(ClaseDeMedioDeRecaudo.InternalOffset),
            MembershipWithdrawalId = retiro.Id,
            Reason = $"Pérdidas del ejercicio {perdida.Year.ToString(CultureInfo.InvariantCulture)} al retiro {retiro.Number}",
            Payload = string.Create(CultureInfo.InvariantCulture,
                $"{{\"withdrawalPublicId\":\"{retiro.PublicId:N}\",\"loss\":{{\"year\":{perdida.Year},\"proportion\":{perdida.Proportion},\"sessionItemPublicId\":\"{perdida.SessionItemPublicId:N}\"}}}}"),
            InputsSha256 = new HuellaDeCartera().Identificador("withdrawal", retiro.PublicId).Decimal("proportion", perdida.Proportion)
                .Decimal("amount", monto, 2).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);
        var unidad = await emision.PerdidaAlRetiroAsync(new PerdidaAlRetiroAEmitir(hecho.Value, operacion, Valores(debitos, "MembershipWithdrawalLoss"), perdida), ct);
        if (unidad.IsFailure) return Result.Failure<MemberAccountOperation?>(unidad.Error);
        registro.RegistrarTransacciones(Filas(cuenta.Id, operacion, debitos, MemberAccountTransactionKind.InternalOffset, fecha, hecho.Value.FechaContable,
            unidad.Value.De(PerdidaAplicadaAlRetiroV1.Type)));
        await db.SaveChangesAsync(ct);
        return Result.Success<MemberAccountOperation?>(operacion);
    }

    private static List<DepositConceptAmountV1> Valores(IEnumerable<DebitoDeCuentaEnElRetiro> debitos, string motivo) =>
        debitos.GroupBy(d => d.Concepto).Select(g => new DepositConceptAmountV1
        {
            Concept = g.Key.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(g.Key)?.ToString(), Amount = -g.Sum(d => d.Monto),
            ReasonCode = motivo,
        }).ToList();

    private static List<MemberAccountTransaction> Filas(int cuenta, MemberAccountOperation operacion, IEnumerable<DebitoDeCuentaEnElRetiro> debitos,
        MemberAccountTransactionKind clase, DateOnly fecha, DateOnly contable, Guid? mensaje) =>
        debitos.GroupBy(d => d.Concepto).Select(g => new MemberAccountTransaction
        {
            MemberAccountId = cuenta,
            Operation = operacion,
            OperationId = operacion.Id,
            Kind = clase,
            Concept = g.Key,
            Amount = -g.Sum(d => d.Monto),
            ValueDate = fecha,
            AccountingDate = contable,
            RegisteredAt = operacion.RegisteredAt,
            MessagePublicId = mensaje,
        }).ToList();
}

/// <summary>
/// El reverso de un pago cruzado al retiro arrastra el débito de la cuenta (feature 014, US10, T100; FR-077, US10-4, R15): <see cref="ReversionDePago"/> lo
/// llama dentro de su unidad de trabajo, con el crédito ya bloqueado. Si el pago reversado es de un cruce, reversa cada operación de cuenta que lo pagó
/// (las cuentas se bloquean después del crédito, el orden fijo) con <c>OperacionDeCuentaReversada</c> —anula la liquidación emitida— y deja el retiro
/// «por rehacer» (<c>ToRedo</c>); un retiro ya pagado con las cuentas canceladas no se arrastra
/// (<c>Lending.MembershipWithdrawal.AccountsClosed</c>, research E69). (nuevo)
/// </summary>
public sealed class ArrastreDelCruceAlRetiro(IApplicationDbContext db, ReversionDeCuentas reversion, ICerrojoDeCartera cerrojo) : IArrastreDeReversoDePago
{
    /// <summary>Quién inicia el reverso de la cuenta arrastrado por el del pago.</summary>
    public const string IniciadoPorElReversoDelPago = "LoanPaymentReversal";

    public async Task<Error?> ValidarAsync(LoanOperation pago, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pago);
        var cuentas = (await OperacionesAsync(pago, seguir: false, ct)).Select(o => o.MemberAccountId).Distinct().ToList();
        if (cuentas.Count == 0) return null;
        var cerradas = await db.MemberAccounts.AsNoTracking().Where(a => cuentas.Contains(a.Id) && a.Status == MemberAccountStatus.Closed)
            .OrderBy(a => a.Number).Select(a => a.Number).ToListAsync(ct);
        return cerradas.Count > 0 ? ErroresDeRetiro.CuentasCerradas(cerradas) : null;
    }

    public async Task<Result> AlReversarAsync(LoanOperation pago, LoanOperation reverso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pago);
        ArgumentNullException.ThrowIfNull(reverso);
        var operaciones = await OperacionesAsync(pago, seguir: true, ct);
        if (operaciones.Count == 0) return Result.Success();
        if (await ValidarAsync(pago, ct) is { } error) return Result.Failure(error);
        await cerrojo.BloquearCuentasAsync(operaciones.Select(o => o.MemberAccountId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

        var motivo = $"Se reversó el pago {pago.Number} del cruce al retiro: {reverso.Reason}";
        foreach (var operacion in operaciones)
        {
            var r = await reversion.ReversarAsync(operacion, motivo, IniciadoPorElReversoDelPago, ct);
            if (r.IsFailure) return Result.Failure(r.Error);
        }
        var retiro = await db.MembershipWithdrawals.FirstAsync(w => w.Id == operaciones[0].MembershipWithdrawalId, ct);
        if (retiro.Status is MembershipWithdrawalStatus.Settled or MembershipWithdrawalStatus.ToRedo)
            retiro.PorRehacer($"Se reversó el pago {pago.Number} del cruce ({reverso.Number}): vuelva a liquidar.");
        return Result.Success();
    }

    /// <summary>Las operaciones de cuenta vivas del cruce que pagaron <paramref name="pago"/>.</summary>
    private async Task<List<MemberAccountOperation>> OperacionesAsync(LoanOperation pago, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.MemberAccountOperations : db.MemberAccountOperations.AsNoTracking();
        return await consulta
            .Where(o => o.RelatedLoanOperationId == pago.Id && o.MembershipWithdrawalId != null && o.ReversesOperationId == null
                        && o.ReversedByOperationId == null && o.Kind == MemberAccountOperationKind.InternalOffset)
            .OrderBy(o => o.Id).ToListAsync(ct);
    }
}
