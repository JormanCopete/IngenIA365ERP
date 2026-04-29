using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayrollProcessing.Commands.RegisterPayrollEntry;

public record RegisterPayrollEntryCommand : IRequest<Result<Guid>>
{
    public Guid EmployeePublicId { get; init; }
    public Guid ConceptPublicId { get; init; }
    public decimal Amount { get; init; }
    public Guid PayPeriodPublicId { get; init; }
    /// <summary>F=fixed, V=variable, U=oneTime</summary>
    public string EntryType { get; init; } = "V";
}

public class RegisterPayrollEntryCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RegisterPayrollEntryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterPayrollEntryCommand request, CancellationToken ct)
    {
        // 1. Resolve employee
        var employee = await context.Employees.FirstOrDefaultAsync(
            e => e.PublicId == request.EmployeePublicId && !e.IsDeleted && e.Status == 1, ct);
        if (employee is null)
            return Result.Failure<Guid>(new Error("PayEntry.EmployeeNotFound",
                "Empleado no encontrado o inactivo."));

        // 2. Resolve concept
        var concept = await context.PayrollConcepts.AsNoTracking().FirstOrDefaultAsync(
            c => c.PublicId == request.ConceptPublicId && !c.IsDeleted, ct);
        if (concept is null)
            return Result.Failure<Guid>(new Error("PayEntry.ConceptNotFound",
                "Concepto de nomina no encontrado."));

        // 3. Resolve pay period
        var period = await context.PayPeriods.FirstOrDefaultAsync(
            p => p.PublicId == request.PayPeriodPublicId && !p.IsDeleted, ct);
        if (period is null)
            return Result.Failure<Guid>(new Error("PayEntry.PeriodNotFound",
                "Periodo de pago no encontrado."));

        // 4. Validate period is open (Status = 0 means open)
        if (period.Status != 0)
            return Result.Failure<Guid>(new Error("PayEntry.PeriodNotOpen",
                "El periodo de pago no esta abierto."));

        // 5. Map entry type
        decimal entryTypeCode = request.EntryType switch
        {
            "F" => 1, // Fixed
            "V" => 2, // Variable
            "U" => 3, // One-time
            _ => 2
        };

        // 6. Create PayrollEntry
        var entry = new PayrollEntry
        {
            Cycle = period.PeriodId,
            PayrollCompanyId = employee.PayrollCompanyId,
            EmployeeId = employee.Id,
            EntityCode = concept.ConceptCode,
            EntryType = entryTypeCode,
            StartDate = period.StartDate,
            IncapacityAmount = request.Amount,
            Days = 0,
            UserName = currentUser.UserName ?? "",
            ProcessDate = dateTime.UtcNow,
            NewEntity = "",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.PayrollEntries.Add(entry);

        await context.SaveChangesAsync(ct);
        return Result.Success(entry.PublicId);
    }
}

public class RegisterPayrollEntryCommandValidator : AbstractValidator<RegisterPayrollEntryCommand>
{
    private static readonly string[] ValidTypes = ["F", "V", "U"];

    public RegisterPayrollEntryCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId)
            .NotEmpty().WithMessage("Empleado requerido.");

        RuleFor(x => x.ConceptPublicId)
            .NotEmpty().WithMessage("Concepto requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Valor debe ser mayor a 0.");

        RuleFor(x => x.PayPeriodPublicId)
            .NotEmpty().WithMessage("Periodo de pago requerido.");

        RuleFor(x => x.EntryType)
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Tipo de novedad invalido. Use F=fijo, V=variable, U=unico.");
    }
}
