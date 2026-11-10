using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Domain.Enums.Connectors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Connectors;

/// <summary>Una llamada hecha en un intento: lo que se guarda como hecho en <c>COR_ConnectorCalls</c>. Nunca lleva credenciales ni token.</summary>
public sealed record LlamadaDeConector(
    ConnectorCallKind Kind,
    DateTime StartedAt,
    int DurationMs,
    int StatusCode,
    ConnectorOutcome Outcome,
    string? ResponseExcerpt,
    string? RequestBodySanitized,
    string? Url);

/// <summary>
/// Registra un intento de envío a un sistema externo (feature 020, R18; T160): las llamadas como hechos y el estado del envío del tablero.
/// Sin ruta (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoConectores"/>. La auditoría lo ve entero: por eso las
/// llamadas llevan el cuerpo <b>saneado</b> (sin las variables sensibles) y la del inicio de sesión no lleva respuesta (traería el token).
/// </summary>
public sealed record RegisterConnectorCallCommand(
    Guid MessagePublicId,
    IReadOnlyList<LlamadaDeConector> Llamadas,
    ConnectorRequestStatus Estado,
    string? Aviso = null) : IRequest<Result>, IReintentableAnteConcurrencia;

public sealed class RegisterConnectorCallCommandValidator : AbstractValidator<RegisterConnectorCallCommand>
{
    public RegisterConnectorCallCommandValidator() => RuleFor(x => x.MessagePublicId).NotEmpty();
}

public sealed class RegisterConnectorCallCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<RegisterConnectorCallCommand, Result>
{
    public async Task<Result> Handle(RegisterConnectorCallCommand request, CancellationToken ct)
    {
        var envio = await db.ConnectorRequests.FirstOrDefaultAsync(r => r.MessagePublicId == request.MessagePublicId, ct);
        if (envio is null) return Result.Failure("Connector.RequestNotFound", "No existe el envío de ese mensaje.");

        // Un envío anulado o cerrado a mano mientras salía no cambia de estado: queda el intento como hecho.
        var terminal = envio.Status is ConnectorRequestStatus.PaymentVoided or ConnectorRequestStatus.ResolvedManually;
        foreach (var l in request.Llamadas)
        {
            if (l.Kind == ConnectorCallKind.Operation) envio.Attempts++;
            db.ConnectorCalls.Add(new ConnectorCall
            {
                RequestId = envio.Id,
                AttemptNumber = Math.Max(envio.Attempts, 1),
                StartedAt = l.StartedAt,
                DurationMs = l.DurationMs,
                Kind = l.Kind,
                StatusCode = l.StatusCode,
                Outcome = l.Outcome,
                ResponseExcerpt = l.Kind == ConnectorCallKind.Login ? null : Recortar(l.ResponseExcerpt),
                RequestBodySanitized = l.Kind == ConnectorCallKind.Login ? null : Recortar(l.RequestBodySanitized),
                Url = Recortar(l.Url, 500),
                CreatedBy = "Proceso de integración",
            });
        }

        var ultima = request.Llamadas.LastOrDefault();
        if (ultima is not null)
        {
            envio.LastStatusCode = ultima.StatusCode;
            envio.LastResponseExcerpt = ultima.Kind == ConnectorCallKind.Login ? null : Recortar(ultima.ResponseExcerpt);
            envio.LastAttemptAt = ultima.StartedAt;
        }
        if (!terminal)
        {
            envio.Status = request.Estado;
            if (request.Estado is ConnectorRequestStatus.Sent or ConnectorRequestStatus.SentWithWarning) envio.SentAt ??= reloj.UtcNow;
            if (request.Aviso is not null) envio.Warning = Recortar(request.Aviso, 500);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string? Recortar(string? texto, int largo = RespuestaHttp.LargoDelExtracto) =>
        texto is null ? null : texto.Length <= largo ? texto : texto[..largo];
}
