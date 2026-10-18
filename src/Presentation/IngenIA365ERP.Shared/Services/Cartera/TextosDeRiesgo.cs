namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos del riesgo y los cierres de Cartera (feature 013, US9, T0714, T0715): causas de la calificación, clases y estados de un cierre, los
/// requisitos del cierre del mes, los modelos de deterioro por segmento y sus marcas. Ningún texto muestra códigos técnicos ni el nombre de otro
/// sistema. (nuevo)
/// </summary>
public static class TextosDeRiesgo
{
    public static IReadOnlyList<string> Categorias { get; } = ["A", "B", "C", "D", "E"];

    public static IReadOnlyList<(string Valor, string Texto)> Causas { get; } =
    [
        ("Arrears", "Altura de mora"),
        ("Model", "Modelo de referencia"),
        ("Drag", "Arrastre"),
        ("Restructured", "Reestructurado"),
        ("Evaluation", "Evaluación de cartera"),
        ("Analysis", "Análisis"),
        ("SingleInstallment", "Pago único"),
        ("NewLoan", "Crédito nuevo"),
        ("Insolvency", "Insolvencia"),
        ("Homologation", "Homologación"),
    ];

    public static string Causa(string? v) => Causas.FirstOrDefault(c => c.Valor == v).Texto ?? v ?? "—";

    /// <summary>La categoría con su subcategoría (E1, E2) si la trae.</summary>
    public static string Categoria(string? categoria, string? subcategoria) =>
        categoria is null ? "—" : string.IsNullOrWhiteSpace(subcategoria) || subcategoria == "None" ? categoria : subcategoria;

    public static string ClaseDeCierre(string? v) => v switch
    {
        "Base" => "Cierre base (activación)",
        "Monthly" => "Mensual",
        _ => v ?? "—",
    };

    public static string EstadoDeCierre(string? v) => v switch
    {
        "Closed" => "Cerrado",
        "Reopened" => "Reabierto",
        _ => v ?? "—",
    };

    public static string Requisito(string? v) => v switch
    {
        "PreviousClose" => "Cierre anterior vigente",
        "AccountingPeriodOpen" => "Mes abierto en Contabilidad",
        "AccrualUpToDate" => "Causación al día hasta fin de mes",
        "ReplicaSynced" => "Valores nacionales al día",
        "NoRunInProgress" => "Ninguna corrida de cierre en curso",
        _ => v ?? "—",
    };

    public static string Modelo(string? v) => v switch
    {
        "ArrearsAnnex1" => "Por altura de mora (Anexo 1)",
        "ExpectedLossAnnex2" => "Pérdida esperada (Anexo 2)",
        _ => v ?? "—",
    };

    public static string Fundamento(string? v) => v switch
    {
        "NotObligated" => "No obligada",
        "Obligated" => "Obligada",
        "Voluntary" => "Adopción voluntaria",
        _ => v ?? "—",
    };

    public static string Marca(string? v) => v switch
    {
        "ObligatoryModelUnavailable" => "Modelo obligatorio no disponible: se aplica el Anexo 1",
        null or "" => "—",
        _ => v,
    };

    /// <summary>Los tres segmentos de la pérdida esperada del Anexo 2 (K6a).</summary>
    public static IReadOnlyList<(string Codigo, string Texto)> SegmentosDePerdidaEsperada { get; } =
    [
        ("CONSUMO_LIBRANZA", "Consumo con libranza"),
        ("CONSUMO_SIN_LIBRANZA", "Consumo sin libranza"),
        ("COMERCIAL_PN", "Comercial persona natural"),
    ];

    public static string Segmento(string? v) => SegmentosDePerdidaEsperada.FirstOrDefault(s => s.Codigo == v).Texto ?? v ?? "—";

    public static string ClaseDeEleccion(string? v) => v switch
    {
        "VoluntaryAdoption" => "Adopción voluntaria",
        "VoluntaryWithdrawal" => "Desistimiento",
        _ => v ?? "—",
    };

    /// <summary>El tipo de garantía con que se tomó la pérdida dado el incumplimiento (claves del Anexo 2).</summary>
    public static string GarantiaDePdi(string? v) => v switch
    {
        "PRENDARIA" => "Prendaria",
        "HIPOTECARIA" => "Hipotecaria",
        "FONDOS_GARANTIAS" => "Fondo de garantías",
        "NO_IDONEA" => "Garantía no idónea",
        "SIN_GARANTIA" => "Sin garantía",
        null or "" => "—",
        _ => v,
    };

    /// <summary>La tabla de probabilidad de incumplimiento que se usó: la del tipo de organización o la de la aplicación voluntaria.</summary>
    public static string TablaDePi(string? v) => v switch
    {
        "VOLUNTARIA" => "Aplicación voluntaria",
        null or "" => "—",
        _ => v.Replace('_', ' ').ToLowerInvariant(),
    };

    public static string EstadoDeVersion(string? v) => v switch
    {
        "PendingValidation" => "Pendiente de validar",
        "Validated" => "Validada",
        _ => v ?? "—",
    };

    public static string Mes(int mes) => mes is >= 1 and <= 12
        ? new System.Globalization.CultureInfo("es-CO").DateTimeFormat.GetMonthName(mes)
        : mes.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
