using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>Una asociación de la resolución a un canal, con la clave técnica enmascarada (api.md §24.2). (nuevo)</summary>
public sealed record AsociacionDeResolucionDto(string ChannelCode, string? SoftwareId, DateOnly ValidFrom, DateOnly? ValidTo, string? TechnicalKeyMasked);

/// <summary>Una resolución de numeración con su estado calculado (api.md §24.2, <c>DianNumberingResolutionDto</c>). (nuevo)</summary>
public sealed record DianNumberingResolutionDto(
    Guid ResolutionPublicId,
    ResolutionKind Kind,
    ResolutionKind? BacksUpKind,
    string ResolutionNumber,
    DateOnly ResolutionDate,
    string Prefix,
    long RangeFrom,
    long RangeTo,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    DianEnvironment Environment,
    long? LastIssuedNumber,
    decimal ConsumedFraction,
    int DaysToExpire,
    string Status,
    IReadOnlyList<AsociacionDeResolucionDto> Channels)
{
    public static DianNumberingResolutionDto De(DianNumberingResolution r, DateOnly hoy) => new(
        r.PublicId, r.Kind, r.BacksUpKind, r.ResolutionNumber, r.ResolutionDate, r.Prefix, r.RangeFrom, r.RangeTo, r.ValidFrom, r.ValidTo,
        r.Environment, r.TieneNumerosEmitidos ? r.LastIssuedNumber : null, ReglasDeResolucion.FraccionConsumida(r),
        ReglasDeResolucion.DiasParaVencer(r, hoy), ReglasDeResolucion.Estado(r, hoy).ToString(),
        r.Channels.Where(c => !c.IsDeleted).OrderBy(c => c.ValidFrom)
            .Select(c => new AsociacionDeResolucionDto(c.ChannelCode, c.SoftwareId, c.ValidFrom, c.ValidTo, ReglasDeResolucion.Enmascarar(c.TechnicalKey)))
            .ToList());
}

