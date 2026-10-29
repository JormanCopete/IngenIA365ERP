using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.PersonBankAccounts;

// Feature 015, E-06 (T0029; R-21; FR-210; contracts/api.md §15; data-model §16.1): las cuentas bancarias de una persona con doble control. Una persona
// pide el alta o el cambio con la certificación bancaria (Core.PersonBankAccounts.Request); otra la aprueba, la rechaza o la retira
// (Core.PersonBankAccounts.Approve). La segregación compara SEC_Users.Id (IActorActual), nunca el usuario del token. Cada escritura lleva
// Idempotency-Key y deja Core.PersonBankAccount.Changed en la cadena; leerlas deja Core.PersonBankAccount.Read (R-32).

/// <summary>Una cuenta bancaria de una persona, como la ve la pantalla (api.md §15). (nuevo)</summary>
public sealed record PersonBankAccountDto(
    Guid PublicId,
    Guid PersonPublicId,
    Guid BankPublicId,
    string BankName,
    string? BankTransferCode,
    string AccountType,
    string AccountNumber,
    string HolderName,
    string HolderTaxId,
    Guid? CertificateAttachmentPublicId,
    PersonBankAccountOrigin Origin,
    PersonBankAccountState Status,
    string RequestedByName,
    string? DecidedByName,
    DateTime? DecidedAt,
    DateOnly? PayableFrom,
    string? Reason,
    Guid? ReplacesPublicId,
    DateTime CreatedAt);

/// <summary>Los errores de las cuentas bancarias de la persona (data-model §21). (nuevo)</summary>
public static class ErroresDeCuentasBancariasDePersona
{
    public const string CodigoAutoaprobacion = "Core.PersonBankAccount.SelfApproval";
    public const string CodigoCertificacionRequerida = "Core.PersonBankAccount.CertificateRequired";
    public const string CodigoNoPendiente = "Core.PersonBankAccount.NotPending";
    public const string CodigoNoActiva = "Core.PersonBankAccount.NotActive";

    public static Error Autoaprobacion() => new(CodigoAutoaprobacion,
        "Usted pidió esta cuenta: no puede aprobarla ni rechazarla. La decide otra persona con el permiso de aprobar cuentas bancarias.");

    public static Error CertificacionRequerida() => new(CodigoCertificacionRequerida,
        "Adjunte la certificación bancaria de la cuenta (titularidad) antes de pedir el alta o el cambio.");

    public static Error NoPendiente(PersonBankAccountState estado) => new ErrorConDatos(CodigoNoPendiente,
        "La cuenta ya no está pendiente de aprobación: otra persona la decidió.", new { status = estado.ToString() });

    public static Error NoActiva(PersonBankAccountState estado) => new ErrorConDatos(CodigoNoActiva,
        "Sólo se retira o se reemplaza una cuenta activa.", new { status = estado.ToString() });

    public static Error NoEncontrada() => new("Generic.NotFound", "Cuenta bancaria no encontrada.");

    public static Error PersonaNoEncontrada() => new("Generic.NotFound", "Persona no encontrada.");

    public static Error BancoNoEncontrado() => new("Generic.NotFound", "Banco no encontrado.");
}

/// <summary>Cómo se proyecta y audita una cuenta (compartido por comandos y consulta). (nuevo)</summary>
internal static class CuentasBancariasDePersonaComun
{
    public const string EventoLeida = "Core.PersonBankAccount.Read";
    public const string EventoCambiada = "Core.PersonBankAccount.Changed";

    public static async Task<PersonBankAccountDto> ADtoAsync(IApplicationDbContext db, PersonBankAccount c, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().Where(p => p.Id == c.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        var banco = await db.Banks.AsNoTracking().Where(b => b.Id == c.BankId).Select(b => new { b.PublicId, b.Name, b.TransferCode }).FirstAsync(ct);
        Guid? reemplaza = c.ReplacesId is { } r
            ? await db.PersonBankAccounts.AsNoTracking().Where(x => x.Id == r).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return new PersonBankAccountDto(c.PublicId, persona, banco.PublicId, banco.Name, banco.TransferCode, c.AccountType, c.AccountNumber, c.HolderName,
            c.HolderTaxId, c.CertificateAttachmentPublicId, c.Origin, c.Status, c.RequestedByName, c.DecidedByName, c.DecidedAt, c.PayableFrom, c.Reason,
            reemplaza, c.CreatedAt);
    }

    /// <summary>El evento sensible en la cadena de sellos (módulo Treasury: las cuentas destino son de giro).</summary>
    public static Task RegistrarAsync(IServiceProvider servicios, string accion, PersonBankAccount cuenta, object? detalle, CancellationToken ct) =>
        AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
        {
            Action = accion,
            EntityType = nameof(PersonBankAccount),
            EntityId = cuenta.PublicId.ToString(),
            Module = ModuloDeAuditoria.Treasury,
            NewValues = detalle,
        }, ct);

