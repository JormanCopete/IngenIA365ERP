using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

// Feature 014, US5 (T135; api.md §4, «close-preview» y «close»; FR-039): la cancelación de una cuenta de ahorro a la vista —liquida a la fecha, retiene,
// cobra el GMF de la entrega y entrega o traslada el saldo; no con bloqueos, embargos, canje, cargos por cobrar ni sobregiro—. (nuevo)

/// <summary>Lo que impide cancelar (api.md §4, <c>blockers</c>). (nuevo)</summary>
public sealed record CloseBlockerDto(string Kind, string Detail);

/// <summary>La vista previa de la cancelación (api.md §4, <c>GET /{id}/close-preview</c>). (nuevo)</summary>
public sealed record MemberAccountClosingPreviewDto(
    DateOnly Date,
    InterestSimulationDto? Settlement,
    decimal BalanceAfterSettlement,
    decimal Gmf,
    decimal PendingCharges,
    decimal ToDeliver,
    IReadOnlyList<CloseBlockerDto> Blockers)
{
    public bool CanClose => Blockers.Count == 0;
}

/// <summary>Cómo se entrega el saldo de una cancelación (api.md §4). (nuevo)</summary>
public enum CloseDeliveryKind
{
    TransferToAccount = 1,
    DisbursementOrder = 2,
}

public sealed record CloseDeliveryInput(CloseDeliveryKind Kind, Guid? TargetAccountPublicId, WithdrawalPayeeInput? Payee);

/// <summary>Lo que dejó la cancelación: la operación de cancelación, la liquidación, la entrega y el estado de la cuenta. (nuevo)</summary>
public sealed record MemberAccountClosingResultDto(
    Guid OperationPublicId,
    string OperationNumber,
    InterestSettlementDto? Settlement,
    MemberAccountOperationResultDto? Delivery,
    decimal Delivered,
    MemberAccountStatus Status);

/// <summary>
/// La cancelación de una cuenta (feature 014, US5, T135; FR-039): lo que la impide, lo que se liquidaría a la fecha y lo que se entregaría. Compartida por la
/// vista previa y el comando. Sólo el ahorro a la vista se cancela por aquí: los aportes y el ahorro permanente se cancelan con el retiro del asociado
/// (<c>Lending.Contributions.ClosureOnlyByWithdrawal</c>) y el CDAT y el contractual llegan con K9. (nuevo)
/// </summary>
public sealed class CancelacionDeCuenta(IApplicationDbContext db, ReglasDeMovimientos movimientos, LiquidacionDeIntereses liquidacion, IDateTimeService reloj)
{
    public const string CodigoSoloPorRetiro = "Lending.Contributions.ClosureOnlyByWithdrawal";
    public const string CodigoFechaDeCancelacion = "Lending.Savings.CloseDateInvalid";

