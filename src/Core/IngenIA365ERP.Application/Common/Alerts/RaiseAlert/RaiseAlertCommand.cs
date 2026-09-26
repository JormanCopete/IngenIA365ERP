using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Common.Alerts.RaiseAlert;

/// <summary>
/// Levanta una alerta (feature 012, T39, T093; FR-022). <b>Sin ruta</b>: lo envían los procesos
/// (<c>ProgramadorDeTareas</c>, despachadores, revisiones) por <c>ISender</c> dentro de <c>IEjecutorEnCooperativa</c>,
/// para que pase por validación y auditoría; lo vigila <c>LosComandosDeConsumoNoTienenRuta</c>. Un comando del módulo
/// que detecta la condición dentro de su propia unidad de trabajo puede usar <see cref="IAlertas"/> directamente.
/// </summary>
public sealed record RaiseAlertCommand(AlertaALevantar Alerta) : IRequest<Result<AlertaLevantada>>;

/// <summary>
/// La forma: tipo, asunto (200) y cuerpo (2000) presentes; la condición (<c>DedupKey</c>, 200) o la entidad para
/// derivarla. Que el tipo sea del catálogo lo responde el handler (<c>Alerts.Type.NotFound</c>).
/// </summary>
public sealed class RaiseAlertCommandValidator : AbstractValidator<RaiseAlertCommand>
{
    public RaiseAlertCommandValidator()
    {
        RuleFor(x => x.Alerta).NotNull();
        RuleFor(x => x.Alerta.TypeCode).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Alerta.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Alerta.Body).NotEmpty().MaximumLength(2000).WithMessage("Decí qué pasó y qué hacer (hasta 2000 caracteres).");
        RuleFor(x => x.Alerta.EntityType).MaximumLength(60);
        RuleFor(x => x.Alerta.DedupKey).MaximumLength(200);
        RuleFor(x => x.Alerta)
            .Must(a => !string.IsNullOrWhiteSpace(a.DedupKey) || a.EntityPublicId is not null)
            .WithMessage("Indicá la condición (dedupKey) o la entidad de la que se deriva.");
    }
}

public sealed class RaiseAlertCommandHandler(IAlertas alertas) : IRequestHandler<RaiseAlertCommand, Result<AlertaLevantada>>
{
    public Task<Result<AlertaLevantada>> Handle(RaiseAlertCommand request, CancellationToken ct) =>
        alertas.LevantarAsync(request.Alerta, ct);
}
