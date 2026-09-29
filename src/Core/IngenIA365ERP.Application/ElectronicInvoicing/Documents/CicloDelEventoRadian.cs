using System.Text;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// El ciclo de un evento RADIAN 030 o 032 del lado de la plataforma (feature 012, I5, T803–T805; contracts/dian.md §5.1 «la misma tabla y
/// la misma máquina simplificada», §14.3; api.md §24.7; T42). Lo usan <see cref="EmitRadianEventCommand"/> al pedirlo y
/// <see cref="IntentoAnteElCanal"/> (el procesador, T805) al transmitirlo:
/// <list type="bullet">
/// <item><see cref="ReconstruirAsync"/>: vuelve a armar el evento por el puerto del módulo fuente sellado en el documento
/// (<see cref="IFuenteDeDocumentoElectronico.LeerEventoRadianAsync"/>) con lo sellado —configuración, prefijo, consecutivo, instante—;</item>
/// <item><see cref="VerificadoAsync"/>: lo compara con el SHA-256 de la versión (si no coincide, no se emite: <c>CanonicalMismatch</c>) y sube
/// el canónico si la versión no lo tiene;</item>
/// <item><see cref="CerrarAsync"/>: con la respuesta definitiva (validado o rechazado) le avisa al módulo por
/// <see cref="IFuenteDeDocumentoElectronico.RegistrarResultadoDeEventoRadianAsync"/>, que deja el evento <c>Emitted</c> o <c>Rejected</c>
/// y atiende la alerta <c>Compras.EventosRadianFaltantes</c> cuando no falta ninguno.</item>
/// </list>
/// La máquina es la del documento electrónico reducida: el evento no tiene contingencia (una DIAN caída lo deja <c>Sent</c> y se consulta),
/// ni representación gráfica ni entrega al comprador; su reintento tras un rechazo es una versión nueva con el mismo número. (nuevo)
/// </summary>
public sealed class CicloDelEventoRadian(
    IApplicationDbContext db,
    IEnumerable<IFuenteDeDocumentoElectronico> fuentes,
    ConstructorDelCanonico constructor,
    GuardadoDeArtefactos? artefactos = null,
    ILogger<CicloDelEventoRadian>? logger = null)
{
    /// <summary>¿El documento electrónico es un evento RADIAN?</summary>
    public static bool EsEvento(ElectronicDocumentKind tipo) => tipo is ElectronicDocumentKind.RadianEvent030 or ElectronicDocumentKind.RadianEvent032;

    /// <summary>El instante sellado del evento, con −05:00 (el <c>IssuedAt</c> guardado va en UTC y sin fracciones).</summary>
    public static DateTimeOffset InstanteDe(ElectronicDocument documento) =>
        new DateTimeOffset(DateTime.SpecifyKind(documento.IssuedAt, DateTimeKind.Utc)).ToOffset(IDateTimeService.DesfaseColombia);

    /// <summary>La fuente del módulo sellado; nula si no hay una registrada.</summary>
    public IFuenteDeDocumentoElectronico? Fuente(string modulo) =>
        fuentes.FirstOrDefault(f => string.Equals(f.SourceModule, modulo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Vuelve a armar el evento con lo sellado en el documento.</summary>
    public async Task<Result<EventoConstruido>> ReconstruirAsync(ElectronicDocument documento, CancellationToken ct)
    {
        var fuente = Fuente(documento.SourceModule);
        if (fuente is null)
            return Result.Failure<EventoConstruido>(ReconstruccionDelCanonico.SourceUnknownCode,
                $"No hay un módulo fuente «{documento.SourceModule}» registrado para el evento {documento.Number}.");
        var entrada = await fuente.LeerEventoRadianAsync(documento.SourceDocumentPublicId, documento.Kind, ct);
        if (entrada.IsFailure) return Result.Failure<EventoConstruido>(entrada.Error);

        var configuracion = documento.EmissionSetting
            ?? await db.ElectronicEmissionSettings.AsNoTracking().FirstAsync(s => s.Id == documento.EmissionSettingId, ct);
        return await constructor.ConstruirEventoAsync(entrada.Value,
            new NumeracionDelEvento(configuracion, documento.Prefix, documento.Consecutive, InstanteDe(documento)), ct);
    }

    /// <summary>El evento de la versión, comprobado contra su SHA-256; si la versión no tiene el canónico, lo sube.</summary>
    public async Task<Result<EventoRadianCanonico>> VerificadoAsync(ElectronicDocument documento, ElectronicDocumentVersion version, CancellationToken ct)
    {
        var rehecho = await ReconstruirAsync(documento, ct);
        if (rehecho.IsFailure)
        {
            logger?.LogCritical("[FE.EventoSinReconstruir] El evento {Numero} v{Version} no se pudo volver a armar: {Codigo} {Mensaje}",
                documento.Number, version.VersionNumber, rehecho.Error.Code, rehecho.Error.Message);
            return Result.Failure<EventoRadianCanonico>(rehecho.Error);
        }
        if (!string.Equals(rehecho.Value.CanonicalSha256, version.CanonicalSha256, StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogCritical("[FE.EventoNoCoincide] El evento {Numero} v{Version} reconstruido da {Obtenido} y se registró {Esperado}: no se emite.",
                documento.Number, version.VersionNumber, rehecho.Value.CanonicalSha256, version.CanonicalSha256);
            return Result.Failure<EventoRadianCanonico>(ErroresDeDocumentosElectronicos.CanonicalMismatch(documento.Number));
        }

        if (artefactos is not null && version.CanonicalAttachmentPublicId is null
            && await artefactos.GuardarAsync(documento, version.VersionNumber, ArtefactosElectronicos.Canonical, GuardadoDeArtefactos.TipoJson,
                Encoding.UTF8.GetBytes(rehecho.Value.Json), ct) is { } id)
            version.FijarArtefacto(ArtefactoDeVersion.Canonico, id);
        return Result.Success(rehecho.Value.Evento);
    }

    /// <summary>
    /// Con la respuesta definitiva (validado, validado con notificaciones o rechazado), el evento del módulo pasa a <c>Emitted</c> o a
    /// <c>Rejected</c>. Nunca tumba el intento: el resultado del canal ya quedó registrado; si falla, queda en la bitácora.
    /// </summary>
    public async Task CerrarAsync(ElectronicDocument documento, CancellationToken ct)
    {
        var validado = documento.Status is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices;
        if (!validado && documento.Status != ElectronicDocumentStatus.Rejected) return;
        var fuente = Fuente(documento.SourceModule);
        if (fuente is null) return;
        try
        {
            var r = await fuente.RegistrarResultadoDeEventoRadianAsync(documento.SourceDocumentPublicId, documento.Kind,
                new ResultadoDeEventoRadian(validado, documento.UniqueCode, documento.IssueDate), ct);
            if (r.IsFailure)
                logger?.LogWarning("[FE.EventoSinCerrar] {Numero}: {Codigo} {Mensaje}", documento.Number, r.Error.Code, r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "[FE.EventoSinCerrar] {Numero}: el módulo no registró la respuesta; se ve en la bandeja.", documento.Number);
        }
    }
}
