using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Imports;

// Las plantillas 2 a 7 de la parametrización (feature 012, T227–T229; contracts/plantillas.md §2–§7): sus hojas y columnas.
// La misma definición arma el libro vacío o lleno con su hoja «Instrucciones» y es lo que exige cada Import…Command;
// CatalogoDePlantillas las publica con su clave. Los encabezados son las constantes de cada clase. (nuevo)

/// <summary>Plantilla 2 — grupos contables (§2). Escribe <c>INV_AccountingGroups</c>. (nuevo)</summary>
public static class PlantillaDeGruposContables
{
    public const string Clave = CatalogoDePlantillas.GruposContablesClave;
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Descripcion = "descripcion";
    public const string Activo = "activo";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Grupos contables", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave; no cambia nunca (la matriz contable lo usa)", Ejemplo: "ABARROTES"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Abarrotes"),
            new(Descripcion, TipoDeValor.Texto, Largo: 300, Ejemplo: "Víveres secos"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; un grupo con productos activos no se inactiva", Ejemplo: "sí"),
        ]),
    ]);
}

/// <summary>Plantilla 3 — unidades de medida (§3). Escribe <c>INV_UnitsOfMeasure</c>; la semilla trae las básicas. (nuevo)</summary>
public static class PlantillaDeUnidades
{
    public const string Clave = CatalogoDePlantillas.UnidadesClave;
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Decimales = "decimales";
    public const string CodigoDian = "codigoDian";
    public const string Activo = "activo";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Unidades de medida", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave", Ejemplo: "UND"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 60, Ejemplo: "Unidad"),
            new(Decimales, TipoDeValor.Entero, Obligatoria: true, Reglas: "0 a 4; bajarlo en una unidad con movimientos se rechaza", Ejemplo: "0"),
            new(CodigoDian, TipoDeValor.Texto, Obligatoria: true, Largo: 3, Reglas: "UN/ECE Rec. 20 de la tabla vigente de la DIAN", Ejemplo: "94"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ]),
    ]);
}

/// <summary>Plantilla 4 — marcas (§4). Escribe <c>INV_Brands</c>. (nuevo)</summary>
public static class PlantillaDeMarcas
{
    public const string Clave = CatalogoDePlantillas.MarcasClave;
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Activo = "activo";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Marcas", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave", Ejemplo: "DIANA"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Arroz Diana"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ]),
    ]);
}

/// <summary>Plantilla 5 — categorías (§5). Escribe <c>INV_ProductCategories</c>, hasta 5 niveles. (nuevo)</summary>
public static class PlantillaDeCategorias
{
    public const string Clave = CatalogoDePlantillas.CategoriasClave;
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Padre = "padre";
    public const string Activo = "activo";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Categorías", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave", Ejemplo: "GRANOS"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Granos"),
            new(Padre, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "vacío = raíz; puede venir en otra fila del mismo archivo, en cualquier orden; sin ciclos y hasta el nivel 5", Ejemplo: "ALIM"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; una categoría con productos o subcategorías activas no se inactiva", Ejemplo: "sí"),
        ]),
    ]);
}

/// <summary>
/// Plantilla 6 — productos con códigos de barras, conversiones e impuestos adicionales (§6): las cuatro hojas se revisan y se
/// aplican juntas. (nuevo)
/// </summary>
public static class PlantillaDeProductos
{
    public const string Clave = CatalogoDePlantillas.ProductosClave;

    public const string HojaProductos = "Productos";
    public const string HojaCodigos = "CodigosDeBarras";
    public const string HojaUnidades = "Unidades";
    public const string HojaImpuestos = "ImpuestosAdicionales";

    public const string PermisoDeReclasificar = "Inventory.Catalog.ReclassifyAccountingGroup";

    // Productos.
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Tipo = "tipo";
    public const string Categoria = "categoria";
    public const string Marca = "marca";
    public const string UnidadBase = "unidadBase";
    public const string GrupoContable = "grupoContable";
    public const string Estado = "estado";
    public const string ControlaLote = "controlaLote";
    public const string ControlaSerie = "controlaSerie";
    public const string ControlaVencimiento = "controlaVencimiento";
    public const string TratamientoIva = "tratamientoIva";
    public const string TarifaIva = "tarifaIva";
    public const string ConceptoRetencion = "conceptoRetencion";
    public const string Referencia = "referencia";
    public const string Peso = "peso";
    public const string Volumen = "volumen";
    public const string MotivoCambioDeGrupo = "motivoCambioDeGrupo";

