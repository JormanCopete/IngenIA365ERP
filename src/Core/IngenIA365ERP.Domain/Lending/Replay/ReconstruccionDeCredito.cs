using System.Collections.Concurrent;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Delinquency;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>
/// Cómo interpreta la reconstrucción una clase de operación (US4, T0488): el punto de extensión por <see cref="LoanOperationKind"/>.
/// US4 trae <c>Disbursement</c>, <c>OpeningBalance</c> y <c>RateReset</c> (<c>DisbursementReversal</c> se resuelve antes: la pareja
/// reversada no entra); US5 suma <c>Payment</c>, <c>PaymentReversal</c> y <c>CreditBalanceResolution</c> con <c>AplicadorDePagos</c>, y
/// US6 <c>CommercialAdjustment</c>.
/// </summary>
public interface IInterpreteDeOperacion
{
    /// <summary>Aplica la operación al crédito, que ya está al día hasta su fecha valor.</summary>
    void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion);
}

/// <summary>
/// El crédito mientras se reconstruye, tal como lo ve un intérprete de operaciones: su plan, la corriente vigente, los saldos que no
/// salen de la causación (los iniciales y, desde US5, los pagos) y las filas de los hechos.
/// </summary>
public sealed class ContextoDeReconstruccion
{
    private readonly List<MovimientoDeLibro> _filas = [];
    private readonly Dictionary<(LoanConcept, int?), decimal> _saldos = [];

    internal ContextoDeReconstruccion(EntradaDeReconstruccion entrada, DateOnly origen)
    {
        Entrada = entrada;
        Origen = origen;
        TasaCorrienteEA = entrada.Condiciones.TasaEfectivaAnual;
    }

    public EntradaDeReconstruccion Entrada { get; }

    public CondicionesDeCredito Condiciones => Entrada.Condiciones;

    /// <summary>La fecha valor del primer desembolso o el corte.</summary>
    public DateOnly Origen { get; }

    public PlanDePagos? Plan { get; private set; }

    /// <summary>La corriente pactada o reajustada (EA), antes de topar.</summary>
    public decimal TasaCorrienteEA { get; private set; }

    /// <summary>Cambió algo que mueve los cortes o los saldos vencidos: la corrida recalcula lo suyo.</summary>
    internal int Version { get; private set; }

    internal IReadOnlyList<MovimientoDeLibro> Filas => _filas;

    internal IReadOnlyDictionary<(LoanConcept, int?), decimal> SaldosFueraDeCausacion => _saldos;

    public void CambiarPlan(PlanDePagos plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Version++;
    }

    public void CambiarTasa(decimal efectivaAnual)
    {
        TasaCorrienteEA = efectivaAnual;
        Version++;
    }

    /// <summary>Un hecho del libro (desembolso, saldo inicial; desde US5, pagos y reversos).</summary>
    public void AgregarFila(MovimientoDeLibro fila) => _filas.Add(fila ?? throw new ArgumentNullException(nameof(fila)));

    /// <summary>
    /// Mueve un saldo que no sale de la causación: un saldo inicial por concepto o, desde US5, lo que aplica un pago. En
    /// <see cref="LoanConcept.Principal"/> por cuota mueve el capital pendiente de esa cuota.
    /// </summary>
    public void SumarSaldo(LoanConcept concepto, int? cuota, decimal monto)
    {
        _saldos[(concepto, cuota)] = _saldos.GetValueOrDefault((concepto, cuota)) + monto;
        Version++;
    }

    /// <summary>El saldo fuera de la causación de un concepto en una cuota.</summary>
    public decimal SaldoFueraDeCausacion(LoanConcept concepto, int? cuota) => _saldos.GetValueOrDefault((concepto, cuota));

    /// <summary>El capital pendiente de una cuota: el del plan más lo movido a mano (pagos de US5).</summary>
    public decimal CapitalPendiente(CuotaDelPlan cuota) => cuota.Capital + SaldoFueraDeCausacion(LoanConcept.Principal, cuota.Numero);
}

