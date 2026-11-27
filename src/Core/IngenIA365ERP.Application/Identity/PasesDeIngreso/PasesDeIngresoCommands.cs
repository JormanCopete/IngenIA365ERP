using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Correccion.CorregirDocumento;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.PasesDeIngreso;

/// <summary>Feature 027: un pase como lo ven la ficha del usuario y la consola del maestro. Nunca lleva el enlace ni la huella.</summary>
public sealed record LoginPassDto(
    Guid PublicId,
    Guid CentralUserId,
    string? UserEmail,
    Guid TenantPublicId,
    string? GrantedByEmail,
    bool GrantedByMaster,
    string Reason,
    DateTime GrantedAt,
    DateTime ExpiresAt,
    int UseCount,
    DateTime? LastUsedAt,
    string? LastUsedIp,
    string Status,
    DateTime? VoidedAt,
    string? VoidReason);

/// <summary>Lectura de los pases para las dos pantallas.</summary>
public static class PasesDeIngresoLectura
{
    public static LoginPassDto Dto(LoginPass p, string? correoDelUsuario, string? correoDeQuienLoDio, DateTime ahora) => new(
        p.PublicId, p.CentralUserId, correoDelUsuario, p.TenantId, correoDeQuienLoDio, p.GrantedByMaster, p.Reason, p.GrantedAt,
        p.ExpiresAt, p.UseCount, p.LastUsedAt, p.LastUsedIp, p.Estado(ahora).ToString(), p.VoidedAt, p.VoidReason);

    public static async Task<IReadOnlyList<LoginPassDto>> ListarAsync(
        IQueryable<LoginPass> consulta, ICentralIdentityProvider identidad, DateTime ahora, CancellationToken ct)
    {
        var pases = await consulta.AsNoTracking().OrderByDescending(p => p.GrantedAt).Take(200).ToListAsync(ct);
        var correos = new Dictionary<Guid, string?>();
        foreach (var id in pases.SelectMany(p => new[] { p.CentralUserId, p.GrantedByUserId }).Distinct())
            correos[id] = (await identidad.FindByIdAsync(id, ct))?.Email;
        return pases.Select(p => Dto(p, correos[p.CentralUserId], p.GrantedByMaster ? "Administrador de la plataforma" : correos[p.GrantedByUserId], ahora))
            .ToList();
    }

    /// <summary>
    /// ¿El usuario de la cooperativa activa es administrador en ella? Lo es si su membresía lo dice (la consulta la concesión), si tiene el rol
    /// de fábrica «CompanyAdmin», o si alguno de sus roles puede conceder pases: entre quienes pueden darlos, sólo el maestro los da, así dos
    /// administradores no se habilitan entre sí.
    /// </summary>
    public static Task<bool> EsAdministradorAsync(IApplicationDbContext cooperativa, int usuario, CancellationToken ct) =>
        cooperativa.UserRoles.AnyAsync(ur => ur.UserId == usuario
            && (ur.Role!.Code == "CompanyAdmin"
                || cooperativa.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                    && rp.Permission!.Resource == "Security.Users" && rp.Permission.Action == "GrantMfaBypass")), ct);
}

public static class ValidacionDelPase
{
    public static IRuleBuilderOptions<T, string?> MotivoDelPase<T>(this IRuleBuilder<T, string?> regla) =>
        regla.Must(m => (m ?? string.Empty).Trim().Length is >= LoginPass.LargoMinimoDelMotivo and <= LoginPass.LargoMaximoDelMotivo)
            .WithMessage($"Escriba el motivo (entre {LoginPass.LargoMinimoDelMotivo} y {LoginPass.LargoMaximoDelMotivo} caracteres).");

    public static IRuleBuilderOptions<T, int> HorasDelPase<T>(this IRuleBuilder<T, int> regla) =>
        regla.InclusiveBetween(1, LoginPass.HorasMaximas).WithMessage($"El pase dura entre 1 y {LoginPass.HorasMaximas} horas.");

    internal static bool EnCooperativa(ICurrentCentralUserContext u, out Guid cooperativa, out Guid actor)
    {
        cooperativa = u.ActiveTenantPublicId ?? Guid.Empty;
        actor = u.CentralUserId ?? Guid.Empty;
        return u.IsAuthenticated && u.Purpose == CentralJwtPurposes.Full && cooperativa != Guid.Empty && actor != Guid.Empty;
    }

