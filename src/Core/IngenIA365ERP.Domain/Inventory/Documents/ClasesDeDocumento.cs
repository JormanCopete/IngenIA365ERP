using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Documents;

/// <summary>Efecto de una clase sobre las existencias (FR-036). (nuevo; no se guarda: es comportamiento de la clase)</summary>
public enum InventoryEffect
{
    /// <summary>Entra a la bodega.</summary>
    Entry = 1,
    /// <summary>Sale de la bodega.</summary>
    Exit = 2,
    /// <summary>Sale de un lugar y entra a otro (traslado, movimiento entre ubicaciones, ensamble; la anulación: el contrario del original).</summary>
    Both = 3,
    /// <summary>No mueve cantidades: sólo costo (ajuste de costo, costos adicionales).</summary>
    CostOnly = 4,
    /// <summary>No toca el inventario.</summary>
    None = 5,
    /// <summary>Reserva sin mover (pedido, I6).</summary>
    Reservation = 6,
}

/// <summary>Sentido fiscal de una clase fiscal: el documento lo recibe la cooperativa o lo emite. (nuevo; api.md §8 <c>fiscalDirection</c>)</summary>
public enum FiscalDirection { Received = 1, Emitted = 2 }

/// <summary>Quién numera la clase (T16). (nuevo; api.md §8 <c>numberedBy</c>)</summary>
public enum NumberedBy
{
    /// <summary><c>INV_DocumentSequences</c> por tipo y prefijo, con <c>Numerador</c>.</summary>
    Sequence = 1,
    /// <summary><c>COR_DianNumberingResolutions.LastIssuedNumber</c> con <c>NumeradorFiscal</c> (I4).</summary>
    DianResolution = 2,
}

/// <summary>Bodegas que admite la clase en su cabecera (<c>WarehouseId</c>). (nuevo)</summary>
[Flags]
public enum AdmittedWarehouses
{
    /// <summary>La cabecera no lleva bodega (factura del proveedor de varias bodegas, costos, caja).</summary>
    None = 0,
    /// <summary>Bodega operativa y activa.</summary>
    Operational = 1,
    /// <summary>Bodega operativa todavía no activada (saldo inicial y su anulación, FR-091).</summary>
    NotActivated = 2,
    /// <summary>La bodega de tránsito de la sucursal (sólo bajas desde el tránsito al resolver una diferencia).</summary>
    Transit = 4,
}

/// <summary>
/// Lo que una clase de documento es y hace (FR-036, T17). El tipo sólo elige su clase y la parametriza; nada de esto
/// es columna de nada. (nuevo)
/// </summary>
/// <param name="Class">La clase.</param>
/// <param name="Group">Su grupo: ruta y familia de permisos. Nulo sólo en <see cref="DocumentClass.Voiding"/>, que
/// toma el del original (<see cref="ClasesDeDocumento.GrupoDe"/>).</param>
/// <param name="Effect">Efecto sobre el inventario.</param>
/// <param name="EffectOnlyWhenReturningGoods">Las notas de venta: su entrada ocurre sólo si devuelven mercancía (<c>ReturnsGoods</c>).</param>
/// <param name="FiscalDirection">Nulo = no es fiscal.</param>
/// <param name="Messages">Tipos de mensaje a Contabilidad que puede emitir (§2.6). Los de Cartera los agrega toda venta con parte a crédito (FR-062).</param>
/// <param name="Chain">Cadena de modo de paso (FR-075).</param>
/// <param name="NumberedBy">Quién numera.</param>
/// <param name="Warehouses">Bodegas que admite.</param>
/// <param name="RequiresCostCenter">Centro de costo obligatorio siempre (no sólo si el tipo lo pide).</param>
/// <param name="ManualCreation">¿Tiene alta manual por su ruta? No: el ajuste de costo (lo genera el sistema) y la anulación (la crea <c>…/void</c>).</param>
/// <param name="AvailableFrom">Entrega en que la clase se vuelve operable (decisiones-transversales §5).</param>
/// <param name="Description">Efecto en palabras, para <c>GET /document-types/classes</c>.</param>
public sealed record DescripcionDeClase(
    DocumentClass Class,
    DocumentClassGroup? Group,
    InventoryEffect Effect,
    bool EffectOnlyWhenReturningGoods,
    FiscalDirection? FiscalDirection,
    IReadOnlyList<string> Messages,
    PostingChain Chain,
    NumberedBy NumberedBy,
    AdmittedWarehouses Warehouses,
    bool RequiresCostCenter,
    bool ManualCreation,
    EntregaDelComercio AvailableFrom,
    string Description)
{
    public bool IsFiscal => FiscalDirection is not null;

    /// <summary>¿Operable en la entrega dada? (por defecto, la vigente del despliegue).</summary>
    public bool Operable(EntregaDelComercio entregaVigente = CatalogoDeParametros.EntregaVigente) => AvailableFrom <= entregaVigente;
}

