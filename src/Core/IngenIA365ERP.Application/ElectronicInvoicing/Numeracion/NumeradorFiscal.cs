using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>
/// Lo que pide numerar un documento fiscal electrónico (T705; contracts/dian.md §9). El canal, el software y el ambiente son los que se
/// <b>sellan</b> en el documento (la configuración vigente a la fecha): la resolución tiene que estar asociada a ese canal en esa fecha.
/// </summary>
/// <param name="Tipo">Factura, documento equivalente POS o documento soporte (las notas no numeran aquí).</param>
/// <param name="Prefijo">El prefijo fiscal del tipo de documento; en contingencia 03, el del tipo del rol de contingencia.</param>
/// <param name="Ambiente">El ambiente del canal sellado.</param>
/// <param name="ChannelCode">El canal sellado.</param>
/// <param name="SoftwareId">El software sellado (modo propio).</param>
/// <param name="Fecha">La fecha de operación.</param>
/// <param name="Contingencia03">Hay contingencia 03 abierta: numera con la resolución <c>Contingency</c> que respalda a <paramref name="Tipo"/>.</param>
public sealed record SolicitudDeNumeroFiscal(
    ElectronicDocumentKind Tipo,
    string Prefijo,
    DianEnvironment Ambiente,
    string ChannelCode,
    string? SoftwareId,
    DateOnly Fecha,
    bool Contingencia03 = false); // (nuevo)

