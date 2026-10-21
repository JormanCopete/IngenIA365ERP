using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Collaterals;

/// <summary>
/// Una garantía real o de fondo (api.md §6.1; data-model §2.8): el dueño (solicitud o crédito), la clase (prenda, hipoteca, fondo de garantías,
/// aportes: el pagaré y el codeudor van por su lado), el constituyente (nulo = el deudor), el valor y el admisible (nulo = el valor), la fecha de
/// constitución (nula = hoy), la vigencia, el avalúo, la póliza, el registro, el grado de la hipoteca y la cobertura y comisión del fondo. (nuevo)
/// </summary>
public sealed record CollateralRegistrationInput
{
    public string OwnerKind { get; init; } = DuenosDeGarantia.Solicitud;
    public Guid OwnerPublicId { get; init; }
    public CollateralKind Kind { get; init; }
    public Guid? ConstituentPersonPublicId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public decimal? AdmissibleValue { get; init; }
    public DateOnly? Date { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public DateOnly? ValuationDate { get; init; }
    public Guid? AppraiserPersonPublicId { get; init; }
    public string? PolicyNumber { get; init; }
    public Guid? PolicyInsurerPersonPublicId { get; init; }
    public DateOnly? PolicyExpiresOn { get; init; }
    public string? RegistrationReference { get; init; }
    public byte? MortgageRank { get; init; }
    public decimal? FundCoverage { get; init; }
    public decimal? FundFee { get; init; }
}

/// <summary>Las reglas de forma de <see cref="CollateralRegistrationInput"/>, compartidas por registrar y actualizar. (nuevo)</summary>
public sealed class CollateralRegistrationInputValidator : AbstractValidator<CollateralRegistrationInput>
{
    public CollateralRegistrationInputValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("Describa la garantía (bien, matrícula, fondo).").MaximumLength(300);
        RuleFor(x => x.Value).GreaterThan(0m).WithMessage("El valor de la garantía es mayor que cero.");
        RuleFor(x => x.AdmissibleValue).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(x => x.Value).When(x => x.AdmissibleValue is not null)
            .WithMessage("El valor admisible va entre cero y el valor de la garantía.");
        RuleFor(x => x.PolicyNumber).MaximumLength(40);
        RuleFor(x => x.RegistrationReference).MaximumLength(60);
        RuleFor(x => x.FundCoverage).InclusiveBetween(0m, 1m).When(x => x.FundCoverage is not null).WithMessage("La cobertura del fondo es una fracción entre 0 y 1.");
        RuleFor(x => x.FundFee).InclusiveBetween(0m, 1m).When(x => x.FundFee is not null).WithMessage("La comisión del fondo es una fracción entre 0 y 1.");
        RuleFor(x => x.ValidUntil).GreaterThanOrEqualTo(x => x.Date).When(x => x.ValidUntil is not null && x.Date is not null)
            .WithMessage("La vigencia termina antes de la constitución.");
    }
}

/// <summary>
/// Registra una garantía (feature 013, US3, T0450; api.md §6.1, <c>POST /api/lending/collaterals</c>, <c>Lending.Guarantees.Manage</c>, con clave;
/// FR-026, FR-043):
/// <list type="number">
/// <item>el dueño en el alcance; un dueño ya decidido (la solicitud aprobada, negada o terminada, o un crédito) no recibe garantías
/// (<c>Lending.Collateral.OwnerLocked</c>);</item>
/// <item>sólo las clases reales que admite la regla de <c>LND_CreditLineCollateralRules</c> que casa por monto y plazo en la versión sellada de la
/// solicitud (<c>Lending.Collateral.KindNotAdmitted</c> con <c>data.admitted[]</c>);</item>
/// <item>un constituyente distinto del deudor necesita su autorización vigente de tratamiento para crédito (<c>Lending.Authorization.Missing</c>);</item>
/// <item>nace <c>Constituted</c> y, con A3, emite <c>GarantiaConstituida</c> (<c>Constituted</c>, + valor) en el mismo guardado.</item>
/// </list>
/// Sus documentos (avalúo, póliza, registro) se suben después como <c>LendingGuaranteeDocument</c> de la garantía. (nuevo)
/// </summary>
public sealed record RegisterCollateralCommand(CollateralRegistrationInput Collateral) : IRequest<Result<CollateralDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterCollateralCommandValidator : AbstractValidator<RegisterCollateralCommand>
{
    public RegisterCollateralCommandValidator()
    {
        RuleFor(x => x.Collateral).NotNull().SetValidator(new CollateralRegistrationInputValidator());
        RuleFor(x => x.Collateral.OwnerKind).Must(DuenosDeGarantia.EsValido).WithMessage("El dueño es Application o Loan.");
        RuleFor(x => x.Collateral.OwnerPublicId).NotEmpty().WithMessage("Indique la solicitud o el crédito que respalda la garantía.");
        RuleFor(x => x.Collateral.Kind).IsInEnum();
    }
}

