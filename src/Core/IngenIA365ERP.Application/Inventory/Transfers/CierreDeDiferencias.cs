using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>
/// El enganche de cierre de las diferencias de traslado (feature 012, US10, T372; data-model §7.1): lo que pasa con una diferencia
/// cuando se decide el documento que la resuelve. Lo llaman la fuente de aprobación de la diferencia
/// (<see cref="FuenteDeAprobacionDeDiferencia"/>, en la transacción del aprobador) y la resolución sin niveles
/// (<c>ResolveTransferDiscrepancyCommand</c>):
/// <list type="bullet">
/// <item><b>aprobada</b>: el documento se confirma por el flujo canónico —existencia, período y numeración se vuelven a
/// comprobar— y la diferencia queda resuelta por la cantidad aprobada (<see cref="TransferDiscrepancy.Resolver"/>); el sobrante toma
/// el costo con que entró;</item>
/// <item><b>rechazada o retirada</b>: el documento vuelve a borrador y se descarta con el motivo (suelta sus vínculos con el
/// despacho) y la diferencia vuelve a pendiente (<see cref="TransferDiscrepancy.VolverAPendiente"/>).</item>
/// </list>
/// Ninguno guarda: guarda quien lo llama. <see cref="ConfirmacionDeDocumento"/> se resuelve al usarla (depende del motor de
/// aprobaciones, que depende de las fuentes). (nuevo)
/// </summary>
public sealed class CierreDeDiferencias(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj, IServiceProvider servicios)
{
    /// <summary>El documento que resuelve la diferencia se aprobó: confirmarlo y resolverla.</summary>
    public async Task<Result<ConfirmationResultDto>> AlAprobarAsync(TransferDiscrepancy diferencia, CancellationToken ct)
    {
        if (diferencia.ResolutionDocumentId is not int documentoId || diferencia.ResolutionQuantityBase is not decimal cantidad)
            return Result.Failure<ConfirmationResultDto>(ErroresDeTraslados.DiscrepancyNotPending(diferencia.Estado));
        var documento = await db.InventoryDocuments.Include(d => d.Lines).FirstAsync(d => d.Id == documentoId, ct);

        var confirmacion = servicios.GetRequiredService<ConfirmacionDeDocumento>();
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(documento.PublicId, null, PorAprobacion: true), ct);
        if (confirmada.IsFailure) return confirmada;

        if (diferencia.Kind == TransferDiscrepancyKind.Surplus)
            diferencia.UnitCost = documento.Lines.Where(l => !l.IsDeleted).Select(l => l.UnitCost).FirstOrDefault(c => c is not null);
        diferencia.Resolver(cantidad, reloj.UtcNow);
        return confirmada;
    }

    /// <summary>La aprobación se rechazó o se retiró: descartar el documento y devolver la diferencia a pendiente.</summary>
    public async Task<Result> AlRechazarAsync(TransferDiscrepancy diferencia, string motivo, CancellationToken ct)
    {
        if (!diferencia.EnAprobacion) return Result.Success();
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;

        if (diferencia.ResolutionDocumentId is int documentoId
            && await db.InventoryDocuments.FirstOrDefaultAsync(d => d.Id == documentoId, ct) is { } documento)
        {
            if (documento.Status == DocumentStatus.PendingApproval) documento.DevolverABorrador();
            if (documento.Status == DocumentStatus.Draft)
            {
                documento.Descartar(actor.UserId ?? documento.CreatedByUserId, ahora, motivo);
                var vinculos = await db.DocumentLinks.Include(l => l.LineLinks).Where(l => l.TargetDocumentId == documento.Id).ToListAsync(ct);
                foreach (var vinculo in vinculos)
                {
                    vinculo.IsDeleted = true;
                    vinculo.DeletedAt = ahora;
                    vinculo.DeletedBy = actor.Name;
                    foreach (var deLinea in vinculo.LineLinks.Where(x => !x.IsDeleted))
                    {
                        deLinea.IsDeleted = true;
                        deLinea.DeletedAt = ahora;
                        deLinea.DeletedBy = actor.Name;
                    }
                }
            }
        }
        diferencia.VolverAPendiente();
        return Result.Success();
    }

    /// <summary>La diferencia que <paramref name="documentoId"/> resuelve (en aprobación), si hay.</summary>
    public Task<TransferDiscrepancy?> DeDocumentoAsync(int documentoId, CancellationToken ct) =>
        db.TransferDiscrepancies.FirstOrDefaultAsync(d => d.ResolutionDocumentId == documentoId && d.ResolvedAt == null, ct);

    /// <summary>Una diferencia por su <c>PublicId</c>, seguida; nula si no existe.</summary>
    public Task<TransferDiscrepancy?> BuscarAsync(Guid publicId, CancellationToken ct) =>
        db.TransferDiscrepancies.FirstOrDefaultAsync(d => d.PublicId == publicId, ct);

    /// <summary>El código de error si no existe.</summary>
    public static Error NoExiste() => ErroresDeTraslados.DiscrepancyNotFound();
}
