using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Invitations.Services;

/// <summary>
/// Lo que el administrador digita al invitar (feature 017, FR-019, D-11). El documento es opcional
/// sólo por compatibilidad de la API: sin él la invitación sale por correo como antes; la pantalla
/// lo exige.
/// </summary>
public sealed record DatosDelInvitado(
    string Email,
    string? DocumentType = null,
    string? DocumentNumber = null,
    string? DocumentNumberConfirmation = null,
    string? FirstName = null,
    string? OtherNames = null,
    string? LastName = null,
    string? SecondLastName = null,
    string? MobilePhone = null,
    Guid? RolePublicId = null,
    Guid? PositionPublicId = null)
{
    public bool TraeDocumento =>
        !string.IsNullOrWhiteSpace(DocumentType) || !string.IsNullOrWhiteSpace(DocumentNumber);

    /// <summary>El nombre para el saludo del correo; nulo si no se digitó.</summary>
    public string? NombreParaSaludo =>
        string.Join(' ', new[] { FirstName, LastName }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } n ? n : null;
}

/// <summary>Una invitación lista para guardar y enviar.</summary>
/// <param name="IdentidadExistente">La persona ya tenía cuenta activa: el correo dice «te invitan a una nueva
/// cooperativa» con su nombre real (T047).</param>
public sealed record InvitacionPreparada(Invitation Invitacion, string TokenPlano, string? NombreDelDestinatario, bool IdentidadExistente = false);