/// <summary>
/// El catálogo fijo de las 34 clases de <see cref="DocumentClass"/> (FR-036, T17; data-model §5 y §5.2;
/// decisiones-transversales §2.6 y §5). Lo consumen <c>GET /api/inventory/document-types/classes</c>, los comandos de
/// tipos de documento y el ciclo común. Puro. Lo fija <c>ClasesDeDocumentoTests</c>.
/// </summary>
public static class ClasesDeDocumento
{
    // Los tipos de mensaje (§2.6). Viven como texto porque sus records están en Application
    // (Common/Integration/Contracts/Inventory) y Domain no los ve.
    public const string VentaFacturada = "VentaFacturada";
    public const string CostoDeVentaReconocido = "CostoDeVentaReconocido";
    public const string CompraRecibida = "CompraRecibida";
    public const string FacturaProveedorRegistrada = "FacturaProveedorRegistrada";
    public const string AjusteInventarioAprobado = "AjusteInventarioAprobado";
    public const string TrasladoDespachado = "TrasladoDespachado";
    public const string TrasladoRecibido = "TrasladoRecibido";
    public const string DevolucionRegistrada = "DevolucionRegistrada";
    public const string DocumentoAnulado = "DocumentoAnulado";
    public const string AjusteDeCostoReconocido = "AjusteDeCostoReconocido";
    public const string NotaCreditoEmitida = "NotaCreditoEmitida";
    public const string NotaDebitoEmitida = "NotaDebitoEmitida";
    public const string MovimientoDeCajaRegistrado = "MovimientoDeCajaRegistrado";
    public const string DiferenciaDeArqueoAprobada = "DiferenciaDeArqueoAprobada";
    public const string SaldoInicialCargado = "SaldoInicialCargado";

    private const AdmittedWarehouses Operativa = AdmittedWarehouses.Operational;
    private const AdmittedWarehouses SinBodega = AdmittedWarehouses.None;

    private static readonly IReadOnlyDictionary<DocumentClass, DescripcionDeClase> Catalogo = Construir();

    /// <summary>Las 34 clases, en el orden de su número.</summary>
    public static IReadOnlyList<DescripcionDeClase> Todas { get; } = Catalogo.Values.OrderBy(d => (int)d.Class).ToList();

    /// <summary>La descripción de una clase.</summary>
    public static DescripcionDeClase De(DocumentClass clase) => Catalogo[clase];

    /// <summary>
    /// El grupo con que se enruta y autoriza un documento. La anulación toma el del original, que es obligatorio para
    /// ella (una anulación no se anula: el original nunca es otra <see cref="DocumentClass.Voiding"/>).
    /// </summary>
    public static DocumentClassGroup GrupoDe(DocumentClass clase, DocumentClass? claseDelOriginal = null)
    {
        if (clase != DocumentClass.Voiding) return Catalogo[clase].Group!.Value;
        if (claseDelOriginal is null or DocumentClass.Voiding)
            throw new ArgumentException("La anulación toma el grupo del documento que anula; indique su clase.", nameof(claseDelOriginal));
        return Catalogo[claseDelOriginal.Value].Group!.Value;
    }

    /// <summary>El efecto real de una anulación: el contrario del original (una entrada anulada sale y al revés).</summary>
    public static InventoryEffect EfectoDeLaAnulacion(DocumentClass claseDelOriginal) => Catalogo[claseDelOriginal].Effect switch
    {
        InventoryEffect.Entry => InventoryEffect.Exit,
        InventoryEffect.Exit => InventoryEffect.Entry,
        var otro => otro,
    };

    /// <summary>Las clases de un grupo, en el orden de su número (la anulación no pertenece a ninguno).</summary>
    public static IReadOnlyList<DocumentClass> DelGrupo(DocumentClassGroup grupo) =>
        Todas.Where(d => d.Group == grupo).Select(d => d.Class).ToList();

