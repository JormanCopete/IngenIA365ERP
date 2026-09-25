using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Payments;

public sealed record PaymentRegisterRowDto(
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    string? Email,
    decimal NetPay,
    string? BankName,
    string? BankAccountType,
    string? BankAccountNumber,
    bool Paid,
    Guid? PaymentPublicId,
    DateTime? PaidAt,
    string? PaymentMethod,
    string? Reference,
    string? PaidBy);

public sealed record PaymentRegisterDto(
    Guid RunPublicId,
    string RunStatus,
    int Employees,
    int PaidCount,
    decimal TotalNet,
    decimal PaidNet,
    IReadOnlyList<PaymentRegisterRowDto> Rows);

// ------------------------------------------------------------------ consulta --

/// <summary>FR-025/FR-040: la relación de pago de la corrida: neto, banco y cuenta del empleado, y el estado de pago.</summary>
public sealed record GetPaymentRegisterQuery(Guid RunPublicId) : IRequest<Result<PaymentRegisterDto>>;

public sealed class GetPaymentRegisterQueryValidator : AbstractValidator<GetPaymentRegisterQuery>
{
    public GetPaymentRegisterQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetPaymentRegisterQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentRegisterQuery, Result<PaymentRegisterDto>>
{
    public async Task<Result<PaymentRegisterDto>> Handle(GetPaymentRegisterQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<PaymentRegisterDto>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            orderby p.LastName, p.FirstName
            select new { re.Id, e.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId, p.Email, re.NetPay, e.PayrollBankId, e.PayrollBankAccountType, e.PayrollBankAccountNumber })
            .ToListAsync(ct);

        var ids = filas.Select(f => f.Id).ToList();
        var pagos = await db.PayrollPayments.AsNoTracking()
            .Where(p => ids.Contains(p.PayrollRunEmployeeId) && !p.IsReverted)
            .ToDictionaryAsync(p => p.PayrollRunEmployeeId, ct);

        var codigosBanco = filas.Select(f => f.PayrollBankId).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToList();
        var bancos = codigosBanco.Count == 0
            ? new Dictionary<string, string>()
            : await db.Banks.AsNoTracking().Where(b => b.LegacyCode != null && codigosBanco.Contains(b.LegacyCode))
                .ToDictionaryAsync(b => b.LegacyCode!, b => b.Name, ct);

        var rows = filas.Select(f =>
        {
            pagos.TryGetValue(f.Id, out var pago);
            return new PaymentRegisterRowDto(f.PublicId, NombreDePersona.Completo(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName), f.TaxId, f.Email, f.NetPay,
                f.PayrollBankId is { } bk && bancos.TryGetValue(bk, out var nombreBanco) ? nombreBanco : f.PayrollBankId,
                TipoCuenta(f.PayrollBankAccountType), f.PayrollBankAccountNumber,
                pago is not null, pago?.PublicId, pago?.PaidAt, pago?.PaymentMethod.ToString(), pago?.Reference, pago?.PaidBy);
        }).ToList();

        return Result.Success(new PaymentRegisterDto(run.PublicId, run.Status.ToString(), rows.Count, rows.Count(r => r.Paid),
            rows.Sum(r => r.NetPay), rows.Where(r => r.Paid).Sum(r => r.NetPay), rows));
    }

    private static string? TipoCuenta(int tipo) => tipo switch { 1 => "Ahorros", 2 => "Corriente", 0 => null, _ => tipo.ToString() };
}

// ------------------------------------------------------------- marcar pagados --

/// <summary>FR-040: marca como pagados a todos (<c>EmployeePublicIds = null</c>) o a los indicados. La ejecución bancaria sigue fuera del sistema.</summary>
public sealed record MarkPaymentsCommand(
    Guid RunPublicId,
    IReadOnlyList<Guid>? EmployeePublicIds,
    DateTime PaidAt,
    PayrollPaymentMethod Method,
    string? Reference) : IRequest<Result<int>>;

public sealed class MarkPaymentsCommandValidator : AbstractValidator<MarkPaymentsCommand>
{
    public MarkPaymentsCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.PaidAt).NotEmpty().WithMessage("La fecha de pago es obligatoria.");
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(60);
    }
}