    internal static bool EsMaestro(ICurrentCentralUserContext u) =>
        u.IsAuthenticated && u.Purpose == CentralJwtPurposes.Full && u.IsGlobalMasterAdmin && u.CentralUserId is not null;
}

// ---------------------------------------------------------------- Conceder (administrador de la cooperativa)

/// <summary>Feature 027 (HU1): el administrador concede un pase al usuario de su cooperativa. El permiso lo exige la ruta.</summary>
public sealed record ConcederPaseDeIngresoCommand(Guid UserPublicId, int Hours, string? Reason) : IRequest<Result<PaseConcedidoResult>>;

public sealed class ConcederPaseDeIngresoCommandValidator : AbstractValidator<ConcederPaseDeIngresoCommand>
{
    public ConcederPaseDeIngresoCommandValidator()
    {
        RuleFor(x => x.UserPublicId).NotEmpty().WithMessage("Indique el usuario.");
        RuleFor(x => x.Hours).HorasDelPase();
        RuleFor(x => x.Reason).MotivoDelPase();
    }
}

public sealed class ConcederPaseDeIngresoCommandHandler(
    ICurrentCentralUserContext currentUser, IApplicationDbContext cooperativa, ICentralIdentityProvider centralIdentity,
    ConcesionDePaseDeIngreso concesion)
    : IRequestHandler<ConcederPaseDeIngresoCommand, Result<PaseConcedidoResult>>
{
    public async Task<Result<PaseConcedidoResult>> Handle(ConcederPaseDeIngresoCommand request, CancellationToken ct)
    {
        if (!ValidacionDelPase.EnCooperativa(currentUser, out var coop, out _))
            return Result.Failure<PaseConcedidoResult>("Identity.Unauthenticated", "Entre a una cooperativa para conceder pases.");

        var usuario = await cooperativa.Users.AsNoTracking().Where(u => u.PublicId == request.UserPublicId).Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);
        var cuenta = await IdentidadDelUsuario.ResolverAsync(cooperativa, centralIdentity, request.UserPublicId, ct);
        if (usuario is null || cuenta is null)
            return Result.Failure<PaseConcedidoResult>("Generic.NotFound", "El usuario no existe.");

        var esAdministrador = await PasesDeIngresoLectura.EsAdministradorAsync(cooperativa, usuario.Id, ct);
        return await concesion.ConcederAsync(coop, cuenta.Value, esAdministrador, TimeSpan.FromHours(request.Hours), request.Reason!,
            AuditEventTypes.LoginPassGranted, ct);
    }
}

// ---------------------------------------------------------------- Conceder (maestro)

/// <summary>Feature 027 (FR-020): el maestro concede un pase en una cooperativa, también a sus administradores.</summary>
public sealed record ConcederPaseDeIngresoComoMaestroCommand(Guid TenantPublicId, Guid CentralUserId, int Hours, string? Reason)
    : IRequest<Result<PaseConcedidoResult>>;

public sealed class ConcederPaseDeIngresoComoMaestroCommandValidator : AbstractValidator<ConcederPaseDeIngresoComoMaestroCommand>
{
    public ConcederPaseDeIngresoComoMaestroCommandValidator()
    {
        RuleFor(x => x.TenantPublicId).NotEmpty().WithMessage("Indique la cooperativa.");
        RuleFor(x => x.CentralUserId).NotEmpty().WithMessage("Indique el usuario.");
        RuleFor(x => x.Hours).HorasDelPase();
        RuleFor(x => x.Reason).MotivoDelPase();
    }
}

public sealed class ConcederPaseDeIngresoComoMaestroCommandHandler(ICurrentCentralUserContext currentUser, ConcesionDePaseDeIngreso concesion)
    : IRequestHandler<ConcederPaseDeIngresoComoMaestroCommand, Result<PaseConcedidoResult>>
{
    public Task<Result<PaseConcedidoResult>> Handle(ConcederPaseDeIngresoComoMaestroCommand request, CancellationToken ct) =>
        !ValidacionDelPase.EsMaestro(currentUser)
            ? Task.FromResult(Result.Failure<PaseConcedidoResult>("Saas.MasterOnly", "Sólo el maestro de plataforma."))
            : concesion.ConcederAsync(request.TenantPublicId, request.CentralUserId, destinatarioEsAdministradorEnLaCooperativa: false,
                TimeSpan.FromHours(request.Hours), request.Reason!, AuditEventTypes.LoginPassGranted, ct);
}

