using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Configuration;

/// <summary>Una combinación modalidad + sub-segmento del catálogo nacional, con lo que la norma le asigna (T44). (nuevo)</summary>
public sealed record ModalityCombinationDto(string? SubsegmentCode, string SarcSegment, string? ExpectedLossSegment, string? UsuryKey,
    bool HousingRateCap, bool AdmitsUvr);

/// <summary>
/// Una modalidad del catálogo vigente (api.md §3.2): su código y nombre, los sub-segmentos que admite, y por combinación el segmento SARC,
/// el de pérdida esperada, la clave de <c>USURA</c> (modalidad certificada) y si admite UVR. <c>sarcSegment</c>, <c>usuryKey</c> y
/// <c>admitsUvr</c> en la raíz son los de la primera combinación (una modalidad con una sola combinación no tiene sub-segmento). (nuevo)
/// </summary>
public sealed record ModalityDto(
    string Code,
    string Name,
    IReadOnlyList<string> Subsegments,
    string SarcSegment,
    string? ExpectedLossSegment,
    string? UsuryKey,
    bool AdmitsUvr,
    DateOnly? RetiredFrom,
    bool Verified,
    IReadOnlyList<ModalityCombinationDto> Combinations);

/// <summary>El documento <c>CatalogoDeModalidades</c> vigente con su identidad y su huella (api.md §3.2). (nuevo)</summary>
public sealed record ModalitiesCatalogDto(Guid DocumentPublicId, string Code, DateOnly ValidFrom, string Sha256, bool Verified,
    IReadOnlyList<ModalityDto> Modalities);

/// <summary>
/// Las modalidades del documento nacional vigente a la fecha (api.md §3.2, <c>GET /api/lending/modalities?date=</c>,
/// <c>Lending.CreditLines.View</c>; T44): sólo lectura, por <see cref="INormativaVigente"/>. Sin documento vigente,
/// <c>NationalValues.Missing</c> (que el lector alerta). Es un catálogo nacional: no tiene oficina ni línea. (nuevo)
/// </summary>
public sealed record ListModalitiesQuery(DateOnly? Date = null) : IRequest<Result<ModalitiesCatalogDto>>;

public sealed class ListModalitiesQueryValidator : AbstractValidator<ListModalitiesQuery>;

public sealed class ListModalitiesQueryHandler(INormativaVigente normativa, IDateTimeService reloj)
    : IRequestHandler<ListModalitiesQuery, Result<ModalitiesCatalogDto>>
{
    public async Task<Result<ModalitiesCatalogDto>> Handle(ListModalitiesQuery request, CancellationToken ct)
    {
        var catalogo = await normativa.CatalogoDeModalidadesAsync(request.Date ?? reloj.HoyLocal, null, ct);
        if (catalogo.IsFailure) return Result.Failure<ModalitiesCatalogDto>(catalogo.Error);
        var d = catalogo.Value;
        return Result.Success(new ModalitiesCatalogDto(d.PublicId, d.Codigo, d.ValidFrom, d.Sha256, d.Verified,
            d.Documento.Modalidades.Select(m =>
            {
                var combinaciones = m.Combinaciones.Select(c => new ModalityCombinationDto(c.SubsegmentCode, c.SarcSegment, c.ExpectedLossSegment,
                    c.UsuryKey, c.HousingRateCap, c.AdmitsUvr)).ToList();
                var primera = combinaciones[0];
                return new ModalityDto(m.Code, m.Name, combinaciones.Select(c => c.SubsegmentCode).OfType<string>().ToList(), primera.SarcSegment,
                    primera.ExpectedLossSegment, primera.UsuryKey, combinaciones.Any(c => c.AdmitsUvr), m.RetiredFrom, m.Verified, combinaciones);
            }).ToList()));
    }
}
