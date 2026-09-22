using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Payments.Commands.ProcessPayment;
using IngenIA365ERP.Application.Lending.Payments.Services;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>
/// Aprueba la liquidación definitiva (feature 010, US3, FR-020; contracts/api.md §3.4
/// <c>POST /{runId}/approve</c>) sobre el ciclo común <see cref="SettlementRunWorkflow"/>, con lo
/// propio de la definitiva en el gancho <c>antesDeGuardar</c> y todo dentro de una transacción:
/// <list type="bullet">
/// <item>cada descuento de Cartera con valor aplicado se paga por <see cref="RecaudoDeCredito"/> —el cuerpo
/// de <c>ProcessPaymentCommand</c>, llamado <b>directo</b> y no por <c>ISender</c>— (forma de pago <c>NM</c>,
/// referencia = número de la liquidación, la cuenta del recaudo es la cuenta débito del concepto
/// <c>DESC_CARTERA</c>): Cartera contabiliza el recaudo y la nómina no lo duplica (D-08); el descuento
/// guarda el recaudo y el saldo que quedó. Anidar el comando era un defecto: es reintentable, y su
/// reintento vaciaba el <c>ChangeTracker</c> compartido dejando el recaudo guardado con la corrida en
/// borrador. Hoy un conflicto de concurrencia deshace la transacción entera y este comando —también
/// reintentable— se repite entero;</item>
/// <item>la ficha se cierra (<c>Status = -1</c>, <c>TerminationDate</c>, <c>TerminationCause</c> = nombre
/// del motivo) y la persona deja de ser empleada;</item>
/// <item>las vacaciones pagadas quedan como movimiento <c>SettlementPayout</c> liquidado;</item>
/// <item>la terminación pasa a <c>Settled</c>;</item>
/// <item>el borrador de la nómina ordinaria del período donde cae el retiro, si existe, queda <c>Stale</c>
/// (D-29): al recalcularlo el empleado ya no entra, porque su último tramo lo pagó esta definitiva.</item>
/// </list>
/// Tras confirmar, el PDF para firma se guarda en <c>COR_Attachments</c> (<c>OwnerEntityType =
/// "EmploymentTermination"</c>); si eso falla la aprobación no se deshace —el documento se vuelve a
/// generar desde la corrida cuando se pida— y queda en el log. Auditoría
/// <c>Payroll.Settlement.Approved</c> (ciclo común) y <c>Payroll.Employee.Terminated</c>.
/// </summary>
public sealed record ApproveSettlementCommand(
    Guid RunPublicId,
    bool Confirm,
    DateOnly? PostingDate = null,
    bool ConfirmWithoutSegregation = false) : IRequest<Result<SettlementApprovedWithPortfolioDto>>, IReintentableAnteConcurrencia;

