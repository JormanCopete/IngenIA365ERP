using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;

// ------------------------------------------------------------------------ DTO --

public sealed record RecurringNoveltyDto(
    Guid PublicId, Guid EmployeePublicId, string EmployeeName, string Document, string ConceptCode, string ConceptName, string Nature,
    decimal? Quantity, decimal? Amount, DateTime StartDate, DateTime? EndDate, int? TotalInstallments, int InstallmentsIssued,
    bool IsActive, string? Notes, string? DeactivationReason, DateTime CreatedAt, string? CreatedBy,
    string ApplyOn = "EveryPeriod");

// ---------------------------------------------------------------------- crear --

/// <summary>FR-007: una novedad que se repite cada período (cuotas de un descuento, un auxilio fijo) se registra una vez.</summary>
public sealed record CreateRecurringNoveltyCommand(
    Guid EmployeePublicId,
    string ConceptCode,
    decimal? Quantity,
    decimal? Amount,
    DateTime StartDate,
    DateTime? EndDate,
    int? TotalInstallments,
    string? Notes,
    /// <summary>Feature 006: en qué períodos del mes se genera. Nulo = cada período.</summary>
    RecurringApplyRule? ApplyOn = null) : IRequest<Result<Guid>>;

public sealed class CreateRecurringNoveltyCommandValidator : AbstractValidator<CreateRecurringNoveltyCommand>
{
    public CreateRecurringNoveltyCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty().WithMessage("El empleado es obligatorio.");
        RuleFor(x => x.ConceptCode).NotEmpty().WithMessage("El concepto es obligatorio.").MaximumLength(30);
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("La fecha desde es obligatoria.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate is not null)
            .WithMessage("La fecha hasta debe ser igual o posterior a la fecha desde.");
        RuleFor(x => x.TotalInstallments).GreaterThan(0).When(x => x.TotalInstallments is not null)
            .WithMessage("El número de cuotas debe ser mayor que cero.");
        RuleFor(x => x).Must(x => x.Quantity is not null || x.Amount is not null)
            .WithMessage("La novedad recurrente necesita cantidad o valor.");
        RuleFor(x => x.Quantity).GreaterThan(0).When(x => x.Quantity is not null).WithMessage("La cantidad debe ser mayor que cero.");
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount is not null).WithMessage("El valor debe ser mayor que cero.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class CreateRecurringNoveltyCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker staleMarker)
    : IRequestHandler<CreateRecurringNoveltyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateRecurringNoveltyCommand request, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId, ct);
        if (employee is null) return Result.Failure<Guid>(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));
        if (employee.TerminationDate < request.StartDate.Date)
            return Result.Failure<Guid>(new Error("Payroll.EmployeeNotActiveInPeriod", "El empleado ya está retirado en la fecha desde."));

        var conceptoRes = await NoveltyRules.ResolveConceptAsync(db, request.ConceptCode, request.StartDate.Date, employee.EmployeeClass, ct);
        if (conceptoRes.IsFailure) return Result.Failure<Guid>(conceptoRes.Error);
        var concept = conceptoRes.Value;
        if (concept.RequiresDates)
            return Result.Failure<Guid>(new Error("Payroll.ConceptNotApplicable",
                $"El concepto {concept.Name} se registra por fechas (incapacidad, licencia, vacaciones) y no puede ser recurrente."));
        if (concept.RequiresQuantity && request.Quantity is null)
            return Result.Failure<Guid>(new Error("Payroll.ConceptRequiresQuantity", $"El concepto {concept.Name} exige cantidad."));
        if (concept.RequiresAmount && request.Amount is null)
            return Result.Failure<Guid>(new Error("Payroll.ConceptRequiresAmount", $"El concepto {concept.Name} exige valor."));
        if (concept.MaxQuantity is { } mq && request.Quantity > mq)
            return Result.Failure<Guid>(new Error("Payroll.NoveltyOverMax", $"La cantidad supera el máximo del concepto ({mq:0.##})."));
        if (concept.MaxAmount is { } ma && request.Amount > ma)
            return Result.Failure<Guid>(new Error("Payroll.NoveltyOverMax", $"El valor supera el máximo del concepto ({ma:N0})."));

        var ahora = clock.UtcNow;
        var recurrente = new PayrollRecurringNovelty
        {
            EmployeeId = employee.Id,
            ConceptCode = concept.Code,
            Quantity = request.Quantity,
            Amount = request.Amount,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate?.Date,
            TotalInstallments = request.TotalInstallments,
            InstallmentsIssued = 0,
            IsActive = true,
            ApplyOn = request.ApplyOn ?? RecurringApplyRule.EveryPeriod,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = ahora,
            CreatedBy = user.UserName,
        };

        // La misma orden dos veces no se registra (RecurrenteRepetida): en producción pasó, y cada cálculo
        // generaba las dos novedades.
        var vigentes = await db.PayrollRecurringNovelties.AsNoTracking()
            .Where(r => r.EmployeeId == employee.Id && r.ConceptCode == concept.Code && r.IsActive)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
        var repetida = vigentes.FirstOrDefault(v => RecurrenteRepetida.EsLaMisma(v, recurrente, concept.AllowsRepeatInPeriod));
        if (repetida is not null) return Result.Failure<Guid>(RecurrenteRepetida.Reparo(repetida, concept));

        db.PayrollRecurringNovelties.Add(recurrente);
        await db.SaveChangesAsync(ct);

        // Un borrador ya calculado que cubra la vigencia queda desactualizado: al recalcular se materializa.
        var calculados = await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayrollPlanId == employee.PayrollPlanId && p.Status == PayPeriodStatus.Calculated
                        && p.EndDate >= recurrente.StartDate && (recurrente.EndDate == null || p.StartDate <= recurrente.EndDate))
            .Select(p => p.Id).ToListAsync(ct);
        foreach (var id in calculados)
            await staleMarker.MarkStaleAsync(id, $"novedad recurrente {concept.Code} registrada", ct);
        if (calculados.Count > 0) await db.SaveChangesAsync(ct);

        return Result.Success(recurrente.PublicId);
    }
}

