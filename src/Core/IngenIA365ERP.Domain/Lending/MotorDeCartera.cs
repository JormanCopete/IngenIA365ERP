using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Delinquency;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// La fachada del motor financiero de Cartera (feature 013, M1, §2.8; molde <c>MotorDeCosteo</c>): la única puerta por la que la
/// aplicación llama al cálculo. Pura, determinista y sin IO —sólo <c>decimal</c> y <c>DateOnly</c>, sin reloj, sin valores legales
/// (la usura, los índices y los festivos llegan por parámetro)—. Cada pieza vive en su carpeta y se puede probar sola; aquí sólo se
/// juntan.
///
/// <para>
/// En US1 expone días (<see cref="ContadorDeDias"/>), tasas (<see cref="EquivalenciaDeTasas"/>, <see cref="Rates.TasaDelDia"/>), usura
/// (<see cref="ControlDeUsura"/>) y plan (<see cref="GeneradorDePlan"/>); US4 la extiende con la reconstrucción y la causación sin
/// cambiar estas entradas (C105). Toda salida que se sella lleva <see cref="VersionDeReglas"/>. (nuevo)
/// </para>
///
/// <para>
/// US4 (T0489) suma la regeneración del plan, el plan migrado, la proyección con pagos a tiempo, la reconstrucción a (D, R)
/// (<see cref="Reconstruir"/>), causar un día (<see cref="Causar"/>), verificar el libro (<see cref="Verificar"/>) y la altura de mora.
/// </para>
/// </summary>
public static class MotorDeCartera
{
    /// <summary>La versión de reglas con que calcula este programa (<c>LND-AAAA.n</c>).</summary>
    public static string VersionDeReglas => VersionesDeReglas.Vigente;

    /// <summary>Días de <paramref name="a"/> (excluida) a <paramref name="b"/> (incluida) en la convención.</summary>
    public static int Dias(DayCountConvention convencion, DateOnly a, DateOnly b) => ContadorDeDias.Dias(convencion, a, b);

    /// <summary>La tasa diaria de una EA en la convención y equivalencia selladas (a 18 decimales).</summary>
    public static decimal TasaDiaria(decimal efectivaAnual, DayCountConvention convencion, RateEquivalence equivalencia, RepaymentPeriodicity periodicidad) =>
        EquivalenciaDeTasas.TasaDiaria(efectivaAnual, convencion, equivalencia, periodicidad);

    /// <summary>La tasa de n días a partir de la diaria.</summary>
    public static decimal TasaDelPeriodo(decimal tasaDiaria, int dias, RateEquivalence equivalencia) =>
        EquivalenciaDeTasas.TasaDelPeriodo(tasaDiaria, dias, equivalencia);

    /// <summary>La nominal del período equivalente (FR-020, informativa).</summary>
    public static decimal NominalDelPeriodo(decimal efectivaAnual, RepaymentPeriodicity periodicidad) =>
        EquivalenciaDeTasas.NominalDelPeriodo(efectivaAnual, periodicidad);

    /// <summary>La EA de una tasa variable con su margen en la expresión del índice, piso y techo (D10).</summary>
    public static decimal TasaVariable(IndiceDeReferencia indice, decimal margen, decimal? piso, decimal? techo) =>
        Rates.TasaDelDia.TasaVariable(indice, margen, piso, techo);

    /// <summary>La corriente y la mora del día, topadas por la usura.</summary>
    public static ResultadoDeTasaDelDia TasaDelDia(EntradaDeTasaDelDia entrada) => Rates.TasaDelDia.Calcular(entrada);

    /// <summary>El plan de pagos proyectado.</summary>
    public static PlanDePagos GenerarPlan(EntradaDePlan entrada) => GeneradorDePlan.Generar(entrada);

    /// <summary>El control de usura de un plan.</summary>
    public static ResultadoDeUsura ControlarUsura(PlanDePagos plan, UsuraCertificada? usura, IReadOnlyList<CargoDeUsura>? cargosAlDesembolso = null) =>
        ControlDeUsura.Evaluar(plan, usura, cargosAlDesembolso);

    /// <summary>Proyecta el plan y lo controla contra la usura en un paso (simular, crear o versionar una línea).</summary>
    public static (PlanDePagos Plan, ResultadoDeUsura Usura) SimularConUsura(EntradaDePlan entrada, UsuraCertificada? usura,
        IReadOnlyList<CargoDeUsura>? cargosAlDesembolso = null)
    {
        var plan = GeneradorDePlan.Generar(entrada);
        return (plan, ControlDeUsura.Evaluar(plan, usura, cargosAlDesembolso));
    }

