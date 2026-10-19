using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

// Feature 014, US6 (T173; api.md §5): la instrucción al vencimiento, la renovación con condiciones nuevas y la redención (anticipada o en la gracia) de un
// CDAT. Las tres exigen la entrega K9 y el CDAT en el alcance (404 Lending.TermDeposit.NotFound fuera de él). (nuevo)

/// <summary>Los códigos de la renovación, la instrucción y la redención (api.md §5). (nuevo)</summary>
public static class ErroresDeVencimientoDeCdat
{
    public const string CodigoFueraDeVentana = "Lending.TermDeposit.NotInRenewalWindow";
    public const string CodigoGraciaVencida = "Lending.TermDeposit.GraceExpired";
    public const string CodigoEmbargado = "Lending.TermDeposit.Garnished";
    public const string CodigoTitularFallecido = "Lending.TermDeposit.DeceasedHolder";
    public const string CodigoRedencionNoAdmitida = "Lending.TermDeposit.EarlyRedemptionNotAllowed";
    public const string CodigoNegociacionConAprobacion = "Lending.TermDeposit.NegotiationRequiresApproval";
    public const string CodigoNoOperable = "Lending.TermDeposit.NotOperable";

    public static Error FueraDeVentana(DateOnly vencimiento, DateOnly finDeLaGracia) => new ErrorConDatos(CodigoFueraDeVentana,
        $"El CDAT se renueva entre su vencimiento ({ErroresDeCuentas.Fecha(vencimiento)}) y el fin de la gracia ({ErroresDeCuentas.Fecha(finDeLaGracia)}).",
        new { maturityDate = vencimiento, graceUntil = finDeLaGracia });

    public static Error GraciaVencida(DateOnly finDeLaGracia) => new ErrorConDatos(CodigoGraciaVencida,
        $"La gracia terminó el {ErroresDeCuentas.Fecha(finDeLaGracia)}: la instrucción al vencimiento ya no cambia.", new { graceUntil = finDeLaGracia });

    public static Error Embargado(string numero) => new ErrorConDatos(CodigoEmbargado,
        $"El CDAT {numero} está embargado: no se paga, renueva ni redime; queda a disposición del juzgado (FR-072).", new { accountNumber = numero });

    public static Error Fallecido(string numero) => new ErrorConDatos(CodigoTitularFallecido,
        $"El titular del CDAT {numero} falleció: queda a disposición para la entrega a sus beneficiarios (FR-072).", new { accountNumber = numero });

    public static Error RedencionNoAdmitida(string numero, string porque) => new ErrorConDatos(CodigoRedencionNoAdmitida,
        $"El CDAT {numero} no se redime antes de su vencimiento: {porque}", new { accountNumber = numero });

    public static Error NoOperable(string numero, MemberAccountStatus estado) => new ErrorConDatos(CodigoNoOperable,
        $"El CDAT {numero} está «{estado}»: no admite la operación.", new { accountNumber = numero, status = estado.ToString() });

    /// <summary>Embargado o con el titular fallecido: no se opera (FR-072).</summary>
    public static async Task<Error?> ADisposicionAsync(IApplicationDbContext db, MemberAccount cuenta, CancellationToken ct)
    {
        if (cuenta.IsGarnished) return Embargado(cuenta.Number);
        return await db.People.AsNoTracking().IgnoreQueryFilters().AnyAsync(p => p.Id == cuenta.HolderPersonId && p.IsDeceased, ct) ? Fallecido(cuenta.Number) : null;
    }
}

// ------------------------------------------------------------------------------------------------------ instrucción --

/// <summary>
/// <c>PUT /api/lending/term-deposits/{id}/maturity-instruction</c> (<c>Lending.TermDeposits.Renew</c>, con clave; api.md §5; research E136): cambia la instrucción
/// al vencimiento —prorrogar, renovar con condiciones o cancelar y pagar— y, si se da, su destino, hasta el fin de la gracia
/// (<c>Lending.TermDeposit.GraceExpired</c>). Se guarda en la cuenta: la versión sellada no se edita. (nuevo)
/// </summary>
public sealed record SetTermDepositMaturityInstructionCommand(Guid AccountPublicId, MaturityInstruction Instruction, TermDepositDestinationInput? PaymentDestination,
    string Reason) : IRequest<Result<TermDepositDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetTermDepositMaturityInstructionCommandValidator : AbstractValidator<SetTermDepositMaturityInstructionCommand>
{
    public SetTermDepositMaturityInstructionCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Instruction).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Diga por qué cambia la instrucción.");
        RuleFor(x => x.PaymentDestination!.Kind).IsInEnum().When(x => x.PaymentDestination is not null);
    }
}

