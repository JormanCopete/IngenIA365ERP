using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Correo;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Notifications;

/// <summary>
/// El remitente de la plataforma <b>sin</b> el modo de prueba: sólo para reenviar lo que una persona ya decidió reenviar desde
/// «Correos retenidos». Todo lo demás usa <see cref="IEmailSender"/>, que pasa por <see cref="CorreoSegunModoDeEnvio"/>. (nuevo)
/// </summary>
public interface IRemitenteDirecto : IEmailSender;

/// <summary>El SMTP de la plataforma, tal cual. (nuevo)</summary>
public sealed class RemitenteDirecto(IEmailSender smtp) : IRemitenteDirecto
{
    public Task SendAsync(EmailMessage message, CancellationToken ct) => smtp.SendAsync(message, ct);
}

/// <summary>El modo de prueba vigente y cuántos correos esperan reenvío: lo lee la franja de aviso del encabezado. (nuevo)</summary>
public sealed record ModoDeCorreoDto(string Mode, string? TestInbox, bool KeepPending, int PendingCount)
{
    public bool IsTestMode => Mode != ParametrosDeCorreo.Enviar;
}

public sealed record CorreoRetenidoDto(
    Guid PublicId, string Mode, string Status, string OriginalTo, string? RedirectedTo, string Subject, int AttachmentCount,
    DateTime CreatedAt, string? CreatedBy, DateTime? ResentAt, string? ResentBy);

public sealed record GetModoDeCorreoQuery : IRequest<Result<ModoDeCorreoDto>>;

public sealed class GetModoDeCorreoQueryHandler(IApplicationDbContext db, ILectorDeParametros lector, IDateTimeService reloj)
    : IRequestHandler<GetModoDeCorreoQuery, Result<ModoDeCorreoDto>>
{
    public async Task<Result<ModoDeCorreoDto>> Handle(GetModoDeCorreoQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var envio = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.EnvioATerceros, hoy, ct: ct);
        if (envio.IsFailure) return Result.Failure<ModoDeCorreoDto>(envio.Error);
        var buzon = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.BuzonDePrueba, hoy, ct: ct);
        var pendientes = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.SuspendidosQuedanPendientes, hoy, ct: ct);
        var cuantos = await db.HeldEmails.CountAsync(h => h.Status == HeldEmailStatus.Pending, ct);
        return new ModoDeCorreoDto(envio.Value.Texto, buzon.IsSuccess ? buzon.Value.Texto : null,
            pendientes.IsSuccess && pendientes.Value.Valor is true, cuantos);
    }
}

/// <summary>Los correos retenidos o desviados, los más recientes primero; <paramref name="Status"/> nulo = todos. (nuevo)</summary>
public sealed record ListCorreosRetenidosQuery(HeldEmailStatus? Status = null, int Take = 500) : IRequest<Result<IReadOnlyList<CorreoRetenidoDto>>>;

public sealed class ListCorreosRetenidosQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListCorreosRetenidosQuery, Result<IReadOnlyList<CorreoRetenidoDto>>>
{
    public async Task<Result<IReadOnlyList<CorreoRetenidoDto>>> Handle(ListCorreosRetenidosQuery request, CancellationToken ct)
    {
        var consulta = db.HeldEmails.AsNoTracking();
        if (request.Status is { } estado) consulta = consulta.Where(h => h.Status == estado);
        var filas = await consulta.OrderByDescending(h => h.Id).Take(Math.Clamp(request.Take, 1, 2000))
            .Select(h => new CorreoRetenidoDto(h.PublicId, h.Mode.ToString(), h.Status.ToString(), h.OriginalTo, h.RedirectedTo, h.Subject,
                h.AttachmentCount, h.CreatedAt, h.CreatedBy, h.ResentAt, h.ResentBy))
            .ToListAsync(ct);
        return filas;
    }
}

/// <summary>Cuántos se reenviaron o descartaron. (nuevo)</summary>
public sealed record ResultadoDeRetenidosDto(int Processed, int Failed, IReadOnlyList<string> Errors);

public static class ErroresDeCorreosRetenidos
{
    public static Error SigueEnModoDePrueba(string modo) => new("Correo.ModoDePruebaActivo",
        $"El envío de correos está en «{modo}». Para reenviar los pendientes a sus destinatarios reales, primero ponga {ParametrosDeCorreo.EnvioATerceros} en «{ParametrosDeCorreo.Enviar}».");

    public static Error NadaQueProcesar() => new("Correo.SinPendientes", "No hay correos pendientes con esos identificadores.");
}

