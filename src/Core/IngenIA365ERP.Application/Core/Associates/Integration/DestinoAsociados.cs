using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Core.Associates.Integration;

/// <summary>
/// Asociados como destino de la bandeja (feature 014, US10, T098; contracts/mensajes.md §1.4, §5.1 y §7; research C16; enmienda E-04): <c>Destino =
/// Associates</c>, el único consumidor de ese destino. Recibe <c>RetiroDeAsociadoAprobado</c> v1 de la Cartera y cambia el estado del asociado con
/// <see cref="ConsumeMembershipWithdrawalApprovedCommand"/>. Una unidad por trabajo y sin comprobantes. Lee sólo por <see cref="IMensajesEntrantes"/> (en
/// su comando). Nunca lanza: una excepción es <c>Retry</c>; un fallo del comando, <c>Rejected</c>. Corre por <c>IEjecutorEnCooperativa</c> con el actor
/// «Proceso de integración».
///
/// <para>
/// <b>Registro</b>: en <c>AddApplication</c>. La fila <c>ASO</c> del registro de participantes (sembrada habilitada por <c>CaptacionesAportes</c>) adopta
/// <c>RetiroDeAsociadoAprobado</c> v1; sin este consumidor el destino se saltaría sin intentos. (nuevo)
/// </para>
/// </summary>
public sealed class DestinoAsociados(ISender sender, ILogger<DestinoAsociados> logger) : IDestinoDeMensajes
{
    public string Destino => IntegrationDestinations.Associates;

    /// <summary><c>RetiroDeAsociadoAprobado</c> v1 (mensajes.md §7).</summary>
    public bool Acepta(string type, int version) => version == 1 && string.Equals(type, RetiroDeAsociadoAprobadoV1.Type, StringComparison.Ordinal);

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
        foreach (var unidad in trabajo.Unidades)
        {
            try
            {
                var r = await sender.Send(new ConsumeMembershipWithdrawalApprovedCommand(unidad.MessagePublicIds), ct);
                resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Asociados no pudo consumir el retiro {Origen}/{Clave}: se reintenta.", unidad.OriginPublicId, unidad.OriginEventKey);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    /// <summary>Una unidad por (módulo, origen, clave de evento): cada retiro es su propio recibo.</summary>
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
