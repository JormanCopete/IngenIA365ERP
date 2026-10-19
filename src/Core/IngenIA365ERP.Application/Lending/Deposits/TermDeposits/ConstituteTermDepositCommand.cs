using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Application.Common.Audit;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>
/// Simula un CDAT (api.md §5, <c>POST /api/lending/term-deposits/simulate</c>, <c>Lending.TermDeposits.Constitute</c>, sin clave): la tasa de la tabla, la
/// negociación si se pide un margen, el plan con sus fechas hábiles, la retención estimada, el vencimiento, el plazo de emisión y la huella que la constitución
/// exige (<c>expectedPreviewSha256</c>). No escribe. (nuevo)
/// </summary>
public sealed record SimulateTermDepositQuery(
    Guid LinePublicId,
    decimal Amount,
    int TermDays,
    RepaymentPeriodicity InterestPeriodicity,
    InterestTiming Timing,
    DateOnly? StartDate = null,
    decimal? RequestedMarginEa = null,
    Guid? HolderPersonPublicId = null,
    Guid? OfficePublicId = null,
    string? Channel = null) : IRequest<Result<TermDepositSimulationDto>>;

public sealed class SimulateTermDepositQueryValidator : AbstractValidator<SimulateTermDepositQuery>
{
    public SimulateTermDepositQueryValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).PrecisionScale(18, 2, true);
        RuleFor(x => x.TermDays).GreaterThan(0);
        RuleFor(x => x.InterestPeriodicity).IsInEnum();
        RuleFor(x => x.Timing).IsInEnum();
        RuleFor(x => x.RequestedMarginEa).Must(m => m is null || EquivalenciaDeTasas.EsTasaPactable(m.Value)).WithMessage("El margen lleva a lo sumo seis decimales.");
    }
}

public sealed class SimulateTermDepositQueryHandler(IApplicationDbContext db, CotizacionDeCdat cotizacion, IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj) : IRequestHandler<SimulateTermDepositQuery, Result<TermDepositSimulationDto>>
{
    public async Task<Result<TermDepositSimulationDto>> Handle(SimulateTermDepositQuery request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K9) return Result.Failure<TermDepositSimulationDto>(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<TermDepositSimulationDto>(linea.Error);
        var titular = request.HolderPersonPublicId is { } t
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == t && !p.IsDeleted, ct)
            : null;
        var oficina = request.OfficePublicId is { } o
            ? await db.Branches.AsNoTracking().Where(b => b.PublicId == o && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct)
            : null;
        var cotizado = await cotizacion.CotizarAsync(linea.Value, new PedidoDeCotizacion(request.Amount, request.TermDays, request.InterestPeriodicity, request.Timing,
            request.StartDate ?? reloj.HoyLocal, request.RequestedMarginEa, titular, oficina, request.Channel), ct);
        return cotizado.IsFailure ? Result.Failure<TermDepositSimulationDto>(cotizado.Error) : Result.Success(cotizado.Value.Vista);
    }
}

/// <summary>
/// Constituye un CDAT (feature 014, US6, T170; api.md §5, <c>POST /api/lending/term-deposits</c>, <c>Lending.TermDeposits.Constitute</c>, con clave; FR-067,
/// FR-127; research E135): con la entrega K9 desplegada, la línea de CDAT en alcance y la cotización (plazo, periodicidad, modalidad, monto, tasa y margen) que
/// coincide con la vista previa si se da su huella, el destino de los intereses (una cuenta de ahorro viva del titular, o un giro a un beneficiario) y el
/// fondeo (por recaudador, o desde una cuenta de ahorro del asociado), abre la cuenta con la apertura común (titular, partes, beneficiarios, listas, número) y
/// guarda la propuesta. Con margen negociado que cabe en la atribución de quien lo pide se sella sola; si no, nace la solicitud <c>PendingDecision</c> y el CDAT
/// la espera. Con fondeo por traslado y nada que esperar, se fondea en la misma transacción y nace activo. (nuevo)
/// </summary>
public sealed record ConstituteTermDepositCommand(ConstituteTermDepositInput Input) : IRequest<Result<TermDepositDto>>, IOperacionIdempotente, IConOficina
{
    public Guid OperationKey { get; init; }

    public Guid? BranchPublicId => Input?.OfficePublicId;
}