/// <summary>
/// La <b>reconstrucción</b> de un crédito a la fecha D con la información hasta R (feature 013, US4, T0488; T24, T25, T30, R32, M3;
/// FR-007, FR-011, FR-062–FR-070): la única fuente de saldos, cuotas, mora y suspensión.
/// <list type="number">
/// <item>Entran las operaciones registradas hasta R, sin las reversadas por un reverso registrado hasta R (ni el reverso), con fecha
/// valor hasta D, en orden (fecha valor, <c>Sequence</c>); la usura se lee bitemporal a R (<see cref="VigenciaBitemporal"/>).</item>
/// <item>Parte del plan pactado (primer desembolso) o migrado (<c>OpeningBalance</c>); cada tramo y cada reajuste lo regeneran
/// (<see cref="GeneradorDePlan.Regenerar"/>).</item>
/// <item>Avanza <b>por segmentos entre eventos</b> —vencimientos, fechas exigibles, operaciones, cambios de usura, umbral y tolerancia,
/// cruces del umbral, fin de cada período de gracia, insolvencia, castigo— y en cada corte fija las ventanas de causación
/// (<see cref="RegistroDeVentanas"/>); el costo es por evento, no por día. Un hecho con fecha valor v rige desde el día v + 1.</item>
/// <item>Devuelve <see cref="EstadoDelCredito"/>: los saldos son el redondeo de los acumulados a D; el libro teórico, día a día desde
/// <c>libroDesde</c> (U(A(d)) − U(A(d−1))) más los hechos de las operaciones.</item>
/// </list>
/// Una D futura se proyecta sin pagos con la última usura conocida y dice hasta cuándo vale (D11). Las reglas son las de la
/// <c>RulesVersion</c> sellada (<see cref="VersionesDeReglas.Para"/>). Pura: sin IO ni reloj. (nuevo)
/// </summary>
public static class ReconstruccionDeCredito
{
    /// <summary>Una operación de una clase que la reconstrucción todavía no interpreta.</summary>
    public const string CodigoOperacionNoSoportada = "Lending.Replay.OperationKindNotSupported";

    /// <summary>El primer hecho del crédito no es un desembolso ni un saldo inicial, o un tramo no cuadra con el plan.</summary>
    public const string CodigoOrigenInvalido = "Lending.Replay.InvalidOrigin";

    private static readonly IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion> Propios = new Dictionary<LoanOperationKind, IInterpreteDeOperacion>
    {
        [LoanOperationKind.Disbursement] = new InterpreteDeDesembolso(),
        [LoanOperationKind.OpeningBalance] = new InterpreteDeSaldoInicial(),
        [LoanOperationKind.RateReset] = new InterpreteDeReajuste(),
    };

    /// <summary>Las tasas del día ya calculadas: la raíz de cada EA es cara y los créditos comparten tasas y usuras (memo puro).</summary>
    private static readonly ConcurrentDictionary<EntradaDeTasaDelDia, ResultadoDeTasaDelDia> Tasas = new();

    private const int MaximoDeTasasEnMemoria = 20_000;

