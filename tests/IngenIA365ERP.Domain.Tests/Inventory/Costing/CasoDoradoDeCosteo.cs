using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Units;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Un caso dorado del costeo (feature 012, T268; SC-007; research R10): un archivo JSON en <c>Casos/</c>, calculado a
/// mano, que la contadora puede leer y firmar. Trae los parámetros (método, ámbito, redondeo, negativo), los movimientos
/// en orden —clase, bodega, fecha, cantidad, cómo se valora, costo de entrada o línea de origen— y, por movimiento, lo
/// que DEBE salir en el kardex (línea a línea, al peso) y el estado del ámbito; al final, el estado de cada ámbito y la
/// existencia por bodega. Molde de <c>Payroll/Calculation/CasoDorado</c>.
///
/// <para>
/// Las líneas se nombran por el movimiento: «V1» es la línea principal de V1 (su primera entrada o salida) y «V1#2» su
/// segunda línea. Un movimiento con <c>retroactivo = true</c> entra por <see cref="Retroactivo"/> con la historia de su
/// ámbito; sus ajustes se esperan en <c>esperado.ajustes</c> y <c>esperado.ajustesPorDocumento</c>.
/// </para>
/// </summary>
public sealed class CasoDoradoDeCosteo
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public const string AmbitoCooperativa = "COOPERATIVA";

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public ParametrosJson Parametros { get; set; } = new();
    public List<MovimientoJson> Movimientos { get; set; } = [];
    public List<EstadoFinalJson> Final { get; set; } = [];
    public List<ExistenciaJson> Existencias { get; set; } = [];

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Inventory", "Costing", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeCosteo Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeCosteo>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    public string AmbitoDe(MovimientoJson m) => Parametros.Ambito == CostScope.Cooperative ? AmbitoCooperativa : m.Bodega;

    public ParametrosDeCosteo ParametrosDelMotor() => new(Parametros.Metodo, Parametros.Montos, Parametros.NegativoPermitido);

    public sealed class ParametrosJson
    {
        public CostMethod Metodo { get; set; } = CostMethod.WeightedAverage;
        public CostScope Ambito { get; set; } = CostScope.Cooperative;
        public RedondeoDeMontos Montos { get; set; } = RedondeoDeMontos.Centavo;
        public bool NegativoPermitido { get; set; }
    }

    public sealed class MovimientoJson
    {
        public string Id { get; set; } = string.Empty;
        public string? Documento { get; set; }
        public DateOnly Fecha { get; set; }
        public DocumentClass Clase { get; set; }
        public string Bodega { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public ValoracionDelMovimiento? Valoracion { get; set; }
        public decimal? CostoUnitario { get; set; }
        public string? Origen { get; set; }
        public bool Anulacion { get; set; }
        public bool Retroactivo { get; set; }
        public bool? SaleDelInventario { get; set; }
        public UnidadJson? Unidad { get; set; }
        public CompraJson? Compra { get; set; }
        public EsperadoJson Esperado { get; set; } = new();

        public string DocumentoOId => Documento ?? Id;

        /// <summary>Por defecto, una salida deja el inventario salvo en los traslados, que lo mueven por dentro.</summary>
        public bool DejaElInventario => SaleDelInventario
            ?? Clase is not (DocumentClass.TransferDispatch or DocumentClass.TransferReceipt);
    }

    /// <summary>La cantidad viene en una unidad de empaque y se convierte con <see cref="ConversionDeUnidades"/>.</summary>
    public sealed class UnidadJson
    {
        public string Codigo { get; set; } = string.Empty;
        public decimal Factor { get; set; }
        public int Decimales { get; set; }
        public string Base { get; set; } = string.Empty;
        public int DecimalesBase { get; set; }
    }

    /// <summary>Una compra valorada con <see cref="CostoDeEntrada.DeCompra"/>.</summary>
    public sealed class CompraJson
    {
        public decimal ValorBruto { get; set; }
        public decimal Descuentos { get; set; }
        public List<ImpuestoJson> Impuestos { get; set; } = [];
        public bool CooperativaResponsableIva { get; set; } = true;
        public bool TipoIvaNoDescontable { get; set; }
        public VatSaleTreatment TratamientoDeVenta { get; set; } = VatSaleTreatment.Taxed;
    }

    public sealed class ImpuestoJson
    {
        public TaxKind Clase { get; set; }
        public decimal Valor { get; set; }
        public string? Codigo { get; set; }
    }

    public sealed class EsperadoJson
    {
        public decimal? CantidadBase { get; set; }
        public decimal? CantidadDeRedondeo { get; set; }
        public CostoDeEntradaJson? CostoDeEntrada { get; set; }
        public List<LineaJson>? Lineas { get; set; }
        public List<LineaJson> Ajustes { get; set; } = [];
        public List<PorDocumentoJson> AjustesPorDocumento { get; set; } = [];
        public EstadoJson? Estado { get; set; }
        public string? Rechazo { get; set; }
        public List<string> Explicacion { get; set; } = [];
    }

    public sealed class CostoDeEntradaJson
    {
        public decimal CostoUnitario { get; set; }
        public decimal ImpuestosAlCosto { get; set; }
    }

    public sealed class LineaJson
    {
        public KardexEntryKind Kind { get; set; }
        public KardexReason Reason { get; set; } = KardexReason.Normal;
        public decimal Cantidad { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal CostoTotal { get; set; }

        /// <summary>La línea que afecta («V1», «V1#2»); ausente = no se compara, «-» = ninguna.</summary>
        public string? Afecta { get; set; }

        /// <summary>La línea que revierte; ausente = no se compara, «-» = ninguna.</summary>
        public string? Revierte { get; set; }

        public DateOnly? Fecha { get; set; }
        public PorcionDelAjuste? Porcion { get; set; }
    }

    public sealed class PorDocumentoJson
    {
        public string Documento { get; set; } = string.Empty;
        public decimal EnExistencia { get; set; }
        public decimal Vendida { get; set; }
    }

    public sealed class EstadoJson
    {
        public decimal Cantidad { get; set; }
        public decimal Valor { get; set; }
        public decimal Promedio { get; set; }
        public decimal? UltimoCosto { get; set; }
    }

    public sealed class EstadoFinalJson
    {
        public string Ambito { get; set; } = AmbitoCooperativa;
        public decimal Cantidad { get; set; }
        public decimal Valor { get; set; }
        public decimal Promedio { get; set; }
        public decimal? UltimoCosto { get; set; }
    }

    public sealed class ExistenciaJson
    {
        public string Bodega { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
    }
}