public sealed class ConstituteTermDepositCommandValidator : AbstractValidator<ConstituteTermDepositCommand>
{
    public ConstituteTermDepositCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.LinePublicId).NotEmpty().WithMessage("Elija la línea.");
            RuleFor(x => x.Input.HolderPersonPublicId).NotEmpty().WithMessage("Elija el titular.");
            RuleFor(x => x.Input.OfficePublicId).NotEmpty().WithMessage("Elija la oficina.");
            RuleFor(x => x.Input.Amount).GreaterThan(0m).PrecisionScale(18, 2, true);
            RuleFor(x => x.Input.TermDays).GreaterThan(0);
            RuleFor(x => x.Input.InterestPeriodicity).IsInEnum();
            RuleFor(x => x.Input.Timing).IsInEnum();
            RuleFor(x => x.Input.MaturityInstruction).IsInEnum();
            RuleFor(x => x.Input.HandlingMode).IsInEnum();
            RuleFor(x => x.Input.Parties).NotNull();
            RuleForEach(x => x.Input.Parties).SetValidator(new ValidadorDeParte());
            RuleFor(x => x.Input.Beneficiaries).NotNull();
            RuleFor(x => x.Input.PaymentDestination).NotNull();
            RuleFor(x => x.Input.PaymentDestination.Kind).IsInEnum().When(x => x.Input.PaymentDestination is not null);
            RuleFor(x => x.Input.PaymentDestination.AccountPublicId).NotEmpty().WithMessage("Elija la cuenta de ahorro que recibe los intereses.")
                .When(x => x.Input.PaymentDestination is { Kind: TermDepositDestinationKind.Account });
            RuleFor(x => x.Input.PaymentDestination.PayeePersonPublicId).NotEmpty().WithMessage("Elija quién recibe el giro de los intereses.")
                .When(x => x.Input.PaymentDestination is { Kind: TermDepositDestinationKind.PaymentOrder });
            RuleFor(x => x.Input.Funding).NotNull();
            RuleFor(x => x.Input.Funding.Kind).IsInEnum().When(x => x.Input.Funding is not null);
            RuleFor(x => x.Input.Funding.SourceAccountPublicId).NotEmpty().WithMessage("Elija la cuenta de ahorro que fondea el CDAT.")
                .When(x => x.Input.Funding is { Kind: TermDepositFundingKind.TransferFromAccount });
            RuleFor(x => x.Input.Negotiation!.RequestedMarginEa).GreaterThan(0m).Must(EquivalenciaDeTasas.EsTasaPactable)
                .WithMessage("El margen es mayor que cero, con a lo sumo seis decimales.").When(x => x.Input.Negotiation is not null);
            RuleFor(x => x.Input.Negotiation!.Reason).NotEmpty().MaximumLength(NegotiatedRateRequest.LargoDelMotivo).When(x => x.Input.Negotiation is not null);
            RuleFor(x => x.Input.Channel).MaximumLength(Domain.Entities.Lending.Deposits.Configuration.NegotiatedRateRule.LargoDelCanal);
        });
    }
}

