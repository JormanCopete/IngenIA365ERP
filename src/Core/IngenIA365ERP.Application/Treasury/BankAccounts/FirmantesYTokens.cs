using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.BankAccounts;

// Feature 015, T1 (fase 34, T0417; FR-208; contracts/api.md §13): los firmantes y los tokens del portal del banco de cada cuenta propia. El alta y el retiro llevan su acta
// y la aprobación de otra persona (sujeto BankAccountAuthorityChange, permiso Treasury.Signers.Approve); la combinación de firmas es por clase y monto (lo que firma solo y
// si firma en conjunto). La vigilancia diaria alerta cuando un firmante o el dueño de un token se retira de la cooperativa.

/// <summary>Un firmante o un token de una cuenta propia. <c>Kind</c>: <c>Signer</c> o <c>Token</c>. (nuevo)</summary>
public sealed record BankAccountAuthorityDto(
    Guid PublicId,
    string Kind,
    Guid BankAccountPublicId,
    string BankAccountCode,
    Guid PersonPublicId,
    string PersonName,
    string? SignatureClass,
    decimal? MaxAmountAlone,
    bool CanSignJointly,
    string? Role,
    string? Serial,
    string? Purpose,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    BankAccountAuthorityState Status,
    Guid MinutesAttachmentPublicId,
    Guid? PendingApprovalRequestPublicId,
    string? RetirementReason);

/// <summary>Lo común de los firmantes y tokens: la lectura de cualquiera de los dos por su <c>PublicId</c>, la huella y el DTO. (nuevo)</summary>
public static class FirmantesYTokens
{
    public const string Firmante = "Signer";
    public const string Token = "Token";
    public const string PermisoDeAprobar = "Treasury.Signers.Approve";

    public static async Task<BankAccountAuthority?> BuscarAsync(IApplicationDbContext db, Guid publicId, bool seguir, CancellationToken ct)
    {
        BankAccountAuthority? f = seguir
            ? await db.BankAccountSigners.FirstOrDefaultAsync(x => x.PublicId == publicId, ct)
            : await db.BankAccountSigners.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (f is not null) return f;
        return seguir
            ? await db.BankAccountTokens.FirstOrDefaultAsync(x => x.PublicId == publicId, ct)
            : await db.BankAccountTokens.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
    }

    public static string Huella(BankAccountAuthority a) => HuellaDeOperacion.Calcular("Tesoreria.FirmanteOToken",
        new { a.PublicId, a.BankAccountId, a.PersonId, a.Status, a.MinutesAttachmentPublicId, a.RetirementMinutesAttachmentPublicId });

    public static async Task<BankAccountAuthorityDto> DtoAsync(IApplicationDbContext db, BankAccountAuthority a, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == a.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == a.PersonId).Select(p => new { p.PublicId, p.FirstName, p.LastName }).FirstAsync(ct);
        var pendiente = a.Status switch
        {
            BankAccountAuthorityState.PendingApproval => a.ApprovalRequestPublicId,
            BankAccountAuthorityState.PendingRetirement => a.RetirementApprovalRequestPublicId,
            _ => null,
        };
        var firmante = a as BankAccountSigner;
        var token = a as BankAccountToken;
        return new BankAccountAuthorityDto(a.PublicId, firmante is not null ? Firmante : Token, cuenta.PublicId, cuenta.Code, persona.PublicId,
            $"{persona.FirstName} {persona.LastName}".Trim(), firmante?.SignatureClass, firmante?.MaxAmountAlone, firmante?.CanSignJointly ?? false, firmante?.Role,
            token?.Serial, token?.Purpose, a.ValidFrom, a.ValidTo, a.Status, a.MinutesAttachmentPublicId, pendiente, a.RetirementReason);
    }

    /// <summary>Si la persona se retiró de la cooperativa: su usuario del ERP está inactivo o todas sus fichas de empleado tienen retiro hasta hoy.</summary>
    public static async Task<bool> SeRetiroAsync(IApplicationDbContext db, BankAccountAuthority a, DateOnly hoy, CancellationToken ct)
    {
        if (a.UserId is { } u && await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == u).Select(x => !x.IsActive || x.IsDeleted).FirstOrDefaultAsync(ct))
            return true;
        var fichas = await db.Employees.AsNoTracking().Where(e => e.PersonId == a.PersonId).Select(e => e.TerminationDate).ToListAsync(ct);
        var limite = hoy.ToDateTime(TimeOnly.MaxValue);
        return fichas.Count > 0 && fichas.All(t => t.Year > 1900 && t <= limite);
    }
}