    // CodigosDeBarras, Unidades, ImpuestosAdicionales.
    public const string Producto = "producto";
    public const string CodigoDeBarras = "codigoDeBarras";
    public const string Unidad = "unidad";
    public const string Factor = "factor";
    public const string Uso = "uso";
    public const string Tarifa = "tarifa";
    public const string UnidadesGravables = "unidadesGravables";

    public static readonly IReadOnlyDictionary<string, ProductKind> EtiquetasDeTipo = new Dictionary<string, ProductKind>
    {
        ["inventariable"] = ProductKind.Inventoriable,
        ["servicio"] = ProductKind.Service,
        ["combo"] = ProductKind.Combo,
        ["kit"] = ProductKind.Kit,
        ["plantilla"] = ProductKind.Template,
        ["variante"] = ProductKind.Variant,
    };

    public static readonly IReadOnlyDictionary<string, ProductStatus> EtiquetasDeEstado = new Dictionary<string, ProductStatus>
    {
        ["activo"] = ProductStatus.Active,
        ["inactivo"] = ProductStatus.Inactive,
        ["bloqueado"] = ProductStatus.Blocked,
    };

    public static readonly IReadOnlyDictionary<string, VatSaleTreatment> EtiquetasDeIva = new Dictionary<string, VatSaleTreatment>
    {
        ["gravado"] = VatSaleTreatment.Taxed,
        ["exento"] = VatSaleTreatment.Exempt,
        ["excluido"] = VatSaleTreatment.Excluded,
    };

