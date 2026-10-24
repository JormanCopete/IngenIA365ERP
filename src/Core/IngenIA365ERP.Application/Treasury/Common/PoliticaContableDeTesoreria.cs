using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// Cambia <c>Contabilidad.FaltaDeRegla</c> de Tesorería (feature 015, US8, T0262; FR-221; <c>PUT /api/treasury/accounting-policy</c>, permiso
/// <c>Treasury.Accounting.PolicyChange</c>), como el de Recaudo; la alerta diaria la deja <c>tesoreria.vigilancia</c>. (nuevo)
/// </summary>
public sealed record SetTreasuryAccountingPolicyCommand(string Policy, string Reason, DateOnly? ValidFrom = null)
    : IRequest<Result<PoliticaContableDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetTreasuryAccountingPolicyCommandValidator : ValidadorConMotivo<SetTreasuryAccountingPolicyCommand>
{
    public SetTreasuryAccountingPolicyCommandValidator() =>
        RuleFor(x => x.Policy).Must(p => p is "Bloquear" or "Advertir").WithMessage("La política es «Bloquear» o «Advertir».");
}

public sealed class SetTreasuryAccountingPolicyCommandHandler(ISender sender, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<SetTreasuryAccountingPolicyCommand, Result<PoliticaContableDto>>
{
    public Task<Result<PoliticaContableDto>> Handle(SetTreasuryAccountingPolicyCommand request, CancellationToken ct) =>
        CambioDePoliticaContable.CambiarAsync(sender, alertas, reloj, ParametrosDeRecaudoYTesoreria.ModuloTesoreria, "Tesorería",
            TiposDeAlerta.TesoreriaPoliticaContableEnAdvertir, "Treasury", request.Policy, request.Reason, request.ValidFrom, ct);
}
