using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Terminations;

/// <summary>
/// Registra la terminación del contrato y crea la liquidación definitiva en borrador en la misma
/// acción (feature 010, US3, FR-018, R7; contracts/api.md §3.4 <c>POST /</c>). La ficha <b>no se
/// toca</b>: se cierra al aprobar (<c>ApproveSettlementCommand</c>) y se reabre al reversar. Los
/// descuentos se proponen desde Cartera y las libranzas (FR-018a) y quedan en
/// <c>PAY_SettlementDeductions</c> enlazados a sus líneas.
/// </summary>
/// <param name="ContractType">Tipo de contrato DIAN al retiro; nulo = el de la ficha (o indefinido).</param>
/// <param name="ContractEndDate">Hasta cuándo iba el contrato; obligatoria en término fijo u obra cuando el motivo la exige (el tiempo faltante, CST art. 64).</param>
public sealed record RegisterTerminationCommand(
    Guid EmployeePublicId,
    DateOnly TerminationDate,
    string ReasonCode,
    DianContractType? ContractType = null,
    DateOnly? ContractEndDate = null,
    string? Notes = null) : IRequest<Result<TerminationRegisteredDto>>;

/// <summary>Errores propios del registro que <c>SettlementErrors</c> no trae.</summary>
public static class TerminationErrors
{
    /// <summary>La ficha ya está retirada por el camino anterior (sin terminación registrada): se reingresa con una ficha nueva, no se liquida de nuevo.</summary>
    public static Error EmployeeAlreadyRetired(DateTime terminationDate) =>
        new ErrorConDatos("Payroll.Termination.EmployeeAlreadyTerminated",
            $"La ficha ya está retirada desde el {terminationDate:dd/MM/yyyy}. Un reingreso es una ficha nueva; una definitiva pendiente de esa época se registra sobre la ficha vigente.",
            new { terminationPublicId = (Guid?)null });
}

public sealed class RegisterTerminationCommandValidator : AbstractValidator<RegisterTerminationCommand>
{
    public RegisterTerminationCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty().WithMessage("El empleado es obligatorio.");
        RuleFor(x => x.TerminationDate).NotEqual(default(DateOnly)).WithMessage("La fecha de retiro es obligatoria.");
        RuleFor(x => x.ReasonCode).NotEmpty().WithMessage("El motivo de retiro es obligatorio.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.ContractType).IsInEnum().When(x => x.ContractType is not null);
        RuleFor(x => x.ContractEndDate).GreaterThanOrEqualTo(x => x.TerminationDate).When(x => x.ContractEndDate is not null)
            .WithMessage("La fecha de fin del contrato no puede ser anterior a la fecha de retiro.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class RegisterTerminationCommandHandler(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<RegisterTerminationCommand, Result<TerminationRegisteredDto>>
{
    public async Task<Result<TerminationRegisteredDto>> Handle(RegisterTerminationCommand request, CancellationToken ct)
    {
        var empleado = await db.Employees.FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId, ct);
        if (empleado is null) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.EmployeeNotFound);

        // --- una sola terminación viva por ficha ---
        var viva = await db.EmploymentTerminations.AsNoTracking()
            .Where(t => t.EmployeeId == empleado.Id && (t.Status == TerminationStatus.Registered || t.Status == TerminationStatus.Settled))
            .OrderByDescending(t => t.Id).FirstOrDefaultAsync(ct);
        if (viva is not null)
        {
            if (viva.Status == TerminationStatus.Registered)
            {
                var borrador = await db.PayrollRuns.AsNoTracking()
                    .Where(r => r.TerminationId == viva.Id && r.Kind == PayrollRunKind.Settlement && (r.Status == PayrollRunStatus.Draft || r.Status == PayrollRunStatus.Stale || r.Status == PayrollRunStatus.Approved))
                    .OrderByDescending(r => r.Version).Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);
                if (borrador is { } runPublicId) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.PendingSettlement(runPublicId));
            }
            return Result.Failure<TerminationRegisteredDto>(SettlementErrors.EmployeeAlreadyTerminated(viva.PublicId));
        }
        if (empleado.Status < 0) return Result.Failure<TerminationRegisteredDto>(TerminationErrors.EmployeeAlreadyRetired(empleado.TerminationDate));

