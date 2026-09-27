using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// La vista previa de un lote manual (feature 012, T499; FR-077; api.md §26.4, <c>POST /api/accounting/inventory/batches/preview</c>,
/// <c>Accounting.InventoryBatches.View</c>). Toma las entregas <c>InBatch</c> sin lote del alcance, en orden de emisión, y delega en
/// <see cref="IContabilidadParaInventario.PrevisualizarLoteAsync"/>, que arma los comprobantes propuestos y el
/// <c>cutoffMessagePublicId</c>. No numera ni guarda. Sin la implementación contable registrada responde
/// <c>Integration.Destination.Unavailable</c>.
/// </summary>
public sealed record PreviewIntegrationBatchQuery(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string>? DocumentTypeCodes,
    string? ScheduleKey,
    Guid? BranchPublicId) : IRequest<Result<VistaPreviaDeLoteDto>>
{
    public AlcanceDeLote Alcance => new(From, To, DocumentTypeCodes, ScheduleKey, BranchPublicId);
}

public sealed class PreviewIntegrationBatchQueryValidator : AbstractValidator<PreviewIntegrationBatchQuery>
{
    public PreviewIntegrationBatchQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.ScheduleKey).MaximumLength(60);
        RuleForEach(x => x.DocumentTypeCodes).NotEmpty().MaximumLength(10);
    }
}

public sealed class PreviewIntegrationBatchQueryHandler(IApplicationDbContext db, IServiceProvider servicios)
    : IRequestHandler<PreviewIntegrationBatchQuery, Result<VistaPreviaDeLoteDto>>
{
    public async Task<Result<VistaPreviaDeLoteDto>> Handle(PreviewIntegrationBatchQuery request, CancellationToken ct)
    {
        var contabilidad = servicios.GetService<IContabilidadParaInventario>();
        if (contabilidad is null)
            return Result.Failure<VistaPreviaDeLoteDto>(ErroresDeIntegracion.DestinoNoDisponible(IntegrationDestinations.Accounting));

        var mensajes = await request.Alcance.Entregas(db).AsNoTracking().Select(d => d.Message!.PublicId).ToListAsync(ct);
        return await contabilidad.PrevisualizarLoteAsync(mensajes, ct);
    }
}
