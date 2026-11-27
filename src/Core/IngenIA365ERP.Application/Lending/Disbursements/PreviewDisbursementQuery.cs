using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// La vista previa del desembolso (feature 013, US3, T0443; api.md §9.1, <c>POST /api/lending/applications/{id}/disbursement-preview</c>,
/// <c>Lending.Disbursements.Execute</c>, sin clave): la misma evaluación que el comando (<see cref="RequisitosDelDesembolso"/>) sin escribir nada
/// —usura del día, aprobación, garantías pendientes, recogidas con lo que cuesta cancelarlas y si son reestructuración, neto, plan propuesto
/// (D12), validación previa contable, segregación— y la huella <c>previewSha256</c> que el comando compara (<c>expectedPreviewSha256</c>). Lo que
/// impediría desembolsar sale en <c>blocking</c> con su código; sólo responde error lo que impide evaluar (solicitud inexistente o fuera del
/// alcance, no aprobada, la usura del día sin publicar). Filtra por el alcance de la solicitud (<c>AlcanceDeCartera</c>).
/// <para>
/// 2026-11-27 (feature 028, T034; FR-013, FR-015; api.md §3): cada recogida trae lo proyectado en la solicitud, lo de ahora, lo que se aplica, la
/// diferencia por concepto con su causa, si se omite y la cuota causada, y <c>payoffDifferences</c> dice si confirmar exige reconocerlas. Como la
/// liquidación del crédito, audita <c>Lending.Payoff.Viewed</c> por cada crédito liquidado.
/// </para>
/// (nuevo)
/// </summary>
public sealed record PreviewDisbursementQuery(DisbursementInput Input) : IRequest<Result<DisbursementPreviewDto>>;