    public static readonly IReadOnlyDictionary<string, ProductUnitUsage> EtiquetasDeUso = new Dictionary<string, ProductUnitUsage>
    {
        ["compra"] = ProductUnitUsage.Purchase,
        ["venta"] = ProductUnitUsage.Sale,
        ["ambas"] = ProductUnitUsage.Both,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Productos (con códigos de barras y conversiones)", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla(HojaProductos,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoLargo, Reglas: "llave", Ejemplo: "ARZ-001"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 200, Ejemplo: "ARROZ DIANA 500 G"),
            new(Tipo, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "Inventoriable (inventariable) o Service (servicio); combos, kits, plantillas y variantes llegan con I6", Ejemplo: "Inventoriable"),
            new(Categoria, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de categoría", Ejemplo: "ARROZ"),
            new(Marca, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de marca", Ejemplo: "DIANA"),
            new(UnidadBase, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "código de unidad; no cambia si el producto tiene movimientos", Ejemplo: "UND"),
            new(GrupoContable, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "código de grupo; obligatorio (la matriz contable asigna las cuentas por él); con movimientos cambia por la reclasificación de grupo",
                Ejemplo: "ABARROTES"),
            new(Estado, TipoDeValor.Enumeracion, Reglas: "Active (activo), Inactive (inactivo), Blocked (bloqueado); vacío = activo al crear, sin cambio al actualizar", Ejemplo: "Active"),
            new(ControlaLote, TipoDeValor.SiNo, Reglas: "«sí» llega con I6", Ejemplo: "no"),
            new(ControlaSerie, TipoDeValor.SiNo, Reglas: "«sí» llega con I6", Ejemplo: "no"),
            new(ControlaVencimiento, TipoDeValor.SiNo, Reglas: "«sí» llega con I6", Ejemplo: "no"),
            new(TratamientoIva, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Taxed (gravado), Exempt (exento), Excluded (excluido)", Ejemplo: "Excluded"),
            new(TarifaIva, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de tarifa de IVA; obligatoria si es gravado, vacía si exento o excluido", Ejemplo: "IVA19"),
            new(ConceptoRetencion, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "concepto de retención en compras", Ejemplo: "COMPRAS"),
            new(Referencia, TipoDeValor.Texto, Largo: 60, Reglas: "entra a la búsqueda"),
            new(Peso, TipoDeValor.Cantidad, Reglas: "kilogramos"),
            new(Volumen, TipoDeValor.Cantidad, Reglas: "litros"),
            new(MotivoCambioDeGrupo, TipoDeValor.Texto, Largo: 400,
                Reglas: "cuando la fila cambia el grupo de un producto con movimientos (exige " + PermisoDeReclasificar + "; llega con la reclasificación de US3)"),
        ]),
        new HojaDePlantilla(HojaCodigos,
        [
            new(Producto, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoLargo, Reglas: "código de producto (de la hoja Productos o existente)", Ejemplo: "ARZ-001"),
            new(CodigoDeBarras, TipoDeValor.Texto, Obligatoria: true, Largo: 48,
                Reglas: "letras, dígitos o guiones; llave; único entre los vivos de la cooperativa", Ejemplo: "7702001000014"),
            new(Unidad, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "vacío = la base; o una unidad alterna del producto (el empaque)", Ejemplo: "PACA25"),
        ], Obligatoria: false),
        new HojaDePlantilla(HojaUnidades,
        [
            new(Producto, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoLargo, Reglas: "llave con unidad", Ejemplo: "ARZ-001"),
            new(Unidad, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "distinta de la base", Ejemplo: "PACA25"),
            new(Factor, TipoDeValor.Costo, Obligatoria: true, Reglas: "> 0, hasta 6 decimales: cuántas unidades base tiene; no cambia con movimientos", Ejemplo: "25"),
            new(Uso, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Purchase (compra), Sale (venta), Both (ambas)", Ejemplo: "Both"),
        ], Obligatoria: false),
        new HojaDePlantilla(HojaImpuestos,
        [
            new(Producto, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoLargo, Reglas: "llave con tarifa", Ejemplo: "BOLSA-01"),
            new(Tarifa, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de tarifa; ni IVA (va en tarifaIva) ni retención", Ejemplo: "BOLSA"),
            new(UnidadesGravables, TipoDeValor.Costo, Reglas: "en impuestos por unidad: unidades gravables por unidad base", Ejemplo: "1"),
        ], Obligatoria: false),
    ]);
}

/// <summary>Plantilla 7 — bodegas y ubicaciones (§7). Escribe <c>INV_Warehouses</c> e <c>INV_WarehouseLocations</c>. (nuevo)</summary>
public static class PlantillaDeBodegas
{
    public const string Clave = CatalogoDePlantillas.BodegasClave;

    public const string HojaBodegas = "Bodegas";
    public const string HojaUbicaciones = "Ubicaciones";

    public const string PermisoDeParametros = "Inventory.Parameters.Manage";

    // Bodegas.
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Sucursal = "sucursal";
    public const string Tipo = "tipo";
    public const string StockNegativo = "stockNegativo";
    public const string Activa = "activa";

    // Ubicaciones.
    public const string Bodega = "bodega";
    public const string PorDefecto = "porDefecto";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Bodegas y ubicaciones", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla(HojaBodegas,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave; no cambia nunca", Ejemplo: "B01"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "BODEGA PRINCIPAL FLORIDA"),
            new(Sucursal, TipoDeValor.Sucursal, Obligatoria: true, Reglas: "código o nombre de la sucursal contable; no cambia", Ejemplo: "01"),
            new(Tipo, TipoDeValor.Texto, Obligatoria: true, Largo: 120,
                Reglas: "código o nombre del tipo de bodega; una fila de tránsito sólo acompaña a la primera bodega operativa de la sucursal (fija su código)", Ejemplo: "principal"),
            new(StockNegativo, TipoDeValor.SiNoIndiferente, Permiso: PermisoDeParametros,
                Reglas: "vacío = hereda el general; con valor crea una vigencia desde hoy (pide motivo)"),
            new(Activa, TipoDeValor.SiNo, Reglas: "vacío = sí; la plantilla nunca activa la operación de la bodega (eso es la activación)", Ejemplo: "sí"),
        ]),
        new HojaDePlantilla(HojaUbicaciones,
        [
            new(Bodega, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de bodega (no de tránsito); llave con codigo", Ejemplo: "B01"),
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "único en su bodega", Ejemplo: "A-01"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 60, Ejemplo: "PASILLO A ESTANTE 1"),
            new(PorDefecto, TipoDeValor.SiNo, Reglas: "vacío = no; al terminar, cada bodega tiene exactamente una por defecto", Ejemplo: "no"),
            new(Activa, TipoDeValor.SiNo, Reglas: "vacío = sí; la por defecto no se inactiva", Ejemplo: "sí"),
        ], Obligatoria: false),
    ]);
}

