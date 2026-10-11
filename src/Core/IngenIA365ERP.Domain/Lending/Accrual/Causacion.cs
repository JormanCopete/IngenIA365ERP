using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Accrual;

/// <summary>
/// A qué se causa: el concepto, la cuota y si es la porción de la gracia total (que lleva su propio redondeo, como la cobra el plan).
/// </summary>
public readonly record struct ClaveDeCausacion(LoanConcept Concepto, int Cuota, bool DeGracia = false);

/// <summary>
/// Lo que define una ventana de causación: la base, la tasa diaria y el período en que corre. Si cualquiera cambia, la ventana se
/// cierra y empieza otra (T25).
/// </summary>
public readonly record struct ParametrosDeVentana(decimal Base, decimal TasaDiaria, int Periodo);

/// <summary>
/// Lo que se quiere causar a una clave en un tramo: sus parámetros y, para la explicación, la EA de donde sale la tasa, el tope del
/// día, si se topó y si va a suspenso.
/// </summary>
public readonly record struct VentanaDeseada(ParametrosDeVentana Parametros, decimal TasaEA, decimal? TopeDiario, bool Topada, bool Suspendida);

/// <summary>Una ventana de causación: desde (excluida) hasta (incluida; nula mientras sigue abierta).</summary>
public sealed class VentanaDeCausacion
{
    internal VentanaDeCausacion(ClaveDeCausacion clave, DateOnly desde, VentanaDeseada deseada)
    {
        Clave = clave;
        Desde = desde;
        Deseada = deseada;
    }

    public ClaveDeCausacion Clave { get; }

    public DateOnly Desde { get; }

    public DateOnly? Hasta { get; internal set; }

    public VentanaDeseada Deseada { get; }

    public ParametrosDeVentana Parametros => Deseada.Parametros;
}

/// <summary>
/// La causación por <b>diferencia de acumulados</b> (feature 013, US4, T0487; T25, R33, M4; FR-010, FR-062–FR-064, D9):
/// <list type="bullet">
/// <item>Lo causado el día d por concepto y cuota es <c>U(A(d)) − U(A(d−1))</c>, con A el acumulado exacto —la suma de sus ventanas—
/// y U el redondeo sellado del concepto (<see cref="RedondeoDeCartera.Concepto"/>): el libro nunca deriva y, con pagos a tiempo, lo
/// causado entre dos vencimientos es exactamente el interés de la cuota.</item>
/// <item>Ventana compuesta <c>B·((1 + r_d)^n − 1)</c> o lineal <c>B·r_d·n</c>, con n los días de la convención desde su inicio; se
/// reinicia sólo si cambian la base, la tasa del día o el período (<see cref="ParametrosDeVentana"/>), así el interés no capitaliza
/// entre períodos ni en la gracia total.</item>
/// <item>Corriente sobre capital no vencido —incluido el de una cuota cuyo vencimiento cayó en día no hábil, hasta su exigible—; mora
/// sobre capital vencido (y sobre la corriente de la cuota sólo con la regla sellada, nunca en vivienda); el interés nunca entra a la
/// base de la corriente. La base y el período los decide la reconstrucción; aquí vive la aritmética.</item>
/// <item>Seguros (D9): al vencer la cuota (<see cref="InsuranceAccrualTiming.AtDueDate"/>) o a prorrata de los días del período
/// (<see cref="InsuranceAccrualTiming.Daily"/>), sobre lo que el plan proyectó para la cuota.</item>
/// </list>
/// Pura: <c>decimal</c> y <c>DateOnly</c>; usa <see cref="ContadorDeDias"/> y <see cref="EquivalenciaDeTasas"/>. (nuevo)
/// </summary>
public static class Causacion
{
    /// <summary>El acumulado exacto de una ventana de <paramref name="dias"/> días.</summary>
    public static decimal Acumulado(decimal @base, decimal tasaDiaria, int dias, RateEquivalence equivalencia) =>
        dias <= 0 ? 0m : @base * EquivalenciaDeTasas.TasaDelPeriodo(tasaDiaria, dias, equivalencia);

    /// <summary>Lo que se causa un día: <c>U(A(d)) − U(A(d−1))</c>.</summary>
    public static decimal DelDia(RedondeoDeCartera redondeo, decimal acumuladoAnterior, decimal acumuladoDelDia)
    {
        ArgumentNullException.ThrowIfNull(redondeo);
        return redondeo.Concepto(acumuladoDelDia) - redondeo.Concepto(acumuladoAnterior);
    }

