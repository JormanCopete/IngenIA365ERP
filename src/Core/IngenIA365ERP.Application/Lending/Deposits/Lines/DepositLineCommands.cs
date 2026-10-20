using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Execution;
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

// Feature 014, US1 (T065, T067; api.md §3; data-model §1.1, §2.1, §2.2; FR-007, FR-018, FR-027, FR-028, FR-038): crear una línea de captación con su
// primera versión y registrar versiones. Todo pasa por ReglasDeLineaDeCaptacion (dominio) y por AltaDeVersionDeCaptacion (el único que registra una
// versión con sus tramos). Las captaciones nunca consultan la usura. (nuevo)

/// <summary>La forma de una versión de captación (Principio VIII): lo que no necesita la base. Las reglas de negocio las decide el dominio.</summary>
public sealed class DepositLineVersionInputValidator : AbstractValidator<DepositLineVersionInput>
{
    public DepositLineVersionInputValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige la versión.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indicá el motivo de la versión.").MaximumLength(300);
        RuleFor(x => x.Rate).NotNull().WithMessage("Indicá la tasa de la línea.");
        RuleFor(x => x.Minimums).NotNull();
        RuleFor(x => x.Rounding).NotNull();
        RuleFor(x => x.Tax).NotNull();
        RuleFor(x => x.Limits).NotNull();
        RuleFor(x => x.Inactivity).NotNull();
        RuleFor(x => x.DepositClass).IsInEnum().When(x => x.DepositClass is not null);
        RuleFor(x => x.Tax.WithholdingRateParameterCode).MaximumLength(60).When(x => x.Tax is not null);
        RuleFor(x => x.Rate.Tiers).Must(t => t.Count <= 50).WithMessage("Una versión admite hasta 50 tramos.").When(x => x.Rate is not null);
    }
}

/// <summary>Las líneas que una versión de captación nombra por PublicId, resueltas a su Id interno. (nuevo)</summary>
internal static class ReferenciasDeLineasDeCaptacion
{
    /// <summary>La línea a la vista destino de un CDAT que pasa a la vista al vencer: viva, de captación y de clase a la vista.</summary>
    public static async Task<Result<int?>> LineaALaVistaAsync(IApplicationDbContext db, DepositLineVersionInput input, CancellationToken ct)
    {
        if (input.TermDeposit?.DemandLinePublicId is not { } publicId) return Result.Success<int?>(null);
        var linea = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == publicId && !l.IsDeleted, ct);
        if (linea is null || linea.Nature != LineNature.Deposit || await LectorDeLineasDeCaptacion.ClaseDeAsync(db, linea.Id, ct) != DepositClass.Demand)
            return Result.Failure<int?>(ErroresDeLineasDeCaptacion.LineaReferidaInvalida("termDeposit.demandLinePublicId",
                "La línea destino de un CDAT que pasa a la vista al vencer tiene que ser una línea de ahorro a la vista viva."));
        return Result.Success<int?>(linea.Id);
    }
}

// ------------------------------------------------------------------------------------------------------------------- crear --

/// <summary>
/// Crea una línea de captación con su primera versión (api.md §3, <c>POST /api/lending/lines/deposit</c>, <c>Lending.DepositLines.Manage</c>). El código
/// es la nomenclatura de la cooperativa (<see cref="CodigoDeCatalogo"/>), único en todo el catálogo de la Cartera (US1-4), y no cambia. Con
/// <see cref="Active"/> nace activa si el perfil regulatorio vigente autoriza captar (US1-1) y el despliegue trae K8; si no, en borrador. (nuevo)
/// </summary>
public sealed record CreateDepositLineCommand(string Code, string Name, DepositClass DepositClass, bool Active, DepositLineVersionInput FirstVersion)
    : IRequest<Result<LendingLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => FirstVersion?.Reason ?? string.Empty;
}

public sealed class CreateDepositLineCommandValidator : AbstractValidator<CreateDepositLineCommand>
{
    public CreateDepositLineCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indicá el código de la línea.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage("El código admite hasta 10 caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indicá el nombre de la línea.").MaximumLength(120);
        RuleFor(x => x.DepositClass).IsInEnum().WithMessage("Elegí la clase de captación.");
        RuleFor(x => x.FirstVersion).NotNull().WithMessage("La línea nace con su primera versión.")
            .SetValidator(new DepositLineVersionInputValidator()!);
    }
}

