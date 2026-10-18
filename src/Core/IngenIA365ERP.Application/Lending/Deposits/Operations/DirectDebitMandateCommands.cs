using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

// Feature 014, US4 (T118, T119, T123; api.md §9; data-model §3.6; FR-090, R16): las autorizaciones de débito automático. La escribe quien tiene
// Lending.DirectDebits.Manage; el débito lo hace el ciclo diario (DebitoAutomatico), sin ruta. (nuevo)

/// <summary>Un débito hecho por una autorización (los últimos, para la pantalla). (nuevo)</summary>
public sealed record DirectDebitDoneDto(Guid OperationPublicId, string Number, DateOnly ValueDate, decimal Amount, bool Reversed);

/// <summary>Una autorización de débito automático (api.md §9 <c>DirectDebitMandate</c>). (nuevo)</summary>
public sealed record DirectDebitMandateDto(
    Guid PublicId,
    Guid AccountPublicId,
    string AccountNumber,
    MemberAccountPersonDto AuthorizedBy,
    DirectDebitScope Scope,
    Guid? LoanPublicId,
    string? LoanNumber,
    bool AllowsPartial,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Status,
    Guid AuthorizationAttachmentPublicId,
    string? RevokeReason,
    IReadOnlyList<DirectDebitDoneDto> LastDebits);

/// <summary>
/// Registra una autorización de débito automático (api.md §9, <c>POST /api/lending/direct-debits</c>, <c>Lending.DirectDebits.Manage</c>, con clave): sobre
/// una cuenta de ahorro a la vista o contractual (<c>Lending.DirectDebit.AccountNotDemand</c>), para un crédito del que el titular es deudor
/// (<c>Lending.DirectDebit.NotHolder</c>) o para todos los suyos; con el soporte firmado; desde hoy o después; sin cruzarse con otra vigente del mismo
/// crédito y cuenta (<c>Lending.DirectDebit.Overlaps</c>). (nuevo)
/// </summary>
public sealed record RegisterDirectDebitMandateCommand(
    Guid AccountPublicId,
    DirectDebitScope Scope,
    Guid? LoanPublicId,
    bool AllowsPartial,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    Guid AuthorizationAttachmentPublicId,
    Guid? AuthorizedByPersonPublicId = null) : IRequest<Result<DirectDebitMandateDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDirectDebitMandateCommandValidator : AbstractValidator<RegisterDirectDebitMandateCommand>
{
    public RegisterDirectDebitMandateCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.LoanPublicId).NotEmpty().When(x => x.Scope == DirectDebitScope.SingleLoan).WithMessage("Diga el crédito que se paga.");
        RuleFor(x => x.LoanPublicId).Null().When(x => x.Scope == DirectDebitScope.AllLoansOfHolder).WithMessage("Para todos los créditos no se nombra uno.");
        RuleFor(x => x.AuthorizationAttachmentPublicId).NotEmpty().WithMessage("Adjunte la autorización firmada por el titular.");
        RuleFor(x => x).Must(x => x.ValidTo is null || x.ValidFrom is null || x.ValidTo >= x.ValidFrom).WithMessage("La vigencia termina después de empezar.");
    }
}

public sealed class RegisterDirectDebitMandateCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterDirectDebitMandateCommand, Result<DirectDebitMandateDto>>
{
    public async Task<Result<DirectDebitMandateDto>> Handle(RegisterDirectDebitMandateCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var desde = request.ValidFrom ?? hoy;
        if (desde < hoy) return Falla(new ErrorConDatos("Lending.DirectDebit.Retroactive", "La autorización rige desde hoy o después.", new { validFrom = desde }));
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.Nature != LineNature.Deposit || cuenta.DepositClass is not (DepositClass.Demand or DepositClass.Contractual)
            || cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            return Falla(new ErrorConDatos(DirectDebitMandate.CodigoCuentaNoAdmitida,
                $"El débito automático se hace desde una cuenta de ahorro a la vista o contractual activa; la cuenta {cuenta.Number} no lo es.", new { account = cuenta.Number }));

        int? creditoId = null;
        if (request.LoanPublicId is { } publico)
        {
            var credito = await db.Loans.AsNoTracking().Where(l => l.PublicId == publico).Select(l => new { l.Id, l.DebtorPersonId, l.Number }).FirstOrDefaultAsync(ct);
            if (credito is null) return Falla(new Error("Lending.Loan.NotFound", "El crédito no existe."));
            if (credito.DebtorPersonId != cuenta.HolderPersonId)
                return Falla(new ErrorConDatos(DirectDebitMandate.CodigoNoEsTitular,
                    $"El titular de la cuenta {cuenta.Number} no es el deudor del crédito {credito.Number}: no puede autorizar su débito.", new { loan = credito.Number }));
            creditoId = credito.Id;
        }
        var autoriza = cuenta.HolderPersonId;
        if (request.AuthorizedByPersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            var esParte = id is { } pid && (pid == cuenta.HolderPersonId || await db.MemberAccountParties.AsNoTracking().AnyAsync(p => p.MemberAccountId == cuenta.Id
                && p.PersonId == pid && p.ValidTo == null && !p.IsDeleted && p.Role == MemberAccountPartyRole.LegalRepresentative, ct));
            if (!esParte) return Falla(new ErrorConDatos(DirectDebitMandate.CodigoNoEsTitular, "Autoriza el titular o su representante legal vigente.", new { person = persona }));
            autoriza = id!.Value;
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var cruzada = await db.DirectDebitMandates.AsNoTracking().AnyAsync(m => m.MemberAccountId == cuenta.Id && !m.IsDeleted && m.RevokedAt == null
                && (m.LoanId == creditoId || m.Scope == DirectDebitScope.AllLoansOfHolder || request.Scope == DirectDebitScope.AllLoansOfHolder)
                && (m.ValidTo == null || m.ValidTo >= desde) && (request.ValidTo == null || m.ValidFrom <= request.ValidTo), ct);
            if (cruzada)
                return Falla(new ErrorConDatos(DirectDebitMandate.CodigoSeCruza,
                    $"La cuenta {cuenta.Number} ya tiene una autorización vigente para ese crédito en esas fechas: revóquela primero.", new { account = cuenta.Number }));
            var mandato = new DirectDebitMandate
            {
                MemberAccountId = cuenta.Id,
                AuthorizedByPersonId = autoriza,
                Scope = request.Scope,
                LoanId = creditoId,
                AllowsPartialDebit = request.AllowsPartial,
                AuthorizationAttachmentPublicId = request.AuthorizationAttachmentPublicId,
                ValidFrom = desde,
                ValidTo = request.ValidTo,
            };
            db.DirectDebitMandates.Add(mandato);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeDebitoAutomatico.DtosAsync(db, [mandato], reloj.HoyLocal, ct))[0]);
        }, ct);
    }

    private static Result<DirectDebitMandateDto> Falla(Error e) => Result.Failure<DirectDebitMandateDto>(e);
}

/// <summary>Revoca una autorización (api.md §9, <c>POST /{id}/revoke</c>, <c>Lending.DirectDebits.Manage</c>, con clave). (nuevo)</summary>
public sealed record RevokeDirectDebitMandateCommand(Guid MandatePublicId, DateOnly? ValidTo, string Reason, Guid? EvidenceAttachmentPublicId = null)
    : IRequest<Result<DirectDebitMandateDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RevokeDirectDebitMandateCommandValidator : ValidadorConMotivo<RevokeDirectDebitMandateCommand>
{
    public RevokeDirectDebitMandateCommandValidator() => RuleFor(x => x.MandatePublicId).NotEmpty();
}

public sealed class RevokeDirectDebitMandateCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RevokeDirectDebitMandateCommand, Result<DirectDebitMandateDto>>
{
    public async Task<Result<DirectDebitMandateDto>> Handle(RevokeDirectDebitMandateCommand request, CancellationToken ct)
    {
        var mandato = await db.DirectDebitMandates.FirstOrDefaultAsync(m => m.PublicId == request.MandatePublicId && !m.IsDeleted, ct);
        if (mandato is null) return Falla(new Error(DirectDebitMandate.CodigoNoEncontrada, "La autorización de débito automático no existe."));
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == mandato.MemberAccountId, ct);
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, new Error(DirectDebitMandate.CodigoNoEncontrada,
            "La autorización de débito automático no existe."), auditoria, nameof(DirectDebitMandate), mandato.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        if (mandato.RevokedAt is not null) return Falla(new Error(DirectDebitMandate.CodigoYaRevocada, "La autorización ya está revocada."));
        var hoy = reloj.HoyLocal;
        var hasta = request.ValidTo ?? hoy;
        if (hasta < hoy.AddDays(-1)) return Falla(new ErrorConDatos("Lending.DirectDebit.Retroactive", "No se revoca hacia atrás.", new { validTo = hasta }));
        mandato.Revocar(hasta < mandato.ValidFrom.AddDays(-1) ? mandato.ValidFrom.AddDays(-1) : hasta, reloj.UtcNow, (await actorActual.ObtenerAsync(ct)).UserId,
            request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ConsultasDeDebitoAutomatico.DtosAsync(db, [mandato], reloj.HoyLocal, ct))[0]);
    }

    private static Result<DirectDebitMandateDto> Falla(Error e) => Result.Failure<DirectDebitMandateDto>(e);
}

