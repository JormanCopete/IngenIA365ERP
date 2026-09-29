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

    /// <summary>I5 (T820): el valorizado por los dos métodos que se espera del kardex que dejó el caso.</summary>
    public ValorizacionJson? Valorizacion { get; set; }

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Inventory", "Costing", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeCosteo Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeCosteo>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>El ámbito del movimiento; con <c>producto</c> (I5, caso 16), «producto@ámbito».</summary>
    public string AmbitoDe(MovimientoJson m) => AmbitoDe(m.Producto, m.Bodega);

    /// <summary>El ámbito de un producto en una bodega (I6: los componentes de un combo o de un ensamble).</summary>
    public string AmbitoDe(string? producto, string bodega)
    {
        var ambito = Parametros.Ambito == CostScope.Cooperative ? AmbitoCooperativa : bodega;
        return producto is { } p ? $"{p}@{ambito}" : ambito;
    }

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

        /// <summary>I5 (T768): costos adicionales repartidos sobre una entrada ya registrada; el movimiento no mueve cantidad.</summary>
        public CostoAdicionalJson? CostoAdicional { get; set; }

        /// <summary>I5 (T821): el cambio de método del ámbito a este valor; el movimiento no mueve cantidad.</summary>
        public CostMethod? CambioDeMetodo { get; set; }

        /// <summary>I5 (T820): el producto, cuando el caso lleva varios (el ámbito pasa a ser «producto@ámbito»).</summary>
        public string? Producto { get; set; }

        /// <summary>
        /// I6 (T904): los componentes de un combo o de un kit, con su cantidad por unidad. Con clase <c>Assembly</c> el movimiento es el
        /// ensamble de <see cref="Cantidad"/> kits (<see cref="MotorDeCosteo.Ensamblar{TClave}"/>): salen los componentes y entra el kit
        /// (<see cref="Producto"/>). Con otra clase es la venta (cantidad negativa) o la devolución (positiva, con <see cref="Origen"/> la
        /// venta) de un combo (<see cref="MotorDeCosteo.MoverCombo{TClave}"/>), que no tiene kardex propio. Las líneas de cada componente
        /// se nombran «{id}:{producto}».
        /// </summary>
        public List<ComponenteJson> Componentes { get; set; } = [];

        public EsperadoJson Esperado { get; set; } = new();

        public string DocumentoOId => Documento ?? Id;

        /// <summary>Por defecto, una salida deja el inventario salvo en los traslados, que lo mueven por dentro.</summary>
        public bool DejaElInventario => SaleDelInventario
            ?? Clase is not (DocumentClass.TransferDispatch or DocumentClass.TransferReceipt);
    }

    /// <summary>I6: un componente de un combo o kit y su cantidad por unidad del combo o del kit.</summary>
    public sealed class ComponenteJson
    {
        public string Producto { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
    }

    /// <summary>I6: lo que se espera de un componente: sus líneas y el estado de su ámbito.</summary>
    public sealed class ComponenteEsperadoJson
    {
        public string Producto { get; set; } = string.Empty;
        public List<LineaJson>? Lineas { get; set; }
        public EstadoJson? Estado { get; set; }
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

    /// <summary>
    /// Un costo adicional (flete, seguro) sobre la entrada <see cref="Entrada"/> (su línea, «C1»): se reparte con
    /// <see cref="Prorrateo"/> (una sola línea: todo a ella) con la existencia del ámbito en ese momento, y entra al kardex por
    /// <see cref="MotorDeCosteo.CostoAdicional"/>.
    /// </summary>
    public sealed class CostoAdicionalJson
    {
        public decimal Monto { get; set; }
        public string Entrada { get; set; } = string.Empty;
        public LandedCostAllocationMethod Metodo { get; set; } = LandedCostAllocationMethod.Value;
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

        /// <summary>I5 (PEPS): los consumos de capa del movimiento; ausente = no se compara.</summary>
        public List<ConsumoJson>? Consumos { get; set; }

        /// <summary>I5 (PEPS): las capas vivas del ámbito después del movimiento, en orden; ausente = no se compara.</summary>
        public List<CapaJson>? Capas { get; set; }

        /// <summary>I5 (T830): lo que <c>SimularImpacto</c> dice antes de confirmar un retroactivo.</summary>
        public ImpactoJson? Impacto { get; set; }

        public string? Rechazo { get; set; }
        public List<string> Explicacion { get; set; } = [];

        /// <summary>I6: por componente de un combo o de un ensamble.</summary>
        public List<ComponenteEsperadoJson> Componentes { get; set; } = [];

        /// <summary>I6: el costo de venta del combo (Σ de sus componentes) o el costo consumido del ensamble.</summary>
        public decimal? CostoCompuesto { get; set; }

        /// <summary>I6: el componente que no alcanzó, en un combo o ensamble rechazado.</summary>
        public string? ComponenteRechazado { get; set; }
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

    /// <summary>Un consumo de capa: la línea que consume («V1», o la anulación que devuelve) y la capa, nombrada por su entrada.</summary>
    public sealed class ConsumoJson
    {
        public string Salida { get; set; } = string.Empty;
        public string Capa { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal CostoUnitario { get; set; }
    }

    public sealed class CapaJson
    {
        public string Capa { get; set; } = string.Empty;
        public decimal Original { get; set; }
        public decimal Restante { get; set; }
        public decimal CostoUnitario { get; set; }
    }

    public sealed class ImpactoJson
    {
        public bool Retroactivo { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>
    /// El valorizado por los dos métodos (FR-043): el grupo contable de cada producto, el corte del sistema anterior de los que
    /// empezaron con saldo inicial, las fechas y lo que se espera por grupo y fecha.
    /// </summary>
    public sealed class ValorizacionJson
    {
        public Dictionary<string, string> Grupos { get; set; } = [];
        public Dictionary<string, DateOnly> Cortes { get; set; } = [];
        public List<DateOnly> Fechas { get; set; } = [];
        public List<GrupoValorizadoJson> Esperado { get; set; } = [];

        /// <summary>Un fragmento que la nota de todo producto sin calcular debe decir.</summary>
        public string? Nota { get; set; }
    }

    public sealed class GrupoValorizadoJson
    {
        public string Grupo { get; set; } = string.Empty;
        public DateOnly Fecha { get; set; }
        public decimal PromedioPonderado { get; set; }
        public decimal Peps { get; set; }
        public decimal Diferencia { get; set; }
        public List<string> SinCalcular { get; set; } = [];
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