    // ------------------------------------------------------------------------------------------------ US4 (T0489, C105)

    /// <summary>El plan regenerado por un tramo, un reajuste o un abono, desde la primera cuota que vence después (D12, FR-067).</summary>
    public static PlanDePagos RegenerarPlan(PlanDePagos anterior, EntradaDeRegeneracion entrada) => GeneradorDePlan.Regenerar(anterior, entrada);

    /// <summary>El plan de un crédito migrado con el capital por cuota conservado (D5).</summary>
    public static PlanDePagos PlanMigrado(EntradaDePlanMigrado entrada) => GeneradorDePlan.DesdeMigracion(entrada);

    /// <summary>Lo que el plan causa hasta una fecha si se paga a tiempo.</summary>
    public static IReadOnlyList<CausadoDeCuota> ProyectarCausacion(PlanDePagos plan, DateOnly fecha) => GeneradorDePlan.ProyectarCausacion(plan, fecha);

    /// <summary>
    /// La reconstrucción del crédito a la fecha D con la información hasta la R de la entrada (T24): la única fuente de saldos, cuotas,
    /// mora y suspensión; con <paramref name="libroDesde"/>, también la causación día a día del libro teórico.
    /// </summary>
    public static EstadoDelCredito Reconstruir(EntradaDeReconstruccion entrada, DateOnly fecha, DateOnly? libroDesde = null,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes = null) =>
        ReconstruccionDeCredito.Reconstruir(entrada, fecha, libroDesde, interpretes);

    /// <summary>Causar un crédito de d − 1 a d: exige la marca en la víspera (SC-020).</summary>
    public static ResultadoDeCausacion Causar(EntradaDeReconstruccion entrada, DateOnly ultimaCausada, DateOnly dia,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes = null) =>
        CausacionDeCredito.Causar(entrada, ultimaCausada, dia, interpretes);