public sealed class RegisterCollateralCommandHandler(
    IApplicationDbContext db,
    IAutorizacionDeDatos autorizaciones,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterCollateralCommand, Result<CollateralDto>>
{
    public async Task<Result<CollateralDto>> Handle(RegisterCollateralCommand request, CancellationToken ct)
    {
        var e = request.Collateral;
        var leido = await GarantiasDeCartera.DuenoAsync(db, alcance, auditoria, e.OwnerKind, e.OwnerPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var dueno = leido.Value;
        if (dueno.Decidido) return Falla(ErroresDeGarantias.DuenoDecidido(dueno.Clase));

        var admitidas = await ClasesAdmitidasAsync(dueno, ct);
        if (!admitidas.Contains(e.Kind)) return Falla(ErroresDeGarantias.ClaseNoAdmitida(e.Kind, admitidas));

        var deudor = await db.People.AsNoTracking().FirstAsync(p => p.Id == dueno.DebtorPersonId, ct);
        var constituyente = deudor;
        if (e.ConstituentPersonPublicId is { } otro && otro != deudor.PublicId)
        {
            var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == otro && !p.IsDeleted, ct);
            if (persona is null) return Falla(ErroresDeGobierno.PersonaInexistente());
            var autorizacion = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditProcessing, null, ct);
            if (autorizacion is not { Autoriza: true })
                return Falla(ErroresDeOriginacion.SinAutorizacion(persona.PublicId, ReferenciasDeGobierno.Nombre(persona), DataAuthorizationPurpose.CreditProcessing));
            constituyente = persona;
        }
        var avaluador = await PersonaAsync(e.AppraiserPersonPublicId, ct);
        if (avaluador.IsFailure) return Falla(avaluador.Error);
        var aseguradora = await PersonaAsync(e.PolicyInsurerPersonPublicId, ct);
        if (aseguradora.IsFailure) return Falla(aseguradora.Error);

        var hoy = reloj.HoyLocal;
        var garantia = new Collateral
        {
            Kind = e.Kind,
            OwnerPersonId = constituyente.Id,
            ApplicationId = dueno.ApplicationId,
            LoanId = dueno.Clase == DuenosDeGarantia.Credito ? dueno.LoanId : null,
            Status = CollateralStatus.Constituted,
            ConstitutedOn = e.Date ?? hoy,
        };
        Aplicar(garantia, e, avaluador.Value, aseguradora.Value);
        db.Collaterals.Add(garantia);

        var emitida = await GarantiasDeCartera.EmitirAsync(db, emision, fechaContable, hoy, reloj.UtcNow, garantia, dueno,
            EmisionDeCartera.MovimientosDeGarantia.Constituida, garantia.Value, garantia.Value, ct);
        if (emitida.IsFailure) return Falla(emitida.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success((await GarantiasDeCartera.DtosAsync(db, [garantia], ct))[0]);
    }

    /// <summary>Copia los datos editables de la entrada (valor, admisible, vigencia, avalúo, póliza, registro, fondo).</summary>
    internal static void Aplicar(Collateral g, CollateralRegistrationInput e, int? avaluador, int? aseguradora)
    {
        g.Description = e.Description.Trim();
        g.Value = e.Value;
        g.AdmissibleValue = e.AdmissibleValue ?? e.Value;
        g.ValidUntil = e.ValidUntil;
        g.ValuationDate = e.ValuationDate;
        g.AppraiserPersonId = avaluador;
        g.PolicyNumber = string.IsNullOrWhiteSpace(e.PolicyNumber) ? null : e.PolicyNumber.Trim();
        g.PolicyInsurerPersonId = aseguradora;
        g.PolicyExpiresOn = e.PolicyExpiresOn;
        g.RegistrationReference = string.IsNullOrWhiteSpace(e.RegistrationReference) ? null : e.RegistrationReference.Trim();
        g.MortgageRank = e.MortgageRank;
        g.FundCoverage = e.FundCoverage;
        g.FundFee = e.FundFee;
    }

    /// <summary>
    /// Las clases reales que admite la regla de garantías que casa con la solicitud (la versión sellada; monto aprobado o pedido y plazo; la primera
    /// por <c>Order</c>, FR-026): las de <c>AdmittedKinds</c> que guarda esta tabla, más aportes si la regla los cuenta como garantía.
    /// </summary>
    private async Task<IReadOnlyCollection<CollateralKind>> ClasesAdmitidasAsync(DuenoDeGarantia dueno, CancellationToken ct)
    {
        if (dueno.CreditLineVersionId is not { } version) return [];
        var reglas = await db.CreditLineCollateralRules.AsNoTracking().Where(r => r.CreditLineVersionId == version && !r.IsDeleted).ToListAsync(ct);
        var plazo = dueno.PlazoEnMeses ?? 0;
        var regla = reglas.OrderBy(r => r.Order).FirstOrDefault(r =>
            (r.MinAmount is not { } mn || dueno.Monto >= mn) && (r.MaxAmount is not { } mx || dueno.Monto <= mx)
            && (r.MinTermMonths is not { } pmn || plazo >= pmn) && (r.MaxTermMonths is not { } pmx || plazo <= pmx));
        if (regla is null) return [];
        var admitidas = ListaDeEnums.Leer<CollateralKind>(regla.AdmittedKinds).Where(GarantiasDeCartera.Reales.Contains).ToHashSet();
        if (regla.ContributionsCountAsCollateral) admitidas.Add(CollateralKind.Contributions);
        return admitidas.Order().ToList();
    }

    private async Task<Result<int?>> PersonaAsync(Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<int?>(null);
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == id && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return persona is null ? Result.Failure<int?>(ErroresDeGobierno.PersonaInexistente()) : Result.Success(persona);
    }

    private static Result<CollateralDto> Falla(Error e) => Result.Failure<CollateralDto>(e);
}
