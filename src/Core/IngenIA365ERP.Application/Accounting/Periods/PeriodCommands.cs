using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Accounting.Setup;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Periods;

// Ejercicios y períodos mensuales (feature 009, FR-020..FR-023; contracts/api.md §5). El cierre
// y la reapertura del ejercicio (comprobante CI) llegan con US5 en E2.

public sealed record PeriodoContableDto(Guid PublicId, int Year, byte Month, DateOnly StartDate, DateOnly EndDate, string Status,
    DateTime? ClosedAt, string? ClosedBy, DateTime? ReopenedAt, string? ReopenedBy, string? ReopenReason, int Drafts);

public sealed record EjercicioDto(Guid PublicId, int Year, string Status, DateTime? ClosedAt, string? ClosedBy, DateTime? ReopenedAt, string? ReopenedBy,
    string? ReopenReason, Guid? ClosingDocumentPublicId, IReadOnlyList<PeriodoContableDto> Periods);

// ------------------------------------------------------------------------------ consultas --

public sealed record ListFiscalYearsQuery : IRequest<Result<IReadOnlyList<int>>>;

public sealed class ListFiscalYearsQueryValidator : AbstractValidator<ListFiscalYearsQuery>;

public sealed class ListFiscalYearsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListFiscalYearsQuery, Result<IReadOnlyList<int>>>
{
    public async Task<Result<IReadOnlyList<int>>> Handle(ListFiscalYearsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<int>>(await db.FiscalYears.AsNoTracking().Where(f => !f.IsDeleted).OrderByDescending(f => f.Year).Select(f => f.Year).ToListAsync(ct));
}

/// <summary>El ejercicio con sus doce períodos y cuántos borradores tiene cada uno (lo que impide cerrarlo).</summary>
public sealed record ListPeriodsQuery(int? Year = null) : IRequest<Result<EjercicioDto>>;

public sealed class ListPeriodsQueryValidator : AbstractValidator<ListPeriodsQuery>
{
    public ListPeriodsQueryValidator() => RuleFor(x => x.Year).Must(y => y is null or >= 2000 and <= 2100);
}

public sealed class ListPeriodsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListPeriodsQuery, Result<EjercicioDto>>
{
    public async Task<Result<EjercicioDto>> Handle(ListPeriodsQuery request, CancellationToken ct)
    {
        var ejercicios = db.FiscalYears.AsNoTracking().Include(f => f.Periods).Include(f => f.ClosingDocument).Where(f => !f.IsDeleted);
        var ejercicio = request.Year is { } y
            ? await ejercicios.FirstOrDefaultAsync(f => f.Year == y, ct)
            : await ejercicios.OrderByDescending(f => f.Year).FirstOrDefaultAsync(ct);
        if (ejercicio is null) return Result.Failure<EjercicioDto>(AccountingErrors.FiscalYearNotFound);

        var borradores = await db.AccountingDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && d.Status == DocumentStatus.Draft && d.Date >= ejercicio.StartDate && d.Date <= ejercicio.EndDate)
            .GroupBy(d => d.Date.Month).Select(g => new { Mes = g.Key, Cuantos = g.Count() })
            .ToDictionaryAsync(g => g.Mes, g => g.Cuantos, ct);

        var periodos = ejercicio.Periods.Where(p => !p.IsDeleted).OrderBy(p => p.Month)
            .Select(p => new PeriodoContableDto(p.PublicId, ejercicio.Year, p.Month, p.StartDate, p.EndDate, p.Status.ToString(),
                p.ClosedAt, p.ClosedBy, p.ReopenedAt, p.ReopenedBy, p.ReopenReason, borradores.GetValueOrDefault(p.Month)))
            .ToList();
        return Result.Success(new EjercicioDto(ejercicio.PublicId, ejercicio.Year, ejercicio.Status.ToString(), ejercicio.ClosedAt, ejercicio.ClosedBy,
            ejercicio.ReopenedAt, ejercicio.ReopenedBy, ejercicio.ReopenReason, ejercicio.ClosingDocument?.PublicId, periodos));
    }
}

// --------------------------------------------------------------------------- abrir ejercicio --

/// <summary>Abre el ejercicio siguiente con sus doce períodos; el anterior tiene que existir (o ser el primero) y no puede repetirse.</summary>
public sealed record OpenFiscalYearCommand(int Year) : IRequest<Result<EjercicioDto>>;

public sealed class OpenFiscalYearCommandValidator : AbstractValidator<OpenFiscalYearCommand>
{
    public OpenFiscalYearCommandValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class OpenFiscalYearCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit, ISender sender)
    : IRequestHandler<OpenFiscalYearCommand, Result<EjercicioDto>>
{
    public async Task<Result<EjercicioDto>> Handle(OpenFiscalYearCommand request, CancellationToken ct)
    {
        if (!await db.AccountingSetups.AnyAsync(s => !s.IsDeleted, ct)) return Result.Failure<EjercicioDto>(AccountingErrors.NotInitialized);
        if (await db.FiscalYears.AnyAsync(f => f.Year == request.Year && !f.IsDeleted, ct)) return Result.Failure<EjercicioDto>(AccountingErrors.FiscalYearAlreadyExists);
        var anterior = await db.FiscalYears.AsNoTracking().Where(f => !f.IsDeleted && f.Year < request.Year).OrderByDescending(f => f.Year).FirstOrDefaultAsync(ct);
        if (anterior is not null && anterior.Year != request.Year - 1) return Result.Failure<EjercicioDto>(AccountingErrors.FiscalYearNotFound);

        var ejercicio = CopiaDelCatalogo.Ejercicio(request.Year, user.UserName ?? "system", clock.UtcNow);
        db.FiscalYears.Add(ejercicio);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.FiscalYear.Opened", nameof(FiscalYear), ejercicio.PublicId, null, new { ejercicio.Year, periods = 12 }, ct);
        return await sender.Send(new ListPeriodsQuery(request.Year), ct);
    }
}

