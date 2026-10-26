using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Checkbooks;

/// <summary>Una chequera, para la pantalla. (nuevo)</summary>
public sealed record CheckbookDto(Guid PublicId, Guid BankAccountPublicId, string BankAccountCode, long FromNumber, long ToNumber, long NextNumber, int Remaining,
    CheckbookState Status, string CustodianName, Guid? CustodyAttachmentPublicId, DateTime RegisteredAt);

/// <summary>Un cheque girado o un cheque en blanco dañado. (nuevo)</summary>
public sealed record IssuedCheckDto(Guid PublicId, Guid CheckbookPublicId, string BankAccountCode, long CheckNumber, Guid? PaymentOrderPublicId, string? OrderNumber,
    Guid? BeneficiaryPersonPublicId, string? BeneficiaryName, decimal Amount, DateOnly IssuedOn, bool Crossed, bool FirstBeneficiaryOnly, IssuedCheckState Status,
    DateOnly? DeliveredOn, DateOnly? CashedOn, string? VoidReason, int DaysWithoutCashing);

/// <summary>Lo común de las chequeras (feature 015, US19; R-19; FR-134 a FR-136, FR-219): visibilidad por la cuenta, el siguiente número bajo el cerrojo, las transiciones y los DTO. (nuevo)</summary>
public static class ChequerasDeTesoreria
{
    public static async Task<Checkbook?> VisibleAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.Checkbooks : db.Checkbooks.AsNoTracking();
        var c = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        return c is not null && (await alcance.ObtenerAsync(ct)).IncluyeCuenta(c.BankAccountId) ? c : null;
    }

    public static async Task<IssuedCheck?> ChequeVisibleAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.IssuedChecks : db.IssuedChecks.AsNoTracking();
        var c = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (c is null) return null;
        var cuenta = await db.Checkbooks.AsNoTracking().Where(x => x.Id == c.CheckbookId).Select(x => x.BankAccountId).FirstAsync(ct);
        return (await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta) ? c : null;
    }

    /// <summary>El siguiente número de la chequera (ya bloqueada por quien llama): agotada, <c>Treasury.Checkbook.Exhausted</c>; marca agotada la que llega al final.</summary>
    public static Result<long> Siguiente(Checkbook c)
    {
        if (c.Status == CheckbookState.Voided) return Result.Failure<long>(ErroresDeTesoreria.ChequeraAnulada());
        if (c.Status == CheckbookState.Exhausted || c.NextCheckNumber > c.ToNumber) return Result.Failure<long>(ErroresDeTesoreria.ChequeraAgotada());
        var n = c.NextCheckNumber;
        c.NextCheckNumber++;
        if (c.NextCheckNumber > c.ToNumber) c.Status = CheckbookState.Exhausted;
        return Result.Success(n);
    }

    public static void Transicion(IApplicationDbContext db, IssuedCheck c, IssuedCheckState hacia, int usuario, DateTime ahora, Guid? soporte = null, string? nota = null)
    {
        db.IssuedCheckEvents.Add(new IssuedCheckEvent
        {
            IssuedCheckId = c.Id, FromStatus = c.Status, ToStatus = hacia, ByUserId = usuario, At = ahora, SupportAttachmentPublicId = soporte,
            Note = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(),
        });
        c.Status = hacia;
    }

    public static async Task<CheckbookDto> DtoAsync(IApplicationDbContext db, Checkbook c, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(x => x.Id == c.BankAccountId).Select(x => new { x.PublicId, x.Code }).FirstAsync(ct);
        var custodio = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == c.CustodianUserId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? string.Empty;
        var restantes = c.Status == CheckbookState.Active ? (int)Math.Max(0, c.ToNumber - c.NextCheckNumber + 1) : 0;
        return new CheckbookDto(c.PublicId, cuenta.PublicId, cuenta.Code, c.FromNumber, c.ToNumber, c.NextCheckNumber, restantes, c.Status, custodio, c.CustodyAttachmentPublicId,
            c.RegisteredAt);
    }

    public static async Task<IssuedCheckDto> DtoAsync(IApplicationDbContext db, IssuedCheck c, DateOnly hoy, CancellationToken ct)
    {
        var chequera = await (from k in db.Checkbooks.AsNoTracking()
                              join a in db.OwnBankAccounts.AsNoTracking() on k.BankAccountId equals a.Id
                              where k.Id == c.CheckbookId
                              select new { k.PublicId, a.Code }).FirstAsync(ct);
        var orden = c.PaymentOrderId is { } o
            ? await db.PaymentOrders.AsNoTracking().Where(x => x.Id == o).Select(x => new { x.PublicId, x.Number }).FirstOrDefaultAsync(ct)
            : null;
        var persona = c.BeneficiaryPersonId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p)
                .Select(x => new { x.PublicId, Nombre = (x.BusinessName ?? (x.FirstName + " " + x.LastName)).Trim() }).FirstOrDefaultAsync(ct)
            : null;
        var dias = c.Status == IssuedCheckState.Delivered && c.DeliveredOn is { } d ? hoy.DayNumber - d.DayNumber : 0;
        return new IssuedCheckDto(c.PublicId, chequera.PublicId, chequera.Code, c.CheckNumber, orden?.PublicId, orden?.Number, persona?.PublicId, persona?.Nombre, c.Amount,
            c.IssuedOn, c.Crossed, c.FirstBeneficiaryOnly, c.Status, c.DeliveredOn, c.CashedOn, c.VoidReason, dias);
    }
}

