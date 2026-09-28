using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>
/// Las resoluciones de numeración (feature 012, I4, T709; <c>GET /api/electronic-invoicing/resolutions?kind=&amp;environment=&amp;status=</c>,
/// api.md §24.2): con el estado calculado a hoy (<c>Active | NotYetValid | Expired | Exhausted</c>, un texto, no un enum guardado), la
/// fracción consumida, los días para vencer y sus asociaciones a canales con la clave técnica enmascarada. (nuevo)
/// </summary>
public sealed record ListNumberingResolutionsQuery(ResolutionKind? Kind = null, DianEnvironment? Environment = null, string? Status = null)
    : IRequest<Result<IReadOnlyList<DianNumberingResolutionDto>>>;

public sealed class ListNumberingResolutionsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListNumberingResolutionsQuery, Result<IReadOnlyList<DianNumberingResolutionDto>>>
{
    public async Task<Result<IReadOnlyList<DianNumberingResolutionDto>>> Handle(ListNumberingResolutionsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var consulta = db.DianNumberingResolutions.AsNoTracking().Include(r => r.Channels).Where(r => r.IsActive);
        if (request.Kind is { } tipo) consulta = consulta.Where(r => r.Kind == tipo);
        if (request.Environment is { } ambiente) consulta = consulta.Where(r => r.Environment == ambiente);

        var todas = await consulta.OrderBy(r => r.Kind).ThenBy(r => r.Prefix).ThenByDescending(r => r.ValidFrom).ToListAsync(ct);
        IEnumerable<Domain.Entities.ElectronicInvoicing.DianNumberingResolution> filtradas = todas;
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<EstadoDeResolucion>(request.Status, ignoreCase: true, out var estado))
            filtradas = todas.Where(r => ReglasDeResolucion.Estado(r, hoy) == estado);

        return Result.Success<IReadOnlyList<DianNumberingResolutionDto>>(filtradas.Select(r => DianNumberingResolutionDto.De(r, hoy)).ToList());
    }
}