    /// <summary>Nulo si la cuenta se cancela por aquí; si no, el error.</summary>
    public static Error? Admite(MemberAccount cuenta)
    {
        if (cuenta.Nature == LineNature.Contribution || cuenta.DepositClass == DepositClass.Permanent)
            return new ErrorConDatos(CodigoSoloPorRetiro,
                $"La cuenta {cuenta.Number} se cancela sólo con el retiro del asociado (aportes y ahorro permanente).", new { nature = cuenta.Nature.ToString() });
        if (cuenta.DepositClass is DepositClass.TermDeposit or DepositClass.Contractual)
            return ErroresDeAportes.EntregaNoDisponible("La cancelación de un CDAT o de un ahorro contractual", nameof(EntregaDeCartera.K9));
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive or MemberAccountStatus.Matured))
            return ErroresDeAportes.NoOperable(cuenta.Number, cuenta.Status);
        return null;
    }

    /// <summary>Lo que impide cancelar a la fecha (FR-039): embargo, canje, cargos por cobrar, sobregiro técnico, titular fallecido.</summary>
    public static IReadOnlyList<CloseBlockerDto> Bloqueos(MemberAccount cuenta, CuentaParaMovimiento leida)
    {
        var bloqueos = new List<CloseBlockerDto>();
        if (cuenta.IsGarnished) bloqueos.Add(new("Garnishment", "La cuenta está embargada."));
        if (leida.Disponible.Retenido > 0m) bloqueos.Add(new("Hold", $"Tiene {leida.Disponible.Retenido:N2} bloqueados."));
        if (leida.Disponible.EnCanje > 0m) bloqueos.Add(new("PendingClearance", $"Tiene {leida.Disponible.EnCanje:N2} en canje."));
        if (cuenta.TechnicalNegativeSince is not null) bloqueos.Add(new("TechnicalOverdraft", "La cuenta tiene un sobregiro técnico."));
        if (leida.Titular.IsDeceased) bloqueos.Add(new("Deceased", "El titular figura como fallecido: la entrega es por el trámite de fallecimiento (K10)."));
        var porCobrar = leida.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable);
        if (porCobrar > 0m) bloqueos.Add(new("PendingCharges", $"Tiene {porCobrar:N2} en cargos por cobrar: cóbrelos o condónelos antes de cancelar."));
        return bloqueos;
    }

    /// <summary>
    /// Lo que se entrega de <paramref name="saldo"/> con el GMF de la cancelación: el mayor valor cuya suma con su GMF no pasa del saldo (el GMF se redondea,
    /// así que se busca hacia abajo unos pocos pesos). Devuelve la entrega y el GMF.
    /// </summary>
    public async Task<(decimal Entrega, decimal Gmf)> EntregaAsync(CuentaParaMovimiento leida, decimal saldo, DateOnly fecha, CancellationToken ct)
    {
        if (saldo <= 0m) return (0m, 0m);
        var monto = saldo;
        for (var intento = 0; intento < 6; intento++)
        {
            var gmf = await movimientos.GmfAsync(leida, HechosGeneradoresDeGmf.Cancelacion, monto, fecha, ct);
            var valor = gmf.IsSuccess ? gmf.Value?.Valor ?? 0m : 0m;
            if (monto + valor <= saldo) return (monto, valor);
            monto -= Math.Max(RedondeoDeCartera.Centavo, monto + valor - saldo);
            if (monto <= 0m) return (0m, 0m);
        }
        return (0m, 0m);
    }

    public async Task<Result<MemberAccountClosingPreviewDto>> PrevisualizarAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        if (Admite(cuenta) is { } no) return Result.Failure<MemberAccountClosingPreviewDto>(no);
        var leida = await movimientos.LeerAsync(cuenta, fecha, ct);
        var bloqueos = Bloqueos(cuenta, leida);
        var control = await db.MemberAccountControls.AsNoTracking().FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        InterestSimulationDto? simulada = null;
        var neto = 0m;
        if (LiquidacionDeIntereses.GanaInteres(cuenta))
        {
            var r = await liquidacion.CalcularAsync(cuenta, control, fecha, reloj.UtcNow, desdeElMotor: true, ct);
            if (r.IsFailure) return Result.Failure<MemberAccountClosingPreviewDto>(r.Error);
            var c = r.Value;
            neto = c.Neto;
            simulada = new InterestSimulationDto(fecha, reloj.UtcNow, c.Desde.AddDays(1), c.Hasta, c.Causado, c.Bruto, c.Redondeo,
                new InterestWithholdingDto(c.Retencion.Base, c.Retencion.ComponenteInflacionario, c.Retencion.ComponenteInflacionarioValor, c.Retencion.BaseGravable,
                    c.Retencion.Tarifa, c.Retencion.UmbralDiario, c.Retencion.UmbralAplica, c.Retencion.DiasQueAlcanzan, c.Retencion.Valor, c.Retencion.MotivoDeExencion,
                    c.Perfil.ToString(), c.Retencion.Explicacion), c.Neto, []);
        }
        // Lo causado sin liquidar ya está en el saldo del titular (interés por pagar): después de liquidar, el saldo es el capital más el neto.
        var capital = leida.Saldos.GetValueOrDefault(MemberAccountConcept.Principal);
        var despues = capital + neto - leida.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable);
        var (entrega, gmf) = await EntregaAsync(leida, despues, fecha, ct);
        return Result.Success(new MemberAccountClosingPreviewDto(fecha, simulada, despues, gmf, leida.Saldos.GetValueOrDefault(MemberAccountConcept.ChargeReceivable),
            entrega, bloqueos));
    }
}

// ------------------------------------------------------------------------------------------------------------- vista previa --

/// <summary>La vista previa de la cancelación (api.md §4, <c>GET /{id}/close-preview?date=</c>, <c>Lending.Accounts.Read</c>). Sin efectos. (nuevo)</summary>
public sealed record PreviewMemberAccountClosingQuery(Guid AccountPublicId, DateOnly? Date = null) : IRequest<Result<MemberAccountClosingPreviewDto>>;

public sealed class PreviewMemberAccountClosingQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    CancelacionDeCuenta cancelacion,
    IDateTimeService reloj) : IRequestHandler<PreviewMemberAccountClosingQuery, Result<MemberAccountClosingPreviewDto>>
{
    public async Task<Result<MemberAccountClosingPreviewDto>> Handle(PreviewMemberAccountClosingQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountClosingPreviewDto>(leida.Error);
        return await cancelacion.PrevisualizarAsync(leida.Value, request.Date ?? reloj.HoyLocal, ct);
    }
}

// ----------------------------------------------------------------------------------------------------------------- cancelar --

