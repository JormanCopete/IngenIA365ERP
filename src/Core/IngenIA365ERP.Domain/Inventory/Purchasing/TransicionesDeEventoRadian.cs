using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Purchasing;

/// <summary>Un evento RADIAN de la factura tal como está hoy (para decidir la transición). (nuevo)</summary>
public sealed record EventoRadianActual(SupplierInvoiceEventCode Codigo, SupplierInvoiceEventStatus Estado, DateOnly? Fecha);

/// <summary>
/// La máquina de estados de los eventos RADIAN de una factura del proveedor (feature 012, T336; data-model §9.3; FR-050,
/// US9-4, T42), pura. (nuevo)
/// <list type="bullet">
/// <item>al confirmar la factura nacen los dos: <c>Pending</c> a crédito, <c>NotApplicable</c> de contado
/// (<see cref="Iniciales"/>);</item>
/// <item>I1: <c>Pending → RegisteredExternally</c> (la cooperativa lo emitió por fuera), y la <b>corrección</b> de un registro
/// externo (<c>RegisteredExternally → RegisteredExternally</c>, con antes y después auditados); el 032 exige el 030 registrado
/// o emitido; la fecha va entre la emisión de la factura y hoy;</item>
/// <item>I5 (declaradas, sin uso en I1): <c>Pending → Emitted</c>, <c>Pending → Rejected</c>, <c>Rejected → Pending</c> al
/// reintentar.</item>
/// </list>
/// Devuelve el código del rechazo (el mismo que publica la aplicación) o nulo si procede.
/// </summary>
public static class TransicionesDeEventoRadian
{
    public const string CodigoNoAplica = "Inventory.RadianEvent.NotApplicable";
    public const string CodigoFueraDeOrden = "Inventory.RadianEvent.OutOfOrder";
    public const string CodigoFechaInvalida = "Inventory.RadianEvent.DateInvalid";
    public const string CodigoYaRegistrado = "Inventory.RadianEvent.AlreadyRegistered";

    /// <summary>Los dos eventos de una factura recién confirmada.</summary>
    public static IReadOnlyList<(SupplierInvoiceEventCode Codigo, SupplierInvoiceEventStatus Estado)> Iniciales(bool aCredito)
    {
        var estado = aCredito ? SupplierInvoiceEventStatus.Pending : SupplierInvoiceEventStatus.NotApplicable;
        return [(SupplierInvoiceEventCode.Receipt030, estado), (SupplierInvoiceEventCode.GoodsReceived032, estado)];
    }

    /// <summary>¿Existe la transición <paramref name="de"/> → <paramref name="a"/>?</summary>
    public static bool EsTransicionValida(SupplierInvoiceEventStatus de, SupplierInvoiceEventStatus a) => (de, a) switch
    {
        (SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.RegisteredExternally) => true,
        (SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.RegisteredExternally) => true, // corrección
        (SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Emitted) => true, // I5
        (SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Rejected) => true, // I5
        (SupplierInvoiceEventStatus.Rejected, SupplierInvoiceEventStatus.Pending) => true, // I5, reintento
        _ => false,
    };

    /// <summary>¿El evento ya está hecho (registrado por fuera o emitido)?</summary>
    public static bool Hecho(SupplierInvoiceEventStatus estado) =>
        estado is SupplierInvoiceEventStatus.RegisteredExternally or SupplierInvoiceEventStatus.Emitted;

    /// <summary>¿Queda alguno pendiente? (la alerta <c>Compras.EventosRadianFaltantes</c> sigue viva mientras sí).</summary>
    public static bool QuedaPendiente(IEnumerable<EventoRadianActual> eventos) =>
        eventos.Any(e => e.Estado == SupplierInvoiceEventStatus.Pending);

    /// <summary>
    /// Registrar (o, con <paramref name="corregir"/>, corregir) que la cooperativa emitió <paramref name="codigo"/> por fuera
    /// el <paramref name="fecha"/>. Nulo = procede.
    /// </summary>
    /// <param name="eventos">Los dos eventos de la factura hoy.</param>
    /// <param name="codigo">El evento que se registra.</param>
    /// <param name="fecha">La fecha en que se emitió.</param>
    /// <param name="emision">La fecha de emisión de la factura.</param>
    /// <param name="hoy">La fecha local de Colombia.</param>
    /// <param name="corregir">Corregir un registro externo ya hecho (fecha, fuente, CUDE o notas).</param>
    public static string? RegistrarExterno(
        IReadOnlyList<EventoRadianActual> eventos, SupplierInvoiceEventCode codigo, DateOnly fecha, DateOnly emision, DateOnly hoy, bool corregir)
    {
        var evento = eventos.FirstOrDefault(e => e.Codigo == codigo);
        if (evento is null || evento.Estado == SupplierInvoiceEventStatus.NotApplicable) return CodigoNoAplica;

        var destino = SupplierInvoiceEventStatus.RegisteredExternally;
        if (corregir)
        {
            if (evento.Estado != SupplierInvoiceEventStatus.RegisteredExternally) return evento.Estado == SupplierInvoiceEventStatus.Emitted ? CodigoYaRegistrado : CodigoNoAplica;
        }
        else if (Hecho(evento.Estado))
        {
            return CodigoYaRegistrado;
        }
        if (!EsTransicionValida(evento.Estado, destino)) return CodigoNoAplica;

        if (codigo == SupplierInvoiceEventCode.GoodsReceived032)
        {
            var acuse = eventos.FirstOrDefault(e => e.Codigo == SupplierInvoiceEventCode.Receipt030);
            if (acuse is null || !Hecho(acuse.Estado)) return CodigoFueraDeOrden;
        }

        if (fecha < emision || fecha > hoy) return CodigoFechaInvalida;
        return null;
    }
}