public sealed class SetTermDepositMaturityInstructionCommandHandler(IApplicationDbContext db, IContextoDeLineasDeCaptacion contexto, LectorDeCdat lector,
    IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<SetTermDepositMaturityInstructionCommand, Result<TermDepositDto>>
{
    public async Task<Result<TermDepositDto>> Handle(SetTermDepositMaturityInstructionCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K9) return Falla(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        var encontrada = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (encontrada.IsFailure) return Falla(encontrada.Error);
        var cdat = encontrada.Value;
        if (cdat.Status != MemberAccountStatus.Active) return Falla(ErroresDeVencimientoDeCdat.NoOperable(cdat.Number, cdat.Status));
        var leido = await lector.LeerAsync(cdat, ct);
        if (leido.Vigente is not { } v) return Falla(ErroresDeVencimientoDeCdat.NoOperable(cdat.Number, cdat.Status));
        var plan = await lector.PlanAsync(v, ct);
        var hoy = reloj.HoyLocal;
        var gracia = await PiezasDeCdat.FinDeLaGraciaAsync(lector, v, plan, ct);
        if (hoy > gracia) return Falla(ErroresDeVencimientoDeCdat.GraciaVencida(gracia));

        DestinoDeCdat? destino = null;
        if (request.PaymentDestination is { } d)
        {
            var resuelto = await DestinosDeCdat.ValidarAsync(db, alcance, auditoria, d, cdat.HolderPersonId, ct);
            if (resuelto.IsFailure) return Falla(resuelto.Error);
            destino = resuelto.Value;
        }
        var actor = await actorActual.ObtenerAsync(ct);
        cdat.MaturityInstructionJson = new InstruccionDeCdat(request.Instruction, destino, actor.UserId, reloj.UtcNow, LectorDeCdat.Recortar(request.Reason, 300))
            .Serializar();
        await db.SaveChangesAsync(ct);
        return Result.Success(await RespuestasDeCdat.DtoAsync(db, lector, cdat, hoy, ct));
    }

    private static Result<TermDepositDto> Falla(Error e) => Result.Failure<TermDepositDto>(e);
}

/// <summary>La validación de un destino pedido para un CDAT (como en la constitución, research E133): cuenta a la vista viva del titular en el alcance, o giro. (nuevo)</summary>
public static class DestinosDeCdat
{
    public static async Task<Result<DestinoDeCdat>> ValidarAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        TermDepositDestinationInput d, int titularId, CancellationToken ct)
    {
        if (d.Kind == TermDepositDestinationKind.Account)
        {
            if (d.AccountPublicId is not { } id) return Result.Failure<DestinoDeCdat>(ErroresDeCdat.DestinoInvalido("Elija la cuenta de ahorro destino."));
            var cuenta = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, id, seguir: false, ct);
            if (cuenta.IsFailure) return Result.Failure<DestinoDeCdat>(cuenta.Error);
            if (cuenta.Value.Nature != LineNature.Deposit || cuenta.Value.DepositClass != DepositClass.Demand || !cuenta.Value.Viva
                || cuenta.Value.HolderPersonId != titularId)
                return Result.Failure<DestinoDeCdat>(ErroresDeCdat.DestinoInvalido("El CDAT se abona en una cuenta de ahorro a la vista viva del titular."));
            return Result.Success(new DestinoDeCdat(TermDepositDestinationKind.Account, id, null, null, null, null));
        }
        if (d.PayeePersonPublicId is not { } persona || !await db.People.AsNoTracking().AnyAsync(p => p.PublicId == persona && !p.IsDeleted, ct))
            return Result.Failure<DestinoDeCdat>(ErroresDeCdat.DestinoInvalido("El beneficiario del giro no existe."));
        if (d.BankPublicId is { } banco && !await db.Banks.AsNoTracking().AnyAsync(b => b.PublicId == banco && !b.IsDeleted, ct))
            return Result.Failure<DestinoDeCdat>(ErroresDeCdat.DestinoInvalido("El banco del giro no existe."));
        return Result.Success(new DestinoDeCdat(TermDepositDestinationKind.PaymentOrder, null, persona, d.BankPublicId, d.AccountType,
            string.IsNullOrWhiteSpace(d.AccountNumber) ? null : d.AccountNumber.Trim()));
    }
}

// --------------------------------------------------------------------------------------------------------- redención --

/// <summary>La vista previa de la redención (api.md §5, <c>POST /{id}/redeem/simulate</c>). (nuevo)</summary>
public sealed record TermDepositRedemptionPreviewDto(
    DateOnly Date,
    bool IsEarly,
    decimal Principal,
    decimal AccruedInterest,
    TermDepositPenaltyDto Penalty,
    decimal Interest,
    decimal Withholding,
    decimal GmfIfCreditedToAccount,
    decimal Net,
    IReadOnlyList<string> Explanation,
    string Fingerprint);

/// <summary>La penalidad de la vista previa: el modo, la tasa de reliquidación y lo que cuesta (ajuste más penalidades). (nuevo)</summary>
public sealed record TermDepositPenaltyDto(string? Kind, decimal? RepricedRate, decimal? RepricedInterest, decimal Amount);

/// <summary>Lo que dejó la redención: aplicada (con la operación y la orden) o en espera de aprobación (la atribución con monto). (nuevo)</summary>
public sealed record TermDepositRedemptionResultDto(string Status, Guid? OperationPublicId, string? OperationNumber, Guid? PayoutOrderPublicId, decimal Delivered,
    Guid? ApprovalPublicId, TermDepositDto? TermDeposit);

/// <summary>
/// <c>POST /api/lending/term-deposits/{id}/redeem/simulate</c> (<c>Lending.TermDeposits.Redeem</c>, sin clave; api.md §5): lo que se pagaría al redimir a
/// <paramref name="Date"/> —antes del vencimiento con la penalidad sellada (<c>Lending.TermDeposit.EarlyRedemptionNotAllowed</c> si la línea no la admite, US6-4);
/// desde el vencimiento sin penalidad—, con la retención, el GMF si se abona en cuenta, el neto, la explicación y la huella que el comando compara. (nuevo)
/// </summary>
public sealed record SimulateTermDepositRedemptionQuery(Guid AccountPublicId, DateOnly Date) : IRequest<Result<TermDepositRedemptionPreviewDto>>;

