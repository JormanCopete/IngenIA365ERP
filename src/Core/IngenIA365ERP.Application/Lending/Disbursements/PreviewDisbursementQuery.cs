using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// La vista previa del desembolso (feature 013, US3, T0443; api.md §9.1, <c>POST /api/lending/applications/{id}/disbursement-preview</c>,
/// <c>Lending.Disbursements.Execute</c>, sin clave): la misma evaluación que el comando (<see cref="RequisitosDelDesembolso"/>) sin escribir nada
/// —usura del día, aprobación, garantías pendientes, recogidas con lo que cuesta cancelarlas y si son reestructuración, neto, plan propuesto
/// (D12), validación previa contable, segregación— y la huella <c>previewSha256</c> que el comando compara (<c>expectedPreviewSha256</c>). Lo que
/// impediría desembolsar sale en <c>blocking</c> con su código; sólo responde error lo que impide evaluar (solicitud inexistente o fuera del
/// alcance, no aprobada, la usura del día sin publicar). Filtra por el alcance de la solicitud (<c>AlcanceDeCartera</c>). (nuevo)
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

public sealed class PreviewDisbursementQueryHandler(RequisitosDelDesembolso requisitos)
    : IRequestHandler<PreviewDisbursementQuery, Result<DisbursementPreviewDto>>
{
    public async Task<Result<DisbursementPreviewDto>> Handle(PreviewDisbursementQuery request, CancellationToken ct)
    {
        // RequisitosDelDesembolso asegura el AlcanceDeCartera de la solicitud (el 404 fuera de él) y lee todo sin seguimiento.
        var evaluados = await requisitos.EvaluarAsync(request.Input, ct);
        if (evaluados.IsFailure) return Result.Failure<DisbursementPreviewDto>(evaluados.Error);
        var r = evaluados.Value;

        var s = r.Simulacion;
        var plan = new DisbursementScheduleDto(s.Plan.Cuotas.Count, s.TasaPactada,
            s.Plan.Cuotas.Select(c => new DisbursementScheduleLineDto(c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, c.Total)).ToList());
        return Result.Success(new DisbursementPreviewDto(
            new DisbursementUsuryDto(s.Usura.EfectivaConCargos, s.Usura.Usura, s.Usura.Pasa, s.Usura.UsuraPublicId),
            new DisbursementApprovalDto(r.Solicitud.ApprovalValidUntil, !r.Solicitud.AprobacionVencida(r.FechaValor)),
            new DisbursementCollateralsDto(r.Pendientes.Count == 0, r.Pendientes),
            r.Recogidas.Select(x => new DisbursementPreviewPayoffDto(x.LoanPublicId, x.Number, x.Liquidacion.ParaCancelar, x.Liquidacion.DiasDeMora,
                x.RequiereReestructuracion)).ToList(),
            r.Tramo,
            r.Bruto,
            r.Neto,
            plan,
            Prevalidacion(r),
            new DisbursementSegregationDto(r.ConflictoDeSegregacion),
            r.Bloqueos.Select(b => b.Code).ToList(),
            r.Avisos,
            r.Huella));
    }

    /// <summary>Lo que respondió la validación previa contable, como lo muestran la vista previa y el resultado del desembolso.</summary>
    public static PrevalidationDto Prevalidacion(RequisitosEvaluados r)
    {
        var p = r.Prevalidacion;
        var aplica = p.Outcome != PrevalidationOutcome.NotApplicable;
        var respondio = p.Outcome != PrevalidationOutcome.NoResponse;
        bool? contabilizable = aplica && respondio ? p.Outcome == PrevalidationOutcome.Postable : null;
        return new PrevalidationDto(aplica, contabilizable, respondio, p.Errors.Cast<object>().ToList(), p.Warnings.Cast<object>().ToList());
    }
}