/// <summary>
/// <c>POST /api/treasury/checkbooks</c> (<c>Treasury.Checkbooks.Manage</c>, con clave; FR-134, FR-219): registra la chequera de una cuenta propia con su rango, su custodio y la
/// constancia de custodia. Un rango que se cruza con otra chequera de la misma cuenta: <c>Treasury.Checkbook.RangeOverlaps</c>. (nuevo)
/// </summary>
public sealed record RegisterCheckbookCommand(Guid BankAccountPublicId, long FromNumber, long ToNumber, Guid? CustodianUserPublicId = null,
    Guid? CustodyAttachmentPublicId = null) : IRequest<Result<CheckbookDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterCheckbookCommandValidator : AbstractValidator<RegisterCheckbookCommand>
{
    public RegisterCheckbookCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.FromNumber).GreaterThan(0);
        RuleFor(x => x.ToNumber).GreaterThanOrEqualTo(x => x.FromNumber).WithMessage("El rango termina en un número mayor o igual al primero.");
        RuleFor(x => x.ToNumber - x.FromNumber).LessThanOrEqualTo(10_000).WithMessage("Una chequera tiene a lo sumo diez mil cheques.");
    }
}

public sealed class RegisterCheckbookCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterCheckbookCommand, Result<CheckbookDto>>
{
    public async Task<Result<CheckbookDto>> Handle(RegisterCheckbookCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CheckbookDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id)) return Result.Failure<CheckbookDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        if (!cuenta.Activa(reloj.HoyLocal)) return Result.Failure<CheckbookDto>(ErroresDeTesoreria.CuentaNoAdmiteChequera());
        var custodio = usuario;
        if (request.CustodianUserPublicId is { } c)
        {
            var id = await db.Users.AsNoTracking().Where(u => u.PublicId == c).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
            if (id is null) return Result.Failure<CheckbookDto>(ErroresDeTesoreria.NoEncontrado("El custodio"));
            custodio = id.Value;
        }
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], ct: ct);
        if (await db.Checkbooks.AnyAsync(k => k.BankAccountId == cuenta.Id && k.Status != CheckbookState.Voided && k.FromNumber <= request.ToNumber && k.ToNumber >= request.FromNumber, ct))
            return Result.Failure<CheckbookDto>(ErroresDeTesoreria.ChequeraSeCruza());
        var chequera = new Checkbook
        {
            BankAccountId = cuenta.Id, FromNumber = request.FromNumber, ToNumber = request.ToNumber, NextCheckNumber = request.FromNumber, CustodianUserId = custodio,
            CustodyAttachmentPublicId = request.CustodyAttachmentPublicId, RegisteredByUserId = usuario, RegisteredAt = reloj.UtcNow,
        };
        db.Checkbooks.Add(chequera);
        await auditoria.AgregarAsync("Treasury.Checkbook.Registered", "Checkbook", chequera.PublicId, null,
            new { account = cuenta.Code, from = request.FromNumber, to = request.ToNumber, custodian = custodio, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, chequera, ct));
    }
}

