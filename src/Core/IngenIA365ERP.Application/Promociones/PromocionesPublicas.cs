using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Promociones;

/// <summary>
/// Piezas promocionales visibles ahora mismo, para la pantalla de inicio de
/// sesión. Se sirve SIN autenticación.
/// </summary>
public sealed record ListarPromocionesPublicasQuery : IRequest<Result<IReadOnlyList<PromoPublicaDto>>>;

/// <param name="TieneImagen">
/// Se manda el indicador y no los bytes: la imagen se pide aparte, para que el
/// navegador pueda cachearla y no viaje otra vez en cada carga del login.
/// </param>
public sealed record PromoPublicaDto(
    Guid PublicId,
    string Titulo,
    string? Texto,
    string? TextoEnlace,
    string? Enlace,
    bool TieneImagen,
    string? ImagenTextoAlternativo);

public sealed class ListarPromocionesPublicasQueryHandler(
    IAdminDbContext db,
    IDateTimeService reloj)
    : IRequestHandler<ListarPromocionesPublicasQuery, Result<IReadOnlyList<PromoPublicaDto>>>
{
    /// <summary>
    /// Tope de piezas devueltas. La pantalla muestra un carrusel, no un
    /// catálogo, y este endpoint es anónimo: sin límite, una carga mal hecha
    /// del administrador se convierte en una respuesta enorme servida a
    /// cualquiera que abra el login.
    /// </summary>
    private const int Maximo = 10;

    public async Task<Result<IReadOnlyList<PromoPublicaDto>>> Handle(
        ListarPromocionesPublicasQuery request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;

        // La vigencia se filtra en la consulta, no después: si se filtrara en
        // memoria, el contenido caducado igual habría salido de la base, y si
        // se filtrara en el navegador habría viajado hasta el visitante.
        var piezas = await db.PromoContenidos
            .AsNoTracking()
            .Where(p => p.Publicado
                        && (p.VigenteDesde == null || p.VigenteDesde <= ahora)
                        && (p.VigenteHasta == null || p.VigenteHasta >= ahora))
            .OrderBy(p => p.Orden).ThenBy(p => p.Id)
            .Take(Maximo)
            .Select(p => new PromoPublicaDto(
                p.PublicId,
                p.Titulo,
                p.Texto,
                p.TextoEnlace,
                p.Enlace,
                p.Imagen != null,
                p.ImagenTextoAlternativo))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<PromoPublicaDto>>(piezas);
    }
}

/// <summary>Imagen de una pieza. También anónimo.</summary>
public sealed record ObtenerImagenPromoQuery(Guid PublicId) : IRequest<Result<ImagenPromo>>;

public sealed record ImagenPromo(byte[] Contenido, string TipoMime);

public sealed class ObtenerImagenPromoQueryHandler(IAdminDbContext db, IDateTimeService reloj)
    : IRequestHandler<ObtenerImagenPromoQuery, Result<ImagenPromo>>
{
    public async Task<Result<ImagenPromo>> Handle(ObtenerImagenPromoQuery request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;

        // Se repite el filtro de vigencia. Sin él, conocer el PublicId de una
        // pieza despublicada bastaría para seguir descargando su imagen.
        var pieza = await db.PromoContenidos
            .AsNoTracking()
            .Where(p => p.PublicId == request.PublicId
                        && p.Publicado
                        && (p.VigenteDesde == null || p.VigenteDesde <= ahora)
                        && (p.VigenteHasta == null || p.VigenteHasta >= ahora))
            .Select(p => new { p.Imagen, p.ImagenTipoMime })
            .FirstOrDefaultAsync(ct);

        if (pieza?.Imagen is null || pieza.ImagenTipoMime is null)
            return Result.Failure<ImagenPromo>("Promociones.NotFound", "No existe la imagen.");

        return Result.Success(new ImagenPromo(pieza.Imagen, pieza.ImagenTipoMime));
    }
}
