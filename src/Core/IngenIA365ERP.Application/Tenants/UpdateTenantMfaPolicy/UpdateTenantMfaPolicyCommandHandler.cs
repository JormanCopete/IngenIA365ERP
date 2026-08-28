using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Memberships;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Tenants.UpdateTenantMfaPolicy;

public sealed class UpdateTenantMfaPolicyCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    IMfaDirectory credenciales,
    IMembershipChangedNotifier membershipNotifier,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<UpdateTenantMfaPolicyCommandHandler> logger)
    : IRequestHandler<UpdateTenantMfaPolicyCommand, Result<UpdateTenantMfaPolicyResult>>
{
    public async Task<Result<UpdateTenantMfaPolicyResult>> Handle(
        UpdateTenantMfaPolicyCommand request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(
            currentUser, adminDb, request.TenantPublicId, ct);
        if (!guard.IsAllowed)
            return Result.Failure<UpdateTenantMfaPolicyResult>(guard.ErrorCode!, guard.Message!);

        var tenant = await adminDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == request.TenantPublicId && t.IsActive, ct);
        if (tenant is null)
            return Result.Failure<UpdateTenantMfaPolicyResult>(
                "Tenant.NotFound", "La empresa no existe o está inactiva.");

        var policy = await adminDb.TenantMfaPolicies
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantPublicId, ct);

        var now = clock.UtcNow;
        var actor = currentUser.CentralUserId!.Value;

        var esFilaNueva = policy is null;
        if (policy is null)
        {
            policy = TenantMfaPolicy.CreateForTenant(request.TenantPublicId);
            adminDb.TenantMfaPolicies.Add(policy);
        }

        // null significa «no tocar los métodos». Ver el comentario del comando.
        var mascaraPedida = request.MetodosAceptados is null
            ? policy.AllowedMethodsMask
            : ConversionDeMetodosMfa.DesdeLiterales(request.MetodosAceptados);

        if (mascaraPedida == MetodosMfa.Ninguno && request.IsRequired)
        {
            return Result.Failure<UpdateTenantMfaPolicyResult>(
                "Validation.TenantMfaPolicy.SinMetodos",
                "Si exigís segundo factor tenés que aceptar al menos un método. " +
                "Sin ninguno, nadie de la cooperativa podría entrar ni tendría nada que inscribir.");
        }

        // El corto-circuito compara el estado COMPLETO y exige que la fila ya
        // existiera. Comparaba sólo IsRequired y salía antes del SaveChanges: con
        // eso, cambiar los métodos sin tocar la exigencia respondía 200, no
        // guardaba nada, no invalidaba ninguna caché, y la pantalla recargaba
        // mostrando el estado viejo sin un solo error. Y si la fila no existía,
        // se había añadido al ChangeTracker cuatro líneas antes y tampoco se
        // guardaba: el silencio era doble.
        var sinCambios =
            !esFilaNueva
            && policy.IsRequired == request.IsRequired
            && policy.AllowedMethodsMask == mascaraPedida;

        if (sinCambios)
        {
            return Result.Success(new UpdateTenantMfaPolicyResult(
                await ContarSinMetodoAceptadoAsync(request.TenantPublicId, mascaraPedida, ct)));
        }

        var cambioLaExigencia = policy.IsRequired != request.IsRequired;
        var cambiaronLosMetodos = policy.AllowedMethodsMask != mascaraPedida;
        var mascaraAnterior = policy.AllowedMethodsMask;

        // Los métodos PRIMERO: `Enable` se niega a exigir con la máscara vacía, y
        // si se llamara antes de fijarla podría rechazar una combinación que la
        // misma petición estaba a punto de volver válida.
        policy.PermitirMetodos(mascaraPedida);

        if (request.IsRequired) policy.Enable(actor, now);
        else policy.Disable(actor, now);

        var action = cambioLaExigencia
            ? (request.IsRequired
                ? AuditEventTypes.TenantMfaPolicyActivated
                : AuditEventTypes.TenantMfaPolicyDeactivated)
            : AuditEventTypes.TenantMfaPolicyMethodsChanged;

        await adminDb.SaveChangesAsync(ct);

        // Invalidar caché de membresías de TODOS los usuarios del tenant.
        await membershipNotifier.PublishForTenantMembersAsync(request.TenantPublicId, ct);

        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: request.TenantPublicId.ToString("N"),
                UserId: actor.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: action,
                EntityType: nameof(TenantMfaPolicy),
                EntityPublicId: request.TenantPublicId.ToString("N"),
                Module: "Identity",
                // Poblados de verdad: iban en null, y una auditoría que dice
                // «cambió la política» sin decir de qué a qué no sirve el día que
                // haya que reconstruir por qué media cooperativa se quedó fuera.
                OldValuesJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    isRequired = !cambioLaExigencia ? request.IsRequired : !request.IsRequired,
                    metodos = ConversionDeMetodosMfa.ALiterales(mascaraAnterior),
                }),
                NewValuesJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    isRequired = request.IsRequired,
                    metodos = ConversionDeMetodosMfa.ALiterales(mascaraPedida),
                }),
                ChangedFields: cambiaronLosMetodos
                    ? ["IsRequired", "AllowedMethodsMask"]
                    : new[] { "IsRequired" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/tenants/{tenantPublicId}/mfa-policy",
                HttpMethod: "PUT", HttpStatusCode: 200, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }

        var sinMetodo = await ContarSinMetodoAceptadoAsync(
            request.TenantPublicId, mascaraPedida, ct);

        if (sinMetodo > 0)
        {
            logger.LogWarning(
                "La política de {Tenant} deja a {Cuantos} persona(s) con segundo factor " +
                "pero sin ningún método aceptado; el login las mandará a inscribir.",
                request.TenantPublicId, sinMetodo);
        }

        return Result.Success(new UpdateTenantMfaPolicyResult(sinMetodo));
    }

    /// <summary>
    /// Cuántos miembros activos tienen segundo factor pero ninguno de los métodos
    /// que la máscara acepta. Es un COUNT barato y evita el incidente del lunes:
    /// la administradora ve el número ANTES de que esas personas se encuentren la
    /// pantalla de inscripción sin haberla pedido.
    ///
    /// <para>
    /// Con la máscara sin restricciones no hay nada que contar, y se evita la
    /// consulta entera — que es el caso de casi todas las cooperativas.
    /// </para>
    /// </summary>
    private async Task<int> ContarSinMetodoAceptadoAsync(
        Guid tenantPublicId, MetodosMfa aceptados, CancellationToken ct)
    {
        if (!Identity.Auth.Common.GuardiaDeMetodos.Restringe(aceptados)) return 0;

        var miembros = await adminDb.TenantMemberships
            .AsNoTracking()
            .Where(m => m.TenantId == tenantPublicId && m.Status == MembershipStatus.Active)
            .Select(m => m.CentralUserId)
            .ToListAsync(ct);

        if (miembros.Count == 0) return 0;

        return await credenciales.ContarSinNingunMetodoAceptadoAsync(miembros, aceptados, ct);
    }
}
