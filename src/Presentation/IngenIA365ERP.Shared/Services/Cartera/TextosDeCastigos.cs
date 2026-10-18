using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de US14 (feature 013, K3, T0917): estados del lote de castigo y de sus créditos, requisitos de la norma y de la política,
/// estados de la evaluación de cartera y categorías. Los enums llegan como número; ningún texto muestra un código técnico (FR-013). (nuevo)
/// </summary>
public static class TextosDeCastigos
{
    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> EstadosDeLote { get; } =
    [
        ("Draft", 1, "Borrador"), ("Submitted", 2, "En el consejo"), ("Approved", 3, "Aprobado"), ("Rejected", 4, "Negado"), ("Applied", 5, "Aplicado"),
        ("Cancelled", 6, "Anulado"),
    ];

    public static string EstadoDeLote(int v) => EstadosDeLote.FirstOrDefault(e => e.Valor == v).Texto ?? "—";

    public static string EstadoDelCredito(int v) => v switch { 1 => "Incluido", 2 => "Rechazado", 3 => "Castigado", _ => "—" };

    public static string Requisito(string? codigo) => codigo switch
    {
        "NotAnAssociate" => "Ya no es asociado",
        "ContributionsOffset" => "Aportes cruzados",
        "ReasonedReport" => "Informe motivado",
        "IrrecoverabilityOpinion" => "Concepto de irrecuperabilidad",
        "FullImpairment" => "Deterioro total",
        "MinimumDaysPastDue" => "Altura mínima de mora",
        "CollectionEffortsExhausted" => "Gestiones agotadas",
        _ => codigo ?? "—",
    };

    public static string Origen(string? origen) => origen switch { "Norma" => "Norma", "Politica" => "Política", _ => origen ?? "—" };

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> EstadosDeEvaluacion { get; } =
    [
        ("Draft", 1, "Borrador"), ("Recommended", 2, "Recomendada por el comité"), ("Approved", 3, "Aprobada por el consejo"), ("Applied", 4, "Aplicada"),
        ("Rejected", 5, "Negada"), ("Discarded", 6, "Descartada"),
    ];

    public static string EstadoDeEvaluacion(int v) => EstadosDeEvaluacion.FirstOrDefault(e => e.Valor == v).Texto ?? "—";

    /// <summary>La categoría (1 A … 5 E) con su subcategoría (1 E1, 2 E2) si la trae.</summary>
    public static string Categoria(int categoria, int? subcategoria = null) => subcategoria switch
    {
        1 => "E1",
        2 => "E2",
        _ => categoria is >= 1 and <= 5 ? TextosDeRiesgo.Categorias[categoria - 1] : categoria.ToString(CultureInfo.InvariantCulture),
    };

    public static string Resultado(int? v) => v is { } r ? TextosDeNovedades.Decision(r) : "Sin decidir";

    public static string Punto(string? acta, int orden) => $"{(string.IsNullOrWhiteSpace(acta) ? "Acta sin número" : $"Acta {acta}")}, punto {orden}";
}
