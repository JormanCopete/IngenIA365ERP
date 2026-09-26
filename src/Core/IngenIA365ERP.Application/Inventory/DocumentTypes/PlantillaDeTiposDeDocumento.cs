using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// La plantilla 8 de la parametrización (feature 012, T153; contracts/plantillas.md §8): hojas <c>TiposDeDocumento</c> y
/// <c>NivelesDeAprobacion</c> con sus columnas y los permisos por hoja y columna de §0.7. La misma definición arma el
/// libro vacío o lleno, su hoja «Instrucciones» y es lo que exige <c>ImportDocumentTypesCommand</c>;
/// <c>CatalogoDePlantillas</c> la publica con la clave <c>inventory.document-types</c>. (nuevo)
/// </summary>
public static class PlantillaDeTiposDeDocumento
{
    public const string Clave = "inventory.document-types";

    public const string HojaTipos = "TiposDeDocumento";
    public const string HojaNiveles = "NivelesDeAprobacion";

    // §0.7: permisos por hoja y columna.
    public const string PermisoDePoliticas = "Inventory.ApprovalPolicies.Manage";
    public const string PermisoDeParametros = "Inventory.Parameters.Manage";
    public const string PermisoDeFiscalSinPaso = "Inventory.DocumentTypes.DisableFiscalPosting";

    // TiposDeDocumento.
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Clase = "clase";
    public const string Prefijo = "prefijo";
    public const string SiguienteNumero = "siguienteNumero";
    public const string VigenciaDelPrefijo = "vigenciaDelPrefijo";
    public const string ExigeTercero = "exigeTercero";
    public const string ExigeCentroDeCosto = "exigeCentroDeCosto";
    public const string ExigeMotivo = "exigeMotivo";
    public const string ExigeReferenciaExterna = "exigeReferenciaExterna";
    public const string Bodegas = "bodegas";
    public const string Canal = "canal";
    public const string RetiroGravado = "retiroGravado";
    public const string IvaNoDescontable = "ivaNoDescontable";
    public const string PermiteFechaFutura = "permiteFechaFutura";
    public const string ModoDePaso = "modoDePaso";
    public const string Granularidad = "granularidad";
    public const string DisparadorDeLote = "disparadorDeLote";
    public const string HoraDeLote = "horaDeLote";
    public const string VigenciaDelModo = "vigenciaDelModo";
    public const string Activo = "activo";

    /// <summary>Las columnas del modo de paso (vigencias de <c>Contabilidad.*</c> con ámbito tipo de documento).</summary>
    public static readonly IReadOnlyList<string> ColumnasDelModoDePaso = [ModoDePaso, Granularidad, DisparadorDeLote, HoraDeLote, VigenciaDelModo];

    // NivelesDeAprobacion.
    public const string TipoDeDocumento = "tipoDeDocumento";
    public const string Nivel = "nivel";
    public const string Umbral = "umbral";
    public const string Permiso = "permiso";
    public const string VigenteDesde = "vigenteDesde";

