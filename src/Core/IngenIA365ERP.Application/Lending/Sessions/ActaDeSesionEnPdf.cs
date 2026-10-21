using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

// Feature 013, T0297 (US17; T64, T73, C48; api.md §5, data-model §3.8): el PDF del acta de una sesión. (nuevo)

/// <summary>
/// Lo que se imprime del acta: la sesión tal como la devuelve <c>GET /sessions/{id}</c>, la cooperativa y las dos marcas de agua —«SIN CERRAR»
/// si la sesión no está cerrada y «AMBIENTE DE PRUEBAS – SIN VALIDEZ» fuera de producción (T73)—. (nuevo)
/// </summary>
public sealed record ActaParaImprimir(SessionDto Acta, string Cooperativa, bool SinCerrar, bool AmbienteDePruebas)
{
    public const string MarcaSinCerrar = "SIN CERRAR";
    public const string MarcaDePruebas = "AMBIENTE DE PRUEBAS – SIN VALIDEZ";
}

/// <summary>
/// Pinta el acta en PDF (órgano, número, fecha, asistentes, quórum, puntos con la regla y los excluidos, votos por persona, resultado,
/// condiciones, la explicación del conteo —el fundamento de los vinculados— y la huella). La implementación con QuestPDF
/// (<c>ActaDeSesionReport</c>) la registra la API, la única capa que conoce la librería. No calcula nada. (nuevo)
/// </summary>
public interface IRenderizadorDeActaDeSesion
{
    byte[] Render(ActaParaImprimir acta);
}

/// <summary>El PDF del acta para descargar: contenido y nombre de archivo. (nuevo)</summary>
public sealed record ActaEnPdfDto(byte[] Contenido, string NombreArchivo);

/// <summary>Cómo se nombra y se arma el acta para imprimir. (nuevo)</summary>
public static class ActasEnPdf
{
    /// <summary><c>acta-{número}.pdf</c>; sin número (abierta), <c>acta-borrador-{sesión}.pdf</c>.</summary>
    public static string NombreDe(SessionDto acta) =>
        acta.Number is { Length: > 0 } numero
            ? $"acta-{numero.ToLowerInvariant()}.pdf"
            : $"acta-borrador-{acta.PublicId:N}.pdf";

    public static ActaParaImprimir Armar(SessionDto acta, ICurrentTenantService? cooperativa, IAmbienteDeDespliegue? ambiente) =>
        new(acta, cooperativa?.TenantName ?? "Cooperativa", acta.Status != SessionStatus.Closed, ambiente?.EsProduccion != true);
}

/// <summary>
/// El <see cref="IActaDeSesionEnPdf"/> de la API (T0297): al cerrar, pinta el acta con <see cref="IRenderizadorDeActaDeSesion"/> y la guarda
/// por <see cref="UploadAttachmentCommand"/> como adjunto <b>de módulo, no borrable</b> (<see cref="AdjuntosDeModulo.ActaDeSesionDeCartera"/>,
/// formato <c>Direct</c>, ya <c>Available</c>), con dueño la sesión. Un fallo de la subida se devuelve: el cierre es todo o nada. Sin ambiente
/// declarado se marca como de pruebas (el defecto que no da validez a un papel). (nuevo)
/// </summary>
public sealed class ActaDeSesionGuardada(
    IRenderizadorDeActaDeSesion renderizador,
    ISender sender,
    ICurrentTenantService cooperativa,
    IAmbienteDeDespliegue? ambiente = null) : IActaDeSesionEnPdf
{
    public async Task<Result<Guid?>> GuardarAsync(SessionDto acta, CancellationToken ct)
    {
        var pdf = renderizador.Render(ActasEnPdf.Armar(acta, cooperativa, ambiente));
        var subida = await sender.Send(new UploadAttachmentCommand(
            AdjuntosDeModulo.ActaDeSesionDeCartera, acta.PublicId, ActasEnPdf.NombreDe(acta), "application/pdf", pdf), ct);
        return subida.IsSuccess ? Result.Success<Guid?>(subida.Value) : Result.Failure<Guid?>(subida.Error);
    }
}

/// <summary>
/// El PDF del acta (api.md §5, <c>GET /api/lending/sessions/{id}/minutes.pdf</c>, <c>Lending.Sessions.Read</c>): la sellada si está cerrada
/// —el mismo contenido que se guardó como adjunto al cerrarla— o el borrador marcado «SIN CERRAR». Es una lectura sensible: deja
/// <c>Lending.Session.Viewed</c> con <c>pdf = true</c> por <see cref="LendingAuditEmitter"/>. El acta es del órgano, no de una oficina: el
/// alcance lo aplica cada historia sobre sus sujetos. (nuevo)
/// </summary>
public sealed record GetSessionMinutesPdfQuery(Guid SessionPublicId) : IRequest<Result<ActaEnPdfDto>>;

public sealed class GetSessionMinutesPdfQueryValidator : AbstractValidator<GetSessionMinutesPdfQuery>
{
    public GetSessionMinutesPdfQueryValidator() => RuleFor(x => x.SessionPublicId).NotEmpty();
}

public sealed class GetSessionMinutesPdfQueryHandler(
    IApplicationDbContext db,
    AsuntosDeActa asuntos,
    LendingAuditEmitter auditoria,
    IRenderizadorDeActaDeSesion renderizador,
    ICurrentTenantService cooperativa,
    IDateTimeService reloj,
    IAmbienteDeDespliegue? ambiente = null) : IRequestHandler<GetSessionMinutesPdfQuery, Result<ActaEnPdfDto>>
{
    public async Task<Result<ActaEnPdfDto>> Handle(GetSessionMinutesPdfQuery request, CancellationToken ct)
    {
        var sesion = await db.LendingSessions.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.SessionPublicId && !s.IsDeleted, ct);
        if (sesion is null) return Result.Failure<ActaEnPdfDto>(ErroresDeGobierno.SesionInexistente());

        var estado = await EstadoDeSesion.CargarAsync(db, sesion, reloj.UtcNow, seguimiento: false, ct);
        string? actual = null;
        if (sesion.Status != SessionStatus.Closed)
        {
            var descritos = await ActasDeSesion.DescribirAsync(estado, asuntos, ct);
            if (descritos.IsSuccess) actual = estado.HuellaActual(descritos.Value.ToDictionary(d => d.Key, d => d.Value.ContentSha256));
        }

        var acta = await ActasDeSesion.DtoAsync(db, estado, actual, ct);
        var pdf = renderizador.Render(ActasEnPdf.Armar(acta, cooperativa, ambiente));
        await auditoria.EmitirActaVistaAsync(sesion.PublicId, sesion.Number, comoPdf: true, ct);
        return Result.Success(new ActaEnPdfDto(pdf, ActasEnPdf.NombreDe(acta)));
    }
}