/// <summary>
/// Cómo se emite una invitación, en un solo sitio (feature 017, research R7). Lo usan la del
/// administrador de la cooperativa, la del maestro y el alta de cooperativa con su primer
/// administrador. <b>Agrega al contexto sin guardar</b> salvo la identidad pendiente, que la crea el
/// proveedor de identidad.
/// <list type="number">
///   <item>Valida el documento contra el catálogo y los tipos que acepta la cooperativa.</item>
///   <item>Si el correo digitado es de otra identidad: «Correo ya registrado», sin decir de quién.</item>
///   <item>Busca la identidad por usuario derivado: la ancla si existe y la invitación sale a <b>su</b>
///         correo, no al digitado (D-05); si no, crea una pendiente con el correo digitado.</item>
///   <item>Rechaza a quien ya es miembro activo y valida rol y cargo en la base de la cooperativa.</item>
/// </list>
/// El mensaje al administrador es el mismo haya o no identidad: nunca sabe de otras cooperativas.
/// </summary>
public sealed class EmisionDeInvitacion(
    IAdminDbContext db,
    ICentralIdentityProvider identidad,
    ITenantDbContextFactory fabrica,
    ISecureTokenGenerator tokens,
    IDateTimeService clock,
    IOptions<IdentityEmailOptions> opciones)
{
    public async Task<Result<InvitacionPreparada>> PrepararAsync(
        Tenant cooperativa, Guid invitador, bool comoAdministrador, DatosDelInvitado datos, CancellationToken ct)
    {
        var correoDigitado = datos.Email.Trim();
        var now = clock.UtcNow;
        var expira = now.AddDays(opciones.Value.InvitationLifetimeDays);

        // Sin documento: la invitación de siempre (compatibilidad de la API).
        if (!datos.TraeDocumento)
        {
            var (planoSin, hashSin) = tokens.Generate();
            await SupersederAsync(cooperativa.PublicId, correoDigitado.ToUpperInvariant(), null, ct);
            var sin = Invitation.Create(correoDigitado, correoDigitado.ToUpperInvariant(), cooperativa.PublicId,
                invitador, comoAdministrador, hashSin, now, expira);
            db.Invitations.Add(sin);
            return Result.Success(new InvitacionPreparada(sin, planoSin, datos.NombreParaSaludo));
        }

        // 1) Documento.
        if (UsuarioDeDocumento.NormalizarNumero(datos.DocumentNumber)
            != UsuarioDeDocumento.NormalizarNumero(datos.DocumentNumberConfirmation))
            return Result.Failure<InvitacionPreparada>(IdentityErrors.DocumentoNoCoincide());

        var documento = UsuarioDeDocumento.Validar(datos.DocumentType, datos.DocumentNumber, soloDeAcceso: true, out var motivo);
        if (documento is null)
            return Result.Failure<InvitacionPreparada>(IdentityErrors.DocumentoInvalido(motivo!));

        var politica = await db.TenantIdentityPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == cooperativa.PublicId, ct);
        var aceptados = politica?.TiposAceptados() ?? TenantIdentityPolicy.TiposAceptadosDe(null);
        if (!aceptados.Contains(documento.Tipo))
            return Result.Failure<InvitacionPreparada>(IdentityErrors.TipoNoAceptado(documento.Tipo.Codigo));

        // 2) Correo y documento contra las identidades existentes.
        var porCorreo = await identidad.FindByEmailAsync(correoDigitado, ct);
        var porDocumento = await identidad.FindByDocumentUsernameAsync(documento.Usuario, ct);

        if (porCorreo is not null && porDocumento is not null && porCorreo.Id != porDocumento.Id)
            return Result.Failure<InvitacionPreparada>(IdentityErrors.CorreoYaRegistrado());
        if (porCorreo is not null && porDocumento is null
            && porCorreo.DocumentUsername is not null && porCorreo.DocumentUsername != documento.Usuario)
            return Result.Failure<InvitacionPreparada>(IdentityErrors.CorreoYaRegistrado());

        // Una cuenta de antes de la 017 sin documento cuyo correo coincide es la misma persona hasta que
        // demuestre lo contrario: se ancla a ella y el documento se le registra al activar, si coincide.
        var ancla = porDocumento ?? porCorreo;
        if (ancla is null)
        {
            var creada = await identidad.CreatePendingUserAsync(new NuevaIdentidadPendiente(
                correoDigitado, documento,
                Limpio(datos.FirstName), Limpio(datos.OtherNames), Limpio(datos.LastName), Limpio(datos.SecondLastName),
                Limpio(datos.MobilePhone)), ct);
            if (!creada.Succeeded)
            {
                // Carrera con otra cooperativa que invitó el mismo documento o correo al mismo tiempo.
                ancla = await identidad.FindByDocumentUsernameAsync(documento.Usuario, ct);
                if (ancla is null)
                    return Result.Failure<InvitacionPreparada>(IdentityErrors.CorreoYaRegistrado());
                var otroPorCorreo = await identidad.FindByEmailAsync(correoDigitado, ct);
                if (otroPorCorreo is not null && otroPorCorreo.Id != ancla.Id)
                    return Result.Failure<InvitacionPreparada>(IdentityErrors.CorreoYaRegistrado());
            }
            else
            {
                ancla = await identidad.FindByIdAsync(creada.CentralUserId!.Value, ct);
            }
        }

        // 3) Ya es miembro activo.
        var yaEsMiembro = await db.TenantMemberships.AsNoTracking().AnyAsync(m =>
            m.CentralUserId == ancla!.Id && m.TenantId == cooperativa.PublicId && m.Status == MembershipStatus.Active, ct);
        if (yaEsMiembro)
            return Result.Failure<InvitacionPreparada>(IdentityErrors.YaEsMiembro());

        // 4) Rol y cargo, en la base de la cooperativa.
        var catalogos = await ValidarRolYCargoAsync(cooperativa, datos.RolePublicId, datos.PositionPublicId, ct);
        if (catalogos is not null)
            return Result.Failure<InvitacionPreparada>(catalogos);

        // 5) La invitación sale al correo de la identidad (D-05).
        var destino = ancla!.Email;
        var normalizado = destino.Trim().ToUpperInvariant();
        await SupersederAsync(cooperativa.PublicId, normalizado, ancla.Id, ct);

        var (plano, hash) = tokens.Generate();
        var invitacion = Invitation.Create(destino, normalizado, cooperativa.PublicId, invitador, comoAdministrador, hash, now, expira);
        invitacion.AnclarADocumento(
            ancla.Id, documento.Tipo.Codigo, documento.Numero,
            Limpio(datos.FirstName), Limpio(datos.OtherNames), Limpio(datos.LastName), Limpio(datos.SecondLastName),
            Limpio(datos.MobilePhone), datos.RolePublicId, datos.PositionPublicId,
            politica?.MaxActivationAttempts ?? TenantIdentityPolicy.IntentosPorDefecto,
            typedEmail: string.Equals(destino, correoDigitado, StringComparison.OrdinalIgnoreCase) ? null : correoDigitado);
        db.Invitations.Add(invitacion);

        // El saludo usa el nombre que ya tiene la identidad, no el digitado: si el documento era de
        // otra persona, el correo no le confirma a nadie el nombre que escribió el administrador.
        var saludo = ancla.FirstName is not null ? $"{ancla.FirstName} {ancla.LastName}".Trim() : datos.NombreParaSaludo;
        return Result.Success(new InvitacionPreparada(invitacion, plano, saludo, ancla.Status == CentralUserStatus.Active));
    }

    /// <summary>
    /// Las invitaciones vivas anteriores a la misma persona en la misma cooperativa quedan
    /// <c>Superseded</c>: una sola sirve a la vez (por correo, y desde la 017 también por identidad).
    /// </summary>
    private async Task SupersederAsync(Guid cooperativa, string correoNormalizado, Guid? centralUserId, CancellationToken ct)
    {
        var previas = await db.Invitations
            .Where(i => i.TenantId == cooperativa
                     && (i.Status == InvitationStatus.Pending || i.Status == InvitationStatus.UnderReview)
                     && (i.NormalizedEmail == correoNormalizado
                         || (centralUserId != null && i.CentralUserId == centralUserId)))
            .ToListAsync(ct);
        foreach (var p in previas)
            p.MarkSuperseded();
    }

    /// <summary>
    /// El rol debe ser asignable y activo, y el cargo existir, en la base de la cooperativa. Se abre por
    /// la fábrica autorizada (Principio IV): el maestro invita sin tener esa cooperativa resuelta.
    /// </summary>
    private async Task<Error?> ValidarRolYCargoAsync(Tenant cooperativa, Guid? rol, Guid? cargo, CancellationToken ct)
    {
        if (rol is null && cargo is null)
            return null;

        await using var ambito = fabrica.Abrir(cooperativa.DatabaseName ?? cooperativa.SchemaName, cooperativa.ConnectionString);
        if (rol is { } r && !await ambito.Db.Roles.AsNoTracking()
                .AnyAsync(x => x.PublicId == r && x.IsActive && x.IsAssignable && !x.IsDeleted, ct))
            return IdentityErrors.RolNoEncontrado();
        if (cargo is { } c && !await ambito.Db.Positions.AsNoTracking()
                .AnyAsync(x => x.PublicId == c && !x.IsDeleted, ct))
            return IdentityErrors.CargoNoEncontrado();
        return null;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
