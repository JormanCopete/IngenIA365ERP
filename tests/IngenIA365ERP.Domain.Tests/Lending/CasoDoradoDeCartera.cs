using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Un caso dorado del motor financiero de Cartera (feature 013, T0330; quickstart §1.2, R45): un archivo JSON en <c>Casos/</c> con
/// comentarios, claves en español y enums por nombre (molde <c>CasoDoradoDeCosteo</c> y los casos de la fase 4), calculado con la
/// hoja <c>Casos/calcular.py</c> —decimal de 40 dígitos, nunca Excel ni el código que se prueba (T26)— para que la contadora lo pueda
/// leer y firmar.
///
/// <para>
/// El esquema es el completo de quickstart §1.2 y no cambia en US4 (C105: el cargador nace aquí y US4 no lo recrea):
/// <list type="bullet">
/// <item><c>condiciones</c>: lo sellado de la línea y el crédito (sistema, convención, equivalencia, redondeo, prelación, monto,
/// <c>plazoCuotas</c> = períodos del plazo —con gracia total incluye los de gracia—, periodicidad, tasa EA, fechas, día de pago,
/// modalidad, extraordinarias, gracia y seguros);</item>
/// <item><c>series</c>: usura por clave de modalidad con su vigencia y registro (R), festivos nacionales e índices;</item>
/// <item><c>operaciones</c> con fecha valor y de registro, y <c>consultas</c> a (D, R): las corre US4;</item>
/// <item><c>esperado</c> de esta fase (casos sin operaciones): tasas, plan cuota a cuota y veredicto de usura; con
/// <c>variantes</c>, lo de cada una en <c>esperado.variantes[nombre]</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// US4 (T0464) amplía el mismo esquema sin romperlo: las condiciones de causación (mora, sobretasa, tasa variable, causación de
/// seguros, reestructuraciones, insolvencia, castigo), las series de umbral de suspensión y tolerancia, la usura con «hasta» y
/// «corrige», operaciones con fecha valor y <c>registradoEn</c> (desembolso o tramo, saldo inicial migrado, reajuste con el índice
/// publicado), variantes con sus propias operaciones y consultas, y <c>consultas</c> a (D, R) con su <c>modo</c>: la reconstrucción
/// (saldos por cuota y concepto, mora, suspensión, tasas, libro desde <c>libroDesde</c>, plan con <c>conPlan</c>), «PagosATiempo» (la
/// causación que proyecta el plan), «Causacion» (causar día a día desde la <c>marca</c>) y «Diferencias» (lo que cambia el libro entre
/// <c>comparaConR</c> y R).
/// </para>
/// </summary>
public sealed class CasoDoradoDeCartera
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
    public CondicionesJson Condiciones { get; set; } = new();
    public List<VarianteJson> Variantes { get; set; } = [];
    public SeriesJson Series { get; set; } = new();
    public List<OperacionJson> Operaciones { get; set; } = [];
    public List<ConsultaJson> Consultas { get; set; } = [];
    public EsperadoJson? Esperado { get; set; }

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeCartera Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeCartera>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>El plan principal y cada variante, con lo que esperan (nada si el caso no trae «esperado» de plan).</summary>
    public IEnumerable<(string Nombre, CondicionesJson Condiciones, EsperadoJson Esperado)> Escenarios()
    {
        if (Esperado is not { } esperado) yield break;
        yield return (Nombre, Condiciones, esperado);
        foreach (var v in Variantes)
        {
            if (esperado.Variantes is null || !esperado.Variantes.TryGetValue(v.Nombre, out var delaVariante))
                throw new InvalidOperationException($"{Nombre}: la variante «{v.Nombre}» no trae lo esperado.");
            yield return ($"{Nombre} · {v.Nombre}", v.Condiciones, delaVariante);
        }
    }

    /// <summary>
    /// La usura que el caso certifica para la modalidad a la fecha de desembolso: la de la clave con el «desde» más reciente que no
    /// pase de esa fecha (y, si hay empate, la registrada después). Nula si no hay.
    /// </summary>
    public UsuraCertificada? UsuraDe(CondicionesJson c) =>
        c.Modalidad is not { } clave
            ? null
            : Series.Usura
                .Where(u => u.Clave == clave && u.Desde <= c.FechaDesembolso)
                .OrderByDescending(u => u.Desde).ThenByDescending(u => u.RegistradoEn)
                .Select(u => new UsuraCertificada(u.Ea, u.PublicId ?? Guid.Empty, u.Clave, u.Desde))
                .FirstOrDefault();

    /// <summary>US5: las operaciones con «esperado» del caso y de cada variante, con las condiciones y la lista con que se corren.</summary>
    public IEnumerable<(string Nombre, CondicionesJson Condiciones, List<OperacionJson> Operaciones, OperacionJson Operacion)> OperacionesConEsperado()
    {
        foreach (var o in Operaciones.Where(o => o.Esperado is not null)) yield return (Nombre, Condiciones, Operaciones, o);
        foreach (var v in Variantes)
            foreach (var o in (v.Operaciones ?? []).Where(o => o.Esperado is not null))
                yield return ($"{Nombre} · {v.Nombre}", v.Condiciones, v.Operaciones!, o);
    }

    /// <summary>Las consultas del caso y de cada variante, con las condiciones y operaciones con que se corren.</summary>
    public IEnumerable<(string Nombre, CondicionesJson Condiciones, List<OperacionJson> Operaciones, ConsultaJson Consulta)> TodasLasConsultas()
    {
        foreach (var c in Consultas) yield return (Nombre, Condiciones, Operaciones, c);
        foreach (var v in Variantes)
            foreach (var c in v.Consultas ?? [])
                yield return ($"{Nombre} · {v.Nombre}", v.Condiciones, v.Operaciones ?? Operaciones, c);
    }

    /// <summary>
    /// La entrada de la reconstrucción con <paramref name="r"/> como R: las condiciones selladas con la versión vigente, el plan pactado
    /// de las condiciones, las operaciones (el reajuste con el último índice publicado a su fecha y conocido al registrarlo, FR-067) y las
    /// series de la modalidad. La usura lleva su <c>Sequence</c> por el orden del archivo, como en la hoja.
    /// </summary>
    public EntradaDeReconstruccion Entrada(CondicionesJson c, IReadOnlyList<OperacionJson> operaciones, DateTime r) =>
        new(c.Credito(VersionesDeReglas.Vigente), c.Entrada(), operaciones.Select((o, i) => Operacion(o, i)).ToList(), r)
        {
            Usura = Series.Usura.Select((u, i) => (u, i)).Where(x => x.u.Clave == c.Modalidad)
                .Select(x => new ValorDeUsura(x.u.Desde, x.u.Ea, x.u.RegistradoEn, x.i + 1, x.u.PublicId ?? Guid.Empty)
                {
                    ValidTo = x.u.Hasta,
                    SupersedesPublicId = x.u.Corrige,
                }).ToList(),
            Festivos = Series.Festivos,
            UmbralesDeSuspension = Series.UmbralesDeSuspension.Select(u => new ValorConVigencia<int>(u.Desde, u.Dias)).ToList(),
            Tolerancias = Series.Tolerancias.Select(t => new ValorConVigencia<decimal>(t.Desde, t.Valor)).ToList(),
            PendientesDeGiro = Series.PendientesDeGiro
                .Select(g => new CuotaPendienteDeGiro(g.Cuota, g.FechaDelDescuento, g.VencimientoDelGiro, g.Monto, g.RegistradoEn) { UmbralDias = g.Umbral })
                .ToList(),
        };

    private EntradaDeOperacion Operacion(OperacionJson o, int indice) =>
        new(o.Id, Enum.Parse<LoanOperationKind>(o.Tipo), o.FechaValor, o.RegistradoEn, o.Secuencia ?? indice + 1)
        {
            Monto = o.Monto,
            Saldos = o.Saldos.Select(s => new SaldoInicialDeCredito(s.Concepto, s.Cuota, s.Valor)).ToList(),
            CuotasMigradas = o.Cuotas.Select(q => new CuotaMigrada(q.Numero, q.Vencimiento, q.Capital) { Desde = q.Desde }).ToList(),
            Indice = o.Indice is { } codigo ? IndicePara(codigo, o.FechaValor, o.RegistradoEn) : null,
            Reversa = o.Reversa,
            Contenido = o.Contenido(),
        };

    private IndiceDeReferencia IndicePara(string codigo, DateOnly fecha, DateTime registrado) =>
        Series.Indices.Where(x => x.Codigo == codigo && x.Desde <= fecha && x.RegistradoEn <= registrado)
            .OrderByDescending(x => x.Desde).ThenByDescending(x => x.RegistradoEn)
            .Select(x => new IndiceDeReferencia(x.Valor, x.PeriodosNominales))
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{Nombre}: no hay índice {codigo} publicado al {fecha:yyyy-MM-dd}.");

    public sealed class CondicionesJson
    {
        public AmortizationSystem Sistema { get; set; }
        public DayCountConvention Convencion { get; set; }
        public RateEquivalence Equivalencia { get; set; } = RateEquivalence.Compound;
        public RedondeoJson Redondeo { get; set; } = new();
        public AllocationOrder Prelacion { get; set; } = AllocationOrder.OldestInstallmentFirst;
        public decimal Monto { get; set; }
        public int PlazoCuotas { get; set; }
        public RepaymentPeriodicity Periodicidad { get; set; } = RepaymentPeriodicity.Monthly;
        public decimal TasaEA { get; set; }
        public DateOnly FechaDesembolso { get; set; }
        public DateOnly? PrimerVencimiento { get; set; }
        public int? DiaDePago { get; set; }
        public string? Modalidad { get; set; }
        public List<ExtraordinariaJson> Extraordinarias { get; set; } = [];
        public GraciaJson? Gracia { get; set; }
        public List<SeguroJson> Seguros { get; set; } = [];
        // US4: condiciones de causación.
        public DefaultRateKind ClaseDeMora { get; set; } = DefaultRateKind.RelativeToCurrent;
        public decimal? FactorDeMora { get; set; }
        public decimal? MoraFijaEA { get; set; }
        public decimal SobretasaDeCargos { get; set; }
        public bool MoraSobreIntereses { get; set; }
        public InsuranceAccrualTiming CausacionDeSeguros { get; set; } = InsuranceAccrualTiming.AtDueDate;
        public int Reestructuraciones { get; set; }
        public DateOnly? InsolvenciaDesde { get; set; }
        public DateOnly? CastigoDesde { get; set; }
        public bool UsuraRequerida { get; set; } = true;
        public TasaVariableJson? TasaVariable { get; set; }
        // US5: la prelación sellada (data-model §1.8); sin lista, la de defecto.
        public List<LoanConcept>? ConceptosDePrelacion { get; set; }
        public bool SuspensoConSuConcepto { get; set; } = true;
        public bool CuotaEspecificaFueraDeOrden { get; set; }
        public bool GastosInformados { get; set; }

        public Prelacion PrelacionSellada() =>
            new(Prelacion, ConceptosDePrelacion ?? [], SuspensoConSuConcepto, CuotaEspecificaFueraDeOrden, GastosInformados);

        /// <summary>Las condiciones selladas del crédito (US4).</summary>
        public CondicionesDeCredito Credito(string rulesVersion) => new(rulesVersion, TasaEA, ClaseDeMora, Convencion, Equivalencia, Periodicidad,
            new RedondeoDeCartera(Redondeo.Unidad, Redondeo.Modo, Redondeo.Momento))
        {
            FactorDeMora = FactorDeMora,
            MoraFijaEA = MoraFijaEA,
            SobretasaDeCargos = SobretasaDeCargos,
            MoraSobreIntereses = MoraSobreIntereses,
            CausacionDeSeguros = CausacionDeSeguros,
            Reestructuraciones = Reestructuraciones,
            InsolvenciaDesde = InsolvenciaDesde,
            CastigoDesde = CastigoDesde,
            UsuraRequerida = UsuraRequerida,
            Margen = TasaVariable?.Margen ?? 0m,
            Piso = TasaVariable?.Piso,
            Techo = TasaVariable?.Techo,
            Prelacion = PrelacionSellada(),
        };

        public EntradaDePlan Entrada() => new(Monto, FechaDesembolso, PlazoCuotas, Periodicidad, Sistema, TasaEA, Convencion, Equivalencia,
            new RedondeoDeCartera(Redondeo.Unidad, Redondeo.Modo, Redondeo.Momento))
        {
            PrimerVencimiento = PrimerVencimiento,
            DiaDePago = DiaDePago,
            Extraordinarias = Extraordinarias.Select(x => new CuotaExtraordinaria(x.Cuota, x.Valor)).ToList(),
            Gracia = Gracia?.Clase ?? GraceKind.None,
            PeriodosDeGracia = Gracia?.Periodos ?? 0,
            TratamientoDeGracia = Gracia?.Tratamiento,
            CuotasDeDistribucion = Gracia?.CuotasDeDistribucion,
            Seguros = Seguros.Select(s => new SeguroDelPlan(s.Codigo, s.Base, s.Tarifa, s.Periodicidad, s.Computa)).ToList(),
        };
    }

    public sealed class VarianteJson
    {
        public string Nombre { get; set; } = string.Empty;
        public CondicionesJson Condiciones { get; set; } = new();
        /// <summary>US4: las operaciones de la variante (sin ellas, las del caso) y sus consultas.</summary>
        public List<OperacionJson>? Operaciones { get; set; }
        public List<ConsultaJson>? Consultas { get; set; }
    }

    public sealed class TasaVariableJson
    {
        public decimal Margen { get; set; }
        public decimal? Piso { get; set; }
        public decimal? Techo { get; set; }
    }

    public sealed class RedondeoJson
    {
        public decimal Unidad { get; set; } = RedondeoDeCartera.Centavo;
        public RoundingMode Modo { get; set; } = RoundingMode.AwayFromZero;
        public RoundingMoment Momento { get; set; } = RoundingMoment.PerConcept;
    }

    public sealed class ExtraordinariaJson
    {
        public int Cuota { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class GraciaJson
    {
        public GraceKind Clase { get; set; }
        public int Periodos { get; set; }
        public GraceInterestTreatment? Tratamiento { get; set; }
        public int? CuotasDeDistribucion { get; set; }
    }

    public sealed class SeguroJson
    {
        public string Codigo { get; set; } = string.Empty;
        public InsuranceBase Base { get; set; }
        public decimal Tarifa { get; set; }
        public RepaymentPeriodicity Periodicidad { get; set; } = RepaymentPeriodicity.Monthly;
        public bool Computa { get; set; }
    }

    public sealed class SeriesJson
    {
        public List<UsuraJson> Usura { get; set; } = [];
        public List<DateOnly> Festivos { get; set; } = [];
        public List<IndiceJson> Indices { get; set; } = [];
        public List<UmbralJson> UmbralesDeSuspension { get; set; } = [];
        public List<ToleranciaJson> Tolerancias { get; set; } = [];
        /// <summary>K2 (T0804): lo descontado por la pagaduría y pendiente de giro, con su ventana y su umbral sellados.</summary>
        public List<PendienteDeGiroJson> PendientesDeGiro { get; set; } = [];
    }

    public sealed class PendienteDeGiroJson
    {
        public int Cuota { get; set; }
        public DateOnly FechaDelDescuento { get; set; }
        public DateOnly VencimientoDelGiro { get; set; }
        public decimal Monto { get; set; }
        public int? Umbral { get; set; }
        public DateTime RegistradoEn { get; set; }
    }

    public sealed class UmbralJson
    {
        public DateOnly Desde { get; set; }
        public int Dias { get; set; }
    }

    public sealed class ToleranciaJson
    {
        public DateOnly Desde { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class UsuraJson
    {
        public string Clave { get; set; } = string.Empty;
        public DateOnly Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public decimal Ea { get; set; }
        public DateTime RegistradoEn { get; set; }
        public Guid? PublicId { get; set; }
        /// <summary>El valor que esta fila corrige (T28): lo usa US4.</summary>
        public Guid? Corrige { get; set; }
    }

    public sealed class IndiceJson
    {
        public string Codigo { get; set; } = string.Empty;
        public string? Clave { get; set; }
        public DateOnly Desde { get; set; }
        public decimal Valor { get; set; }
        public int? PeriodosNominales { get; set; }
        public DateTime RegistradoEn { get; set; }
    }

    /// <summary>Una operación con fecha valor y de registro (US4/US5).</summary>
    public sealed class OperacionJson
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public PaymentModality? Modalidad { get; set; }
        public decimal? Valor { get; set; }
        public DateOnly FechaValor { get; set; }
        public DateTime RegistradoEn { get; set; }
        /// <summary>US5: la distribución de un pago con la información hasta su registro (simular = aplicar).</summary>
        public EsperadoDePagoJson? Esperado { get; set; }
        // US5
        public PrepaymentChoice? Eleccion { get; set; }
        public int? Cuota { get; set; }
        public LoanConcept? Concepto { get; set; }
        public bool ComoAbonoACapital { get; set; }
        public TopeLegalJson? TopeLegal { get; set; }

        /// <summary>El contenido que lee el intérprete de la reconstrucción.</summary>
        public object? Contenido() => Tipo switch
        {
            nameof(LoanOperationKind.Payment) or nameof(LoanOperationKind.ProvisionalCollection) when Valor is { } v =>
                new ContenidoDePago(Modalidad ?? PaymentModality.FreeAmount, v) { Eleccion = Eleccion, Cuota = Cuota },
            nameof(LoanOperationKind.CreditBalanceResolution) =>
                new ContenidoDeResolucion(Concepto ?? LoanConcept.CreditBalance, Valor ?? 0m) { ComoAbonoACapital = ComoAbonoACapital, Eleccion = Eleccion },
            // US6 (T0565): el ajuste de Comercial con su clase, su valor con signo y el recálculo del plan.
            nameof(LoanOperationKind.CommercialAdjustment) => new ContenidoDeAjusteComercial(
                ContenidoDeAjusteComercial.DeTexto(Clase) ?? throw new InvalidOperationException($"Clase de ajuste desconocida: {Clase}."), Valor ?? 0m)
            {
                Recalculo = Recalculo ?? PrepaymentChoice.ReduceInstallment,
            },
            _ => null,
        };

        // US6 (T0565): la clase del ajuste de Comercial (CreditNote, Return, DebitNote, Voiding, VoidingByDianRejection) y el recálculo.
        public string? Clase { get; set; }
        public PrepaymentChoice? Recalculo { get; set; }

        /// <summary>La solicitud del pago.</summary>
        public SolicitudDeDistribucion Solicitud() =>
            new(Modalidad ?? PaymentModality.FreeAmount, Valor ?? 0m, FechaValor) { Eleccion = Eleccion, Cuota = Cuota };
        // US4
        public long? Secuencia { get; set; }
        public decimal? Monto { get; set; }
        public string? Indice { get; set; }
        public string? Reversa { get; set; }
        public List<SaldoInicialJson> Saldos { get; set; } = [];
        public List<CuotaMigradaJson> Cuotas { get; set; } = [];
    }

    public sealed class SaldoInicialJson
    {
        public LoanConcept Concepto { get; set; }
        public int? Cuota { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class CuotaMigradaJson
    {
        public int Numero { get; set; }
        public DateOnly Vencimiento { get; set; }
        public DateOnly? Desde { get; set; }
        public decimal Capital { get; set; }
    }

    /// <summary>Una consulta a la fecha D con la información hasta R (US4).</summary>
    public sealed class ConsultaJson
    {
        /// <summary><c>Reconstruccion</c> (defecto), <c>PagosATiempo</c>, <c>Causacion</c> o <c>Diferencias</c>.</summary>
        public string Modo { get; set; } = "Reconstruccion";
        public DateOnly Fecha { get; set; }
        public DateTime InformacionHasta { get; set; }
        public DateOnly? LibroDesde { get; set; }
        public bool ConPlan { get; set; }
        public DateOnly? Marca { get; set; }
        public DateTime? ComparaConR { get; set; }
        /// <summary>US5: la consulta trae la distribución de cada pago a (D, R).</summary>
        public bool ConPagos { get; set; }
        public EsperadoDeConsultaJson? Esperado { get; set; }
    }

    /// <summary>Lo que espera una consulta; cada modo trae lo suyo (la reconstrucción, todos sus campos).</summary>
    public sealed class EsperadoDeConsultaJson
    {
        public Dictionary<string, decimal>? Saldos { get; set; }
        public List<SaldoDeCuotaJson>? PorCuota { get; set; }
        public decimal? CapitalVencido { get; set; }
        public int? DiasDeMora { get; set; }
        public int? CuotaMasAntigua { get; set; }
        public int? CuotasVencidas { get; set; }
        public bool? Suspendida { get; set; }
        public decimal? ValorParaCancelar { get; set; }
        public decimal? TasaDiariaCorriente { get; set; }
        public decimal? TasaDiariaMora { get; set; }
        public bool? UsuraTopada { get; set; }
        public DateOnly? ValidaHasta { get; set; }
        public List<TransicionJson>? Transiciones { get; set; }
        public List<FilaDeLibroJson>? Libro { get; set; }
        public PlanDeConsultaJson? Plan { get; set; }
        public List<CausadoJson>? Causado { get; set; }
        public List<FilaDeLibroJson>? Causacion { get; set; }
        public List<FilaDeLibroJson>? Diferencias { get; set; }
        public List<PagoEsperadoJson>? Pagos { get; set; }
    }

    /// <summary>US5: lo que se espera de la distribución de un pago (quickstart §1.2).</summary>
    public class EsperadoDePagoJson
    {
        public List<LineaJson> Distribucion { get; set; } = [];
        public List<LineaJson> AnticipoUsado { get; set; } = [];
        public decimal Anticipo { get; set; }
        public int? CuotaDelAnticipo { get; set; }
        public decimal SaldoAFavor { get; set; }
        public decimal Excedente { get; set; }
        public decimal Aplicado { get; set; }
        public bool Cancela { get; set; }
        public List<string> Avisos { get; set; } = [];
        public PlanDeConsultaJson? PlanNuevo { get; set; }
        public TopeEsperadoJson? TopeLegal { get; set; }
    }

    /// <summary>La distribución de un pago a (D, R), con el Id de su operación.</summary>
    public sealed class PagoEsperadoJson : EsperadoDePagoJson
    {
        public string Id { get; set; } = string.Empty;
    }

    public sealed class LineaJson
    {
        public int? Cuota { get; set; }
        public LoanConcept Concepto { get; set; }
        public decimal Valor { get; set; }
    }

    /// <summary>El tope legal del prepago sin penalidad (caso 32): el SMMLV del desembolso y las veces, de la ley y de la cooperativa.</summary>
    public sealed class TopeLegalJson
    {
        public decimal Smmlv { get; set; }
        public decimal VecesNacional { get; set; }
        public decimal? VecesCooperativa { get; set; }
    }

    public sealed class TopeEsperadoJson
    {
        public bool DentroDelTope { get; set; }
        public decimal TopeEnPesos { get; set; }
        public decimal Veces { get; set; }
    }

    public sealed class SaldoDeCuotaJson
    {
        public int? Cuota { get; set; }
        public LoanConcept Concepto { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class TransicionJson
    {
        public DateOnly Fecha { get; set; }
        public bool Suspendida { get; set; }
    }

    public sealed class FilaDeLibroJson
    {
        public DateOnly Dia { get; set; }
        public LoanTransactionKind? Clase { get; set; }
        public LoanConcept Concepto { get; set; }
        public int? Cuota { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class PlanDeConsultaJson
    {
        public List<CuotaJson> Cuotas { get; set; } = [];
        public TotalesJson Totales { get; set; } = new();
    }

    public sealed class CausadoJson
    {
        public int Cuota { get; set; }
        public LoanConcept Concepto { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class EsperadoJson
    {
        public TasasJson Tasas { get; set; } = new();
        public PlanJson Plan { get; set; } = new();
        public UsuraEsperadaJson Usura { get; set; } = new();
        public Dictionary<string, EsperadoJson>? Variantes { get; set; }
    }

    public sealed class TasasJson
    {
        public decimal Diaria { get; set; }
        public decimal NominalDelPeriodo { get; set; }
        public decimal? CuotaNivelada { get; set; }
        /// <summary>Efectiva con cargos, a 6 decimales.</summary>
        public decimal Efectiva { get; set; }
        public decimal EfectivaSinCargos { get; set; }
    }

    public sealed class PlanJson
    {
        public decimal InteresDeGracia { get; set; }
        public decimal Residuo { get; set; }
        public TotalesJson Totales { get; set; } = new();
        public List<CuotaJson> Cuotas { get; set; } = [];
    }

    public sealed class TotalesJson
    {
        public decimal Capital { get; set; }
        public decimal Interes { get; set; }
        public decimal Seguro { get; set; }
    }

    public sealed class CuotaJson
    {
        public int Numero { get; set; }
        public DateOnly Vencimiento { get; set; }
        public int Dias { get; set; }
        public decimal Capital { get; set; }
        public decimal Interes { get; set; }
        public decimal Seguro { get; set; }
        public decimal Saldo { get; set; }
    }

    public sealed class UsuraEsperadaJson
    {
        public VeredictoDeUsura Veredicto { get; set; }
        public decimal? Usura { get; set; }
        /// <summary>Usura − efectiva con cargos, a 6 decimales.</summary>
        public decimal? Holgura { get; set; }
    }
}