    /// <summary>Normaliza un número de cuenta: sin espacios ni guiones.</summary>
    public static string Normalizar(string numero) => new string(numero.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

/// <summary>
/// Pide el alta de una cuenta bancaria de la persona, o el cambio de una activa (<see cref="ReplacesPublicId"/>): nace <c>PendingApproval</c> con la
/// certificación obligatoria (<c>Core.PersonBankAccount.CertificateRequired</c>), que es un adjunto <c>PersonBankCertificate</c> de la misma
/// persona. Si la misma cuenta ya es de otra persona (o de un usuario interno), levanta <c>Plataforma.CuentaDestinoCompartida</c>. (nuevo)
/// </summary>
public sealed record RequestPersonBankAccountCommand(
    Guid PersonPublicId,
    Guid BankPublicId,
    string AccountType,
    string AccountNumber,
    string HolderName,
    string HolderTaxId,
    Guid? CertificateAttachmentPublicId,
    Guid? ReplacesPublicId) : IRequest<Result<PersonBankAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestPersonBankAccountCommandValidator : AbstractValidator<RequestPersonBankAccountCommand>
{
    public RequestPersonBankAccountCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEqual(Guid.Empty).WithMessage("Indique la persona.");
        RuleFor(x => x.BankPublicId).NotEqual(Guid.Empty).WithMessage("Indique el banco.");
        RuleFor(x => x.AccountType).NotEmpty().WithMessage("Indique el tipo de cuenta.").MaximumLength(20);
        RuleFor(x => x.AccountNumber).NotEmpty().WithMessage("Indique el número de la cuenta.").MaximumLength(30)
            .Must(n => n is null || n.Any(char.IsDigit)).WithMessage("El número de la cuenta lleva dígitos.");
        RuleFor(x => x.HolderName).NotEmpty().WithMessage("Indique el titular según la certificación.").MaximumLength(200);
        RuleFor(x => x.HolderTaxId).NotEmpty().WithMessage("Indique el documento del titular según la certificación.").MaximumLength(20);
    }
}

public sealed class RequestPersonBankAccountCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlertas alertas,
    IServiceProvider servicios) : IRequestHandler<RequestPersonBankAccountCommand, Result<PersonBankAccountDto>>
{
    public async Task<Result<PersonBankAccountDto>> Handle(RequestPersonBankAccountCommand request, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.PersonaNoEncontrada());
        var banco = await db.Banks.AsNoTracking().Where(b => b.PublicId == request.BankPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (banco is null) return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.BancoNoEncontrado());

        // La certificación es un adjunto de la persona, del tipo de la certificación y ya disponible.
        if (request.CertificateAttachmentPublicId is not { } certificacion
            || !await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == certificacion
                && a.OwnerEntityType == AdjuntosDeModulo.CertificacionBancariaDePersona
                && a.OwnerEntityPublicId == request.PersonPublicId
                && a.Status == EstadoDeAdjunto.Available, ct))
            return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.CertificacionRequerida());

        PersonBankAccount? reemplazada = null;
        if (request.ReplacesPublicId is { } anterior)
        {
            reemplazada = await db.PersonBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == anterior && c.PersonId == persona, ct);
            if (reemplazada is null) return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoEncontrada());
            if (reemplazada.Status != PersonBankAccountState.Active)
                return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoActiva(reemplazada.Status));
        }

        var actor = await actorActual.ObtenerAsync(ct);
        var numero = CuentasBancariasDePersonaComun.Normalizar(request.AccountNumber);
        var cuenta = new PersonBankAccount
        {
            PersonId = persona.Value,
            BankId = banco.Value,
            AccountType = request.AccountType.Trim(),
            AccountNumber = numero,
            HolderName = request.HolderName.Trim(),
            HolderTaxId = request.HolderTaxId.Trim(),
            CertificateAttachmentPublicId = certificacion,
            Origin = PersonBankAccountOrigin.Registered,
            Status = PersonBankAccountState.PendingApproval,
            RequestedByUserId = actor.UserId,
            RequestedByName = actor.Name,
            ReplacesId = reemplazada?.Id,
        };
        db.PersonBankAccounts.Add(cuenta);
        await db.SaveChangesAsync(ct);

        var compartida = await db.PersonBankAccounts.AsNoTracking().AnyAsync(c => c.BankId == banco && c.AccountNumber == numero && c.PersonId != persona
            && (c.Status == PersonBankAccountState.Active || c.Status == PersonBankAccountState.PendingApproval), ct);
        if (compartida)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.PlataformaCuentaDestinoCompartida,
                "Cuenta bancaria compartida entre personas",
                "La cuenta pedida ya está registrada para otra persona: revise la certificación antes de aprobarla.",
                nameof(PersonBankAccount), cuenta.PublicId, DedupKey: $"cuenta-compartida:{cuenta.PublicId:N}", ModuloDeOrigen: "Core"), ct);

        await CuentasBancariasDePersonaComun.RegistrarAsync(servicios, CuentasBancariasDePersonaComun.EventoCambiada, cuenta,
            new { change = "Requested", replaces = request.ReplacesPublicId, sharedWithOtherPerson = compartida }, ct);
        return Result.Success(await CuentasBancariasDePersonaComun.ADtoAsync(db, cuenta, ct));
    }
}

