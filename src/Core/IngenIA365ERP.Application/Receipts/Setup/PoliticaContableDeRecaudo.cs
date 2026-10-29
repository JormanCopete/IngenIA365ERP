using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Receipts.Setup;

/// <summary>
/// Cambia <c>Contabilidad.FaltaDeRegla</c> de Recaudo (feature 015, US8, T0262; FR-221; <c>PUT /api/receipts/accounting-policy</c>, permiso
/// <c>Receipts.Accounting.PolicyChange</c>): «bloquear» o «advertir», con motivo. La vigencia la escribe el único escritor de parámetros
/// (<c>AddParameterVersionCommand</c>), el comando lo audita con su motivo y pasar a «advertir» alerta de una vez; mientras siga así, la tarea
/// <c>recaudo.antiguedad</c> alerta cada día. (nuevo)
/// </summary>
public sealed record SetReceiptsAccountingPolicyCommand(string Policy, string Reason, DateOnly? ValidFrom = null)
    : IRequest<Result<PoliticaContableDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetReceiptsAccountingPolicyCommandValidator : ValidadorConMotivo<SetReceiptsAccountingPolicyCommand>
{
    public SetReceiptsAccountingPolicyCommandValidator() =>
        RuleFor(x => x.Policy).Must(p => p is "Bloquear" or "Advertir").WithMessage("La política es «Bloquear» o «Advertir».");
}

public sealed class SetReceiptsAccountingPolicyCommandHandler(ISender sender, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<SetReceiptsAccountingPolicyCommand, Result<PoliticaContableDto>>
{
    public Task<Result<PoliticaContableDto>> Handle(SetReceiptsAccountingPolicyCommand request, CancellationToken ct) =>
        CambioDePoliticaContable.CambiarAsync(sender, alertas, reloj, ParametrosDeRecaudoYTesoreria.ModuloRecaudo, "Recaudo",
            TiposDeAlerta.RecaudoPoliticaContableEnAdvertir, "Receipts", request.Policy, request.Reason, request.ValidFrom, ct);
}
