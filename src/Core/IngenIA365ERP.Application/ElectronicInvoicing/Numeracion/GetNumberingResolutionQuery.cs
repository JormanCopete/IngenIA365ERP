using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>Los documentos que numeró una resolución en un mes de expedición: cuántos y del primero al último consecutivo. (nuevo)</summary>
public sealed record DocumentosDeResolucionPorMesDto(int Year, int Month, int Count, long FirstConsecutive, long LastConsecutive);

/// <summary>El detalle de una resolución (api.md §24.2 <c>GET /resolutions/{id}</c>): la de la lista más sus documentos por mes. (nuevo)</summary>
public sealed record DianNumberingResolutionDetailDto(DianNumberingResolutionDto Resolution, IReadOnlyList<DocumentosDeResolucionPorMesDto> DocumentsByMonth);

/// <summary>
/// Una resolución de numeración con los documentos que numeró (feature 012, I4, T745; <c>GET /api/electronic-invoicing/resolutions/{id}</c>,
/// api.md §24.2): el mismo <see cref="DianNumberingResolutionDto"/> de la lista —estado calculado a hoy, consumo, días para vencer, asociaciones
/// con la clave técnica enmascarada— y los documentos electrónicos agrupados por mes de expedición, del más antiguo al más reciente. Sólo
/// lectura; una que no existe (o dada de baja) responde <c>ElectronicInvoicing.Resolution.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetNumberingResolutionQuery(Guid ResolutionPublicId) : IRequest<Result<DianNumberingResolutionDetailDto>>;

public sealed class GetNumberingResolutionQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetNumberingResolutionQuery, Result<DianNumberingResolutionDetailDto>>
{
    public async Task<Result<DianNumberingResolutionDetailDto>> Handle(GetNumberingResolutionQuery request, CancellationToken ct)
    {
        var resolucion = await db.DianNumberingResolutions.AsNoTracking().Include(r => r.Channels)
            .FirstOrDefaultAsync(r => r.PublicId == request.ResolutionPublicId, ct);
        if (resolucion is null) return Result.Failure<DianNumberingResolutionDetailDto>(ErroresDeNumeracionYConfiguracion.ResolutionNotFound());

        var porMes = await db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.ResolutionId == resolucion.Id)
            .GroupBy(d => new { d.IssueDate.Year, d.IssueDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Cuantos = g.Count(),
                Primero = g.Min(d => d.Consecutive),
                Ultimo = g.Max(d => d.Consecutive),
            })
            .ToListAsync(ct);

        return Result.Success(new DianNumberingResolutionDetailDto(
            DianNumberingResolutionDto.De(resolucion, reloj.HoyLocal),
            porMes.OrderBy(m => m.Year).ThenBy(m => m.Month)
                .Select(m => new DocumentosDeResolucionPorMesDto(m.Year, m.Month, m.Cuantos, m.Primero, m.Ultimo))
                .ToList()));
    }
}
