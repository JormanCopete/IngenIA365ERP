using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Comercial como destino de la bandeja (feature 013, US6, T0598; DT §2.9, CH9; mensajes.md §11.1, §13.1): <c>Destino = Inventory</c>, el único
/// consumidor de ese destino. Recibe las respuestas de Cartera a una venta a crédito —<c>ObligacionCreada</c> v1 y <c>ObligacionRechazada</c> v1— y las
/// guarda con <see cref="ConsumeLendingObligationResponseCommand"/> en <c>INV_LendingObligationResponses</c>: número y estado, <b>nunca</b> saldos ni
/// cuotas (enmienda de FR-062 de la 012). Una unidad por trabajo y sin comprobantes. Lee sólo por <see cref="IMensajesEntrantes"/> (en su comando). Nunca
/// lanza: una excepción es <c>Retry</c>; un fallo del comando, <c>Rejected</c>. Corre por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de
/// integración».
///
/// <para>
/// <b>Registro</b>: en <c>AddApplication</c>. La fila <c>INV</c> del registro de participantes (sembrada activa por <c>PlataformaParaCartera</c>) adopta
/// los dos contratos; hasta este consumidor el destino se saltaba sin intentos y las respuestas esperaban en <c>Pending</c>. (nuevo)
/// </para>
/// </summary>
public sealed class DestinoInventario(ISender sender, ILogger<DestinoInventario> logger, IApplicationDbContext? db = null) : IDestinoDeMensajes
{
    public string Destino => IntegrationDestinations.Inventory;

    /// <summary>
    /// <c>ObligacionCreada</c> v1 y <c>ObligacionRechazada</c> v1 (mensajes.md §13.1) y, desde la decisión del dueño del 2026-10-18 (feature 014, D51),
    /// los cargos gravados de captaciones <c>CargoDeCaptacionCausado</c> v1 y <c>CargoDeCaptacionReversado</c> v1, que se facturan
    /// (<see cref="ConsumeDepositChargeMessageCommand"/>).
    /// </summary>
    public bool Acepta(string type, int version) =>
        version == 1 && (string.Equals(type, ObligacionCreadaV1.Type, StringComparison.Ordinal)
            || string.Equals(type, ObligacionRechazadaV1.Type, StringComparison.Ordinal)
            || EsDeCargo(type)
            // Feature 015, T2 (fase 42; E-03): el aviso del pago de una factura de proveedor.
            || string.Equals(type, IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.PagoDeFacturaDeProveedorV1.Type, StringComparison.Ordinal));

    private static bool EsDeCargo(string type) =>
        string.Equals(type, IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.CargoDeCaptacionCausadoV1.Type, StringComparison.Ordinal)
        || string.Equals(type, IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.CargoDeCaptacionReversadoV1.Type, StringComparison.Ordinal);

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
        foreach (var unidad in trabajo.Unidades)
        {
            try
            {
                var deCargo = db is not null && await db.IntegrationMessages.AsNoTracking()
                    .AnyAsync(m => unidad.MessagePublicIds.Contains(m.PublicId)
                                   && (m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.CargoDeCaptacionCausadoV1.Type
                                       || m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.CargoDeCaptacionReversadoV1.Type), ct);
                var r = unidad.OriginModule == ModulosDeOrigen.Tesoreria
                    ? await sender.Send(new ConsumeSupplierInvoicePaymentCommand(unidad.MessagePublicIds), ct)
                    : deCargo
                        ? await sender.Send(new ConsumeDepositChargeMessageCommand(unidad.MessagePublicIds), ct)
                        : await sender.Send(new ConsumeLendingObligationResponseCommand(unidad.MessagePublicIds), ct);
                resultados.Add(new ResultadoDeUnidad(unidad, r.IsSuccess ? r.Value : new ResultadoDeConsumo.Rejected(r.Error.Code, r.Error.Message)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Comercial no pudo guardar la respuesta de Cartera {Origen}/{Clave}: se reintenta.", unidad.OriginPublicId, unidad.OriginEventKey);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    /// <summary>Una unidad por (módulo, origen, clave de evento): cada respuesta es su propio recibo.</summary>
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
