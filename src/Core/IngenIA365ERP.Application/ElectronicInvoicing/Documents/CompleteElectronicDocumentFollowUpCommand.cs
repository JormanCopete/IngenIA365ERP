using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Mensajeria;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>Qué hizo el seguimiento de un documento (feature 023). (nuevo)</summary>
/// <param name="Hecho">Verdadero si quedó al día (nada pendiente).</param>
/// <param name="Detalle">Por qué no hizo nada o qué quedó pendiente.</param>
public sealed record SeguimientoHechoDto(Guid ElectronicDocumentPublicId, bool Hecho, string? Detalle);

/// <summary>
/// El seguimiento durable de un documento electrónico (feature 023; research R7; data-model §1): lo que antes corría en línea —dentro del
/// cobro o del intento ante el canal— y no tiene por qué esperar a nadie. Lo envían el manejador del aviso <c>fe.seguimiento</c> y el
/// barrido de respaldo cuando vence <c>FollowUpDueAt</c>. Ver <see cref="SeguimientoDelDocumento"/>.
/// </summary>
public sealed record CompleteElectronicDocumentFollowUpCommand(Guid ElectronicDocumentPublicId) : IRequest<Result<SeguimientoHechoDto>>;

public sealed class CompleteElectronicDocumentFollowUpCommandValidator : AbstractValidator<CompleteElectronicDocumentFollowUpCommand>
{
    public CompleteElectronicDocumentFollowUpCommandValidator() => RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
}

public sealed class CompleteElectronicDocumentFollowUpCommandHandler(SeguimientoDelDocumento seguimiento)
    : IRequestHandler<CompleteElectronicDocumentFollowUpCommand, Result<SeguimientoHechoDto>>
{
    public Task<Result<SeguimientoHechoDto>> Handle(CompleteElectronicDocumentFollowUpCommand request, CancellationToken ct) =>
        seguimiento.CompletarAsync(request.ElectronicDocumentPublicId, ct);
}