        // --- fechas ---
        var fecha = request.TerminationDate;
        if (fecha < DateOnly.FromDateTime(empleado.JoinDate.Date)) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.TerminationDateBeforeHire);
        if (fecha > clock.TodayUtc) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.TerminationDateInFuture);

        // --- FR-021: la fecha tiene que caer en un período abierto del plan (o en ninguno) ---
        var fechaDt = fecha.ToDateTime(TimeOnly.MinValue);
        var periodo = await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayrollPlanId == empleado.PayrollPlanId && p.StartDate <= fechaDt && p.EndDate >= fechaDt)
            .OrderByDescending(p => p.Status).FirstOrDefaultAsync(ct);
        if (periodo is not null && periodo.Status == PayPeriodStatus.Approved)
        {
            var abierto = await db.PayPeriods.AsNoTracking()
                .Where(p => p.PayrollPlanId == empleado.PayrollPlanId && (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated) && p.EndDate > periodo.EndDate)
                .OrderBy(p => p.StartDate).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct);
            return Result.Failure<TerminationRegisteredDto>(SettlementErrors.TerminationPeriodApproved(periodo.PublicId,
                string.IsNullOrWhiteSpace(periodo.Description) ? $"{periodo.StartDate:dd/MM/yyyy} – {periodo.EndDate:dd/MM/yyyy}" : periodo.Description, abierto));
        }

        // --- motivo del catálogo ---
        var codigo = CodigoDeCatalogo.Normalizar(request.ReasonCode)!;
        var motivo = await db.TerminationReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Code == codigo && r.IsActive, ct);
        if (motivo is null) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.TerminationReasonNotFound);

        var tipoContrato = request.ContractType ?? empleado.DianContractType ?? DianContractType.Indefinite;
        if (motivo.RequiresContractEndDate && tipoContrato is (DianContractType.FixedTerm or DianContractType.WorkOrLabor) && request.ContractEndDate is null)
            return Result.Failure<TerminationRegisteredDto>(SettlementErrors.ContractEndDateRequired);

        var ahora = clock.UtcNow;
        var quien = user.UserName;
        var calculador = new DefinitivaCalculator(db, loader, persister, clock, user);

        var resultado = await TransaccionDeLiquidacion.EjecutarAsync(db, async () =>
        {
            var terminacion = new EmploymentTermination
            {
                EmployeeId = empleado.Id,
                TerminationDate = fecha,
                TerminationReasonId = motivo.Id,
                ContractTypeAtTermination = tipoContrato,
                ContractEndDate = request.ContractEndDate,
                Status = TerminationStatus.Registered,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            db.EmploymentTerminations.Add(terminacion);
            await db.SaveChangesAsync(ct);

            var calculo = await calculador.CalcularAsync(terminacion, empleado, recalculo: false, ct);
            if (calculo.IsFailure) return Result.Failure<(EmploymentTermination, CalculoDeDefinitiva)>(calculo.Error);
            return Result.Success((terminacion, calculo.Value));
        }, ct);
        if (resultado.IsFailure) return Result.Failure<TerminationRegisteredDto>(resultado.Error);

        var (terminacionCreada, calculado) = resultado.Value;
        await audit.EmitAsync(AuditEventTypes.PayrollSettlementCalculated, nameof(EmploymentTermination), terminacionCreada.PublicId, null,
            new
            {
                action = "TerminationRegistered", employeePublicId = empleado.PublicId, terminationDate = fecha, reasonCode = motivo.Code, reasonName = motivo.Name,
                contractType = tipoContrato.ToString(), contractEndDate = request.ContractEndDate, runPublicId = calculado.Run.PublicId, version = calculado.Run.Version,
                net = calculado.Run.TotalNet, deductionsProposed = calculado.Deducciones.Sum(d => d.ProposedAmount),
            }, ct);

        return Result.Success(Respuesta(terminacionCreada, calculado));
    }

    /// <summary>La respuesta de registrar y de recalcular: la corrida, sus líneas resumidas, la propuesta de descuentos y los avisos.</summary>
    public static TerminationRegisteredDto Respuesta(EmploymentTermination terminacion, CalculoDeDefinitiva calculo)
    {
        var run = calculo.Run;
        var fila = run.Employees.FirstOrDefault();
        var lineas = calculo.Resultado.Lines.OrderBy(l => l.Order)
            .Select(l => new SettlementLineDto(l.Code, l.Name, l.Nature, l.Quantity, l.BaseAmount, l.Amount, l.AffectsAccounting, l.Explanation.Summary))
            .ToList();
        var deducciones = SettlementDeductionsReader.Armar(run, fila, calculo.Deducciones.Where(d => !d.IsDeleted).ToList());
        var t = calculo.Resultado.Totals;
        return new TerminationRegisteredDto(
            terminacion.PublicId, run.PublicId, run.Version, terminacion.TerminationDate, lineas, deducciones, calculo.Avisos,
            calculo.Resultado.Skips.Select(s => s.Text).ToList(), calculo.Resultado.Refusals,
            new RunTotalsDto(t.Earnings, t.Deductions, t.EmployerContributions, t.Provisions, t.Net, t.RoundingAdjustment));
    }
}
