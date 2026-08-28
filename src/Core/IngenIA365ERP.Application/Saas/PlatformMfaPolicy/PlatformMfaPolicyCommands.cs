using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Saas.PlatformMfaPolicy;

/// <summary>
/// Qué métodos de segundo factor acepta la plataforma para el maestro.
/// <b>No hay campo para «si se exige»</b>: eso es fijo. Ver
/// <see cref="Domain.Entities.Admin.PlatformMfaPolicy"/>.
/// </summary>
public sealed record GetPlatformMfaPolicyQuery : IRequest<Result<PlatformMfaPolicyDto>>;

/// <param name="RescateActivo">
/// Si el interruptor de configuración está puesto. Se devuelve para que la
/// pantalla lo diga en voz alta: mientras esté activo la política guardada no se
/// aplica, y no saberlo llevaría a alguien a creer que la restricción funciona
/// cuando no lo hace.
/// </param>
public sealed record PlatformMfaPolicyDto(
    IReadOnlyList<string> MetodosAceptados,
    bool RescateActivo,
    DateTime? ChangedAt);

public sealed class GetPlatformMfaPolicyQueryHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IPoliticaDePlataforma politica)
    : IRequestHandler<GetPlatformMfaPolicyQuery, Result<PlatformMfaPolicyDto>>
{
    public async Task<Result<PlatformMfaPolicyDto>> Handle(
        GetPlatformMfaPolicyQuery request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsGlobalMasterAdmin)
        {
            return Result.Failure<PlatformMfaPolicyDto>(
                "Saas.MasterOnly", "Sólo el administrador maestro puede ver esta política.");
        }

        var fila = await adminDb.PlatformMfaPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Scope == Domain.Entities.Admin.PlatformMfaPolicy.FilaUnica, ct);

        // Lo que devuelve la política EFECTIVA, no lo que dice la fila: con el
        // rescate activo son cosas distintas, y mostrar la fila sin más haría creer
        // que la restricción está en vigor.
        var efectivos = await politica.MetodosAceptadosAsync(ct);
        var guardados = fila?.AllowedMethodsMask ?? ConversionDeMetodosMfa.Todos;

        return Result.Success(new PlatformMfaPolicyDto(
            MetodosAceptados: ConversionDeMetodosMfa.ALiterales(efectivos),
            RescateActivo: efectivos != guardados,
            ChangedAt: fila?.ChangedAt));
    }
}

public sealed record UpdatePlatformMfaPolicyCommand(IReadOnlyList<string> MetodosAceptados)
    : IRequest<Result<PlatformMfaPolicyDto>>;

public sealed class UpdatePlatformMfaPolicyCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMfaDirectory credenciales,
    IPoliticaDePlataforma politica,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<UpdatePlatformMfaPolicyCommandHandler> logger)
    : IRequestHandler<UpdatePlatformMfaPolicyCommand, Result<PlatformMfaPolicyDto>>
{
    public async Task<Result<PlatformMfaPolicyDto>> Handle(
        UpdatePlatformMfaPolicyCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsGlobalMasterAdmin)
        {
            return Result.Failure<PlatformMfaPolicyDto>(
                "Saas.MasterOnly", "Sólo el administrador maestro puede cambiar esta política.");
        }

        var pedidos = ConversionDeMetodosMfa.DesdeLiterales(request.MetodosAceptados);
        if (pedidos == MetodosMfa.Ninguno)
        {
            return Result.Failure<PlatformMfaPolicyDto>(
                "Validation.PlatformMfaPolicy.SinMetodos",
                "Tenés que aceptar al menos un método. El segundo factor del maestro es " +
                "obligatorio, y sin ningún método aceptado te quedarías sin poder entrar.");
        }

        var actor = currentUser.CentralUserId!.Value;

        // Antes de guardar: ¿te quedarías vos mismo fuera? Es la única cuenta sin
        // rescate por la aplicación, así que la comprobación va aquí, delante, y no
        // en forma de sorpresa en el siguiente login.
        var losQueTiene = await credenciales.MetodosActivosAsync(actor, ct);
        if ((losQueTiene & pedidos) == MetodosMfa.Ninguno)
        {
            return Result.Failure<PlatformMfaPolicyDto>(
                "Saas.PlatformMfaPolicy.TeDejariaFuera",
                $"No tenés ningún autenticador de los que esa política aceptaría " +
                $"({string.Join(", ", request.MetodosAceptados)}). Inscribí uno primero: " +
                "sos la única cuenta que no puede rescatar nadie más.");
        }

        var fila = await adminDb.PlatformMfaPolicies
            .FirstOrDefaultAsync(p => p.Scope == Domain.Entities.Admin.PlatformMfaPolicy.FilaUnica, ct);

        var anterior = fila?.AllowedMethodsMask ?? ConversionDeMetodosMfa.Todos;

        if (fila is null)
        {
            fila = Domain.Entities.Admin.PlatformMfaPolicy.Inicial();
            adminDb.PlatformMfaPolicies.Add(fila);
        }

        var now = clock.UtcNow;
        fila.PermitirMetodos(pedidos, actor, now);
        await adminDb.SaveChangesAsync(ct);

        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: actor.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.PlatformMfaPolicyMethodsChanged,
                EntityType: nameof(Domain.Entities.Admin.PlatformMfaPolicy),
                EntityPublicId: fila.PublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: System.Text.Json.JsonSerializer.Serialize(
                    new { metodos = ConversionDeMetodosMfa.ALiterales(anterior) }),
                NewValuesJson: System.Text.Json.JsonSerializer.Serialize(
                    new { metodos = ConversionDeMetodosMfa.ALiterales(pedidos) }),
                ChangedFields: new[] { "AllowedMethodsMask" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/saas/mfa-policy",
                HttpMethod: "PUT", HttpStatusCode: 200, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló al cambiar la política de la plataforma.");
        }

        var efectivos = await politica.MetodosAceptadosAsync(ct);

        return Result.Success(new PlatformMfaPolicyDto(
            MetodosAceptados: ConversionDeMetodosMfa.ALiterales(efectivos),
            RescateActivo: efectivos != pedidos,
            ChangedAt: fila.ChangedAt));
    }
}
