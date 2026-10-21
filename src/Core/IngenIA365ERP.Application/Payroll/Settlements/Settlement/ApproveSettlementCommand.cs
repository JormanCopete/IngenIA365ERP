using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Core;
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
/// <item>feature 013 (T0159, R104, T92): en el gancho <c>antesDeContabilizar</c>, antes del comprobante, vuelve a
/// consultar las obligaciones del empleado en Cartera (<see cref="PropuestaDeDescuentosDeCartera"/>): si el conjunto vivo
/// ya no es el propuesto, <c>Payroll.Settlement.LoanProposalOutdated</c>; con la persona «migrada, sin activar» exige
/// <see cref="ApproveSettlementCommand.AcknowledgeLendingPendingActivation"/>. Después aplica los descuentos por el contrato
/// de obligaciones (<see cref="RecaudoPorNomina"/>, llamado <b>directo</b> y no por <c>ISender</c>; fecha valor = la de
/// aprobación; <c>FullPayoff</c>, <c>CycleInstallment</c> o <c>FreeAmount</c> si se ajustó), <b>todo o nada</b>: un ítem
/// rechazado o con excedente responde <c>Payroll.Settlement.LoanAmountsChanged</c> y se revierte todo. El descuento guarda
/// el comprobante de Cartera y el saldo que quedó. <c>DESC_CARTERA</c> contabiliza en la definitiva contra la cuenta del
/// concepto (enmienda D-08); sin cuentas, <c>Payroll.Settlement.ConceptAccountsMissing</c>. Un conflicto de concurrencia
/// deshace la transacción entera y este comando —reintentable— se repite entero;</item>
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
    bool ConfirmWithoutSegregation = false,
    bool AcknowledgeLendingPendingActivation = false) : IRequest<Result<SettlementApprovedWithPortfolioDto>>, IReintentableAnteConcurrencia;

