using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Reversals;

// Feature 015, US5 (T0176; FR-059, FR-063, FR-195; contracts/api.md §4): las aplicaciones que el módulo reversó y la confirmación de la causa del dinero.

/// <summary>Un reverso del módulo para la pantalla. (nuevo)</summary>
public sealed record ModuleReversalDto(
    Guid PublicId,
    Guid ReceiptPublicId,
    string ReceiptNumber,
    string? ProductNumber,
    string Module,
    string MessageType,
    string ModuleReversalNumber,
    decimal Amount,
    string Reason,
    ModuleReversalCause Cause,
    string? ConfirmedByName,
    DateTime? ConfirmedAt,
    string? ConfirmationNote,
    Guid? PayerCreditBalancePublicId,
    DateTime CreatedAt);

/// <summary><c>GET /api/receipts/module-reversals?state=</c> (<c>Receipts.Reversals.View</c>): los reversos de los recibos al alcance, por causa. (nuevo)</summary>
public sealed record ListModuleReversalsQuery(ModuleReversalCause? State = null) : IRequest<Result<IReadOnlyList<ModuleReversalDto>>>;

public sealed class ListModuleReversalsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<ListModuleReversalsQuery, Result<IReadOnlyList<ModuleReversalDto>>>
{
    public async Task<Result<IReadOnlyList<ModuleReversalDto>>> Handle(ListModuleReversalsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = from m in db.ModuleReversals.AsNoTracking()
                       join a in db.ReceiptApplications.AsNoTracking() on m.ReceiptApplicationId equals a.Id
                       join r in db.IssuedReceipts.AsNoTracking() on a.ReceiptId equals r.Id
                       join u in db.Users.AsNoTracking().IgnoreQueryFilters() on m.ConfirmedByUserId equals u.Id into us
                       from u in us.DefaultIfEmpty()
                       join s in db.PayerCreditBalances.AsNoTracking() on m.PayerCreditBalanceId equals s.Id into ss
                       from s in ss.DefaultIfEmpty()
                       select new { m, a.ProductNumber, r.PublicId, r.Number, r.BranchId, Confirmo = u != null ? u.Username : null, Saldo = s != null ? (Guid?)s.PublicId : null };
        if (request.State is { } e) consulta = consulta.Where(x => x.m.Cause == e);
        var filas = (await consulta.OrderByDescending(x => x.m.Id).Take(200).ToListAsync(ct)).Where(x => mio.IncluyeOficina(x.BranchId)).ToList();
        return Result.Success<IReadOnlyList<ModuleReversalDto>>(filas.Select(x => new ModuleReversalDto(x.m.PublicId, x.PublicId, x.Number, x.ProductNumber, x.m.Module,
            x.m.MessageType, x.m.ModuleReversalNumber, x.m.Amount, x.m.Reason, x.m.Cause, x.Confirmo, x.m.ConfirmedAt, x.m.ConfirmationNote, x.Saldo, x.m.CreatedAt)).ToList());
    }
}

/// <summary>
/// <c>POST /api/receipts/module-reversals/{id}/confirm-cause</c> (<c>Receipts.Reversals.Resolve</c>): una persona confirma la causa del dinero de un reverso del módulo
/// (FR-195): <c>MoneyReceived</c> → nace un saldo a favor del pagador por el valor reversado; <c>MoneyNotReceived</c> → el medio se da de baja. Emite
/// <c>AplicacionReversadaPorModulo</c> con la causa. Una sola vez. (nuevo)
/// </summary>
public sealed record ConfirmModuleReversalCauseCommand(Guid ReversalPublicId, ModuleReversalCause Cause, string Note) : IRequest<Result<ModuleReversalDto>>,
    IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmModuleReversalCauseCommandValidator : AbstractValidator<ConfirmModuleReversalCauseCommand>
{
    public ConfirmModuleReversalCauseCommandValidator()
    {
        RuleFor(x => x.ReversalPublicId).NotEmpty();
        RuleFor(x => x.Cause).Must(c => c is ModuleReversalCause.MoneyReceived or ModuleReversalCause.MoneyNotReceived).WithMessage(ErroresDeRecaudo.CausaInvalida().Message);
        RuleFor(x => x.Note).NotEmpty().WithMessage("Escriba cómo confirmó el dinero.").MaximumLength(500);
    }
}

public sealed class ConfirmModuleReversalCauseCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ConfirmModuleReversalCauseCommand, Result<ModuleReversalDto>>
{
    public async Task<Result<ModuleReversalDto>> Handle(ConfirmModuleReversalCauseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ModuleReversalDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.ModuleReversals.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.ReversalPublicId, ct);
        if (leido is null) return Result.Failure<ModuleReversalDto>(ErroresDeRecaudo.NoEncontrado("El reverso"));
        var aplicacion = await db.ReceiptApplications.AsNoTracking().FirstAsync(a => a.Id == leido.ReceiptApplicationId, ct);
        var recibo = await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.Id == aplicacion.ReceiptId, ct);
        if (!(await alcance.ObtenerAsync(ct)).IncluyeOficina(recibo.BranchId)) return Result.Failure<ModuleReversalDto>(ErroresDeRecaudo.NoEncontrado("El reverso"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([recibo.PayerPersonId], ct);
        var reverso = await db.ModuleReversals.FirstAsync(m => m.Id == leido.Id, ct);
        if (reverso.Cause != ModuleReversalCause.PendingConfirmation) return Result.Failure<ModuleReversalDto>(ErroresDeRecaudo.CausaYaConfirmada());

        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        reverso.Cause = request.Cause;
        reverso.ConfirmedByUserId = usuario;
        reverso.ConfirmedAt = ahora;
        reverso.ConfirmationNote = request.Note.Trim();
        var porcion = reverso.PortionId is { } pid ? await db.ReceiptApplicationPortions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct) : null;
        var medio = porcion is null ? null : await db.ReceiptMeans.FirstOrDefaultAsync(m => m.Id == porcion.ReceiptMeansId, ct);
        if (request.Cause == ModuleReversalCause.MoneyReceived)
        {
            var saldo = SaldosAFavor.Reconocer(db, recibo.PayerPersonId, PayerCreditBalanceOrigin.ModuleReversal, reverso.PublicId, reverso.ModuleReversalNumber,
                recibo.BranchId, reverso.Amount, hoy, usuario, ahora, recibo.OwnBankAccountPublicId);
            await db.SaveChangesAsync(ct);
            SaldosAFavor.Reconocido(db, saldo, usuario, ahora);
            reverso.PayerCreditBalanceId = saldo.Id;
        }
        else if (medio is not null)
            medio.Status = ReceiptMeansState.WrittenOff;
        await db.SaveChangesAsync(ct);
        await emision.AplicacionReversadaPorModuloAsync(reverso, recibo, aplicacion, medio, hoy, ct);
        await auditoria.AgregarAsync("Receipts.ModuleReversal.CauseConfirmed", "ModuleReversal", reverso.PublicId, null,
            new { receipt = recibo.Number, cause = request.Cause.ToString(), amount = reverso.Amount, note = reverso.ConfirmationNote, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        var dto = (await new ListModuleReversalsQueryHandler(db, alcance).Handle(new ListModuleReversalsQuery(), ct)).Value.First(x => x.PublicId == reverso.PublicId);
        return Result.Success(dto);
    }
}
