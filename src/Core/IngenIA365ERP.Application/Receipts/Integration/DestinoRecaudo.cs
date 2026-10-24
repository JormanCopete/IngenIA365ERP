using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// El consumidor único del destino <c>Receipts</c> (feature 015, US5, T0176; FR-059, FR-063, FR-151, FR-195; contracts/mensajes.md §3.1): en R0 recibe de Cartera y
/// Captaciones el aviso de que reversaron un pago o una consignación que había aplicado Recaudo (<c>PagoReversadoPorCartera</c> v1,
/// <c>ConsignacionReversadaPorCartera</c> v1) y lo manda a <see cref="ConsumeLendingPaymentReversalCommand"/>, como «Proceso de integración». Los de libranza
/// (<c>DescuentosPorPagaduria</c>) los suma la fase 12. Lo registra la bandera transitoria (<c>ConsumidoresDeRecaudoYTesoreria</c>) sólo si los dobles no se
/// registran; nunca conviven dos consumidores del destino. (nuevo)
/// </summary>
public sealed class DestinoRecaudo(ISender sender, ILogger<DestinoRecaudo> logger) : IDestinoDeMensajes
{
    public static readonly IReadOnlyList<(string Type, int Version)> Aceptados =
    [
        (PagoReversadoPorCarteraV1.Type, 1),
        (IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.ConsignacionReversadaPorCarteraV1.Type, 1),
    ];

    public string Destino => IntegrationDestinations.Receipts;

    public bool Acepta(string type, int version) => Aceptados.Contains((type, version));

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
        foreach (var unidad in trabajo.Unidades)
        {
            try
            {
                var r = await sender.Send(new ConsumeLendingPaymentReversalCommand(unidad.MessagePublicIds), ct);
                resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Recaudo no pudo consumir la unidad {Origen}/{Clave}: se reintenta.", unidad.OriginPublicId, unidad.OriginEventKey);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        return entregas
            .GroupBy(m => (m.Envelope.OriginModule, m.Envelope.Origin.PublicId, m.Envelope.OriginEventKey))
            .Select(g => TrabajoDeConsumo.DeUnaUnidad(new UnidadDeConsumo(Destino, g.Key.PublicId, g.Key.OriginEventKey,
                g.Select(m => m.MessageId).ToList(),
                g.Select(m => m.Entrega.BatchPublicId).FirstOrDefault(b => b is not null),
                g.Max(m => m.Entrega.Attempts))
            {
                OriginModule = g.Key.OriginModule,
            }))
            .ToList();
    }
}
