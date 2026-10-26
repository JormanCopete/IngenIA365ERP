using System.Globalization;
using System.Net;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Notifications;

// Feature 015, R1, US3 (T0364; FR-020, FR-045, FR-177): el recibo de caja enviado al pagador. Sólo por correo y sólo con la autorización del canal del titular
// (IAutorizacionDeDatos, finalidad ContactChannel, canal Email); el contenido es el del documento impreso, que ya enmascara lo de otro titular cuando paga un tercero
// (FR-177). SMS y mensajería siguen simulados hasta que haya proveedor: se responde «simulado» y no sale nada. El egreso de caja lo usa igual (fase 26).

/// <summary>El resultado de un aviso: enviado, sin autorización, sin correo o simulado (SMS y mensajería). (nuevo)</summary>
public sealed record AvisoDeRecaudoDto(string Outcome, string Channel, string? Destination, string Message);

/// <summary>Los resultados de un aviso. (nuevo)</summary>
public static class ResultadosDeAviso
{
    public const string Enviado = "Sent";
    public const string SinAutorizacion = "NotAuthorized";
    public const string SinDestino = "NoDestination";
    public const string Simulado = "Simulated";
}

/// <summary>
/// <c>POST /api/receipts/receipts/{id}/notify</c> (<c>Receipts.Receipt.Print</c>) <b>(nuevo)</b>: envía el recibo al pagador por el canal pedido. Con correo: el PDF del
/// recibo en carta (código de verificación) al correo del pagador o al indicado, sólo si el pagador autorizó el canal; sin autorización o sin correo no sale nada y
/// se responde por qué. SMS y WhatsApp: simulados. Deja el evento <c>Receipts.Receipt.Notified</c> con el resultado (sin el contenido). (nuevo)
/// </summary>
public sealed record SendReceiptNoticeCommand(Guid ReceiptPublicId, ReceiptNoticeChannel Channel = ReceiptNoticeChannel.Email, string? Email = null)
    : IRequest<Result<AvisoDeRecaudoDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendReceiptNoticeCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IAutorizacionDeDatos autorizaciones,
    IDocumentosDeRecaudoEnPdf pdf,
    IEmailSender correo,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<SendReceiptNoticeCommand, Result<AvisoDeRecaudoDto>>
{
    public async Task<Result<AvisoDeRecaudoDto>> Handle(SendReceiptNoticeCommand request, CancellationToken ct)
    {
        var modelo = await new GetReceiptDocumentQueryHandler(db, alcance).Handle(new GetReceiptDocumentQuery(request.ReceiptPublicId, FormatoDeRecibo.Letter, Copy: true), ct);
        if (modelo.IsFailure) return Result.Failure<AvisoDeRecaudoDto>(modelo.Error);
        var recibo = await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.PublicId == request.ReceiptPublicId, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == recibo.PayerPersonId, ct);

        var aviso = await AvisosDeRecaudo.EnviarAsync(autorizaciones, correo, pagador.PublicId, pagador.Email, request.Channel, request.Email,
            $"Recibo {recibo.Number}", AvisosDeRecaudo.Cuerpo(modelo.Value.Company.Name, "recibo de caja", recibo.Number, recibo.ValueDate, recibo.TotalApplied,
                recibo.VerificationCode),
            () => new EmailAttachment($"recibo-{recibo.Number}.pdf", "application/pdf", pdf.Recibo(modelo.Value)), ct);
        await auditoria.AgregarAsync("Receipts.Receipt.Notified", "Receipt", recibo.PublicId, null,
            new { receipt = recibo.Number, channel = aviso.Channel, outcome = aviso.Outcome }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(aviso);
    }
}

/// <summary>El envío común de los avisos de Recaudo (recibo y egreso). (nuevo)</summary>
public static class AvisosDeRecaudo
{
    public const string CanalDeCorreo = nameof(ReceiptNoticeChannel.Email);

    public static async Task<AvisoDeRecaudoDto> EnviarAsync(IAutorizacionDeDatos autorizaciones, IEmailSender correo, Guid persona, string? correoDelMaestro,
        ReceiptNoticeChannel canal, string? correoIndicado, string asunto, string cuerpoHtml, Func<EmailAttachment> adjunto, CancellationToken ct)
    {
        if (canal is not ReceiptNoticeChannel.Email)
            return new AvisoDeRecaudoDto(ResultadosDeAviso.Simulado, canal.ToString(), null,
                "SMS y mensajería siguen simulados hasta que haya proveedor: el aviso no salió (FR-020).");
        var autorizada = await autorizaciones.VigenteAsync(persona, DataAuthorizationPurpose.ContactChannel, CanalDeCorreo, ct);
        if (autorizada is not { Autoriza: true })
            return new AvisoDeRecaudoDto(ResultadosDeAviso.SinAutorizacion, CanalDeCorreo, null,
                "La persona no autorizó el correo como canal de contacto: el aviso no se envía. Entregue el documento impreso.");
        var destino = string.IsNullOrWhiteSpace(correoIndicado) ? correoDelMaestro?.Trim() : correoIndicado.Trim();
        if (string.IsNullOrWhiteSpace(destino))
            return new AvisoDeRecaudoDto(ResultadosDeAviso.SinDestino, CanalDeCorreo, null, "La persona no tiene correo registrado: regístrelo o indíquelo.");
        await correo.SendAsync(new EmailMessage(destino, asunto, cuerpoHtml, Attachments: [adjunto()]), ct);
        return new AvisoDeRecaudoDto(ResultadosDeAviso.Enviado, CanalDeCorreo, Enmascarar(destino), "Aviso enviado por correo.");
    }

    /// <summary>El cuerpo del correo: sin saldos ni datos de otros titulares (FR-177); el detalle va en el PDF, que ya enmascara.</summary>
    public static string Cuerpo(string empresa, string documento, string numero, DateOnly fecha, decimal valor, string codigo) =>
        string.Create(CultureInfo.InvariantCulture,
            $"<p>{WebUtility.HtmlEncode(empresa)} le envía el {documento} {WebUtility.HtmlEncode(numero)} del {fecha:dd/MM/yyyy} por {valor:N0}.</p>" +
            $"<p>Código de verificación: <strong>{WebUtility.HtmlEncode(codigo)}</strong>. El documento va adjunto.</p>");

    private static string Enmascarar(string correo)
    {
        var arroba = correo.IndexOf('@', StringComparison.Ordinal);
        return arroba <= 1 ? "***" : $"{correo[0]}***{correo[arroba..]}";
    }
}