/// <summary><c>POST /api/treasury/checkbooks/{id}/void</c> (<c>Treasury.Checkbooks.Manage</c>, con clave): anula la chequera (perdida o retirada) con motivo; sus números sin usar no se usan. (nuevo)</summary>
public sealed record VoidCheckbookCommand(Guid CheckbookPublicId, string Reason) : IRequest<Result<CheckbookDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidCheckbookCommandValidator : AbstractValidator<VoidCheckbookCommand>
{
    public VoidCheckbookCommandValidator()
    {
        RuleFor(x => x.CheckbookPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class VoidCheckbookCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IActorActual actorActual, ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<VoidCheckbookCommand, Result<CheckbookDto>>
{
    public async Task<Result<CheckbookDto>> Handle(VoidCheckbookCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var leida = await ChequerasDeTesoreria.VisibleAsync(db, alcance, request.CheckbookPublicId, seguir: false, ct);
        if (leida is null) return Result.Failure<CheckbookDto>(ErroresDeTesoreria.NoEncontrado("La chequera"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Chequera, [leida.Id], ct: ct);
        var chequera = await db.Checkbooks.FirstAsync(k => k.Id == leida.Id, ct);
        if (chequera.Status == CheckbookState.Voided) return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, chequera, ct));
        chequera.Status = CheckbookState.Voided;
        chequera.VoidReason = request.Reason.Trim();
        await auditoria.AgregarAsync("Treasury.Checkbook.Voided", "Checkbook", chequera.PublicId, null,
            new { next = chequera.NextCheckNumber, to = chequera.ToNumber, reason = chequera.VoidReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, chequera, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/checkbooks/{id}/spoiled</c> (<c>Treasury.Checks.Print</c>, con clave; FR-136): el siguiente cheque en blanco se dañó; queda <c>SpoiledBlank</c> con su
/// número y su motivo, y explica el hueco de la numeración. (nuevo)
/// </summary>
public sealed record SpoilBlankCheckCommand(Guid CheckbookPublicId, string Reason) : IRequest<Result<IssuedCheckDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class SpoilBlankCheckCommandValidator : AbstractValidator<SpoilBlankCheckCommand>
{
    public SpoilBlankCheckCommandValidator()
    {
        RuleFor(x => x.CheckbookPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class SpoilBlankCheckCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IActorActual actorActual, ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria, IDateTimeService reloj) : IRequestHandler<SpoilBlankCheckCommand, Result<IssuedCheckDto>>
{
    public async Task<Result<IssuedCheckDto>> Handle(SpoilBlankCheckCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leida = await ChequerasDeTesoreria.VisibleAsync(db, alcance, request.CheckbookPublicId, seguir: false, ct);
        if (leida is null) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("La chequera"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Chequera, [leida.Id], ct: ct);
        var chequera = await db.Checkbooks.FirstAsync(k => k.Id == leida.Id, ct);
        var numero = ChequerasDeTesoreria.Siguiente(chequera);
        if (numero.IsFailure) return Result.Failure<IssuedCheckDto>(numero.Error);
        var cheque = new IssuedCheck
        {
            CheckbookId = chequera.Id, CheckNumber = numero.Value, Amount = 0m, IssuedOn = reloj.HoyLocal, Status = IssuedCheckState.SpoiledBlank, VoidReason = request.Reason.Trim(),
        };
        db.IssuedChecks.Add(cheque);
        await db.SaveChangesAsync(ct);
        db.IssuedCheckEvents.Add(new IssuedCheckEvent
        {
            IssuedCheckId = cheque.Id, FromStatus = IssuedCheckState.SpoiledBlank, ToStatus = IssuedCheckState.SpoiledBlank, ByUserId = usuario, At = reloj.UtcNow, Note = cheque.VoidReason,
        });
        await auditoria.AgregarAsync("Treasury.Check.SpoiledBlank", "IssuedCheck", cheque.PublicId, null, new { number = cheque.CheckNumber, reason = cheque.VoidReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, cheque, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/checks</c> (<c>Treasury.Checks.Print</c>, con clave; R-19; FR-135, FR-136): emite los cheques de un lote aprobado de forma
/// «cheques» desde una chequera de su cuenta, uno por orden sin cheque vivo, con el número siguiente bajo el cerrojo de la chequera, cruzados y para el primer beneficiario
/// por defecto. Cada cheque emite <c>ChequeGiradoEmitido</c> al <b>emitir</b>; el lote y sus órdenes quedan enviados. El «girada» lo registra la entrega con constancia. (nuevo)
/// </summary>
public sealed record IssueChecksForBatchCommand(Guid BatchPublicId, Guid CheckbookPublicId, bool Crossed = true, bool FirstBeneficiaryOnly = true)
    : IRequest<Result<IReadOnlyList<IssuedCheckDto>>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class IssueChecksForBatchCommandValidator : AbstractValidator<IssueChecksForBatchCommand>
{
    public IssueChecksForBatchCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.CheckbookPublicId).NotEmpty();
    }
}

public sealed class IssueChecksForBatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<IssueChecksForBatchCommand, Result<IReadOnlyList<IssuedCheckDto>>>
{
    public async Task<Result<IReadOnlyList<IssuedCheckDto>>> Handle(IssueChecksForBatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var mio = await alcance.ObtenerAsync(ct);
        var loteLeido = await db.PaymentBatches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.BatchPublicId, ct);
        if (loteLeido is null || !mio.IncluyeCuenta(loteLeido.BankAccountId)) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.NoEncontrado("El lote"));
        if (loteLeido.Form != PaymentBatchForm.Checks) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.LoteNoEsDeCheques());
        if (loteLeido.Status is not (PaymentBatchState.Approved or PaymentBatchState.Sent or PaymentBatchState.PartiallyConfirmed))
            return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.LoteNoAprobado(loteLeido.Status.ToString()));
        var chequeraLeida = await ChequerasDeTesoreria.VisibleAsync(db, alcance, request.CheckbookPublicId, seguir: false, ct);
        if (chequeraLeida is null || chequeraLeida.BankAccountId != loteLeido.BankAccountId)
            return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.ChequeraDeOtraCuenta());

        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var items = await db.PaymentBatchItems.AsNoTracking().Where(i => i.PaymentBatchId == loteLeido.Id && i.IsLive).OrderBy(i => i.Id).ToListAsync(ct);
        var ordenesIds = items.Select(i => i.PaymentOrderId).ToList();
        var conCheque = await db.IssuedChecks.AsNoTracking()
            .Where(c => c.PaymentOrderId != null && ordenesIds.Contains(c.PaymentOrderId.Value) && c.Status != IssuedCheckState.Voided && c.Status != IssuedCheckState.SpoiledBlank)
            .Select(c => c.PaymentOrderId!.Value).ToListAsync(ct);
        var pendientes = items.Where(i => !conCheque.Contains(i.PaymentOrderId)).ToList();
        if (pendientes.Count == 0) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.LoteSinChequesPorEmitir());

        // Validación previa contable (FR-221): ChequeGiradoEmitido tiene que tener su regla antes de emitir el primer cheque.
        var cuentaPrevia = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == loteLeido.BankAccountId, ct);
        var previa = await emision.ValidacionPreviaAsync([ChequeGiradoEmitidoV1.Type], cuentaPrevia, hoy, ct);
        if (previa.IsFailure) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [loteLeido.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Chequera, [chequeraLeida.Id], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [loteLeido.Id], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, pendientes.Select(i => (long)i.PaymentOrderId).Order().ToList(), ct: ct);
        var chequera = await db.Checkbooks.FirstAsync(k => k.Id == chequeraLeida.Id, ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == loteLeido.Id, ct);
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == lote.BankAccountId, ct);
        if (chequera.ToNumber - chequera.NextCheckNumber + 1 < pendientes.Count) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(ErroresDeTesoreria.ChequeraAgotada());

        var emitidos = new List<IssuedCheck>();
        foreach (var item in pendientes)
        {
            var orden = await db.PaymentOrders.FirstAsync(o => o.Id == item.PaymentOrderId, ct);
            var numero = ChequerasDeTesoreria.Siguiente(chequera);
            if (numero.IsFailure) return Result.Failure<IReadOnlyList<IssuedCheckDto>>(numero.Error);
            var cheque = new IssuedCheck
            {
                CheckbookId = chequera.Id, CheckNumber = numero.Value, PaymentOrderId = orden.Id, PaymentBatchItemId = item.Id, BeneficiaryPersonId = orden.BeneficiaryPersonId,
                Amount = orden.NetAmount, IssuedOn = hoy, Crossed = request.Crossed, FirstBeneficiaryOnly = request.FirstBeneficiaryOnly,
            };
            db.IssuedChecks.Add(cheque);
            if (orden.Status is PaymentOrderState.Approved or PaymentOrderState.InBatch) orden.Status = PaymentOrderState.Sent;
            emitidos.Add(cheque);
            await db.SaveChangesAsync(ct);
            db.IssuedCheckEvents.Add(new IssuedCheckEvent
            {
                IssuedCheckId = cheque.Id, FromStatus = IssuedCheckState.Issued, ToStatus = IssuedCheckState.Issued, ByUserId = usuario, At = ahora,
            });
            await emision.ChequeGiradoEmitidoAsync(cuenta, orden, cheque, ct);
        }
        if (lote.Status == PaymentBatchState.Approved)
        {
            lote.Status = PaymentBatchState.Sent;
            lote.SentAt = ahora;
            lote.SentByUserId = usuario;
        }
        await auditoria.AgregarAsync("Treasury.Checks.Printd", "PaymentBatch", lote.PublicId, null,
            new { batch = lote.Number, checks = emitidos.Select(c => c.CheckNumber).ToList(), total = emitidos.Sum(c => c.Amount), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        var lista = new List<IssuedCheckDto>(emitidos.Count);
        foreach (var c in emitidos) lista.Add(await ChequerasDeTesoreria.DtoAsync(db, c, hoy, ct));
        return Result.Success<IReadOnlyList<IssuedCheckDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/treasury/issued-checks/{id}/deliver</c> (<c>Treasury.Checks.Deliver</c>, con clave; FR-113): entrega el cheque con la constancia firmada
/// (<c>TreasuryDeliveryReceipt</c>, obligatoria) y registra el resultado «girada» de su orden por el registro único de resultados. (nuevo)
/// </summary>
public sealed record DeliverIssuedCheckCommand(Guid IssuedCheckPublicId, Guid DeliveryReceiptAttachmentPublicId, DateOnly? DeliveredOn = null)
    : IRequest<Result<IssuedCheckDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class DeliverIssuedCheckCommandValidator : AbstractValidator<DeliverIssuedCheckCommand>
{
    public DeliverIssuedCheckCommandValidator()
    {
        RuleFor(x => x.IssuedCheckPublicId).NotEmpty();
        RuleFor(x => x.DeliveryReceiptAttachmentPublicId).NotEmpty().WithMessage("Adjunte la constancia de entrega firmada.");
    }
}

public sealed class DeliverIssuedCheckCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    RegistroDeResultadosDelGiro registro,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<DeliverIssuedCheckCommand, Result<IssuedCheckDto>>
{
    public async Task<Result<IssuedCheckDto>> Handle(DeliverIssuedCheckCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leido = await ChequerasDeTesoreria.ChequeVisibleAsync(db, alcance, request.IssuedCheckPublicId, seguir: false, ct);
        if (leido is null) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El cheque"));
        var hoy = reloj.HoyLocal;
        if (leido.Status == IssuedCheckState.Delivered) return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, leido, hoy, ct));
        if (leido.Status != IssuedCheckState.Issued) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.ChequeNoEntregable(leido.Status.ToString()));
        if (leido.PaymentOrderId is not { } ordenId) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.ChequeNoEntregable(leido.Status.ToString()));
        var ordenLeida = await db.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == ordenId, ct);
        await registro.TomarCerrojosDeRecaudoAsync([ordenLeida], ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [ordenId], ct: ct);
        var cheque = await db.IssuedChecks.FirstAsync(c => c.Id == leido.Id, ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == ordenId, ct);
        var fecha = request.DeliveredOn ?? hoy;
        var r = await registro.RegistrarAsync(orden, new ResultadoARegistrar(DisbursementOutcome.Disbursed, fecha, orden.Number, cheque.CheckNumber.ToString(),
            null, PaymentResponseSource.Manual, request.DeliveryReceiptAttachmentPublicId), new QuienRegistra(usuario, actor.CentralUserId, actor.Name), ct);
        if (r.IsFailure) return Result.Failure<IssuedCheckDto>(r.Error);
        cheque.DeliveryReceiptAttachmentPublicId = request.DeliveryReceiptAttachmentPublicId;
        cheque.DeliveredOn = fecha;
        ChequerasDeTesoreria.Transicion(db, cheque, IssuedCheckState.Delivered, usuario, reloj.UtcNow, request.DeliveryReceiptAttachmentPublicId);
        await auditoria.AgregarAsync("Treasury.Check.Delivered", "IssuedCheck", cheque.PublicId, null, new { number = cheque.CheckNumber, order = orden.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, cheque, hoy, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/issued-checks/{id}/void</c> (<c>Treasury.Checks.Void</c>, con clave; FR-135, FR-136): anula un cheque girado con motivo. Antes de entregarlo: queda
/// <c>Voided</c>, emite <c>ChequeGiradoAnulado</c> (<c>SpoiledBeforeDelivery</c>) y su orden puede recibir otro cheque del lote, sin resultado. Entregado y no cobrado a los
/// <c>Cheques.DiasSinCobrar</c> días: queda <c>NotCashed</c>, emite el anulado (<c>NotCashed</c>) e informa «cheque no cobrado» al origen. (nuevo)
/// </summary>
public sealed record VoidIssuedCheckCommand(Guid IssuedCheckPublicId, string Reason) : IRequest<Result<IssuedCheckDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidIssuedCheckCommandValidator : AbstractValidator<VoidIssuedCheckCommand>
{
    public VoidIssuedCheckCommandValidator()
    {
        RuleFor(x => x.IssuedCheckPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class VoidIssuedCheckCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    RegistroDeResultadosDelGiro registro,
    EmisionDeTesoreria emision,
    ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<VoidIssuedCheckCommand, Result<IssuedCheckDto>>
{
    public const string CausaAntesDeEntregar = "SpoiledBeforeDelivery";
    public const string CausaNoCobrado = "NotCashed";

    public async Task<Result<IssuedCheckDto>> Handle(VoidIssuedCheckCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leido = await ChequerasDeTesoreria.ChequeVisibleAsync(db, alcance, request.IssuedCheckPublicId, seguir: false, ct);
        if (leido is null) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El cheque"));
        var hoy = reloj.HoyLocal;
        if (leido.Status is IssuedCheckState.Voided or IssuedCheckState.NotCashed) return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, leido, hoy, ct));
        if (leido.Status is not (IssuedCheckState.Issued or IssuedCheckState.Delivered) || leido.PaymentOrderId is not { } ordenId)
            return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.ChequeNoAnulable(leido.Status.ToString()));
        if (leido.Status == IssuedCheckState.Delivered)
        {
            var dias = await parametros.EnteroAsync(Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.ChequesDiasSinCobrar, hoy, ct) ?? 30;
            if (leido.DeliveredOn is { } d && hoy.DayNumber - d.DayNumber < dias) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.ChequeTodaviaCobrable(dias));
        }
        var ordenLeida = await db.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == ordenId, ct);
        // Validación previa contable (FR-221): ChequeGiradoAnulado tiene que tener su regla antes de anular.
        var cuentaPrevia = await (from k in db.Checkbooks.AsNoTracking()
                                  join a in db.OwnBankAccounts.AsNoTracking() on k.BankAccountId equals a.Id
                                  where k.Id == leido.CheckbookId
                                  select a).FirstAsync(ct);
        var previa = await emision.ValidacionPreviaAsync([ChequeGiradoAnuladoV1.Type], cuentaPrevia, hoy, ct);
        if (previa.IsFailure) return Result.Failure<IssuedCheckDto>(previa.Error);
        await registro.TomarCerrojosDeRecaudoAsync([ordenLeida], ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [ordenId], ct: ct);
        var cheque = await db.IssuedChecks.FirstAsync(c => c.Id == leido.Id, ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == ordenId, ct);
        var cuenta = await (from k in db.Checkbooks.AsNoTracking()
                            join a in db.OwnBankAccounts.AsNoTracking() on k.BankAccountId equals a.Id
                            where k.Id == cheque.CheckbookId
                            select a).FirstAsync(ct);
        var ahora = reloj.UtcNow;
        string causa;
        if (cheque.Status == IssuedCheckState.Issued)
        {
            causa = CausaAntesDeEntregar;
            ChequerasDeTesoreria.Transicion(db, cheque, IssuedCheckState.Voided, usuario, ahora, nota: request.Reason);
        }
        else
        {
            causa = CausaNoCobrado;
            var r = await registro.RegistrarAsync(orden, new ResultadoARegistrar(DisbursementOutcome.CheckNotCashed, hoy, orden.Number, cheque.CheckNumber.ToString(),
                request.Reason, PaymentResponseSource.Manual), new QuienRegistra(usuario, actor.CentralUserId, actor.Name), ct);
            if (r.IsFailure) return Result.Failure<IssuedCheckDto>(r.Error);
            ChequerasDeTesoreria.Transicion(db, cheque, IssuedCheckState.NotCashed, usuario, ahora, nota: request.Reason);
        }
        cheque.VoidReason = request.Reason.Trim();
        await emision.ChequeGiradoAnuladoAsync(cuenta, orden, cheque, causa, hoy, ct);
        await auditoria.AgregarAsync("Treasury.Check.Voided", "IssuedCheck", cheque.PublicId, null,
            new { number = cheque.CheckNumber, order = orden.Number, cause = causa, reason = cheque.VoidReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, cheque, hoy, ct));
    }
}

/// <summary><c>POST /api/treasury/issued-checks/{id}/cashed</c> (<c>Treasury.Checks.Deliver</c>, con clave): el banco pagó el cheque entregado (manual en T0; la conciliación lo cruza en T1). (nuevo)</summary>
public sealed record MarkIssuedCheckCashedCommand(Guid IssuedCheckPublicId, DateOnly? CashedOn = null) : IRequest<Result<IssuedCheckDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkIssuedCheckCashedCommandHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IActorActual actorActual, ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria, IDateTimeService reloj, Accounting.EmisionDeTesoreria? emision = null) : IRequestHandler<MarkIssuedCheckCashedCommand, Result<IssuedCheckDto>>
{
    public async Task<Result<IssuedCheckDto>> Handle(MarkIssuedCheckCashedCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leido = await ChequerasDeTesoreria.ChequeVisibleAsync(db, alcance, request.IssuedCheckPublicId, seguir: false, ct);
        if (leido is null) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.NoEncontrado("El cheque"));
        var hoy = reloj.HoyLocal;
        if (leido.Status == IssuedCheckState.Cashed) return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, leido, hoy, ct));
        if (leido.Status != IssuedCheckState.Delivered) return Result.Failure<IssuedCheckDto>(ErroresDeTesoreria.ChequeNoEntregable(leido.Status.ToString()));
        await cerrojo.TomarSetupAsync(ct: ct);
        var cheque = await db.IssuedChecks.FirstAsync(c => c.Id == leido.Id, ct);
        cheque.CashedOn = request.CashedOn ?? hoy;
        ChequerasDeTesoreria.Transicion(db, cheque, IssuedCheckState.Cashed, usuario, reloj.UtcNow);
        // T2 (fase 42; FR-220; pedido del dueño): el cobro tardío de un cheque que el cierre del mes pasó a cuenta por pagar.
        if (cheque.MovedToPayableRunId is not null && emision is not null)
        {
            var chequera = await db.Checkbooks.AsNoTracking().FirstAsync(k => k.Id == cheque.CheckbookId, ct);
            var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == chequera.BankAccountId, ct);
            await emision.ChequeNoCobradoPagadoAsync(cuenta, cheque, cheque.CashedOn!.Value, null, ct);
        }
        await auditoria.AgregarAsync("Treasury.Check.Cashed", "IssuedCheck", cheque.PublicId, null, new { number = cheque.CheckNumber, on = cheque.CashedOn, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequerasDeTesoreria.DtoAsync(db, cheque, hoy, ct));
    }
}