/// <summary>Las autorizaciones (api.md §9, <c>GET /api/lending/direct-debits</c>, <c>Lending.Accounts.Read</c>), del alcance. (nuevo)</summary>
public sealed record ListDirectDebitMandatesQuery(Guid? PersonPublicId = null, Guid? AccountPublicId = null, Guid? LoanPublicId = null, string? Status = null)
    : IRequest<Result<IReadOnlyList<DirectDebitMandateDto>>>;

public sealed class ListDirectDebitMandatesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListDirectDebitMandatesQuery, Result<IReadOnlyList<DirectDebitMandateDto>>>
{
    public async Task<Result<IReadOnlyList<DirectDebitMandateDto>>> Handle(ListDirectDebitMandatesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted).PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.AccountPublicId is { } cuenta) cuentas = cuentas.Where(a => a.PublicId == cuenta);
        if (request.PersonPublicId is { } persona) cuentas = cuentas.Where(a => db.People.Any(p => p.Id == a.HolderPersonId && p.PublicId == persona));
        var ids = cuentas.Select(a => a.Id);
        var consulta = db.DirectDebitMandates.AsNoTracking().Where(m => !m.IsDeleted && ids.Contains(m.MemberAccountId));
        if (request.LoanPublicId is { } credito) consulta = consulta.Where(m => db.Loans.Any(l => l.Id == m.LoanId && l.PublicId == credito));
        var hoy = reloj.HoyLocal;
        if (string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase))
            consulta = consulta.Where(m => m.RevokedAt == null && (m.ValidTo == null || m.ValidTo >= hoy));
        if (string.Equals(request.Status, "Revoked", StringComparison.OrdinalIgnoreCase)) consulta = consulta.Where(m => m.RevokedAt != null);
        var filas = await consulta.OrderByDescending(m => m.ValidFrom).ThenByDescending(m => m.Id).Take(500).ToListAsync(ct);
        return Result.Success(await ConsultasDeDebitoAutomatico.DtosAsync(db, filas, hoy, ct));
    }
}

/// <summary>Arma los DTO de las autorizaciones con sus últimos débitos. (nuevo)</summary>
public static class ConsultasDeDebitoAutomatico
{
    public static async Task<IReadOnlyList<DirectDebitMandateDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<DirectDebitMandate> mandatos, DateOnly dia,
        CancellationToken ct)
    {

        var idsDeCuenta = mandatos.Select(m => m.MemberAccountId).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => idsDeCuenta.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.PublicId, a.Number }, ct);
        var idsDeCredito = mandatos.Where(m => m.LoanId is not null).Select(m => m.LoanId!.Value).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => idsDeCredito.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => new { l.PublicId, l.Number }, ct);
        var personas = await LectorDeCuentas.PersonasAsync(db, mandatos.Select(m => m.AuthorizedByPersonId), ct);
        var debitos = await db.MemberAccountOperations.AsNoTracking()
            .Where(o => idsDeCuenta.Contains(o.MemberAccountId) && o.Kind == MemberAccountOperationKind.DirectDebit)
            .OrderByDescending(o => o.Id).Take(100)
            .Select(o => new { o.MemberAccountId, Dto = new DirectDebitDoneDto(o.PublicId, o.Number, o.ValueDate, o.Amount, o.ReversedByOperationId != null) })
            .ToListAsync(ct);
        return mandatos.Select(m => new DirectDebitMandateDto(m.PublicId, cuentas[m.MemberAccountId].PublicId, cuentas[m.MemberAccountId].Number,
            personas[m.AuthorizedByPersonId], m.Scope, m.LoanId is { } l ? creditos.GetValueOrDefault(l)?.PublicId : null,
            m.LoanId is { } n ? creditos.GetValueOrDefault(n)?.Number : null, m.AllowsPartialDebit, m.ValidFrom, m.ValidTo,
            m.RevokedAt is not null ? "Revoked" : m.ValidTo is { } fin && fin < dia ? "Ended" : m.ValidFrom > dia ? "Scheduled" : "Active",
            m.AuthorizationAttachmentPublicId, m.RevokeReason,
            debitos.Where(x => x.MemberAccountId == m.MemberAccountId).Select(x => x.Dto).Take(5).ToList())).ToList();
    }
}