/// <summary>
/// El número asignado: prefijo, consecutivo y la resolución con que se numeró. <see cref="AvisarPorAgotar"/> dice si con este número
/// se cruzó el umbral de <c>Dian.AvisoResolucionPorcentaje</c> (lo levanta quien confirma, después del guardado). (nuevo)
/// </summary>
public sealed record NumeroFiscal(
    int ResolutionId,
    Guid ResolutionPublicId,
    string ResolutionNumber,
    ResolutionKind ResolutionKind,
    string Prefijo,
    long Consecutivo,
    string? TechnicalKey,
    long Disponibles)
{
    /// <summary>El número completo, prefijo + consecutivo.</summary>
    public string Numero => Prefijo + Consecutivo.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// El <b>único</b> escritor de <c>COR_DianNumberingResolutions.LastIssuedNumber</c> (feature 012, I4, T705; T15, T16; FR-038, FR-065;
/// contracts/dian.md §9; <c>SoloElNumeradorNumera</c>). Lo llama la confirmación del documento fiscal electrónico <b>después</b> del
/// cerrojo de existencias —la fila de la resolución es la última del orden canónico de <see cref="ICerrojoDeInventario"/>— y dentro de
/// la misma transacción: el <c>SaveChanges</c> de la confirmación guarda el número del documento y el de la resolución juntos, sin huecos
/// ni repetidos, y el borrador nunca consume.
///
/// <para>
/// Busca por (<c>Kind</c>, <c>Prefix</c>, <c>Environment</c> del canal sellado) la resolución vigente a la fecha de operación <b>y</b>
/// asociada al canal sellado en esa fecha; en contingencia 03, la <c>Contingency</c> cuyo <c>BacksUpKind</c> es el tipo. Rechaza:
/// vencida → <c>ElectronicInvoicing.Resolution.Expired</c>; agotada → <c>ElectronicInvoicing.Resolution.Exhausted</c>; sin resolución,
/// de otro ambiente o no asociada → <c>Inventory.Numbering.ResolutionUnavailable</c>. Nunca numera fuera de una resolución vigente ni
/// renumera un documento ya numerado. La única excepción es <see cref="ReutilizarNumeroParaReemplazo"/>, la vía del caso b.
/// </para>
/// </summary>
public sealed class NumeradorFiscal(IApplicationDbContext db, ICerrojoDeInventario cerrojo)
{
    /// <summary>Numera <paramref name="documento"/> (sin número todavía) y le copia prefijo y consecutivo.</summary>
    public async Task<Result<NumeroFiscal>> NumerarDocumentoAsync(InventoryDocument documento, SolicitudDeNumeroFiscal solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento.Number is not null)
            throw new InvalidOperationException($"El documento {documento.PublicId} ya tiene número: un documento numerado no se renumera.");

        var numero = await NumerarAsync(solicitud, ct);
        if (numero.IsFailure) return numero;
        documento.Prefix = numero.Value.Prefijo;
        documento.Number = numero.Value.Consecutivo;
        return numero;
    }

    /// <summary>Toma el siguiente número de la resolución que corresponde, bajo el cerrojo, e incrementa <c>LastIssuedNumber</c> por EF.</summary>
    public async Task<Result<NumeroFiscal>> NumerarAsync(SolicitudDeNumeroFiscal solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var tipoBase = ReglasDeResolucion.TipoDeResolucion(solicitud.Tipo)
            ?? throw new InvalidOperationException($"El tipo {solicitud.Tipo} no numera con resolución: las notas llevan su consecutivo propio.");
        var tipo = solicitud.Contingencia03 ? ResolutionKind.Contingency : tipoBase;
        var prefijo = ReglasDeResolucion.Prefijo(solicitud.Prefijo);
        var canal = ReglasDeResolucion.Canal(solicitud.ChannelCode);
        var fecha = solicitud.Fecha;

        var candidatas = await db.DianNumberingResolutions.AsNoTracking()
            .Include(r => r.Channels)
            .Where(r => r.IsActive && r.Kind == tipo && r.Prefix == prefijo && r.Environment == solicitud.Ambiente)
            .Where(r => !solicitud.Contingencia03 || r.BacksUpKind == tipoBase)
            .ToListAsync(ct);

        var asociadas = candidatas
            .Where(r => r.VigenteEn(fecha) && ReglasDeResolucion.AsociacionVigente(r, canal, solicitud.SoftwareId, fecha) is not null)
            .OrderBy(r => r.Agotada)
            .ThenBy(r => r.ValidFrom) // los rangos se consumen en orden: primero la más antigua que tenga números
            .ThenBy(r => r.RangeFrom)
            .ToList();

        if (asociadas.Count == 0) return Result.Failure<NumeroFiscal>(SinResolucion(candidatas, solicitud, tipo, prefijo, canal));

        // La fila de la resolución, al final del orden canónico y en exclusivo; cargada ya bloqueada lee el LastIssuedNumber del último
        // que confirmó. Si al leerla bloqueada resulta agotada, pasa a la siguiente candidata.
        Domain.Entities.ElectronicInvoicing.DianNumberingResolution? resolucion = null;
        foreach (var candidata in asociadas)
        {
            await cerrojo.BloquearResolucionFiscalAsync(candidata.Id, ct);
            var bloqueada = await db.DianNumberingResolutions.Include(r => r.Channels).FirstAsync(r => r.Id == candidata.Id, ct);
            if (bloqueada.Agotada) continue;
            resolucion = bloqueada;
            break;
        }
        if (resolucion is null)
        {
            var ultima = asociadas[^1];
            return Result.Failure<NumeroFiscal>(ErroresDeNumeracionYConfiguracion.ResolutionExhausted(ultima.ResolutionNumber, ultima.Prefix, ultima.RangeTo));
        }

        resolucion.LastIssuedNumber = resolucion.LastIssuedNumber + 1;
        var asociacion = ReglasDeResolucion.AsociacionVigente(resolucion, canal, solicitud.SoftwareId, fecha);
        return Result.Success(new NumeroFiscal(resolucion.Id, resolucion.PublicId, resolucion.ResolutionNumber, resolucion.Kind,
            resolucion.Prefix, resolucion.LastIssuedNumber, asociacion?.TechnicalKey, resolucion.Disponibles));
    }

    /// <summary>
    /// La vía <b>exclusiva</b> del caso b (T705, FR-066): el documento de reemplazo toma el mismo prefijo y consecutivo del rechazado, sin
    /// consumir la resolución. Sólo la invoca <c>ReplaceRejectedDocumentCommand</c> (lo vigila <c>SoloElNumeradorNumera</c>). A diferencia
    /// de la nómina electrónica, un número rechazado se reusa. (nuevo)
    /// </summary>
    public static void ReutilizarNumeroParaReemplazo(InventoryDocument reemplazo, string prefijo, long consecutivo)
    {
        ArgumentNullException.ThrowIfNull(reemplazo);
        if (reemplazo.Number is not null)
            throw new InvalidOperationException($"El reemplazo {reemplazo.PublicId} ya tiene número: un documento numerado no se renumera.");
        if (consecutivo < 1) throw new ArgumentOutOfRangeException(nameof(consecutivo), consecutivo, "El consecutivo del rechazado es 1 o mayor.");
        reemplazo.Prefix = ReglasDeResolucion.Prefijo(prefijo);
        reemplazo.Number = consecutivo;
    }

    /// <summary>Por qué no hay resolución: vencida si alguna asociada al canal ya pasó su vigencia; si no, no disponible.</summary>
    private static Error SinResolucion(IReadOnlyList<DianNumberingResolution> candidatas, SolicitudDeNumeroFiscal s, ResolutionKind tipo,
        string prefijo, string canal)
    {
        var vencida = candidatas
            .Where(r => r.ValidTo < s.Fecha)
            .Where(r => r.Channels.Any(c => !c.IsDeleted && string.Equals(c.ChannelCode, canal, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => r.ValidTo)
            .FirstOrDefault();
        if (vencida is not null && !candidatas.Any(r => r.VigenteEn(s.Fecha)))
            return ErroresDeNumeracionYConfiguracion.ResolutionExpired(vencida.ResolutionNumber, vencida.Prefix, vencida.ValidTo);

        return ErroresDeNumeracionYConfiguracion.ResolutionUnavailable(ReglasDeResolucion.Nombre(tipo), prefijo,
            ReglasDeResolucion.Nombre(s.Ambiente), s.Fecha, canal);
    }
}