/// <summary>
/// Aprueba una cuenta pendiente: otra persona que la que la pidió (<c>Core.PersonBankAccount.SelfApproval</c>). Queda <c>Active</c> con
/// <c>PayableFrom</c> = hoy + <c>CuentasDestino.DiasDeEspera</c> (sin valor, hoy) y, si reemplaza a otra, aquélla queda <c>Retired</c>. (nuevo)
/// </summary>
public sealed record ApprovePersonBankAccountCommand(Guid AccountPublicId) : IRequest<Result<PersonBankAccountDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApprovePersonBankAccountCommandValidator : AbstractValidator<ApprovePersonBankAccountCommand>
{
    public ApprovePersonBankAccountCommandValidator() => RuleFor(x => x.AccountPublicId).NotEqual(Guid.Empty);
}

public sealed class ApprovePersonBankAccountCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    IServiceProvider servicios) : IRequestHandler<ApprovePersonBankAccountCommand, Result<PersonBankAccountDto>>
{
    public async Task<Result<PersonBankAccountDto>> Handle(ApprovePersonBankAccountCommand request, CancellationToken ct)
    {
        var cuenta = await db.PersonBankAccounts.FirstOrDefaultAsync(c => c.PublicId == request.AccountPublicId, ct);
        if (cuenta is null) return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoEncontrada());
        if (cuenta.Status != PersonBankAccountState.PendingApproval)
            return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoPendiente(cuenta.Status));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null || actor.UserId == cuenta.RequestedByUserId)
            return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.Autoaprobacion());

        var hoy = reloj.HoyLocal;
        var espera = await parametros.LeerComoAsync<int?>(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, ParametrosDeRecaudoYTesoreria.CuentasDestinoDiasDeEspera, hoy, ct: ct);
        if (espera.IsFailure) return Result.Failure<PersonBankAccountDto>(espera.Error);

        cuenta.Status = PersonBankAccountState.Active;
        cuenta.DecidedByUserId = actor.UserId;
        cuenta.DecidedByName = actor.Name;
        cuenta.DecidedAt = reloj.UtcNow;
        cuenta.PayableFrom = hoy.AddDays(espera.Value ?? 0);

        if (cuenta.ReplacesId is { } anterior
            && await db.PersonBankAccounts.FirstOrDefaultAsync(c => c.Id == anterior, ct) is { Status: PersonBankAccountState.Active } reemplazada)
        {
            reemplazada.Status = PersonBankAccountState.Retired;
            reemplazada.RetiredAt = reloj.UtcNow;
            reemplazada.Reason = $"Reemplazada por la cuenta {cuenta.PublicId}.";
        }

        await db.SaveChangesAsync(ct);
        await CuentasBancariasDePersonaComun.RegistrarAsync(servicios, CuentasBancariasDePersonaComun.EventoCambiada, cuenta,
            new { change = "Approved", payableFrom = cuenta.PayableFrom }, ct);
        return Result.Success(await CuentasBancariasDePersonaComun.ADtoAsync(db, cuenta, ct));
    }
}

/// <summary>Rechaza una cuenta pendiente con motivo: otra persona que la que la pidió. (nuevo)</summary>
public sealed record RejectPersonBankAccountCommand(Guid AccountPublicId, string Reason) : IRequest<Result<PersonBankAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectPersonBankAccountCommandValidator : ValidadorConMotivo<RejectPersonBankAccountCommand>
{
    public RejectPersonBankAccountCommandValidator() => RuleFor(x => x.AccountPublicId).NotEqual(Guid.Empty);
}