    /// <summary>
    /// Lo causado del seguro de una cuota hasta <paramref name="dia"/>, sin redondear (D9). Nada hasta el origen del crédito; al vencer,
    /// todo; con <see cref="InsuranceAccrualTiming.Daily"/>, la proporción de los días del período ya corridos (si el período no tiene
    /// días en la convención, todo al vencer).
    /// </summary>
    public static decimal SeguroExacto(CuotaDelPlan cuota, DateOnly dia, InsuranceAccrualTiming momento, DayCountConvention convencion, DateOnly origen)
    {
        ArgumentNullException.ThrowIfNull(cuota);
        var prima = cuota.Seguro;
        if (prima == 0m || dia <= origen) return 0m;
        if (momento == InsuranceAccrualTiming.Daily)
        {
            if (dia >= cuota.Vencimiento) return prima;
            var total = cuota.Vencimiento > cuota.Desde ? ContadorDeDias.Dias(convencion, cuota.Desde, cuota.Vencimiento) : 0;
            if (total == 0 || dia <= cuota.Desde) return 0m;
            return prima * ContadorDeDias.Dias(convencion, cuota.Desde, dia) / total;
        }
        return dia >= cuota.Vencimiento ? prima : 0m;
    }
}

/// <summary>
/// Las ventanas de causación de un crédito (US4, T0487): en cada corte se le dice qué se quiere causar desde el día siguiente
/// (<see cref="Fijar"/>); cierra lo que cambió o ya no va y abre lo nuevo. <see cref="Exacto"/> da el acumulado exacto de una clave a
/// cualquier fecha con la historia de sus ventanas.
/// </summary>
public sealed class RegistroDeVentanas
{
    private readonly Dictionary<ClaveDeCausacion, VentanaDeCausacion> _abiertas = [];
    private readonly Dictionary<ClaveDeCausacion, List<VentanaDeCausacion>> _porClave = [];
    private readonly List<VentanaDeCausacion> _todas = [];

    public RegistroDeVentanas(DayCountConvention convencion, RateEquivalence equivalencia)
    {
        Convencion = convencion;
        Equivalencia = equivalencia;
    }

    public DayCountConvention Convencion { get; }

    public RateEquivalence Equivalencia { get; }

    /// <summary>Todas las ventanas, en el orden en que se abrieron.</summary>
    public IReadOnlyList<VentanaDeCausacion> Ventanas => _todas;

    /// <summary>Las claves que alguna vez causaron.</summary>
    public IEnumerable<ClaveDeCausacion> Claves => _porClave.Keys;

    /// <summary>
    /// Desde el día siguiente a <paramref name="corte"/> se causa lo de <paramref name="deseadas"/>: se cierra en el corte toda ventana
    /// abierta cuya clave no esté o cuyos parámetros cambiaron, y se abre en el corte la de cada clave que quedó sin ventana.
    /// </summary>
    public void Fijar(DateOnly corte, IReadOnlyDictionary<ClaveDeCausacion, VentanaDeseada> deseadas)
    {
        ArgumentNullException.ThrowIfNull(deseadas);
        List<ClaveDeCausacion>? cerrar = null;
        foreach (var (clave, ventana) in _abiertas)
            if (!deseadas.TryGetValue(clave, out var deseada) || deseada.Parametros != ventana.Parametros)
                (cerrar ??= []).Add(clave);
        if (cerrar is not null)
            foreach (var clave in cerrar)
            {
                _abiertas[clave].Hasta = corte;
                _abiertas.Remove(clave);
            }
        foreach (var (clave, deseada) in deseadas)
        {
            if (_abiertas.ContainsKey(clave)) continue;
            var ventana = new VentanaDeCausacion(clave, corte, deseada);
            _abiertas[clave] = ventana;
            _todas.Add(ventana);
            if (!_porClave.TryGetValue(clave, out var lista)) _porClave[clave] = lista = [];
            lista.Add(ventana);
        }
    }

    /// <summary>El acumulado exacto de <paramref name="clave"/> al día <paramref name="dia"/> (incluido).</summary>
    public decimal Exacto(ClaveDeCausacion clave, DateOnly dia)
    {
        if (!_porClave.TryGetValue(clave, out var ventanas)) return 0m;
        var total = 0m;
        foreach (var v in ventanas)
        {
            if (v.Desde >= dia) continue;
            var hasta = v.Hasta is { } h && h < dia ? h : dia;
            total += Causacion.Acumulado(v.Parametros.Base, v.Parametros.TasaDiaria, ContadorDeDias.Dias(Convencion, v.Desde, hasta), Equivalencia);
        }
        return total;
    }
}
