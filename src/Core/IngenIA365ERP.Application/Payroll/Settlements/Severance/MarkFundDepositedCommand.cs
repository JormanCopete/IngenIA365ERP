using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

/// <summary>
/// Registra la consignación de las cesantías de un fondo (FR-012; contracts/api.md §3.2
/// <c>POST /{runId}/funds/{fundId}/mark-deposited</c>): fecha, referencia y quién marcó, sobre una
/// liquidación <b>aprobada</b> (<c>Payroll.Severance.NotApproved</c>) y una sola vez por fondo
/// (<c>Payroll.Severance.AlreadyDeposited</c>). Guarda el valor consignado tal como quedó en la
/// relación por fondo, para que la pantalla lo muestre aunque el asiento se reverse después: la
/// consignación ocurrió y queda como historial (data-model §2.7a). Auditoría
/// <c>Payroll.Severance.Deposited</c>.
/// </summary>
public sealed record MarkFundDepositedCommand(Guid RunPublicId, Guid FundPublicId, DateOnly DepositedAt, string? Reference) : IRequest<Result<FundDepositDto>>;

public sealed class MarkFundDepositedCommandValidator : AbstractValidator<MarkFundDepositedCommand>
{
    public MarkFundDepositedCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.FundPublicId).NotEmpty();
        RuleFor(x => x.DepositedAt).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de la consignación.");
        RuleFor(x => x.Reference).MaximumLength(60).WithMessage("La referencia no puede superar 60 caracteres.");
    }
}

public sealed class MarkFundDepositedCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit) : IRequestHandler<MarkFundDepositedCommand, Result<FundDepositDto>>
{
    public async Task<Result<FundDepositDto>> Handle(MarkFundDepositedCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<FundDepositDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Severance) return Result.Failure<FundDepositDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Severance));
        if (run.Status != PayrollRunStatus.Approved) return Result.Failure<FundDepositDto>(SettlementErrors.SeveranceNotApproved);

        var fondo = await db.SeveranceProviders.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.FundPublicId, ct);
        if (fondo is null) return Result.Failure<FundDepositDto>(SeveranceErrors.FundNotFound);

        var hoy = clock.TodayUtc;
        var corte = run.CutoffDate!.Value;
        if (request.DepositedAt < corte || request.DepositedAt > hoy)
            return Result.Failure<FundDepositDto>(SeveranceErrors.DepositDateInvalid(request.DepositedAt, corte, hoy));

        if (await db.SeveranceFundDeposits.AsNoTracking().AnyAsync(d => d.PayrollRunId == run.Id && d.SeveranceFundId == fondo.Id, ct))
            return Result.Failure<FundDepositDto>(SettlementErrors.SeveranceAlreadyDeposited(fondo.PublicId));

        var relacion = await new DepositScheduleBuilder(db).BuildAsync(run, ct);
        var bloque = relacion.Funds.FirstOrDefault(b => b.FundPublicId == fondo.PublicId);
        if (bloque is null) return Result.Failure<FundDepositDto>(SeveranceErrors.FundNotInRun(fondo.Name));

        var ahora = clock.UtcNow;
        var yo = user.UserName ?? string.Empty;
        var referencia = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        var consignacion = new SeveranceFundDeposit
        {
            PayrollRunId = run.Id,
            SeveranceFundId = fondo.Id,
            DepositedAt = request.DepositedAt,
            DepositedBy = yo,
            Reference = referencia,
            Amount = bloque.Total,
            CreatedAt = ahora,
            CreatedBy = yo,
        };
        db.SeveranceFundDeposits.Add(consignacion);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollSeveranceDeposited, nameof(SeveranceFundDeposit), consignacion.PublicId, null, new
        {
            runPublicId = run.PublicId, year = run.Year, fundPublicId = fondo.PublicId, fund = fondo.Name,
            depositedAt = request.DepositedAt, reference = referencia, amount = bloque.Total, employees = bloque.Employees,
        }, ct);

        return Result.Success(new FundDepositDto(run.PublicId, fondo.PublicId, fondo.Name, request.DepositedAt, yo, referencia, bloque.Total));
    }
}