public sealed class MarkPaymentsCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<MarkPaymentsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(MarkPaymentsCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<int>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));
        if (run.Status != PayrollRunStatus.Approved)
            return Result.Failure<int>(new Error("Payroll.RunNotApproved", "Sólo se marca el pago de una liquidación aprobada."));

        var empleados = await (
            from re in db.PayrollRunEmployees
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            where re.PayrollRunId == run.Id
            select new { re, e.PublicId }).ToListAsync(ct);

        var objetivo = request.EmployeePublicIds is null
            ? empleados
            : empleados.Where(x => request.EmployeePublicIds.Contains(x.PublicId)).ToList();
        if (request.EmployeePublicIds is not null && objetivo.Count != request.EmployeePublicIds.Distinct().Count())
            return Result.Failure<int>(new Error("Payroll.RunEmployeeNotFound", "Alguno de los empleados indicados no está en esta corrida."));

        var ids = objetivo.Select(x => x.re.Id).ToList();
        var yaPagados = await db.PayrollPayments.Where(p => ids.Contains(p.PayrollRunEmployeeId) && !p.IsReverted).Select(p => p.PayrollRunEmployeeId).ToListAsync(ct);
        if (request.EmployeePublicIds is not null && yaPagados.Count > 0)
            return Result.Failure<int>(new Error("Payroll.PaymentAlreadyMarked",
                $"{yaPagados.Count} empleado(s) ya tienen marca de pago vigente; retire la marca antes de volver a marcar."));

        var ahora = clock.UtcNow;
        var marcados = 0;
        foreach (var x in objetivo.Where(x => !yaPagados.Contains(x.re.Id)))
        {
            db.PayrollPayments.Add(new PayrollPayment
            {
                PayrollRunEmployeeId = x.re.Id,
                PaidAt = request.PaidAt,
                PaymentMethod = request.Method,
                Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
                PaidBy = user.UserName ?? "sistema",
                CreatedAt = ahora,
                CreatedBy = user.UserName,
            });
            marcados++;
        }
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollPaymentsMarked, "PayrollRun", run.PublicId, null,
            new { marked = marcados, employees = objetivo.Select(x => x.PublicId).ToList(), request.PaidAt, method = request.Method.ToString(), request.Reference }, ct);
        return Result.Success(marcados);
    }
}

// ---------------------------------------------------------------- retirar marca --

public sealed record RevertPaymentMarkCommand(Guid RunPublicId, Guid EmployeePublicId, string Reason) : IRequest<Result>;

public sealed class RevertPaymentMarkCommandValidator : AbstractValidator<RevertPaymentMarkCommand>
{
    public RevertPaymentMarkCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(300);
    }
}

public sealed class RevertPaymentMarkCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<RevertPaymentMarkCommand, Result>
{
    public async Task<Result> Handle(RevertPaymentMarkCommand request, CancellationToken ct)
    {
        // Un AsNoTracking en cualquier fuente del join vuelve toda la consulta sin seguimiento:
        // se resuelve el Id y se carga la marca aparte, con seguimiento, para poder cambiarla.
        var pagoId = await (
            from p in db.PayrollPayments.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on p.PayrollRunEmployeeId equals re.Id
            join r in db.PayrollRuns.AsNoTracking() on re.PayrollRunId equals r.Id
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            where r.PublicId == request.RunPublicId && e.PublicId == request.EmployeePublicId && !p.IsReverted
            select (int?)p.Id).FirstOrDefaultAsync(ct);
        var pago = pagoId is null ? null : await db.PayrollPayments.FirstOrDefaultAsync(p => p.Id == pagoId.Value, ct);
        if (pago is null)
            return Result.Failure(new Error("Payroll.PaymentNotFound", "El empleado no tiene una marca de pago vigente en esta corrida."));

        pago.IsReverted = true;
        pago.RevertedAt = clock.UtcNow;
        pago.RevertedBy = user.UserName;
        pago.RevertReason = request.Reason.Trim();
        pago.UpdatedAt = clock.UtcNow;
        pago.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollPaymentMarkReverted, nameof(PayrollPayment), pago.PublicId,
            new { paidAt = pago.PaidAt, pago.PaidBy }, new { reason = request.Reason.Trim(), employee = request.EmployeePublicId }, ct);
        return Result.Success();
    }
}
