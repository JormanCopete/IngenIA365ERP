namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// Las etiquetas en español y los nombres de los enums de ventas, caja y medios de pago (feature 012, I3, T633; decisiones-transversales
/// §2.5: «valores en inglés; la pantalla traduce»). La API los entrega como número y los recibe por nombre o número: las pantallas los
/// muestran con la etiqueta y los mandan con el nombre. Los números y los nombres son los de <c>Domain.Enums.Core</c>,
/// <c>Domain.Enums.Inventory</c> y <c>CashRegisterPrintFormat</c> de Application; lo fija <c>TextosDeVentasTests</c>. (nuevo)
/// </summary>
public static class TextosDeVentas
{
    // ------------------------------------------------------------------------------------ constantes --

    public const int MedioEfectivo = 1;
    public const int MedioTarjetaCredito = 2;
    public const int MedioTarjetaDebito = 3;
    public const int MedioCreditoAsociado = 4;
    public const int MedioCreditoCliente = 5;
    public const int MedioBono = 8;

    public const int ArqueoPorDenominaciones = 1;
    public const int ArqueoPorLote = 2;
    public const int ArqueoPorReferencias = 3;

    public const int SesionAbierta = 1;

    public const int RolVentaPos = 1;
    public const int RolFacturaAPedido = 2;

    /// <summary><c>DocumentClassGroup</c> de las ventas y de la caja (los tipos que se asignan a una caja o a un medio).</summary>
    public const int GrupoDeVentas = 5;
    public const int GrupoDeCaja = 6;

    /// <summary>Las clases de venta que se consultan en <c>/ventas/documentos</c>: factura, documento equivalente POS y comprobante no electrónico.</summary>
    public static readonly int[] ClasesDeVenta = [26, 28, 29];

    /// <summary>Las notas de venta de I3: al comprobante no electrónico, crédito y de ajuste POS.</summary>
    public static readonly int[] ClasesDeNota = [30, 31, 32];

    /// <summary>Un medio de crédito (asociado o cliente): pide cuotas, plazo y periodicidad (FR-096).</summary>
    public static bool EsCredito(int clase) => clase is MedioCreditoAsociado or MedioCreditoCliente;

    /// <summary>Un medio de tarjeta: datáfono, autorización y los últimos cuatro dígitos.</summary>
    public static bool EsTarjeta(int clase) => clase is MedioTarjetaCredito or MedioTarjetaDebito;

    // ---------------------------------------------------------------------------------- medios de pago --

    /// <summary><c>PaymentMeansClass</c>.</summary>
    public static IReadOnlyDictionary<int, string> ClasesDeMedio { get; } = new Dictionary<int, string>
    {
        [1] = "Efectivo",
        [2] = "Tarjeta de crédito",
        [3] = "Tarjeta débito",
        [4] = "Crédito de asociado",
        [5] = "Crédito de cliente",
        [6] = "Consignación",
        [7] = "Transferencia",
        [8] = "Bono",
        [9] = "Cheque",
        [99] = "Otro",
    };

    public static IReadOnlyDictionary<int, string> NombresDeClaseDeMedio { get; } = new Dictionary<int, string>
    {
        [1] = "Cash",
        [2] = "CreditCard",
        [3] = "DebitCard",
        [4] = "AssociateCredit",
        [5] = "CustomerCredit",
        [6] = "BankDeposit",
        [7] = "Transfer",
        [8] = "Voucher",
        [9] = "Check",
        [99] = "Other",
    };