/// <summary>
/// Código vivo repetido (de cualquier naturaleza) → <c>Catalogo.CodigoDuplicado</c> con el nombre del existente, antes de guardar; versión mal formada
/// → <c>Lending.DepositLine.Invalid</c> / <c>Lending.TermDeposit.LineIncomplete</c> / <c>Lending.Savings.RateTiersOverlap</c>; retroactiva →
/// <c>Lending.DepositLine.Retroactive</c>; activa sin perfil que autorice captar → <c>Lending.Savings.LineNotAllowedByProfile</c>; activa sin K8 →
/// <c>Lending.Savings.DeliveryNotAvailable</c>. Si algo falla no se guarda nada. Un perfil que capta sólo de asociados fuerza «exige asociado».
/// </summary>
public sealed class CreateDepositLineCommandHandler(
    IApplicationDbContext db,
    AltaDeVersionDeCaptacion alta,
    IContextoDeLineasDeCaptacion contexto,
    IDateTimeService reloj) : IRequestHandler<CreateDepositLineCommand, Result<LendingLineDto>>
{
    public async Task<Result<LendingLineDto>> Handle(CreateDepositLineCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetida = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.Code == codigo && !l.IsDeleted, ct);
        if (repetida is not null)
            return Result.Failure<LendingLineDto>(CodigoDeCatalogo.Duplicado("una línea de la Cartera", codigo, repetida.Name, repetida.PublicId));

        var destino = await ReferenciasDeLineasDeCaptacion.LineaALaVistaAsync(db, request.FirstVersion, ct);
        if (destino.IsFailure) return Result.Failure<LendingLineDto>(destino.Error);
        var hoy = reloj.HoyLocal;
        var perfil = await contexto.PerfilAsync(hoy, ct);
        if (request.FirstVersion.DepositClass is { } dicha && dicha != request.DepositClass)
            return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(ReglasDeLineaDeCaptacion.ValidarClaseInmutable(request.DepositClass, dicha)!));
        var borrador = request.FirstVersion.Borrador(request.DepositClass, destino.Value, perfil.SoloAsociados);
        if (ReglasDeLineaDeCaptacion.ValidarVersion(borrador,
            await Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Deposit, hoy, ct)) is { } problema) return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(problema));

        if (request.Active)
        {
            if (EntregaDeLineas.NoDisponible(LineNature.Deposit, contexto.Entrega) is { } sinEntrega) return Result.Failure<LendingLineDto>(sinEntrega);
            if (ReglasDeLineaDeCaptacion.ValidarClaseAutorizada(request.DepositClass, perfil.AutorizaCaptar, perfil.TipoDeOrganizacion, perfil.PerfilPublicId)
                is { } noAutorizada)
                return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(noAutorizada));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = CreditLine.Nueva(codigo, request.Name, null, LineNature.Deposit);
            db.CreditLines.Add(linea);
            await db.SaveChangesAsync(ct);
            await alta.RegistrarAsync(linea, borrador, ct);
            if (request.Active) linea.Activar();
            await db.SaveChangesAsync(ct);
            return Result.Success(await LectorDeLineasDeCaptacion.DtoAsync(db, linea, Max(hoy, borrador.ValidFrom), null, ct));
        }, ct);
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}

// -------------------------------------------------------------------------------------------------------------- versionar --

/// <summary>
/// Registra una versión nueva de una línea de captación (api.md §3, <c>POST /{id}/deposit-versions</c>): la anterior rige hasta la víspera (lectura
/// bitemporal), lo causado, liquidado o sellado no cambia (FR-007) y un cambio de tasa de una línea a la vista avisa a sus titulares (FR-038). (nuevo)
/// </summary>
public sealed record AddDepositLineVersionCommand(Guid LinePublicId, DepositLineVersionInput Version)
    : IRequest<Result<DepositLineVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => Version?.Reason ?? string.Empty;
}