/// <summary>
/// La cancelación de una cuenta de ahorro a la vista (feature 014, US5, T135; api.md §4, <c>POST /{id}/close</c>, <c>Lending.Accounts.Close</c>, con clave;
/// FR-039), hoy y con las firmas del manejo, en una transacción con el cerrojo de la persona y de las cuentas:
/// <list type="number">
/// <item>sin bloqueos (<c>Lending.Savings.CloseBlocked</c> con la lista: embargo, bloqueo, canje, cargos por cobrar, sobregiro, fallecido);</item>
/// <item>nace la operación <c>Cancellation</c> (<c>CAN-</c>), pone al día la causación hasta hoy en ella (partes <c>ALD-</c>), liquida los períodos vencidos y
/// el último hasta hoy con su retención —antes de entregar— (<c>InteresDeCaptacionLiquidado</c> + <c>RetencionPracticada</c>, origen <c>AccountClosing</c>);</item>
/// <item>entrega todo el saldo con el GMF de la cancelación (hecho generador <c>Cancellation</c>): por orden de giro a Tesorería (la cuenta queda en cierre
/// y se cancela cuando el giro se confirma; si se rechaza, vuelve a activa con su saldo) o por traslado a otra cuenta (se cancela en el acto).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record CloseMemberAccountCommand(Guid AccountPublicId, DateOnly? Date, CloseDeliveryInput Delivery, IReadOnlyList<Guid> Signatures, string Reason)
    : IRequest<Result<MemberAccountClosingResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseMemberAccountCommandValidator : ValidadorConMotivo<CloseMemberAccountCommand>
{
    public CloseMemberAccountCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Delivery).NotNull();
        RuleFor(x => x.Delivery.Kind).IsInEnum().When(x => x.Delivery is not null);
        RuleFor(x => x.Delivery.TargetAccountPublicId).NotEmpty()
            .When(x => x.Delivery is { Kind: CloseDeliveryKind.TransferToAccount }).WithMessage("El traslado nombra la cuenta destino.");
        RuleFor(x => x.Signatures).NotNull();
    }
}