    /// <summary><c>CardKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> TiposDeTarjeta { get; } = new Dictionary<int, string>
    {
        [1] = "Crédito",
        [2] = "Débito",
        [3] = "Crédito y débito",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTipoDeTarjeta { get; } = new Dictionary<int, string>
    {
        [1] = "Credit",
        [2] = "Debit",
        [3] = "Both",
    };

    /// <summary><c>PaymentReferenceKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> TiposDeReferencia { get; } = new Dictionary<int, string>
    {
        [1] = "Número de aprobación",
        [2] = "Número de recibo",
        [3] = "Número de consignación",
        [4] = "Número de bono",
        [5] = "Número de cheque",
        [99] = "Otra",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTipoDeReferencia { get; } = new Dictionary<int, string>
    {
        [1] = "Approval",
        [2] = "Receipt",
        [3] = "Deposit",
        [4] = "VoucherNumber",
        [5] = "CheckNumber",
        [99] = "Other",
    };

    /// <summary><c>CashCountMethod</c>.</summary>
    public static IReadOnlyDictionary<int, string> FormasDeArqueo { get; } = new Dictionary<int, string>
    {
        [1] = "Conteo físico por denominaciones",
        [2] = "Total del lote del datáfono",
        [3] = "Cotejo por referencias",
        [4] = "No se arquea",
    };

    public static IReadOnlyDictionary<int, string> NombresDeFormaDeArqueo { get; } = new Dictionary<int, string>
    {
        [1] = "PhysicalCount",
        [2] = "VoucherTotal",
        [3] = "ByReference",
        [4] = "None",
    };

    /// <summary><c>CashDenominationKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> TiposDeDenominacion { get; } = new Dictionary<int, string>
    {
        [1] = "Billete",
        [2] = "Moneda",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTipoDeDenominacion { get; } = new Dictionary<int, string>
    {
        [1] = "Bill",
        [2] = "Coin",
    };

    // ---------------------------------------------------------------------------------- puntos y cajas --

    /// <summary><c>CashRegisterDocumentRole</c>: los seis roles de tipo de una caja (§20.1).</summary>
    public static IReadOnlyDictionary<int, string> RolesDeCaja { get; } = new Dictionary<int, string>
    {
        [1] = "Venta POS",
        [2] = "Factura a pedido del comprador",
        [3] = "Nota de ajuste POS",
        [4] = "Nota crédito de factura",
        [5] = "Venta POS en contingencia",
        [6] = "Factura en contingencia",
    };

    public static IReadOnlyDictionary<int, string> NombresDeRolDeCaja { get; } = new Dictionary<int, string>
    {
        [1] = "PosSale",
        [2] = "InvoiceOnRequest",
        [3] = "PosAdjustmentNote",
        [4] = "InvoiceCreditNote",
        [5] = "PosSaleContingency",
        [6] = "InvoiceContingency",
    };

    /// <summary><c>CashRegisterPrintFormat</c>: el valor es el ancho en milímetros.</summary>
    public static IReadOnlyDictionary<int, string> FormatosDeImpresion { get; } = new Dictionary<int, string>
    {
        [58] = "Tirilla de 58 mm",
        [80] = "Tirilla de 80 mm",
        [216] = "Carta",
    };

    public static IReadOnlyDictionary<int, string> NombresDeFormatoDeImpresion { get; } = new Dictionary<int, string>
    {
        [58] = "Ticket58",
        [80] = "Ticket80",
        [216] = "Letter",
    };

    // ------------------------------------------------------------------------------------------ caja --

    /// <summary><c>CashSessionStatus</c>.</summary>
    public static IReadOnlyDictionary<int, string> EstadosDeSesion { get; } = new Dictionary<int, string>
    {
        [1] = "Abierta",
        [2] = "Cerrada",
    };

    public static IReadOnlyDictionary<int, string> NombresDeEstadoDeSesion { get; } = new Dictionary<int, string>
    {
        [1] = "Open",
        [2] = "Closed",
    };

    /// <summary><c>CashMovementKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> ClasesDeMovimiento { get; } = new Dictionary<int, string>
    {
        [1] = "Retiro a caja fuerte",
        [2] = "Traslado a otra caja",
        [3] = "Retiro para consignar",
        [4] = "Ingreso de base",
        [5] = "Reclasificación entre medios",
    };

    public static IReadOnlyDictionary<int, string> NombresDeClaseDeMovimiento { get; } = new Dictionary<int, string>
    {
        [1] = "WithdrawalToSafe",
        [2] = "WithdrawalToRegister",
        [3] = "WithdrawalForDeposit",
        [4] = "BaseIncome",
        [5] = "ReclassificationBetweenMeans",
    };

    /// <summary><c>CashMovementDestination</c>.</summary>
    public static IReadOnlyDictionary<int, string> DestinosDeMovimiento { get; } = new Dictionary<int, string>
    {
        [1] = "Caja fuerte",
        [2] = "Otra caja",
        [3] = "Consignación",
    };

    public static IReadOnlyDictionary<int, string> NombresDeDestinoDeMovimiento { get; } = new Dictionary<int, string>
    {
        [1] = "Safe",
        [2] = "Register",
        [3] = "Deposit",
    };

    /// <summary><c>CashDifferenceTreatment</c>.</summary>
    public static IReadOnlyDictionary<int, string> TratamientosDeDiferencia { get; } = new Dictionary<int, string>
    {
        [1] = "Sobrante",
        [2] = "Faltante a cargo del cajero",
        [3] = "Faltante al gasto",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTratamientoDeDiferencia { get; } = new Dictionary<int, string>
    {
        [1] = "Surplus",
        [2] = "ShortageToCashier",
        [3] = "ShortageToExpense",
    };

    /// <summary><c>DayCloseStatus</c>.</summary>
    public static IReadOnlyDictionary<int, string> EstadosDeCierreDelDia { get; } = new Dictionary<int, string>
    {
        [1] = "Cerrado",
        [2] = "Reabierto",
    };

    public static IReadOnlyDictionary<int, string> NombresDeEstadoDeCierreDelDia { get; } = new Dictionary<int, string>
    {
        [1] = "Closed",
        [2] = "Reopened",
    };

    // --------------------------------------------------------------------------------------- pagos --

    /// <summary><c>PaymentDirection</c>.</summary>
    public static IReadOnlyDictionary<int, string> DireccionesDePago { get; } = new Dictionary<int, string>
    {
        [1] = "Recibido",
        [2] = "Reintegrado",
    };

    public static IReadOnlyDictionary<int, string> NombresDeDireccionDePago { get; } = new Dictionary<int, string>
    {
        [1] = "Received",
        [2] = "Refunded",
    };

    /// <summary><c>CreditOrigin</c>.</summary>
    public static IReadOnlyDictionary<int, string> OrigenesDeCredito { get; } = new Dictionary<int, string>
    {
        [1] = "Crédito provisional",
        [2] = "Sin respuesta de Cartera",
        [3] = "Validado por Cartera",
    };

    public static IReadOnlyDictionary<int, string> NombresDeOrigenDeCredito { get; } = new Dictionary<int, string>
    {
        [1] = "ProvisionalCredit",
        [2] = "LendingNoResponse",
        [3] = "Validated",
    };

    /// <summary><c>VoucherRedemptionStatus</c>.</summary>
    public static IReadOnlyDictionary<int, string> EstadosDeBono { get; } = new Dictionary<int, string>
    {
        [1] = "Usado",
        [2] = "Liberado",
    };

    public static IReadOnlyDictionary<int, string> NombresDeEstadoDeBono { get; } = new Dictionary<int, string>
    {
        [1] = "Active",
        [2] = "Released",
    };

    // ------------------------------------------------------------------------------ promociones (I6) --

    /// <summary><c>PromotionKind</c> (I6, T899).</summary>
    public static IReadOnlyDictionary<int, string> ClasesDePromocion { get; } = new Dictionary<int, string>
    {
        [1] = "Porcentaje",
        [2] = "Valor por unidad",
        [3] = "Lleve N pague M",
        [4] = "Precio por cantidad",
        [5] = "Precio de paquete",
    };

    public static IReadOnlyDictionary<int, string> NombresDeClaseDePromocion { get; } = new Dictionary<int, string>
    {
        [1] = "Percent",
        [2] = "Amount",
        [3] = "BuyNPayM",
        [4] = "QuantityPrice",
        [5] = "BundlePrice",
    };

    /// <summary><c>PromotionScopeKind</c>: el ámbito de una promoción.</summary>
    public static IReadOnlyDictionary<int, string> AmbitosDePromocion { get; } = new Dictionary<int, string>
    {
        [1] = "Producto",
        [2] = "Categoría",
        [3] = "Segmento",
        [4] = "Canal",
    };

    /// <summary><c>DiscountSource.Promotion</c>.</summary>
    public const int DescuentoDePromocion = 2;

    /// <summary>Las clases del ciclo comercial de I6 (<c>DocumentClass</c>).</summary>
    public const int ClaseCotizacion = 23;
    public const int ClasePedido = 24;
    public const int ClaseRemision = 25;
    public const int ClaseFactura = 26;
    public const int ClaseFacturaDesdeRemisiones = 27;
    public const int ClaseComprobanteNoElectronico = 29;
    public const int ClaseNotaDebito = 33;

    // ----------------------------------------------------------------------------------- ayudantes --

    public static string ClaseDeMedio(int v) => Texto(ClasesDeMedio, v, "Medio");
    public static string TipoDeTarjeta(int v) => Texto(TiposDeTarjeta, v, "Tarjeta");
    public static string TipoDeReferencia(int? v) => v is { } x ? Texto(TiposDeReferencia, x, "Referencia") : "—";
    public static string FormaDeArqueo(int v) => Texto(FormasDeArqueo, v, "Arqueo");
    public static string TipoDeDenominacion(int v) => Texto(TiposDeDenominacion, v, "Denominación");
    public static string RolDeCaja(int v) => Texto(RolesDeCaja, v, "Rol");
    public static string FormatoDeImpresion(int v) => Texto(FormatosDeImpresion, v, "Formato");
    public static string EstadoDeSesion(int v) => Texto(EstadosDeSesion, v, "Estado");
    public static string ClaseDeMovimiento(int v) => Texto(ClasesDeMovimiento, v, "Movimiento");
    public static string DestinoDeMovimiento(int? v) => v is { } x ? Texto(DestinosDeMovimiento, x, "Destino") : "—";
    public static string TratamientoDeDiferencia(int? v) => v is { } x ? Texto(TratamientosDeDiferencia, x, "Tratamiento") : "—";
    public static string EstadoDeCierreDelDia(int v) => Texto(EstadosDeCierreDelDia, v, "Estado");
    public static string DireccionDePago(int v) => Texto(DireccionesDePago, v, "Dirección");
    public static string OrigenDeCredito(int? v) => v is { } x ? Texto(OrigenesDeCredito, x, "Origen") : "—";
    public static string EstadoDeBono(int? v) => v is { } x ? Texto(EstadosDeBono, x, "Bono") : "—";
    public static string ClaseDePromocion(int v) => Texto(ClasesDePromocion, v, "Clase");
    public static string AmbitoDePromocion(int v) => Texto(AmbitosDePromocion, v, "Ámbito");

    public static string NombreDeClaseDeMedio(int v) => Nombre(NombresDeClaseDeMedio, v);
    public static string NombreDeTipoDeTarjeta(int v) => Nombre(NombresDeTipoDeTarjeta, v);
    public static string NombreDeTipoDeReferencia(int v) => Nombre(NombresDeTipoDeReferencia, v);
    public static string NombreDeFormaDeArqueo(int v) => Nombre(NombresDeFormaDeArqueo, v);
    public static string NombreDeTipoDeDenominacion(int v) => Nombre(NombresDeTipoDeDenominacion, v);
    public static string NombreDeRolDeCaja(int v) => Nombre(NombresDeRolDeCaja, v);
    public static string NombreDeFormato(int v) => Nombre(NombresDeFormatoDeImpresion, v);
    public static string NombreDeEstadoDeSesion(int v) => Nombre(NombresDeEstadoDeSesion, v);
    public static string NombreDeClaseDeMovimiento(int v) => Nombre(NombresDeClaseDeMovimiento, v);
    public static string NombreDeDestino(int v) => Nombre(NombresDeDestinoDeMovimiento, v);
    public static string NombreDeTratamiento(int v) => Nombre(NombresDeTratamientoDeDiferencia, v);
    public static string NombreDeClaseDePromocion(int v) => Nombre(NombresDeClaseDePromocion, v);

    /// <summary>El número de un nombre (lo que vuelve de un <c>select</c> que muestra etiquetas y guarda nombres).</summary>
    public static int? Numero(IReadOnlyDictionary<int, string> nombres, string? nombre) =>
        nombre is null ? null : nombres.Where(p => string.Equals(p.Value, nombre, StringComparison.Ordinal)).Select(p => (int?)p.Key).FirstOrDefault();

    private static string Texto(IReadOnlyDictionary<int, string> d, int v, string prefijo) => d.TryGetValue(v, out var t) ? t : $"{prefijo} {v}";

    private static string Nombre(IReadOnlyDictionary<int, string> d, int v) =>
        d.TryGetValue(v, out var n) ? n : throw new ArgumentOutOfRangeException(nameof(v), v, "Valor sin nombre en TextosDeVentas.");
}