/// <summary>Lo que hace un firmante o un token cuando la aprobación de su alta o su retiro se completa o se devuelve (<see cref="IAprobableDeTesoreria"/>). (nuevo)</summary>
public sealed class AprobableCambioDeFirmantes(IApplicationDbContext db, IDateTimeService reloj) : IAprobableDeTesoreria
{
    public string Sujeto => ApprovalSubjects.BankAccountAuthorityChange;

    public async Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) => await DecidirAsync(solicitud, aprobar: true, null, ct);

    public async Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        await DecidirAsync(solicitud, aprobar: false, motivo, ct);

    private async Task<Result<EstadoDeFuenteDto>> DecidirAsync(ApprovalRequest solicitud, bool aprobar, string? motivo, CancellationToken ct)
    {
        var a = await FirmantesYTokens.BuscarAsync(db, solicitud.SourcePublicId, seguir: true, ct);
        if (a is null) return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.NoEncontrado("El firmante o token"));
        var hoy = reloj.HoyLocal;
        switch (a.Status)
        {
            case BankAccountAuthorityState.PendingApproval:
                a.Status = aprobar ? BankAccountAuthorityState.Active : BankAccountAuthorityState.Rejected;
                break;
            case BankAccountAuthorityState.PendingRetirement:
                if (aprobar)
                {
                    a.Status = BankAccountAuthorityState.Retired;
                    a.ValidTo = hoy < a.ValidFrom ? a.ValidFrom : hoy;
                }
                else
                {
                    a.Status = BankAccountAuthorityState.Active;
                    a.RetirementReason = null;
                    a.RetirementMinutesAttachmentPublicId = null;
                }
                break;
            default:
                return Result.Failure<EstadoDeFuenteDto>(ErroresDeTesoreria.FirmanteEstado(a.Status.ToString(), "pendiente de aprobación"));
        }
        a.DecidedAt = reloj.UtcNow;
        a.DecisionReason = motivo;
        return Result.Success(new EstadoDeFuenteDto(a.PublicId, ApprovalSourceTypes.TreasuryDocument, a.Status.ToString(), solicitud.SourceLabel));
    }
}

// -------------------------------------------------------------------------------------------------------------- registrar --

/// <summary>
/// <c>POST /api/treasury/bank-accounts/{id}/signers</c> (<c>Treasury.Signers.Manage</c>, con clave): registra un firmante con su clase de firma, lo que firma solo, si firma en
/// conjunto, su cargo, su vigencia y el acta; queda pendiente hasta que otra persona lo aprueba. (nuevo)
/// </summary>
public sealed record RegisterBankAccountSignerCommand(
    Guid BankAccountPublicId,
    Guid PersonPublicId,
    string SignatureClass,
    decimal? MaxAmountAlone,
    bool CanSignJointly,
    string? Role,
    Guid MinutesAttachmentPublicId,
    DateOnly? ValidFrom = null) : IRequest<Result<BankAccountAuthorityDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterBankAccountSignerCommandValidator : AbstractValidator<RegisterBankAccountSignerCommand>
{
    public RegisterBankAccountSignerCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique la persona que firma.");
        RuleFor(x => x.SignatureClass).NotEmpty().WithMessage("Indique la clase de firma (A, B…).").MaximumLength(10);
        RuleFor(x => x.MaxAmountAlone).GreaterThan(0m).When(x => x.MaxAmountAlone is not null);
        RuleFor(x => x.Role).MaximumLength(80);
        RuleFor(x => x.MinutesAttachmentPublicId).NotEmpty().WithMessage("Adjunte el acta que autoriza al firmante.");
    }
}

public sealed class RegisterBankAccountSignerCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    AprobacionDiferidaEnTesoreria aprobacion,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterBankAccountSignerCommand, Result<BankAccountAuthorityDto>>
{
    public async Task<Result<BankAccountAuthorityDto>> Handle(RegisterBankAccountSignerCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.BankAccountPublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("La persona"));
        var clase = request.SignatureClass.Trim().ToUpperInvariant();
        var hoy = reloj.HoyLocal;
        if (await db.BankAccountSigners.AsNoTracking().AnyAsync(s => s.BankAccountId == cuenta.Id && s.PersonId == persona && s.SignatureClass == clase
                && (s.Status == BankAccountAuthorityState.Active || s.Status == BankAccountAuthorityState.PendingApproval || s.Status == BankAccountAuthorityState.PendingRetirement), ct))
            return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.FirmanteRepetido());
        var firmante = new BankAccountSigner
        {
            BankAccountId = cuenta.Id, PersonId = persona.Value, UserId = await UsuarioDeAsync(persona.Value, ct), SignatureClass = clase,
            MaxAmountAlone = request.MaxAmountAlone, CanSignJointly = request.CanSignJointly, Role = string.IsNullOrWhiteSpace(request.Role) ? null : request.Role.Trim(),
            ValidFrom = request.ValidFrom ?? hoy, MinutesAttachmentPublicId = request.MinutesAttachmentPublicId, RequestedByUserId = usuario, RequestedAt = reloj.UtcNow,
        };
        var pedida = await PedirAsync(aprobacion, firmante, string.Create(CultureInfo.InvariantCulture, $"Alta de firmante {clase} en {cuenta.Code}"), usuario, hoy, ct);
        if (pedida.IsFailure) return Result.Failure<BankAccountAuthorityDto>(pedida.Error);
        firmante.ApprovalRequestPublicId = pedida.Value?.PublicId;
        if (pedida.Value is null) firmante.Status = BankAccountAuthorityState.Active;
        db.BankAccountSigners.Add(firmante);
        await auditoria.AgregarAsync("Treasury.Signer.Requested", "BankAccountSigner", firmante.PublicId, null,
            new { account = cuenta.Code, signatureClass = clase, maxAlone = firmante.MaxAmountAlone, jointly = firmante.CanSignJointly, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await FirmantesYTokens.DtoAsync(db, firmante, ct));
    }

    private Task<int?> UsuarioDeAsync(int persona, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.PersonId == persona && u.IsActive).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);

    internal static Task<Result<ApprovalRequest?>> PedirAsync(AprobacionDiferidaEnTesoreria aprobacion, BankAccountAuthority a, string rotulo, int usuario, DateOnly hoy,
        CancellationToken ct) =>
        aprobacion.PedirAsync(new PedidoDeAprobacionDiferida(ApprovalSubjects.BankAccountAuthorityChange, a.PublicId, rotulo, 0m, hoy, FirmantesYTokens.Huella(a),
            null, [usuario]), ct);
}

/// <summary>
/// <c>POST /api/treasury/bank-accounts/{id}/tokens</c> (<c>Treasury.Signers.Manage</c>, con clave): registra un token del portal del banco con su serial, quién lo tiene, para
/// qué sirve, su vigencia y el acta; queda pendiente hasta que otra persona lo aprueba. Un serial vigente no se asigna dos veces. (nuevo)
/// </summary>
public sealed record RegisterBankAccountTokenCommand(
    Guid BankAccountPublicId,
    Guid PersonPublicId,
    string Serial,
    string? Purpose,
    Guid MinutesAttachmentPublicId,
    DateOnly? ValidFrom = null) : IRequest<Result<BankAccountAuthorityDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterBankAccountTokenCommandValidator : AbstractValidator<RegisterBankAccountTokenCommand>
{
    public RegisterBankAccountTokenCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique quién tiene el token.");
        RuleFor(x => x.Serial).NotEmpty().WithMessage("Indique el serial del token.").MaximumLength(40);
        RuleFor(x => x.Purpose).MaximumLength(80);
        RuleFor(x => x.MinutesAttachmentPublicId).NotEmpty().WithMessage("Adjunte el acta que autoriza el token.");
    }
}

public sealed class RegisterBankAccountTokenCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    AprobacionDiferidaEnTesoreria aprobacion,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterBankAccountTokenCommand, Result<BankAccountAuthorityDto>>
{
    public async Task<Result<BankAccountAuthorityDto>> Handle(RegisterBankAccountTokenCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.BankAccountPublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("La persona"));
        var serial = request.Serial.Trim();
        if (await db.BankAccountTokens.AsNoTracking().AnyAsync(t => t.Serial == serial
                && (t.Status == BankAccountAuthorityState.Active || t.Status == BankAccountAuthorityState.PendingApproval || t.Status == BankAccountAuthorityState.PendingRetirement), ct))
            return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.TokenRepetido(serial));
        var hoy = reloj.HoyLocal;
        var token = new BankAccountToken
        {
            BankAccountId = cuenta.Id, PersonId = persona.Value,
            UserId = await db.Users.AsNoTracking().Where(u => u.PersonId == persona && u.IsActive).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct),
            Serial = serial, Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(), ValidFrom = request.ValidFrom ?? hoy,
            MinutesAttachmentPublicId = request.MinutesAttachmentPublicId, RequestedByUserId = usuario, RequestedAt = reloj.UtcNow,
        };
        var pedida = await RegisterBankAccountSignerCommandHandler.PedirAsync(aprobacion, token,
            string.Create(CultureInfo.InvariantCulture, $"Alta del token {serial} en {cuenta.Code}"), usuario, hoy, ct);
        if (pedida.IsFailure) return Result.Failure<BankAccountAuthorityDto>(pedida.Error);
        token.ApprovalRequestPublicId = pedida.Value?.PublicId;
        if (pedida.Value is null) token.Status = BankAccountAuthorityState.Active;
        db.BankAccountTokens.Add(token);
        await auditoria.AgregarAsync("Treasury.Token.Requested", "BankAccountToken", token.PublicId, null,
            new { account = cuenta.Code, serial, purpose = token.Purpose, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await FirmantesYTokens.DtoAsync(db, token, ct));
    }
}

// ----------------------------------------------------------------------------------------------------------------- retirar --

/// <summary>
/// <c>POST /api/treasury/bank-accounts/{id}/signers/{signerId}/retire</c> y <c>…/tokens/{tokenId}/retire</c> (<c>Treasury.Signers.Manage</c>, con clave; nuevo): pide el retiro
/// de un firmante o un token activo con su motivo y su acta; sigue vigente hasta que otra persona aprueba el retiro, que le cierra la vigencia. (nuevo)
/// </summary>
public sealed record RetireBankAccountAuthorityCommand(Guid AuthorityPublicId, string Reason, Guid MinutesAttachmentPublicId) : IRequest<Result<BankAccountAuthorityDto>>,
    IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RetireBankAccountAuthorityCommandValidator : AbstractValidator<RetireBankAccountAuthorityCommand>
{
    public RetireBankAccountAuthorityCommandValidator()
    {
        RuleFor(x => x.AuthorityPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del retiro.").MaximumLength(500);
        RuleFor(x => x.MinutesAttachmentPublicId).NotEmpty().WithMessage("Adjunte el acta del retiro.");
    }
}

public sealed class RetireBankAccountAuthorityCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    AprobacionDiferidaEnTesoreria aprobacion,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RetireBankAccountAuthorityCommand, Result<BankAccountAuthorityDto>>
{
    public async Task<Result<BankAccountAuthorityDto>> Handle(RetireBankAccountAuthorityCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var a = await FirmantesYTokens.BuscarAsync(db, request.AuthorityPublicId, seguir: true, ct);
        if (a is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(a.BankAccountId))
            return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("El firmante o token"));
        if (a.Status != BankAccountAuthorityState.Active) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.FirmanteEstado(a.Status.ToString(), "activo"));
        var hoy = reloj.HoyLocal;
        a.Status = BankAccountAuthorityState.PendingRetirement;
        a.RetirementReason = request.Reason.Trim();
        a.RetirementMinutesAttachmentPublicId = request.MinutesAttachmentPublicId;
        var pedida = await RegisterBankAccountSignerCommandHandler.PedirAsync(aprobacion, a, string.Create(CultureInfo.InvariantCulture, $"Retiro de firmante o token"), usuario, hoy, ct);
        if (pedida.IsFailure) return Result.Failure<BankAccountAuthorityDto>(pedida.Error);
        a.RetirementApprovalRequestPublicId = pedida.Value?.PublicId;
        if (pedida.Value is null)
        {
            a.Status = BankAccountAuthorityState.Retired;
            a.ValidTo = hoy;
        }
        await auditoria.AgregarAsync(a is BankAccountSigner ? "Treasury.Signer.RetirementRequested" : "Treasury.Token.RetirementRequested",
            a is BankAccountSigner ? "BankAccountSigner" : "BankAccountToken", a.PublicId, null, new { reason = a.RetirementReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await FirmantesYTokens.DtoAsync(db, a, ct));
    }
}