public sealed class AddDepositLineVersionCommandValidator : AbstractValidator<AddDepositLineVersionCommand>
{
    public AddDepositLineVersionCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Version).NotNull().SetValidator(new DepositLineVersionInputValidator()!);
    }
}

/// <summary>
/// Línea inexistente o fuera del alcance → 404 <c>Lending.Line.NotFound</c>; de otra naturaleza → <c>Lending.CreditLine.NatureMismatch</c>; cerrada →
/// <c>Lending.CreditLine.Closed</c>; otra clase → <c>Lending.DepositLine.ClassImmutable</c>; empieza antes de la última registrada →
/// <c>Lending.DepositLine.VersionOverlaps</c>; antes del aviso a los titulares → <c>Lending.Savings.RateChangeNoticeTooShort</c>; las demás, las del alta.
/// </summary>
public sealed class AddDepositLineVersionCommandHandler(
    IApplicationDbContext db,
    AltaDeVersionDeCaptacion alta,
    AvisoDeCambioDeTasa aviso,
    IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<AddDepositLineVersionCommand, Result<DepositLineVersionDto>>
{
    public async Task<Result<DepositLineVersionDto>> Handle(AddDepositLineVersionCommand request, CancellationToken ct)
    {
        var encontrada = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Result.Failure<DepositLineVersionDto>(encontrada.Error);
        var linea = encontrada.Value;
        if (linea.Nature == LineNature.Placement) return Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.UsarRutaDeCredito(linea.Code));
        if (linea.ValidarNaturaleza(LineNature.Deposit) is { } distinta) return Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.De(distinta));
        if (linea.Status == CreditLineStatus.Closed) return Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.De(linea.ValidarActivacion()!));

        var clase = await LectorDeLineasDeCaptacion.ClaseDeAsync(db, linea.Id, ct) ?? request.Version.DepositClass ?? DepositClass.Demand;
        if (request.Version.DepositClass is { } dicha && ReglasDeLineaDeCaptacion.ValidarClaseInmutable(clase, dicha) is { } otraClase)
            return Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.De(otraClase));
        var destino = await ReferenciasDeLineasDeCaptacion.LineaALaVistaAsync(db, request.Version, ct);
        if (destino.IsFailure) return Result.Failure<DepositLineVersionDto>(destino.Error);

        var hoy = reloj.HoyLocal;
        var perfil = await contexto.PerfilAsync(hoy, ct);
        var borrador = request.Version.Borrador(clase, destino.Value, perfil.SoloAsociados);
        if (ReglasDeLineaDeCaptacion.ValidarVersion(borrador,
            await Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Deposit, hoy, ct)) is { } problema) return Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.De(problema));
        var contraLoRegistrado = await alta.ValidarContraLoRegistradoAsync(linea, borrador.ValidFrom, ct);
        if (contraLoRegistrado.IsFailure) return Result.Failure<DepositLineVersionDto>(contraLoRegistrado.Error);

        // FR-038: la versión que rige la víspera del cambio es la que los titulares tienen; si la tasa cambia, se avisa con anticipación.
        var previa = await LectorDeLineasDeCaptacion.VigenteOProximaDeCaptacionAsync(db, linea.Id, borrador.ValidFrom.AddDays(-1), ct);
        var anterior = previa is null ? null : BorradorDeVersionDeCaptacion.De(previa, await LectorDeLineasDeCaptacion.TramosAsync(db, previa.Id, ct));
        if (await aviso.AnticipacionAsync(linea.Id, anterior, borrador, hoy, ct) is { } dias
            && ReglasDeLineaDeCaptacion.ValidarAnticipacionDelAviso(borrador.ValidFrom, hoy, dias) is { } corto)
            return Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.De(corto));
        var actor = await actorActual.ObtenerAsync(ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var seguida = await db.CreditLines.FirstAsync(l => l.Id == linea.Id, ct);
            var version = await alta.RegistrarAsync(seguida, borrador, ct);
            if (anterior is not null && await aviso.AvisarAsync(seguida, version, anterior, borrador, hoy, actor.UserId, ct) > 0)
                await db.SaveChangesAsync(ct);
            return Result.Success(await LectorDeLineasDeCaptacion.DtoDeCaptacionAsync(db, version, ct));
        }, ct);
    }
}
