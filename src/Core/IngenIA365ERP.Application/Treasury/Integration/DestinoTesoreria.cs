using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Treasury.Integration;

/// <summary>
/// Tesorería como destino de la bandeja (feature 015, US10, T0140; R-26; FR-107, FR-115, FR-151; contracts/mensajes.md §2.3, §5): <c>Destino = Treasury</c>, el
/// <b>único</b> consumidor real de ese destino (<c>UnSoloConsumidorPorDestino</c>, <c>NoHayDoblesDePrueba</c>). Recibe de Cartera y Captaciones
/// <c>OrdenDeGiroEmitida</c> v1 y v2 (<see cref="ConsumePaymentOrderCommand"/>) y <c>AnulacionDeOrdenDeGiro</c> v1
/// (<see cref="ConsumePaymentOrderVoidCommand"/>); los reparte por la clave de evento (la orden nace con <c>Confirmation</c>; su anulación lleva
/// <c>Confirmation:{orden:N}</c>, C12), y cada comando valida el tipo exacto al leer. Una unidad por trabajo y sin comprobantes. Nunca lanza: una excepción es
/// <c>Retry</c>; un fallo del comando, <c>Rejected</c>. Corre por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración».
///
/// <para>
/// <b>Registro transitorio</b> (T0141; DT Q9, R-26): mientras existan los dobles de prueba de la 013, la API registra este consumidor sólo con
/// <c>Lending:TestDoubles:Enabled = false</c> (y <c>TesoreriaDePrueba</c> sólo con <c>true</c>), para que <c>ConsumidoresPorDestino.Validar()</c> nunca vea dos.
/// La fase 19 borra los dobles y la bandera. (nuevo)
/// </para>
/// </summary>
public sealed class DestinoTesoreria(ISender sender, ILogger<DestinoTesoreria> logger) : IDestinoDeMensajes
{
    /// <summary>Los tipos y versiones que Tesorería adopta en T0 (mensajes.md §7; <c>ArchivoDeDesembolsos*</c> llegan con E-01 c, fase 16).</summary>
    public static readonly IReadOnlyList<(string Type, int Version)> Aceptados =
    [
        (OrdenDeGiroEmitidaV1.Type, 1),
        (OrdenDeGiroEmitidaV2.Type, 2),
        (AnulacionDeOrdenDeGiroV1.Type, 1),
    ];

    public string Destino => IntegrationDestinations.Treasury;

    public bool Acepta(string type, int version) => Aceptados.Contains((type, version));

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
                logger.LogWarning(ex, "Tesorería no pudo consumir la unidad {Origen}/{Clave}: se reintenta.", unidad.OriginPublicId, unidad.OriginEventKey);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    /// <summary>La orden (clave <c>Confirmation</c>) o su anulación (clave <c>Confirmation:{orden:N}</c>).</summary>
    public static IRequest<IngenIA365ERP.Application.Common.Models.Result<ResultadoDeConsumo>> ComandoDe(UnidadDeConsumo unidad) =>
        string.Equals(unidad.OriginEventKey, ClavesDeEvento.Confirmacion, StringComparison.Ordinal)
            ? new ConsumePaymentOrderCommand(unidad.MessagePublicIds)
            : new ConsumePaymentOrderVoidCommand(unidad.MessagePublicIds);

    /// <summary>Una unidad por (módulo, origen, clave de evento): cada orden es su propio recibo.</summary>
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
