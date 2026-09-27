using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory;

/// <summary>
/// El adaptador de <see cref="IContabilidadParaInventario"/> (feature 012, T517; FR-014; contracts/contabilidad.md §4.1): lo
/// único de Contabilidad que Inventario conoce. Delega en las cuatro consultas por <see cref="ISender"/>, en proceso y sin HTTP,
/// para que pasen por la validación de siempre. Con él registrado, la validación previa deja de quedar <c>NotApplicable</c> y la
/// vista previa de un lote deja de responder <c>Integration.Destination.Unavailable</c>. (nuevo)
/// </summary>
public sealed class ContabilidadParaInventario(ISender sender, IApplicationDbContext db) : IContabilidadParaInventario
{
    public Task<Result<ResultadoDeContabilizacionDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct) =>
        sender.Send(new EvaluateInventoryPostingQuery(mensajes), ct);

    public Task<Result<IReadOnlyList<ConjuntoDeCuentasDto>>> SaldosDeCuentasMapeadasAsync(DateOnly corte, CancellationToken ct) =>
        sender.Send(new InventoryAccountBalancesQuery(corte), ct);

    public Task<Result<CompletitudDeLaMatrizDto>> CompletitudAsync(DateOnly fecha, CancellationToken ct) =>
        sender.Send(new InventoryRulesCompletenessQuery(fecha), ct);

    public Task<Result<VistaPreviaDeLoteDto>> PrevisualizarLoteAsync(IReadOnlyList<Guid> messagePublicIds, CancellationToken ct) =>
        sender.Send(new PreviewInventoryBatchQuery(messagePublicIds), ct);

    /// <summary>Sin configuración inicial viva la contabilidad no está iniciada (la misma pregunta que <c>Accounting.NotInitialized</c>).</summary>
    public async Task<bool> SinIniciarAsync(CancellationToken ct) =>
        !await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct);
}
