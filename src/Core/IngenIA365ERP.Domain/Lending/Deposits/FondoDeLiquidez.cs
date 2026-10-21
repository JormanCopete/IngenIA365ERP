using System.Globalization;
using System.Text.Json;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Cómo cuenta un depósito para el fondo de liquidez (Decreto 1068 de 2015, Libro 2, Parte 11, Título 7, Capítulo II, sustituido por el Decreto 976 de
/// 2026). El ahorro permanente se separa por lo que digan los estatutos: sólo retirable al desvincularse o con retiro parcial. (nuevo)
/// </summary>
public enum ClaseDeDepositoParaLiquidez
{
    ALaVista = 1,
    Cdat = 2,
    Contractual = 3,
    PermanenteConRetiroParcial = 4,
    PermanenteSoloAlRetirarse = 5,
}

/// <summary>
/// El régimen del fondo: <see cref="General"/> (porcentaje plano sobre los depósitos; Decreto 976 de 2026, Sección 1: básica, intermedia, fondos de empleados
/// y mutuales) o <see cref="PorMadurez"/> (porcentaje por la madurez residual sobre capital e intereses; Sección 2: categoría plena). (nuevo)
/// </summary>
public enum RegimenDeFondoDeLiquidez
{
    General = 1,
    PorMadurez = 2,
}

/// <summary>Una banda de madurez residual: hasta <see cref="HastaDias"/> días (nulo = sin tope) con su porcentaje como fracción. (nuevo)</summary>
public sealed record BandaDeMadurez(int? HastaDias, decimal Porcentaje);

/// <summary>
/// Las reglas del fondo para una categoría de la entidad, tal como las guarda el parámetro <c>FondoDeLiquidez.PorcentajesPorCategoria</c> (con su
/// vigencia, por cooperativa): el régimen, el porcentaje de los depósitos y del ahorro permanente sólo retirable al desvincularse, las bandas de madurez del
/// régimen por madurez, si la base lleva los intereses causados y por causar, si el CDAT o contractual con redención anticipada pactada va a la primera banda,
/// y el fin de la transición (mientras no llega, rige el régimen general y el nuevo se informa). (nuevo)
/// </summary>
public sealed record ReglaDeFondoDeLiquidez(
    RegimenDeFondoDeLiquidez Regimen,
    decimal PorcentajeDepositos,
    decimal PorcentajePermanenteSoloAlRetirarse,
    IReadOnlyList<BandaDeMadurez> Bandas,
    bool IncluyeInteresesCausados,
    bool IncluyeInteresesPorCausar,
    bool RedencionAnticipadaEnLaPrimeraBanda,
    DateOnly? FinDeLaTransicion,
    string Fuente);

/// <summary>
/// Los pisos legales vigentes (valores nacionales <c>LIMITE</c> con vigencia): el porcentaje de los depósitos, el del ahorro permanente sólo retirable al
/// desvincularse y, en categoría plena, el de la banda media y el del permanente. La cooperativa puede ser más estricta, nunca menos: rige el mayor. Un piso
/// nulo no acota. (nuevo)
/// </summary>
public sealed record PisosDelFondoDeLiquidez(decimal? Depositos, decimal? PermanenteSoloAlRetirarse, decimal? BandaMediaPlena, decimal? PermanenteSoloAlRetirarsePlena)
{
    public static PisosDelFondoDeLiquidez Ninguno { get; } = new(null, null, null, null);
}

/// <summary>Un depósito (una cuenta) a la fecha: su clase, capital, interés causado, interés por causar hasta el vencimiento y días al vencimiento. (nuevo)</summary>
public sealed record DepositoParaFondo(
    ClaseDeDepositoParaLiquidez Clase,
    decimal Capital,
    decimal InteresCausado,
    decimal InteresPorCausar,
    int? DiasAlVencimiento,
    bool RedencionAnticipadaPactada);

/// <summary>Un renglón de la base: la clase, la banda (nula en el régimen general), las cuentas, la base, el porcentaje y el requerido. (nuevo)</summary>
public sealed record RenglonDelFondo(
    ClaseDeDepositoParaLiquidez Clase,
    string? Banda,
    int Cuentas,
    decimal Capital,
    decimal Intereses,
    decimal Base,
    decimal Porcentaje,
    decimal Requerido);

/// <summary>
/// El cálculo del fondo a una fecha: el régimen que rige, los renglones, el requerido y, en transición, el cálculo del régimen nuevo como información. (nuevo)
/// </summary>
public sealed record CalculoDelFondoDeLiquidez(
    RegimenDeFondoDeLiquidez Regimen,
    IReadOnlyList<RenglonDelFondo> Renglones,
    decimal Requerido,
    CalculoDelFondoDeLiquidez? RegimenNuevoEnTransicion,
    DateOnly? FinDeLaTransicion,
    IReadOnlyList<string> Explicacion);

/// <summary>
/// Motor puro del fondo de liquidez (feature 014, US15, T214; FR-025, FR-109; research R25 y §24). Sin reloj ni base: recibe la fecha, la regla de la
/// categoría de la entidad, la regla general (la que rige durante la transición), los pisos legales y los depósitos, y devuelve la base y el requerido.
/// <list type="bullet">
/// <item><b>Régimen general</b> (Decreto 1068 de 2015 art. 2.11.7.2.1, hoy 2.11.7.2.1.1 por el Decreto 976 de 2026): el porcentaje de los depósitos sobre el
/// saldo de cada clase y el del ahorro permanente sólo retirable al desvincularse sobre el suyo; la base es el capital (y los intereses causados si la
/// regla lo dice).</item>
/// <item><b>Régimen por madurez</b> (categoría plena, art. 2.11.7.2.2.1 y siguientes del Decreto 976 de 2026): base = capital más intereses causados y por
/// causar hasta la madurez contractual; el CDAT y el contractual van a la banda de su madurez residual (vencidos o con redención anticipada pactada, a la
/// primera); el ahorro a la vista y el permanente con retiro parcial, al porcentaje de los depósitos; el permanente sólo retirable al desvincularse, al
/// suyo.</item>
/// <item><b>Transición</b>: si la regla de la categoría es por madurez y la fecha es anterior a su fin, rige la general y la nueva se calcula aparte.</item>
/// </list>
/// Cada porcentaje es el mayor entre el de la cooperativa y su piso legal. El requerido de cada renglón se redondea al centavo. (nuevo)
/// </summary>
public static class FondoDeLiquidez
{
    private const int Centavos = 2;

    public static CalculoDelFondoDeLiquidez Calcular(DateOnly fecha, ReglaDeFondoDeLiquidez regla, ReglaDeFondoDeLiquidez general, PisosDelFondoDeLiquidez pisos,
        IReadOnlyList<DepositoParaFondo> depositos)
    {
        ArgumentNullException.ThrowIfNull(regla);
        ArgumentNullException.ThrowIfNull(general);
        ArgumentNullException.ThrowIfNull(pisos);
        ArgumentNullException.ThrowIfNull(depositos);

        if (regla.Regimen == RegimenDeFondoDeLiquidez.PorMadurez && regla.FinDeLaTransicion is { } fin && fecha < fin)
        {
            var vigente = CalcularRegimen(general with { Regimen = RegimenDeFondoDeLiquidez.General }, pisos, depositos, enTransicion: false);
            var nuevo = CalcularRegimen(regla, pisos, depositos, enTransicion: true);
            var explicacion = vigente.Explicacion.Prepend(
                    $"En transición hasta el {fin:yyyy-MM-dd}: rige el régimen general; el de madurez residual ({regla.Fuente}) se informa y requeriría {nuevo.Requerido:N2}.")
                .ToList();
            return vigente with { RegimenNuevoEnTransicion = nuevo, FinDeLaTransicion = fin, Explicacion = explicacion };
        }
        return CalcularRegimen(regla, pisos, depositos, enTransicion: false);
    }

    private static CalculoDelFondoDeLiquidez CalcularRegimen(ReglaDeFondoDeLiquidez regla, PisosDelFondoDeLiquidez pisos, IReadOnlyList<DepositoParaFondo> depositos,
        bool enTransicion)
    {
        var plena = regla.Regimen == RegimenDeFondoDeLiquidez.PorMadurez;
        var pDepositos = Mayor(regla.PorcentajeDepositos, pisos.Depositos);
        var pPermanente = Mayor(regla.PorcentajePermanenteSoloAlRetirarse, plena ? pisos.PermanenteSoloAlRetirarsePlena ?? pisos.PermanenteSoloAlRetirarse : pisos.PermanenteSoloAlRetirarse);
        var bandas = plena ? Ordenadas(regla.Bandas) : [];
        var explicacion = new List<string>
        {
            plena
                ? $"Régimen por madurez residual ({regla.Fuente}): base = capital{(regla.IncluyeInteresesCausados ? " + intereses causados" : string.Empty)}{(regla.IncluyeInteresesPorCausar ? " + intereses por causar" : string.Empty)}."
                : $"Régimen general ({regla.Fuente}): {Pct(pDepositos)} de los depósitos y {Pct(pPermanente)} del ahorro permanente sólo retirable al desvincularse.",
        };

        var grupos = new Dictionary<(ClaseDeDepositoParaLiquidez Clase, int Banda), List<DepositoParaFondo>>();
        foreach (var d in depositos.Where(d => d.Capital != 0m || d.InteresCausado != 0m))
        {
            var banda = plena && d.Clase is ClaseDeDepositoParaLiquidez.Cdat or ClaseDeDepositoParaLiquidez.Contractual
                ? IndiceDeBanda(bandas, d.RedencionAnticipadaPactada && regla.RedencionAnticipadaEnLaPrimeraBanda ? 0 : d.DiasAlVencimiento ?? 0)
                : -1;
            var clave = (d.Clase, banda);
            if (!grupos.TryGetValue(clave, out var lista)) grupos[clave] = lista = [];
            lista.Add(d);
        }

        var renglones = new List<RenglonDelFondo>();
        foreach (var ((clase, banda), lista) in grupos.OrderBy(g => g.Key.Clase).ThenBy(g => g.Key.Banda))
        {
            var capital = lista.Sum(d => d.Capital);
            var intereses = (regla.IncluyeInteresesCausados ? lista.Sum(d => d.InteresCausado) : 0m)
                            + (plena && regla.IncluyeInteresesPorCausar ? lista.Sum(d => d.InteresPorCausar) : 0m);
            var porcentaje = banda >= 0
                ? PorcentajeDeBanda(bandas, banda, pDepositos, pisos.BandaMediaPlena)
                : clase == ClaseDeDepositoParaLiquidez.PermanenteSoloAlRetirarse ? pPermanente : pDepositos;
            var baseDelRenglon = capital + intereses;
            var requerido = Math.Round(Math.Max(0m, baseDelRenglon) * porcentaje, Centavos, MidpointRounding.AwayFromZero);
            renglones.Add(new RenglonDelFondo(clase, banda >= 0 ? NombreDeBanda(bandas, banda) : null, lista.Count, capital, intereses, baseDelRenglon, porcentaje,
                requerido));
        }
        var total = renglones.Sum(r => r.Requerido);
        explicacion.Add($"Requerido{(enTransicion ? " (régimen nuevo, informativo)" : string.Empty)}: {total:N2} sobre una base de {renglones.Sum(r => r.Base):N2}.");
        return new CalculoDelFondoDeLiquidez(regla.Regimen, renglones, total, null, null, explicacion);
    }

    /// <summary>El porcentaje de una banda: el de la cooperativa, nunca menor que el piso de los depósitos en la primera ni que el de la banda media en las demás con porcentaje.</summary>
    private static decimal PorcentajeDeBanda(IReadOnlyList<BandaDeMadurez> bandas, int indice, decimal pDepositos, decimal? pisoMedia)
    {
        var propio = bandas[indice].Porcentaje;
        if (indice == 0) return Math.Max(propio, pDepositos);
        return propio > 0m ? Mayor(propio, pisoMedia) : propio;
    }

    private static decimal Mayor(decimal propio, decimal? piso) => piso is { } p ? Math.Max(propio, p) : propio;

    /// <summary>Las bandas por su tope, la abierta al final; sin bandas, una sola abierta al cero (nada se exige por madurez).</summary>
    public static IReadOnlyList<BandaDeMadurez> Ordenadas(IReadOnlyList<BandaDeMadurez> bandas)
    {
        ArgumentNullException.ThrowIfNull(bandas);
        var lista = bandas.OrderBy(b => b.HastaDias ?? int.MaxValue).ToList();
        if (lista.Count == 0 || lista[^1].HastaDias is not null) lista.Add(new BandaDeMadurez(null, 0m));
        return lista;
    }

    /// <summary>El índice de la banda de unos días al vencimiento (vencido o cero: la primera).</summary>
    public static int IndiceDeBanda(IReadOnlyList<BandaDeMadurez> bandas, int dias)
    {
        ArgumentNullException.ThrowIfNull(bandas);
        for (var i = 0; i < bandas.Count; i++)
            if (bandas[i].HastaDias is not { } hasta || dias <= hasta) return i;
        return bandas.Count - 1;
    }

    /// <summary>El nombre de la banda («0 a 30 días», «31 a 540 días», «más de 540 días»).</summary>
    public static string NombreDeBanda(IReadOnlyList<BandaDeMadurez> bandas, int indice)
    {
        ArgumentNullException.ThrowIfNull(bandas);
        var desde = indice == 0 ? 0 : (bandas[indice - 1].HastaDias ?? 0) + 1;
        return bandas[indice].HastaDias is { } hasta
            ? string.Create(CultureInfo.InvariantCulture, $"{desde} a {hasta} días")
            : string.Create(CultureInfo.InvariantCulture, $"más de {desde - 1} días");
    }

    private static string Pct(decimal fraccion) => (fraccion * 100m).ToString("0.##", CultureInfo.GetCultureInfo("es-CO")) + " %";
}

/// <summary>
/// Las reglas del fondo por categoría leídas del JSON del parámetro <c>FondoDeLiquidez.PorcentajesPorCategoria</c> (feature 014, US15, T214). La categoría
/// <c>*</c> es la de cualquier categoría que no tenga la suya y también la que rige durante la transición. Forma:
/// <code>
/// { "*":     { "regimen": "General", "porcentajeDepositos": 0.10, "porcentajePermanenteSoloAlRetirarse": 0.02, "fuente": "…" },
///   "Plena": { "regimen": "PorMadurez", "porcentajeDepositos": 0.10, "porcentajePermanenteSoloAlRetirarse": 0.05,
///              "bandas": [ { "hastaDias": 30, "porcentaje": 0.10 }, { "hastaDias": 540, "porcentaje": 0.05 }, { "hastaDias": null, "porcentaje": 0 } ],
///              "incluyeInteresesCausados": true, "incluyeInteresesPorCausar": true, "redencionAnticipadaEnLaPrimeraBanda": true,
///              "finDeLaTransicion": "2030-02-06", "fuente": "…" } }
/// </code>
/// (nuevo)
/// </summary>
public static class ReglasDelFondoDeLiquidez
{
    public const string Cualquiera = "*";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    /// <summary>Lee el JSON; nulo si está vacío o mal formado (el lector lo trata como falta de reglas).</summary>
    public static IReadOnlyDictionary<string, ReglaDeFondoDeLiquidez>? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var crudo = JsonSerializer.Deserialize<Dictionary<string, ReglaJson>>(json, Opciones);
            if (crudo is null || crudo.Count == 0) return null;
            var reglas = new Dictionary<string, ReglaDeFondoDeLiquidez>(StringComparer.OrdinalIgnoreCase);
            foreach (var (categoria, r) in crudo)
            {
                if (r.PorcentajeDepositos is not { } p || p < 0m || p > 1m) return null;
                var regimen = Enum.TryParse<RegimenDeFondoDeLiquidez>(r.Regimen, true, out var x) ? x : RegimenDeFondoDeLiquidez.General;
                reglas[categoria.Trim()] = new ReglaDeFondoDeLiquidez(regimen, p, r.PorcentajePermanenteSoloAlRetirarse ?? p,
                    (r.Bandas ?? []).Select(b => new BandaDeMadurez(b.HastaDias, b.Porcentaje)).ToList(),
                    r.IncluyeInteresesCausados ?? regimen == RegimenDeFondoDeLiquidez.PorMadurez,
                    r.IncluyeInteresesPorCausar ?? regimen == RegimenDeFondoDeLiquidez.PorMadurez,
                    r.RedencionAnticipadaEnLaPrimeraBanda ?? true,
                    DateOnly.TryParse(r.FinDeLaTransicion, CultureInfo.InvariantCulture, out var fin) ? fin : null,
                    r.Fuente ?? string.Empty);
            }
            return reglas;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>La regla de la categoría (sin distinguir mayúsculas) o la de <c>*</c>; nula si no hay ninguna.</summary>
    public static ReglaDeFondoDeLiquidez? Para(IReadOnlyDictionary<string, ReglaDeFondoDeLiquidez> reglas, string? categoria)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        if (!string.IsNullOrWhiteSpace(categoria) && reglas.TryGetValue(categoria.Trim(), out var propia)) return propia;
        return reglas.TryGetValue(Cualquiera, out var general) ? general : null;
    }

    private sealed class ReglaJson
    {
        public string? Regimen { get; set; }
        public decimal? PorcentajeDepositos { get; set; }
        public decimal? PorcentajePermanenteSoloAlRetirarse { get; set; }
        public List<BandaJson>? Bandas { get; set; }
        public bool? IncluyeInteresesCausados { get; set; }
        public bool? IncluyeInteresesPorCausar { get; set; }
        public bool? RedencionAnticipadaEnLaPrimeraBanda { get; set; }
        public string? FinDeLaTransicion { get; set; }
        public string? Fuente { get; set; }
    }

    private sealed class BandaJson
    {
        public int? HastaDias { get; set; }
        public decimal Porcentaje { get; set; }
    }
}