// ---------------------------------------------------------------- Reenviar

/// <summary>
/// Feature 027: reenviar emite un pase nuevo con el mismo motivo y el mismo vencimiento y deja el anterior «reemplazado»: el enlace en claro
/// sólo existe al enviarlo, así que no hay cómo mandar el mismo.
/// </summary>
public sealed record ReenviarPaseDeIngresoCommand(Guid PassPublicId) : IRequest<Result<PaseConcedidoResult>>;

public sealed class ReenviarPaseDeIngresoCommandHandler(
    ICurrentCentralUserContext currentUser, IAdminDbContext admin, IServiceProvider servicios, ConcesionDePaseDeIngreso concesion,
    IDateTimeService clock)
    : IRequestHandler<ReenviarPaseDeIngresoCommand, Result<PaseConcedidoResult>>
{
    public async Task<Result<PaseConcedidoResult>> Handle(ReenviarPaseDeIngresoCommand request, CancellationToken ct)
    {
        var maestro = ValidacionDelPase.EsMaestro(currentUser);
        if (!maestro && !ValidacionDelPase.EnCooperativa(currentUser, out _, out _))
            return Result.Failure<PaseConcedidoResult>("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var pase = await admin.LoginPasses.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PassPublicId, ct);
        if (pase is null || (!maestro && pase.TenantId != currentUser.ActiveTenantPublicId))
            return Result.Failure<PaseConcedidoResult>("Generic.NotFound", "El pase no existe.");

        var ahora = clock.UtcNow;
        if (!pase.EstaVigente(ahora))
            return Result.Failure<PaseConcedidoResult>("LoginPass.NotActive", "El pase ya no está vigente: conceda uno nuevo.");

        var esAdministrador = false;
        if (!maestro)
        {
            var cooperativa = (IApplicationDbContext)servicios.GetService(typeof(IApplicationDbContext))!;
            var usuario = await cooperativa.Users.AsNoTracking().Where(u => u.CentralUserId == pase.CentralUserId).Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(ct);
            esAdministrador = usuario is { } id && await PasesDeIngresoLectura.EsAdministradorAsync(cooperativa, id, ct);
        }

        return await concesion.ConcederAsync(pase.TenantId, pase.CentralUserId, esAdministrador, pase.ExpiresAt - ahora, pase.Reason,
            AuditEventTypes.LoginPassResent, ct);
    }
}

// ---------------------------------------------------------------- Anular

/// <summary>Feature 027 (HU3): un administrador de la cooperativa (o el maestro) anula un pase con motivo.</summary>
public sealed record AnularPaseDeIngresoCommand(Guid PassPublicId, string? Reason) : IRequest<Result<LoginPassDto>>;

public sealed class AnularPaseDeIngresoCommandValidator : AbstractValidator<AnularPaseDeIngresoCommand>
{
    public AnularPaseDeIngresoCommandValidator()
    {
        RuleFor(x => x.PassPublicId).NotEmpty().WithMessage("Indique el pase.");
        RuleFor(x => x.Reason).MotivoDelPase();
    }
}

public sealed class AnularPaseDeIngresoCommandHandler(
    ICurrentCentralUserContext currentUser, IAdminDbContext admin, ICentralIdentityProvider centralIdentity, AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<AnularPaseDeIngresoCommand, Result<LoginPassDto>>
{
    public async Task<Result<LoginPassDto>> Handle(AnularPaseDeIngresoCommand request, CancellationToken ct)
    {
        var maestro = ValidacionDelPase.EsMaestro(currentUser);
        if (!maestro && !ValidacionDelPase.EnCooperativa(currentUser, out _, out _))
            return Result.Failure<LoginPassDto>("Identity.Unauthenticated", "Inicie sesión de nuevo.");

        var pase = await admin.LoginPasses.FirstOrDefaultAsync(p => p.PublicId == request.PassPublicId, ct);
        if (pase is null || (!maestro && pase.TenantId != currentUser.ActiveTenantPublicId))
            return Result.Failure<LoginPassDto>("Generic.NotFound", "El pase no existe.");

        var ahora = clock.UtcNow;
        var actor = currentUser.CentralUserId!.Value;
        var estaba = pase.Estado(ahora);
        pase.Anular(ahora, LoginPass.Motivos.RevocadoPorAdministrador, actor);
        await admin.SaveChangesAsync(ct);

        if (estaba == EstadoDelPase.Active)
            await auditoria.EnCooperativaAsync(pase.TenantId, AuditEventTypes.LoginPassVoided, actor, currentUser.Email, nameof(LoginPass),
                pase.PublicId, null, new { Motivo = request.Reason!.Trim(), pase.VoidReason }, ahora, ct);

        var correo = (await centralIdentity.FindByIdAsync(pase.CentralUserId, ct))?.Email;
        var quien = pase.GrantedByMaster ? "Administrador de la plataforma" : (await centralIdentity.FindByIdAsync(pase.GrantedByUserId, ct))?.Email;
        return Result.Success(PasesDeIngresoLectura.Dto(pase, correo, quien, ahora));
    }
}

// ---------------------------------------------------------------- Listar

/// <summary>Feature 027 (FR-016): los pases de un usuario en la cooperativa activa, para su ficha.</summary>
public sealed record ListarPasesDelUsuarioQuery(Guid UserPublicId) : IRequest<Result<IReadOnlyList<LoginPassDto>>>;

public sealed class ListarPasesDelUsuarioQueryHandler(
    ICurrentCentralUserContext currentUser, IApplicationDbContext cooperativa, IAdminDbContext admin, ICentralIdentityProvider centralIdentity,
    IDateTimeService clock)
    : IRequestHandler<ListarPasesDelUsuarioQuery, Result<IReadOnlyList<LoginPassDto>>>
{
    public async Task<Result<IReadOnlyList<LoginPassDto>>> Handle(ListarPasesDelUsuarioQuery request, CancellationToken ct)
    {
        if (!ValidacionDelPase.EnCooperativa(currentUser, out var coop, out _))
            return Result.Failure<IReadOnlyList<LoginPassDto>>("Identity.Unauthenticated", "Inicie sesión de nuevo.");
        var cuenta = await IdentidadDelUsuario.ResolverAsync(cooperativa, centralIdentity, request.UserPublicId, ct);
        if (cuenta is null) return Result.Success<IReadOnlyList<LoginPassDto>>([]);
        return Result.Success(await PasesDeIngresoLectura.ListarAsync(
            admin.LoginPasses.Where(p => p.TenantId == coop && p.CentralUserId == cuenta.Value), centralIdentity, clock.UtcNow, ct));
    }
}

/// <summary>Feature 027 (FR-020): los pases de una cooperativa (o de una persona en ella), para la consola del maestro.</summary>
public sealed record ListarPasesDeLaCooperativaQuery(Guid TenantPublicId, Guid? CentralUserId) : IRequest<Result<IReadOnlyList<LoginPassDto>>>;

public sealed class ListarPasesDeLaCooperativaQueryHandler(
    ICurrentCentralUserContext currentUser, IAdminDbContext admin, ICentralIdentityProvider centralIdentity, IDateTimeService clock)
    : IRequestHandler<ListarPasesDeLaCooperativaQuery, Result<IReadOnlyList<LoginPassDto>>>
{
    public async Task<Result<IReadOnlyList<LoginPassDto>>> Handle(ListarPasesDeLaCooperativaQuery request, CancellationToken ct)
    {
        if (!ValidacionDelPase.EsMaestro(currentUser))
            return Result.Failure<IReadOnlyList<LoginPassDto>>("Saas.MasterOnly", "Sólo el maestro de plataforma.");
        var consulta = admin.LoginPasses.Where(p => p.TenantId == request.TenantPublicId);
        if (request.CentralUserId is { } persona) consulta = consulta.Where(p => p.CentralUserId == persona);
        return Result.Success(await PasesDeIngresoLectura.ListarAsync(consulta, centralIdentity, clock.UtcNow, ct));
    }
}