/// <summary>
/// Registrar una resolución de numeración DIAN (feature 012, I4, T709; <c>POST /api/electronic-invoicing/resolutions</c>, api.md §24.2;
/// contracts/dian.md §9; FR-065). <c>LastIssuedNumber</c> nace en <c>RangeFrom − 1</c>. Rechaza prefijo inválido, rango inválido, cruce con
/// otra del mismo tipo, prefijo y ambiente (rangos o vigencias), la de contingencia sin tipo respaldado y un prefijo que ya numera notas
/// (<c>ElectronicInvoicing.Resolution.PrefixInUse</c>, T710). Nunca la crea un canal. (nuevo)
/// </summary>
public sealed record RegisterNumberingResolutionCommand(
    ResolutionKind Kind,
    ResolutionKind? BacksUpKind,
    string ResolutionNumber,
    DateOnly ResolutionDate,
    string? Prefix,
    long RangeFrom,
    long RangeTo,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    DianEnvironment Environment,
    string Reason,
    string? Notes = null)
    : IRequest<Result<DianNumberingResolutionDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterNumberingResolutionCommandValidator : ValidadorConMotivo<RegisterNumberingResolutionCommand>
{
    public RegisterNumberingResolutionCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Environment).IsInEnum();
        RuleFor(x => x.ResolutionNumber).NotEmpty().WithMessage("Indique el número de la resolución.").MaximumLength(30);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class RegisterNumberingResolutionCommandHandler(IApplicationDbContext db, IPrefijosDeModulos prefijos, IDateTimeService reloj)
    : IRequestHandler<RegisterNumberingResolutionCommand, Result<DianNumberingResolutionDto>>
{
    public async Task<Result<DianNumberingResolutionDto>> Handle(RegisterNumberingResolutionCommand request, CancellationToken ct)
    {
        var datos = new DatosDeResolucion(request.Kind, request.BacksUpKind, request.ResolutionNumber, request.Prefix, request.RangeFrom,
            request.RangeTo, request.ValidFrom, request.ValidTo, request.Environment);
        var revision = await ValidacionDeResolucion.RevisarAsync(db, prefijos, datos, excluirId: null, ct);
        if (revision.IsFailure) return Result.Failure<DianNumberingResolutionDto>(revision.Error);

        var resolucion = new DianNumberingResolution
        {
            Kind = request.Kind,
            BacksUpKind = datos.Respaldo,
            ResolutionNumber = request.ResolutionNumber.Trim(),
            ResolutionDate = request.ResolutionDate,
            Prefix = datos.PrefijoNormalizado,
            RangeTo = request.RangeTo,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Environment = request.Environment,
            IsActive = true,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        // Primero el valor inicial del numerador (nada emitido: RangeFrom − 1), luego el rango, que lo arrastra mientras no se emita.
        resolucion.RangeFrom = request.RangeFrom;

        db.DianNumberingResolutions.Add(resolucion);
        await db.SaveChangesAsync(ct);
        return Result.Success(DianNumberingResolutionDto.De(resolucion, reloj.HoyLocal));
    }
}

/// <summary>Los datos de una resolución que se revisan al registrarla o corregirla. (nuevo)</summary>
public sealed record DatosDeResolucion(
    ResolutionKind Kind,
    ResolutionKind? BacksUpKind,
    string ResolutionNumber,
    string? Prefix,
    long RangeFrom,
    long RangeTo,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    DianEnvironment Environment)
{
    public string PrefijoNormalizado => ReglasDeResolucion.Prefijo(Prefix);

    /// <summary>El tipo respaldado sólo existe en las de contingencia.</summary>
    public ResolutionKind? Respaldo => Kind == ResolutionKind.Contingency ? BacksUpKind : null;
}

/// <summary>Las reglas de alta y corrección de una resolución (api.md §24.2; T709, T710). (nuevo)</summary>
public static class ValidacionDeResolucion
{
    public static async Task<Result> RevisarAsync(IApplicationDbContext db, IPrefijosDeModulos prefijos, DatosDeResolucion datos, int? excluirId,
        CancellationToken ct)
    {
        var prefijo = datos.PrefijoNormalizado;
        if (!ReglasDeResolucion.PrefijoValido(prefijo)) return Result.Failure(ErroresDeNumeracionYConfiguracion.PrefixInvalid());
        if (datos.RangeFrom < 1 || datos.RangeFrom > datos.RangeTo || datos.ValidTo < datos.ValidFrom)
            return Result.Failure(ErroresDeNumeracionYConfiguracion.RangeInvalid());
        if (datos.Kind == ResolutionKind.Contingency && datos.BacksUpKind is null or ResolutionKind.Contingency)
            return Result.Failure(ErroresDeNumeracionYConfiguracion.BackedKindRequired());

        var numero = datos.ResolutionNumber.Trim();
        var mismas = await db.DianNumberingResolutions.AsNoTracking()
            .Where(r => r.Environment == datos.Environment && r.Prefix == prefijo && (excluirId == null || r.Id != excluirId))
            .ToListAsync(ct);
        var cruce = mismas.FirstOrDefault(r => r.ResolutionNumber == numero
            || (r.Kind == datos.Kind && r.IsActive
                && ((r.RangeFrom <= datos.RangeTo && datos.RangeFrom <= r.RangeTo) || (r.ValidFrom <= datos.ValidTo && datos.ValidFrom <= r.ValidTo))));
        if (cruce is not null) return Result.Failure(ErroresDeNumeracionYConfiguracion.ResolutionOverlaps(cruce.ResolutionNumber));

        if (prefijo.Length > 0 && await prefijos.UsoDeAsync(prefijo, ct) is { } uso)
            return Result.Failure(ErroresDeNumeracionYConfiguracion.PrefixInUse(prefijo, uso.UsadoPor));
        return Result.Success();
    }
}