public sealed class ApproveSettlementCommandValidator : AbstractValidator<ApproveSettlementCommand>
{
    public ApproveSettlementCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ApproveSettlementCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    RecaudoDeCredito recaudo,
    ISender sender,
    IDateTimeService clock,
    ICurrentUserService user,
    ICurrentTenantService tenant,
    ISettlementDocumentRenderer renderer,
    PayrollAuditEmitter audit,
    IPayrollRunStaleMarker staleMarker,
    ILogger<ApproveSettlementCommandHandler> logger)
    : IRequestHandler<ApproveSettlementCommand, Result<SettlementApprovedWithPortfolioDto>>
{
    /// <summary>Forma de pago con la que Cartera registra el recaudo por descuento en la liquidación (research R7).</summary>
    public const string FormaDePagoNomina = "NM";

    public async Task<Result<SettlementApprovedWithPortfolioDto>> Handle(ApproveSettlementCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementApprovedWithPortfolioDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement || run.TerminationId is null)
            return Result.Failure<SettlementApprovedWithPortfolioDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));

        var terminacion = await db.EmploymentTerminations.Include(t => t.TerminationReason).Include(t => t.Deductions)
            .FirstOrDefaultAsync(t => t.Id == run.TerminationId, ct);
        if (terminacion is null) return Result.Failure<SettlementApprovedWithPortfolioDto>(SettlementErrors.TerminationNotFound);
        if (terminacion.Status != TerminationStatus.Registered)
            return Result.Failure<SettlementApprovedWithPortfolioDto>(SettlementErrors.NotDraft(run.Status));

        var empleado = await db.Employees.FirstOrDefaultAsync(e => e.Id == terminacion.EmployeeId, ct);
        if (empleado is null) return Result.Failure<SettlementApprovedWithPortfolioDto>(SettlementErrors.EmployeeNotFound);
        var persona = await db.People.FirstOrDefaultAsync(p => p.Id == empleado.PersonId, ct);

        var pagos = new List<PortfolioPaymentDto>();
        var peticion = new SettlementApprovalRequest(run.PublicId, PayrollRunKind.Settlement, request.Confirm, request.PostingDate, ConfirmEmpty: false, request.ConfirmWithoutSegregation);

        var aprobado = await TransaccionDeLiquidacion.EjecutarAsync(db,
            () => workflow.ApproveAsync(peticion, (corrida, filas, token) => CerrarFichaYPagarAsync(corrida, filas, terminacion, empleado, persona, pagos, token), ct), ct);
        if (aprobado.IsFailure) return Result.Failure<SettlementApprovedWithPortfolioDto>(aprobado.Error);

        await audit.EmitAsync(AuditEventTypes.PayrollEmployeeTerminated, nameof(Employee), empleado.PublicId,
            new { status = 1, terminationDate = (DateOnly?)null },
            new
            {
                status = empleado.Status, terminationDate = terminacion.TerminationDate, terminationCause = empleado.TerminationCause,
                terminationPublicId = terminacion.PublicId, runPublicId = run.PublicId, document = aprobado.Value.Number, net = aprobado.Value.Total,
                portfolioPayments = pagos,
            }, ct);

        var adjunto = await GuardarDocumentoAsync(run.PublicId, terminacion, aprobado.Value.Number, ct);

        return Result.Success(new SettlementApprovedWithPortfolioDto(
            run.PublicId, terminacion.PublicId, aprobado.Value.DocumentPublicId, aprobado.Value.Number, aprobado.Value.Total,
            aprobado.Value.PostingDate, aprobado.Value.ApprovedWithoutSegregation, pagos, adjunto));
    }

    /// <summary>Lo propio de la definitiva dentro de la transacción de la aprobación. Un fallo aquí revierte todo, recaudos incluidos.</summary>
    private async Task<Result> CerrarFichaYPagarAsync(PayrollRun corrida, IReadOnlyList<PayrollRunEmployee> filas, EmploymentTermination terminacion,
        Employee empleado, Domain.Entities.Core.Person? persona, List<PortfolioPaymentDto> pagos, CancellationToken ct)
    {
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? string.Empty;
        var fecha = terminacion.TerminationDate;
        var referencia = corrida.AccountingDocument?.Referencia() ?? $"Definitiva v{corrida.Version}";

        // --- recaudos en Cartera, uno por obligación con valor aplicado ---
        var prestamos = terminacion.Deductions
            .Where(d => !d.IsDeleted && d.Status is not SettlementDeductionStatus.Reverted && d.Kind == SettlementDeductionKind.CooperativeLoan && d.AppliedAmount > 0m && d.LoanPortfolioId is not null)
            .ToList();
        string? cuentaDelRecaudo = null;
        if (prestamos.Count > 0)
        {
            cuentaDelRecaudo = await CuentaDebitoDelDescuentoAsync(empleado, ct);
            if (cuentaDelRecaudo is null) return Result.Failure(SettlementErrors.ConceptAccountsMissing([WellKnownConceptCodes.LoanDeduction]));
        }
        foreach (var d in prestamos)
        {
            var credito = await db.LoanPortfolios.FirstOrDefaultAsync(l => l.Id == d.LoanPortfolioId, ct);
            if (credito is null)
                return Result.Failure(new Error("Payroll.Settlement.PortfolioUnavailable", $"El crédito del descuento «{d.Description}» ya no existe en Cartera. Recalcule la liquidación antes de aprobar."));

            var pago = await recaudo.AplicarAsync(new ProcessPaymentCommand
            {
                PortfolioPublicId = credito.PublicId,
                Amount = d.AppliedAmount,
                PaymentDate = fecha,
                PaymentMethod = FormaDePagoNomina,
                Reference = $"Liquidación definitiva {referencia}",
                CashAccountCode = cuentaDelRecaudo,
            }, ct);
            if (pago.IsFailure)
                return Result.Failure(new Error(pago.Error.Code, $"Cartera no pudo aplicar el descuento «{d.Description}» ({d.AppliedAmount:N0}): {pago.Error.Message}"));

            var saldoQueQueda = await db.LoanPortfolios.AsNoTracking().Where(l => l.Id == credito.Id).Select(l => l.CurrentBalance).FirstOrDefaultAsync(ct);
            d.CarteraTransactionPublicId = pago.Value.TransactionPublicId;
            d.RemainingBalanceAfter = saldoQueQueda;
            pagos.Add(new PortfolioPaymentDto(d.PublicId, pago.Value.TransactionPublicId, d.AppliedAmount, saldoQueQueda, d.Description));
        }

        foreach (var d in terminacion.Deductions.Where(d => !d.IsDeleted && d.Status is not SettlementDeductionStatus.Reverted))
        {
            d.Status = SettlementDeductionStatus.Applied;
            d.UpdatedAt = ahora;
            d.UpdatedBy = quien;
        }

        // --- la ficha se cierra aquí, no al registrar (FR-020) ---
        empleado.Status = -1;
        empleado.TerminationDate = fecha.ToDateTime(TimeOnly.MinValue);
        empleado.TerminationCause = terminacion.TerminationReason?.Name ?? empleado.TerminationCause;
        empleado.UpdatedAt = ahora;
        empleado.UpdatedBy = quien;
        if (persona is not null)
        {
            persona.IsEmployee = false;
            persona.UpdatedAt = ahora;
            persona.UpdatedBy = quien;
        }

        // --- las vacaciones pagadas en la definitiva quedan como movimiento, para que el saldo cierre en cero ---
        var fila = filas.FirstOrDefault();
        var vacaciones = fila?.Lines.FirstOrDefault(l => l.ConceptCode.Equals(WellKnownConceptCodes.VacationCompensation, StringComparison.OrdinalIgnoreCase)
                                                        || l.ConceptCode.Equals(WellKnownConceptCodes.VacationPayout, StringComparison.OrdinalIgnoreCase));
        if (vacaciones is { Quantity: > 0m })
        {
            db.VacationMovements.Add(new VacationMovement
            {
                EmployeeId = empleado.Id,
                Kind = VacationMovementKind.SettlementPayout,
                StartDate = fecha,
                EndDate = fecha,
                BusinessDays = vacaciones.Quantity.Value,
                CalendarDays = 0,
                WeekPolicyUsed = string.Empty,
                Status = VacationMovementStatus.Liquidated,
                PayrollRunId = corrida.Id,
                Notes = $"Vacaciones pendientes pagadas en la liquidación definitiva ({referencia}).",
                CreatedAt = ahora,
                CreatedBy = quien,
            });
        }

        terminacion.Status = TerminationStatus.Settled;
        terminacion.UpdatedAt = ahora;
        terminacion.UpdatedBy = quien;

        // --- la ordinaria del período donde cae el retiro ya no lo incluye (D-29): su borrador, si lo hay, se recalcula ---
        var fechaDt = fecha.ToDateTime(TimeOnly.MinValue);
        var periodosDelRetiro = await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayrollPlanId == empleado.PayrollPlanId && p.Status == PayPeriodStatus.Calculated && p.StartDate <= fechaDt && p.EndDate >= fechaDt)
            .Select(p => p.Id)
            .ToListAsync(ct);
        foreach (var periodoId in periodosDelRetiro)
            await staleMarker.MarkStaleAsync(periodoId, $"Se aprobó la liquidación definitiva ({referencia}) del empleado retirado el {fecha:dd/MM/yyyy}: ya no entra a la nómina ordinaria de este período (D-29).", ct);

        return Result.Success();
    }

    /// <summary>
    /// La cuenta con la que Cartera recibe el recaudo: la cuenta <b>débito</b> parametrizada para
    /// <c>DESC_CARTERA</c> (la del pasivo con el empleado que el descuento reduce), por el centro de
    /// costo de la ficha o la fila por defecto. Sin ella no hay recaudo, y la aprobación lo dice.
    /// </summary>
    private async Task<string?> CuentaDebitoDelDescuentoAsync(Employee empleado, CancellationToken ct)
    {
        var filas = await db.PayrollConceptDefinitionAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.ConceptCode == WellKnownConceptCodes.LoanDeduction)
            .Select(a => new { a.CostCenterId, a.DebitAccountId })
            .ToListAsync(ct);
        if (filas.Count == 0) return null;
        int? centro = null;
        if (!string.IsNullOrWhiteSpace(empleado.CostCenterId))
            centro = await db.CostCenters.AsNoTracking().Where(c => c.LegacyCode == empleado.CostCenterId).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        var fila = filas.FirstOrDefault(f => centro is not null && f.CostCenterId == centro) ?? filas.FirstOrDefault(f => f.CostCenterId == null) ?? filas[0];
        return await db.ChartOfAccounts.AsNoTracking().Where(c => c.Id == fila.DebitAccountId).Select(c => c.Code).FirstOrDefaultAsync(ct);
    }

    /// <summary>El PDF para firma queda adjunto a la terminación; si no se puede, la aprobación sigue y el documento se genera al pedirlo.</summary>
    private async Task<Guid?> GuardarDocumentoAsync(Guid runPublicId, EmploymentTermination terminacion, string numero, CancellationToken ct)
    {
        try
        {
            var modelo = await new SettlementDocumentModelBuilder(db, tenant, clock).BuildAsync(runPublicId, ct);
            if (modelo.IsFailure)
            {
                logger.LogWarning("No se armó el documento de la definitiva {Run} para adjuntarlo: {Error}", runPublicId, modelo.Error.Message);
                return null;
            }
            var pdf = renderer.Render(modelo.Value);
            var nombre = $"liquidacion-definitiva-{modelo.Value.EmployeeDocument}-{modelo.Value.TerminationDate:yyyyMMdd}.pdf";
            var subida = await sender.Send(new UploadAttachmentCommand(nameof(EmploymentTermination), terminacion.PublicId, nombre, "application/pdf", pdf), ct);
            if (subida.IsFailure)
            {
                logger.LogWarning("El documento de la definitiva {Run} no se pudo adjuntar: {Error}", runPublicId, subida.Error.Message);
                return null;
            }
            terminacion.SettlementDocumentAttachmentPublicId = subida.Value;
            await db.SaveChangesAsync(ct);
            return subida.Value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "El documento de la definitiva {Run} ({Numero}) no se pudo adjuntar; se genera al pedirlo.", runPublicId, numero);
            return null;
        }
    }
}
