namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos del proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 3) y el rango que propone cada periodicidad —el mismo cálculo que
/// <c>PeriodoDeCausacion</c> del servidor, que es quien valida—. Los números son los de los enums de la API (<c>AccrualProcessStatus</c>,
/// <c>AccrualProcessPeriodicity</c>, <c>AccrualProcessConcepts</c>, <c>AccrualExclusionReason</c>), que llegan como número. Ninguno muestra un código.
/// (nuevo)
/// </summary>
public static class TextosDeCausacion
{
    public const int Borrador = 1;
    public const int EsperandoAprobacion = 2;
    public const int EnFirme = 3;
    public const int Reprocesado = 4;
    public const int Anulado = 5;

    public const int Diaria = 1;
    public const int Semanal = 7;
    public const int Decadal = 10;
    public const int Quincenal = 15;
    public const int Mensual = 30;

    public const int Incluido = 1;
    public const int Excluido = 2;

    public static readonly IReadOnlyList<(string Nombre, int Valor, string Texto)> Estados =
    [
        ("Draft", Borrador, "Borrador"), ("PendingApproval", EsperandoAprobacion, "Esperando aprobación"), ("Confirmed", EnFirme, "En firme"),
        ("Reprocessed", Reprocesado, "Reprocesado"), ("Voided", Anulado, "Anulado"),
    ];

    public static string Estado(int estado) => Estados.FirstOrDefault(e => e.Valor == estado).Texto ?? estado.ToString();

    public static readonly IReadOnlyList<(int Valor, string Texto)> Periodicidades =
        [(Mensual, "Mensual"), (Quincenal, "Quincenal"), (Decadal, "Decadal"), (Semanal, "Semanal"), (Diaria, "Diaria")];

    public static string Periodicidad(int valor, byte? subperiodo = null)
    {
        var texto = Periodicidades.FirstOrDefault(p => p.Valor == valor).Texto ?? valor.ToString();
        return subperiodo is { } s
            ? valor == Quincenal ? $"{texto} ({(s == 1 ? "primera" : "segunda")} quincena)" : $"{texto} ({Ordinal(s)} década)"
            : texto;
    }

    private static string Ordinal(byte n) => n switch { 1 => "primera", 2 => "segunda", _ => "tercera" };

    /// <summary>Los conceptos (bandera) con su nombre y su tipo de hecho.</summary>
    public static readonly IReadOnlyList<(int Valor, string TipoDeHecho, string Texto)> Conceptos =
    [
        (1, "InteresCausado", "Interés corriente"), (2, "MoraCausada", "Interés de mora"), (4, "SeguroCausado", "Seguro"),
        (8, "AdministracionCausada", "Administración"), (16, "CorreccionMonetariaUvrCausada", "Otros rubros (corrección monetaria UVR)"),
    ];

    public static string Concepto(int? valor) => Conceptos.FirstOrDefault(c => c.Valor == valor).Texto ?? "—";

    public static string ConceptoDelHecho(string? tipoDeHecho) => Conceptos.FirstOrDefault(c => c.TipoDeHecho == tipoDeHecho).Texto ?? "—";

    public static string ListaDeConceptos(IEnumerable<int> valores) => string.Join(", ", valores.Select(v => Concepto(v)));

    /// <summary>El concepto del libro de una línea (<c>LoanConcept</c>).</summary>
    public static string ConceptoDelLibro(int? concepto) => concepto switch
    {
        null => "—",
        2 => "Interés corriente",
        3 => "Interés de mora",
        4 => "Interés corriente en suspenso",
        5 => "Interés de mora en suspenso",
        6 => "Seguro",
        11 => "Administración",
        _ => "Otro",
    };

    public static readonly IReadOnlyList<(string Nombre, int Valor, string Texto)> Motivos =
    [
        ("AccountingPeriodClosed", 1, "Mes contable cerrado"), ("AccountingPeriodMissing", 2, "Mes contable inexistente"),
        ("AccountingNotStarted", 3, "Contabilidad sin iniciar"), ("LendingMonthClosed", 4, "Mes cerrado por Cartera"),
        ("PostingRuleMissing", 5, "Falta la regla contable"), ("OutOfScope", 6, "Fuera de su alcance"),
        ("OutOfFilter", 7, "Comparte el hecho con créditos fuera del filtro"), ("LaterPayment", 8, "Con abonos posteriores (intacto)"),
        ("SharedFact", 9, "Va en un hecho que quedó fuera"), ("AccountingNoResponse", 10, "Contabilidad no respondió"),
    ];

    public static string Motivo(int? motivo) => motivo is null ? "—" : Motivos.FirstOrDefault(m => m.Valor == motivo).Texto ?? "Fuera";

    public static string EstadoDelItem(int estado) => estado == Incluido ? "Se contabiliza" : "Queda fuera";

    public static string MetodoDeAprobacion(int? metodo) => metodo switch
    {
        2 => "llave del aprobador",
        3 => "código del aprobador",
        1 => "sesión propia",
        _ => "—",
    };

    public static string EstadoDelLote(int estado) => estado switch
    {
        0 => "Pedido",
        1 => "Contabilizándose",
        2 => "Contabilizado",
        3 => "Contabilizado con rechazos",
        4 => "Vacío",
        _ => estado.ToString(),
    };

    /// <summary>
    /// El rango que propone la periodicidad para <paramref name="referencia"/>: el mes; la quincena (del 1 al 15 o del 16 al fin de mes); la década (1–10,
    /// 11–20, 21–fin); la semana de lunes a domingo; o el día.
    /// </summary>
    public static (DateOnly Desde, DateOnly Hasta) Proponer(int periodicidad, DateOnly referencia, byte? subperiodo)
    {
        var primero = new DateOnly(referencia.Year, referencia.Month, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        switch (periodicidad)
        {
            case Diaria:
                return (referencia, referencia);
            case Semanal:
                var lunes = referencia.AddDays(-(((int)referencia.DayOfWeek + Semanal - 1) % Semanal));
                return (lunes, lunes.AddDays(Semanal - 1));
            case Quincenal:
            case Decadal:
                var cuantos = Mensual / periodicidad;
                var numero = subperiodo is { } s && s >= 1 && s <= cuantos ? s : Math.Min(cuantos, (referencia.Day - 1) / periodicidad + 1);
                var desde = primero.AddDays((numero - 1) * periodicidad);
                return (desde, numero == cuantos ? ultimo : desde.AddDays(periodicidad - 1));
            default:
                return (primero, ultimo);
        }
    }

    /// <summary>Cuántos subperíodos tiene la periodicidad (2 quincenas, 3 décadas; 0 las demás).</summary>
    public static int Subperiodos(int periodicidad) => periodicidad is Quincenal or Decadal ? Mensual / periodicidad : 0;
}
