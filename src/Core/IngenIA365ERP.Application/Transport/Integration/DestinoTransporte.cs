using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Transport.Integration;

/// <summary>
/// Transporte como destino opcional de la bandeja (feature 020, R12; T157; contracts/mensajes.md §3 de la 020): <c>Destino = TRN</c>, el
/// único consumidor de ese destino. Escucha <c>PagoAplicado</c>, <c>PagoReversado</c>, <c>ReciboEmitido</c>, <c>ReciboAnulado</c> y
/// <c>DocumentoAnulado</c> v1 y los aplica con <see cref="ApplyChargeAllocationsCommand"/>; lo que no toca ningún cargo responde
/// <c>AlreadyProcessed</c> sin escribir. Una unidad por trabajo, sin comprobantes. Nunca lanza: una excepción es <c>Retry</c>; un fallo
/// del comando, <c>Rejected</c>. Corre por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración».
/// </summary>
public sealed class DestinoTransporte(ISender sender, ILogger<DestinoTransporte> logger) : IDestinoDeMensajes
{
    private static readonly HashSet<string> Tipos = new(StringComparer.Ordinal)
    {
        PagoAplicadoV1.Type, PagoReversadoV1.Type, ReciboEmitidoV1.Type, ReciboAnuladoV1.Type, DocumentoAnuladoV1.Type,
    };

    public string Destino => IntegrationDestinations.Transport;

    public bool Acepta(string type, int version) => version == 1 && Tipos.Contains(type);

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
        foreach (var unidad in trabajo.Unidades)
        {
            try
            {
                var r = await sender.Send(new ApplyChargeAllocationsCommand(unidad.MessagePublicIds), ct);
                resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Transporte no pudo aplicar {Origen}/{Clave}: se reintenta.", unidad.OriginPublicId, unidad.OriginEventKey);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    /// <summary>Una unidad por (módulo, origen, clave de evento): cada pago o anulación es su propio recibo.</summary>
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