public sealed class ConstituteTermDepositCommandHandler(
    IApplicationDbContext db,
    IContextoDeLineasDeCaptacion contexto,
    AperturaDeCuentas apertura,
    CotizacionDeCdat cotizacion,
    ConstitucionDeCdat constitucion,
    LectorDeCdat lector,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ConstituteTermDepositCommand, Result<TermDepositDto>>
{
    public async Task<Result<TermDepositDto>> Handle(ConstituteTermDepositCommand request, CancellationToken ct)
    {
        var e = request.Input;
        var hoy = reloj.HoyLocal;
        if (contexto.Entrega < EntregaDeCartera.K9) return Falla(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        if (e.StartDate is { } inicio && inicio != hoy) return Falla(ErroresDeCdat.FechaDeInicio(hoy));
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, e.LinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Falla(linea.Error);
        var titular = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == e.HolderPersonPublicId && !p.IsDeleted, ct);
        if (titular is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == e.OfficePublicId && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null) return Falla(Error.NotFound);

        var cotizado = await cotizacion.CotizarAsync(linea.Value, new PedidoDeCotizacion(e.Amount, e.TermDays, e.InterestPeriodicity, e.Timing, hoy,
            e.Negotiation?.RequestedMarginEa, titular, oficina, e.Channel), ct);
        if (cotizado.IsFailure) return Falla(cotizado.Error);
        var c = cotizado.Value;
        if (c.Negociacion is { Motor.CabeEnLaRegla: false } rechazada) return Falla(NegociacionDeTasa.Rechazo(rechazada.Motor));
        if (e.ExpectedPreviewSha256 is { Length: > 0 } esperada && !string.Equals(esperada, c.Vista.Fingerprint, StringComparison.Ordinal))
            return Falla(ErroresDeCdat.VistaPreviaCambio(c.Vista));

        // El destino de los intereses y el origen del fondeo: cuentas de ahorro vivas del titular, en el alcance de quien opera (E123).
        var destino = await DestinoAsync(e.PaymentDestination, titular, ct);
        if (destino.IsFailure) return Falla(destino.Error);
        MemberAccount? origen = null;
        if (e.Funding.Kind == TermDepositFundingKind.TransferFromAccount)
        {
            var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, e.Funding.SourceAccountPublicId!.Value, seguir: true, ct);
            if (leida.IsFailure) return Falla(leida.Error);
            origen = leida.Value;
            if (origen.Nature != LineNature.Deposit || origen.DepositClass is not (DepositClass.Demand or DepositClass.Permanent) || !origen.Viva)
                return Falla(ErroresDeCdat.FondeoInvalido("El CDAT se fondea desde una cuenta de ahorro viva del asociado."));
        }

        var negociacion = c.Negociacion;
        var cabe = negociacion is { CabeEnAtribucion: true };
        var abierta = await apertura.AbrirAsync(new OpenAccountInput
        {
            LinePublicId = e.LinePublicId, HolderPersonPublicId = e.HolderPersonPublicId, OfficePublicId = e.OfficePublicId, HandlingMode = e.HandlingMode,
            Parties = e.Parties, Beneficiaries = e.Beneficiaries, BeneficiaryRecordAttachmentPublicId = e.BeneficiaryRecordAttachmentPublicId, OpeningDate = hoy,
        }, paraCdat: true, async cuenta =>
        {
            var usuario = cuenta.OpenedByUserId;
            NegociacionDeCdat? sellada = null;
            if (negociacion is { Regla: { } regla } && cabe)
                sellada = new NegociacionDeCdat(e.Negotiation!.RequestedMarginEa, regla.Id, regla.PublicId, usuario, e.Negotiation.Reason, null);
            var propuesta = new PropuestaDeCdat(e.Amount, e.TermDays, e.InterestPeriodicity, e.Timing, hoy, c.Version.Id, c.TasaDeTabla, c.TramoId, destino.Value,
                e.MaturityInstruction, e.Funding.Kind, origen?.PublicId, sellada, e.Channel, reloj.UtcNow);
            cuenta.PendingTermsJson = propuesta.Serializar();
            if (negociacion is { Regla: { } pendiente } && !cabe)
            {
                var solicitud = new NegotiatedRateRequest
                {
                    MemberAccountId = cuenta.Id, NegotiatedRateRuleId = pendiente.Id, TableAnnualRate = c.TasaDeTabla, RequestedSpread = e.Negotiation!.RequestedMarginEa,
                    MaxSpread = pendiente.MaxSpread, Amount = e.Amount, TermDays = e.TermDays, Channel = e.Channel,
                    Reason = NegotiatedRateRequest.Recortar(e.Negotiation.Reason), RequestedByUserId = usuario ?? 0, RequestedAt = reloj.UtcNow,
                };
                var radicada = await intervenciones.VerificarAsync(SolicitudesDeTasaNegociada.ExpedienteDe(solicitud.PublicId), AccionesDeSegregacion.Radicar, hoy, ct);
                if (radicada.IsFailure) return Result.Failure(radicada.Error);
                solicitud.RequestedByUserId = radicada.Value.UsuarioId;
                db.NegotiatedRateRequests.Add(solicitud);
                intervenciones.Registrar(radicada.Value, SolicitudesDeTasaNegociada.Expediente, solicitud.PublicId);
                await db.SaveChangesAsync(ct);
                return Result.Success();
            }
            await db.SaveChangesAsync(ct);
            if (origen is null || (await LectorDeCuentas.CoincidenciasSinLiberarAsync(db, [cuenta.HolderPersonId], ct)).Count > 0) return Result.Success();
            var fondeado = await constitucion.FondearAsync(cuenta, new FondeoDeCdat(TermDepositFundingKind.TransferFromAccount, hoy, PrevalidationOutcome.NotApplicable,
                "Fondeo del CDAT al constituirlo")
            {
                Origen = origen, Firmantes = e.Funding.Signers ?? [], AprobacionPublicId = e.Funding.ApprovalPublicId,
            }, ct);
            return fondeado.IsFailure ? Result.Failure(fondeado.Error) : Result.Success();
        }, ct);
        if (abierta.IsFailure) return Falla(abierta.Error);
        var cuentaCreada = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == abierta.Value.Cuenta.Id, ct);
        return Result.Success(await RespuestasDeCdat.DtoAsync(db, lector, cuentaCreada, hoy, ct));
    }

    /// <summary>
    /// El destino resuelto: una cuenta de ahorro a la vista viva del titular en el alcance, o el beneficiario del giro (persona viva; el banco, si se da, del
    /// catálogo).
    /// </summary>
    private async Task<Result<DestinoDeCdat>> DestinoAsync(TermDepositDestinationInput d, Person titular, CancellationToken ct)
    {
        if (d.Kind == TermDepositDestinationKind.Account)
        {
            var cuenta = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, d.AccountPublicId!.Value, seguir: false, ct);
            if (cuenta.IsFailure) return Result.Failure<DestinoDeCdat>(cuenta.Error);
            if (cuenta.Value.Nature != LineNature.Deposit || cuenta.Value.DepositClass != DepositClass.Demand || !cuenta.Value.Viva
                || cuenta.Value.HolderPersonId != titular.Id)
                return Result.Failure<DestinoDeCdat>(ErroresDeCdat.DestinoInvalido("Los intereses del CDAT se abonan en una cuenta de ahorro a la vista viva del titular."));
            return Result.Success(new DestinoDeCdat(TermDepositDestinationKind.Account, cuenta.Value.PublicId, null, null, null, null));
        }
        var persona = await db.People.AsNoTracking().AnyAsync(p => p.PublicId == d.PayeePersonPublicId && !p.IsDeleted, ct);
        if (!persona) return Result.Failure<DestinoDeCdat>(ErroresDeGobierno.PersonaInexistente());
        if (d.BankPublicId is { } banco && !await db.Banks.AsNoTracking().AnyAsync(b => b.PublicId == banco && !b.IsDeleted, ct))
            return Result.Failure<DestinoDeCdat>(ErroresDeCdat.DestinoInvalido("El banco del giro no existe."));
        return Result.Success(new DestinoDeCdat(TermDepositDestinationKind.PaymentOrder, null, d.PayeePersonPublicId, d.BankPublicId, d.AccountType,
            string.IsNullOrWhiteSpace(d.AccountNumber) ? null : d.AccountNumber.Trim()));
    }

    private static Result<TermDepositDto> Falla(Error e) => Result.Failure<TermDepositDto>(e);
}