    /// <summary>Las etiquetas en español que la columna <c>clase</c> admite además del nombre del enum (§8).</summary>
    public static readonly IReadOnlyDictionary<string, DocumentClass> EtiquetasDeClase = new Dictionary<string, DocumentClass>
    {
        ["Solicitud de compra"] = DocumentClass.PurchaseRequest,
        ["Orden de compra"] = DocumentClass.PurchaseOrder,
        ["Recepción de compra"] = DocumentClass.PurchaseReceipt,
        ["Factura del proveedor"] = DocumentClass.SupplierInvoice,
        ["Nota del proveedor"] = DocumentClass.SupplierNote,
        ["Documento soporte"] = DocumentClass.SupportDocument,
        ["Nota de ajuste al documento soporte"] = DocumentClass.SupportDocumentAdjustmentNote,
        ["Costos adicionales"] = DocumentClass.LandedCost,
        ["Devolución a proveedor"] = DocumentClass.SupplierReturn,
        ["Ajuste positivo"] = DocumentClass.PositiveAdjustment,
        ["Ajuste negativo"] = DocumentClass.NegativeAdjustment,
        ["Consumo interno"] = DocumentClass.InternalConsumption,
        ["Baja"] = DocumentClass.WriteOff,
        ["Ensamble"] = DocumentClass.Assembly,
        ["Saldo inicial"] = DocumentClass.OpeningBalance,
        ["Despacho de traslado"] = DocumentClass.TransferDispatch,
        ["Recepción de traslado"] = DocumentClass.TransferReceipt,
        ["Movimiento entre ubicaciones"] = DocumentClass.LocationMove,
        ["Conteo"] = DocumentClass.PhysicalCount,
        ["Ajuste de costo"] = DocumentClass.CostAdjustment,
        ["Movimiento de caja"] = DocumentClass.CashMovement,
        ["Arqueo con diferencia"] = DocumentClass.CashCountDifference,
        ["Cotización"] = DocumentClass.SalesQuote,
        ["Pedido"] = DocumentClass.SalesOrder,
        ["Remisión"] = DocumentClass.Shipment,
        ["Factura de venta"] = DocumentClass.SalesInvoice,
        ["Factura desde remisiones"] = DocumentClass.SalesInvoiceFromShipments,
        ["Documento equivalente POS"] = DocumentClass.PosEquivalentDocument,
        ["Comprobante de venta no electrónico"] = DocumentClass.NonElectronicSalesReceipt,
        ["Nota sobre comprobante no electrónico"] = DocumentClass.NonElectronicSalesNote,
        ["Nota crédito"] = DocumentClass.CreditNote,
        ["Nota de ajuste del documento equivalente"] = DocumentClass.PosAdjustmentNote,
        ["Nota débito"] = DocumentClass.DebitNote,
        ["Anulación"] = DocumentClass.Voiding,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Tipos de documento", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla(HojaTipos,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; no se renombra", Ejemplo: "AJN"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Ajuste negativo"),
            new(Clase, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "la clase (en inglés o su etiqueta: Ajuste negativo, Recepción de compra…); no cambia en un tipo existente; sólo clases operables en esta entrega",
                Ejemplo: "NegativeAdjustment"),
            new(Prefijo, TipoDeValor.Texto, Largo: 4,
                Reglas: "hasta 4 letras o dígitos; en las clases numeradas por resolución DIAN, el de la resolución; cambiarlo abre un consecutivo nuevo desde vigenciaDelPrefijo (pide motivo)",
                Ejemplo: "AN"),
            new(SiguienteNumero, TipoDeValor.Entero,
                Reglas: "≥ 1 (vacío: 1 al crear, sin cambio al actualizar); no baja de lo ya emitido; vacío en las clases numeradas por resolución DIAN",
                Ejemplo: "1"),
            new(VigenciaDelPrefijo, TipoDeValor.Fecha, Reglas: "vacío = hoy", Ejemplo: "AAAA-MM-01"),
            new(ExigeTercero, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "no"),
            new(ExigeCentroDeCosto, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "no"),
            new(ExigeMotivo, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "sí"),
            new(ExigeReferenciaExterna, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "no"),
            new(Bodegas, TipoDeValor.Lista,
                Reglas: "* = todas las operativas; códigos de bodega separados por coma cuando existan las bodegas; vacío = todas al crear, sin cambio al actualizar",
                Ejemplo: "*"),
            new(Canal, TipoDeValor.Codigo, Largo: 10, Reglas: "sólo clases de venta; código del canal de venta cuando existan los canales"),
            new(RetiroGravado, TipoDeValor.SiNo, Reglas: "sólo consumo interno", Ejemplo: "no"),
            new(IvaNoDescontable, TipoDeValor.SiNo, Reglas: "sólo clases de compra", Ejemplo: "no"),
            new(PermiteFechaFutura, TipoDeValor.SiNo, Reglas: "sólo clases no fiscales", Ejemplo: "no"),
            new(ModoDePaso, TipoDeValor.Enumeracion, Permiso: PermisoDeParametros,
                Reglas: "EnLinea, PorLotes, NoPasa o vacío (hereda el general); un tipo fiscal sin paso exige además " + PermisoDeFiscalSinPaso),
            new(Granularidad, TipoDeValor.Enumeracion, Permiso: PermisoDeParametros, Reglas: "PorDocumento, Resumido o vacío; sólo con PorLotes"),
            new(DisparadorDeLote, TipoDeValor.Enumeracion, Permiso: PermisoDeParametros, Reglas: "HoraDiaria, CierreDeTurno, CierreDePeriodo o vacío"),
            new(HoraDeLote, TipoDeValor.Hora, Permiso: PermisoDeParametros, Reglas: "HH:mm"),
            new(VigenciaDelModo, TipoDeValor.Fecha, Permiso: PermisoDeParametros, Reglas: "desde cuándo rigen las cuatro anteriores; vacío = hoy"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; inactivar o reactivar pide motivo", Ejemplo: "sí"),
        ]),
        new HojaDePlantilla(HojaNiveles,
        [
            new(TipoDeDocumento, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "código de la hoja TiposDeDocumento o existente", Ejemplo: "AJN"),
            new(Nivel, TipoDeValor.Entero, Obligatoria: true, Reglas: "1, 2, 3… consecutivos; llave con tipoDeDocumento", Ejemplo: "1"),
            new(Umbral, TipoDeValor.Monto, Obligatoria: true, Reglas: "≥ 0; no decrece con el nivel", Ejemplo: "0"),
            new(Permiso, TipoDeValor.Texto, Obligatoria: true, Largo: 100,
                Reglas: "código del catálogo de permisos (sugeridos Inventory.Approvals.Supervisor e Inventory.Approvals.Management)",
                Ejemplo: "Inventory.Approvals.Supervisor"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true,
                Reglas: "la misma en todos los niveles del tipo; las filas de un tipo reemplazan su política desde esa fecha (pide motivo)",
                Ejemplo: "AAAA-MM-01"),
        ], Obligatoria: false, Permiso: PermisoDePoliticas),
    ]);
}
