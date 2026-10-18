using System.Globalization;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>Por qué se rechaza un valor nacional al registrarlo. Application lo traduce a su código de error.</summary>
public enum MotivoDeRechazoDeValorNacional
{
    /// <summary><c>NationalValues.CodeUnknown</c>.</summary>
    CodigoDesconocido = 1,

    /// <summary><c>NationalValues.KeyInvalid</c>.</summary>
    ClaveInvalida = 2,

    /// <summary><c>NationalValues.CodeNotAvailable</c> (un código de una entrega posterior a la vigente: <c>UVR</c> y <c>TOPE_TASA_VIVIENDA</c> antes de K6b).</summary>
    CodigoNoDisponible = 3,

    /// <summary>La unidad no es la del código (o la del límite, en <c>LIMITE</c>).</summary>
    UnidadInvalida = 4,

    /// <summary>Falta el valor, o sobra (el <c>FESTIVO</c> no lleva).</summary>
    ValorInvalido = 5,
}

/// <summary>Un rechazo al validar un valor nacional: su motivo, el campo y el texto para quien registra.</summary>
public sealed record RechazoDeValorNacional(MotivoDeRechazoDeValorNacional Motivo, string Campo, string Mensaje);

/// <summary>
/// Definición de un código de valor nacional: unidades admitidas, si lleva clave y valor, desde qué entrega de Cartera se admite y,
/// para los índices que se leen como «último publicado» (IBR, IPC), cuántos días calendario de antigüedad admite antes
/// de alertar <c>Cartera.IndiceDesactualizado</c>.
/// </summary>
public sealed record DefinicionDeValorNacional(
    string Codigo,
    IReadOnlyList<UnidadDeValorNacional> UnidadesAdmitidas,
    bool LlevaClave,
    bool LlevaValor,
    EntregaDeCartera DisponibleDesde,
    int? AntiguedadMaximaDias,
    string Descripcion)
{
    /// <summary>¿Se admite en K1? (<c>UVR</c> y <c>TOPE_TASA_VIVIENDA</c> llegan con K6b.)</summary>
    public bool DisponibleEnK1 => DisponibleDesde == EntregaDeCartera.K1;

    /// <summary>¿Se admite con la entrega dada (por defecto, <see cref="CatalogoDeCartera.EntregaVigente"/>)? K6b (T1050) habilita UVR y el tope.</summary>
    public bool DisponibleEn(EntregaDeCartera? entrega = null) => (entrega ?? CatalogoDeCartera.EntregaVigente) >= DisponibleDesde;
}

/// <summary>
/// Catálogo cerrado de códigos de valor nacional (feature 013, T37, T40, R47; DT §2.21; data-model parte 3 §3.1, §12.3).
/// Las claves por código: <c>USURA</c> una modalidad certificada del <see cref="CatalogoDeModalidades"/> vigente (eso lo
/// valida Application con el documento); <c>IBR</c> e <c>IPC</c> un plazo o serie; <c>FESTIVO</c> la fecha ISO;
/// <c>LIMITE</c> un código de <see cref="CatalogoDeLimitesLegales"/>; los demás, vacía (<c>''</c>).
///
/// <para>La antigüedad máxima del IBR (5 días calendario: se publica cada día hábil y un puente largo no pasa de cuatro)
/// y del IPC (40: se publica una vez al mes) es configuración técnica de la alerta, no un valor legal.</para>
/// </summary>
public static class CodigosDeValorNacional
{
    public const string Usura = "USURA";
    public const string Ibr = "IBR";
    public const string Ipc = "IPC";
    public const string TopeTasaVivienda = "TOPE_TASA_VIVIENDA";
    public const string Uvr = "UVR";
    public const string Smmlv = "SMMLV";
    public const string Uvt = "UVT";
    public const string Festivo = "FESTIVO";
    public const string Limite = "LIMITE";

    // Feature 014 (T016; research R26 y C09; data-model §9.2): los valores nacionales de captaciones, cada uno desde su entrega.

