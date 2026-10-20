using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

// Feature 014, US1 (T065; api.md §3; data-model §1.1, §2.5; FR-007, FR-014, FR-029, FR-040–FR-049): crear la línea de aportes con su primera versión
// y registrar versiones. Las reglas son las de ReglasDeLineaDeCaptacion (dominio) con los topes legales que lee IContextoDeLineasDeCaptacion; el único
// que registra una versión es AltaDeVersionDeCaptacion. Una sola línea de aportes activa por cooperativa (UK_LND_CreditLines_ActiveContribution). (nuevo)

/// <summary>La forma de una versión de aportes (Principio VIII). Las reglas de negocio las decide el dominio.</summary>
public sealed class ContributionLineVersionInputValidator : AbstractValidator<ContributionLineVersionInput>
{
    public ContributionLineVersionInputValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige la versión.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indicá el motivo de la versión.").MaximumLength(300);
        RuleFor(x => x.MinimumNonReducible).NotNull().WithMessage("Indicá el mínimo no reducible por asociado.");
        RuleFor(x => x.Installment).NotNull().WithMessage("Indicá la cuota periódica.");
        RuleFor(x => x.PermanentSavingsShare!.DepositLinePublicId).NotEmpty().WithMessage("Elegí la línea de ahorro permanente.")
            .When(x => x.PermanentSavingsShare is not null);
    }
}

/// <summary>La línea de ahorro permanente que recibe su parte de la cuota: viva, de captación y de clase permanente. (nuevo)</summary>
internal static class ReferenciasDeLineasDeAportes
{
    public static async Task<Result<int?>> LineaPermanenteAsync(IApplicationDbContext db, ContributionLineVersionInput input, CancellationToken ct)
    {
        if (input.PermanentSavingsShare?.DepositLinePublicId is not { } publicId) return Result.Success<int?>(null);
        var linea = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == publicId && !l.IsDeleted, ct);
        if (linea is null || linea.Nature != LineNature.Deposit || await LectorDeLineasDeCaptacion.ClaseDeAsync(db, linea.Id, ct) != DepositClass.Permanent)
            return Result.Failure<int?>(ErroresDeLineasDeCaptacion.LineaReferidaInvalida("permanentSavingsShare.depositLinePublicId",
                "El reparto de la cuota va a una línea de ahorro permanente viva."));
        return Result.Success<int?>(linea.Id);
    }

    /// <summary>
    /// Las reglas de la versión que dependen de lo de fuera: los topes de un fondo de empleados (US1-3) y el límite legal de participación (FR-043),
    /// leídos a la fecha desde la que rige.
    /// </summary>
    public static async Task<Result> TopesAsync(IContextoDeLineasDeCaptacion contexto, BorradorDeVersionDeAportes b, CancellationToken ct)
    {
        var perfil = await contexto.PerfilAsync(b.ValidFrom, ct);
        var topes = await contexto.TopesDeAportesAsync(b.ValidFrom, perfil.EsFondoDeEmpleados, ct);
        if (topes.IsFailure) return Result.Failure(topes.Error);
        var t = topes.Value;
        if (ReglasDeLineaDeCaptacion.ValidarTopesDeFondoDeEmpleados(b, t.TopeDeCuota, t.FuenteDelTope, t.FraccionMinima, t.FuenteDeLaFraccion) is { } fondo)
            return Result.Failure(ErroresDeLineasDeCaptacion.De(fondo));
        if (ReglasDeLineaDeCaptacion.ValidarLimiteDeParticipacion(b, t.LimiteDeParticipacion, t.ClaveDelLimite, t.FuenteDelLimite) is { } limite)
            return Result.Failure(ErroresDeLineasDeCaptacion.De(limite));
        return Result.Success();
    }

    /// <summary>Otra línea de aportes activa (o nula): el asociado tiene una sola cuenta de aportes.</summary>
    public static Task<CreditLine?> OtraActivaAsync(IApplicationDbContext db, int? excepto, CancellationToken ct) =>
        db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => !l.IsDeleted && l.Nature == LineNature.Contribution && l.Status == CreditLineStatus.Active
            && l.Id != (excepto ?? 0), ct);
}

