using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Promociones;

// ---------------------------------------------------------------- listar ----

/// <summary>Todas las piezas, publicadas o no. Sólo para administración.</summary>
public sealed record ListarPromocionesQuery : IRequest<Result<IReadOnlyList<PromoAdminDto>>>;

public sealed record PromoAdminDto(
    Guid PublicId,
    string Titulo,
    string? Texto,
    string? TextoEnlace,
    string? Enlace,
    bool TieneImagen,
    string? ImagenTextoAlternativo,
    int Orden,
    bool Publicado,
    DateTime? VigenteDesde,
    DateTime? VigenteHasta);

public sealed class ListarPromocionesQueryHandler(IAdminDbContext db)
    : IRequestHandler<ListarPromocionesQuery, Result<IReadOnlyList<PromoAdminDto>>>
{
    public async Task<Result<IReadOnlyList<PromoAdminDto>>> Handle(
        ListarPromocionesQuery request, CancellationToken ct)
    {
        var piezas = await db.PromoContenidos
            .AsNoTracking()
            .OrderBy(p => p.Orden).ThenBy(p => p.Id)
            .Select(p => new PromoAdminDto(
                p.PublicId, p.Titulo, p.Texto, p.TextoEnlace, p.Enlace,
                p.Imagen != null, p.ImagenTextoAlternativo,
                p.Orden, p.Publicado, p.VigenteDesde, p.VigenteHasta))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<PromoAdminDto>>(piezas);
    }
}

// ---------------------------------------------------------------- guardar ---

/// <summary>
/// Crea o actualiza una pieza. <paramref name="PublicId"/> nulo crea.
/// </summary>
/// <param name="ImagenBase64">
/// Nulo deja la imagen como está; cadena vacía la quita.
/// </param>
public sealed record GuardarPromoCommand(
    Guid? PublicId,
    string Titulo,
    string? Texto,
    string? TextoEnlace,
    string? Enlace,
    string? ImagenBase64,
    string? ImagenTipoMime,
    string? ImagenTextoAlternativo,
    int Orden,
    bool Publicado,
    DateTime? VigenteDesde,
    DateTime? VigenteHasta) : IRequest<Result<Guid>>;

public sealed class GuardarPromoCommandValidator : AbstractValidator<GuardarPromoCommand>
{
    public GuardarPromoCommandValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Texto).MaximumLength(400);
        RuleFor(x => x.TextoEnlace).MaximumLength(60);
        RuleFor(x => x.Enlace).MaximumLength(500)
            .Must(EsDestinoAceptable)
            .WithMessage("El enlace debe ser https o una ruta interna que empiece por /.");

        RuleFor(x => x.VigenteHasta)
            .GreaterThan(x => x.VigenteDesde!.Value)
            .When(x => x.VigenteDesde.HasValue && x.VigenteHasta.HasValue)
            .WithMessage("La fecha final debe ser posterior a la inicial.");

        RuleFor(x => x.ImagenTipoMime)
            .Must(t => t is null || PromoContenido.TiposImagenAdmitidos.Contains(t))
            .WithMessage("Formato de imagen no admitido. Use PNG, JPEG, WebP o AVIF.");

        RuleFor(x => x.ImagenBase64)
            .Must(NoExcedeElTope)
            .WithMessage($"La imagen supera {PromoContenido.MaximoBytesImagen / 1024} KB.");

        // Una imagen sin descripción en la pantalla de entrada deja sin
        // contenido a quien navega con lector de pantalla, y ahí no hay forma
        // de saltarla.
        RuleFor(x => x.ImagenTextoAlternativo)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.ImagenBase64))
            .WithMessage("Describe la imagen: se lee en voz alta a quien usa lector de pantalla.")
            .MaximumLength(200);
    }

    /// <summary>
    /// Sólo https o ruta interna. http en claro expondría al visitante a que le
    /// cambien el destino en tránsito, justo desde la pantalla de entrada.
    /// </summary>
    private static bool EsDestinoAceptable(string? enlace) =>
        string.IsNullOrEmpty(enlace)
        || enlace.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || (enlace.StartsWith('/') && !enlace.StartsWith("//", StringComparison.Ordinal));

    private static bool NoExcedeElTope(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
            return true;

        // Se estima el tamaño desde la longitud del texto en vez de decodificar:
        // decodificar primero significaría reservar en memoria lo que se quiere
        // rechazar por grande.
        var bytesAproximados = base64.Length * 3L / 4;
        return bytesAproximados <= PromoContenido.MaximoBytesImagen;
    }
}