public sealed class SimulateTermDepositRedemptionQueryHandler(IApplicationDbContext db, IContextoDeLineasDeCaptacion contexto, SalidaDeCdat salidas, LectorDeCdat lector,
    IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj) : IRequestHandler<SimulateTermDepositRedemptionQuery, Result<TermDepositRedemptionPreviewDto>>
{
    public async Task<Result<TermDepositRedemptionPreviewDto>> Handle(SimulateTermDepositRedemptionQuery request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K9) return Result.Failure<TermDepositRedemptionPreviewDto>(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        var encontrada = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<TermDepositRedemptionPreviewDto>(encontrada.Error);
        var calculo = await RedencionesDeCdat.CalcularAsync(db, salidas, lector, encontrada.Value, request.Date, reloj.HoyLocal, ct);
        return calculo.IsFailure ? Result.Failure<TermDepositRedemptionPreviewDto>(calculo.Error) : Result.Success(RedencionesDeCdat.Dto(calculo.Value, request.Date));
    }
}

/// <summary>El destino de la redención (api.md §5): a una cuenta de ahorro del titular o por orden de giro a un beneficiario. (nuevo)</summary>
public sealed record TermDepositRedemptionDestinationInput(TermDepositDestinationKind Kind, Guid? TargetAccountPublicId = null, Guid? PayeePersonPublicId = null,
    Guid? BankPublicId = null, byte? AccountType = null, string? AccountNumber = null)
{
    public TermDepositDestinationInput ComoDestino() => new(Kind, TargetAccountPublicId, PayeePersonPublicId, BankPublicId, AccountType, AccountNumber);
}

/// <summary>
/// <c>POST /api/lending/term-deposits/{id}/redeem</c> (<c>Lending.TermDeposits.Redeem</c>, con clave; api.md §5; FR-071, FR-072; US6-4): redime el CDAT hoy.
/// Antes del vencimiento, sólo si la versión lo admite y pasaron los días mínimos, con la penalidad sellada y, si el capital supera la atribución con monto
/// del usuario (<c>Lending.TermDeposits.Redeem</c>), en espera de un aprobador presente (202, sin mover saldo); desde el vencimiento hasta el fin de la gracia
/// (o vencido por pagar), sin penalidad. Compara la huella de la vista previa (<c>Lending.Savings.PreviewChanged</c>). Embargado o con el titular fallecido
/// no se redime. (nuevo)
/// </summary>
public sealed record RedeemTermDepositCommand(Guid AccountPublicId, TermDepositRedemptionDestinationInput Destination, string? ExpectedPreviewSha256, string Reason,
    Guid? ApprovalPublicId = null) : IRequest<Result<TermDepositRedemptionResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RedeemTermDepositCommandValidator : AbstractValidator<RedeemTermDepositCommand>
{
    public RedeemTermDepositCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Destination).NotNull();
        RuleFor(x => x.Destination.Kind).IsInEnum().When(x => x.Destination is not null);
        RuleFor(x => x.Destination.TargetAccountPublicId).NotEmpty().When(x => x.Destination is { Kind: TermDepositDestinationKind.Account })
            .WithMessage("Elija la cuenta de ahorro destino.");
        RuleFor(x => x.Destination.PayeePersonPublicId).NotEmpty().When(x => x.Destination is { Kind: TermDepositDestinationKind.PaymentOrder })
            .WithMessage("Elija el beneficiario del giro.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Diga por qué se redime.");
    }
}

public sealed class RedeemTermDepositCommandHandler(IApplicationDbContext db, IContextoDeLineasDeCaptacion contexto, SalidaDeCdat salidas, LectorDeCdat lector,
    ReglasDeMovimientos movimientos, ICerrojoDeCartera cerrojo, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<RedeemTermDepositCommand, Result<TermDepositRedemptionResultDto>>
{
    public async Task<Result<TermDepositRedemptionResultDto>> Handle(RedeemTermDepositCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K9) return Falla(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        var encontrada = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (encontrada.IsFailure) return Falla(encontrada.Error);
        var cdat = encontrada.Value;
        var destino = await DestinosDeCdat.ValidarAsync(db, alcance, auditoria, request.Destination.ComoDestino(), cdat.HolderPersonId, ct);
        if (destino.IsFailure) return Falla(destino.Error);
        var hoy = reloj.HoyLocal;
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cdat.HolderPersonId], ModoDeEspera.Esperar, ct);
            var resuelto = await PiezasDeCdat.DestinoAsync(db, destino.Value, ct);
            if (resuelto.IsFailure) return Falla(resuelto.Error);
            var ids = new List<int> { cdat.Id };
            if (resuelto.Value.Cuenta is { } otra) ids.Add(otra.Id);
            await cerrojo.BloquearCuentasAsync(ids.Order().ToList(), ModoDeEspera.Esperar, ct);
            var calculo = await RedencionesDeCdat.CalcularAsync(db, salidas, lector, cdat, hoy, hoy, ct);
            if (calculo.IsFailure) return Falla(calculo.Error);
            var s = calculo.Value;
            if (!string.IsNullOrWhiteSpace(request.ExpectedPreviewSha256) && !string.Equals(request.ExpectedPreviewSha256, s.Huella, StringComparison.Ordinal))
                return Falla(ErroresDeCdat.VistaPreviaCambio(RedencionesDeCdat.Dto(s, hoy)));
            int? aprobadoPor = null;
            if (s.Anticipada)
            {
                var huella = ReglasDeMovimientos.HuellaDeOperacion(nameof(MemberAccountOperationKind.TermDepositRedemption), null, null, cdat.PublicId, s.Capital,
                    resuelto.Value.Cuenta?.PublicId);
                var atribucion = await movimientos.AtribucionAsync(ReglasDeMovimientos.PermisoDeRedencion, cdat, s.Capital, huella,
                    $"Redención anticipada del CDAT {cdat.Number}", request.ApprovalPublicId, hoy, ct);
                if (atribucion.IsFailure)
                {
                    if (atribucion.Error.Code != ErroresDeMovimientos.CodigoPendienteDeAprobacion) return Falla(atribucion.Error);
                    await db.SaveChangesAsync(ct);
                    Guid? aprobacion = atribucion.Error is ErrorConDatos datos && JsonSerializer.SerializeToElement(datos.Data) is { ValueKind: JsonValueKind.Object } json
                                       && json.TryGetProperty("approvalPublicId", out var ap) && ap.TryGetGuid(out var g)
                        ? g
                        : null;
                    return Result.Success(new TermDepositRedemptionResultDto("PendingApproval", null, null, null, 0m, aprobacion, null));
                }
                aprobadoPor = atribucion.Value.AprobadoPor;
            }
            var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cdat.Id, ct);
            var aplicada = await salidas.AplicarAsync(s, control, resuelto.Value, hoy, s.Anticipada ? null : EmisionDeCartera.CancelacionEnLaGracia, request.Reason, null,
                aprobadoPor, PrevalidationOutcome.NotApplicable, ct);
            if (aplicada.IsFailure) return Falla(aplicada.Error);
            var a = aplicada.Value;
            return Result.Success(new TermDepositRedemptionResultDto("Applied", a.Operacion.PublicId, a.Operacion.Number, a.Orden?.PublicId, a.Entregado, null,
                await RespuestasDeCdat.DtoAsync(db, lector, cdat, hoy, ct)));
        }, ct);
    }

    private static Result<TermDepositRedemptionResultDto> Falla(Error e) => Result.Failure<TermDepositRedemptionResultDto>(e);
}