    /// <summary>
    /// Verificar el libro de un crédito contra su reconstrucción hasta <paramref name="hasta"/> (T33, data-model §5.3): las diferencias
    /// por (día valor, concepto, cuota); vacía si el libro cuadra. Son las que <c>Conciliar</c> registra.
    /// </summary>
    public static IReadOnlyList<DiferenciaDeLibro> Verificar(EntradaDeReconstruccion entrada, DateOnly hasta, IEnumerable<MovimientoDeLibro> libro,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes = null)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var operaciones = ReconstruccionDeCredito.OperacionesVigentes(entrada, hasta);
        if (operaciones.Count == 0) return LibroTeorico.Vacio.Diferencias(libro, hasta);
        var estado = ReconstruccionDeCredito.Reconstruir(entrada, hasta, operaciones[0].FechaValor, interpretes);
        return estado.Libro.Diferencias(libro, hasta);
    }

    /// <summary>
    /// Como <see cref="Verificar"/>, con la corrección monetaria de un crédito en UVR aparte
    /// (<see cref="LibroTeorico.DiferenciasSeparandoLaCorreccionMonetaria"/>): la usa la corrida de corrección por valor nacional, que registra las
    /// diferencias de la corrección monetaria como <c>MonetaryCorrection</c> y las demás como ajuste de causación (K6b, fase 24). Con
    /// <paramref name="separarPagos"/> (la corrección de una UVR; decisión del dueño del 2026-10-17), el reparto de los pagos va aparte
    /// (<see cref="DiferenciaDeLibro.DePago"/>) y se registra como <c>PaymentReallocation</c> con su hecho.
    /// </summary>
    public static IReadOnlyList<DiferenciaDeLibro> VerificarSeparandoLaCorreccionMonetaria(EntradaDeReconstruccion entrada, DateOnly hasta,
        IEnumerable<MovimientoDeLibro> libro, IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes = null, bool separarPagos = false)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var operaciones = ReconstruccionDeCredito.OperacionesVigentes(entrada, hasta);
        if (operaciones.Count == 0) return LibroTeorico.Vacio.DiferenciasSeparandoLaCorreccionMonetaria(libro, hasta, separarPagos);
        var estado = ReconstruccionDeCredito.Reconstruir(entrada, hasta, operaciones[0].FechaValor, interpretes);
        return estado.Libro.DiferenciasSeparandoLaCorreccionMonetaria(libro, hasta, separarPagos);
    }

    // ------------------------------------------------------------------------------------------------ US5 (T0534–T0536)

    /// <summary>
    /// Reparte un pago sobre el estado a su fecha valor con la prelación sellada (FR-071, FR-072): la misma función para simular y para
    /// aplicar; la reconstrucción la usa con cada pago.
    /// </summary>
    /// <remarks>K6b (T1054): sobre un crédito en UVR (<see cref="EstadoDelCredito.Uvr"/>) el valor pasa a UVR a su fecha valor, se distribuye en UVR y
    /// vuelve a pesos con la misma función que usa la reconstrucción (<see cref="Housing.ConversionUvr.Distribuir"/>): simular = aplicar.</remarks>
    public static Payments.DistribucionDePago DistribuirPago(EstadoDelCredito estado, Payments.SolicitudDeDistribucion solicitud,
        Payments.Prelacion prelacion, RedondeoDeCartera redondeo) =>
        estado?.Uvr is { } uvr
            ? Housing.ConversionUvr.Distribuir(uvr, solicitud, prelacion)
            : Payments.AplicadorDePagos.Distribuir(estado!, solicitud, prelacion, redondeo);

    /// <summary>Las dos alternativas de un abono extraordinario sin elección (reducir plazo, reducir cuota), cada una con su plan (SC-003).</summary>
    public static IReadOnlyList<Payments.DistribucionDePago> AlternativasDeAbono(EstadoDelCredito estado, Payments.SolicitudDeDistribucion solicitud,
        Payments.Prelacion prelacion, RedondeoDeCartera redondeo) =>
        estado?.Uvr is { } uvr
            ? Housing.ConversionUvr.Alternativas(uvr, solicitud, prelacion)
            : Payments.AplicadorDePagos.Alternativas(estado!, solicitud, prelacion, redondeo);

    /// <summary>El anticipo aplicado a una cuota en su vencimiento (paso (5) del ciclo diario; D6).</summary>
    /// <remarks>K6b: sobre un crédito en UVR, en UVR y en pesos a la UVR del día.</remarks>
    public static Payments.AplicacionDeAnticipo? AplicarAnticipo(EstadoDelCredito estado, int cuota, Payments.Prelacion prelacion) =>
        estado?.Uvr is { } uvr
            ? Payments.AplicadorDePagos.AplicarAnticipo(uvr.Estado, cuota, prelacion) is { } a
                ? Housing.ConversionUvr.AnticipoEnPesos(a, uvr.Serie.Del(a.Fecha).Valor, uvr.RedondeoDePesos.Modo)
                : null
            : Payments.AplicadorDePagos.AplicarAnticipo(estado!, cuota, prelacion);

    /// <summary>El <c>SimulationId</c>: la huella canónica de la simulación (T32).</summary>
    public static string HuellaDeSimulacion(Guid credito, string rulesVersion, Payments.DistribucionDePago distribucion) =>
        HuellaDeCartera.DeSimulacion(credito, rulesVersion, distribucion);

    // ------------------------------------------------------------------------------------------------ K3 (T0874)

    /// <summary>El plan de una novedad desde la fecha de la decisión (prórroga, cambio de fecha o de tasa, novación, refinanciación).</summary>
    public static PlanDePagos RegenerarPlanPorNovedad(EntradaDeNovedadDePlan entrada) => GeneradorDePlan.RegenerarPorNovedad(entrada);

    /// <summary>
    /// La simulación del plan de una novedad (FR-091): el plan nuevo controlado contra la usura vigente a la fecha de la decisión
    /// (<c>Lending.Usury.Exceeded</c> si la pasa).
    /// </summary>
    public static (PlanDePagos Plan, ResultadoDeUsura Usura) SimularNovedad(EntradaDeNovedadDePlan entrada, UsuraCertificada? usura)
    {
        var plan = GeneradorDePlan.RegenerarPorNovedad(entrada);
        return (plan, ControlDeUsura.Evaluar(plan, usura));
    }

    /// <summary>La versión completa del plan de un crédito que conserva su número tras la novedad.</summary>
    public static PlanDePagos UnirPlanConAnterior(PlanDePagos anterior, PlanDePagos novedad) => GeneradorDePlan.UnirConAnterior(anterior, novedad);

    /// <summary>Los días de mora con tolerancia y fecha exigible (T30).</summary>
    public static ResultadoDeAlturaDeMora AlturaDeMora(DateOnly dia, IEnumerable<CuotaParaMora> cuotas, decimal tolerancia, int? umbralDePagaduria = null) =>
        Delinquency.AlturaDeMora.Calcular(dia, cuotas, tolerancia, umbralDePagaduria);
}
