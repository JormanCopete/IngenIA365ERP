using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// La fachada del motor de captaciones y aportes (feature 014, T033; research §0.4; molde <see cref="MotorDeCartera"/>): la única puerta por la que la
/// aplicación llama al cálculo de las cuentas. Pura, determinista y sin IO —sólo <c>decimal</c>, <c>DateOnly</c> y el instante R que recibe—, sin
/// valores legales (la UVT, el componente inflacionario, las tarifas y los topes llegan por parámetro). Cada pieza vive en su archivo y se prueba
/// sola; aquí sólo se juntan.
///
/// <para>
/// La fase fundacional trae la reconstrucción a (D, R) (<see cref="Reconstruir"/>), la serie de saldos diarios (<see cref="SaldosDiarios"/>), la
/// verificación del libro (<see cref="Verificar"/>) y el efecto de una fecha valor (<see cref="CambiosPorFechaValor"/>). Las historias suman, sin
/// cambiar estas entradas, el interés de captación, la retención, el GMF, el plan del CDAT, el contractual, la tasa negociada, los cargos, la
/// revalorización, el fondo de liquidez y el límite inembargable (research §0.4). Toda salida que se sella lleva <see cref="VersionDeReglas"/>.
/// </para>
/// </summary>
public static class MotorDeCaptaciones
{
    /// <summary>La versión de reglas con que calcula este programa (<c>LND-AAAA.n</c>, la misma de la 013 mientras ninguna regla de captación cambie).</summary>
    public static string VersionDeReglas => VersionesDeReglas.Vigente;

    /// <summary>Los saldos por concepto a D con la información hasta R (R04).</summary>
    public static SaldosDeCuenta Reconstruir(IEnumerable<MovimientoDeCuenta> libro, DateOnly fecha, DateTime informacionHasta) =>
        ReconstruccionDeCuenta.Reconstruir(libro, fecha, informacionHasta);

    /// <summary>Los saldos al cierre de cada día de (desde, hasta] con la información hasta R (la serie del interés por saldo diario, R05).</summary>
    public static IReadOnlyList<SaldoDelDia> SaldosDiarios(IEnumerable<MovimientoDeCuenta> libro, DateOnly desde, DateOnly hasta, DateTime informacionHasta) =>
        ReconstruccionDeCuenta.SaldosDiarios(libro, desde, hasta, informacionHasta);

    /// <summary>Las diferencias por concepto del libro contra un estado esperado a la fecha (data-model §4.2).</summary>
    public static IReadOnlyList<DiferenciaDeCuenta> Verificar(IEnumerable<MovimientoDeCuenta> libro, DateOnly fecha,
        IReadOnlyDictionary<MemberAccountConcept, decimal> esperado) => ReconstruccionDeCuenta.Verificar(libro, fecha, esperado);

    /// <summary>Lo que una operación con fecha valor anterior cambió en la serie de saldos, día por día y por concepto (FR-062).</summary>
    public static IReadOnlyList<(DateOnly Dia, MemberAccountConcept Concepto, decimal Cambio)> CambiosPorFechaValor(IEnumerable<MovimientoDeCuenta> libro,
        DateOnly desde, DateOnly hasta, DateTime antes, DateTime despues) => ReconstruccionDeCuenta.CambiosPorFechaValor(libro, desde, hasta, antes, despues);

    /// <summary>US5 (T127): el interés de una ventana día por día por el método de saldo de la versión (R05).</summary>
    public static InteresDelPeriodo Interes(EntradaDeInteres entrada) => InteresDeCaptacion.Calcular(entrada);

    /// <summary>US5 (T128): la retención en la fuente sobre los rendimientos de un abono o pago de intereses (R06).</summary>
    public static RetencionCalculada Retencion(PoliticaDeRetencion politica, EntradaDeRetencion entrada) =>
        RetencionSobreRendimientos.Calcular(politica, entrada);

    /// <summary>US6 (T166): el plan de intereses de un CDAT con sus fechas de pago hábiles (FR-023).</summary>
    public static PlanDeCdatCalculado PlanDeCdat(CondicionesDeCdat condiciones, Common.Calendar.CalendarioHabil calendario) =>
        Deposits.PlanDeCdat.Generar(condiciones, calendario);

    /// <summary>US6 (T168): la regla de tasa negociada más específica y si el margen cabe en ella y en la atribución de quien lo pide (FR-127).</summary>
    public static EvaluacionDeTasaNegociada TasaNegociada(IEnumerable<ReglaDeTasaNegociada> reglas, PedidoDeTasaNegociada pedido, decimal? atribucion) =>
        Deposits.TasaNegociada.Evaluar(reglas, pedido, atribucion);
}