public sealed class CloseMemberAccountCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ICerrojoDeCartera cerrojo,
    CancelacionDeCuenta cancelacion,
    CausacionDeCaptaciones causacion,
    LiquidacionDeIntereses liquidacion,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    TrasladoDeCuenta traslados,
    OrdenesDeGiroDeCuentas ordenes,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CloseMemberAccountCommand, Result<MemberAccountClosingResultDto>>
{
    /// <summary>El prefijo del comprobante de una cancelación.</summary>
    public const string PrefijoCancelacion = "CAN-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<MemberAccountClosingResultDto>> Handle(CloseMemberAccountCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var fecha = request.Date ?? hoy;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var vista = leida.Value;
        if (CancelacionDeCuenta.Admite(vista) is { } no) return Falla(no);
        if (fecha != hoy)
            return Falla(new ErrorConDatos(CancelacionDeCuenta.CodigoFechaDeCancelacion, $"La cuenta se cancela con la fecha de hoy ({ErroresDeCuentas.Fecha(hoy)}).",
                new { date = fecha, today = hoy }));

        MemberAccount? destino = null;
        var personaDelGiro = vista.HolderPersonId;
        BeneficiarioDeOrden? banco = null;
        if (request.Delivery.Kind == CloseDeliveryKind.TransferToAccount)
        {
            destino = await db.MemberAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.Delivery.TargetAccountPublicId && !a.IsDeleted, ct);
            if (destino is null) return Falla(ErroresDeCuentas.CuentaInexistente());
            if (destino.Id == vista.Id) return Falla(ErroresDeMovimientos.MismaCuenta());
        }
        else
        {
            if (request.Delivery.Payee?.PersonPublicId is { } beneficiario && beneficiario != Guid.Empty)
                personaDelGiro = await db.People.AsNoTracking().Where(p => p.PublicId == beneficiario).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? personaDelGiro;
            var b = await ordenes.BeneficiarioAsync(personaDelGiro, request.Delivery.Payee?.BankPublicId, request.Delivery.Payee?.AccountType,
                request.Delivery.Payee?.AccountNumber, ct);
            if (b.IsFailure) return Falla(b.Error);
            banco = b.Value;
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            var personas = new[] { vista.HolderPersonId, destino?.HolderPersonId ?? vista.HolderPersonId }.Distinct().Order().ToList();
            await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync(new[] { vista.Id, destino?.Id ?? vista.Id }.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == vista.Id, ct);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
            var antes = await movimientos.LeerAsync(cuenta, fecha, ct);
            var bloqueos = CancelacionDeCuenta.Bloqueos(cuenta, antes);
            if (bloqueos.Count > 0) return Falla(ErroresDeMovimientos.CierreBloqueado(bloqueos.Cast<object>().ToList()));

            // 1. La operación de la cancelación: en ella van la puesta al día de la causación y la liquidación a la fecha.
            var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
            if (hecho.IsFailure) return Falla(hecho.Error);
            var actor = await actorActual.ObtenerAsync(ct);
            var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Cancellation, PrefijoCancelacion, ct);
            var motivo = request.Reason.Trim();
            var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new MemberAccountOperation
            {
                MemberAccountId = cuenta.Id,
                CounterpartMemberAccountId = destino?.Id,
                Kind = MemberAccountOperationKind.Cancellation,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = fecha,
                AccountingDate = hecho.Value.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = 0m,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                CollectorCode = LiquidacionDeIntereses.RecaudadorPropio,
                Reason = motivo.Length <= 500 ? motivo : motivo[..500],
                Payload = JsonSerializer.Serialize(new { account = cuenta.PublicId, date = fecha, delivery = request.Delivery, signatures = request.Signatures }, Json),
                InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Fecha("date", fecha).Texto("delivery", request.Delivery.Kind.ToString())
                    .Identificador("target", destino?.PublicId).Calcular(),
                RulesVersion = VersionesDeReglas.Vigente,
            });
            await db.SaveChangesAsync(ct);

            InterestSettlementDto? liquidada = null;
            if (LiquidacionDeIntereses.GanaInteres(cuenta))
            {
                var alDia = await causacion.PonerAlDiaAsync(cuenta, fecha, operacion, ct);
                if (alDia.IsFailure) return Falla(alDia.Error);
                await db.SaveChangesAsync(ct);
                foreach (var fin in (await liquidacion.PeriodosVencidosAsync(cuenta, control, fecha, ct)).Where(f => f < fecha))
                {
                    var vencida = await liquidacion.LiquidarAsync(cuenta, control, fin, null, null, motivo, ct);
                    if (vencida.IsFailure) return Falla(vencida.Error);
                }
                var ultima = await liquidacion.LiquidarAsync(cuenta, control, fecha, null, operacion, motivo, ct);
                if (ultima.IsFailure) return Falla(ultima.Error);
                if (ultima.Value is { Liquidacion: { } l, Operacion: { } o }) liquidada = Liquidaciones.Dto(l, o, ultima.Value.YaExistia);
            }

            // 2. La entrega de todo el saldo con el GMF de la cancelación.
            var despues = await movimientos.LeerAsync(cuenta, fecha, ct);
            var saldo = despues.Saldo - despues.Disponible.Retenido - despues.Disponible.Embargado - despues.Disponible.EnCanje;
            var (entrega, _) = await cancelacion.EntregaAsync(despues, saldo, fecha, ct);
            MemberAccountOperationResultDto? resultado = null;
            if (entrega > 0m)
            {
                if (destino is not null)
                {
                    var t = await traslados.AplicarAsync(Pantalla.Traslado(cuenta.PublicId, destino.PublicId, entrega, fecha, request.Signatures, null, motivo)
                        with { EsCancelacion = true }, ct);
                    if (t.IsFailure) return Falla(t.Error);
                    resultado = new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, t.Value.Comprobante, null, null, null);
                }
                else
                {
                    var r = await retiros.AplicarAsync(Pantalla.Retiro(cuenta.PublicId, entrega, fecha, ClaseDeMedioDeRecaudo.Transfer, request.Signatures, null, motivo,
                        new OrdenDeRetiro(personaDelGiro, banco!)) with
                    {
                        EsCancelacion = true, ExigeAtribucion = false, HechoGenerador = HechosGeneradoresDeGmf.Cancelacion,
                    }, ct);
                    if (r.IsFailure) return Falla(r.Error);
                    var orden = r.Value.Orden;
                    resultado = new MemberAccountOperationResultDto(MemberAccountOperationResultDto.Aplicada, r.Value.Comprobante,
                        orden is null ? null : new MemberAccountOrderDto(orden.PublicId, orden.Number, orden.Status.ToString()), null, null);
                }
            }

            // 3. El estado: cancelada si ya no queda saldo y no hay giro en curso; en cierre mientras Tesorería gira.
            var final = await movimientos.LeerAsync(cuenta, fecha, ct);
            var ahora = reloj.UtcNow;
            var enGiro = destino is null && entrega > 0m;
            if (cuenta.Status != MemberAccountStatus.Closing) cuenta.PedirCierre(ahora);
            if (!enGiro && final.Saldo == 0m) cuenta.Cerrar(fecha, actor.UserId, motivo, ahora);
            await db.SaveChangesAsync(ct);
            return Result.Success(new MemberAccountClosingResultDto(operacion.PublicId, operacion.Number, liquidada, resultado, entrega, cuenta.Status));
        }, ct);
    }

    private static Result<MemberAccountClosingResultDto> Falla(Error e) => Result.Failure<MemberAccountClosingResultDto>(e);
}
