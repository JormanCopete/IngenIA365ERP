using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;
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
    bool ApprovedWithoutSegregation,
    IReadOnlyList<WarningDto>? Warnings = null);

/// <summary>
/// Aprueba la corrida en borrador (FR-019..FR-023; D-07): sin bloqueos —o con excepción
/// autorizada por quien tiene <c>Payroll.Runs.AuthorizeException</c>—, con segregación de
/// funciones (o con doble confirmación si la cooperativa lo permite), genera el comprobante
/// contable <c>NM</c> con el contabilizador y cierra el período, todo en UNA transacción:
/// si el asiento no se puede generar, no hay aprobación.
/// <para>
/// Feature 013 (T0165; R106, FR-112, T92): corre en una <see cref="TransaccionExplicita"/> (sigue reintentable: cada repetición
/// relee todo) y, <b>antes</b> de armar el NM, entrega a Cartera por el contrato de obligaciones (<see cref="RecaudoPorNomina"/>)
/// lo descontado de cada obligación (<c>CycleInstallment</c>, fecha valor = fin del período, A9) y lo no descontado en la misma
/// llamada. Un rechazo o un excedente <b>no</b> bloquea: la línea <c>DESC_CARTERA</c> baja a lo que Cartera aplicó, el neto sube,
/// la fila de <c>PAY_PayrollLoanDeductions</c> guarda el resultado y se avisa <c>Payroll.LoanDeductionNotApplied</c>. Con personas
/// «migradas, sin activar» exige <see cref="ApprovePayrollRunCommand.AcknowledgeLendingPendingActivation"/>.
/// </para>
/// </summary>
public sealed record ApprovePayrollRunCommand(
    Guid RunPublicId,
    bool Confirm,
    IReadOnlyList<ApprovalExceptionDto>? Exceptions = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false,
    bool AcknowledgeLendingPendingActivation = false) : IRequest<Result<ApproveRunResultDto>>, IReintentableAnteConcurrencia;

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
    IPayrollRunStaleMarker staleMarker,
    RecaudoPorNomina recaudo,
    IConsultaDeObligaciones obligaciones)
    : IRequestHandler<ApprovePayrollRunCommand, Result<ApproveRunResultDto>>
{
    public const string AuthorizeExceptionPermission = "Payroll.Runs.AuthorizeException";

    public async Task<Result<ApproveRunResultDto>> Handle(ApprovePayrollRunCommand request, CancellationToken ct)
    {
        Aprobacion? aprobacion = null;
        var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            aprobacion = null;
            var intento = await AprobarAsync(request, ct);
            if (intento.IsFailure) return Result.Failure<ApproveRunResultDto>(intento.Error);
            aprobacion = intento.Value;
            return Result.Success(intento.Value.Dto);
        }, ct);
        if (r.IsFailure || aprobacion is null) return r;

        await audit.EmitAsync(AuditEventTypes.PayrollRunApproved, nameof(PayrollRun), aprobacion.Dto.RunPublicId,
            new { status = "Draft" }, aprobacion.Despues, ct);
        return r;
    }

    /// <summary>Lo aprobado y lo que la auditoría registra después de confirmar.</summary>
    private sealed record Aprobacion(ApproveRunResultDto Dto, object Despues);

    private async Task<Result<Aprobacion>> AprobarAsync(ApprovePayrollRunCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null)
            return Fallo("Payroll.RunNotFound", "No existe la corrida indicada.");
        // Feature 010: una prima o una definitiva se aprueba por su ruta con su permiso; si no, Payroll.Runs.Approve las aprobaría todas.
        if (run.EsEspecial)
            return Result.Failure<Aprobacion>(SettlementErrors.UseSettlementRoute(run.Kind));
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
            select new { e.Id, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName })
            .ToDictionaryAsync(x => x.Id, x => NombreDePersona.Completo(x.FirstName, x.OtherNames, x.LastName, x.SecondLastName), ct);

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

        // --- feature 013: Cartera por el contrato de obligaciones, ANTES del NM (T0165, R106) ---
        var ahora = clock.UtcNow;
        var avisos = new List<WarningDto>();
        var cartera = await RecaudarEnCarteraAsync(run, period, empleados, nombres, request.AcknowledgeLendingPendingActivation, yo, ahora, avisos, ct);
        if (cartera.IsFailure) return Result.Failure<Aprobacion>(cartera.Error);

        // --- comprobante contable (FR-019, FR-023) ---
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
        var despues = new
        {
            status = "Approved", periodPublicId = period.PublicId, version = run.Version, employees = run.EmployeeCount,
            totales, document = posting.Value.Referencia(),
            documentPublicId = posting.Value.PublicId, exceptions = autorizadas, approvedWithoutSegregation = sinSegregacion,
            lendingActivationAcknowledgedBy = run.LendingActivationAckBy,
            loanDeductions = cartera.Value,
        };

        return Result.Success(new Aprobacion(new ApproveRunResultDto(run.PublicId, period.PublicId, posting.Value.PublicId,
            posting.Value.Referencia(), totales, run.EmployeeCount, sinSegregacion, avisos), despues));
    }

    /// <summary>
    /// Feature 013 (T0165): entrega a Cartera lo descontado y lo no descontado de cada obligación y deja el resultado en
    /// <c>PAY_PayrollLoanDeductions</c>. Un rechazo o un excedente no bloquea: la línea baja a lo aplicado y el neto sube.
    /// Devuelve el resumen para la auditoría.
    /// </summary>
    private async Task<Result<IReadOnlyList<object>>> RecaudarEnCarteraAsync(PayrollRun run, PayPeriod period, List<PayrollRunEmployee> empleados,
        IReadOnlyDictionary<int, string> nombres, bool reconocido, string yo, DateTime ahora, List<WarningDto> avisos, CancellationToken ct)
    {
        var filas = await db.PayrollLoanDeductions.Where(d => d.PayrollRunId == run.Id).OrderBy(d => d.ObligationPublicId).ToListAsync(ct);
        var idsEmpleados = empleados.Select(e => e.EmployeeId).ToList();
        var personas = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where idsEmpleados.Contains(e.Id)
            select new { e.Id, p.PublicId }).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);

        // --- «Cartera por activar» (DE-10) ---
        if (personas.Count > 0)
        {
            ObligacionesDePersonasDto? consulta = null;
            try
            {
                consulta = await obligaciones.ObligacionesAsync(new ConsultaDeObligaciones(personas.Values.Distinct().ToList(), clock.HoyLocal, IncludeAsCoDebtor: false), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                if (filas.Count > 0)
                    return Result.Failure<IReadOnlyList<object>>(new Error(SettlementErrors.PortfolioUnavailableCode,
                        $"Cartera no respondió al confirmar los descuentos de la nómina ({ex.Message}). Intente aprobar de nuevo en unos minutos."));
            }
            var migradas = consulta?.People.Where(p => p.MigratedNotActivated).Select(p => p.PersonPublicId).ToHashSet() ?? [];
            if (migradas.Count > 0)
            {
                var nombresMigrados = personas.Where(x => migradas.Contains(x.Value)).Select(x => nombres.GetValueOrDefault(x.Key, x.Value.ToString())).ToList();
                if (!reconocido) return Result.Failure<IReadOnlyList<object>>(SettlementErrors.LendingPendingActivationAckRequired(nombresMigrados));
                run.LendingActivationAckBy = yo;
                run.LendingActivationAckAt = ahora;
            }
        }
        if (filas.Count == 0) return Result.Success<IReadOnlyList<object>>([]);

        // --- el lote: lo descontado (CycleInstallment) y lo no descontado, en una sola llamada (C32, C56) ---
        var filaEmpleado = empleados.ToDictionary(e => e.Id);
        Guid Pagador(PayrollLoanDeduction f) => personas[filaEmpleado[f.PayrollRunEmployeeId].EmployeeId];
        var recibo = new ReferenciaDeRecibo(
            $"Nómina {period.StartDate:dd/MM/yyyy}-{period.EndDate:dd/MM/yyyy} v{run.Version}", clock.HoyLocal, null, null, "NM");
        var lote = new LoteDeNomina(run.PublicId, recibo, DateOnly.FromDateTime(period.EndDate),
            filas.Where(f => f.DeductedAmount > 0m)
                .Select(f => new PagoDeNomina(f.ObligationPublicId, Pagador(f), ModalidadDePago.CycleInstallment, f.DeductedAmount)).ToList(),
            filas.Where(f => f.NotDeductedAmount > 0m)
                .Select(f => new NoDescontadoDeNomina(f.ObligationPublicId, f.ExpectedAmount, f.NotDeductedAmount,
                    Enum.TryParse<MotivoDeNoDescuento>(f.NotDeductedReason, out var m) ? m : MotivoDeNoDescuento.Other,
                    $"Nómina {period.StartDate:dd/MM/yyyy}–{period.EndDate:dd/MM/yyyy}", f.PublicId)).ToList());
        var resultado = await recaudo.AplicarAsync(lote, ct);

        // Feature 013, T0801 (A8 = «Bloquear todo», 2026-10-14): un descuento que Contabilidad no puede registrar no baja la línea: la aprobación
        // entera falla y dice qué regla falta (la transacción de Nómina revierte lo que Cartera alcanzó a aplicar, I-4).
        var noContabilizables = resultado.Pagos.Where(p => p.NoContabilizable).ToList();
        if (noContabilizables.Count > 0)
            return Result.Failure<IReadOnlyList<object>>(SettlementErrors.LoanDeductionNotPostable(noContabilizables
                .Select(p => (filas.First(f => f.ObligationPublicId == p.ObligationPublicId).ObligationNumber,
                    p.Respuesta.Message, (object?)p.Respuesta.Data)).ToList()));

        var motivos = new List<string>();
        foreach (var f in filas)
        {
            f.UpdatedAt = ahora;
            f.UpdatedBy = yo;
            if (f.DeductedAmount <= 0m)
            {
                f.AppliedAmount = 0m;
                f.Status = PayrollLoanDeductionStatus.NotApplied;
                var registro = resultado.NoDescontados.ElementAtOrDefault(lote.NoDescontados.ToList().FindIndex(n => n.FilaPublicId == f.PublicId));
                f.ResultCode = registro is null ? null : registro.Result == ResultadoDelContrato.Rejected ? registro.Code : registro.Result.ToString();
                f.ResultMessage = Recortar(registro?.Message);
                continue;
            }

            var pago = resultado.Pagos.First(p => p.ObligationPublicId == f.ObligationPublicId);
            var aplicado = Math.Min(pago.ValorAplicado, f.DeductedAmount);
            f.AppliedAmount = aplicado;
            f.CarteraReceiptPublicId = pago.Aplicado ? pago.Respuesta.Value!.OperationPublicId : null;
            f.ResultCode = pago.Aplicado ? pago.Respuesta.Result.ToString() : pago.Respuesta.Code;
            f.ResultMessage = Recortar(pago.Respuesta.Message);
            f.Status = !pago.Aplicado ? PayrollLoanDeductionStatus.Rejected
                : aplicado < f.DeductedAmount ? PayrollLoanDeductionStatus.PartiallyApplied
                : PayrollLoanDeductionStatus.Applied;
            if (aplicado >= f.DeductedAmount) continue;

            // --- no bloquea (FR-112): la línea baja a lo aplicado y el neto del empleado sube por la diferencia ---
            var diferencia = f.DeductedAmount - aplicado;
            var fila = filaEmpleado[f.PayrollRunEmployeeId];
            var linea = f.PayrollRunLineId is { } lid ? fila.Lines.FirstOrDefault(l => l.Id == lid) : null;
            if (linea is not null)
            {
                linea.Amount -= diferencia;
                linea.ExplanationJson = ConNota(linea.ExplanationJson, aplicado, f, pago);
                linea.UpdatedAt = ahora;
                linea.UpdatedBy = yo;
            }
            fila.TotalDeductions -= diferencia;
            fila.NetPay += diferencia;
            fila.UpdatedAt = ahora;
            fila.UpdatedBy = yo;
            run.TotalDeductions -= diferencia;
            run.TotalNet += diferencia;
            motivos.Add($"{nombres.GetValueOrDefault(fila.EmployeeId, string.Empty)}, crédito {f.ObligationNumber}: " +
                        (pago.Aplicado ? $"Cartera tomó {Fmt.Num(aplicado)} de {Fmt.Num(f.DeductedAmount)}; la diferencia vuelve al neto" : pago.Respuesta.Message));
        }
        if (motivos.Count > 0) avisos.Add(SettlementErrors.LoanDeductionNotApplied(motivos));

        return Result.Success<IReadOnlyList<object>>(filas.Select(f => (object)new
        {
            obligationPublicId = f.ObligationPublicId, obligationNumber = f.ObligationNumber, expected = f.ExpectedAmount, deducted = f.DeductedAmount,
            notDeducted = f.NotDeductedAmount, reason = f.NotDeductedReason, applied = f.AppliedAmount, status = f.Status.ToString(),
            receipt = f.CarteraReceiptPublicId, resultCode = f.ResultCode,
        }).ToList());
    }

    private static string? Recortar(string? texto) => texto is null ? null : texto.Length <= 500 ? texto : texto[..500];

    /// <summary>La explicación de la línea dice por qué bajó (FR-013): lo que Cartera aplicó y su respuesta.</summary>
    private static string ConNota(string explicacionJson, decimal aplicado, PayrollLoanDeduction f, RecaudoDeNomina pago)
    {
        Explanation? exp = null;
        try { exp = JsonSerializer.Deserialize<Explanation>(explicacionJson, RunJson.Options); }
        catch (JsonException) { exp = null; }
        exp ??= new Explanation { Form = "Cuota de Cartera" };
        exp.Step("Aplicado por Cartera al aprobar", aplicado);
        exp.Note("Respuesta de Cartera", pago.Respuesta.Message);
        exp.Summary = $"Cuota del crédito {f.ObligationNumber}: {Fmt.Money(aplicado)} (Cartera no tomó {Fmt.Money(f.DeductedAmount - aplicado)})";
        return JsonSerializer.Serialize(exp, RunJson.Options);
    }

    private static Result<Aprobacion> Fallo(string code, string message) =>
        Result.Failure<Aprobacion>(new Error(code, message));

    private static bool Igual(string? a, string b) =>
        !string.IsNullOrEmpty(a) && a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static string NombreDe(PayrollRunEmployee e, IReadOnlyDictionary<int, string> nombres) =>
        nombres.TryGetValue(e.EmployeeId, out var n) && n.Length > 0 ? n : e.Employee?.PublicId.ToString() ?? e.EmployeeId.ToString();
}