/// <summary>
/// Fondea por traslado un CDAT que espera (feature 014, US6, T170; <c>POST /api/lending/term-deposits/{id}/fund</c>, <c>Lending.TermDeposits.Constitute</c>,
/// con clave; nueva en research E135): el CDAT en apertura sin solicitud de tasa en espera, desde una cuenta de ahorro del asociado en el alcance. El fondeo
/// por recaudador llega por el contrato de cuentas. (nuevo)
/// </summary>
public sealed record FundTermDepositCommand(Guid AccountPublicId, Guid SourceAccountPublicId, IReadOnlyList<Guid> Signers, Guid? ApprovalPublicId, string? Reason)
    : IRequest<Result<TermDepositDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class FundTermDepositCommandValidator : AbstractValidator<FundTermDepositCommand>
{
    public FundTermDepositCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.SourceAccountPublicId).NotEmpty();
        RuleFor(x => x.Signers).NotNull();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class FundTermDepositCommandHandler(IApplicationDbContext db, IContextoDeLineasDeCaptacion contexto, ConstitucionDeCdat constitucion, LectorDeCdat lector,
    ICerrojoDeCartera cerrojo, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj) : IRequestHandler<FundTermDepositCommand, Result<TermDepositDto>>
{
    public async Task<Result<TermDepositDto>> Handle(FundTermDepositCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K9) return Result.Failure<TermDepositDto>(ErroresDeCdat.EntregaNoDisponible("El CDAT"));
        var cdat = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (cdat.IsFailure) return Result.Failure<TermDepositDto>(cdat.Error);
        var origen = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.SourceAccountPublicId, seguir: true, ct);
        if (origen.IsFailure) return Result.Failure<TermDepositDto>(origen.Error);
        var hoy = reloj.HoyLocal;
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cdat.Value.HolderPersonId], ModoDeEspera.Esperar, ct);
            var r = await constitucion.FondearAsync(cdat.Value, new FondeoDeCdat(TermDepositFundingKind.TransferFromAccount, hoy, PrevalidationOutcome.NotApplicable,
                request.Reason)
            {
                Origen = origen.Value, Firmantes = request.Signers, AprobacionPublicId = request.ApprovalPublicId,
            }, ct);
            if (r.IsFailure) return Result.Failure<TermDepositDto>(r.Error);
            return Result.Success(await RespuestasDeCdat.DtoAsync(db, lector, cdat.Value, hoy, ct));
        }, ct);
    }
}
