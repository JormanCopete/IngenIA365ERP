using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Sales.Shipments;

/// <summary>
/// Levanta <c>Inventario.RemisionSinFacturar</c> por cada remisión que pasó <c>Ventas.RemisionDiasMaximosSinFacturar</c> con saldo pendiente
/// de facturar (feature 012, I6, T889; FR-022, FR-052; contracts/api.md §18.4). <b>Sin ruta</b>: lo corre la tarea programada
/// <see cref="TareaDeRemisionesSinFacturar"/> con el actor «Proceso de integración». Una sola vez por remisión: la condición es
/// <c>RemisionSinFacturar:{shipmentPublicId}</c> y una remisión que ya la levantó (pendiente o atendida) no la vuelve a levantar. Responde
/// cuántas levantó. (nuevo)
/// </summary>
public sealed record RaiseUnbilledShipmentAlertsCommand : IRequest<Result<int>>;

public sealed class RaiseUnbilledShipmentAlertsCommandHandler(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IAlertas alertas,
    IDateTimeService reloj)
    : IRequestHandler<RaiseUnbilledShipmentAlertsCommand, Result<int>>
{
    /// <summary>La condición de la alerta de una remisión.</summary>
    public static string Condicion(Guid remision) => $"RemisionSinFacturar:{remision}";

    public async Task<Result<int>> Handle(RaiseUnbilledShipmentAlertsCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.LeerComoAsync<int>(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasRemisionDiasMaximosSinFacturar, hoy, ct: ct);
        if (dias.IsFailure) return Result.Failure<int>(dias.Error);
        var limite = hoy.AddDays(-Math.Max(0, dias.Value));

        var remisiones = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.Class == DocumentClass.Shipment && d.Status == DocumentStatus.Confirmed && d.OperationDate < limite)
            .OrderBy(d => d.OperationDate).ThenBy(d => d.Id).ToListAsync(ct);
        if (remisiones.Count == 0) return Result.Success(0);

        var condiciones = remisiones.Select(r => Condicion(r.PublicId)).ToList();
        var yaLevantadas = (await db.Alerts.AsNoTracking().Where(a => condiciones.Contains(a.DedupKey)).Select(a => a.DedupKey).ToListAsync(ct)).ToHashSet();
        var pendiente = await new VinculosDelCiclo(db, reloj).PendientePorFacturarAsync(remisiones.SelectMany(r => r.Lines).Where(l => !l.IsDeleted).ToList(), 0, ct);
        var bodegas = await db.Warehouses.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.PublicId, ct);

        var levantadas = 0;
        foreach (var r in remisiones)
        {
            var condicion = Condicion(r.PublicId);
            if (yaLevantadas.Contains(condicion)) continue;
            if (!r.Lines.Any(l => !l.IsDeleted && pendiente.GetValueOrDefault(l.Id) > 0m)) continue;
            var numero = VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number) ?? r.PublicId.ToString();
            var levantada = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RemisionSinFacturar,
                $"Remisión sin facturar: {numero}",
                $"La remisión {numero} del {r.OperationDate:dd/MM/yyyy} lleva más de {dias.Value} días sin facturarse por completo.",
                "InventoryDocument", r.PublicId, r.WarehouseId is int b && bodegas.TryGetValue(b, out var bodega) ? bodega : null, null, condicion), ct);
            if (levantada.IsSuccess) levantadas++;
        }
        return Result.Success(levantadas);
    }
}

/// <summary>
/// La tarea programada <c>inventario.remisiones</c> (feature 012, I6, T889): una vez al día corre <see cref="RaiseUnbilledShipmentAlertsCommand"/>
/// en cada cooperativa. Antes de que el despliegue llegue a I6 no corre (no hay remisiones). Singleton. (nuevo)
/// </summary>
public sealed class TareaDeRemisionesSinFacturar(EntregaDelComercio entrega = CatalogoDeParametros.EntregaVigente) : ITareaProgramada
{
    public const string NombreDeLaTarea = "inventario.remisiones";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        entrega >= EntregaDelComercio.I6
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<ISender>().Send(new RaiseUnbilledShipmentAlertsCommand(), ct);
}
