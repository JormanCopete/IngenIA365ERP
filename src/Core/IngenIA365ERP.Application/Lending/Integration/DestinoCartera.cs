using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Cartera como destino de la bandeja (feature 013, US3, T0449; US6, T0586; T17; mensajes.md §15.1): <c>Destino = Lending</c>, un comando sin ruta por
/// familia —<see cref="ConsumeTreasuryResultCommand"/> para <c>ResultadoDelGiro</c> v1 y <see cref="ConsumeCommercialCreditMessageCommand"/> para
/// <c>VentaACreditoRegistrada</c> y <c>AjusteDeVentaACredito</c> en toda versión (C16)—, una unidad por trabajo (Cartera no resume) y sin comprobantes
/// (totales en cero). Lee sólo por <see cref="IMensajesEntrantes"/> (en sus comandos). Nunca lanza: una excepción es <c>Retry</c>; un fallo del comando,
/// <c>Rejected</c>. Corre por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración» y nunca usa los permisos del usuario de origen (I-2).
///
/// <para>
/// <b>Registro</b> (bloque 7C): en <c>AddApplication</c>. El destino <c>Lending</c> cuenta cuando este consumidor acepta todo lo que adopta la fila
/// <c>CAR</c> del registro (<c>RegistroDeIntegracion.ImplementacionDisponible</c>): desde US6 acepta la familia de Comercial que la semilla le adopta,
/// así que el despachador ya le entrega las ventas a crédito, sus ajustes y el <c>ResultadoDelGiro</c>. (nuevo)
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
                var r = await sender.Send(ComandoDe(unidad), ct);
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

    /// <summary>
    /// El comando de la familia de la unidad: lo de Comercial (<c>INV</c>) a <see cref="ConsumeCommercialCreditMessageCommand"/>; lo de Tesorería, a
    /// <see cref="ConsumeTreasuryResultCommand"/>. El tipo exacto lo valida cada comando al leer.
    /// </summary>
    private static IRequest<IngenIA365ERP.Application.Common.Models.Result<ResultadoDeConsumo>> ComandoDe(UnidadDeConsumo unidad) =>
        unidad.OriginModule == ModulosDeOrigen.Inventario
            ? new ConsumeCommercialCreditMessageCommand(unidad.MessagePublicIds)
            : new ConsumeTreasuryResultCommand(unidad.MessagePublicIds);

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
