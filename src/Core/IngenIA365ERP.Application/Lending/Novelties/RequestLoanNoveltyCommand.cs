using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Radica una novedad en borrador (feature 013, K3, T0878; api.md §11.1, <c>POST /api/lending/novelties</c>, <c>Lending.Novelties.Request</c>, con clave;
/// FR-090, FR-092, FR-098): sobre un crédito vigente del alcance —en una refinanciación, también los que recoge, del mismo deudor—, con la <b>causa</b>
/// que calcula el sistema (<see cref="ClasificadorDeNovedad"/>: <c>Lending.Novelty.ModificationNotAllowed</c> si no cabe como modificación), la
/// calificación oficial de hoy como piso (<c>RatingAtRequest</c>, FR-093), el número del numerador y un consentimiento <c>Pending</c> por cada
/// codeudor, avalista y propietario de una garantía de los créditos. Bajo el cerrojo de los créditos; deja la intervención <c>Radicar</c> (quien radica
/// no decide ni aplica). La simulación y el radicado son otros pasos. (nuevo)
/// </summary>
public sealed record RequestLoanNoveltyCommand(
    IReadOnlyList<Guid> LoanPublicIds,
    NoveltyMechanism Mechanism,
    NoveltyRequestMotive RequestMotive,
    NewTermsInput NewTerms,
    string Reason,
    Guid? InsolvencyProceedingPublicId = null) : IRequest<Result<LoanNoveltyDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestLoanNoveltyCommandValidator : AbstractValidator<RequestLoanNoveltyCommand>
{
    public RequestLoanNoveltyCommandValidator()
    {
        RuleFor(x => x.LoanPublicIds).NotEmpty().WithMessage("Elija el crédito de la novedad.");
        RuleForEach(x => x.LoanPublicIds).NotEmpty();
        RuleFor(x => x.Mechanism).IsInEnum();
        RuleFor(x => x.RequestMotive).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se pide la novedad.").MaximumLength(1000);
        RuleFor(x => x.NewTerms).NotNull();
        RuleFor(x => x.NewTerms.Installments).GreaterThan(0).When(x => x.NewTerms?.Installments is not null)
            .WithMessage("Las cuotas del plan nuevo son una o más.");
        RuleFor(x => x.NewTerms.Installments).NotNull().When(x => x.Mechanism is NoveltyMechanism.Extension or NoveltyMechanism.Novation or NoveltyMechanism.Refinancing)
            .WithMessage("Diga en cuántas cuotas queda el crédito (las del plan nuevo desde hoy).");
        RuleFor(x => x.NewTerms.Rate).GreaterThan(0m).PrecisionScale(9, 6, true).When(x => x.NewTerms?.Rate is not null)
            .WithMessage("La tasa nueva es una fracción efectiva anual con hasta seis decimales (0,245 es 24,5 %).");
        RuleFor(x => x.NewTerms.Rate).NotNull().When(x => x.Mechanism == NoveltyMechanism.RateChange)
            .WithMessage("Un cambio de tasa lleva la tasa nueva.");
        RuleFor(x => x.NewTerms.PaymentDay).InclusiveBetween(1, 31).When(x => x.NewTerms?.PaymentDay is not null)
            .WithMessage("El día de pago va del 1 al 31.");
        RuleFor(x => x.NewTerms.BranchPublicId).NotEmpty().When(x => x.Mechanism == NoveltyMechanism.BranchChange)
            .WithMessage("Elija la oficina a la que pasa el crédito.");
        RuleFor(x => x.NewTerms).Must(t => t.PaymentDay is not null || t.FirstDueDate is not null)
            .When(x => x.Mechanism == NoveltyMechanism.PaymentDateChange && x.NewTerms is not null)
            .WithMessage("Un cambio de fecha lleva el día de pago o el primer vencimiento nuevos.");
    }
}

public sealed class RequestLoanNoveltyCommandHandler(
    IApplicationDbContext db,
    CalculoDeNovedad calculo,
    NumeradorDeCartera numerador,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RequestLoanNoveltyCommand, Result<LoanNoveltyDto>>
{
    /// <summary>Los estados en que un crédito recibe novedades.</summary>
    public static readonly LoanStatus[] Vigentes = [LoanStatus.Active];

    public async Task<Result<LoanNoveltyDto>> Handle(RequestLoanNoveltyCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var hoy = reloj.HoyLocal;

        // Los créditos: el primero es el principal; una refinanciación recoge los demás y los de payoffLoans.
        var ids = request.LoanPublicIds.Concat(request.NewTerms.PayoffLoans ?? []).Distinct().ToList();
        if (ids.Count > 1 && request.Mechanism != NoveltyMechanism.Refinancing) return Falla(ErroresDeNovedades.SoloLaRefinanciacionRecoge());
        var creditos = new List<Loan>();
        foreach (var publicId in ids)
        {
            var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == publicId && !l.IsDeleted, ct);
            if (credito is null) return Falla(Settlement.LecturaDelCredito.Inexistente());
            var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, Settlement.LecturaDelCredito.Inexistente(), auditoria, "Loan",
                credito.PublicId, ct);
            if (dentro.IsFailure) return Falla(dentro.Error);
            if (!Vigentes.Contains(credito.Status)) return Falla(ErroresDeNovedades.CreditoNoActivo(credito.Number, credito.Status.ToString()));
            creditos.Add(credito);
        }
        var principal = creditos[0];
        if (creditos.Any(c => c.DebtorPersonId != principal.DebtorPersonId)) return Falla(ErroresDeNovedades.CreditosDeOtroDeudor());

        // El acuerdo de insolvencia: el proceso del deudor, en negociación.
        InsolvencyProceeding? proceso = null;
        if (request.InsolvencyProceedingPublicId is { } procesoPublicId)
        {
            proceso = await db.InsolvencyProceedings.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == procesoPublicId && !p.IsDeleted, ct);
            if (proceso is null) return Falla(ErroresDeNovedades.InsolvenciaInexistente());
            if (proceso.PersonId != principal.DebtorPersonId) return Falla(ErroresDeNovedades.InsolvenciaDeOtroDeudor());
            if (proceso.Status != InsolvencyProceedingStatus.Negotiating)
                return Falla(ErroresDeGobierno.De(ProblemaDeCartera.Con(InsolvencyProceeding.CodigoNoNegociando,
                    "El proceso de insolvencia ya no está en negociación: su acuerdo no se puede tramitar.", "status", proceso.Status.ToString())));
        }

        // La causa (FR-090), con los créditos reconstruidos a hoy. El cambio de oficina no tiene causa: es una modificación (T0883).
        var papeles = creditos.Select((c, i) => (c, i == 0 ? NoveltyLoanRole.Modified : NoveltyLoanRole.Collected)).ToList();
        var cargados = await calculo.CargarAsync(papeles, hoy, ct);
        if (cargados.IsFailure) return Falla(cargados.Error);
        ClasificacionDeNovedad c;
        if (request.Mechanism == NoveltyMechanism.BranchChange)
        {
            var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == request.NewTerms.BranchPublicId && !b.IsDeleted).Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);
            var invalida = ErroresDeNovedades.CondicionesInvalidas("newTerms.branchPublicId", "Elija una oficina distinta de la del crédito.");
            if (oficina is null || oficina == principal.BranchId) return Falla(invalida);
            var enAlcance = await alcance.AsegurarAsync(oficina, null, invalida, auditoria, "Branch", request.NewTerms.BranchPublicId, ct);
            if (enAlcance.IsFailure) return Falla(enAlcance.Error);
            c = CambioDeOficina(hoy);
        }
        else
        {
            var clasificada = await calculo.ClasificarAsync(request.Mechanism, request.RequestMotive, cargados.Value[0], cargados.Value.Skip(1).ToList(),
                proceso is not null, hoy, ct);
            if (clasificada.IsFailure) return Falla(clasificada.Error);
            c = clasificada.Value;
        }
        if (c.Problema is { } problema) return Falla(ErroresDeGobierno.De(problema));

        var calificacion = await calculo.CalificacionOficialAsync(principal.Id, hoy, ct);
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(NovedadesDeCartera.Expediente, Guid.Empty), AccionesDeSegregacion.Radicar,
            hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([principal.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync(creditos.Select(x => x.Id).Order().ToList(), ModoDeEspera.Esperar, ct);

            var numero = await numerador.AsignarAsync(LendingNumberKind.LoanNovelty, string.Empty, NovedadesDeCartera.Prefijo, ct);
            LoanNovelty n;
            try
            {
                n = LoanNovelty.Nueva(numero, principal.Id, request.Mechanism, c.Causa, c.DeterminacionJson, request.Reason, request.NewTerms.ComoJson(),
                    calificacion, usuario, reloj.UtcNow, proceso?.Id);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            db.LoanNovelties.Add(n);
            await db.SaveChangesAsync(ct);

            foreach (var (credito, papel) in papeles)
                db.LoanNoveltyLoans.Add(new LoanNoveltyLoan { LoanNoveltyId = n.Id, LoanId = credito.Id, Role = papel });
            foreach (var consentimiento in await ConsentimientosAsync(n.Id, creditos, principal.DebtorPersonId, hoy, ct))
                db.LoanNoveltyConsents.Add(consentimiento);
            intervenciones.Registrar(verificada.Value with { Expediente = new ExpedienteDeCartera(NovedadesDeCartera.Expediente, n.PublicId) },
                Domain.Lending.Attributions.LendingDecisionSubjects.Novelty, n.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await NovedadesDeCartera.DtoAsync(db, n, ct));
        }, ct);
    }

    /// <summary>
    /// Un consentimiento <c>Pending</c> por cada codeudor y avalista vigente de los créditos y por cada garantía viva de un tercero (su propietario):
    /// quienes respaldan la obligación tienen que aceptar el cambio (FR-092). El deudor no consiente: es quien la pide.
    /// </summary>
    private async Task<IReadOnlyList<LoanNoveltyConsent>> ConsentimientosAsync(int noveltyId, IReadOnlyList<Loan> creditos, int deudor, DateOnly hoy,
        CancellationToken ct)
    {
        var ids = creditos.Select(x => x.Id).ToList();
        var partes = (await db.LoanParties.AsNoTracking().Where(p => ids.Contains(p.LoanId) && !p.IsDeleted).ToListAsync(ct))
            .Where(p => p.VigenteA(hoy) && p.Role is CreditPartyRole.CoDebtor or CreditPartyRole.Guarantor && p.PersonId != deudor)
            .GroupBy(p => p.PersonId).Select(g => g.OrderBy(p => p.Role).First()).ToList();
        var garantias = await db.Collaterals.AsNoTracking()
            .Where(g => g.LoanId != null && ids.Contains(g.LoanId.Value) && !g.IsDeleted && g.OwnerPersonId != deudor
                        && (g.Status == CollateralStatus.Constituted || g.Status == CollateralStatus.Valid))
            .OrderBy(g => g.Id).ToListAsync(ct);
        var salida = partes.Select(p => new LoanNoveltyConsent
        {
            LoanNoveltyId = noveltyId,
            PersonId = p.PersonId,
            PartyRole = p.Role == CreditPartyRole.CoDebtor ? ConsentPartyRole.CoDebtor : ConsentPartyRole.Guarantor,
        }).ToList();
        salida.AddRange(garantias.Select(g => new LoanNoveltyConsent
        {
            LoanNoveltyId = noveltyId,
            PersonId = g.OwnerPersonId,
            CollateralId = g.Id,
            PartyRole = ConsentPartyRole.CollateralOwner,
        }));
        return salida;
    }

    /// <summary>La «causa» de un cambio de oficina (T0883): modificación, sin mirar la mora; no cambia el plan ni la calificación.</summary>
    private static ClasificacionDeNovedad CambioDeOficina(DateOnly hoy)
    {
        var explicacion = new ExplicacionDeCartera().Linea("Cambio de oficina del crédito: no cambia las condiciones ni la calificación; es una modificación.");
        var json = new HuellaDeCartera()
            .Enumeracion<NoveltyCause>("cause", NoveltyCause.Modification)
            .Enumeracion<NoveltyMechanism>("mechanism", NoveltyMechanism.BranchChange)
            .Fecha("requestedOn", hoy)
            .Logico("admitted", true)
            .Texto("explanation", explicacion.Texto)
            .Canonico();
        return new ClasificacionDeNovedad(NoveltyCause.Modification, null, 0, null, explicacion, json);
    }

    private static Result<LoanNoveltyDto> Falla(Error e) => Result.Failure<LoanNoveltyDto>(e);
}
