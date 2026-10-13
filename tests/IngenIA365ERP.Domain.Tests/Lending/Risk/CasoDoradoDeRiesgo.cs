using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Un caso dorado de riesgo de Cartera (feature 013, US9, T0672; quickstart §1.3): un JSON en <c>Casos/</c> con comentarios, claves en
/// español y enums por nombre, calculado a mano y repasado con la hoja <c>Casos/riesgo.py</c> (decimal de 40 dígitos, independiente
/// del C#) para que la contadora lo lea y lo firme. Molde: <see cref="CasoDoradoDeCartera"/>.
///
/// <para>Entran la norma de prueba (<c>Normas/{norma}.json</c>), la política del consejo (el esquema parcial de
/// <see cref="PoliticaSarc"/>), las opciones (arrastre, D19, indicadores, redondeo), los créditos con su altura de mora —en días o
/// con sus cuotas, el corte y los festivos, para que la fecha exigible salga del calendario hábil—, el general y el selector; sale, por
/// crédito, la calificación con causa, subcategoría y contable, y el deterioro desglosado con su explicación. Las variantes cambian
/// norma, política, opciones, general o selector sobre los mismos créditos.</para>
/// </summary>
public sealed class CasoDoradoDeRiesgo
{
    private static readonly JsonSerializerOptions Lectura = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Norma { get; set; } = "anexo1-prueba";
    public JsonElement? Politica { get; set; }
    public OpcionesJson Opciones { get; set; } = new();
    public DateOnly? Corte { get; set; }
    public List<DateOnly> Festivos { get; set; } = [];
    public List<CreditoJson> Creditos { get; set; } = [];
    public GeneralJson? General { get; set; }
    public SelectorJson? Selector { get; set; }
    public List<VarianteJson> Variantes { get; set; } = [];
    public EsperadoJson Esperado { get; set; } = new();