/// <summary>
/// Reenvía a su destinatario real los correos pendientes (<paramref name="PublicIds"/>; vacío = todos los pendientes). Sólo con el
/// envío ya en «Enviar»: con el modo de prueba encendido, reenviar sería justo lo que el modo evita. Cada uno por separado: el que
/// falla queda pendiente y se informa. (nuevo)
/// </summary>
public sealed record ResendCorreosRetenidosCommand(IReadOnlyList<Guid> PublicIds) : IRequest<Result<ResultadoDeRetenidosDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ResendCorreosRetenidosCommandHandler(
    IApplicationDbContext db, IRemitenteDirecto remitente, ILectorDeParametros lector, IDateTimeService reloj, ICurrentUserService usuario)
    : IRequestHandler<ResendCorreosRetenidosCommand, Result<ResultadoDeRetenidosDto>>
{
    public async Task<Result<ResultadoDeRetenidosDto>> Handle(ResendCorreosRetenidosCommand request, CancellationToken ct)
    {
        var envio = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.EnvioATerceros, reloj.HoyLocal, ct: ct);
        if (envio.IsFailure) return Result.Failure<ResultadoDeRetenidosDto>(envio.Error);
        if (envio.Value.Texto != ParametrosDeCorreo.Enviar)
            return Result.Failure<ResultadoDeRetenidosDto>(ErroresDeCorreosRetenidos.SigueEnModoDePrueba(envio.Value.Texto));

        var consulta = db.HeldEmails.Include(h => h.Attachments).Where(h => h.Status == HeldEmailStatus.Pending);
        if (request.PublicIds is { Count: > 0 } ids) consulta = consulta.Where(h => ids.Contains(h.PublicId));
        var pendientes = await consulta.OrderBy(h => h.Id).ToListAsync(ct);
        if (pendientes.Count == 0) return Result.Failure<ResultadoDeRetenidosDto>(ErroresDeCorreosRetenidos.NadaQueProcesar());

        var errores = new List<string>();
        var enviados = 0;
        foreach (var h in pendientes)
        {
            try
            {
                await remitente.SendAsync(new EmailMessage(h.OriginalTo, h.Subject, h.BodyHtml ?? string.Empty, h.BodyText, h.FromOverride,
                    h.Attachments.Select(a => new EmailAttachment(a.FileName, a.ContentType, a.Content)).ToList()), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errores.Add($"{h.OriginalTo} — {h.Subject}: {ex.GetBaseException().Message}");
                continue;
            }
            h.Status = HeldEmailStatus.Resent;
            h.ResentAt = reloj.UtcNow;
            h.ResentBy = usuario.UserName;
            Vaciar(h, reloj.UtcNow);
            enviados++;
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoDeRetenidosDto(enviados, errores.Count, errores);
    }

    /// <summary>Lo reenviado o descartado ya no necesita el cuerpo ni los adjuntos.</summary>
    internal static void Vaciar(HeldEmail h, DateTime ahora)
    {
        h.BodyHtml = null;
        h.BodyText = null;
        foreach (var a in h.Attachments)
        {
            a.IsDeleted = true;
            a.DeletedAt = ahora;
        }
    }
}

/// <summary>Descarta correos pendientes sin enviarlos: queda la constancia. (nuevo)</summary>
public sealed record DiscardCorreosRetenidosCommand(IReadOnlyList<Guid> PublicIds) : IRequest<Result<ResultadoDeRetenidosDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DiscardCorreosRetenidosCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<DiscardCorreosRetenidosCommand, Result<ResultadoDeRetenidosDto>>
{
    public async Task<Result<ResultadoDeRetenidosDto>> Handle(DiscardCorreosRetenidosCommand request, CancellationToken ct)
    {
        var consulta = db.HeldEmails.Include(h => h.Attachments).Where(h => h.Status == HeldEmailStatus.Pending);
        if (request.PublicIds is { Count: > 0 } ids) consulta = consulta.Where(h => ids.Contains(h.PublicId));
        var pendientes = await consulta.ToListAsync(ct);
        if (pendientes.Count == 0) return Result.Failure<ResultadoDeRetenidosDto>(ErroresDeCorreosRetenidos.NadaQueProcesar());
        foreach (var h in pendientes)
        {
            h.Status = HeldEmailStatus.Discarded;
            ResendCorreosRetenidosCommandHandler.Vaciar(h, reloj.UtcNow);
        }
        await db.SaveChangesAsync(ct);
        return new ResultadoDeRetenidosDto(pendientes.Count, 0, []);
    }
}
