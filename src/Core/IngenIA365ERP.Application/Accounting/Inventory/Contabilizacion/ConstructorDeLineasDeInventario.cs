using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using O = IngenIA365ERP.Application.Accounting.Inventory.Reglas.OperacionesDeInventario;
using R = IngenIA365ERP.Application.Accounting.Inventory.Reglas.RolesDeCuenta;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>Un mensaje de una unidad tal como lo ve el constructor: el sobre y su contenido tipado. (nuevo)</summary>
public sealed record MensajeDeUnidad(IntegrationEnvelopeV1 Sobre, object? Contenido)
{
    public static MensajeDeUnidad De(MensajeEntrante mensaje) => new(mensaje.Envelope, mensaje.Payload);

    public string Tipo => Sobre.Type;
}

/// <summary>
/// De qué salió cada línea del <see cref="PostingRequest"/> (el mapa de §3.2, FR-074): el mensaje, el rol, las líneas del
/// documento que suman ahí y la cuenta. <see cref="ConservaDetalle"/> dice si la cuenta exige tercero, cruce o base gravable:
/// en un resumido esas líneas no se suman (§5.3). En paralelo con <c>Request.Lines</c>. (nuevo)
/// </summary>
public sealed record LineaDeLaUnidad(
    string MessageType,
    string Rol,
    IReadOnlyList<int> DocumentLines,
    string AccountCode,
    string AccountName,
    bool EsImpuesto,
    bool ConservaDetalle);

/// <summary>
/// Un error al armar el comprobante de una unidad (matriz, tipo de comprobante o contenido): el error de siempre y de dónde
/// salió. <see cref="DeLaCuenta"/> distingue la tarifa distinta de la cuenta (C8) de la de la regla, porque las corrige gente
/// distinta (§4.3). (nuevo)
/// </summary>
public sealed record FalloDeConstruccion(
    Error Error,
    string MessageType,
    IReadOnlyList<int> DocumentLines,
    string? AccountCode,
    bool DeLaCuenta = false);

/// <summary>
/// El comprobante que produce una unidad (nuevo). Sin fallos, <see cref="Request"/> es lo que se entrega a
/// <c>AccountingPoster</c>; sin líneas distintas de cero, <see cref="ValorCero"/> (recibo sin comprobante, §3.1 paso 8).
/// </summary>
public sealed record ConstruccionDeUnidad(
    PostingRequest? Request,
    TipoDeLaUnidad? Tipo,
    IReadOnlyList<LineaDeLaUnidad> Mapa,
    IReadOnlyList<FalloDeConstruccion> Fallos,
    DateOnly Fecha,
    MensajeDeUnidad Principal)
{
    public bool EsValida => Fallos.Count == 0 && Request is not null;

    public bool ValorCero => EsValida && Request!.Lines.Count == 0;

    public decimal TotalDebito => Request?.Lines.Sum(l => l.Debit) ?? 0m;
}

/// <summary>
/// Lo que el constructor necesita ya cargado (nuevo): la matriz vigente en el rango de fechas de las reglas, los mapeos de
/// tipo de comprobante y las personas del sobre, de los pagos y del cajero (<c>PublicId → Id</c>, también las dadas de baja:
/// el contrato de la 009 las rechaza con su código). Se carga una vez por unidad, por grupo o por lote
/// (<see cref="CargarAsync"/>) y el constructor no hace IO.
/// </summary>
public sealed class CatalogosDelConstructor(MatrizVigente matriz, MapeosDeInventario mapeos, IReadOnlyDictionary<Guid, int> personas)
{
    public MatrizVigente Matriz { get; } = matriz;

    public MapeosDeInventario Mapeos { get; } = mapeos;

    public IReadOnlyDictionary<Guid, int> Personas { get; } = personas;

    /// <summary>Carga lo que piden las unidades: fechas de las reglas (incluidas las de los originales anulados) y personas.</summary>
    public static async Task<CatalogosDelConstructor> CargarAsync(
        IApplicationDbContext db, ResolutorDeReglas resolutor, TiposDeComprobanteDeInventario tipos,
        IEnumerable<IReadOnlyList<MensajeDeUnidad>> unidades, DateOnly hoy, CancellationToken ct)
    {
        var partidas = unidades.SelectMany(u => ConstructorDeLineasDeInventario.Desglosar(u).Partidas).ToList();
        var fechas = partidas.Select(p => p.FechaDeReglas).ToList();
        var desde = fechas.Count == 0 ? hoy : fechas.Min();
        var hasta = fechas.Count == 0 ? hoy : fechas.Max();
        var matriz = await resolutor.CargarAsync(desde, hasta, ct);
        var mapeos = await tipos.CargarAsync(ct);

        var guids = partidas.SelectMany(p => new[] { p.TerceroNatural, p.TerceroDelSobre })
            .Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();
        var personas = guids.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => guids.Contains(p.PublicId))
                .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return new CatalogosDelConstructor(matriz, mapeos, personas);
    }
}

