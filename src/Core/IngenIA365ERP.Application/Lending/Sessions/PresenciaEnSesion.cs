using System.Text.Json;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

/// <summary>
/// El voto presencial de un miembro en el equipo de la secretaría (feature 013, T0296; api.md §5; F8): el miembro se identifica con
/// <b>sus</b> credenciales —passkey o TOTP—, nunca con contraseña, sobre el mismo mecanismo de la aprobación presencial de la 012
/// (<see cref="IDesafiosDePresencia"/>, <see cref="IWebAuthnService"/>, base64url por <c>wwwroot/js/webauthn.js</c>). El desafío se ata a la
/// sesión (<c>RequestPublicId</c> = la sesión), a quien lo pidió y a la persona que va a votar; dura dos minutos y se consume en el primer
/// intento. Un miembro sin usuario activo con identidad central no puede identificarse: <c>Lending.Session.MemberNotUser</c>. Toda falla de
/// identidad es <c>Approvals.Presence.*</c> (422, nunca 401). (nuevo)
/// </summary>
public sealed class PresenciaEnSesion(
    IApplicationDbContext db,
    IMfaDirectory credenciales,
    IWebAuthnService webAuthn,
    ICentralIdentityProvider identidadCentral,
    IDesafiosDePresencia desafios,
    IDateTimeService reloj)
{
    /// <summary>Lo que dura un desafío presencial (el mismo de la 012).</summary>
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan MemoriaDelTotp = TimeSpan.FromMinutes(3);

    /// <summary>El desafío para que <paramref name="personId"/> vote presente en <paramref name="sesion"/>, pedido por <paramref name="solicitante"/>.</summary>
    public async Task<Result<DesafioDePresenciaDto>> CrearAsync(Session sesion, int solicitante, int personId, CancellationToken ct)
    {
        var miembro = await UsuarioDeAsync(personId, ct);
        if (miembro is null) return Result.Failure<DesafioDePresenciaDto>(ErroresDeGobierno.MiembroSinUsuario());

        var llaves = await credenciales.ListarWebAuthnActivasAsync(miembro.CentralUserId, ct);
        var totps = await credenciales.ListarCifradosTotpActivosAsync(miembro.CentralUserId, ct);
        var metodos = new List<string>();
        if (llaves.Count > 0) metodos.Add("Passkey");
        if (totps.Count > 0) metodos.Add("Totp");
        if (metodos.Count == 0) return Result.Failure<DesafioDePresenciaDto>(ErroresDeAprobaciones.PresenciaInvalida());

        var opciones = llaves.Count > 0 ? webAuthn.CrearOpcionesDeIngreso(llaves) : null;
        var vence = reloj.UtcNow.Add(Vigencia);
        var desafio = new DesafioDePresencia(Guid.NewGuid(), sesion.PublicId, solicitante, miembro.Id, miembro.CentralUserId, miembro.Nombre, metodos,
            opciones?.ParaGuardarJson, vence);
        await desafios.GuardarAsync(desafio, Vigencia, ct);
        return Result.Success(new DesafioDePresenciaDto(desafio.PublicId, vence, metodos, opciones?.OpcionesJson));
    }

    /// <summary>
    /// Verifica que quien vota presente es <paramref name="personId"/>: el desafío es de esta sesión, de quien lo pidió y del usuario de esa
    /// persona; la passkey es suya y firma bien, o el TOTP es suyo y no se reusó. Devuelve el <c>PublicId</c> del desafío para sellarlo en el voto.
    /// </summary>
    public async Task<Result<Guid>> VerificarAsync(Session sesion, int solicitante, int personId, VoteMethod metodo, PresenciaDto presencia, CancellationToken ct)
    {
        var desafio = await desafios.ConsumirAsync(presencia.ChallengePublicId, ct);
        if (desafio is null || desafio.ExpiresAt < reloj.UtcNow) return Result.Failure<Guid>(ErroresDeAprobaciones.PresenciaVencida());
        var miembro = await UsuarioDeAsync(personId, ct);
        if (miembro is null) return Result.Failure<Guid>(ErroresDeGobierno.MiembroSinUsuario());
        if (desafio.RequestPublicId != sesion.PublicId || desafio.RequesterUserId != solicitante || desafio.ApproverUserId != miembro.Id)
            return Result.Failure<Guid>(ErroresDeAprobaciones.PresenciaInvalida());

        if (metodo == VoteMethod.InPersonTotp)
        {
            var codigo = presencia.TotpCode?.Trim() ?? string.Empty;
            if (!desafio.Methods.Contains("Totp") || !await identidadCentral.VerifyMfaCodeAsync(desafio.ApproverCentralUserId, codigo, ct))
                return Result.Failure<Guid>(ErroresDeAprobaciones.PresenciaInvalida());
            if (!await desafios.MarcarTotpUsadoAsync(desafio.ApproverCentralUserId, codigo, MemoriaDelTotp, ct))
                return Result.Failure<Guid>(ErroresDeAprobaciones.TotpReusado());
            return Result.Success(desafio.PublicId);
        }

        if (desafio.OpcionesWebAuthnJson is null || presencia.Assertion is null || LeerCredentialId(presencia.Assertion) is not { } credentialId)
            return Result.Failure<Guid>(ErroresDeAprobaciones.PresenciaInvalida());
        // Que la llave exista Y sea del miembro: una llave ajena válida firmaría bien, pero de otra persona.
        var guardada = await credenciales.BuscarWebAuthnPorCredentialIdAsync(credentialId, ct);
        if (guardada is null || guardada.CentralUserId != desafio.ApproverCentralUserId) return Result.Failure<Guid>(ErroresDeAprobaciones.PresenciaInvalida());

        var verificacion = await webAuthn.VerificarIngresoAsync(presencia.Assertion, desafio.OpcionesWebAuthnJson, guardada.ClavePublicaCose, guardada.SignCount, ct);
        if (!verificacion.Exito) return Result.Failure<Guid>(ErroresDeAprobaciones.PresenciaInvalida());
        await credenciales.ActualizarContadorWebAuthnAsync(guardada.PublicId, verificacion.ContadorNuevo, verificacion.EstaRespaldada, ct);
        await credenciales.MarcarUsoAsync(desafio.ApproverCentralUserId, guardada.PublicId, reloj.UtcNow, ct);
        return Result.Success(desafio.PublicId);
    }

    /// <summary>El usuario activo, con identidad central, de una persona del maestro.</summary>
    internal async Task<UsuarioDelMiembro?> UsuarioDeAsync(int personId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.PersonId == personId && u.IsActive && !u.IsDeleted && u.CentralUserId != null)
            .OrderBy(u => u.Id)
            .Select(u => new UsuarioDelMiembro(u.Id, u.CentralUserId!.Value, u.Email ?? u.Username))
            .FirstOrDefaultAsync(ct);

    private static byte[]? LeerCredentialId(string asercion)
    {
        try
        {
            using var doc = JsonDocument.Parse(asercion);
            if (!doc.RootElement.TryGetProperty("rawId", out var raw) || raw.GetString() is not { Length: > 0 } texto) return null;
            return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(texto);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>El usuario de un miembro: su <c>SEC_Users.Id</c>, su identidad central y su nombre.</summary>
internal sealed record UsuarioDelMiembro(int Id, Guid CentralUserId, string Nombre);