    /// <param name="entrada">El crédito y sus series.</param>
    /// <param name="fecha">D.</param>
    /// <param name="libroDesde">Desde qué día valor se arma la causación del libro teórico (nula: sólo los hechos de las operaciones).</param>
    /// <param name="interpretes">Los intérpretes que suman otras historias (US5, US6), además de los propios.</param>
    public static EstadoDelCredito Reconstruir(EntradaDeReconstruccion entrada, DateOnly fecha, DateOnly? libroDesde = null,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? interpretes = null)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var reglas = VersionesDeReglas.Para(entrada.Condiciones.RulesVersion);
        ValidarTasasSelladas(entrada.Condiciones);
        return new Corrida(entrada, fecha, libroDesde, interpretes, reglas).Ejecutar();
    }

    /// <summary>
    /// Una tasa sellada (corriente, mora fija, factor, margen, piso, techo) lleva a lo sumo seis decimales de fracción: con más, la
    /// condición no se pudo haber sellado y se rechaza con <c>Lending.Rate.TooPrecise</c>; nunca se redondea (T35).
    /// </summary>
    private static void ValidarTasasSelladas(CondicionesDeCredito c)
    {
        foreach (var (campo, tasa) in new (string, decimal?)[]
                 {
                     ("annualRate", c.TasaEfectivaAnual), ("defaultRate", c.MoraFijaEA), ("defaultRateFactor", c.FactorDeMora),
                     ("margin", c.Margen), ("rateFloor", c.Piso), ("rateCeiling", c.Techo),
                 })
            if (tasa is { } t && !EquivalenciaDeTasas.EsTasaPactable(t))
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(EquivalenciaDeTasas.CodigoTasaDemasiadoPrecisa,
                    $"La tasa sellada «{campo}» tiene más de seis decimales de fracción.", "field", campo));
    }

    /// <summary>Las operaciones que cuentan a (D, R), en orden.</summary>
    public static IReadOnlyList<EntradaDeOperacion> OperacionesVigentes(EntradaDeReconstruccion entrada, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var r = entrada.InformacionHasta;
        var conocidas = entrada.Operaciones.Where(o => o.RegistradoEn <= r).ToList();
        var reversadas = conocidas.Where(o => o.Reversa is not null).Select(o => o.Reversa!).ToHashSet(StringComparer.Ordinal);
        return conocidas
            .Where(o => o.Reversa is null && !reversadas.Contains(o.Id) && o.FechaValor <= fecha)
            .OrderBy(o => o.FechaValor).ThenBy(o => o.Sequence).ThenBy(o => o.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>La tasa del día (memo de <see cref="Rates.TasaDelDia.Calcular"/>).</summary>
    internal static ResultadoDeTasaDelDia TasaDelDia(EntradaDeTasaDelDia e)
    {
        if (Tasas.TryGetValue(e, out var r)) return r;
        if (Tasas.Count > MaximoDeTasasEnMemoria) Tasas.Clear();
        return Tasas.GetOrAdd(e, Rates.TasaDelDia.Calcular);
    }

    private sealed class Corrida(
        EntradaDeReconstruccion entrada, DateOnly fecha, DateOnly? libroDesde,
        IReadOnlyDictionary<LoanOperationKind, IInterpreteDeOperacion>? extras, ReglasDeCalculo reglas)
    {
        private readonly CondicionesDeCredito _c = entrada.Condiciones;
        private readonly CalendarioHabil _calendario = FechasDeVencimiento.CalendarioDelArticulo829(entrada.Festivos);
        private readonly List<ValorDeUsura> _usura = [.. entrada.Usura];
        private ContextoDeReconstruccion _ctx = null!;
        private RegistroDeVentanas _ventanas = null!;
        private int _versionDelPlan = -1;
        private DateOnly[] _exigibles = [];
        private List<DateOnly> _cortesDePeriodo = [];
        private readonly Dictionary<int, decimal> _saldoVencido = [];
        private int _versionDeSaldos = -1;

        public EstadoDelCredito Ejecutar()
        {
            var r = entrada.InformacionHasta;
            var operaciones = OperacionesVigentes(entrada, fecha);
            if (operaciones.Count == 0 || operaciones[0].FechaValor > fecha)
                return new EstadoDelCredito(fecha, r, reglas.Version, null, null, [], [], ResultadoDeAlturaDeMora.AlDia, null, null, null, [],
                    LibroTeorico.Vacio, () => new ExplicacionDeCartera().Linea($"Reglas {reglas.Version}: a {ExplicacionDeCartera.Fecha(fecha)} no hay crédito con la información hasta R."));

            var origen = operaciones[0].FechaValor;
            _ctx = new ContextoDeReconstruccion(entrada, origen);
            _ventanas = new RegistroDeVentanas(_c.Convencion, _c.Equivalencia);
            var pendientes = new Queue<EntradaDeOperacion>(operaciones);
            while (pendientes.Count > 0 && pendientes.Peek().FechaValor <= origen) Aplicar(pendientes.Dequeue());
            if (_ctx.Plan is null)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoOrigenInvalido,
                    "El crédito empieza con un desembolso o con un saldo inicial migrado.", "operationKind", operaciones[0].Clase.ToString()));

            var transiciones = new List<TransicionDeEstado>();
            DateOnly? primerSinUsura = null;
            var previo = Evaluar(origen).Motivo is not null;
            var cortes = Cortes(pendientes);
            var c = origen;
            while (c < fecha)
            {
                var aplico = false;
                while (pendientes.Count > 0 && pendientes.Peek().FechaValor <= c)
                {
                    Aplicar(pendientes.Dequeue());
                    aplico = true;
                }
                if (aplico) cortes = Cortes(pendientes);
                var d = c.AddDays(1);
                var ev = Evaluar(d);
                var (deseadas, certificada) = Deseadas(d, ev);
                if (!certificada && primerSinUsura is null) primerSinUsura = d;
                _ventanas.Fijar(c, deseadas);
                var ahora = ev.Motivo is not null;
                if (ReglaDeSuspension.Transicion(previo, ahora) is { } t) transiciones.Add(new TransicionDeEstado(d, t, ev.Motivo));
                previo = ahora;
                var siguiente = cortes.GetViewBetween(d, fecha);
                c = siguiente.Count > 0 ? siguiente.Min : fecha;
            }
            while (pendientes.Count > 0) Aplicar(pendientes.Dequeue());

            var plan = _ctx.Plan!;
            Preparar();
            var claves = _ventanas.Claves.ToList();
            foreach (var cuota in plan.Cuotas)
                if (cuota.Seguro != 0m) claves.Add(new ClaveDeCausacion(LoanConcept.Insurance, cuota.Numero));
            claves = claves.Distinct().OrderBy(k => (int)k.Concepto).ThenBy(k => k.Cuota).ThenBy(k => k.DeGracia).ToList();

            // El libro teórico: los hechos y la causación día a día desde libroDesde.
            var filas = new List<MovimientoDeLibro>(_ctx.Filas.Where(f => libroDesde is null || f.Dia >= libroDesde));
            if (libroDesde is { } desde)
            {
                var red = _c.Redondeo;
                for (var d = desde > origen ? desde : origen.AddDays(1); d <= fecha; d = d.AddDays(1))
                {
                    var porClave = new Dictionary<(LoanConcept, int), decimal>();
                    foreach (var k in claves)
                    {
                        var delta = red.Concepto(Exacto(k, d)) - red.Concepto(Exacto(k, d.AddDays(-1)));
                        if (delta != 0m) porClave[(k.Concepto, k.Cuota)] = porClave.GetValueOrDefault((k.Concepto, k.Cuota)) + delta;
                    }
                    foreach (var ((concepto, cuota), monto) in porClave)
                        if (monto != 0m) filas.Add(new MovimientoDeLibro(d, LoanTransactionKind.Accrual, concepto, cuota, monto));
                }
            }

            // Los saldos a D.
            var redondeo = _c.Redondeo;
            var saldos = new Dictionary<(int?, LoanConcept), decimal>();
            foreach (var cuota in plan.Cuotas)
            {
                var capital = _ctx.CapitalPendiente(cuota);
                if (capital != 0m) saldos[(cuota.Numero, LoanConcept.Principal)] = capital;
            }
            foreach (var k in claves)
            {
                var v = redondeo.Concepto(Exacto(k, fecha));
                if (v != 0m) saldos[(k.Cuota, k.Concepto)] = saldos.GetValueOrDefault((k.Cuota, k.Concepto)) + v;
            }
            foreach (var ((concepto, cuota), monto) in _ctx.SaldosFueraDeCausacion)
            {
                if (concepto == LoanConcept.Principal && cuota is not null) continue;
                saldos[(cuota, concepto)] = saldos.GetValueOrDefault((cuota, concepto)) + monto;
            }
            var listaDeSaldos = saldos.Where(s => s.Value != 0m)
                .OrderBy(s => s.Key.Item1 ?? 0).ThenBy(s => (int)s.Key.Item2)
                .Select(s => new SaldoDeCuota(s.Key.Item1, s.Key.Item2, s.Value)).ToList();

            var final = fecha > origen ? Evaluar(fecha) : new Evaluacion(new bool[plan.Cuotas.Count], ResultadoDeAlturaDeMora.AlDia, null);
            var cuotas = plan.Cuotas.Select((q, i) => new EstadoDeCuota(q.Numero, q.Vencimiento, _exigibles[i], _ctx.CapitalPendiente(q), final.Vencidas[i])).ToList();
            var tasa = TasaDelDia(EntradaDeTasa(Usura(fecha).Valor));
            var ventanas = _ventanas.Ventanas;
            return new EstadoDelCredito(fecha, r, reglas.Version, origen, plan, cuotas, listaDeSaldos, final.Altura, final.Motivo, tasa,
                primerSinUsura, transiciones, new LibroTeorico(filas), () => Explicar(plan, ventanas, primerSinUsura));
        }

        private void Aplicar(EntradaDeOperacion op)
        {
            IInterpreteDeOperacion? interprete = null;
            if (extras is not null && extras.TryGetValue(op.Clase, out var extra)) interprete = extra;
            else if (Propios.TryGetValue(op.Clase, out var propio)) interprete = propio;
            if (interprete is null)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoOperacionNoSoportada,
                    $"La reconstrucción todavía no interpreta operaciones {op.Clase}.", "operationKind", op.Clase.ToString()));
            interprete.Aplicar(_ctx, op);
        }

        /// <summary>Recalcula lo que depende del plan y de los saldos fuera de la causación.</summary>
        private void Preparar()
        {
            if (_versionDelPlan == _ctx.Version) return;
            _versionDelPlan = _ctx.Version;
            var plan = _ctx.Plan!;
            _exigibles = plan.Cuotas.Select(q => FechasDeVencimiento.FechaExigible(q.Vencimiento, _calendario)).ToArray();
            var cortes = new SortedSet<DateOnly>(plan.PeriodosDeGracia.Select(p => p.Hasta));
            foreach (var q in plan.Cuotas) cortes.Add(q.Vencimiento);
            var ultimo = cortes.Max;
            if (ultimo < fecha)
            {
                var meses = (fecha.Year - ultimo.Year) * 12 + fecha.Month - ultimo.Month + 2;
                foreach (var f in FechasDeVencimiento.Generar(ultimo, RepaymentPeriodicity.Monthly, meses, ultimo.Day)) cortes.Add(f);
            }
            _cortesDePeriodo = [.. cortes];
        }

        private SortedSet<DateOnly> Cortes(IEnumerable<EntradaDeOperacion> pendientes)
        {
            Preparar();
            var plan = _ctx.Plan!;
            var origen = _ctx.Origen;
            var s = new SortedSet<DateOnly> { origen };
            void Poner(DateOnly d)
            {
                if (d >= origen && d < fecha) s.Add(d);
            }
            foreach (var op in pendientes) Poner(op.FechaValor);
            foreach (var d in _cortesDePeriodo) Poner(d);
            foreach (var d in _exigibles) Poner(d);
            foreach (var u in _usura)
            {
                Poner(u.ValidFrom.AddDays(-1));
                if (u.ValidTo is { } h) Poner(h);
            }
            foreach (var u in entrada.UmbralesDeSuspension)
            {
                Poner(u.Desde.AddDays(-1));
                foreach (var ex in _exigibles) Poner(ex.AddDays(u.Valor - 1));
            }
            foreach (var t in entrada.Tolerancias) Poner(t.Desde.AddDays(-1));
            if (_c.InsolvenciaDesde is { } i) Poner(i.AddDays(-1));
            if (_c.CastigoDesde is { } k) Poner(k.AddDays(-1));
            return s;
        }

        private sealed record Evaluacion(bool[] Vencidas, ResultadoDeAlturaDeMora Altura, MotivoDeSuspension? Motivo);

        private Evaluacion Evaluar(DateOnly d)
        {
            Preparar();
            var plan = _ctx.Plan!;
            var vencidas = new bool[plan.Cuotas.Count];
            var paraMora = new List<CuotaParaMora>();
            for (var i = 0; i < plan.Cuotas.Count; i++)
            {
                vencidas[i] = AlturaDeMora.EstaVencida(_exigibles[i], d);
                if (vencidas[i]) paraMora.Add(new CuotaParaMora(plan.Cuotas[i].Numero, _exigibles[i], SaldoVencido(i, d)));
            }
            var tolerancia = Vigente(entrada.Tolerancias, d) ?? 0m;
            var umbral = Vigente(entrada.UmbralesDeSuspension, d);
            var altura = AlturaDeMora.Calcular(d, paraMora, tolerancia);
            var motivo = ReglaDeSuspension.Evaluar(new EntradaDeSuspension(altura.Dias, umbral, _c.Reestructuraciones,
                _c.InsolvenciaDesde is { } ins && ins <= d, _c.CastigoDesde is { } cas && cas <= d));
            return new Evaluacion(vencidas, altura, motivo);
        }

        /// <summary>
        /// El saldo vencido de la cuota i (capital pendiente, su corriente con su suspenso y su seguro, a la víspera del día): no cambia
        /// después de su exigible salvo por una operación, así que se guarda mientras no cambie el contexto.
        /// </summary>
        private decimal SaldoVencido(int i, DateOnly d)
        {
            if (_versionDeSaldos != _ctx.Version)
            {
                _saldoVencido.Clear();
                _versionDeSaldos = _ctx.Version;
            }
            if (_saldoVencido.TryGetValue(i, out var guardado)) return guardado;
            var cuota = _ctx.Plan!.Cuotas[i];
            var vispera = d.AddDays(-1);
            var saldo = _ctx.CapitalPendiente(cuota)
                + Saldo(cuota.Numero, LoanConcept.CurrentInterest, vispera)
                + Saldo(cuota.Numero, LoanConcept.CurrentInterestInSuspense, vispera)
                + Saldo(cuota.Numero, LoanConcept.Insurance, vispera);
            _saldoVencido[i] = saldo;
            return saldo;
        }

        /// <summary>El saldo de un concepto en una cuota a un día: lo de fuera de la causación más el redondeo de cada acumulado.</summary>
        private decimal Saldo(int cuota, LoanConcept concepto, DateOnly dia)
        {
            var red = _c.Redondeo;
            var total = _ctx.SaldoFueraDeCausacion(concepto, cuota);
            foreach (var k in _ventanas.Claves)
                if (k.Concepto == concepto && k.Cuota == cuota) total += red.Concepto(_ventanas.Exacto(k, dia));
            if (concepto == LoanConcept.Insurance) total += red.Concepto(SeguroExacto(cuota, dia));
            return total;
        }

        private decimal Exacto(ClaveDeCausacion k, DateOnly dia) =>
            k.Concepto == LoanConcept.Insurance ? SeguroExacto(k.Cuota, dia) : _ventanas.Exacto(k, dia);

        private decimal SeguroExacto(int numero, DateOnly dia)
        {
            var cuota = _ctx.Plan!.Cuotas.FirstOrDefault(q => q.Numero == numero);
            return cuota is null ? 0m : Causacion.SeguroExacto(cuota, dia, _c.CausacionDeSeguros, _c.Convencion, _ctx.Origen);
        }

        private (Dictionary<ClaveDeCausacion, VentanaDeseada> Deseadas, bool Certificada) Deseadas(DateOnly d, Evaluacion ev)
        {
            var plan = _ctx.Plan!;
            var (usura, certificada) = Usura(d);
            var tasa = TasaDelDia(EntradaDeTasa(usura));
            var periodo = _cortesDePeriodo.BinarySearch(d);
            if (periodo < 0) periodo = ~periodo;
            var suspendida = ev.Motivo is not null;
            var corriente = ReglaDeSuspension.ConceptoCorriente(suspendida);
            var mora = ReglaDeSuspension.ConceptoDeMora(suspendida);
            var salida = new Dictionary<ClaveDeCausacion, VentanaDeseada>();
            var cuotas = plan.Cuotas;

            if (plan.PeriodosDeGracia.Count > 0 && d <= cuotas[0].Desde)
            {
                var todo = 0m;
                foreach (var q in cuotas) todo += _ctx.CapitalPendiente(q);
                salida[new ClaveDeCausacion(corriente, cuotas[0].Numero, DeGracia: true)] = new VentanaDeseada(
                    new ParametrosDeVentana(todo, tasa.TasaDiariaCorriente, periodo), tasa.CorrienteEA, tasa.TopeDiarioCorriente, tasa.CorrienteTopada, suspendida);
            }
            else
            {
                var delPeriodo = cuotas[^1].Numero;
                foreach (var q in cuotas)
                    if (q.Vencimiento >= d) { delPeriodo = q.Numero; break; }
                var noVencido = 0m;
                for (var i = 0; i < cuotas.Count; i++)
                    if (!ev.Vencidas[i]) noVencido += _ctx.CapitalPendiente(cuotas[i]);
                if (noVencido > 0m)
                    salida[new ClaveDeCausacion(corriente, delPeriodo)] = new VentanaDeseada(
                        new ParametrosDeVentana(noVencido, tasa.TasaDiariaCorriente, periodo), tasa.CorrienteEA, tasa.TopeDiarioCorriente, tasa.CorrienteTopada, suspendida);
            }
            for (var i = 0; i < cuotas.Count; i++)
            {
                if (!ev.Vencidas[i]) continue;
                var q = cuotas[i];
                var b = _ctx.CapitalPendiente(q);
                if (_c.MoraSobreIntereses)
                    b += Saldo(q.Numero, LoanConcept.CurrentInterest, d.AddDays(-1)) + Saldo(q.Numero, LoanConcept.CurrentInterestInSuspense, d.AddDays(-1));
                if (b > 0m)
                    salida[new ClaveDeCausacion(mora, q.Numero)] = new VentanaDeseada(
                        new ParametrosDeVentana(b, tasa.TasaDiariaMora, periodo), tasa.MoraEA, tasa.TopeDiarioMora, tasa.MoraTopada, suspendida);
            }
            return (salida, certificada || !_c.UsuraRequerida);
        }

        /// <summary>La usura del día conocida en R y si está certificada; sin certificar, la última conocida que empezó antes (D11).</summary>
        private (decimal? Valor, bool Certificada) Usura(DateOnly d)
        {
            var r = entrada.InformacionHasta;
            var vigente = VigenciaBitemporal.Vigente(_usura, d, r);
            if (vigente is not null) return (vigente.Ea, true);
            var conocidas = _usura.Where(u => u.ConocidaEn <= r).ToList();
            var reemplazadas = conocidas.Where(u => u.SupersedesPublicId is not null).Select(u => u.SupersedesPublicId!.Value).ToHashSet();
            var ultima = conocidas.Where(u => !reemplazadas.Contains(u.PublicId) && u.ValidFrom <= d)
                .OrderByDescending(u => u.ValidFrom).ThenByDescending(u => u.Sequence).FirstOrDefault();
            return (ultima?.Ea, false);
        }

        private EntradaDeTasaDelDia EntradaDeTasa(decimal? usura) => new(_ctx.TasaCorrienteEA, _c.ClaseDeMora, _c.MoraFijaEA, _c.FactorDeMora,
            usura, _c.SobretasaDeCargos, _c.Convencion, _c.Equivalencia, _c.Periodicidad);

        private static T? Vigente<T>(IReadOnlyList<ValorConVigencia<T>> serie, DateOnly d) where T : struct
        {
            ValorConVigencia<T>? mejor = null;
            foreach (var v in serie)
                if (v.Desde <= d && (mejor is null || v.Desde > mejor.Desde)) mejor = v;
            return mejor?.Valor;
        }

        private ExplicacionDeCartera Explicar(PlanDePagos plan, IReadOnlyList<VentanaDeCausacion> ventanas, DateOnly? primerSinUsura)
        {
            var e = new ExplicacionDeCartera()
                .Linea($"Reglas {reglas.Version}: {reglas.Descripcion}")
                .Linea($"Reconstrucción a {ExplicacionDeCartera.Fecha(fecha)} con la información hasta {entrada.InformacionHasta:yyyy-MM-dd'T'HH:mm:ss}; "
                    + $"plan de {plan.Cuotas.Count} cuotas (la regenerada desde la {plan.PrimeraCuotaRegenerada}).");
            if (primerSinUsura is { } f)
                e.Linea($"Sin usura certificada desde el {ExplicacionDeCartera.Fecha(f)}: se proyectó con la última conocida; la liquidación vale hasta el "
                    + $"{ExplicacionDeCartera.Fecha(f.AddDays(-1))}.");
            foreach (var v in ventanas)
            {
                if (v.Desde >= fecha) continue;
                var hasta = v.Hasta is { } h && h < fecha ? h : fecha;
                var dias = ContadorDeDias.Dias(_c.Convencion, v.Desde, hasta);
                e.Tramo(new TramoDeCartera(v.Desde, hasta, dias, v.Parametros.TasaDiaria, v.Parametros.Base,
                    Causacion.Acumulado(v.Parametros.Base, v.Parametros.TasaDiaria, dias, _c.Equivalencia), v.Clave.Concepto.ToString(), v.Clave.Cuota,
                    v.Deseada.TasaEA, v.Deseada.TopeDiario, v.Deseada.Topada, v.Deseada.Suspendida));
            }
            return e;
        }
    }

    private sealed class InterpreteDeDesembolso : IInterpreteDeOperacion
    {
        public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
        {
            var monto = operacion.Monto ?? throw new ArgumentException("Un desembolso lleva su monto.", nameof(operacion));
            if (contexto.Plan is null)
            {
                var plan = contexto.Entrada.Plan ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoOrigenInvalido,
                    "El primer desembolso necesita el plan pactado.", "operation", operacion.Id));
                if (plan.Monto != monto || plan.FechaDesembolso != operacion.FechaValor)
                    throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoOrigenInvalido,
                        "El plan pactado es el del primer tramo: su monto y su fecha son los del desembolso.", "operation", operacion.Id));
                contexto.CambiarPlan(GeneradorDePlan.Generar(plan));
            }
            else
            {
                contexto.CambiarPlan(GeneradorDePlan.Regenerar(contexto.Plan, new EntradaDeRegeneracion(operacion.FechaValor) { CambioDeCapital = monto }));
            }
            contexto.AgregarFila(new MovimientoDeLibro(operacion.FechaValor, LoanTransactionKind.Disbursement, LoanConcept.Principal, null, monto)
            {
                OperacionId = operacion.Id,
            });
        }
    }

    private sealed class InterpreteDeSaldoInicial : IInterpreteDeOperacion
    {
        public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
        {
            if (contexto.Plan is not null)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoOrigenInvalido,
                    "El saldo inicial es el primer hecho de un crédito migrado.", "operation", operacion.Id));
            var c = contexto.Condiciones;
            var capital = operacion.Saldos.Where(s => s.Concepto == LoanConcept.Principal).Sum(s => s.Monto);
            contexto.CambiarPlan(GeneradorDePlan.DesdeMigracion(new EntradaDePlanMigrado(operacion.FechaValor, capital, operacion.CuotasMigradas,
                c.Periodicidad, contexto.Entrada.Plan?.Sistema ?? AmortizationSystem.LevelPayment, c.TasaEfectivaAnual, c.Convencion, c.Equivalencia, c.Redondeo)));
            foreach (var s in operacion.Saldos)
            {
                contexto.AgregarFila(new MovimientoDeLibro(operacion.FechaValor, LoanTransactionKind.OpeningBalance, s.Concepto, s.Cuota, s.Monto)
                {
                    OperacionId = operacion.Id,
                });
                if (s.Concepto != LoanConcept.Principal) contexto.SumarSaldo(s.Concepto, s.Cuota, s.Monto);
            }
        }
    }

    private sealed class InterpreteDeReajuste : IInterpreteDeOperacion
    {
        public void Aplicar(ContextoDeReconstruccion contexto, EntradaDeOperacion operacion)
        {
            var c = contexto.Condiciones;
            var ea = operacion.TasaEfectivaAnual
                ?? Rates.TasaDelDia.TasaVariable(operacion.Indice ?? throw new ArgumentException("Un reajuste lleva el índice o la tasa.", nameof(operacion)),
                    c.Margen, c.Piso, c.Techo);
            contexto.CambiarTasa(ea);
            contexto.CambiarPlan(GeneradorDePlan.Regenerar(contexto.Plan!, new EntradaDeRegeneracion(operacion.FechaValor) { NuevaTasaEfectivaAnual = ea }));
        }
    }
}