    /// <summary>Porcentaje del rendimiento que no es renta (componente inflacionario) por año gravable; la clave es el año (<c>2025</c>).</summary>
    public const string ComponenteInflacionario = "COMPONENTE_INFLACIONARIO";

    /// <summary>Límite inembargable de los depósitos de ahorro (CGP art. 594), en pesos.</summary>
    public const string LimiteInembargable = "LIMITE_INEMBARGABLE";

    /// <summary>Tope de lo que se entrega a herederos sin juicio de sucesión (EOSF art. 127), en pesos.</summary>
    public const string TopeEntregaSinSucesion = "TOPE_ENTREGA_SIN_SUCESION";

    /// <summary>Tope del seguro de depósitos por ahorrador (Fogacoop), en pesos.</summary>
    public const string TopeSeguroDepositos = "TOPE_SEGURO_DEPOSITOS";

    private static readonly IReadOnlyList<DefinicionDeValorNacional> Lista =
    [
        new(Usura, [UnidadDeValorNacional.EA], LlevaClave: true, LlevaValor: true, DisponibleDesde: EntregaDeCartera.K1, null, "Tasa de usura certificada por modalidad"),
        new(Ibr, [UnidadDeValorNacional.NominalMV], true, true, EntregaDeCartera.K1, AntiguedadMaximaDias: 5, "Indicador bancario de referencia por plazo"),
        new(Ipc, [UnidadDeValorNacional.Fraccion], true, true, EntregaDeCartera.K1, AntiguedadMaximaDias: 40, "Índice de precios al consumidor por serie"),
        // K6b (T1050): el tope de vivienda y la UVR diaria, sin clave, se admiten desde K6b.
        new(TopeTasaVivienda, [UnidadDeValorNacional.EA], false, true, DisponibleDesde: EntregaDeCartera.K6b, null, "Tope de tasa de vivienda (Ley 546 de 1999)"),
        new(Uvr, [UnidadDeValorNacional.Pesos], false, true, DisponibleDesde: EntregaDeCartera.K6b, null, "Valor diario de una UVR"),
        new(Smmlv, [UnidadDeValorNacional.Pesos], false, true, EntregaDeCartera.K1, null, "Salario mínimo mensual legal vigente"),
        new(Uvt, [UnidadDeValorNacional.Pesos], false, true, EntregaDeCartera.K1, null, "Unidad de valor tributario"),
        new(Festivo, [], true, LlevaValor: false, EntregaDeCartera.K1, null, "Festivo nacional (clave = la fecha)"),
        new(Limite,
            [UnidadDeValorNacional.Fraccion, UnidadDeValorNacional.Dias, UnidadDeValorNacional.Meses, UnidadDeValorNacional.Horas, UnidadDeValorNacional.Veces],
            true, true, EntregaDeCartera.K1, null, "Límite legal (clave = código de CatalogoDeLimitesLegales)"),
        // Feature 014 (T016): no se admiten hasta que su entrega suba CatalogoDeCartera.EntregaVigente.
        new(ComponenteInflacionario, [UnidadDeValorNacional.Fraccion], LlevaClave: true, LlevaValor: true, DisponibleDesde: EntregaDeCartera.K8, null,
            "Componente inflacionario de los rendimientos financieros (clave = año gravable)"),
        new(LimiteInembargable, [UnidadDeValorNacional.Pesos], false, true, DisponibleDesde: EntregaDeCartera.K8, null,
            "Límite inembargable de los depósitos de ahorro"),
        new(TopeEntregaSinSucesion, [UnidadDeValorNacional.Pesos], false, true, DisponibleDesde: EntregaDeCartera.K10, null,
            "Tope de entrega de depósitos sin juicio de sucesión"),
        new(TopeSeguroDepositos, [UnidadDeValorNacional.Pesos], false, true, DisponibleDesde: EntregaDeCartera.K11, null,
            "Tope del seguro de depósitos por ahorrador"),
    ];

