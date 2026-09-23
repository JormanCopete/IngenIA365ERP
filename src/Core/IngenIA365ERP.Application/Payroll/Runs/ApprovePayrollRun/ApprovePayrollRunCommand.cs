using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;

public sealed record ApproveRunResultDto(
    Guid RunPublicId,
    Guid PeriodPublicId,
    Guid AccountingDocumentPublicId,
    string AccountingDocumentNumber,
    RunTotalsDto Totals,
    int EmployeeCount,
    bool ApprovedWithoutSegregation);

/// <summary>
/// Aprueba la corrida en borrador (FR-019..FR-023; D-07): sin bloqueos —o con excepción
/// autorizada por quien tiene <c>Payroll.Runs.AuthorizeException</c>—, con segregación de
/// funciones (o con doble confirmación si la cooperativa lo permite), genera el comprobante
/// contable <c>NM</c> con el contabilizador y cierra el período, todo en UNA transacción:
/// si el asiento no se puede generar, no hay aprobación.
/// </summary>
public sealed record ApprovePayrollRunCommand(
    Guid RunPublicId,
    bool Confirm,
    IReadOnlyList<ApprovalExceptionDto>? Exceptions = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false) : IRequest<Result<ApproveRunResultDto>>, IReintentableAnteConcurrencia;

public sealed class ApprovePayrollRunCommandValidator : AbstractValidator<ApprovePayrollRunCommand>
{
    public ApprovePayrollRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleForEach(x => x.Exceptions).ChildRules(e =>
        {
            e.RuleFor(x => x.EmployeePublicId).NotEmpty();
            e.RuleFor(x => x.Flag).NotEmpty().WithMessage("La excepción debe indicar el bloqueo que autoriza.");
            e.RuleFor(x => x.Reason).NotEmpty().WithMessage("Toda excepción lleva motivo.").MaximumLength(300);
        });
    }
}