// ---------------------------------------------------------------------------- cerrar período --

/// <summary>FR-021: cerrar exige que no queden borradores fechados en el mes; la respuesta los lista.</summary>
public sealed record ClosePeriodCommand(int Year, int Month) : IRequest<Result>;

public sealed class ClosePeriodCommandValidator : AbstractValidator<ClosePeriodCommand>
{
    public ClosePeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class ClosePeriodCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ClosePeriodCommand, Result>
{
    public async Task<Result> Handle(ClosePeriodCommand request, CancellationToken ct)
    {
        var periodo = await db.AccountingPeriods.Include(p => p.FiscalYear).FirstOrDefaultAsync(p => !p.IsDeleted && p.FiscalYear!.Year == request.Year && p.Month == request.Month, ct);
        if (periodo is null) return Result.Failure(AccountingErrors.PeriodNotFound(new DateOnly(request.Year, request.Month, 1)));
        if (periodo.Status == PeriodStatus.Closed) return Result.Failure(AccountingErrors.PeriodAlreadyClosed);

        var borradores = await db.AccountingDocuments.AsNoTracking().Include(d => d.VoucherType)
            .Where(d => !d.IsDeleted && d.Status == DocumentStatus.Draft && d.Date >= periodo.StartDate && d.Date <= periodo.EndDate)
            .OrderBy(d => d.Date)
            .Select(d => new { d.PublicId, voucherType = d.VoucherType!.Code, d.Date, d.Description, d.RegisteredBy })
            .ToListAsync(ct);
        if (borradores.Count > 0)
            return Result.Failure(new ErrorConDatos(AccountingErrors.PeriodHasDrafts.Code,
                $"{AccountingErrors.PeriodHasDrafts.Message} Hay {borradores.Count}: {string.Join("; ", borradores.Take(3).Select(b => $"{b.voucherType} {b.Date:yyyy-MM-dd} «{b.Description}» de {b.RegisteredBy}"))}{(borradores.Count > 3 ? "…" : string.Empty)}",
                new { drafts = borradores }));

        periodo.Status = PeriodStatus.Closed;
        periodo.ClosedAt = clock.UtcNow;
        periodo.ClosedBy = user.UserName ?? "system";
        periodo.UpdatedAt = periodo.ClosedAt;
        periodo.UpdatedBy = periodo.ClosedBy;
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Period.Closed", nameof(AccountingPeriod), periodo.PublicId, new { status = "Open" }, new { status = "Closed", request.Year, request.Month }, ct);
        return Result.Success();
    }
}

// --------------------------------------------------------------------------- reabrir período --

/// <summary>FR-022: reabrir exige motivo, queda auditado y deja desactualizadas las conciliaciones cerradas del mes.</summary>
public sealed record ReopenPeriodCommand(int Year, int Month, string Reason) : IRequest<Result>;

public sealed class ReopenPeriodCommandValidator : AbstractValidator<ReopenPeriodCommand>
{
    public ReopenPeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la reapertura.").MaximumLength(200);
    }
}

public sealed class ReopenPeriodCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ReopenPeriodCommand, Result>
{
    public async Task<Result> Handle(ReopenPeriodCommand request, CancellationToken ct)
    {
        var periodo = await db.AccountingPeriods.Include(p => p.FiscalYear).FirstOrDefaultAsync(p => !p.IsDeleted && p.FiscalYear!.Year == request.Year && p.Month == request.Month, ct);
        if (periodo is null) return Result.Failure(AccountingErrors.PeriodNotFound(new DateOnly(request.Year, request.Month, 1)));
        if (periodo.Status == PeriodStatus.Open) return Result.Failure(AccountingErrors.PeriodNotClosed);
        if (periodo.FiscalYear!.Status == PeriodStatus.Closed) return Result.Failure(AccountingErrors.FiscalYearAlreadyClosed);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        periodo.Status = PeriodStatus.Open;
        periodo.ReopenedAt = ahora;
        periodo.ReopenedBy = quien;
        periodo.ReopenReason = request.Reason.Trim();
        periodo.UpdatedAt = ahora;
        periodo.UpdatedBy = quien;

        var desactualizadas = 0;
        foreach (var c in await db.BankReconciliations.Where(r => !r.IsDeleted && r.PeriodId == periodo.Id && r.Status == ReconciliationStatus.Closed).ToListAsync(ct))
        {
            c.Status = ReconciliationStatus.Outdated;
            c.UpdatedAt = ahora;
            c.UpdatedBy = quien;
            desactualizadas++;
        }
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Period.Reopened", nameof(AccountingPeriod), periodo.PublicId, new { status = "Closed" },
            new { status = "Open", request.Year, request.Month, reason = periodo.ReopenReason, outdatedReconciliations = desactualizadas }, ct);
        return Result.Success();
    }
}
