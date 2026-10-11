using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Cartera como destino de la bandeja (feature 013, US3, T0449; T17; mensajes.md §15.1): <c>Destino = Lending</c>, un comando sin ruta por familia
/// —en K1 <see cref="ConsumeTreasuryResultCommand"/> para <c>ResultadoDelGiro</c> v1; US6 suma la familia de Comercial—, una unidad por trabajo
/// (Cartera no resume) y sin comprobantes (totales en cero). Nunca lanza: una excepción es <c>Retry</c>; un fallo del comando, <c>Rejected</c>.
/// Corre por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración» y nunca usa los permisos del usuario de origen (I-2).
///
/// <para>
/// <b>Registro</b>: queda para el bloque 7C, junto con las rutas y las e2e. Registrarlo ya haría que el despachador consuma las ventas a crédito de
/// Comercial que hoy se saltan (no hay consumidor de <c>Lending</c>) y las rechace por versión antes de que US6 traiga su familia. (nuevo)
/// </para>
/// </summary>
public sealed class DestinoCartera(ISender sender, ILogger<DestinoCartera> logger) : IDestinoDeMensajes
{
    public string Destino => IntegrationDestinations.Lending;

    public bool Acepta(string type, int version) => VersionesAceptadasDeCartera.Acepta(type, version);

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
        foreach (var unidad in trabajo.Unidades)
        {
            try
            {
                var r = await sender.Send(new ConsumeTreasuryResultCommand(unidad.MessagePublicIds), ct);
                resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cartera no pudo consumir la unidad {Origen}/{Clave}: se reintenta.", unidad.OriginPublicId, unidad.OriginEventKey);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    /// <summary>Una unidad por (módulo, origen, clave de evento): Cartera no resume.</summary>
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