// --------------------------------------------------------------------------------------------------- aprobar y devolver --

/// <summary>
/// <c>POST /api/treasury/bank-accounts/{id}/signers/{signerId}/approve</c>, <c>…/reject</c> y lo mismo con <c>tokens</c> (<c>Treasury.Signers.Approve</c>, nuevo; con clave):
/// decide el alta o el retiro pendiente por el motor de aprobaciones; quien lo pidió no lo decide (<c>Treasury.Signer.SelfApproval</c>). (nuevo)
/// </summary>
public sealed record DecideBankAccountAuthorityCommand(Guid AuthorityPublicId, bool Approve, string? Reason = null) : IRequest<Result<BankAccountAuthorityDto>>,
    IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class DecideBankAccountAuthorityCommandValidator : AbstractValidator<DecideBankAccountAuthorityCommand>
{
    public DecideBankAccountAuthorityCommandValidator()
    {
        RuleFor(x => x.AuthorityPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().When(x => !x.Approve).WithMessage("Escriba el motivo del rechazo.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class DecideBankAccountAuthorityCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IMotorDeAprobaciones motor)
    : IRequestHandler<DecideBankAccountAuthorityCommand, Result<BankAccountAuthorityDto>>
{
    public async Task<Result<BankAccountAuthorityDto>> Handle(DecideBankAccountAuthorityCommand request, CancellationToken ct)
    {
        var a = await FirmantesYTokens.BuscarAsync(db, request.AuthorityPublicId, seguir: false, ct);
        if (a is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(a.BankAccountId))
            return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.NoEncontrado("El firmante o token"));
        var solicitud = a.Status switch
        {
            BankAccountAuthorityState.PendingApproval => a.ApprovalRequestPublicId,
            BankAccountAuthorityState.PendingRetirement => a.RetirementApprovalRequestPublicId,
            _ => null,
        };
        if (solicitud is not { } id) return Result.Failure<BankAccountAuthorityDto>(ErroresDeTesoreria.FirmanteEstado(a.Status.ToString(), "pendiente de aprobación"));
        var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(id, request.Approve ? ApprovalDecisionKind.Approve : ApprovalDecisionKind.Reject, request.Reason,
            FirmantesYTokens.Huella(a)), ct);
        if (decidida.IsFailure) return Result.Failure<BankAccountAuthorityDto>(decidida.Error);
        var actual = await FirmantesYTokens.BuscarAsync(db, a.PublicId, seguir: false, ct);
        return Result.Success(await FirmantesYTokens.DtoAsync(db, actual!, ct));
    }
}

// ------------------------------------------------------------------------------------------------------------------- listar --

/// <summary>
/// <c>GET /api/treasury/bank-accounts/{id}/signers</c> y <c>…/tokens</c> (<c>Treasury.BankAccounts.View</c> o <c>Treasury.Signers.Manage</c>): los firmantes o los tokens de una
/// cuenta, con los retirados si se piden. (nuevo)
/// </summary>
public sealed record ListBankAccountAuthoritiesQuery(Guid BankAccountPublicId, string Kind, bool IncludeRetired = false) : IRequest<Result<IReadOnlyList<BankAccountAuthorityDto>>>;

public sealed class ListBankAccountAuthoritiesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListBankAccountAuthoritiesQuery, Result<IReadOnlyList<BankAccountAuthorityDto>>>
{
    public async Task<Result<IReadOnlyList<BankAccountAuthorityDto>>> Handle(ListBankAccountAuthoritiesQuery request, CancellationToken ct)
    {
        var cuenta = await CuentasPropias.VisibleAsync(db, alcance, request.BankAccountPublicId, seguir: false, ct);
        if (cuenta is null) return Result.Failure<IReadOnlyList<BankAccountAuthorityDto>>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria"));
        var vivos = new[] { BankAccountAuthorityState.PendingApproval, BankAccountAuthorityState.Active, BankAccountAuthorityState.PendingRetirement };
        List<BankAccountAuthority> filas = request.Kind == FirmantesYTokens.Token
            ? [.. await db.BankAccountTokens.AsNoTracking().Where(t => t.BankAccountId == cuenta.Id && (request.IncludeRetired || vivos.Contains(t.Status))).OrderBy(t => t.Id)
                .ToListAsync(ct)]
            : [.. await db.BankAccountSigners.AsNoTracking().Where(s => s.BankAccountId == cuenta.Id && (request.IncludeRetired || vivos.Contains(s.Status)))
                .OrderBy(s => s.SignatureClass).ThenBy(s => s.Id).ToListAsync(ct)];
        var lista = new List<BankAccountAuthorityDto>(filas.Count);
        foreach (var a in filas) lista.Add(await FirmantesYTokens.DtoAsync(db, a, ct));
        return Result.Success<IReadOnlyList<BankAccountAuthorityDto>>(lista);
    }
}

// --------------------------------------------------------------------------------------------------------- la vigilancia --

/// <summary>
/// La alerta <c>Tesoreria.FirmanteRetirado</c> (FR-208; nuevo): cada firmante o dueño de un token vigente que se retiró de la cooperativa (su usuario del ERP inactivo o todas
/// sus fichas de empleado retiradas), una vez por firmante o token. Sólo avisa: el retiro lo pide una persona con su acta. La corre <c>tesoreria.vigilancia</c>. (nuevo)
/// </summary>
public sealed class VigilanciaDeFirmantes(IApplicationDbContext db, IAlertas alertas, IDateTimeService reloj)
{
    public async Task<int> FirmantesRetiradosAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var vivos = new[] { BankAccountAuthorityState.Active, BankAccountAuthorityState.PendingRetirement };
        List<BankAccountAuthority> todos =
        [
            .. await db.BankAccountSigners.AsNoTracking().Where(s => vivos.Contains(s.Status)).ToListAsync(ct),
            .. await db.BankAccountTokens.AsNoTracking().Where(t => vivos.Contains(t.Status)).ToListAsync(ct),
        ];
        var alertados = 0;
        foreach (var a in todos.Where(x => x.Vigente(hoy)))
        {
            if (!await FirmantesYTokens.SeRetiroAsync(db, a, hoy, ct)) continue;
            var dto = await FirmantesYTokens.DtoAsync(db, a, ct);
            var que = a is BankAccountToken t ? $"tiene el token {t.Serial}" : $"es firmante {dto.SignatureClass}";
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaFirmanteRetirado, "Firmante retirado de la cooperativa",
                string.Create(CultureInfo.InvariantCulture,
                    $"{dto.PersonName} se retiró de la cooperativa y {que} de la cuenta {dto.BankAccountCode}: pida el retiro con su acta y avísele al banco."),
                a is BankAccountToken ? "BankAccountToken" : "BankAccountSigner", a.PublicId, DedupKey: $"Tesoreria.FirmanteRetirado:{a.PublicId:N}", ModuloDeOrigen: "Treasury"),
                ct);
            alertados++;
        }
        return alertados;
    }
}