/// <summary>Lo común de la vista previa y la redención: las reglas de cuándo se redime y la forma de la respuesta. (nuevo)</summary>
public static class RedencionesDeCdat
{
    /// <summary>
    /// Las reglas de FR-071 y FR-072 y el cálculo: activo o vencido por pagar; ni embargado ni de titular fallecido; antes del vencimiento sólo si la versión
    /// admite la redención anticipada y pasaron sus días mínimos; la fecha es hoy o después (la simulación admite futuras).
    /// </summary>
    public static async Task<Result<SalidaDeCdatCalculada>> CalcularAsync(IApplicationDbContext db, SalidaDeCdat salidas, LectorDeCdat lector, MemberAccount cdat,
        DateOnly fecha, DateOnly hoy, CancellationToken ct)
    {
        if (cdat.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Matured))
            return Result.Failure<SalidaDeCdatCalculada>(ErroresDeVencimientoDeCdat.NoOperable(cdat.Number, cdat.Status));
        if (await ErroresDeVencimientoDeCdat.ADisposicionAsync(db, cdat, ct) is { } aDisposicion) return Result.Failure<SalidaDeCdatCalculada>(aDisposicion);
        if (fecha < hoy) return Result.Failure<SalidaDeCdatCalculada>(ErroresDeCdat.FechaDeInicio(hoy));
        var leido = await lector.LeerAsync(cdat, ct);
        if (leido.Vigente is not { } v) return Result.Failure<SalidaDeCdatCalculada>(ErroresDeVencimientoDeCdat.NoOperable(cdat.Number, cdat.Status));
        if (cdat.Status == MemberAccountStatus.Active && fecha < (v.MaturityDate ?? fecha))
        {
            if (v.EarlyRedemptionAllowed != true)
                return Result.Failure<SalidaDeCdatCalculada>(ErroresDeVencimientoDeCdat.RedencionNoAdmitida(cdat.Number, "la línea no admite la redención anticipada (D18)."));
            var corridos = fecha.DayNumber - (v.StartDate ?? v.EffectiveValueDate).DayNumber;
            if (v.MinDaysBeforeRedemption is { } minimo && corridos < minimo)
                return Result.Failure<SalidaDeCdatCalculada>(ErroresDeVencimientoDeCdat.RedencionNoAdmitida(cdat.Number,
                    $"se redime desde los {minimo} días; van {corridos}."));
        }
        return await salidas.CalcularAsync(cdat, fecha, ct);
    }

    public static TermDepositRedemptionPreviewDto Dto(SalidaDeCdatCalculada s, DateOnly fecha) => new(fecha, s.Anticipada, s.Capital, s.InteresPorPagar,
        new TermDepositPenaltyDto(s.Anticipada ? s.Version.EarlyRedemptionPenaltyMode?.ToString() : null,
            s.Anticipada && s.Version.EarlyRedemptionPenaltyMode == EarlyRedemptionPenaltyMode.PenaltyRate ? s.Version.PenaltyAnnualRate : null, s.Calculo.InteresReliquidado,
            s.Calculo.CostoDeLaRedencion),
        s.Calculo.InteresAPagar, s.Retencion.Valor, s.GmfSiSeAbona?.Valor ?? 0m, s.Entregado, [s.Calculo.Explicacion, s.Retencion.Explicacion], s.Huella);
}

// --------------------------------------------------------------------------------------------------------- renovación --

/// <summary>El cambio de monto de una renovación (api.md §5): aumento desde una cuenta de ahorro del titular o disminución a cuenta o por giro. (nuevo)</summary>
public sealed record TermDepositAmountChangeInput(string Kind, decimal Amount, Guid? SourceAccountPublicId = null, IReadOnlyList<Guid>? Signers = null,
    TermDepositDestinationInput? Destination = null)
{
    public const string Aumento = "Increase";
    public const string Disminucion = "Decrease";
}