    private static Dictionary<DocumentClass, DescripcionDeClase> Construir()
    {
        var d = new Dictionary<DocumentClass, DescripcionDeClase>();

        void C(DocumentClass clase, DocumentClassGroup? grupo, InventoryEffect efecto, FiscalDirection? fiscal, string[] mensajes,
            PostingChain cadena, EntregaDelComercio desde, AdmittedWarehouses bodegas, string descripcion,
            NumberedBy numera = NumberedBy.Sequence, bool centroDeCosto = false, bool altaManual = true, bool soloConDevolucion = false) =>
            d.Add(clase, new DescripcionDeClase(clase, grupo, efecto, soloConDevolucion, fiscal, mensajes, cadena, numera, bodegas,
                centroDeCosto, altaManual, desde, descripcion));

        const DocumentClassGroup Compras = DocumentClassGroup.Purchases;
        const DocumentClassGroup Ajustes = DocumentClassGroup.Adjustments;
        const DocumentClassGroup Ventas = DocumentClassGroup.Sales;
        const DocumentClassGroup Caja = DocumentClassGroup.Cash;
        const EntregaDelComercio I1 = EntregaDelComercio.I1, I3 = EntregaDelComercio.I3, I4 = EntregaDelComercio.I4,
            I5 = EntregaDelComercio.I5, I6 = EntregaDelComercio.I6;

        // ---- compras ----
        C(DocumentClass.PurchaseRequest, Compras, InventoryEffect.None, null, [], PostingChain.None, I5, Operativa,
            "Ninguno: pide mercancía.");
        C(DocumentClass.PurchaseOrder, Compras, InventoryEffect.None, null, [], PostingChain.None, I5, Operativa,
            "Ninguno: compromete la compra.");
        C(DocumentClass.PurchaseReceipt, Compras, InventoryEffect.Entry, null, [CompraRecibida], PostingChain.Purchases, I1, Operativa,
            "Entrada a la bodega que recibe.");
        C(DocumentClass.SupplierInvoice, Compras, InventoryEffect.None, FiscalDirection.Received,
            [FacturaProveedorRegistrada, AjusteDeCostoReconocido], PostingChain.Purchases, I1, SinBodega,
            "Ninguno; ajusta el costo si el precio difiere del recibido.");
        C(DocumentClass.SupplierNote, Compras, InventoryEffect.None, FiscalDirection.Received,
            [FacturaProveedorRegistrada, AjusteDeCostoReconocido], PostingChain.Purchases, I1, SinBodega,
            "Ninguno; ajusta el costo si cambia el precio.");
        C(DocumentClass.SupportDocument, Compras, InventoryEffect.None, FiscalDirection.Emitted,
            [FacturaProveedorRegistrada], PostingChain.Purchases, I4, SinBodega,
            "Ninguno; acompaña la recepción de un vendedor no obligado a facturar.", numera: NumberedBy.DianResolution);
        C(DocumentClass.SupportDocumentAdjustmentNote, Compras, InventoryEffect.None, FiscalDirection.Emitted,
            [FacturaProveedorRegistrada, AjusteDeCostoReconocido], PostingChain.Purchases, I4, SinBodega,
            "Ninguno; ajusta el costo si cambia el precio.");
        C(DocumentClass.LandedCost, Compras, InventoryEffect.CostOnly, null, [AjusteDeCostoReconocido], PostingChain.None, I5, SinBodega,
            "Sólo costo: reparte fletes y otros costos entre lo recibido.");
        C(DocumentClass.SupplierReturn, Compras, InventoryEffect.Exit, null, [DevolucionRegistrada, AjusteDeCostoReconocido],
            PostingChain.Purchases, I1, Operativa, "Salida de la bodega hacia el proveedor.");

        // ---- ajustes ----
        C(DocumentClass.PositiveAdjustment, Ajustes, InventoryEffect.Entry, null, [AjusteInventarioAprobado], PostingChain.None, I1, Operativa,
            "Entrada por ajuste.");
        C(DocumentClass.NegativeAdjustment, Ajustes, InventoryEffect.Exit, null, [AjusteInventarioAprobado], PostingChain.None, I1, Operativa,
            "Salida por ajuste, con causa.");
        C(DocumentClass.InternalConsumption, Ajustes, InventoryEffect.Exit, null, [AjusteInventarioAprobado], PostingChain.None, I1, Operativa,
            "Salida por consumo interno, con centro de costo; si es retiro gravado, lleva base e IVA.", centroDeCosto: true);
        C(DocumentClass.WriteOff, Ajustes, InventoryEffect.Exit, null, [AjusteInventarioAprobado], PostingChain.None, I1,
            AdmittedWarehouses.Operational | AdmittedWarehouses.Transit,
            "Salida por baja (daño, vencimiento, hurto, destrucción), con causa.");
        C(DocumentClass.Assembly, Ajustes, InventoryEffect.Both, null, [AjusteInventarioAprobado], PostingChain.None, I6, Operativa,
            "Salida de componentes y entrada del kit.");
        C(DocumentClass.LocationMove, Ajustes, InventoryEffect.Both, null, [], PostingChain.None, I1, Operativa,
            "Dentro de una bodega, de una ubicación a otra.");

        // ---- saldo inicial, traslados, conteos y costo ----
        C(DocumentClass.OpeningBalance, DocumentClassGroup.OpeningBalance, InventoryEffect.Entry, null, [SaldoInicialCargado],
            PostingChain.None, I1, AdmittedWarehouses.NotActivated, "Entrada del saldo inicial en una bodega todavía no activa.");
        C(DocumentClass.TransferDispatch, DocumentClassGroup.Transfers, InventoryEffect.Both, null, [TrasladoDespachado],
            PostingChain.Transfers, I1, Operativa, "Salida del origen y entrada a la bodega de tránsito.");
        C(DocumentClass.TransferReceipt, DocumentClassGroup.Transfers, InventoryEffect.Both, null, [TrasladoRecibido],
            PostingChain.Transfers, I1, Operativa, "Salida del tránsito y entrada al destino.");
        C(DocumentClass.PhysicalCount, DocumentClassGroup.Counts, InventoryEffect.None, null, [], PostingChain.None, I1, Operativa,
            "Ninguno; su ajuste es un ajuste.");
        C(DocumentClass.CostAdjustment, DocumentClassGroup.Costing, InventoryEffect.CostOnly, null, [AjusteDeCostoReconocido],
            PostingChain.None, I1, SinBodega, "Sólo costo: retroactivos, diferencias de precio y negativos regularizados.", altaManual: false);

        // ---- caja ----
        C(DocumentClass.CashMovement, Caja, InventoryEffect.None, null, [MovimientoDeCajaRegistrado], PostingChain.None, I3, SinBodega,
            "Ninguno: retiro parcial o ingreso de base.");
        C(DocumentClass.CashCountDifference, Caja, InventoryEffect.None, null, [DiferenciaDeArqueoAprobada], PostingChain.None, I3, SinBodega,
            "Ninguno: diferencia de arqueo aprobada.");

        // ---- ventas ----
        C(DocumentClass.SalesQuote, Ventas, InventoryEffect.None, null, [], PostingChain.None, I6, Operativa,
            "Ninguno: cotiza.");
        C(DocumentClass.SalesOrder, Ventas, InventoryEffect.Reservation, null, [], PostingChain.None, I6, Operativa,
            "Reserva la mercancía.");
        C(DocumentClass.Shipment, Ventas, InventoryEffect.Exit, null, [CostoDeVentaReconocido], PostingChain.Sales, I6, Operativa,
            "Salida por remisión.");
        C(DocumentClass.SalesInvoice, Ventas, InventoryEffect.Exit, FiscalDirection.Emitted, [VentaFacturada, CostoDeVentaReconocido],
            PostingChain.Sales, I3, Operativa, "Salida (libera la reserva del pedido).", numera: NumberedBy.DianResolution);
        C(DocumentClass.SalesInvoiceFromShipments, Ventas, InventoryEffect.None, FiscalDirection.Emitted, [VentaFacturada],
            PostingChain.Sales, I6, SinBodega, "Ninguno: factura lo que ya salió por remisiones.", numera: NumberedBy.DianResolution);
        C(DocumentClass.PosEquivalentDocument, Ventas, InventoryEffect.Exit, FiscalDirection.Emitted, [VentaFacturada, CostoDeVentaReconocido],
            PostingChain.Sales, I3, Operativa, "Salida por la venta en el punto de venta.", numera: NumberedBy.DianResolution);
        C(DocumentClass.NonElectronicSalesReceipt, Ventas, InventoryEffect.Exit, null, [VentaFacturada, CostoDeVentaReconocido],
            PostingChain.Sales, I3, Operativa, "Salida por la venta (cooperativa no obligada a facturar electrónicamente).");
        C(DocumentClass.NonElectronicSalesNote, Ventas, InventoryEffect.Entry, null, [NotaCreditoEmitida, DevolucionRegistrada],
            PostingChain.Sales, I3, Operativa, "Entrada al costo con que salió si devuelve mercancía; si no, ninguno.", soloConDevolucion: true);
        C(DocumentClass.CreditNote, Ventas, InventoryEffect.Entry, FiscalDirection.Emitted, [NotaCreditoEmitida, DevolucionRegistrada],
            PostingChain.Sales, I3, Operativa, "Entrada al costo con que salió si devuelve mercancía; si no, ninguno.", soloConDevolucion: true);
        C(DocumentClass.PosAdjustmentNote, Ventas, InventoryEffect.Entry, FiscalDirection.Emitted, [NotaCreditoEmitida, DevolucionRegistrada],
            PostingChain.Sales, I3, Operativa, "Entrada al costo con que salió si devuelve mercancía; si no, ninguno.", soloConDevolucion: true);
        C(DocumentClass.DebitNote, Ventas, InventoryEffect.None, FiscalDirection.Emitted, [NotaDebitoEmitida],
            PostingChain.Sales, I6, SinBodega, "Ninguno.");

        // ---- anulación ----
        C(DocumentClass.Voiding, null, InventoryEffect.Both, null, [DocumentoAnulado, AjusteDeCostoReconocido], PostingChain.None, I1,
            AdmittedWarehouses.Operational | AdmittedWarehouses.NotActivated,
            "El contrario del original, al costo del original, en su propia fecha.", altaManual: false);

        return d;
    }
}