public sealed class ApprovePayrollRunCommandHandler(
    IApplicationDbContext db,
    PayrollAccountingPoster poster,
    PayrollPolicyReader policies,
    IPermissionChecker permissions,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit,
    IPayrollRunStaleMarker staleMarker)
    : IRequestHandler<ApprovePayrollRunCommand, Result<ApproveRunResultDto>>
{
    public const string AuthorizeExceptionPermission = "Payroll.Runs.AuthorizeException";

    public async Task<Result<ApproveRunResultDto>> Handle(ApprovePayrollRunCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null)
            return Fallo("Payroll.RunNotFound", "No existe la corrida indicada.");
        // Feature 010: una prima o una definitiva se aprueba por su ruta con su permiso; si no, Payroll.Runs.Approve las aprobaría todas.
        if (run.EsEspecial)
            return Result.Failure<ApproveRunResultDto>(Settlements.Common.SettlementErrors.UseSettlementRoute(run.Kind));
        if (run.Status == PayrollRunStatus.Stale)
            return Fallo("Payroll.RunStale", "El borrador está desactualizado: cambió una novedad, un salario, un concepto o un parámetro desde el cálculo. Recalcule antes de aprobar.");
        if (run.Status != PayrollRunStatus.Draft)
            return Fallo("Payroll.RunNotDraft", $"La corrida está {run.Status}: sólo se aprueba el borrador vigente.");
        var period = run.PayPeriod!;
        if (period.Status != PayPeriodStatus.Calculated)
            return Fallo("Payroll.PeriodNotOpen", $"El período está {period.Status}; debe estar calculado para aprobarse.");

        if (!request.Confirm)
            return Fallo("Payroll.ConfirmationRequired", "La aprobación exige confirmación explícita (confirm = true).");

        var empleados = await db.PayrollRunEmployees
            .Include(e => e.Lines)
            .Include(e => e.Employee)
            .Where(e => e.PayrollRunId == run.Id)
            .ToListAsync(ct);

        if (empleados.Count == 0 && !request.ConfirmEmpty)
            return Fallo("Payroll.ConfirmationRequired", "El borrador no tiene empleados. Aprobar un período vacío exige confirmEmpty = true.");

        // Los bloqueos se nombran por persona, no por identificador: quien aprueba tiene que
        // saber a quién autoriza. (Employee.Person no se incluye: el join es explícito.)
        var idsEmpleados = empleados.Select(e => e.EmployeeId).ToList();
        var nombres = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where idsEmpleados.Contains(e.Id)
            select new { e.Id, Nombre = (p.FirstName + " " + p.LastName).Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        // --- bloqueos y excepciones (FR-022) ---
        var excepciones = request.Exceptions ?? [];
        var bloqueosSinExcepcion = new List<string>();
        foreach (var e in empleados.Where(e => e.HasBlockers))
        {
            foreach (var flag in RunJson.FlagNames(e.Flags))
            {
                var cubierta = excepciones.Any(x => x.EmployeePublicId == e.Employee!.PublicId && x.Flag.Equals(flag, StringComparison.OrdinalIgnoreCase));
                if (!cubierta) bloqueosSinExcepcion.Add($"{NombreDe(e, nombres)}: {RunJson.FlagLabel(Enum.Parse<RunEmployeeFlag>(flag))}");
            }
        }
        if (bloqueosSinExcepcion.Count > 0)
            return Fallo("Payroll.ApprovalBlocked",
                "La aprobación está bloqueada. Corrija la causa o autorice la excepción con motivo: " + string.Join("; ", bloqueosSinExcepcion));

        if (excepciones.Count > 0 && !await permissions.HasPermissionAsync(AuthorizeExceptionPermission, ct))
            return Fallo("Payroll.ExceptionNotAuthorized",
                "Autorizar una excepción a un bloqueo exige el permiso Payroll.Runs.AuthorizeException.");

        // --- segregación de funciones (FR-020, FR-021) ---
        var politica = await policies.ReadAsync(DateOnly.FromDateTime(period.EndDate), ct);
        var yo = user.UserName ?? string.Empty;
        var actores = await db.PayrollNovelties.AsNoTracking()
            .Where(n => n.PayPeriodId == period.Id && n.Status == NoveltyStatus.Active)
            .Select(n => new { n.CreatedBy, n.UpdatedBy })
            .ToListAsync(ct);
        var participo = actores.Any(a => Igual(a.CreatedBy, yo) || Igual(a.UpdatedBy, yo)) || Igual(run.CalculatedBy, yo);
        var sinSegregacion = false;
        if (participo)
        {
            if (!politica.AllowSameUserApproval)
                return Fallo("Payroll.SegregationOfDuties",
                    "Quien registra novedades o calcula no puede aprobar la misma nómina. Otra persona con el permiso Payroll.Runs.Approve (Seguridad › Roles) debe hacerlo, o la cooperativa habilita la política AllowSameUserApproval en Nómina › Políticas de la empresa, con vigencia que cubra el fin del período; quien apruebe así deberá confirmarlo expresamente y quedará registrado.");
            if (!request.ConfirmWithoutSegregation)
                // Código propio, no el genérico de confirmación: la pantalla tiene que distinguir
                // ESTE caso para revelar la casilla de la segunda confirmación, y hasta el 2026-09-22
                // lo hacía buscando «segregaci» en el texto —que no aparece en ninguna parte de este
                // mensaje—, así que la casilla no salía nunca y aprobar la propia nómina era
                // imposible desde la aplicación. Un mensaje es para quien lee, no para ramificar.
                return Fallo("Payroll.SegregationConfirmationRequired",
                    "Usted participó en las novedades o el cálculo de este período. La cooperativa permite aprobarlo igual, pero exige confirmarlo expresamente: marque «Confirmo aprobar sin segregación de funciones». Quedará registrado en la corrida.");
            sinSegregacion = true;
        }

        // --- comprobante contable (FR-019, FR-023) ---
        var ahora = clock.UtcNow;
        var fechaDocumento = DateOnly.FromDateTime(period.EndDate);
        var detalle = $"Nómina {period.Description ?? period.StartDate.ToString("MMMM yyyy")} v{run.Version} ({period.StartDate:dd/MM/yyyy}–{period.EndDate:dd/MM/yyyy})";
        var posting = await poster.PostAsync(run,
            empleados.Select(e => (e, e.Employee!, (IReadOnlyList<PayrollRunLine>)e.Lines.ToList())).ToList(),
            fechaDocumento, detalle, ct);
        if (posting.IsFailure) return Fallo(posting.Error.Code, posting.Error.Message);

        // --- cierre: corrida, período y trazas, en el mismo SaveChanges que el asiento ---
        var autorizadas = excepciones.Select(x => x with { AuthorizedBy = yo, AuthorizedAt = ahora }).ToList();
        run.Status = PayrollRunStatus.Approved;
        run.ApprovedAt = ahora;
        run.ApprovedBy = yo;
        run.ApprovedWithoutSegregation = sinSegregacion;
        run.ExceptionsJson = autorizadas.Count == 0 ? null : JsonSerializer.Serialize(autorizadas, RunJson.Options);
        run.AccountingDocument = posting.Value;
        run.UpdatedAt = ahora;
        run.UpdatedBy = yo;

        period.Status = PayPeriodStatus.Approved;
        period.ApprovedAt = ahora;
        period.ApprovedBy = yo;
        period.RunPublicId = run.PublicId;
        period.StatusMessage = PayPeriod.Mensaje($"Aprobado por {yo} el {ahora:dd/MM/yyyy HH:mm} UTC · comprobante {posting.Value.Referencia()}");
        period.UpdatedAt = ahora;
        period.UpdatedBy = yo;

        // US6: cada recurrente con novedad activa en el período emite su cuota al aprobar (la reversión la descuenta).
        var recurrentesIds = await db.PayrollNovelties.AsNoTracking()
            .Where(n => n.PayPeriodId == period.Id && n.Status == NoveltyStatus.Active && n.RecurringNoveltyId != null)
            .Select(n => n.RecurringNoveltyId!.Value).Distinct().ToListAsync(ct);
        if (recurrentesIds.Count > 0)
        {
            foreach (var r in await db.PayrollRecurringNovelties.Where(r => recurrentesIds.Contains(r.Id)).ToListAsync(ct))
            {
                r.InstallmentsIssued++;
                r.UpdatedAt = ahora;
                r.UpdatedBy = yo;
            }
        }

        // Feature 010 (revisión N1): las provisiones y bases de un borrador de prima, cesantías, vacaciones o
        // definitiva de estos empleados con corte desde este período cambian con esta aprobación: quedan Stale.
        await staleMarker.MarkSettlementDraftsStaleAsync(idsEmpleados, DateOnly.FromDateTime(period.StartDate),
            $"aprobación de la nómina ordinaria {period.StartDate:yyyy-MM-dd} a {period.EndDate:yyyy-MM-dd}", ct);

        await db.SaveChangesAsync(ct);

        var totales = new RunTotalsDto(run.TotalEarnings, run.TotalDeductions, run.TotalEmployerContributions, run.TotalProvisions, run.TotalNet, run.RoundingAdjustment);
        await audit.EmitAsync(AuditEventTypes.PayrollRunApproved, nameof(PayrollRun), run.PublicId,
            new { status = "Draft" },
            new
            {
                status = "Approved", periodPublicId = period.PublicId, version = run.Version, employees = run.EmployeeCount,
                totales, document = posting.Value.Referencia(),
                documentPublicId = posting.Value.PublicId, exceptions = autorizadas, approvedWithoutSegregation = sinSegregacion,
            }, ct);

        return Result.Success(new ApproveRunResultDto(run.PublicId, period.PublicId, posting.Value.PublicId,
            posting.Value.Referencia(), totales, run.EmployeeCount, sinSegregacion));
    }

    private static Result<ApproveRunResultDto> Fallo(string code, string message) =>
        Result.Failure<ApproveRunResultDto>(new Error(code, message));

    private static bool Igual(string? a, string b) =>
        !string.IsNullOrEmpty(a) && a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static string NombreDe(PayrollRunEmployee e, IReadOnlyDictionary<int, string> nombres) =>
        nombres.TryGetValue(e.EmployeeId, out var n) && n.Length > 0 ? n : e.Employee?.PublicId.ToString() ?? e.EmployeeId.ToString();
}