/// <summary>
/// Hace el seguimiento de un documento electrónico (feature 023; research R7), idempotente por las marcas que ya existen:
/// <list type="number">
/// <item>sube el canónico si la versión no lo tiene (el intento también lo sube: en contingencia 03 o con el canal caído, el intento tarda);</item>
/// <item>si el documento ya se entrega (validado, o en contingencia 04), genera y guarda la representación gráfica si la versión no la tiene y
/// entrega al comprador si <c>EmailSentAt</c> está vacío (<see cref="EntregaAlComprador"/>, nunca dos correos).</item>
/// </list>
/// Toma el arrendamiento de la fila sin respetar la espera del canal (lo que hace no habla con el canal); si lo tiene otro —un intento en
/// curso—, no hace nada y lo retoma el barrido. Si algo falla, cuenta un intento con las mismas esperas del canal y, al llegar a
/// <c>Dian.TopeDeIntentos</c>, queda <c>FollowUpDeadAt</c> con la alerta <c>Dian.SeguimientoDetenido</c>. Los eventos RADIAN no tienen
/// representación ni entrega. Scoped. (nuevo)
/// </summary>
public sealed class SeguimientoDelDocumento(
    IApplicationDbContext db,
    IArrendamientoDeDocumentoElectronico arrendamiento,
    GuardadoDeArtefactos artefactos,
    EsperasDeReintento esperas,
    TopeDeIntentos topes,
    IAlertas alertas,
    IDateTimeService reloj,
    ILogger<SeguimientoDelDocumento> logger,
    GeneracionDeRepresentacion? representacion = null,
    EntregaAlComprador? entrega = null)
{
    /// <summary>Cuánto después de registrarse se agenda el primer seguimiento: después del primer intento ante el canal.</summary>
    public static readonly TimeSpan PrimeraEspera = TimeSpan.FromMinutes(1);

    private readonly string _dueno = Recortar($"seguimiento:{System.Environment.MachineName}:{Guid.NewGuid():N}", 100);

    public async Task<Result<SeguimientoHechoDto>> CompletarAsync(Guid publicId, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var previo = await db.ElectronicDocuments.AsNoTracking().Where(d => d.PublicId == publicId)
            .Select(d => new { d.Id, d.FollowUpDueAt, d.FollowUpDeadAt }).FirstOrDefaultAsync(ct);
        if (previo is null) return Result.Failure<SeguimientoHechoDto>(ErroresDeDocumentosElectronicos.NotFound());
        if (previo.FollowUpDueAt is null || previo.FollowUpDeadAt is not null || previo.FollowUpDueAt > ahora)
            return Hecho(publicId, false, "No tiene seguimiento pendiente todavía.");

        if (!await arrendamiento.TomarAsync(previo.Id, ahora, ahora + esperas.Arrendamiento, _dueno, respetarEspera: false, ct))
            return Hecho(publicId, false, "Otro proceso tiene el documento; se retoma después.");

        var documento = await db.ElectronicDocuments.Include(d => d.Versions).Include(d => d.EmissionSetting)
            .FirstAsync(d => d.Id == previo.Id, ct);
        try
        {
            var pendiente = await HacerAsync(documento, ct);
            if (pendiente is null)
            {
                documento.FollowUpDueAt = null;
                documento.FollowUpAttempts = 0;
                documento.FollowUpLastError = null;
                if (documento.FollowUpDeadAt is null) await alertas.AtenderPorProcesoAsync(ClaveDeAlerta(documento), "El seguimiento terminó.", ct);
                return Hecho(publicId, true, null);
            }
            await ContarFallaAsync(documento, pendiente, ahora, ct);
            return Hecho(publicId, false, pendiente);
        }
        finally
        {
            if (documento.LeaseOwner == _dueno)
            {
                documento.LeaseUntil = null;
                documento.LeaseOwner = null;
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>Lo que hace; nulo si quedó al día, o el motivo de lo que falló.</summary>
    private async Task<string?> HacerAsync(ElectronicDocument documento, CancellationToken ct)
    {
        if (CicloDelEventoRadian.EsEvento(documento.Kind)) return null;
        var version = documento.Versions.FirstOrDefault(v => v.Id == documento.CurrentVersionId)
                      ?? documento.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (version is null) return null;

        try
        {
            if (version.CanonicalAttachmentPublicId is null)
            {
                var canonico = await artefactos.CanonicoVerificadoAsync(documento, version, ct);
                if (canonico.IsFailure) return $"Canónico: {canonico.Error.Message}";
                if (version.CanonicalAttachmentPublicId is null) return "Canónico: el almacén de adjuntos no lo guardó.";
            }

            if (!EntregaAlComprador.Entregable(documento.Status)) return null; // lo vuelve a agendar el intento que lo deje entregable

            byte[]? pdf = null;
            if (representacion is not null && version.GraphicPdfAttachmentPublicId is null)
            {
                pdf = await representacion.GenerarAsync(documento, version, ct);
                if (version.GraphicPdfAttachmentPublicId is null && pdf is not null)
                    return "Representación gráfica: el almacén de adjuntos no la guardó.";
            }

            if (entrega is not null && documento.EmailSentAt is null && documento.DeliveredAt is null)
            {
                var r = await entrega.EntregarAsync(documento, version, pdf, null, forzar: false, ct);
                if (r.IsFailure) return $"Entrega al comprador: {r.Error.Message}";
            }
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "[FE.SeguimientoFallido] {Numero} ({DocumentoPublicId}): se reintenta.", documento.Number, documento.PublicId);
            return ex.Message;
        }
    }

    private async Task ContarFallaAsync(ElectronicDocument documento, string motivo, DateTime ahora, CancellationToken ct)
    {
        documento.FollowUpAttempts++;
        documento.FollowUpLastError = motivo.Length <= 500 ? motivo : motivo[..500];
        var tope = await topes.DeDocumentosAsync(ct);
        if (documento.FollowUpAttempts < tope)
        {
            documento.FollowUpDueAt = ahora + esperas.Tras(documento.FollowUpAttempts);
            logger.LogWarning("[FE.SeguimientoPendiente] {Numero} ({DocumentoPublicId}) intento {Intento} de {Tope}: {Motivo}",
                documento.Number, documento.PublicId, documento.FollowUpAttempts, tope, motivo);
            return;
        }

        documento.FollowUpDueAt = null;
        documento.FollowUpDeadAt = ahora;
        logger.LogError("[FE.SeguimientoDetenido] {Numero} ({DocumentoPublicId}) agotó {Tope} intentos: {Motivo}",
            documento.Number, documento.PublicId, tope, motivo);
        await alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.SeguimientoDetenido,
            $"Documento electrónico {documento.Number}: seguimiento detenido",
            $"No se pudo completar la representación o la entrega al comprador tras {tope} intentos. Último motivo: {documento.FollowUpLastError}. " +
            "Reprocéselo en Administración › Mensajes muertos.",
            EntityType: GuardadoDeArtefactos.EntidadDelDocumento,
            EntityPublicId: documento.PublicId,
            DedupKey: ClaveDeAlerta(documento)), ct);
    }

    public static string ClaveDeAlerta(ElectronicDocument documento) => $"{TiposDeAlerta.SeguimientoDetenido}:{documento.PublicId:N}";

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<SeguimientoHechoDto> Hecho(Guid id, bool hecho, string? detalle) => Result.Success(new SeguimientoHechoDto(id, hecho, detalle));
}
