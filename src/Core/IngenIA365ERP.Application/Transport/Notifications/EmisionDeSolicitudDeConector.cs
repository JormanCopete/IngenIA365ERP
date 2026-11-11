using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Transport;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Transport.Billing;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Connectors;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Transport.Notifications;

/// <summary>Con qué pago quedó saldado un cargo: el número del recibo (o de la factura de contado) y su fecha.</summary>
public sealed record PagoQueSalda(string? Recibo, DateOnly? Fecha);

/// <summary>
/// La emisión del aviso al sistema externo cuando un cargo queda pagado (feature 020, R14; FR-045 a FR-048; T155). Emite
/// <c>SolicitudDeConector</c> v1 por <see cref="EmisorDeMensajes"/> en la <b>misma unidad de trabajo</b> que dejó el cargo pagado y
/// <b>nunca guarda</b>. Sólo la primera vez: un cargo que ya avisó no vuelve a avisar (un repago tras una anulación queda para revisar).
///
/// <list type="bullet">
/// <item>Las variables se resuelven <b>ahora</b> —vehículo, propietario del cargo, servicio, período, pago, factura— y el envío lleva
/// <c>envio.id</c> = <see cref="ServiceCharge.ExternalId"/>, el mismo en todos los reintentos;</item>
/// <item>se sella la versión vigente del conector y la lista de variables sensibles;</item>
/// <item>nace el envío del tablero (<c>COR_ConnectorRequests</c>, <c>Pending</c>) y el cargo guarda su <c>PublicId</c>;</item>
/// <item>un servicio sin conector no avisa (no es un error); un conector sin versión vigente deja el cargo
/// <see cref="ChargeReviewFlag.NoConnector"/>. Un conector deshabilitado o sin credenciales <b>sí</b> emite: el consumidor deja el envío en
/// espera y sale cuando se habilite o se pongan las credenciales.</item>
/// </list>
/// Implementa <see cref="INotificadorDeCargos"/>: la ruta de contado de la facturación la llama al dejar los cargos pagados (T158).
/// </summary>
public sealed class EmisionDeSolicitudDeConector(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    FacturasDeServicios facturas,
    IDateTimeService reloj,
    ILogger<EmisionDeSolicitudDeConector> logger) : INotificadorDeCargos
{
    public const string ClaseDeOrigen = "ServiceCharge";

    public Task AlPagarAsync(ServiceCharge cargo, CancellationToken ct) => EmitirAsync(cargo, new PagoQueSalda(cargo.InvoiceNumber, reloj.HoyLocal), ct);

    /// <summary>Emite el aviso del cargo recién pagado. Devuelve el envío creado, o nulo si no hubo aviso.</summary>
    public async Task<ConnectorRequest?> EmitirAsync(ServiceCharge cargo, PagoQueSalda pago, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        if (cargo.NotifiedRequestPublicId is not null || cargo.Status != ChargeStatus.Paid) return null;

        var servicio = cargo.Service ?? await db.TransportServices.FirstAsync(s => s.Id == cargo.ServiceId, ct);
        if (servicio.ConnectorId is not int conectorId) return null;

        var conector = await db.Connectors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == conectorId, ct);
        if (conector?.CurrentVersionNumber is not int version)
        {
            cargo.ReviewFlag = ChargeReviewFlag.NoConnector;
            logger.LogWarning("[Transporte.Aviso] El cargo {Cargo} no avisa: su conector no tiene versión vigente.", cargo.PublicId);
            return null;
        }

        var vehiculo = cargo.Vehicle ?? await db.Vehicles.AsNoTracking().FirstAsync(v => v.Id == cargo.VehicleId, ct);
        var propietario = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == cargo.OwnerPersonId, ct);
        PeriodoDeServicio.TryParse(cargo.Period, out var periodo);
        var fecha = pago.Fecha ?? reloj.HoyLocal;

        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vehiculo.placa"] = vehiculo.Plate,
            ["vehiculo.numeroInterno"] = vehiculo.InternalNumber,
            ["vehiculo.radioMovil"] = vehiculo.RadioCode ?? string.Empty,
            ["propietario.documento"] = propietario.TaxId,
            ["propietario.nombre"] = NombreVisible.De(propietario),
            ["servicio.codigo"] = servicio.ProductCode,
            ["servicio.nombre"] = servicio.Name,
            ["periodo.inicio"] = periodo.Inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["periodo.fin"] = periodo.Fin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["periodo.texto"] = periodo.Texto(),
            ["pago.valor"] = cargo.Amount.ToString(CultureInfo.InvariantCulture),
            ["pago.recibo"] = string.IsNullOrWhiteSpace(pago.Recibo) ? cargo.InvoiceNumber : pago.Recibo!,
            ["pago.fecha"] = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["factura.numero"] = cargo.InvoiceNumber,
            ["envio.id"] = cargo.ExternalId.ToString("D"),
        };
        var contenido = new SolicitudDeConectorV1
        {
            ConnectorPublicId = conector.PublicId,
            ConnectorVersion = version,
            ExternalId = cargo.ExternalId,
            OriginRef = OrigenDe(cargo),
            Variables = variables,
            Sensitive = [.. VariablesDeConector.Sensibles.Order(StringComparer.Ordinal)],
        };

        var oficina = await facturas.OficinaAsync(cargo.InvoicePublicId, ct)
                      ?? await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        if (oficina is not { } sucursal)
        {
            cargo.ReviewFlag = ChargeReviewFlag.NoConnector;
            logger.LogWarning("[Transporte.Aviso] El cargo {Cargo} no avisa: la cooperativa no tiene oficina.", cargo.PublicId);
            return null;
        }

        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, cargo.PublicId, ClaseDeOrigen, ModulosDeOrigen.Transporte, cargo.InvoiceNumber, fecha, sucursal,
                PersonPublicId: propietario.PublicId),
            ClavesDeEvento.ConfirmacionPor(cargo.PublicId),
            [contenido],
            new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);

        var envio = new ConnectorRequest
        {
            MessagePublicId = mensajes[0].PublicId,
            ConnectorId = conectorId,
            VersionNumber = version,
            OriginRef = contenido.OriginRef,
            ExternalId = cargo.ExternalId,
            Status = ConnectorRequestStatus.Pending,
        };
        db.ConnectorRequests.Add(envio);
        cargo.NotifiedRequestPublicId = envio.PublicId;
        return envio;
    }

    /// <summary>Cómo nombra el envío a su cargo (el conector no conoce Transporte).</summary>
    public static string OrigenDe(ServiceCharge cargo) => $"TRN:{cargo.PublicId:N}";
}
