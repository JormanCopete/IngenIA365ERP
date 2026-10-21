using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// La respuesta al juzgado como documento emitido (feature 014, US13, T196; data-model §5.2.1; research E158, E176; FR-055): cuando se registra una
/// respuesta —la que escribe la persona o la de «aportes inembargables» que nace sola con el oficio—, se emite en la misma transacción un documento de la
/// clase <c>GarnishmentResponse</c> (prefijo <c>RJZ-</c>) a nombre de la persona embargada, con número, código de verificación y el PDF guardado, y su Id
/// queda en <c>LND_GarnishmentResponses.IssuedDocumentId</c>. Lo que se le dijo al juzgado ese día no se anula por el reverso de una operación (no cubre
/// cuentas). Si no se puede emitir (sin firmante, el almacén caído) la respuesta se registra igual sin documento y queda el aviso: la respuesta firmada
/// es su soporte. (nuevo)
/// </summary>
public sealed class RespuestasAlJuzgado(
    IApplicationDbContext db,
    EmisionDeDocumentosDeCartera emision,
    ConstructorDeDocumentosDeCartera constructor,
    IDateTimeService reloj,
    ILogger<RespuestasAlJuzgado>? logger = null)
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>El aviso que acompaña una respuesta registrada sin su documento.</summary>
    public const string CodigoSinDocumento = "Lending.Garnishment.ResponseDocumentNotIssued";

    /// <summary>Emite el documento de la respuesta dentro de la transacción de quien llama; devuelve su Id, o nulo con el motivo.</summary>
    public async Task<(int? DocumentoId, string? Aviso)> EmitirAsync(Garnishment embargo, GarnishmentResponseKind clase, DateOnly enviada, string texto,
        decimal? retenido, decimal? consignado, int? usuario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(embargo);
        var hoy = reloj.HoyLocal;
        var firmante = await constructor.FirmanteAsync(hoy, ct);
        if (firmante.IsFailure) return (null, Aviso(embargo, firmante.Error.Message));
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == embargo.PersonId, ct);
        var emitido = await emision.EmitirAsync(new SolicitudDeEmision(IssuedDocumentKind.GarnishmentResponse, persona, firmante.Value, usuario,
            DocumentDeliveryMode.Download, hoy)
        {
            Respuesta = new RespuestaParaDocumento(embargo.Id, clase, enviada, texto, retenido, consignado),
        }, ct);
        return emitido.IsFailure ? (null, Aviso(embargo, emitido.Error.Message)) : (emitido.Value.Documento.Id, null);
    }

    private string Aviso(Garnishment embargo, string motivo)
    {
        _log.LogWarning("[Cartera.RespuestaAlJuzgadoSinDocumento] La respuesta del embargo {Oficio} quedó sin documento emitido: {Motivo}", embargo.OrderNumber,
            motivo);
        return $"{CodigoSinDocumento}: la respuesta quedó registrada sin el documento con código de verificación ({motivo}).";
    }
}