/// <summary>
/// De una unidad de mensajes a un <see cref="PostingRequest"/> (feature 012, T511; contracts/contabilidad.md §3.2 a §3.7;
/// FR-074, FR-077, FR-079). Lo usan el consumidor (<c>PostInventoryMessagesCommand</c>, el resumido), la validación previa
/// (<c>EvaluateInventoryPostingQuery</c>) y la vista previa de un lote: lo evaluado y lo contabilizado salen del mismo código
/// (SC-021 por construcción). <b>Sin IO</b>: recibe la matriz, los mapeos y las personas ya cargados
/// (<see cref="CatalogosDelConstructor"/>).
///
/// <para>
/// Dos pasos. <see cref="Desglosar"/> convierte cada contenido en <b>partidas</b> —un rol, un lado para el importe positivo, un
/// importe con su signo y los valores que busca en la matriz—, según las tablas de §3.3 a §3.6; el espejo de un
/// <c>DocumentoAnulado</c> (§3.7) desglosa cada contenido anulado como su tipo original, con la fecha del original para las
/// reglas y su número para el cruce, y como los importes vienen con el signo contrario cada lado queda invertido.
/// <see cref="Construir"/> resuelve cada partida en la matriz, arma las líneas (un negativo invierte el lado, los ceros se
/// descartan, tercero natural, centro, cruce y base sólo donde la cuenta los exige) y suma las iguales salvo las de impuesto.
/// </para>
///
/// <para>
/// Los invariantes del contenido de <c>mensajes.md</c> §6 (venta y notas; factura del proveedor) y el cuadre de las partidas se
/// comprueban antes de resolver: si fallan es un defecto del emisor, <c>Accounting.InventoryMessage.Unbalanced</c>. (nuevo)
/// </para>
/// </summary>
public static class ConstructorDeLineasDeInventario
{
    /// <summary>El resultado del primer paso: las partidas, los fallos del contenido y el mensaje principal.</summary>
    public sealed record Desglose(IReadOnlyList<Partida> Partidas, IReadOnlyList<FalloDeConstruccion> Fallos, MensajeDeUnidad? Principal);

    /// <summary>
    /// Una partida: lo que un renglón del contenido pide a un rol. <see cref="DebitoSiPositivo"/> es el lado del importe positivo
    /// (§2.2); un importe negativo lo invierte. <see cref="Cruce"/> es el documento que salda, cuando no es el de la unidad (§3.2).
    /// </summary>
    public sealed record Partida(
        string MessageType,
        string Operacion,
        string Rol,
        bool DebitoSiPositivo,
        decimal Importe,
        ValoresBuscados Valores,
        DateOnly FechaDeReglas,
        IReadOnlyList<int> DocumentLines,
        Guid? TerceroNatural,
        Guid? TerceroDelSobre,
        decimal? Tarifa,
        decimal? Base,
        CruceDeLaPartida? Cruce,
        string Detalle,
        string NumeroDelDocumento,
        bool EsCredito = false)
    {
        public bool EsImpuesto => Rol is R.Impuesto or R.Retencion;

        /// <summary>El importe con el signo del débito: suma cero en un contenido que cuadra.</summary>
        public decimal Neto => DebitoSiPositivo ? Importe : -Importe;
    }

    /// <summary>Contra qué documento cruza una partida que salda otro: la operación del original (para su tipo de cruce) y su número.</summary>
    public sealed record CruceDeLaPartida(string OperacionDelOriginal, string Numero);

    // =================================================================================================== paso 1 --

    /// <summary>Las partidas de una unidad y los fallos de su contenido (invariantes, cuadre). Puro.</summary>
    public static Desglose Desglosar(IReadOnlyList<MensajeDeUnidad> unidad)
    {
        ArgumentNullException.ThrowIfNull(unidad);
        var partidas = new List<Partida>();
        var fallos = new List<FalloDeConstruccion>();
        if (unidad.Count == 0) return new Desglose(partidas, fallos, null);

        foreach (var mensaje in unidad)
        {
            if (mensaje.Contenido is DocumentoAnuladoV1 anulado)
            {
                foreach (var contenido in anulado.VoidedContents)
                {
                    var original = ContenidoTipado(contenido.Type, contenido.Version, contenido.Content);
                    if (original is null) continue;
                    var sobreDelOriginal = mensaje.Sobre with
                    {
                        Type = contenido.Type,
                        Origin = mensaje.Sobre.Origin with
                        {
                            OperationDate = ResolutorDeReglas.FechaDeLasReglasDelAnulado(contenido),
                            Number = anulado.VoidedDocument.Number,
                            DocumentClass = anulado.VoidedDocument.DocumentClass,
                            DocumentTypeCode = anulado.VoidedDocumentTypeCode,
                            PublicId = anulado.VoidedDocument.PublicId,
                        },
                        Related = null,
                    };
                    DesglosarContenido(new MensajeDeUnidad(sobreDelOriginal, original), mensaje.Tipo, partidas, fallos);
                }
            }
            else
            {
                DesglosarContenido(mensaje, mensaje.Tipo, partidas, fallos);
            }
        }

        // Cuadre por construcción (§3.3): cada renglón con sus dos lados, la venta y la factura por sus totales, el ajuste de
        // costo por su contrapartida. Una diferencia es un defecto del emisor.
        var diferencia = partidas.Sum(p => p.Neto);
        if (diferencia != 0m && fallos.Count == 0)
            fallos.Add(new FalloDeConstruccion(AccountingErrors.InventoryMessageUnbalanced(diferencia), unidad[0].Tipo, [], null));

        return new Desglose(partidas, fallos, Principal(unidad));
    }

    /// <summary>
    /// El mensaje que da tipo, fecha y descripción a la unidad (T28): el primero emitido, salvo el costo y la devolución física si
    /// la unidad trae otro.
    /// </summary>
    public static MensajeDeUnidad? Principal(IReadOnlyList<MensajeDeUnidad> unidad)
    {
        if (unidad.Count == 0) return null;
        return unidad.Select((m, i) => (m, i))
            .OrderBy(x => x.m.Tipo is CostoDeVentaReconocidoV1.Type or DevolucionRegistradaV1.Type ? 1 : 0).ThenBy(x => x.i)
            .First().m;
    }