/// <summary><c>GET /api/treasury/checkbooks?account=</c> (<c>Treasury.Checkbooks.View</c>). (nuevo)</summary>
public sealed record ListCheckbooksQuery(Guid? BankAccountPublicId = null) : IRequest<Result<IReadOnlyList<CheckbookDto>>>;

public sealed class ListCheckbooksQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<ListCheckbooksQuery, Result<IReadOnlyList<CheckbookDto>>>
{
    public async Task<Result<IReadOnlyList<CheckbookDto>>> Handle(ListCheckbooksQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.Checkbooks.AsNoTracking();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(k => k.BankAccountId == id);
        }
        var filas = (await consulta.OrderByDescending(k => k.Id).Take(200).ToListAsync(ct)).Where(k => mio.IncluyeCuenta(k.BankAccountId)).ToList();
        var lista = new List<CheckbookDto>(filas.Count);
        foreach (var k in filas) lista.Add(await ChequerasDeTesoreria.DtoAsync(db, k, ct));
        return Result.Success<IReadOnlyList<CheckbookDto>>(lista);
    }
}

/// <summary><c>GET /api/treasury/issued-checks?checkbook=&amp;state=</c> (<c>Treasury.Checks.View</c>): los cheques girados y en blanco dañados, con los días sin cobrar. (nuevo)</summary>
public sealed record ListIssuedChecksQuery(Guid? CheckbookPublicId = null, IssuedCheckState? State = null) : IRequest<Result<IReadOnlyList<IssuedCheckDto>>>;

public sealed class ListIssuedChecksQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj)
    : IRequestHandler<ListIssuedChecksQuery, Result<IReadOnlyList<IssuedCheckDto>>>
{
    public async Task<Result<IReadOnlyList<IssuedCheckDto>>> Handle(ListIssuedChecksQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = from c in db.IssuedChecks.AsNoTracking()
                       join k in db.Checkbooks.AsNoTracking() on c.CheckbookId equals k.Id
                       select new { c, k.BankAccountId, k.PublicId };
        if (request.CheckbookPublicId is { } kp) consulta = consulta.Where(x => x.PublicId == kp);
        if (request.State is { } s) consulta = consulta.Where(x => x.c.Status == s);
        var filas = (await consulta.OrderByDescending(x => x.c.Id).Take(300).ToListAsync(ct)).Where(x => mio.IncluyeCuenta(x.BankAccountId)).ToList();
        var lista = new List<IssuedCheckDto>(filas.Count);
        foreach (var x in filas) lista.Add(await ChequerasDeTesoreria.DtoAsync(db, x.c, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<IssuedCheckDto>>(lista);
    }
}