public sealed class ApproveSettlementCommandValidator : AbstractValidator<ApproveSettlementCommand>
{
    public ApproveSettlementCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ApproveSettlementCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    PropuestaDeDescuentosDeCartera propuesta,
    RecaudoPorNomina recaudo,
    PayrollPolicyReader policies,
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
            () =>
            {
                pagos.Clear();
                return workflow.ApproveAsync(peticion,
                    (corrida, _, token) => AplicarEnCarteraAsync(corrida, terminacion, persona, request.AcknowledgeLendingPendingActivation, pagos, token),
                    (corrida, filas, token) => CerrarFichaAsync(corrida, filas, terminacion, empleado, persona, token), ct);
            }, ct);
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

    /// <summary>
    /// Feature 013 (T0159): antes del comprobante, vuelve a consultar Cartera y aplica los descuentos de la definitiva por el
    /// contrato de obligaciones, todo o nada. Un fallo aquí revierte todo, recaudos incluidos.
    /// </summary>
    private async Task<Result> AplicarEnCarteraAsync(PayrollRun corrida, EmploymentTermination terminacion, Domain.Entities.Core.Person? persona,
        bool reconocido, List<PortfolioPaymentDto> pagos, CancellationToken ct)
    {
        var prestamos = terminacion.Deductions
            .Where(d => !d.IsDeleted && d.Status is not SettlementDeductionStatus.Reverted && d.Kind == SettlementDeductionKind.CooperativeLoan)
            .ToList();
        if (persona is null) return prestamos.Count == 0 ? Result.Success() : Result.Failure(SettlementErrors.EmployeeNotFound);

        // --- el conjunto vivo de hoy contra lo propuesto (R104) ---
        var politicas = await policies.ReadAsync(terminacion.TerminationDate, ct);
        PropuestaDeDescuentosDeCartera.PropuestaDeCartera vivas;
        try
        {
            vivas = await propuesta.VivasAsync(persona.PublicId, politicas.DeduccionAlRetiroModo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cartera no respondió al aprobar la definitiva {Run}", corrida.PublicId);
            if (prestamos.Count == 0) return Result.Success();
            return Result.Failure(new Error(SettlementErrors.PortfolioUnavailableCode,
                "Cartera no respondió al confirmar los descuentos de la liquidación. Intente aprobar de nuevo en unos minutos."));
        }

        if (vivas.MigradaSinActivar)
        {
            if (!reconocido)
                return Result.Failure(SettlementErrors.LendingPendingActivationAckRequired(
                    [NombreDePersona.Completo(persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName)]));
            corrida.LendingActivationAckBy = user.UserName ?? string.Empty;
            corrida.LendingActivationAckAt = clock.UtcNow;
        }

        var propuestas = prestamos.Where(d => d.ObligationPublicId is not null).ToDictionary(d => d.ObligationPublicId!.Value);
        var actuales = vivas.Deudas.Where(d => d.ObligationPublicId is not null).ToDictionary(d => d.ObligationPublicId!.Value);
        if (!propuestas.Keys.ToHashSet().SetEquals(actuales.Keys))
            return Result.Failure(SettlementErrors.LoanProposalOutdated(
                propuestas.Values.Select(d => d.ObligationNumber ?? d.Description).ToList(),
                actuales.Values.Select(d => d.ObligationNumber ?? d.Description).ToList()));

        var conValor = propuestas.Values.Where(d => d.AppliedAmount > 0m).ToList();
        if (conValor.Count == 0) return Result.Success();

        // --- el lote, todo o nada (FR-088) ---
        var hoy = clock.HoyLocal;
        var recibo = new ReferenciaDeRecibo($"Liquidación definitiva v{corrida.Version}", hoy, null, null, "Liquidacion");
        var lote = new LoteDeNomina(corrida.PublicId, recibo, hoy,
            conValor.Select(d => new PagoDeNomina(d.ObligationPublicId!.Value, persona.PublicId, Modalidad(d, politicas.DeduccionAlRetiroModo), d.AppliedAmount)).ToList(),
            []);
        var resultado = await recaudo.AplicarAsync(lote, ct);

        // Feature 013, T0801 (A8 = «Bloquear todo», 2026-10-14): lo que Contabilidad no puede registrar se dice con su propio código.
        var noContabilizables = resultado.Pagos.Where(p => p.NoContabilizable).ToList();
        if (noContabilizables.Count > 0)
            return Result.Failure(SettlementErrors.LoanDeductionNotPostable(noContabilizables
                .Select(p => (propuestas[p.ObligationPublicId].ObligationNumber ?? propuestas[p.ObligationPublicId].Description, p.Respuesta.Message,
                    (object?)p.Respuesta.Data)).ToList()));

        var fallidos = resultado.Pagos.Where(p => !p.Aplicado || p.Excedente > 0m || p.ValorAplicado != p.Enviado).ToList();
        if (fallidos.Count > 0)
        {
            var items = resultado.Pagos.Select(p => new
            {
                obligationPublicId = p.ObligationPublicId,
                obligationNumber = propuestas[p.ObligationPublicId].ObligationNumber,
                sent = p.Enviado,
                result = p.Respuesta.Result.ToString(),
                code = p.Respuesta.Code,
                message = p.Respuesta.Message,
                applied = p.ValorAplicado,
                surplus = p.Excedente,
            }).ToList();
            var detalle = string.Join("; ", fallidos.Select(p =>
                $"{propuestas[p.ObligationPublicId].ObligationNumber ?? propuestas[p.ObligationPublicId].Description}: " +
                (p.Aplicado ? $"Cartera tomó {Domain.Payroll.Calculation.Rules.Fmt.Num(p.ValorAplicado)} de {Domain.Payroll.Calculation.Rules.Fmt.Num(p.Enviado)}" : p.Respuesta.Message)));
            return Result.Failure(SettlementErrors.LoanAmountsChanged(detalle, items));
        }

        foreach (var p in resultado.Pagos)
        {
            var d = propuestas[p.ObligationPublicId];
            var comprobante = p.Respuesta.Value!;
            d.CarteraTransactionPublicId = comprobante.OperationPublicId;
            d.RemainingBalanceAfter = comprobante.BalancesAfter.PayoffAmount;
            pagos.Add(new PortfolioPaymentDto(d.PublicId, comprobante.OperationPublicId, d.AppliedAmount, d.RemainingBalanceAfter, d.Description));
        }
        return Result.Success();
    }

    /// <summary>La modalidad del pago (R104): ajustado → valor libre; si no, según la política con que se propuso.</summary>
    private static ModalidadDePago Modalidad(SettlementDeduction d, DeduccionAlRetiro modo) =>
        d.FueAjustado ? ModalidadDePago.FreeAmount
        : modo == DeduccionAlRetiro.SoloCuotasCausadas ? ModalidadDePago.CycleInstallment
        : ModalidadDePago.FullPayoff;

    /// <summary>Lo propio de la definitiva dentro de la transacción de la aprobación, después del comprobante. Un fallo aquí revierte todo.</summary>
    private async Task<Result> CerrarFichaAsync(PayrollRun corrida, IReadOnlyList<PayrollRunEmployee> filas, EmploymentTermination terminacion,
        Employee empleado, Domain.Entities.Core.Person? persona, CancellationToken ct)
    {
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? string.Empty;
        var fecha = terminacion.TerminationDate;
        var referencia = corrida.AccountingDocument?.Referencia() ?? $"Definitiva v{corrida.Version}";

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