/// <summary>El cuerpo de <c>POST /api/lending/term-deposits/{id}/renew</c> (api.md §5). (nuevo)</summary>
public sealed record RenewTermDepositInput(int TermDays, RepaymentPeriodicity InterestPeriodicity, InterestTiming Timing, string Reason,
    TermDepositAmountChangeInput? AmountChange = null, decimal? RequestedMarginEa = null, MaturityInstruction? MaturityInstruction = null,
    TermDepositDestinationInput? PaymentDestination = null);

/// <summary>
/// <c>POST /api/lending/term-deposits/{id}/renew</c> (<c>Lending.TermDeposits.Renew</c>, con clave; api.md §5; US6; FR-068, FR-073): renueva el CDAT con
/// condiciones nuevas entre su vencimiento y el fin de la gracia (<c>Lending.TermDeposit.NotInRenewalWindow</c>): paga lo pendiente del plan, cambia el monto
/// (aumento desde una cuenta de ahorro del titular con su GMF; disminución a una cuenta o por giro), valida el plazo, la periodicidad y la tasa de la tabla
/// como la constitución y, con margen, sólo dentro de la atribución de quien renueva (<c>Lending.TermDeposit.NegotiationRequiresApproval</c>). Versión nueva
/// (<c>Cause = Renewal</c>) con su plan, título nuevo y el hecho <c>CdatRenovado</c> (+ GMF, + reclasificación por plazo). (nuevo)
/// </summary>
public sealed record RenewTermDepositCommand(Guid AccountPublicId, RenewTermDepositInput Input) : IRequest<Result<TermDepositDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RenewTermDepositCommandValidator : AbstractValidator<RenewTermDepositCommand>
{
    public RenewTermDepositCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.TermDays).GreaterThan(0).When(x => x.Input is not null);
        RuleFor(x => x.Input.InterestPeriodicity).IsInEnum().When(x => x.Input is not null);
        RuleFor(x => x.Input.Timing).IsInEnum().When(x => x.Input is not null);
        RuleFor(x => x.Input.Reason).NotEmpty().MaximumLength(500).When(x => x.Input is not null);
        RuleFor(x => x.Input.RequestedMarginEa).GreaterThan(0m).When(x => x.Input?.RequestedMarginEa is not null);
        RuleFor(x => x.Input.AmountChange!.Amount).GreaterThan(0m).When(x => x.Input?.AmountChange is not null);
        RuleFor(x => x.Input.AmountChange!.Kind).Must(k => k is TermDepositAmountChangeInput.Aumento or TermDepositAmountChangeInput.Disminucion)
            .When(x => x.Input?.AmountChange is not null).WithMessage("El cambio de monto es Increase o Decrease.");
        RuleFor(x => x.Input.AmountChange!.SourceAccountPublicId).NotEmpty()
            .When(x => x.Input?.AmountChange is { Kind: TermDepositAmountChangeInput.Aumento }).WithMessage("El aumento sale de una cuenta de ahorro del titular.");
        RuleFor(x => x.Input.AmountChange!.Destination).NotNull()
            .When(x => x.Input?.AmountChange is { Kind: TermDepositAmountChangeInput.Disminucion }).WithMessage("Diga a dónde va la disminución.");
    }
}

