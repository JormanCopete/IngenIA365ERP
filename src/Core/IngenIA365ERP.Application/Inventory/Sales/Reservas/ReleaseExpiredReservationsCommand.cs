using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Sales.Reservas;

/// <summary>
/// Vence las reservas de pedido que pasaron su fecha (feature 012, I6, T889; FR-052; data-model §14; decisiones-transversales T47): las
/// <c>Active</c> con <c>ExpiresOn</c> anterior a hoy (hora de Colombia, T20) quedan <c>Expired</c> y lo que les quedaba vuelve al disponible.
/// <b>Sin ruta</b>: lo corre la tarea programada <see cref="TareaDeReservasVencidas"/> con el actor «Proceso de integración», así que no lleva
/// clave (<c>LosComandosDeInventarioLlevanClave</c> sólo mira los comandos con ruta). Idempotente: una segunda pasada no encuentra nada.
/// Responde cuántas venció. (nuevo)
/// </summary>
public sealed record ReleaseExpiredReservationsCommand : IRequest<Result<int>>;

/// <summary>
/// Lee las vencidas, bloquea sus filas de <c>INV_StockBalances</c> en el orden canónico (T15), las vuelve a leer ya bloqueadas —una salida en
/// vuelo pudo consumirlas— y las vence por <see cref="ReservasDeInventario.VencerAsync"/>, en una transacción y un guardado. (nuevo)
/// </summary>
public sealed class ReleaseExpiredReservationsCommandHandler(
    IApplicationDbContext db,
    ReservasDeInventario reservas,
    ICerrojoDeInventario cerrojo,
    IDateTimeService reloj)
    : IRequestHandler<ReleaseExpiredReservationsCommand, Result<int>>
{
    public Task<Result<int>> Handle(ReleaseExpiredReservationsCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => VencerAsync(ct), ct);

    private async Task<Result<int>> VencerAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var candidatas = await reservas.VencidasAsync(hoy, ct);
        if (candidatas.Count == 0) return Result.Success(0);
        await cerrojo.BloquearAsync(new PedidoDeCerrojo
        {
            Existencias = candidatas.Select(r => new ClaveDeExistencia(r.ProductId, r.WarehouseId)).Distinct().ToList(),
        }, ct);
        var vencidas = await reservas.VencidasAsync(hoy, ct);
        var cuantas = await reservas.VencerAsync(vencidas, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(cuantas);
    }
}

/// <summary>
/// La tarea programada <c>inventario.reservas</c> (feature 012, I6, T889; T47 «I6: liberar reservas vencidas»): una vez al día corre
/// <see cref="ReleaseExpiredReservationsCommand"/> en cada cooperativa por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración»
/// y el arrendamiento <c>scheduled.tasks</c>. Antes de que el despliegue llegue a I6 no corre (no hay pedidos). Singleton. (nuevo)
/// </summary>
public sealed class TareaDeReservasVencidas(EntregaDelComercio entrega = CatalogoDeParametros.EntregaVigente) : ITareaProgramada
{
    public const string NombreDeLaTarea = "inventario.reservas";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        entrega >= EntregaDelComercio.I6
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<ISender>().Send(new ReleaseExpiredReservationsCommand(), ct);
}