    public static string Directorio => Path.Combine(AppContext.BaseDirectory, "Lending", "Risk");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(Path.Combine(Directorio, "Casos"), "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeRiesgo Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeRiesgo>(File.ReadAllText(ruta), Lectura)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>La norma de prueba por nombre.</summary>
    public static Anexo1 LeerNorma(string nombre)
    {
        var r = Anexo1.Leer(File.ReadAllText(Path.Combine(Directorio, "Normas", nombre + ".json")), 1);
        return r.Documento ?? throw new InvalidOperationException($"La norma {nombre} no se lee: {string.Join("; ", r.Errores.Select(e => $"{e.Ruta} {e.Mensaje}"))}");
    }

    /// <summary>La regla de pérdida esperada de prueba por nombre.</summary>
    public static ReglaDePerdidaEsperada LeerRegla(string nombre)
    {
        var r = ReglaDePerdidaEsperada.Leer(File.ReadAllText(Path.Combine(Directorio, "Normas", nombre + ".json")), 1);
        return r.Documento ?? throw new InvalidOperationException($"La regla {nombre} no se lee.");
    }

    /// <summary>La política del caso; sin política, nula.</summary>
    public static PoliticaSarc? LeerPolitica(JsonElement? politica)
    {
        if (politica is not { ValueKind: JsonValueKind.Object } p) return null;
        var r = PoliticaSarc.Leer(p.GetRawText(), 1);
        return r.Documento ?? throw new InvalidOperationException($"La política no se lee: {string.Join("; ", r.Errores.Select(e => $"{e.Ruta} {e.Mensaje}"))}");
    }

    /// <summary>El caso y cada variante, con lo que heredan.</summary>
    public IEnumerable<Escenario> Escenarios()
    {
        yield return new(Nombre, Norma, Politica, Opciones, General, Selector, Esperado);
        foreach (var v in Variantes)
            yield return new($"{Nombre} · {v.Nombre}", v.Norma ?? Norma, v.Politica ?? Politica, v.Opciones ?? Opciones, v.General ?? General,
                v.Selector ?? Selector, v.Esperado);
    }

    public sealed record Escenario(string Nombre, string Norma, JsonElement? Politica, OpcionesJson Opciones, GeneralJson? General,
        SelectorJson? Selector, EsperadoJson Esperado);

    public sealed class OpcionesJson
    {
        public bool ConArrastre { get; set; } = true;
        public bool RigeD19 { get; set; } = true;
        public bool IndicadoresDisponibles { get; set; }
        public RedondeoDeDeterioro Redondeo { get; set; } = RedondeoDeDeterioro.Centavo;
    }

    public sealed class CreditoJson
    {
        public int Id { get; set; }
        public int Deudor { get; set; }
        public List<int> Codeudores { get; set; } = [];
        public string Segmento { get; set; } = string.Empty;
        public int? DiasDeMora { get; set; }
        public List<CuotaJson>? Cuotas { get; set; }
        public decimal Tolerancia { get; set; }
        public int? MesesEnE { get; set; }
        public bool PagoUnico { get; set; }
        public bool Nuevo { get; set; }
        public ReestructuracionJson? Reestructuracion { get; set; }
        public InsolvenciaJson? Insolvencia { get; set; }
        public ImpuestaJson? Analisis { get; set; }
        public ImpuestaJson? Evaluacion { get; set; }
        public RatingCategory? Modelo { get; set; }
        public SaldosJson Saldos { get; set; } = new();
        public bool? Suspendido { get; set; }
        public List<GarantiaJson> Garantias { get; set; } = [];
        public decimal Aportes { get; set; }
    }

    public sealed class CuotaJson
    {
        public int Numero { get; set; }
        public DateOnly Vencimiento { get; set; }
        public decimal SaldoVencido { get; set; }
    }

    public sealed class ReestructuracionJson
    {
        public RatingCategory CategoriaAlReestructurar { get; set; }
        public int CuotasPagadasSeguidas { get; set; }
    }

    public sealed class InsolvenciaJson
    {
        public InsolvencyProceedingStatus Estado { get; set; }
        public RatingCategory CategoriaAlIniciar { get; set; }
    }

    public sealed class ImpuestaJson
    {
        public RatingCategory Categoria { get; set; }
        public RatingSubcategory Subcategoria { get; set; } = RatingSubcategory.None;
    }

    public sealed class SaldosJson
    {
        public decimal Capital { get; set; }
        public decimal InteresCorriente { get; set; }
        public decimal InteresDeMora { get; set; }
        public decimal Otros { get; set; }
    }

    public sealed class GarantiaJson
    {
        public NaturalezaDeGarantia Naturaleza { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class GeneralJson
    {
        public string? Perfil { get; set; }
        public decimal LimiteLegalMinimo { get; set; }
        public List<ImpairmentModel> ModelosConGeneral { get; set; } = [ImpairmentModel.ArrearsAnnex1];
        public List<LineaJson> Lineas { get; set; } = [];
        public List<DimensionJson> Anteriores { get; set; } = [];
    }

    public class DimensionJson
    {
        public int Sucursal { get; set; }
        public string Modalidad { get; set; } = string.Empty;
        public string? Subsegmento { get; set; }
        public bool Empleado { get; set; }
        public ImpairmentModel Modelo { get; set; } = ImpairmentModel.ArrearsAnnex1;
        public decimal Monto { get; set; }

        public DimensionDeGeneral Dimension() => new(Sucursal, Modalidad, Subsegmento, Empleado, Modelo);
    }

    public sealed class LineaJson : DimensionJson
    {
        public int Credito { get; set; }
    }

    public sealed class SelectorJson
    {
        public string Regla { get; set; } = "regla-prueba";
        public string? Tipo { get; set; }
        public string? CategoriaDeFondo { get; set; }
        public EntregaDeCartera Entrega { get; set; } = EntregaDeCartera.K1;
        public ImpairmentModelElectionKind? Eleccion { get; set; }
        public List<string> SegmentosDeLaEleccion { get; set; } = [];
    }

    public sealed class VarianteJson
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Norma { get; set; }
        public JsonElement? Politica { get; set; }
        public OpcionesJson? Opciones { get; set; }
        public GeneralJson? General { get; set; }
        public SelectorJson? Selector { get; set; }
        public EsperadoJson Esperado { get; set; } = new();
    }

    public sealed class EsperadoJson
    {
        public List<CreditoEsperadoJson>? Creditos { get; set; }
        public List<GeneralEsperadoJson>? General { get; set; }
        public List<CeldaEsperadaJson>? Celdas { get; set; }
        public List<string>? MenosEstrictas { get; set; }
        public List<ModeloEsperadoJson>? Selector { get; set; }
        public bool? Marca { get; set; }
    }

    public sealed class CreditoEsperadoJson
    {
        public int Id { get; set; }
        public int? DiasDeMora { get; set; }
        public RatingCategory? CategoriaPorMora { get; set; }
        public RatingCategory Categoria { get; set; }
        public RatingSubcategory Subcategoria { get; set; } = RatingSubcategory.None;
        public RatingCause Causa { get; set; }
        public RatingCategory? Contable { get; set; }
        public List<string>? Candidatas { get; set; }
        public decimal? GarantiaAdmisible { get; set; }
        public decimal? Aportes { get; set; }
        public decimal? Base { get; set; }
        public decimal? Porcentaje { get; set; }
        public decimal? DeterioroCapital { get; set; }
        public decimal? DeterioroIntereses { get; set; }
        public decimal? DeterioroOtros { get; set; }
        public decimal? DeterioroTotal { get; set; }
        public List<string>? ExplicacionContiene { get; set; }
    }

    public sealed class GeneralEsperadoJson : DimensionJson
    {
        public decimal CarteraBruta { get; set; }
        public decimal PorcentajeMinimo { get; set; }
        public decimal PorcentajeAdicional { get; set; }
        public decimal MontoMinimo { get; set; }
        public decimal MontoAdicional { get; set; }
        public decimal Compensacion { get; set; }
        public decimal Total { get; set; }
        public decimal Anterior { get; set; }
        public decimal Delta { get; set; }
    }

    public sealed class CeldaEsperadaJson
    {
        public string Ruta { get; set; } = string.Empty;
        public decimal Norma { get; set; }
        public decimal Propuesta { get; set; }
        public decimal Efectiva { get; set; }
    }

    public sealed class ModeloEsperadoJson
    {
        public string Segmento { get; set; } = string.Empty;
        public ImpairmentModel Modelo { get; set; }
        public ModelBasis Fundamento { get; set; }
        public bool Marca { get; set; }
    }
}
