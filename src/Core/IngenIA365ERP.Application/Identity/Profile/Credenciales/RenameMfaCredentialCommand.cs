using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.Credenciales;

/// <summary>
/// Le pone nombre a una credencial. Sin esto, con dos autenticadores la pantalla
/// muestra dos filas indistinguibles y revocar se vuelve una apuesta.
/// </summary>
public sealed record RenameMfaCredentialCommand(Guid CredencialPublicId, string? Label)
    : IRequest<Result>;

public sealed class RenameMfaCredentialCommandValidator : AbstractValidator<RenameMfaCredentialCommand>
{
    public RenameMfaCredentialCommandValidator()
    {
        // Se admite vacío: quitarle el nombre a una credencial es legítimo. Lo que
        // no se admite es pasarse del ancho de la columna, que truncaría en
        // silencio.
        RuleFor(x => x.Label)
            .MaximumLength(100)
            .WithMessage("El nombre no puede pasar de 100 caracteres.");
    }
}

public sealed class RenameMfaCredentialCommandHandler(
    ICurrentCentralUserContext currentUser,
    IMfaDirectory credenciales,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RenameMfaCredentialCommandHandler> logger)
    : IRequestHandler<RenameMfaCredentialCommand, Result>
{
    public async Task<Result> Handle(RenameMfaCredentialCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");

        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.WrongTokenPurpose", "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;

        var renombrada = await credenciales.RenombrarAsync(
            centralUserId, request.CredencialPublicId, request.Label, clock.UtcNow, ct);

        if (!renombrada)
        {
            return Result.Failure(
                "Profile.Mfa.CredentialNotFound", "Esa credencial no existe o no es tuya.");
        }

        await EmitirAuditoriaAsync(centralUserId, request.CredencialPublicId, clock.UtcNow, ct);
        return Result.Success();
    }

    private async Task EmitirAuditoriaAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime ahora, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.ProfileMfaCredentialRenamed,
                EntityType: nameof(Domain.Entities.Admin.MfaCredential),
                EntityPublicId: credencialPublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/profile/mfa/credentials/{publicId}",
                HttpMethod: "PATCH",
                HttpStatusCode: 204,
                DurationMs: null,
                OccurredAt: ahora), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}",
                AuditEventTypes.ProfileMfaCredentialRenamed);
        }
    }
}