/// <summary>
/// Las columnas de las plantillas 10 a 13 (§10–§13): en I1 sólo se descargan vacías (T238, FR-095); su importación llega con
/// I3 (T627–T629), que reemplaza estas definiciones por las suyas si hace falta. (nuevo)
/// </summary>
public static class PlantillasQueSeImportanConI3
{
    public static DefinicionDePlantilla PuntosDeVenta { get; } = new(CatalogoDePlantillas.PuntosDeVentaClave, "Puntos de venta y cajas", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("PuntosDeVenta",
        [
            C("codigo", TipoDeValor.Codigo, true, 10, "llave; no cambia nunca"), C("nombre", TipoDeValor.Texto, true, 120),
            C("sucursal", TipoDeValor.Sucursal, true), C("canal", TipoDeValor.Codigo, true, 10, "el punto fija el canal"),
            C("posHabilitado", TipoDeValor.SiNo, reglas: "vacío = sí"), C("bodegaPorDefecto", TipoDeValor.Codigo, true, 10, "operativa y de la misma sucursal"),
            C("activo", TipoDeValor.SiNo, reglas: "vacío = sí"),
        ]),
        new HojaDePlantilla("Cajas",
        [
            C("codigo", TipoDeValor.Codigo, true, 10, "llave, única en la cooperativa"), C("punto", TipoDeValor.Codigo, true, 10),
            C("nombre", TipoDeValor.Texto, true, 60), C("bodega", TipoDeValor.Codigo, true, 10),
            C("tipoVentaPos", TipoDeValor.Codigo, true, 10), C("tipoFactura", TipoDeValor.Codigo, largo: 10),
            C("tipoNotaVentaPos", TipoDeValor.Codigo, largo: 10), C("tipoNotaCreditoFactura", TipoDeValor.Codigo, largo: 10),
            C("tipoContingenciaVentaPos", TipoDeValor.Codigo, largo: 10), C("tipoContingenciaFactura", TipoDeValor.Codigo, largo: 10),
            C("impresion", TipoDeValor.Enumeracion, reglas: "Tirilla80, Carta; vacío = Tirilla80"), C("activa", TipoDeValor.SiNo, reglas: "vacío = sí"),
        ]),
    ]);

    public static DefinicionDePlantilla MediosDePago { get; } = new(CatalogoDePlantillas.MediosDePagoClave, "Medios de pago (con franquicias, adquirentes y datáfonos)",
        ModuloDeAuditoria.PaymentMeans,
    [
        new HojaDePlantilla("Franquicias",
        [
            C("codigo", TipoDeValor.Codigo, true, 10), C("nombre", TipoDeValor.Texto, true, 60),
            C("tipoDeTarjeta", TipoDeValor.Enumeracion, true, reglas: "Credit, Debit, Both"), C("activa", TipoDeValor.SiNo),
        ]),
        new HojaDePlantilla("Adquirentes",
        [
            C("codigo", TipoDeValor.Codigo, true, 10), C("nombre", TipoDeValor.Texto, true, 60),
            C("tercero", TipoDeValor.Persona), C("activo", TipoDeValor.SiNo),
        ]),
        new HojaDePlantilla("Datafonos",
        [
            C("codigo", TipoDeValor.Codigo, true, 10), C("adquirente", TipoDeValor.Codigo, true, 10),
            C("serial", TipoDeValor.Texto, largo: 40), C("cajaPorDefecto", TipoDeValor.Codigo, largo: 10), C("activo", TipoDeValor.SiNo),
        ]),
        new HojaDePlantilla("MediosDePago",
        [
            C("codigo", TipoDeValor.Codigo, true, 10, "llave; no cambia nunca"), C("nombre", TipoDeValor.Texto, true, 60),
            C("orden", TipoDeValor.Entero), C("teclaRapida", TipoDeValor.Texto, largo: 1),
            C("clase", TipoDeValor.Enumeracion, true, reglas: "Cash, CreditCard, DebitCard, AssociateCredit, CustomerCredit, BankDeposit, Transfer, Voucher, Check, Other"),
            C("franquicia", TipoDeValor.Codigo, largo: 10), C("adquirente", TipoDeValor.Codigo, largo: 10), C("banco", TipoDeValor.Banco),
            C("cuentaDestino", TipoDeValor.Texto, largo: 30), C("tipoCuentaDestino", TipoDeValor.Enumeracion, reglas: "Ahorros, Corriente"),
            C("exigeReferencia", TipoDeValor.SiNo), C("tipoReferencia", TipoDeValor.Enumeracion),
            C("largoMinimoReferencia", TipoDeValor.Entero), C("largoMaximoReferencia", TipoDeValor.Entero),
            C("admiteVueltas", TipoDeValor.SiNo), C("admitePagoParcial", TipoDeValor.SiNo), C("referenciaUnica", TipoDeValor.SiNo),
            C("arqueo", TipoDeValor.Enumeracion, reglas: "PhysicalCount, VoucherTotal, ByReference, None"), C("tolerancia", TipoDeValor.Monto),
            C("comisionEsperadaPorcentaje", TipoDeValor.Porcentaje), C("comisionEsperadaValor", TipoDeValor.Monto),
            C("codigoDian", TipoDeValor.Texto, true, 3), C("plazoDias", TipoDeValor.Entero), C("cuotas", TipoDeValor.Entero),
            C("periodicidad", TipoDeValor.Enumeracion, reglas: "Mensual, Quincenal, Semanal"), C("lineaSugerida", TipoDeValor.Texto, largo: 20),
            C("puntos", TipoDeValor.Lista, permiso: "Inventory.PointsOfSale.Manage"), C("canales", TipoDeValor.Lista, permiso: "Inventory.PointsOfSale.Manage"),
            C("tiposDeDocumento", TipoDeValor.Lista, permiso: "Inventory.PointsOfSale.Manage"),
            C("vigenteDesde", TipoDeValor.Fecha, true), C("vigenteHasta", TipoDeValor.Fecha), C("activo", TipoDeValor.SiNo),
        ]),
    ]);

    public static DefinicionDePlantilla ListasDePrecios { get; } = new(CatalogoDePlantillas.ListasDePreciosClave, "Listas de precios", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Listas",
        [
            C("codigo", TipoDeValor.Codigo, true, 10), C("nombre", TipoDeValor.Texto, true, 120), C("incluyeImpuestos", TipoDeValor.SiNo, true),
            C("cliente", TipoDeValor.Persona), C("segmento", TipoDeValor.Texto), C("canal", TipoDeValor.Codigo, largo: 10),
            C("sucursal", TipoDeValor.Sucursal), C("vigenteDesde", TipoDeValor.Fecha, true), C("vigenteHasta", TipoDeValor.Fecha),
            C("activa", TipoDeValor.SiNo),
        ]),
        new HojaDePlantilla("Precios",
        [
            C("lista", TipoDeValor.Codigo, true, 10), C("producto", TipoDeValor.Codigo, true, 20),
            C("unidad", TipoDeValor.Codigo, largo: 10, reglas: "vacío = la base"), C("precio", TipoDeValor.Monto, true),
        ]),
    ]);

    public static DefinicionDePlantilla TopesDeDescuento { get; } = new(CatalogoDePlantillas.TopesDeDescuentoClave, "Topes de descuento", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            C("rol", TipoDeValor.Codigo, true, reglas: "código del rol de la cooperativa"),
            C("topeLineaPorcentaje", TipoDeValor.Porcentaje, true), C("topeDocumentoPorcentaje", TipoDeValor.Porcentaje, true),
            C("vigenteDesde", TipoDeValor.Fecha, true), C("vigenteHasta", TipoDeValor.Fecha),
        ]),
    ]);

    private static ColumnaDePlantilla C(string nombre, TipoDeValor tipo, bool obligatoria = false, int? largo = null, string? reglas = null, string? permiso = null) =>
        new(nombre, tipo, obligatoria, largo, permiso, reglas);
}