// ----------------------------------------------------------------- desactivar --

public sealed record DeactivateRecurringNoveltyCommand(Guid RecurringPublicId, string Reason) : IRequest<Result>;

public sealed class DeactivateRecurringNoveltyCommandValidator : AbstractValidator<DeactivateRecurringNoveltyCommand>
{
    public DeactivateRecurringNoveltyCommandValidator()
    {
        RuleFor(x => x.RecurringPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(300);
    }
}

/// <summary>Desactiva la recurrente y anula (Principio VII: estado, no borrado) sus novedades en períodos aún no aprobados.</summary>
public sealed class DeactivateRecurringNoveltyCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker staleMarker)
    : IRequestHandler<DeactivateRecurringNoveltyCommand, Result>
{
    public async Task<Result> Handle(DeactivateRecurringNoveltyCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure(new Error("Payroll.ReasonRequired", "El motivo es obligatorio."));
        var recurrente = await db.PayrollRecurringNovelties.FirstOrDefaultAsync(r => r.PublicId == request.RecurringPublicId, ct);
        if (recurrente is null) return Result.Failure(new Error("Payroll.RecurringNoveltyNotFound", "No existe la novedad recurrente indicada."));
        if (!recurrente.IsActive) return Result.Failure(new Error("Payroll.NoveltyNotActive", "La novedad recurrente ya está desactivada."));

        var ahora = clock.UtcNow;
        recurrente.IsActive = false;
        recurrente.DeactivationReason = request.Reason.Trim();
        recurrente.UpdatedAt = ahora;
        recurrente.UpdatedBy = user.UserName;

        var pendientes = await (
            from n in db.PayrollNovelties
            join p in db.PayPeriods.AsNoTracking() on n.PayPeriodId equals p.Id
            where n.RecurringNoveltyId == recurrente.Id && n.Status == NoveltyStatus.Active
                  && (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated)
            select n.Id).ToListAsync(ct);
        var novedades = pendientes.Count == 0 ? [] : await db.PayrollNovelties.Where(n => pendientes.Contains(n.Id)).ToListAsync(ct);
        foreach (var n in novedades)
        {
            n.Status = NoveltyStatus.Cancelled;
            n.StatusReason = $"Recurrente desactivada: {recurrente.DeactivationReason}";
            n.UpdatedAt = ahora;
            n.UpdatedBy = user.UserName;
        }
        await db.SaveChangesAsync(ct);
        foreach (var periodId in novedades.Select(n => n.PayPeriodId).Distinct())
            await staleMarker.MarkStaleAsync(periodId, $"recurrente {recurrente.ConceptCode} desactivada", ct);
        if (novedades.Count > 0) await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// --------------------------------------------------------------------- listar --

public sealed record ListRecurringNoveltiesQuery(Guid? EmployeePublicId, bool? Active) : IRequest<Result<IReadOnlyList<RecurringNoveltyDto>>>;

public sealed class ListRecurringNoveltiesQueryValidator : AbstractValidator<ListRecurringNoveltiesQuery>
{
    public ListRecurringNoveltiesQueryValidator() => RuleFor(x => x.EmployeePublicId).NotEqual(Guid.Empty).When(x => x.EmployeePublicId is not null);
}

public sealed class ListRecurringNoveltiesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListRecurringNoveltiesQuery, Result<IReadOnlyList<RecurringNoveltyDto>>>
{
    public async Task<Result<IReadOnlyList<RecurringNoveltyDto>>> Handle(ListRecurringNoveltiesQuery request, CancellationToken ct)
    {
        var filas = await (
            from r in db.PayrollRecurringNovelties.AsNoTracking()
            join e in db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where (request.EmployeePublicId == null || e.PublicId == request.EmployeePublicId)
                  && (request.Active == null || r.IsActive == request.Active)
            orderby r.IsActive descending, p.LastName, p.FirstName, r.StartDate
            select new { r, e.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId }).ToListAsync(ct);

        var codigos = filas.Select(f => f.r.ConceptCode).Distinct().ToList();
        var conceptos = (await db.PayrollConceptDefinitions.AsNoTracking().Where(c => codigos.Contains(c.Code)).OrderByDescending(c => c.ValidFrom).ToListAsync(ct))
            .GroupBy(c => c.Code).ToDictionary(g => g.Key, g => g.First());

        return Result.Success<IReadOnlyList<RecurringNoveltyDto>>(filas.Select(f =>
        {
            conceptos.TryGetValue(f.r.ConceptCode, out var c);
            return new RecurringNoveltyDto(f.r.PublicId, f.PublicId, NombreDePersona.Completo(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName), f.TaxId, f.r.ConceptCode, c?.Name ?? f.r.ConceptCode, (c?.Nature ?? ConceptNature.Earning).ToString(),
                f.r.Quantity, f.r.Amount, f.r.StartDate, f.r.EndDate, f.r.TotalInstallments, f.r.InstallmentsIssued, f.r.IsActive, f.r.Notes, f.r.DeactivationReason, f.r.CreatedAt, f.r.CreatedBy,
                f.r.ApplyOn.ToString());
        }).ToList());
    }
}

// ----------------------------------------------------------- materialización --

public sealed record MaterializationResult(int Created, IReadOnlyList<string> Warnings);

/// <summary>
/// Antes de calcular un período, convierte cada recurrente activa que lo cubre en una novedad
/// <c>Origin = Recurring</c> con su número de cuota, si la recurrente aún no dejó novedad en ese
/// período <b>en ningún estado</b>: una anulada quiere decir que alguien decidió que esta vez no va,
/// y hasta el 2026-09-19 el siguiente cálculo la volvía a crear porque sólo se miraban las activas.
/// Guarda en el acto: la novedad es un hecho del período, exista o no el cálculo. La cuota se
/// cuenta como emitida al aprobar (y se descuenta al reversar), no aquí.
///
/// <para>
/// Aplica las mismas reglas de repetición que una novedad digitada: si el concepto no admite
/// repetirse y el empleado ya tiene una activa de ese concepto en el período, la recurrente no
/// entra; y de dos recurrentes que son la misma orden (<see cref="RecurrenteRepetida"/>) entra la
/// más antigua. En ambos casos el cálculo lo dice en sus avisos.
/// </para>
/// </summary>
public sealed class RecurringNoveltiesMaterializer(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
{
    /// <summary>
    /// Con este motivo anula «Descartar borrador» las novedades que las recurrentes generaron para el
    /// período: son las únicas anuladas que el próximo cálculo vuelve a generar. Una anulada por una
    /// persona, con su motivo, no vuelve.
    /// </summary>
    public const string AnuladaPorDescarte = "Borrador descartado: se regenera en el próximo cálculo.";

    public async Task<MaterializationResult> MaterializeAsync(PayPeriod period, CancellationToken ct)
    {
        var candidatas = (await (
            from r in db.PayrollRecurringNovelties.AsNoTracking()
            join e in db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where r.IsActive && r.StartDate <= period.EndDate && (r.EndDate == null || r.EndDate >= period.StartDate)
                  && (r.TotalInstallments == null || r.InstallmentsIssued < r.TotalInstallments)
                  && e.PayrollPlanId == period.PayrollPlanId
            orderby r.Id
            select new { r, e, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName }).ToListAsync(ct))
            .Select(x => new { x.r, x.e, nombre = NombreDePersona.Completo(x.FirstName, x.OtherNames, x.LastName, x.SecondLastName) })
            .ToList();
        if (candidatas.Count == 0) return new MaterializationResult(0, []);

        var delPeriodo = await db.PayrollNovelties.AsNoTracking()
            .Where(n => n.PayPeriodId == period.Id)
            .Select(n => new { n.EmployeeId, n.ConceptCode, n.Status, n.StatusReason, n.RecurringNoveltyId })
            .ToListAsync(ct);
        // Con novedad en el período, en cualquier estado: ya entró, o alguien la anuló para este período.
        // Sólo la anulada por el descarte del borrador se vuelve a generar.
        var ya = delPeriodo
            .Where(n => n.RecurringNoveltyId != null && !(n.Status == NoveltyStatus.Cancelled && n.StatusReason == AnuladaPorDescarte))
            .Select(n => n.RecurringNoveltyId!.Value).ToHashSet();
        // Las que hoy pesan en el período: activas de cada concepto por empleado, y qué recurrentes las generaron.
        var activasPorConcepto = delPeriodo.Where(n => n.Status == NoveltyStatus.Active)
            .GroupBy(n => (n.EmployeeId, Codigo: n.ConceptCode.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.Count());
        var recurrentesActivas = delPeriodo.Where(n => n.Status == NoveltyStatus.Active && n.RecurringNoveltyId != null)
            .Select(n => n.RecurringNoveltyId!.Value).ToHashSet();

        var avisos = new List<string>();
        var creadas = 0;
        var ahora = clock.UtcNow;

        // Feature 006: «primero/último del mes» se decide con el sub-período del período que se
        // calcula. En semanal el último del mes es la mayor semana creada para ese mes.
        var periodicidad = period.PayrollPlan?.Periodicity
            ?? await db.PayrollPlans.AsNoTracking().Where(p => p.Id == period.PayrollPlanId).Select(p => p.Periodicity).FirstAsync(ct);
        byte? mayorSemana = periodicidad == PayrollPeriodicity.Weekly
            ? await db.PayPeriods.AsNoTracking()
                .Where(p => p.PayrollPlanId == period.PayrollPlanId && p.ImputationYear == period.ImputationYear && p.ImputationMonth == period.ImputationMonth)
                .MaxAsync(p => (byte?)p.SubPeriodNumber, ct)
            : null;
        var esPrimero = period.SubPeriodNumber == 1;
        var esUltimo = PeriodCalendar.EsUltimoDelMes(periodicidad, period.SubPeriodNumber, mayorSemana);

        var generadasAhora = new List<PayrollRecurringNovelty>();
        foreach (var c in candidatas.Where(c => !ya.Contains(c.r.Id)))
        {
            if (NoveltyRules.EnsureEmployeeInPeriod(c.e, period).IsFailure) continue;
            var aplica = c.r.ApplyOn switch
            {
                RecurringApplyRule.FirstOfMonth => esPrimero,
                RecurringApplyRule.LastOfMonth => esUltimo,
                _ => true,
            };
            if (!aplica) continue;
            var concepto = await NoveltyRules.ResolveConceptAsync(db, c.r.ConceptCode, period.EndDate, c.e.EmployeeClass, ct);
            if (concepto.IsFailure)
            {
                avisos.Add($"Recurrente {c.r.ConceptCode} de {c.nombre}: {concepto.Error.Message}");
                continue;
            }

            // Las mismas reglas que una novedad digitada (NoveltyRules.EnsureNoDuplicateAsync y RecurrenteRepetida).
            var clave = (c.e.Id, concepto.Value.Code.ToUpperInvariant());
            var activas = activasPorConcepto.GetValueOrDefault(clave) + generadasAhora.Count(g => g.EmployeeId == c.e.Id && string.Equals(g.ConceptCode, concepto.Value.Code, StringComparison.OrdinalIgnoreCase));
            if (!concepto.Value.AllowsRepeatInPeriod && activas > 0)
            {
                avisos.Add($"Recurrente {concepto.Value.Name} de {c.nombre}: no se generó porque el empleado ya tiene una novedad activa de ese concepto en el período y el concepto no admite repetirse. Si sobra una recurrente, desactívela en Novedades › Recurrentes.");
                continue;
            }
            var gemela = candidatas
                .Where(o => o.r.Id != c.r.Id && (recurrentesActivas.Contains(o.r.Id) || generadasAhora.Contains(o.r)))
                .Select(o => o.r)
                .FirstOrDefault(o => RecurrenteRepetida.EsLaMisma(o, c.r, concepto.Value.AllowsRepeatInPeriod));
            if (gemela is not null)
            {
                avisos.Add($"Recurrente {concepto.Value.Name} de {c.nombre} (registrada el {c.r.CreatedAt:dd/MM/yyyy HH:mm}): es la misma orden que otra recurrente activa ({RecurrenteRepetida.Descripcion(gemela, concepto.Value.Name)}, registrada el {gemela.CreatedAt:dd/MM/yyyy HH:mm}) y no se generó dos veces. Desactive la sobrante en Novedades › Recurrentes.");
                continue;
            }

            db.PayrollNovelties.Add(new PayrollNovelty
            {
                PayPeriodId = period.Id,
                EmployeeId = c.e.Id,
                ConceptDefinitionId = concepto.Value.Id,
                ConceptCode = concepto.Value.Code,
                Quantity = c.r.Quantity,
                Amount = c.r.Amount,
                Notes = c.r.Notes,
                Status = NoveltyStatus.Active,
                Origin = NoveltyOrigin.Recurring,
                RecurringNoveltyId = c.r.Id,
                InstallmentNumber = c.r.InstallmentsIssued + 1,
                InstallmentTotal = c.r.TotalInstallments,
                CreatedAt = ahora,
                CreatedBy = user.UserName ?? "sistema",
            });
            generadasAhora.Add(c.r);
            creadas++;
        }
        if (creadas > 0) await db.SaveChangesAsync(ct);
        return new MaterializationResult(creadas, avisos);
    }
}
