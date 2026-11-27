using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.PasesDeIngreso;

/// <summary>
/// Feature 027: lo común de conceder un pase, sea el administrador de la cooperativa o el maestro. Quién es el actor y si el destinatario es
/// administrador en la cooperativa lo resuelve el manejador que llama; aquí se leen los demás hechos, se aplican las
/// <see cref="ReglasDelPaseDeIngreso"/>, se reemplaza el vivo anterior, se guarda la huella y se avisa.
/// </summary>
public sealed class ConcesionDePaseDeIngreso(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext admin,
    ICentralIdentityProvider centralIdentity,
    IMfaDirectory credenciales,
    ISecureTokenGenerator tokens,
    IAvisosDelPaseDeIngreso avisos,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock,
    ILogger<ConcesionDePaseDeIngreso> logger)
{
    public async Task<Result<PaseConcedidoResult>> ConcederAsync(
        Guid cooperativa, Guid destinatario, bool destinatarioEsAdministradorEnLaCooperativa, TimeSpan duracion, string motivo,
        string accionDeAuditoria, CancellationToken ct)
    {
        var actor = currentUser.CentralUserId!.Value;
        var ahora = clock.UtcNow;

        var cuenta = await centralIdentity.FindByIdAsync(destinatario, ct);
        if (cuenta is null)
            return Result.Failure<PaseConcedidoResult>("Generic.NotFound", "El usuario no existe.");

        var tenant = await admin.Tenants.AsNoTracking()
            .Where(t => t.PublicId == cooperativa)
            .Select(t => new { t.Name, t.IsActive })
            .FirstOrDefaultAsync(ct);
        if (tenant is null || !tenant.IsActive)
            return Result.Failure<PaseConcedidoResult>("Tenant.NotFound", "La cooperativa no existe o está inactiva.");

        var politica = await admin.TenantMfaPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.TenantId == cooperativa, ct);
        var membresia = await admin.TenantMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.TenantId == cooperativa && m.CentralUserId == destinatario, ct);

        var desdeUsuario = ahora - ReglasDelPaseDeIngreso.VentanaPorUsuario;
        var desdeCooperativa = ahora - ReglasDelPaseDeIngreso.VentanaPorCooperativa;
        var alUsuario = await admin.LoginPasses.CountAsync(
            p => p.CentralUserId == destinatario && p.TenantId == cooperativa && p.GrantedAt > desdeUsuario, ct);
        var enLaCooperativa = await admin.LoginPasses.CountAsync(p => p.TenantId == cooperativa && p.GrantedAt > desdeCooperativa, ct);

        var rechazo = ReglasDelPaseDeIngreso.Evaluar(new ReglasDelPaseDeIngreso.Hechos(
            Actor: actor,
            ActorEsMaestro: currentUser.IsGlobalMasterAdmin,
            Destinatario: destinatario,
            DestinatarioEsMaestro: cuenta.IsGlobalMasterAdmin,
            DestinatarioEsAdministrador: destinatarioEsAdministradorEnLaCooperativa || membresia?.IsTenantAdmin == true,
            DestinatarioMiembroActivo: membresia?.Status == MembershipStatus.Active,
            DestinatarioActivo: cuenta.Status == CentralUserStatus.Active && !cuenta.IsDeleted,
            CorreoVerificado: cuenta.EmailConfirmed,
            TieneSegundoFactor: await credenciales.ContarActivasAsync(destinatario, ct) > 0,
            OpcionEncendida: politica?.AllowLoginPass == true,
            HorasMaximasDeLaCooperativa: politica?.LoginPassMaxHours ?? LoginPass.HorasMaximas,
            DuracionPedida: duracion,
            ConcedidosAlUsuarioEnLaVentana: alUsuario,
            ConcedidosEnLaCooperativaEnLaVentana: enLaCooperativa));
        if (rechazo is not null)
            return Result.Failure<PaseConcedidoResult>(rechazo.Codigo, rechazo.Mensaje);

        // Un solo pase vivo por persona y cooperativa: el anterior queda «reemplazado».
        var vivos = await admin.LoginPasses
            .Where(p => p.CentralUserId == destinatario && p.TenantId == cooperativa && p.VoidedAt == null && p.ExpiresAt > ahora)
            .ToListAsync(ct);
        foreach (var anterior in vivos) anterior.Anular(ahora, LoginPass.Motivos.Reemplazado, actor);

        var secreto = tokens.Generate();
        var pase = LoginPass.Conceder(destinatario, cooperativa, actor, currentUser.IsGlobalMasterAdmin, motivo, secreto.Sha256Hash,
            cuenta.SecurityStamp, ahora, duracion);
        admin.LoginPasses.Add(pase);
        await admin.SaveChangesAsync(ct);

        var concedidoPor = currentUser.IsGlobalMasterAdmin ? "El administrador de la plataforma" : currentUser.Email ?? "Un administrador";

        // Si el correo no sale, el pase queda y se dice: el administrador lo anula o lo reenvía. Nunca se le muestra el enlace.
        var enviado = true;
        try
        {
            await avisos.EnviarPaseAsync(cuenta.Email, tenant.Name, secreto.PlainTokenBase64Url, pase.ExpiresAt, concedidoPor, pase.Reason, ct);
        }
        catch (Exception ex)
        {
            enviado = false;
            logger.LogError(ex, "No se pudo enviar el pase de ingreso {Pase} a {Correo}.", pase.PublicId, cuenta.Email);
        }

        await AvisarAdministradoresAsync(cooperativa, tenant.Name, actor, destinatario, cuenta.Email, pase, concedidoPor, ct);

        await auditoria.EnCooperativaAsync(cooperativa, accionDeAuditoria, actor, currentUser.Email, nameof(LoginPass), pase.PublicId,
            null, new
            {
                Destinatario = cuenta.Email,
                pase.Reason,
                pase.ExpiresAt,
                pase.GrantedByMaster,
                Reemplazados = vivos.Select(v => v.PublicId).ToArray(),
                CorreoEnviado = enviado,
            }, ahora, ct);

        return Result.Success(new PaseConcedidoResult(
            PasesDeIngresoLectura.Dto(pase, cuenta.Email, currentUser.Email, ahora), enviado));
    }

    private async Task AvisarAdministradoresAsync(Guid cooperativa, string nombre, Guid actor, Guid destinatario, string correoDestinatario,
        LoginPass pase, string concedidoPor, CancellationToken ct)
    {
        var administradores = await admin.TenantMemberships.AsNoTracking()
            .Where(m => m.TenantId == cooperativa && m.IsTenantAdmin && m.Status == MembershipStatus.Active
                     && m.CentralUserId != actor && m.CentralUserId != destinatario)
            .Select(m => m.CentralUserId)
            .ToListAsync(ct);

        foreach (var id in administradores)
        {
            try
            {
                var cuenta = await centralIdentity.FindByIdAsync(id, ct);
                if (cuenta is null || cuenta.Status != CentralUserStatus.Active || !cuenta.EmailConfirmed) continue;
                await avisos.AvisarAdministradorAsync(cuenta.Email, nombre, correoDestinatario, pase.ExpiresAt, concedidoPor, pase.Reason, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo avisar a un administrador del pase de ingreso {Pase}.", pase.PublicId);
            }
        }
    }
}

public sealed record PaseConcedidoResult(LoginPassDto Pass, bool EmailSent);