// ------------------------------------------------------------------------------------------------------------------- crear --

/// <summary>
/// Crea la línea de aportes con su primera versión (api.md §3, <c>POST /api/lending/lines/contribution</c>, <c>Lending.ContributionLines.Manage</c>).
/// Con <see cref="Active"/> nace activa (el perfil no la limita: toda cooperativa capta aportes) si el despliegue trae K7 y no hay otra activa. (nuevo)
/// </summary>
public sealed record CreateContributionLineCommand(string Code, string Name, bool Active, ContributionLineVersionInput FirstVersion)
    : IRequest<Result<LendingLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => FirstVersion?.Reason ?? string.Empty;
}

public sealed class CreateContributionLineCommandValidator : AbstractValidator<CreateContributionLineCommand>
{
    public CreateContributionLineCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indicá el código de la línea.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage("El código admite hasta 10 caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indicá el nombre de la línea.").MaximumLength(120);
        RuleFor(x => x.FirstVersion).NotNull().WithMessage("La línea nace con su primera versión.")
            .SetValidator(new ContributionLineVersionInputValidator()!);
    }
}

/// <summary>
/// Código vivo repetido → <c>Catalogo.CodigoDuplicado</c> con el nombre del existente; versión mal formada → <c>Lending.ContributionLine.Invalid</c>;
/// retroactiva → <c>.Retroactive</c>; en un fondo de empleados, cuota sobre el tope → <c>Lending.Contributions.InstallmentCapExceeded</c> y parte de
/// aportes bajo la mínima → <c>Lending.Contributions.FractionBelowLegal</c>, con el tope y su fuente; límite de participación menos estricto que el
/// legal → <c>Lending.Parameter.BelowLegalFloor</c>; activa con otra activa → <c>Lending.Contributions.LineAlreadyActive</c>; activa sin K7 →
/// <c>Lending.Savings.DeliveryNotAvailable</c>. Si algo falla no se guarda nada.
/// </summary>
public sealed class CreateContributionLineCommandHandler(
    IApplicationDbContext db,
    AltaDeVersionDeCaptacion alta,
    IContextoDeLineasDeCaptacion contexto,
    IDateTimeService reloj) : IRequestHandler<CreateContributionLineCommand, Result<LendingLineDto>>
{
    public async Task<Result<LendingLineDto>> Handle(CreateContributionLineCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetida = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.Code == codigo && !l.IsDeleted, ct);
        if (repetida is not null)
            return Result.Failure<LendingLineDto>(CodigoDeCatalogo.Duplicado("una línea de la Cartera", codigo, repetida.Name, repetida.PublicId));

        var permanente = await ReferenciasDeLineasDeAportes.LineaPermanenteAsync(db, request.FirstVersion, ct);
        if (permanente.IsFailure) return Result.Failure<LendingLineDto>(permanente.Error);
        var hoy = reloj.HoyLocal;
        var borrador = request.FirstVersion.Borrador(permanente.Value);
        if (ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(borrador,
                await Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Contribution, hoy, ct)) is { } problema)
            return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(problema));
        var topes = await ReferenciasDeLineasDeAportes.TopesAsync(contexto, borrador, ct);
        if (topes.IsFailure) return Result.Failure<LendingLineDto>(topes.Error);

        if (request.Active)
        {
            if (EntregaDeLineas.NoDisponible(LineNature.Contribution, contexto.Entrega) is { } sinEntrega) return Result.Failure<LendingLineDto>(sinEntrega);
            if (await ReferenciasDeLineasDeAportes.OtraActivaAsync(db, null, ct) is { } otra)
                return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.AportesYaActiva(otra.Code, otra.Name));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = CreditLine.Nueva(codigo, request.Name, null, LineNature.Contribution);
            db.CreditLines.Add(linea);
            await db.SaveChangesAsync(ct);
            await alta.RegistrarAportesAsync(linea, borrador, ct);
            if (request.Active) linea.Activar();
            await db.SaveChangesAsync(ct);
            return Result.Success(await LectorDeLineasDeCaptacion.DtoAsync(db, linea, hoy > borrador.ValidFrom ? hoy : borrador.ValidFrom, null, ct));
        }, ct);
    }
}