public sealed class RenewTermDepositCommandHandler(
    IApplicationDbContext db,
    IContextoDeLineasDeCaptacion contexto,
    LectorDeCdat lector,
    PagoDeInteresesDeCdat pagos,
    NegociacionDeTasa negociacion,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RenewTermDepositCommand, Result<TermDepositDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string PrefijoRenovacion = "RNC-";

    public async Task<Result<TermDepositDto>> Handle(RenewTermDepositCommand request, CancellationToken ct)
    {
        var e = request.Input;
        if (contexto.Entrega < EntregaDeCartera.K9) return Falla(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        var encontrada = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (encontrada.IsFailure) return Falla(encontrada.Error);
        var cdat = encontrada.Value;
        if (cdat.Status != MemberAccountStatus.Active) return Falla(ErroresDeVencimientoDeCdat.NoOperable(cdat.Number, cdat.Status));
        if (await ErroresDeVencimientoDeCdat.ADisposicionAsync(db, cdat, ct) is { } aDisposicion) return Falla(aDisposicion);
        var leido = await lector.LeerAsync(cdat, ct);
        if (leido.Vigente is not { } v) return Falla(ErroresDeVencimientoDeCdat.NoOperable(cdat.Number, cdat.Status));
        var plan = await lector.PlanAsync(v, ct);
        var hoy = reloj.HoyLocal;
        var gracia = await PiezasDeCdat.FinDeLaGraciaAsync(lector, v, plan, ct);
        if (hoy < plan.Vencimiento || hoy > gracia) return Falla(ErroresDeVencimientoDeCdat.FueraDeVentana(plan.Vencimiento, gracia));

        // Las condiciones nuevas, como en la constitución (FR-067): la línea vigente, el plazo, la periodicidad, el monto mínimo y la tasa de la tabla.
        var linea = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cdat.CreditLineId, hoy, null, ct);
        if (linea is null) return Falla(ErroresDeCuentas.LineaSinVersion(cdat.Number, hoy));
        if (PlanDeCdat.ValidarPlazo(e.TermDays, linea.MinTermDays, linea.MaxTermDays) is { } plazo) return Falla(ErroresDeCdat.De(plazo));
        if (PlanDeCdat.ValidarPeriodicidad(e.InterestPeriodicity, e.Timing, e.TermDays, linea.LeerPeriodicidadesDeInteres(), linea.LeerModalidadesDeInteres()) is { } per)
            return Falla(ErroresDeCdat.De(per));
        var cambio = e.AmountChange;
        var delta = cambio is null ? 0m : cambio.Kind == TermDepositAmountChangeInput.Aumento ? cambio.Amount : -cambio.Amount;
        var capital = v.PrincipalAmount + delta;
        if (capital < linea.MinOpeningAmount || capital <= 0m) return Falla(ErroresDeCdat.MontoFueraDeRango(capital, linea.MinOpeningAmount));
        var tabla = await lector.TasaDeLaTablaAsync(linea, e.TermDays, capital, hoy, ct);
        if (tabla.IsFailure) return Falla(tabla.Error);

        NegociacionDeCdat? negociada = null;
        if (e.RequestedMarginEa is { } margen)
        {
            if (!linea.AllowsNegotiatedRate) return Falla(ErroresDeCdat.LineaNoNegocia(cdat.Number));
            var titular = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cdat.HolderPersonId, ct);
            var codigo = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == cdat.CreditLineId).Select(l => l.Code).FirstAsync(ct);
            var pedido = await negociacion.PedidoAsync(titular, cdat.BranchId, capital, e.TermDays, tabla.Value.Tasa, margen, null, hoy, ct);
            var evaluada = await negociacion.EvaluarAsync(cdat.CreditLineId, codigo, pedido, hoy, ct);
            if (evaluada.IsFailure) return Falla(evaluada.Error);
            if (!evaluada.Value.Motor.CabeEnLaRegla) return Falla(NegociacionDeTasa.Rechazo(evaluada.Value.Motor));
            if (!evaluada.Value.CabeEnAtribucion)
                return Falla(new ErrorConDatos(ErroresDeVencimientoDeCdat.CodigoNegociacionConAprobacion,
                    "El margen pedido supera su atribución: la renovación con tasa negociada fuera de ella se pide como un CDAT nuevo o con quien tenga el nivel.",
                    new { requestedMarginEa = margen, maxMarginEa = evaluada.Value.Motor.MargenMaximo }));
            var actorQuePide = await actorActual.ObtenerAsync(ct);
            negociada = new NegociacionDeCdat(margen, evaluada.Value.Regla!.Id, evaluada.Value.Regla.PublicId, actorQuePide.UserId, e.Reason, null);
        }

        DestinoDeCdat? nuevoDestino = null;
        if (e.PaymentDestination is { } pd)
        {
            var validado = await DestinosDeCdat.ValidarAsync(db, alcance, auditoria, pd, cdat.HolderPersonId, ct);
            if (validado.IsFailure) return Falla(validado.Error);
            nuevoDestino = validado.Value;
        }
        MemberAccount? origen = null;
        DestinoDeCdat? destinoDeLaDisminucion = null;
        if (cambio is { Kind: TermDepositAmountChangeInput.Aumento, SourceAccountPublicId: { } fuente })
        {
            var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, fuente, seguir: true, ct);
            if (leida.IsFailure) return Falla(leida.Error);
            origen = leida.Value;
            if (origen.Nature != LineNature.Deposit || origen.DepositClass is not (DepositClass.Demand or DepositClass.Permanent) || !origen.Viva)
                return Falla(ErroresDeCdat.FondeoInvalido("El aumento sale de una cuenta de ahorro viva del asociado."));
        }
        else if (cambio is { Kind: TermDepositAmountChangeInput.Disminucion, Destination: { } dd })
        {
            var validado = await DestinosDeCdat.ValidarAsync(db, alcance, auditoria, dd, cdat.HolderPersonId, ct);
            if (validado.IsFailure) return Falla(validado.Error);
            destinoDeLaDisminucion = validado.Value;
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cdat.HolderPersonId], ModoDeEspera.Esperar, ct);
            DestinoResueltoDeCdat? disminucion = null;
            if (destinoDeLaDisminucion is not null)
            {
                var r = await PiezasDeCdat.DestinoAsync(db, destinoDeLaDisminucion, ct);
                if (r.IsFailure) return Falla(r.Error);
                disminucion = r.Value;
            }
            var ids = new List<int> { cdat.Id };
            if (origen is not null) ids.Add(origen.Id);
            if (disminucion?.Cuenta is { } dc) ids.Add(dc.Id);
            if (v.DestinationAccountId is { } interes) ids.Add(interes);
            await cerrojo.BloquearCuentasAsync(ids.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cdat.Id, ct);

            // Lo que el plan causa hasta el vencimiento y el proceso no causó entra en la renovación; lo pendiente del plan se paga antes (FR-069).
            var faltante = control.LastAccruedDate < plan.Vencimiento
                ? PlanDeCdat.CausadoEntre(plan, control.LastAccruedDate > plan.Condiciones.Inicio ? control.LastAccruedDate : plan.Condiciones.Inicio, plan.Vencimiento)
                : 0m;
            for (var dia = control.LastAccruedDate.AddDays(1); dia <= plan.Vencimiento; dia = dia.AddDays(1)) RegistroDeCuentas.AvanzarCausacion(control, dia);
            var pagados = await PiezasDeCdat.PeriodosPagadosAsync(db, cdat, plan, ct);
            foreach (var p in plan.Periodos.Where(p => !pagados.Contains(p.Numero)))
            {
                var pago = await pagos.PagarAsync(cdat, control, v, plan, p, hoy, null, ct);
                if (pago.IsFailure) return Falla(pago.Error);
            }

            var inicio = v.GraceAccrues == true ? plan.Vencimiento : hoy;
            var condiciones = new CondicionesDeCdat(capital, tabla.Value.Tasa + (negociada?.Margen ?? 0m), inicio, e.TermDays, e.InterestPeriodicity, e.Timing,
                linea.DayCount, linea.Redondeo());
            var calendario = await lector.CalendarioAsync(inicio, PlanDeCdat.Vencimiento(inicio, e.TermDays), ct);
            var nuevo = PlanDeCdat.Generar(condiciones, calendario);
            var antes = await emision.HechoDeCuentaAsync(cdat, hoy, ct);
            if (antes.IsFailure) return Falla(antes.Error);

            // El aumento se revisa con las reglas del retiro de la cuenta origen y su GMF (traslado entre ahorro y CDAT, D32).
            RetiroRevisado? retiro = null;
            if (origen is not null)
            {
                var hecho = HechosGeneradoresDeGmf.DeTraslado(origen.DepositClass, DepositClass.TermDeposit, origen.HolderPersonId == cdat.HolderPersonId);
                var revisado = await retiros.RevisarAsync(origen, new RetiroPedido(null, null, null, origen.PublicId, delta, hoy, ClaseDeMedioDeRecaudo.Transfer,
                    cambio!.Signers ?? [], null, null, null, PrevalidationOutcome.NotApplicable, [], e.Reason)
                {
                    Clase = MemberAccountOperationKind.Transfer, HechoGenerador = hecho, Prefijo = PrefijoRenovacion,
                }, pedirAprobacion: true, ct);
                if (revisado.IsFailure) return Falla(revisado.Error);
                retiro = revisado.Value;
            }
            GmfCalculado? gmfDisminucion = null;
            if (disminucion?.Cuenta is not null)
            {
                var g = await movimientos.GmfAsync(await movimientos.LeerAsync(cdat, hoy, ct), HechosGeneradoresDeGmf.RedencionDeCdatAbonada, -delta, hoy, ct);
                if (g.IsFailure) return Falla(g.Error);
                gmfDisminucion = g.Value;
            }

            var actor = await actorActual.ObtenerAsync(ct);
            var ahora = reloj.UtcNow;
            var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.TermDepositRenewal, PrefijoRenovacion, ct);
            var huella = new HuellaDeCartera().Identificador("account", cdat.PublicId).Entero("version", v.VersionNumber + 1).Fecha("start", inicio)
                .Decimal("principal", capital, 2).Decimal("rate", condiciones.TasaEfectivaAnual, 6).Entero("termDays", e.TermDays).Calcular();
            var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
            {
                MemberAccountId = cdat.Id,
                CounterpartMemberAccountId = origen?.Id ?? disminucion?.Cuenta?.Id,
                Kind = MemberAccountOperationKind.TermDepositRenewal,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = hoy,
                AccountingDate = antes.Value.FechaContable,
                RegisteredAt = sello.RegisteredAt,
                Amount = capital,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                AuthorizedByUserId = retiro?.Atribucion.AprobadoPor,
                Reason = LectorDeCdat.Recortar(e.Reason, 500),
                Payload = JsonSerializer.Serialize(new { previous = v.VersionNumber, start = inicio, principal = capital, change = delta, termDays = e.TermDays,
                    rate = condiciones.TasaEfectivaAnual }, Json),
                InputsSha256 = huella,
                RulesVersion = VersionesDeReglas.Vigente,
            });
            await db.SaveChangesAsync(ct);

            AccountPayoutOrder? orden = null;
            if (disminucion is { Cuenta: null } giro)
            {
                orden = AccountPayoutOrder.Emitir(cdat.Id, operacion.Id, PayoutPurpose.TermDepositRedemption, giro.Beneficiario!.Id, giro.Beneficiario.IdType ?? string.Empty,
                    giro.Beneficiario.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(giro.Beneficiario), giro.BancoId, giro.TipoDeCuenta, giro.NumeroDeCuenta, -delta,
                    antes.Value.FechaContable.AddDays(1), ahora, actor.UserId);
                await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
                db.AccountPayoutOrders.Add(orden);
                await db.SaveChangesAsync(ct);
            }

            var destinoVersion = nuevoDestino ?? new DestinoDeCdat(
                v.InterestDestination == InterestDestination.PaymentOrder ? TermDepositDestinationKind.PaymentOrder : TermDepositDestinationKind.Account, null, null, null,
                v.DestinationAccountType, v.DestinationBankAccountNumber);
            int? destinoCuenta = v.DestinationAccountId, destinoPersona = v.DestinationPersonId, destinoBanco = v.DestinationBankId;
            if (nuevoDestino is not null)
            {
                var r = await PiezasDeCdat.DestinoAsync(db, nuevoDestino, ct);
                if (r.IsFailure) return Falla(r.Error);
                (destinoCuenta, destinoPersona, destinoBanco) = (r.Value.Cuenta?.Id, r.Value.Beneficiario?.Id, r.Value.BancoId);
            }
            var terminos = LectorDeCdat.NuevaVersion(cdat, v.VersionNumber + 1, operacion, AccountTermsCause.Renewal, linea, nuevo, tabla.Value.Tasa, negociada,
                destinoVersion, destinoCuenta, destinoPersona, destinoBanco, e.MaturityInstruction ?? v.MaturityInstruction ?? MaturityInstruction.Rollover, 0, v.Id, ahora,
                huella);
            registro.SellarCondiciones(control, terminos, LectorDeCdat.FilasDelPlan(terminos, nuevo), v.VersionNumber);
            await db.SaveChangesAsync(ct);

            var despues = await emision.HechoDeCuentaAsync(cdat, hoy, ct);
            if (despues.IsFailure) return Falla(despues.Error);
            OtraCuentaDeCdat? otra = null;
            (GmfAEmitir, HechoDeCuenta)? gmfAEmitir = null;
            HechoDeCuenta? hechoOtra = null;
            var otraCuenta = origen ?? disminucion?.Cuenta;
            if (otraCuenta is not null)
            {
                var h = await emision.HechoDeCuentaAsync(otraCuenta, hoy, ct);
                if (h.IsFailure) return Falla(h.Error);
                hechoOtra = h.Value;
                otra = new OtraCuentaDeCdat(h.Value, [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -delta }]);
                var gmf = origen is not null ? retiro?.Gmf : gmfDisminucion;
                if (GravamenMovimientosFinancieros.SeCobra(gmf))
                    gmfAEmitir = (new GmfAEmitir(gmf, origen is not null && retiro!.Cuenta.ExentaDeGmf ? origen.PublicId : null,
                        origen is not null ? origen.DepositClass?.ToString() : nameof(DepositClass.TermDeposit)), h.Value);
            }
            var valores = delta == 0m ? new List<DepositConceptAmountV1>() : [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = delta }];
            var cambiaElPlazo = v.IssueTermBand != terminos.IssueTermBand;
            var unidad = await emision.CdatRenovadoAsync(new HechoDeCdatAEmitir(despues.Value, operacion, valores, EmisionDeCartera.Terminos(terminos, cdat.Number),
                PrevalidationOutcome.NotApplicable)
            {
                TerminosAnteriores = EmisionDeCartera.Terminos(v, cdat.Number),
                OtraCuenta = otra,
                Orden = orden,
                Beneficiario = orden is null ? null : disminucion?.Payee,
                TipoDeOrden = nameof(PayoutPurpose.TermDepositRedemption),
                Gmf = gmfAEmitir,
                CambioDeMonto = cambio is null ? null : new TermDepositAmountChangeV1 { Kind = cambio.Kind, Amount = cambio.Amount },
                Reclasificacion = cambiaElPlazo
                    ? (antes.Value.Dimensiones(), [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = v.PrincipalAmount }])
                    : null,
                AjusteDeCausacion = faltante,
                CausaDelAjuste = EmisionDeCartera.CausaCorreccionDeTasa,
            }, ct);
            if (unidad.IsFailure) return Falla(unidad.Error);
            var mensaje = unidad.Value.De(CdatRenovadoV1.Type);
            var mGmf = unidad.Value.De(GmfRetenidoV1.Type);
            var mAj = unidad.Value.De(AjusteDeCausacionDeCaptacionV1.Type);
            var contable = despues.Value.FechaContable;
            var filas = new List<MemberAccountTransaction>();
            if (faltante != 0m)
                filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, MemberAccountTransactionKind.AccrualAdjustment, MemberAccountConcept.InterestPayable, faltante, plan.Vencimiento,
                    contable, mAj, v.Id, plan.Periodos[^1].Numero));
            if (delta > 0m && origen is not null)
            {
                filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, MemberAccountTransactionKind.TransferIn, MemberAccountConcept.Principal, delta, hoy, contable, mensaje, terminos.Id));
                filas.Add(PiezasDeCdat.Fila(origen.Id, operacion, MemberAccountTransactionKind.TransferOut, MemberAccountConcept.Principal, -delta, hoy, contable, mensaje));
                if (GravamenMovimientosFinancieros.SeCobra(retiro?.Gmf))
                {
                    filas.Add(PiezasDeCdat.Fila(origen.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -retiro!.Gmf!.Valor, hoy, contable,
                        mGmf));
                    filas.Add(PiezasDeCdat.Fila(origen.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, retiro.Gmf.Valor, hoy, contable, mGmf));
                }
            }
            else if (delta < 0m)
            {
                filas.Add(PiezasDeCdat.Fila(cdat.Id, operacion, disminucion?.Cuenta is null ? MemberAccountTransactionKind.Withdrawal : MemberAccountTransactionKind.TransferOut,
                    MemberAccountConcept.Principal, delta, hoy, contable, mensaje, terminos.Id));
                if (disminucion?.Cuenta is { } dd2)
                {
                    filas.Add(PiezasDeCdat.Fila(dd2.Id, operacion, MemberAccountTransactionKind.TransferIn, MemberAccountConcept.Principal, -delta, hoy, contable, mensaje));
                    if (GravamenMovimientosFinancieros.SeCobra(gmfDisminucion))
                    {
                        filas.Add(PiezasDeCdat.Fila(dd2.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -gmfDisminucion.Valor, hoy,
                            contable, mGmf));
                        filas.Add(PiezasDeCdat.Fila(dd2.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, gmfDisminucion.Valor, hoy, contable,
                            mGmf));
                    }
                }
            }
            if (filas.Count > 0) registro.RegistrarTransacciones(filas);
            var gmfUsado = origen is not null ? retiro?.Gmf : gmfDisminucion;
            if (otraCuenta is not null && gmfUsado is not null)
                registro.RegistrarUsoDeGmf(origen?.HolderPersonId ?? cdat.HolderPersonId, operacion, otraCuenta.Id, hoy, gmfUsado.HechoGenerador, gmfUsado.Base,
                    gmfUsado.ExentoAplicado, gmfUsado.BaseGravada, gmfUsado.Valor, gmfUsado.Politica, gmfUsado.Tarifa);
            if (otraCuenta is not null)
                RegistroDeCuentas.MarcarActividadDelTitular(await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == otraCuenta.Id, ct), hoy);
            RegistroDeCuentas.MarcarActividadDelTitular(control, hoy);
            cdat.MaturityInstructionJson = null;
            await db.SaveChangesAsync(ct);
            _ = hechoOtra;
            return Result.Success(await RespuestasDeCdat.DtoAsync(db, lector, cdat, hoy, ct));
        }, ct);
    }

    private static Result<TermDepositDto> Falla(Error e) => Result.Failure<TermDepositDto>(e);
}
