using FluentValidation;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Treasury.Contabilizacion;

/// <summary>
/// Contabiliza una <b>unidad</b> de hechos de Tesorería (feature 015, T0257; contabilidad.md §4): un comprobante del tipo del hecho principal por la matriz
/// <c>ACC_TreasuryPostingRules</c>; el rol <c>Banco</c> por el vínculo de la cuenta propia y el <c>PuenteDeGiroDelOrigen</c> por origen y clase de orden, para que la
/// contadora lo lleve a la misma cuenta que el puente del origen. Siempre por <c>AccountingPoster</c>. <b>Sin ruta ni permisos</b>. (nuevo)
/// </summary>
public sealed record PostTreasuryFactsCommand(IReadOnlyList<Guid> MessagePublicIds, Guid? BatchPublicId)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class PostTreasuryFactsCommandValidator : AbstractValidator<PostTreasuryFactsCommand>
{
    public PostTreasuryFactsCommandValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("La unidad no trae hechos.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty).When(x => x.BatchPublicId is not null);
    }
}

public sealed class PostTreasuryFactsCommandHandler(ContabilizacionDeModulo contabilizacion) : IRequestHandler<PostTreasuryFactsCommand, Result<ResultadoDeConsumo>>
{
    public Task<Result<ResultadoDeConsumo>> Handle(PostTreasuryFactsCommand request, CancellationToken ct) =>
        contabilizacion.UnidadAsync(MatricesDeRecaudoYTesoreria.Tesoreria, request.MessagePublicIds, request.BatchPublicId, ct);
}
