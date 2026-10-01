using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Tests.Taxes;

/// <summary>
/// Un caso dorado del motor tributario (feature 012, T097; quickstart §1.1): un archivo JSON en <c>Casos/</c>, calculado
/// a mano, que la contadora puede leer y firmar (T169). Trae su propio catálogo —impuestos, conceptos, tarifas con sus
/// condiciones y vigencias, la UVT y el redondeo—, no la semilla: la semilla está pendiente de validar y un caso dorado
/// no puede depender de ella. Cada archivo tiene uno o más escenarios («A frente a B»), cada uno con su operación y lo
/// que DEBE salir renglón a renglón.
/// </summary>
public sealed class CasoTributario
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Uvt { get; set; }
    public RedondeoUvt RedondeoUvt { get; set; }
    public int Decimales { get; set; } = 2;
    public List<ImpuestoJson> Impuestos { get; set; } = [];
    public List<ConceptoJson> Conceptos { get; set; } = [];
    public List<TarifaJson> Tarifas { get; set; } = [];
    public List<EscenarioJson> Escenarios { get; set; } = [];

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Taxes", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoTributario Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoTributario>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    public TaxCatalogSnapshot Foto(PerfilTributario cooperativa) => new(
        Fecha,
        Uvt,
        RedondeoUvt,
        Decimales,
        cooperativa,
        Impuestos.Select(i => new ImpuestoEnFoto(i.Id, i.Codigo, i.Nombre, i.Clase, i.Forma, i.CalculadoSobre,
            i.EsRetencion ?? i.Clase is TaxKind.ReteFuente or TaxKind.ReteIva or TaxKind.ReteIca, i.CodigoDian, i.Activo)).ToList(),
        Tarifas.Select(t => new TarifaEnFoto(t.Id, t.Impuesto, t.Codigo, t.Nombre ?? t.Codigo, t.Tarifa, t.ValorPorUnidad, t.Concepto,
            t.Municipio, t.Actividad, t.BaseMinimaUvt, t.BaseMinimaPesos, t.Condiciones?.AFoto() ?? CondicionesDeTarifa.Ninguna,
            t.AplicaA, t.Prioridad, t.Desde, t.Hasta, t.Norma ?? "caso dorado")).ToList(),
        Conceptos.Select(c => new ConceptoEnFoto(c.Id, c.Codigo, c.Nombre)).ToList());

    public sealed class ImpuestoJson
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public TaxKind Clase { get; set; }
        public TaxCalculationForm Forma { get; set; }
        public int? CalculadoSobre { get; set; }
        public bool? EsRetencion { get; set; }
        public string? CodigoDian { get; set; }
        public bool Activo { get; set; } = true;
    }

    public sealed class ConceptoJson
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    public sealed class TarifaJson
    {
        public int Id { get; set; }
        public int Impuesto { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public decimal? Tarifa { get; set; }
        public decimal? ValorPorUnidad { get; set; }
        public int? Concepto { get; set; }
        public string? Municipio { get; set; }
        public string? Actividad { get; set; }
        public decimal? BaseMinimaUvt { get; set; }
        public decimal? BaseMinimaPesos { get; set; }
        public CondicionesJson? Condiciones { get; set; }
        public TaxAppliesTo AplicaA { get; set; } = TaxAppliesTo.Both;
        public int Prioridad { get; set; }
        public DateOnly Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public string? Norma { get; set; }
    }

    public sealed class CondicionesJson
    {
        public string? SujetoTipoPersona { get; set; }
        public bool? SujetoDeclarante { get; set; }
        public bool? SujetoResponsableIva { get; set; }
        public bool? SujetoGranContribuyente { get; set; }
        public bool? SujetoAutorretenedor { get; set; }
        public bool? SujetoRegimenSimple { get; set; }
        public bool? AgenteGranContribuyente { get; set; }
        public bool? AgenteRetenedorIva { get; set; }

        public CondicionesDeTarifa AFoto() => new(SujetoTipoPersona, SujetoDeclarante, SujetoResponsableIva, SujetoGranContribuyente,
            SujetoAutorretenedor, SujetoRegimenSimple, AgenteGranContribuyente, AgenteRetenedorIva);
    }

    public sealed class PerfilJson
    {
        public string? TipoPersona { get; set; }
        public bool ResponsableIva { get; set; }
        public bool Declarante { get; set; }
        public bool GranContribuyente { get; set; }
        public bool Autorretenedor { get; set; }
        public bool RegimenSimple { get; set; }
        public bool AgenteRetenedorIva { get; set; }
        public bool AgenteDeRetencion { get; set; }
        public bool ExentoDeRetencion { get; set; }
        public bool ExentoDeReteIca { get; set; }
        public string? Ciiu { get; set; }

        public PerfilTributario APerfil() => new()
        {
            PersonType = TipoPersona,
            IsVatResponsible = ResponsableIva,
            IsIncomeTaxFiler = Declarante,
            IsLargeContributor = GranContribuyente,
            IsSelfWithholder = Autorretenedor,
            IsSimpleTaxRegime = RegimenSimple,
            IsVatWithholdingAgent = AgenteRetenedorIva,
            EsAgenteDeRetencion = AgenteDeRetencion,
            WithholdingExempt = ExentoDeRetencion,
            IcaWithholdingExempt = ExentoDeReteIca,
            CiiuCode = Ciiu,
        };
    }

    public sealed class EscenarioJson
    {
        public string Nombre { get; set; } = string.Empty;
        public TaxAppliesTo Perspectiva { get; set; }
        public PerfilJson Vendedor { get; set; } = new();
        public PerfilJson Comprador { get; set; } = new();
        public string? Municipio { get; set; }
        public bool IvaNoDescontable { get; set; }
        public List<LineaJson> Lineas { get; set; } = [];
        public List<RenglonJson>? Original { get; set; }
        public EsperadoJson Esperado { get; set; } = new();

        /// <summary>La cooperativa es el comprador en compras y el vendedor en ventas.</summary>
        public PerfilTributario Cooperativa => (Perspectiva == TaxAppliesTo.Purchases ? Comprador : Vendedor).APerfil();

        public EntradaTributaria Entrada(DateOnly fecha, CasoTributario caso) => new()
        {
            Fecha = fecha,
            Perspectiva = Perspectiva,
            Vendedor = Vendedor.APerfil(),
            Comprador = Comprador.APerfil(),
            MunicipioDane = Municipio,
            TipoIvaNoDescontable = IvaNoDescontable,
            Lineas = Lineas.Select(l => new LineaTributaria(l.Numero, l.Base, l.Unidades, l.TratamientoIva,
                l.Impuestos.Select(i => new ImpuestoDeLinea(i.Impuesto, i.Tarifa, i.AplicaA, i.UnidadesGravables)).ToList(),
                l.Concepto, l.LineaOriginal)).ToList(),
            Original = Original?.Select(r => r.ARenglon(caso)).ToList(),
        };
    }

    public sealed class LineaJson
    {
        public int Numero { get; set; }
        public decimal Base { get; set; }
        public decimal Unidades { get; set; } = 1;
        public VatSaleTreatment TratamientoIva { get; set; } = VatSaleTreatment.Taxed;
        public int? Concepto { get; set; }
        public int? LineaOriginal { get; set; }
        public List<ImpuestoDeLineaJson> Impuestos { get; set; } = [];
    }

    public sealed class ImpuestoDeLineaJson
    {
        public int Impuesto { get; set; }
        public string? Tarifa { get; set; }
        public TaxAppliesTo AplicaA { get; set; } = TaxAppliesTo.Both;
        public decimal? UnidadesGravables { get; set; }
    }

    /// <summary>Un renglón de la foto del original (notas).</summary>
    public sealed class RenglonJson
    {
        public int? Linea { get; set; }
        public string Tarifa { get; set; } = string.Empty;
        public TaxTreatment Tratamiento { get; set; }
        public decimal Base { get; set; }
        public decimal Valor { get; set; }

        public RenglonTributario ARenglon(CasoTributario caso)
        {
            var tarifa = caso.Tarifas.Single(t => t.Codigo == Tarifa);
            var impuesto = caso.Impuestos.Single(i => i.Id == tarifa.Impuesto);
            return new RenglonTributario(Linea, impuesto.Id, tarifa.Id, tarifa.Codigo, impuesto.Clase, Tratamiento, tarifa.Concepto,
                tarifa.Municipio, tarifa.Tarifa, tarifa.ValorPorUnidad, null, Base, Valor, impuesto.CodigoDian, new ExplicacionTributaria());
        }
    }

    public sealed class EsperadoJson
    {
        public List<RenglonEsperadoJson> Renglones { get; set; } = [];
        /// <summary>Códigos de rechazo esperados (<c>Core.TaxRate.Ambiguous</c>).</summary>
        public List<string> Rechazos { get; set; } = [];
        /// <summary>Lo que el mensaje del rechazo debe nombrar (los códigos de las dos tarifas que empatan).</summary>
        public List<string> RechazoNombra { get; set; } = [];
    }

    public sealed class RenglonEsperadoJson
    {
        public int? Linea { get; set; }
        public string Tarifa { get; set; } = string.Empty;
        public TaxTreatment Tratamiento { get; set; }
        public decimal Base { get; set; }
        public decimal Valor { get; set; }
        /// <summary>Lo que la explicación debe decir (fragmentos: «Base mínima en pesos: 1414098», «fila *»).</summary>
        public List<string> Explicacion { get; set; } = [];
    }
}