// -------------------------------------------------------------------------------------------------------------- versionar --

/// <summary>
/// Registra una versión nueva de la línea de aportes (api.md §3, <c>POST /{id}/contribution-versions</c>): la anterior rige hasta la víspera; lo
/// exigido, pagado o cerrado no cambia (FR-007). (nuevo)
/// </summary>
public sealed record AddContributionLineVersionCommand(Guid LinePublicId, ContributionLineVersionInput Version)
    : IRequest<Result<ContributionLineVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => Version?.Reason ?? string.Empty;
}

public sealed class AddContributionLineVersionCommandValidator : AbstractValidator<AddContributionLineVersionCommand>
{
    public AddContributionLineVersionCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Version).NotNull().SetValidator(new ContributionLineVersionInputValidator()!);
    }
}

/// <summary>
/// Línea inexistente o fuera del alcance → 404; de otra naturaleza → <c>Lending.CreditLine.NatureMismatch</c>; cerrada →
/// <c>Lending.CreditLine.Closed</c>; empieza antes de la última → <c>Lending.ContributionLine.VersionOverlaps</c>; las demás, las del alta.
/// </summary>
public sealed class AddContributionLineVersionCommandHandler(
    IApplicationDbContext db,
    AltaDeVersionDeCaptacion alta,
    IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<AddContributionLineVersionCommand, Result<ContributionLineVersionDto>>
{
    public async Task<Result<ContributionLineVersionDto>> Handle(AddContributionLineVersionCommand request, CancellationToken ct)
    {
        var encontrada = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<ContributionLineVersionDto>(encontrada.Error);
        var linea = encontrada.Value;
        if (linea.Nature == LineNature.Placement) return Result.Failure<ContributionLineVersionDto>(ErroresDeLineasDeCaptacion.UsarRutaDeCredito(linea.Code));
        if (linea.ValidarNaturaleza(LineNature.Contribution) is { } distinta) return Result.Failure<ContributionLineVersionDto>(ErroresDeLineasDeCaptacion.De(distinta));
        if (linea.Status == CreditLineStatus.Closed) return Result.Failure<ContributionLineVersionDto>(ErroresDeLineasDeCaptacion.De(linea.ValidarActivacion()!));

        var permanente = await ReferenciasDeLineasDeAportes.LineaPermanenteAsync(db, request.Version, ct);
        if (permanente.IsFailure) return Result.Failure<ContributionLineVersionDto>(permanente.Error);
        var borrador = request.Version.Borrador(permanente.Value);
        if (ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(borrador,
                await Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Contribution, reloj.HoyLocal, ct)) is { } problema)
            return Result.Failure<ContributionLineVersionDto>(ErroresDeLineasDeCaptacion.De(problema));
        var contraLoRegistrado = await alta.ValidarContraLoRegistradoAsync(linea, borrador.ValidFrom, ct);
        if (contraLoRegistrado.IsFailure) return Result.Failure<ContributionLineVersionDto>(contraLoRegistrado.Error);
        var topes = await ReferenciasDeLineasDeAportes.TopesAsync(contexto, borrador, ct);
        if (topes.IsFailure) return Result.Failure<ContributionLineVersionDto>(topes.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var seguida = await db.CreditLines.FirstAsync(l => l.Id == linea.Id, ct);
            var version = await alta.RegistrarAportesAsync(seguida, borrador, ct);
            return Result.Success(await LectorDeLineasDeCaptacion.DtoDeAportesAsync(db, version, ct));
        }, ct);
    }
}