    /// <summary>La fecha del comprobante (§3.2): la de operación; en un ajuste de costo, su <c>effectiveDate</c>. Nunca la de hoy.</summary>
    public static DateOnly FechaDelComprobante(MensajeDeUnidad mensaje) => mensaje.Contenido is AjusteDeCostoReconocidoV1 ajuste
        ? ajuste.EffectiveDate
        : mensaje.Sobre.Origin.OperationDate;

    /// <summary>Un contenido anulado con su record del catálogo; los que llegan como JSON se leen con las opciones de los mensajes.</summary>
    public static object? ContenidoTipado(string tipo, int version, object? contenido)
    {
        if (contenido is null) return null;
        // Armada en memoria (la validación previa) la anulación trae JsonNode; leída de la base, JsonElement. Se leen igual.
        if (contenido is System.Text.Json.Nodes.JsonNode nodo) contenido = JsonSerializer.SerializeToElement(nodo);
        if (contenido is not JsonElement json) return contenido;
        var registro = CatalogoDeMensajesV1.Todos.FirstOrDefault(t => t.Type == tipo && t.Version == version);
        return registro is null ? null : json.Deserialize(registro.Record, OpcionesDeMensajes.Opciones);
    }

    private static void DesglosarContenido(MensajeDeUnidad mensaje, string tipoDelMensaje, List<Partida> partidas, List<FalloDeConstruccion> fallos)
    {
        var sobre = mensaje.Sobre;
        var fecha = ResolutorDeReglas.FechaDeLasReglas(sobre, mensaje.Contenido);
        var d = new Acumulador(tipoDelMensaje, sobre, fecha, partidas);

        switch (mensaje.Contenido)
        {
            case VentaFacturadaV1 venta:
                Invariantes(venta.Lines, venta.Taxes, venta.Payments, venta.Totals, tipoDelMensaje, fallos);
                Venta(d, venta.Operation, venta.PointOfSaleCode, venta.Lines, venta.Taxes, venta.Payments, nota: false);
                break;
            case NotaDebitoEmitidaV1 debito:
                Invariantes(debito.Lines, debito.Taxes, debito.Payments, debito.Totals, tipoDelMensaje, fallos);
                Venta(d, debito.Operation, debito.PointOfSaleCode, debito.Lines, debito.Taxes, debito.Payments, nota: false);
                break;
            case NotaCreditoEmitidaV1 credito:
                Invariantes(credito.Lines, credito.Taxes, credito.Payments, credito.Totals, tipoDelMensaje, fallos);
                Venta(d, credito.Operation, credito.PointOfSaleCode, credito.Lines, credito.Taxes, credito.Payments, nota: true);
                break;
            case CostoDeVentaReconocidoV1 costo:
                foreach (var l in costo.Lines)
                {
                    d.Agregar(costo.Operation, R.Costo, true, l.Cost, Grupo(l, sobre), l.DocumentLines, dim: l.AccountingGroupCode);
                    d.Agregar(costo.Operation, R.RolDeLaBodega(R.Inventario, l.WarehouseBehavior), false, l.Cost, Grupo(l, sobre), l.DocumentLines, dim: l.AccountingGroupCode);
                }
                break;
            case CompraRecibidaV1 compra:
                foreach (var l in compra.Lines)
                {
                    d.Agregar(compra.Operation, R.RolDeLaBodega(R.Inventario, l.WarehouseBehavior), true, l.Cost, Grupo(l, sobre), l.DocumentLines, dim: l.AccountingGroupCode);
                    d.Agregar(compra.Operation, R.MercanciaPorFacturar, false, l.Cost, Grupo(l, sobre), l.DocumentLines, tercero: sobre.PersonPublicId, dim: l.AccountingGroupCode);
                }
                break;
            case FacturaProveedorRegistradaV1 factura:
                FacturaDelProveedor(d, factura, tipoDelMensaje, fallos);
                break;
            case DevolucionRegistradaV1 devolucion:
                foreach (var l in devolucion.Lines)
                {
                    var inventario = R.RolDeLaBodega(R.Inventario, l.WarehouseBehavior);
                    if (devolucion.Operation == O.DevolucionAProveedor)
                    {
                        d.Agregar(devolucion.Operation, R.MercanciaPorFacturar, true, l.Cost, Grupo(l, sobre), l.DocumentLines, tercero: sobre.PersonPublicId, dim: l.AccountingGroupCode);
                        d.Agregar(devolucion.Operation, inventario, false, l.Cost, Grupo(l, sobre), l.DocumentLines, dim: l.AccountingGroupCode);
                    }
                    else
                    {
                        d.Agregar(devolucion.Operation, inventario, true, l.Cost, Grupo(l, sobre), l.DocumentLines, dim: l.AccountingGroupCode);
                        d.Agregar(devolucion.Operation, R.Costo, false, l.Cost, Grupo(l, sobre), l.DocumentLines, dim: l.AccountingGroupCode);
                    }
                }
                break;
            case AjusteInventarioAprobadoV1 ajuste:
                Ajuste(d, ajuste, sobre);
                break;
            case TrasladoDespachadoV1 despacho:
                Traslado(d, despacho.Operation, despacho.Lines, sobre);
                break;
            case TrasladoRecibidoV1 recepcion:
                Traslado(d, recepcion.Operation, recepcion.Lines, sobre);
                break;
            case AjusteDeCostoReconocidoV1 costo:
                foreach (var l in costo.Lines)
                {
                    var valores = new ValoresBuscados(l.AccountingGroupCode, l.WarehouseCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
                    d.Agregar(costo.Operation, R.RolDeLaBodega(R.Inventario, l.WarehouseBehavior), true, l.InventoryAmount, valores, [], dim: l.AccountingGroupCode);
                    d.Agregar(costo.Operation, R.Costo, true, l.SoldAmount, valores, [], dim: l.AccountingGroupCode);
                    var contrapartida = costo.Reason == KardexReason.RoundingResidue ? R.Redondeo : R.Contrapartida;
                    var motivo = contrapartida == R.Contrapartida ? costo.Reason.ToString() : null;
                    d.Agregar(costo.Operation, contrapartida, false, l.InventoryAmount + l.SoldAmount, valores with { ReasonCode = motivo }, [], dim: l.AccountingGroupCode);
                }
                break;
            case GrupoContableReclasificadoV1 reclasificacion:
                foreach (var l in reclasificacion.Lines)
                {
                    var rol = R.RolDeLaBodega(R.Inventario, l.WarehouseBehavior);
                    var nuevo = new ValoresBuscados(reclasificacion.ToAccountingGroupCode, l.WarehouseCode, BranchPublicId: l.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
                    d.Agregar(reclasificacion.Operation, rol, true, l.Value, nuevo, [], dim: reclasificacion.ToAccountingGroupCode);
                    d.Agregar(reclasificacion.Operation, rol, false, l.Value, nuevo with { AccountingGroupCode = reclasificacion.FromAccountingGroupCode }, [],
                        dim: reclasificacion.FromAccountingGroupCode);
                }
                break;
            case MovimientoDeCajaRegistradoV1 caja:
                MovimientoDeCaja(d, caja, sobre);
                break;
            case DiferenciaDeArqueoAprobadaV1 arqueo:
                foreach (var l in arqueo.Lines)
                {
                    var medio = new ValoresBuscados(PaymentMeansCode: l.PaymentMeansCode, PointOfSaleCode: arqueo.PointOfSaleCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
                    if (l.Difference > 0m)
                    {
                        d.Agregar(arqueo.Operation, R.MedioDePago, true, l.Difference, medio, [], dim: l.PaymentMeansCode);
                        d.Agregar(arqueo.Operation, R.Sobrante, false, l.Difference, medio with { PaymentMeansCode = null, ReasonCode = nameof(CashDifferenceTreatment.Surplus) }, [], dim: l.PaymentMeansCode);
                    }
                    else if (l.Difference < 0m)
                    {
                        var faltante = -l.Difference;
                        var aCajero = l.Treatment == CashDifferenceTreatment.ShortageToCashier;
                        d.Agregar(arqueo.Operation, aCajero ? R.Faltante : R.GastoDeArqueo, true, faltante,
                            medio with { PaymentMeansCode = null, ReasonCode = l.Treatment.ToString() }, [],
                            tercero: aCajero ? arqueo.Cashier.PersonPublicId : null, dim: l.PaymentMeansCode);
                        d.Agregar(arqueo.Operation, R.MedioDePago, false, faltante, medio, [], dim: l.PaymentMeansCode);
                    }
                }
                break;
        }
    }

    private static ValoresBuscados Grupo(CostLineV1 l, IntegrationEnvelopeV1 sobre) =>
        new(l.AccountingGroupCode, l.WarehouseCode, BranchPublicId: l.BranchPublicId ?? sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);

    private static void Venta(
        Acumulador d, string operacion, string? punto, IReadOnlyList<SalesAmountLineV1> lineas, IReadOnlyList<TaxLineV1> impuestos,
        IReadOnlyList<PaymentLineV1> pagos, bool nota)
    {
        var sobre = d.Sobre;
        // La nota crédito invierte la venta (§3.3): devolución e impuesto al débito, descuento, retención y reintegros al crédito.
        foreach (var p in pagos)
        {
            var valores = new ValoresBuscados(PaymentMeansCode: p.PaymentMeansCode, PointOfSaleCode: punto, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
            // Un reintegro a un medio de crédito salda la venta: cruza contra ella (§3.2).
            var cruce = nota && sobre.Related is { } original ? new CruceDeLaPartida(O.Venta, original.Number) : null;
            d.Agregar(operacion, R.MedioDePago, p.Direction == PaymentDirection.Received, p.Amount, valores, [p.LineNumber],
                tercero: p.ThirdPartyPersonPublicId, cruce: cruce, dim: p.PaymentMeansCode,
                credito: p.PaymentMeansClass is PaymentMeansClass.AssociateCredit or PaymentMeansClass.CustomerCredit);
        }
        foreach (var l in lineas)
        {
            var valores = new ValoresBuscados(l.AccountingGroupCode, l.WarehouseCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
            d.Agregar(operacion, R.Descuento, !nota, l.DiscountAmount, valores, l.DocumentLines, dim: l.AccountingGroupCode);
            d.Agregar(operacion, nota ? R.Devolucion : R.Ingreso, nota, l.GrossAmount, valores, l.DocumentLines, dim: l.AccountingGroupCode);
        }
        foreach (var t in impuestos)
        {
            var valores = new ValoresBuscados(TaxRateCode: t.TaxRateCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
            if (t.Treatment == TaxTreatment.Generated)
                d.Agregar(operacion, R.Impuesto, nota, t.Amount, valores, t.DocumentLines, tercero: sobre.PersonPublicId, tarifa: t.Rate, @base: t.TaxableBase, dim: t.TaxRateCode);
            else if (t.Treatment == TaxTreatment.WithholdingSuffered)
                d.Agregar(operacion, R.Retencion, !nota, t.Amount, valores, t.DocumentLines, tercero: sobre.PersonPublicId, tarifa: t.Rate, @base: t.TaxableBase, dim: t.TaxRateCode);
        }
    }

    private static void FacturaDelProveedor(Acumulador d, FacturaProveedorRegistradaV1 f, string tipoDelMensaje, List<FalloDeConstruccion> fallos)
    {
        var sobre = d.Sobre;
        var t = f.Totals;
        var diferencia = new[]
        {
            f.Lines.Sum(l => l.NetAmount) - (t.Subtotal - t.DiscountTotal),
            t.Total - (t.Subtotal - t.DiscountTotal + t.TaxTotal),
            t.AmountPayable - (t.Total - t.WithholdingTotal),
            f.Lines.Sum(l => l.TaxAddedToCost) - f.Taxes.Where(x => x.Treatment == TaxTreatment.AddedToCost).Sum(x => x.Amount),
        }.FirstOrDefault(x => x != 0m);
        if (diferencia != 0m) fallos.Add(new FalloDeConstruccion(AccountingErrors.InventoryMessageUnbalanced(diferencia), tipoDelMensaje, [], null));

        // El número del proveedor es el cruce de la factura; una nota del proveedor salda su factura (§3.2).
        var numeroDelProveedor = $"{f.SupplierDocument.Prefix}{f.SupplierDocument.Number}";
        var esNota = f.SupplierDocument.Kind is "CreditNote" or "DebitNote" or "SupportDocumentAdjustmentNote";
        var cruce = esNota && sobre.Related is { } original
            ? new CruceDeLaPartida(O.FacturaProveedor, original.Number)
            : new CruceDeLaPartida(O.FacturaProveedor, numeroDelProveedor);

        foreach (var l in f.Lines)
        {
            var valores = new ValoresBuscados(l.AccountingGroupCode, l.WarehouseCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
            var rol = l.LineKind == "Service" ? R.Contrapartida : R.MercanciaPorFacturar;
            d.Agregar(f.Operation, rol, true, l.NetAmount + l.TaxAddedToCost, valores, l.DocumentLines,
                tercero: rol == R.MercanciaPorFacturar ? sobre.PersonPublicId : null, cruce: cruce, dim: l.AccountingGroupCode);
        }
        foreach (var x in f.Taxes)
        {
            var valores = new ValoresBuscados(TaxRateCode: x.TaxRateCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
            if (x.Treatment == TaxTreatment.Deductible)
                d.Agregar(f.Operation, R.Impuesto, true, x.Amount, valores, x.DocumentLines, tercero: sobre.PersonPublicId, tarifa: x.Rate, @base: x.TaxableBase, cruce: cruce, dim: x.TaxRateCode);
            else if (x.Treatment == TaxTreatment.WithholdingApplied)
                d.Agregar(f.Operation, R.Retencion, false, x.Amount, valores, x.DocumentLines, tercero: sobre.PersonPublicId, tarifa: x.Rate, @base: x.TaxableBase, cruce: cruce, dim: x.TaxRateCode);
        }
        d.Agregar(f.Operation, R.CuentaPorPagar, false, t.AmountPayable,
            new ValoresBuscados(BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId),
            f.Lines.SelectMany(l => l.DocumentLines).Distinct().Order().ToList(), tercero: sobre.PersonPublicId, cruce: cruce, dim: numeroDelProveedor);
    }

    private static void Ajuste(Acumulador d, AjusteInventarioAprobadoV1 a, IntegrationEnvelopeV1 sobre)
    {
        var op = a.Operation;
        foreach (var l in a.Lines)
        {
            var valores = Grupo(l, sobre);
            var inventario = R.RolDeLaBodega(R.Inventario, l.WarehouseBehavior);
            if (op == O.Ensamble)
            {
                d.Agregar(op, inventario, l.Movement == KardexEntryKind.Entry, l.Cost, valores, l.DocumentLines, dim: l.AccountingGroupCode);
                continue;
            }
            var entrada = op == O.AjustePositivo;
            var motivo = R.ContrapartidaExigeMotivo(op) ? a.CauseCode : null;
            d.Agregar(op, inventario, entrada, l.Cost, valores, l.DocumentLines, dim: l.AccountingGroupCode);
            d.Agregar(op, R.Contrapartida, !entrada, l.Cost, valores with { ReasonCode = motivo }, l.DocumentLines, dim: l.AccountingGroupCode);
        }
        if (op == O.RetiroGravado && a.TaxableWithdrawal is { } retiro)
        {
            foreach (var t in retiro.Taxes)
            {
                var valores = new ValoresBuscados(BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
                d.Agregar(op, R.Contrapartida, true, t.Amount, valores, t.DocumentLines, dim: t.TaxRateCode);
                d.Agregar(op, R.Impuesto, false, t.Amount, valores with { TaxRateCode = t.TaxRateCode }, t.DocumentLines,
                    tercero: sobre.PersonPublicId, tarifa: t.Rate, @base: t.TaxableBase == 0m ? retiro.Base : t.TaxableBase, dim: t.TaxRateCode);
            }
        }
    }

    private static void Traslado(Acumulador d, string operacion, IReadOnlyList<TransferCostLineV1> lineas, IntegrationEnvelopeV1 sobre)
    {
        foreach (var l in lineas)
        {
            d.Agregar(operacion, R.RolDeLaBodega(R.Inventario, l.ToWarehouseBehavior), true, l.Cost,
                new ValoresBuscados(l.AccountingGroupCode, l.ToWarehouseCode, BranchPublicId: l.ToBranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId),
                l.DocumentLines, dim: l.AccountingGroupCode);
            d.Agregar(operacion, R.RolDeLaBodega(R.Inventario, l.FromWarehouseBehavior), false, l.Cost,
                new ValoresBuscados(l.AccountingGroupCode, l.FromWarehouseCode, BranchPublicId: l.FromBranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId),
                l.DocumentLines, dim: l.AccountingGroupCode);
        }
    }

    private static void MovimientoDeCaja(Acumulador d, MovimientoDeCajaRegistradoV1 m, IntegrationEnvelopeV1 sobre)
    {
        var op = m.Operation;
        var origen = new ValoresBuscados(PaymentMeansCode: m.PaymentMeansCode, PointOfSaleCode: m.PointOfSaleCode, BranchPublicId: sobre.BranchPublicId, CostCenterPublicId: sobre.CostCenterPublicId);
        switch (m.MovementKind)
        {
            case CashMovementKind.WithdrawalToSafe:
            case CashMovementKind.WithdrawalForDeposit:
                var destino = m.MovementKind == CashMovementKind.WithdrawalToSafe ? nameof(CashMovementDestination.Safe) : nameof(CashMovementDestination.Deposit);
                d.Agregar(op, R.CajaDestino, true, m.Amount, origen with { PaymentMeansCode = null, ReasonCode = m.Destination?.ToString() ?? destino }, [], dim: destino);
                d.Agregar(op, R.MedioDePago, false, m.Amount, origen, [], dim: m.PaymentMeansCode);
                break;
            case CashMovementKind.WithdrawalToRegister:
                d.Agregar(op, R.MedioDePago, true, m.Amount, origen with { PointOfSaleCode = m.DestinationPointOfSaleCode ?? m.PointOfSaleCode }, [], dim: m.PaymentMeansCode);
                d.Agregar(op, R.MedioDePago, false, m.Amount, origen, [], dim: m.PaymentMeansCode);
                break;
            case CashMovementKind.BaseIncome:
                d.Agregar(op, R.MedioDePago, true, m.Amount, origen, [], dim: m.PaymentMeansCode);
                d.Agregar(op, R.CajaDestino, false, m.Amount, origen with { PaymentMeansCode = null, ReasonCode = nameof(CashMovementDestination.Safe) }, [], dim: nameof(CashMovementDestination.Safe));
                break;
            case CashMovementKind.ReclassificationBetweenMeans:
                d.Agregar(op, R.MedioDePago, true, m.Amount, origen with { PaymentMeansCode = m.DestinationPaymentMeansCode }, [], dim: m.DestinationPaymentMeansCode);
                d.Agregar(op, R.MedioDePago, false, m.Amount, origen, [], dim: m.PaymentMeansCode);
                break;
        }
    }

    private static void Invariantes(
        IReadOnlyList<SalesAmountLineV1> lineas, IReadOnlyList<TaxLineV1> impuestos, IReadOnlyList<PaymentLineV1> pagos, SalesTotalsV1 t,
        string tipoDelMensaje, List<FalloDeConstruccion> fallos)
    {
        // mensajes.md §6.1 y §6.11: los pagos en la dirección de la operación (recibidos en la venta, reintegrados en la nota).
        var diferencia = new[]
        {
            lineas.Sum(l => l.GrossAmount) - t.Subtotal,
            lineas.Sum(l => l.DiscountAmount) - t.DiscountTotal,
            impuestos.Where(x => x.Treatment == TaxTreatment.Generated).Sum(x => x.Amount) - t.TaxTotal,
            impuestos.Where(x => x.Treatment == TaxTreatment.WithholdingSuffered).Sum(x => x.Amount) - t.WithholdingTotal,
            t.Total - (t.Subtotal - t.DiscountTotal + t.TaxTotal),
            t.AmountDue - (t.Total - t.WithholdingTotal),
            pagos.Sum(p => p.Amount) - t.AmountDue,
        }.FirstOrDefault(x => x != 0m);
        if (diferencia != 0m) fallos.Add(new FalloDeConstruccion(AccountingErrors.InventoryMessageUnbalanced(diferencia), tipoDelMensaje, [], null));
    }

    private sealed class Acumulador(string tipoDelMensaje, IntegrationEnvelopeV1 sobre, DateOnly fechaDeReglas, List<Partida> destino)
    {
        public IntegrationEnvelopeV1 Sobre { get; } = sobre;

        public void Agregar(
            string operacion, string rol, bool debitoSiPositivo, decimal importe, ValoresBuscados valores, IReadOnlyList<int> lineas,
            Guid? tercero = null, decimal? tarifa = null, decimal? @base = null, CruceDeLaPartida? cruce = null, string? dim = null, bool credito = false)
        {
            var detalle = string.IsNullOrWhiteSpace(dim) ? Sobre.Origin.Number : $"{Sobre.Origin.Number} · {dim}";
            destino.Add(new Partida(tipoDelMensaje, operacion, rol, debitoSiPositivo, importe, valores, fechaDeReglas, lineas,
                tercero, Sobre.PersonPublicId, tarifa, @base, cruce, detalle, Sobre.Origin.Number, credito));
        }
    }

    // =================================================================================================== paso 2 --

    /// <summary>
    /// El comprobante de la unidad con la matriz y los mapeos cargados. <paramref name="origen"/> nulo = el documento de la unidad
    /// (o el producto en una reclasificación).
    /// </summary>
    public static ConstruccionDeUnidad Construir(IReadOnlyList<MensajeDeUnidad> unidad, CatalogosDelConstructor c, AccountingOrigin? origen = null)
    {
        ArgumentNullException.ThrowIfNull(c);
        var desglose = Desglosar(unidad);
        var principal = desglose.Principal ?? throw new ArgumentException("La unidad no trae mensajes.", nameof(unidad));
        var fecha = FechaDelComprobante(principal);
        var fallos = new List<FalloDeConstruccion>(desglose.Fallos);

        var operacion = TiposDeComprobanteDeInventario.OperacionDe(unidad.Select(m => (m.Sobre, m.Contenido)).ToList());
        TipoDeLaUnidad? tipo = null;
        if (operacion is null)
        {
            fallos.Add(new FalloDeConstruccion(AccountingErrors.VoucherTypeNotFound($"del mensaje {principal.Tipo}"), principal.Tipo, [], null));
        }
        else
        {
            var resuelto = c.Mapeos.Resolver(operacion);
            if (resuelto.IsFailure) fallos.Add(new FalloDeConstruccion(resuelto.Error, principal.Tipo, [], null));
            else tipo = resuelto.Value;
        }

        // Una partida con importe resuelve su regla; la cuenta decide tercero, cruce, centro y base (§3.2).
        var lineas = new List<(PostingLine Linea, LineaDeLaUnidad Mapa, string? Clave)>();
        foreach (var p in desglose.Partidas)
        {
            if (p.Importe == 0m) continue;
            var peticion = new PeticionDeRegla(p.Operacion, p.Rol, p.Valores, p.FechaDeReglas, p.Importe, p.DocumentLines, p.EsImpuesto ? p.Tarifa : null);
            var resolucion = c.Matriz.Resolver(peticion);
            if (resolucion.IsFailure)
            {
                var deLaCuenta = resolucion.Error.Code == "Accounting.InventoryRule.TaxRateMismatch"
                    && c.Matriz.Resolver(peticion with { TarifaDelMensaje = null }) is { IsSuccess: true, Value: { } sinTarifa }
                    && (sinTarifa.Regla.TaxRate is null || sinTarifa.Regla.TaxRate == p.Tarifa);
                fallos.Add(new FalloDeConstruccion(resolucion.Error, p.MessageType, p.DocumentLines, null, deLaCuenta));
                continue;
            }
            var regla = resolucion.Value!;
            var cuenta = regla.CuentaParaReglas;
            var rol = RolesDeCuenta.Buscar(p.Rol);

            // I3 (T659; contabilidad.md §3.4; T32): el pago a crédito provisional va a una cuenta por cobrar que exige tercero (el cliente)
            // y documento cruce (FV + número de la venta): así la vista pending-documents de la 009 sirve de cartera provisional.
            if (p.EsCredito && p.Rol == R.MedioDePago && !(cuenta.RequiresThirdParty && cuenta.RequiresCrossDocument))
            {
                fallos.Add(new FalloDeConstruccion(AccountingErrors.InventoryCreditAccountRequirements(regla.Cuenta.Code, p.Valores.PaymentMeansCode),
                    p.MessageType, p.DocumentLines, regla.Cuenta.Code));
                continue;
            }

            int? persona = null;
            if ((rol?.SiempreLlevaTercero ?? false) || cuenta.RequiresThirdParty)
            {
                var guid = p.TerceroNatural ?? (cuenta.RequiresThirdParty ? p.TerceroDelSobre : null);
                persona = guid is { } g && c.Personas.TryGetValue(g, out var id) ? id : null;
            }

            string? tipoDeCruce = null, numeroDeCruce = null;
            if (cuenta.RequiresCrossDocument)
            {
                if (p.Cruce is { } cruce)
                {
                    tipoDeCruce = cruce.OperacionDelOriginal == tipo?.Operation
                        ? tipo?.CrossDocumentTypeCode
                        : c.Mapeos.Resolver(cruce.OperacionDelOriginal, null) is { IsSuccess: true } m ? m.Value.CrossDocumentTypeCode : null;
                    numeroDeCruce = cruce.Numero;
                }
                else
                {
                    // El documento de la partida: el de la unidad o, en el espejo de una anulación, el original (§3.7).
                    tipoDeCruce = tipo?.CrossDocumentTypeCode;
                    numeroDeCruce = p.NumeroDelDocumento;
                }
            }

            var positivo = p.Importe > 0m;
            var debito = positivo == p.DebitoSiPositivo;
            var valor = Math.Abs(p.Importe);
            var linea = new PostingLine
            {
                AccountCode = regla.Cuenta.Code,
                Debit = debito ? valor : 0m,
                Credit = debito ? 0m : valor,
                Detail = p.Detalle,
                PersonId = persona,
                BranchId = regla.BranchId,
                CostCenterId = cuenta.RequiresCostCenter ? regla.CostCenterId : null,
                CrossDocumentType = tipoDeCruce,
                CrossDocumentNumber = numeroDeCruce,
                TaxBase = p.EsImpuesto && cuenta.RequiresTaxBase && p.Base is { } b ? Math.Abs(b) : null,
            };
            var conservaDetalle = cuenta.RequiresThirdParty || cuenta.RequiresCrossDocument || cuenta.RequiresTaxBase;
            var mapa = new LineaDeLaUnidad(p.MessageType, p.Rol, p.DocumentLines, regla.Cuenta.Code, regla.Cuenta.Name, p.EsImpuesto, conservaDetalle);
            var clave = p.EsImpuesto
                ? null
                : string.Join('|', linea.AccountCode, debito ? "D" : "C", linea.BranchId, linea.CostCenterId, linea.PersonId, linea.CrossDocumentType, linea.CrossDocumentNumber);
            lineas.Add((linea, mapa, clave));
        }

        if (fallos.Count > 0)
            return new ConstruccionDeUnidad(null, tipo, [], fallos, fecha, principal);

        var sumadas = SumarIguales(lineas);
        var sobre = principal.Sobre;
        var origenDelComprobante = origen ?? OrigenDe(principal);
        var descripcion = $"{NombreDe(sobre.Origin)} {sobre.Origin.Number}".Trim();
        var request = new PostingRequest(tipo!.VoucherTypeCode, fecha, descripcion, origenDelComprobante,
            sumadas.Select(l => l.Linea).ToList(), Domain.Enums.Accounting.DocumentKind.Regular,
            new UsuarioDeOrigen(sobre.OriginUser.CentralUserId, sobre.OriginUser.Name));
        return new ConstruccionDeUnidad(request, tipo, sumadas.Select(l => l.Mapa).ToList(), [], fecha, principal);
    }

    /// <summary>Suma las líneas con la misma cuenta, lado, sucursal, centro, tercero y cruce; las de impuesto (clave nula) no se suman.</summary>
    private static List<(PostingLine Linea, LineaDeLaUnidad Mapa)> SumarIguales(List<(PostingLine Linea, LineaDeLaUnidad Mapa, string? Clave)> lineas)
    {
        var resultado = new List<(PostingLine Linea, LineaDeLaUnidad Mapa)>();
        var indice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (linea, mapa, clave) in lineas)
        {
            if (clave is not null && indice.TryGetValue(clave, out var i))
            {
                var (previa, mapaPrevio) = resultado[i];
                resultado[i] = (
                    previa with { Debit = previa.Debit + linea.Debit, Credit = previa.Credit + linea.Credit },
                    mapaPrevio with { DocumentLines = mapaPrevio.DocumentLines.Concat(mapa.DocumentLines).Distinct().Order().ToList() });
                continue;
            }
            if (clave is not null) indice[clave] = resultado.Count;
            resultado.Add((linea, mapa));
        }
        return resultado;
    }

    /// <summary>El origen del comprobante por documento (§3.2): el documento, o el producto en una reclasificación.</summary>
    public static AccountingOrigin OrigenDe(MensajeDeUnidad principal) => principal.Contenido is GrupoContableReclasificadoV1 reclasificacion
        ? new AccountingOrigin(ModuloContable.Inventario, OrigenesDeInventario.Producto, reclasificacion.ProductPublicId)
        : new AccountingOrigin(ModuloContable.Inventario, OrigenesDeInventario.Documento, principal.Sobre.Origin.PublicId);

    /// <summary>El nombre de la clase de documento para la descripción del comprobante («Documento equivalente POS PV01-1532»).</summary>
    public static string NombreDe(MessageOriginV1 origen) => origen.DocumentClass switch
    {
        DocumentClass.PurchaseReceipt => "Recepción de compra",
        DocumentClass.SupplierInvoice => "Factura del proveedor",
        DocumentClass.SupplierNote => "Nota del proveedor",
        DocumentClass.SupportDocument => "Documento soporte",
        DocumentClass.SupportDocumentAdjustmentNote => "Nota de ajuste al documento soporte",
        DocumentClass.LandedCost => "Costos adicionales",
        DocumentClass.SupplierReturn => "Devolución a proveedor",
        DocumentClass.PositiveAdjustment => "Ajuste positivo",
        DocumentClass.NegativeAdjustment => "Ajuste negativo",
        DocumentClass.InternalConsumption => "Consumo interno",
        DocumentClass.WriteOff => "Baja",
        DocumentClass.Assembly => "Ensamble",
        DocumentClass.OpeningBalance => "Saldo inicial",
        DocumentClass.TransferDispatch => "Despacho de traslado",
        DocumentClass.TransferReceipt => "Recepción de traslado",
        DocumentClass.CostAdjustment => "Ajuste de costo",
        DocumentClass.CashMovement => "Movimiento de caja",
        DocumentClass.CashCountDifference => "Diferencia de arqueo",
        DocumentClass.Shipment => "Remisión",
        DocumentClass.SalesInvoice => "Factura de venta",
        DocumentClass.SalesInvoiceFromShipments => "Factura desde remisiones",
        DocumentClass.PosEquivalentDocument => "Documento equivalente POS",
        DocumentClass.NonElectronicSalesReceipt => "Recibo de venta",
        DocumentClass.NonElectronicSalesNote => "Nota de venta",
        DocumentClass.CreditNote => "Nota crédito",
        DocumentClass.PosAdjustmentNote => "Nota de ajuste POS",
        DocumentClass.DebitNote => "Nota débito",
        DocumentClass.Voiding => "Anulación",
        null => "Operación",
        _ => origen.DocumentTypeCode ?? origen.DocumentClass.ToString()!,
    };

    /// <summary>¿El tipo de mensaje va a Contabilidad? (los de Cartera no se evalúan ni se contabilizan aquí).</summary>
    public static bool VaAContabilidad(string tipo) =>
        CatalogoDeMensajesV1.Todos.Any(t => t.Type == tipo && t.Destination == IntegrationDestinations.Accounting);

    /// <summary>¿La unidad es informativa? (sólo recibo, sin comprobante; §3.9).</summary>
    public static bool EsInformativa(IReadOnlyList<MensajeDeUnidad> unidad) =>
        unidad.Count > 0 && unidad.All(m => m.Sobre.Kind == IntegrationMessageKind.Informational);
}
