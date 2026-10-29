using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Profile.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;

public sealed class RequestPasswordResetCommandHandler(
    ICentralIdentityProvider centralIdentity,
    ICanalDeRecuperacion canal,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RequestPasswordResetCommandHandler> logger)
    : IRequestHandler<RequestPasswordResetCommand, Result>
{
    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken ct)
    {
        var texto = request.Email.Trim();
        var now = clock.UtcNow;

        // Feature 017 (T081): «Correo o usuario», por el mismo resolvedor del ingreso. Un correo sin verificar
        // no identifica a nadie, igual que al entrar: el enlace nunca va a un buzón que nadie comprobó.
        var user = (await new Auth.ResolutorDeIdentificador(centralIdentity).ResolverAsync(texto, ct)).Cuenta;
        if (user is null)
        {
            // Inexistente: log + audit, pero responder éxito (FR-041).
            logger.LogInformation(
                "Password reset solicitado para un identificador sin cuenta: {Identificador}.", texto);
            await EmitAuditAsync(
                centralUserId: null,
                email: texto,
                action: AuditEventTypes.ProfilePasswordResetRequestedNoSuchEmail,
                request, now, ct);
            return Result.Success();
        }

        await canal.EnviarEnlaceAsync(user, request.IpAddress, ct);

        await EmitAuditAsync(
            centralUserId: user.Id,
            email: user.Email,
            action: AuditEventTypes.ProfilePasswordResetRequested,
            request, now, ct);

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid? centralUserId,
        string email,
        string action,
        RequestPasswordResetCommand request,
        DateTime now,
        CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId?.ToString("N") ?? string.Empty,
                UserName: email,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId?.ToString("N") ?? string.Empty,
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: null,
                Endpoint: "/api/auth/password/forgot",
                HttpMethod: "POST",
                HttpStatusCode: 202,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }
}