public sealed class RejectPersonBankAccountCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IServiceProvider servicios) : IRequestHandler<RejectPersonBankAccountCommand, Result<PersonBankAccountDto>>
{
    public async Task<Result<PersonBankAccountDto>> Handle(RejectPersonBankAccountCommand request, CancellationToken ct)
    {
        var cuenta = await db.PersonBankAccounts.FirstOrDefaultAsync(c => c.PublicId == request.AccountPublicId, ct);
        if (cuenta is null) return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoEncontrada());
        if (cuenta.Status != PersonBankAccountState.PendingApproval)
            return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoPendiente(cuenta.Status));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null || actor.UserId == cuenta.RequestedByUserId)
            return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.Autoaprobacion());

        cuenta.Status = PersonBankAccountState.Rejected;
        cuenta.DecidedByUserId = actor.UserId;
        cuenta.DecidedByName = actor.Name;
        cuenta.DecidedAt = reloj.UtcNow;
        cuenta.Reason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await CuentasBancariasDePersonaComun.RegistrarAsync(servicios, CuentasBancariasDePersonaComun.EventoCambiada, cuenta, new { change = "Rejected" }, ct);
        return Result.Success(await CuentasBancariasDePersonaComun.ADtoAsync(db, cuenta, ct));
    }
}

/// <summary>Retira una cuenta activa con motivo: deja de recibir giros. (nuevo)</summary>
public sealed record RetirePersonBankAccountCommand(Guid AccountPublicId, string Reason) : IRequest<Result<PersonBankAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RetirePersonBankAccountCommandValidator : ValidadorConMotivo<RetirePersonBankAccountCommand>
{
    public RetirePersonBankAccountCommandValidator() => RuleFor(x => x.AccountPublicId).NotEqual(Guid.Empty);
}

public sealed class RetirePersonBankAccountCommandHandler(
    IApplicationDbContext db,
    IDateTimeService reloj,
    IServiceProvider servicios) : IRequestHandler<RetirePersonBankAccountCommand, Result<PersonBankAccountDto>>
{
    public async Task<Result<PersonBankAccountDto>> Handle(RetirePersonBankAccountCommand request, CancellationToken ct)
    {
        var cuenta = await db.PersonBankAccounts.FirstOrDefaultAsync(c => c.PublicId == request.AccountPublicId, ct);
        if (cuenta is null) return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoEncontrada());
        if (cuenta.Status != PersonBankAccountState.Active)
            return Result.Failure<PersonBankAccountDto>(ErroresDeCuentasBancariasDePersona.NoActiva(cuenta.Status));

        cuenta.Status = PersonBankAccountState.Retired;
        cuenta.RetiredAt = reloj.UtcNow;
        cuenta.Reason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await CuentasBancariasDePersonaComun.RegistrarAsync(servicios, CuentasBancariasDePersonaComun.EventoCambiada, cuenta, new { change = "Retired" }, ct);
        return Result.Success(await CuentasBancariasDePersonaComun.ADtoAsync(db, cuenta, ct));
    }
}

/// <summary>Las cuentas bancarias de una persona, todas sus filas (la historia), como lectura sensible auditada (<c>Core.PersonBankAccount.Read</c>). (nuevo)</summary>
public sealed record ListPersonBankAccountsQuery(Guid PersonPublicId) : IRequest<Result<IReadOnlyList<PersonBankAccountDto>>>;

public sealed class ListPersonBankAccountsQueryHandler(IApplicationDbContext db, IServiceProvider servicios)
    : IRequestHandler<ListPersonBankAccountsQuery, Result<IReadOnlyList<PersonBankAccountDto>>>
{
    public async Task<Result<IReadOnlyList<PersonBankAccountDto>>> Handle(ListPersonBankAccountsQuery request, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<IReadOnlyList<PersonBankAccountDto>>(ErroresDeCuentasBancariasDePersona.PersonaNoEncontrada());

        var cuentas = await db.PersonBankAccounts.AsNoTracking().Where(c => c.PersonId == persona).OrderByDescending(c => c.Id).ToListAsync(ct);
        var lista = new List<PersonBankAccountDto>(cuentas.Count);
        foreach (var c in cuentas) lista.Add(await CuentasBancariasDePersonaComun.ADtoAsync(db, c, ct));

        await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
        {
            Action = CuentasBancariasDePersonaComun.EventoLeida,
            EntityType = "Person",
            EntityId = request.PersonPublicId.ToString(),
            Module = ModuloDeAuditoria.Treasury,
            NewValues = new { accounts = lista.Count },
        }, ct);
        return Result.Success<IReadOnlyList<PersonBankAccountDto>>(lista);
    }
}