public sealed class PreviewDisbursementQueryValidator : AbstractValidator<PreviewDisbursementQuery>
{
    public PreviewDisbursementQueryValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ApplicationPublicId).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class PreviewDisbursementQueryHandler(RequisitosDelDesembolso requisitos, LendingAuditEmitter auditoria)
    : IRequestHandler<PreviewDisbursementQuery, Result<DisbursementPreviewDto>>
{
    public async Task<Result<DisbursementPreviewDto>> Handle(PreviewDisbursementQuery request, CancellationToken ct)
    {
        // RequisitosDelDesembolso asegura el AlcanceDeCartera de la solicitud (el 404 fuera de él) y lee todo sin seguimiento.
        var evaluados = await requisitos.EvaluarAsync(request.Input, ct);
        if (evaluados.IsFailure) return Result.Failure<DisbursementPreviewDto>(evaluados.Error);
        var r = evaluados.Value;

        // 2026-11-27 (feature 028, T034): ver lo que cuesta recoger un crédito es ver su liquidación (como GetLoanSettlementQuery).
        foreach (var x in r.Recogidas)
            if (x.Liquidacion.Detalle is { } l)
                await auditoria.EmitirLiquidacionVistaAsync(x.LoanPublicId, l.Fecha, l.InformacionHasta, l.Huella, ct);

        var s = r.Simulacion;
        var plan = new DisbursementScheduleDto(s.Plan.Cuotas.Count, s.TasaPactada,
            s.Plan.Cuotas.Select(c => new DisbursementScheduleLineDto(c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, c.Total)
                { Administration = c.Administracion }).ToList());
        var porReconocer = r.Recogidas.Where(x => x.PorReconocer).ToList();
        return Result.Success(new DisbursementPreviewDto(
            new DisbursementUsuryDto(s.Usura.EfectivaConCargos, s.Usura.Usura, s.Usura.Pasa, s.Usura.UsuraPublicId),
            new DisbursementApprovalDto(r.Solicitud.ApprovalValidUntil, !r.Solicitud.AprobacionVencida(r.FechaValor)),
            new DisbursementCollateralsDto(r.Pendientes.Count == 0, r.Pendientes),
            r.Recogidas.Select(Recogida).ToList(),
            r.Tramo,
            r.Bruto,
            r.Neto,
            plan,
            Prevalidacion(r),
            new DisbursementSegregationDto(r.ConflictoDeSegregacion),
            r.Bloqueos.Select(b => b.Code).ToList(),
            r.Avisos,
            r.Huella)
        {
            PayoffDifferences = new DisbursementPayoffDifferencesDto(porReconocer.Count, porReconocer.Sum(x => x.Diferencia.Total), r.RequiereReconocimiento,
                r.Recogidas.Any(x => x.SobreLaTolerancia)),
        });
    }

    /// <summary>Una recogida como la muestra la vista previa (api.md §3).</summary>
    public static DisbursementPreviewPayoffDto Recogida(RecogidaEvaluada x)
    {
        ArgumentNullException.ThrowIfNull(x);
        return new DisbursementPreviewPayoffDto(x.LoanPublicId, x.Number, x.Liquidacion.ParaCancelar, x.Liquidacion.DiasDeMora, x.RequiereReestructuracion)
        {
            ProductPublicId = x.LoanPublicId,
            Mode = x.Total ? nameof(PayoffMode.Total) : nameof(PayoffMode.Partial),
            RequestedMode = x.ModoPedido.ToString(),
            Projected = x.ConProyeccion ? x.Proyectado : null,
            Current = x.Actual,
            ToApply = x.AAplicar,
            Difference = x.ConProyeccion || x.Omitida ? x.Diferencia.Total : null,
            Causes = RequisitosDelDesembolso.Causas(x.Diferencia.Causa),
            Detail = Detalle(x.Diferencia.PorConcepto),
            Omitted = x.Omitida,
            AboveTolerance = x.SobreLaTolerancia,
            InterestMode = x.ModoDeIntereses,
            CurrentInstallment = x.Liquidacion.Detalle?.CuotaCausada is { } c
                ? new DisbursementPayoffInstallmentDto(c.Numero, c.Vencimiento, c.Valor, nameof(InstallmentTreatment.Include), null)
                : null,
            Warnings = x.Avisos,
        };
    }

    /// <summary>La diferencia por concepto, sólo los que cambiaron, con los nombres de <c>breakdown</c> (api.md §1).</summary>
    public static IReadOnlyDictionary<string, decimal> Detalle(DesgloseDeRecogida d)
    {
        ArgumentNullException.ThrowIfNull(d);
        var conceptos = new (string Nombre, decimal Valor)[]
        {
            ("principal", d.Capital), ("currentInterest", d.InteresCorriente), ("interestReturned", d.InteresDevuelto),
            ("interestCharged", d.InteresCobradoPorProporcionales), ("defaultInterest", d.Mora), ("insurance", d.Seguro), ("administration", d.Administracion),
            ("otherCharges", d.OtrosCargos), ("advance", d.Anticipo), ("creditBalance", d.SaldoAFavor),
        };
        return conceptos.Where(c => c.Valor != 0m).ToDictionary(c => c.Nombre, c => c.Valor);
    }

    /// <summary>
    /// Lo que respondió la validación previa contable, como lo muestran la vista previa y el resultado del desembolso. 2026-11-27 (feature 028, T037): con la
    /// de las recogidas, si la hubo: aplica si alguna aplica, respondió si respondieron todas, se puede contabilizar si todas, y sus hallazgos juntos.
    /// </summary>
    public static PrevalidationDto Prevalidacion(RequisitosEvaluados r)
    {
        var resultados = new[] { r.Prevalidacion, r.PrevalidacionDeRecogidas }.OfType<ResultadoDeValidacionPreviaDeCartera>()
            .Where(p => p.Outcome != PrevalidationOutcome.NotApplicable).ToList();
        var aplica = resultados.Count > 0;
        var respondio = resultados.All(p => p.Outcome != PrevalidationOutcome.NoResponse);
        bool? contabilizable = aplica && respondio ? resultados.All(p => p.Outcome == PrevalidationOutcome.Postable) : null;
        return new PrevalidationDto(aplica, contabilizable, respondio, resultados.SelectMany(p => p.Errors).Cast<object>().ToList(),
            resultados.SelectMany(p => p.Warnings).Cast<object>().ToList());
    }
}