    private static readonly Dictionary<string, DefinicionDeValorNacional> PorCodigo = Lista.ToDictionary(c => c.Codigo, StringComparer.Ordinal);

    public static IReadOnlyList<DefinicionDeValorNacional> Todos => Lista;

    public static DefinicionDeValorNacional? Buscar(string? codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo, out var d) ? d : null;

    /// <summary>
    /// Valida código, clave, unidad y presencia del valor, sin leer nada más (la clave de <c>USURA</c> contra el catálogo
    /// de modalidades y el valor de un <c>LIMITE</c> contra su dirección los completa quien registra). Vacío = válido. Un código de una
    /// entrega posterior a <paramref name="entrega"/> (por defecto la vigente) se rechaza con <see cref="MotivoDeRechazoDeValorNacional.CodigoNoDisponible"/>.
    /// </summary>
    public static IReadOnlyList<RechazoDeValorNacional> Validar(string? codigo, string? clave, UnidadDeValorNacional? unidad, decimal? valor,
        EntregaDeCartera? entrega = null)
    {
        var def = Buscar(codigo);
        if (def is null)
            return [new(MotivoDeRechazoDeValorNacional.CodigoDesconocido, "code", $"«{codigo}» no es un código de valor nacional.")];
        if (!def.DisponibleEn(entrega))
            return [new(MotivoDeRechazoDeValorNacional.CodigoNoDisponible, "code", $"{def.Codigo} todavía no está disponible (llega con {def.DisponibleDesde}).")];

        var rechazos = new List<RechazoDeValorNacional>();
        clave ??= string.Empty;

        if (!def.LlevaClave && clave.Length > 0)
            rechazos.Add(new(MotivoDeRechazoDeValorNacional.ClaveInvalida, "key", $"{def.Codigo} no lleva clave."));
        else if (def.LlevaClave && string.IsNullOrWhiteSpace(clave))
            rechazos.Add(new(MotivoDeRechazoDeValorNacional.ClaveInvalida, "key", $"{def.Codigo} exige clave."));
        else if (def.Codigo == Festivo && !DateOnly.TryParseExact(clave, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            rechazos.Add(new(MotivoDeRechazoDeValorNacional.ClaveInvalida, "key", "La clave del festivo es su fecha (aaaa-mm-dd)."));
        else if (def.Codigo == ComponenteInflacionario && !(clave.Length == 4 && int.TryParse(clave, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            rechazos.Add(new(MotivoDeRechazoDeValorNacional.ClaveInvalida, "key", "La clave del componente inflacionario es el año gravable (aaaa)."));

        if (def.Codigo == Limite && clave.Length > 0)
        {
            var limite = CatalogoDeLimitesLegales.Buscar(clave);
            if (limite is null)
                rechazos.Add(new(MotivoDeRechazoDeValorNacional.ClaveInvalida, "key", $"«{clave}» no es un límite legal del catálogo."));
            else if (unidad != limite.Unidad)
                rechazos.Add(new(MotivoDeRechazoDeValorNacional.UnidadInvalida, "unit", $"El límite {clave} se expresa en {limite.Unidad}."));
        }
        else if (def.UnidadesAdmitidas.Count == 0 ? unidad is not null : unidad is null || !def.UnidadesAdmitidas.Contains(unidad.Value))
        {
            rechazos.Add(new(MotivoDeRechazoDeValorNacional.UnidadInvalida, "unit", def.UnidadesAdmitidas.Count == 0
                ? $"{def.Codigo} no lleva unidad."
                : $"{def.Codigo} se expresa en {string.Join(" o ", def.UnidadesAdmitidas)}."));
        }

        if (def.LlevaValor != valor.HasValue)
            rechazos.Add(new(MotivoDeRechazoDeValorNacional.ValorInvalido, "value", def.LlevaValor ? $"{def.Codigo} exige valor." : $"{def.Codigo} no lleva valor."));

        return rechazos;
    }
}