public sealed class GuardarPromoCommandHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext currentUser)
    : IRequestHandler<GuardarPromoCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(GuardarPromoCommand request, CancellationToken ct)
    {
        var guardia = VerificarAdministrador();
        if (guardia is not null) return Result.Failure<Guid>(guardia.Code, guardia.Message);

        var pieza = request.PublicId is { } id
            ? await db.PromoContenidos.FirstOrDefaultAsync(p => p.PublicId == id, ct)
            : null;

        if (request.PublicId is not null && pieza is null)
            return Result.Failure<Guid>("Promociones.NotFound", "No existe la pieza indicada.");

        if (pieza is null)
        {
            pieza = new PromoContenido();
            db.PromoContenidos.Add(pieza);
        }

        pieza.Titulo = request.Titulo;
        pieza.Texto = request.Texto;
        pieza.TextoEnlace = request.TextoEnlace;
        pieza.Enlace = request.Enlace;
        pieza.Orden = request.Orden;
        pieza.Publicado = request.Publicado;
        pieza.VigenteDesde = request.VigenteDesde;
        pieza.VigenteHasta = request.VigenteHasta;
        pieza.ImagenTextoAlternativo = request.ImagenTextoAlternativo;

        if (request.ImagenBase64 is not null)
        {
            if (request.ImagenBase64.Length == 0)
            {
                pieza.Imagen = null;
                pieza.ImagenTipoMime = null;
            }
            else
            {
                if (!TryDecodificar(request.ImagenBase64, out var bytes))
                    return Result.Failure<Guid>("Validation.ImagenIlegible", "La imagen no se pudo leer.");

                // Se vuelve a medir sobre los bytes reales: la comprobación del
                // validador es una estimación desde la longitud del texto.
                if (bytes.Length > PromoContenido.MaximoBytesImagen)
                    return Result.Failure<Guid>("Validation.ImagenMuyGrande",
                        $"La imagen supera {PromoContenido.MaximoBytesImagen / 1024} KB.");

                pieza.Imagen = bytes;
                pieza.ImagenTipoMime = request.ImagenTipoMime;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(pieza.PublicId);
    }

    private static bool TryDecodificar(string base64, out byte[] bytes)
    {
        // Se acepta también el formato data: que produce el navegador.
        var coma = base64.IndexOf(',');
        var carga = base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma > 0
            ? base64[(coma + 1)..]
            : base64;

        bytes = [];
        var destino = new byte[(carga.Length * 3 / 4) + 3];
        if (!Convert.TryFromBase64String(carga, destino, out var escritos))
            return false;

        bytes = destino[..escritos];
        return true;
    }

    private Error? VerificarAdministrador()
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return new Error("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return new Error("Identity.WrongTokenPurpose", "Este endpoint requiere purpose=full.");

        // Es contenido de la empresa dueña del producto hacia sus clientes, no
        // de una cooperativa: lo administra el maestro global, no el
        // administrador de un tenant.
        if (!currentUser.IsGlobalMasterAdmin)
            return new Error("Promociones.Forbidden",
                "Sólo la administración de IngenIA365 puede editar el contenido promocional.");

        return null;
    }

    private sealed record Error(string Code, string Message);
}

// --------------------------------------------------------------- eliminar ---

public sealed record EliminarPromoCommand(Guid PublicId) : IRequest<Result>;

public sealed class EliminarPromoCommandHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext currentUser)
    : IRequestHandler<EliminarPromoCommand, Result>
{
    public async Task<Result> Handle(EliminarPromoCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (!currentUser.IsGlobalMasterAdmin)
            return Result.Failure("Promociones.Forbidden",
                "Sólo la administración de IngenIA365 puede editar el contenido promocional.");

        var pieza = await db.PromoContenidos.FirstOrDefaultAsync(p => p.PublicId == request.PublicId, ct);
        if (pieza is null)
            return Result.Failure("Promociones.NotFound", "No existe la pieza indicada.");

        // Borrado lógico, como exige el principio VII.
        pieza.IsDeleted = true;
        pieza.DeletedAt = DateTime.UtcNow;
        pieza.DeletedBy = currentUser.Email;
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
