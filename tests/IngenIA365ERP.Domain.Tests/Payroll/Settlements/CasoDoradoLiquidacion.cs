using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Settlements;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;
using IngenIA365ERP.Domain.Tests.Payroll.Calculation;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Domain.Tests.Payroll.Settlements;

/// <summary>
/// Un caso dorado del motor de liquidaciones especiales (feature 010, research R14; SC-001):
/// un archivo JSON en <c>Casos/</c> que la contadora puede leer, derivar a mano y firmar.
/// Es el hermano de <see cref="CasoDorado"/>: tipo, corte, empleado con historial de
/// salarios, bases por mes, suspensiones, saldo inicial, movimientos de vacaciones,
/// provisiones, motivo de retiro, deudas, políticas y lo que DEBE salir al peso, con la
/// derivación escrita en <see cref="Derivacion"/>. Un archivo puede traer varios casos en
/// <c>casos</c> cuando la misma regla se prueba con dos empleados.
///
/// <para>
/// Los parámetros son los de la semilla 2026 más los del archivo
/// <c>Parametros/liquidacion-2026.json</c> (todos DATOS, ninguno en el programa), y cada caso
/// puede sobreescribir escalares en <c>parametros</c>. Los conceptos son los de la semilla
/// más los de la 010 (<see cref="ConceptosDeLiquidacion"/>).
/// </para>
/// </summary>
public sealed class CasoDoradoLiquidacion
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

    /// <summary>La cuenta hecha a mano que respalda cada valor esperado. Obligatoria.</summary>
    public string Derivacion { get; set; } = string.Empty;

    public SettlementKind Tipo { get; set; }
    public DateTime Corte { get; set; }
    public DateTime? InicioPeriodo { get; set; }
    public EmpleadoJson Empleado { get; set; } = new();
    public List<AusenciaJson> Ausencias { get; set; } = [];
    public SaldoInicialJson? SaldoInicial { get; set; }
    public List<MovimientoJson> MovimientosVacaciones { get; set; } = [];
    public MovimientoJson? MovimientoALiquidar { get; set; }
    public List<BaseMensualJson> BasesPorMes { get; set; } = [];
    public Dictionary<string, decimal> Provisiones { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public TerminacionJson? Terminacion { get; set; }
    public List<DeudaJson> Deudas { get; set; } = [];
    public List<PrimaPagadaJson> PrimaPagadaEnDefinitiva { get; set; } = [];
    public SalarioPendienteJson? SalarioPendiente { get; set; }
    public AcumuladoJson? AcumuladoRetencion { get; set; }
    public PoliticasJson Politicas { get; set; } = new();
    public Dictionary<string, decimal> Parametros { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public EsperadoJson Esperado { get; set; } = new();

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Payroll", "Settlements", "Casos");
    public static string ArchivoDeParametros => Path.Combine(AppContext.BaseDirectory, "Payroll", "Settlements", "Parametros", "liquidacion-2026.json");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>Uno o varios casos por archivo (raíz con <c>casos</c>).</summary>
    public static IReadOnlyList<CasoDoradoLiquidacion> Cargar(string path)
    {
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (doc.RootElement.TryGetProperty("casos", out var casos))
            return casos.EnumerateArray().Select(c => c.Deserialize<CasoDoradoLiquidacion>(Opciones)!).ToList();
        return [JsonSerializer.Deserialize<CasoDoradoLiquidacion>(json, Opciones) ?? throw new InvalidOperationException($"El caso {path} está vacío.")];
    }

    public SettlementInput ConstruirEntrada()
    {
        var conceptos = ConceptosDeLiquidacion.Catalogo();
        var id = 0;
        foreach (var c in conceptos) c.Id = ++id;

        var parametros = ParametrosDeLiquidacion.Catalogo();
        id = 0;
        foreach (var p in parametros)
        {
            p.Id = ++id;
            if (Parametros.TryGetValue(p.Code, out var valor)) p.Value = valor;
        }

        return new SettlementInput
        {
            Kind = Tipo,
            CutoffDate = Corte,
            PeriodStart = InicioPeriodo,
            Employee = new SettlementEmployeeInput
            {
                PublicId = Empleado.Id,
                DisplayName = Empleado.Nombre ?? Nombre,
                Class = Empleado.Clase,
                JoinDate = Empleado.Ingreso,
                TerminationDate = Empleado.Retiro ?? (Tipo == SettlementKind.Settlement ? Corte : null),
                SalaryHistory = Empleado.Salarios.Select(s => new SalaryChangeInput(s.Desde, s.Mensual)).ToList(),
                ApprenticeStage = Empleado.EtapaAprendiz,
                ApprenticeStageFrom = Empleado.EtapaDesde,
                TransportAllowanceEntitled = Empleado.ConAuxilio,
                Affiliations = new AffiliationsInput
                {
                    Health = Empleado.Afiliaciones.Salud,
                    Pension = Empleado.Afiliaciones.Pension,
                    WorkRiskClass = Empleado.Afiliaciones.Arl,
                    FamilyCompensation = Empleado.Afiliaciones.Caja,
                },
                WithholdingProcedure = Empleado.ProcedimientoRetencion,
                WithholdingRatePercent = Empleado.TasaRetencion,
                TaxDeductions = Empleado.DeduccionesTributarias.Select(d => new TaxDeductionInput(d.Tipo, d.Mensual, d.Porcentaje)).ToList(),
                ContractType = Empleado.TipoContrato,
                ContractEndDate = Empleado.FinContrato,
            },
            Absences = Ausencias.Select(a => new AbsenceInput(a.Desde, a.Hasta, a.Suspension, a.Descripcion)).ToList(),
            OpeningBalance = SaldoInicial is null ? null : new OpeningBalanceInput
            {
                AsOfDate = SaldoInicial.Fecha,
                PendingVacationDays = SaldoInicial.DiasVacaciones,
                AccruedSeverance = SaldoInicial.Cesantias,
                AccruedSeveranceInterest = SaldoInicial.InteresesCesantias,
                AccruedServiceBonus = SaldoInicial.Prima,
                ServiceBonusDaysAccrued = SaldoInicial.DiasPrima,
                SeveranceDaysAccrued = SaldoInicial.DiasCesantias,
                EnteredBy = SaldoInicial.DigitadoPor ?? string.Empty,
                EnteredAt = SaldoInicial.DigitadoEl,
            },
            VacationMovements = MovimientosVacaciones.Select(Movimiento).ToList(),
            MovementToSettle = MovimientoALiquidar is null ? null : Movimiento(MovimientoALiquidar),
            MonthlyBases = BasesPorMes.Select(b => new MonthlyBaseInput(b.Anio, b.Mes, b.VariablesPrestacionales, b.VariablesVacaciones, b.IngresoLaboral)).ToList(),
            Provisions = Provisiones.Select(p => new ProvisionBalanceInput(p.Key, p.Value)).ToList(),
            Termination = Terminacion is null ? null : new TerminationInput
            {
                ReasonCode = Terminacion.Motivo,
                ReasonName = Terminacion.NombreMotivo ?? Terminacion.Motivo,
                GeneratesSeverancePay = Terminacion.GeneraIndemnizacion,
                VoluntaryRetirementBonus = Terminacion.BonificacionRetiro,
            },
            ProposedDeductions = Deudas.Select(d => new ProposedDeductionInput
            {
                ConceptCode = d.Concepto,
                Description = d.Descripcion,
                ProposedAmount = d.Propuesto,
                AppliedAmount = d.Aplicado ?? d.Propuesto,
                AccountedByOtherModule = d.ContabilizadaPorOtroModulo,
            }).ToList(),
            ServiceBonusPaidInSettlements = PrimaPagadaEnDefinitiva
                .Select((p, i) => new PaidServiceBonusInput(p.Corrida ?? new Guid(i + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), p.Hasta, p.Valor, p.Dias)).ToList(),
            PendingSalary = SalarioPendiente is null ? null : new PendingSalaryInput(SalarioPendiente.Inicio, SalarioPendiente.Fin),
            WithholdingYearToDate = AcumuladoRetencion is null ? null : new AcumuladoAnualDeRetencion(AcumuladoRetencion.RentaExentaUsada, AcumuladoRetencion.DeduccionesUsadas),
            Policies = new SettlementPolicies
            {
                Rounding = Politicas.Redondeo,
                SemanaLaboral = Politicas.SemanaLaboral,
                VacacionesPagoAnticipado = Politicas.PagoAnticipadoVacaciones,
                RetefteTopesAnualesModo = Politicas.TopesAnuales,
                PayrollStartDate = Politicas.ArranqueNomina,
            },
            Parameters = parametros,
            Concepts = conceptos,
        };
    }

    private static VacationMovementInput Movimiento(MovimientoJson m) => new()
    {
        PublicId = m.Id,
        Kind = m.Tipo,
        StartDate = m.Desde,
        EndDate = m.Hasta,
        BusinessDays = m.DiasHabiles,
        CalendarDays = m.DiasCalendario,
        IsCancelled = m.Anulado,
        Description = m.Descripcion,
    };

    // ------------------------------------------------------------------ JSON --

    public sealed class EmpleadoJson
    {
        public Guid Id { get; set; } = new("22222222-2222-2222-2222-222222222222");
        public string? Nombre { get; set; }
        public EmployeeClass Clase { get; set; } = EmployeeClass.Standard;
        public DateTime Ingreso { get; set; }
        public DateTime? Retiro { get; set; }
        public List<CasoDorado.SalarioJson> Salarios { get; set; } = [];
        public ApprenticeStage? EtapaAprendiz { get; set; }
        public DateTime? EtapaDesde { get; set; }
        public bool ConAuxilio { get; set; } = true;
        public CasoDorado.AfiliacionesJson Afiliaciones { get; set; } = new();
        public byte ProcedimientoRetencion { get; set; } = 1;
        public decimal? TasaRetencion { get; set; }
        public List<CasoDorado.DeduccionTributariaJson> DeduccionesTributarias { get; set; } = [];
        public DianContractType TipoContrato { get; set; } = DianContractType.Indefinite;
        public DateTime? FinContrato { get; set; }
    }

    public sealed class AusenciaJson
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public bool Suspension { get; set; }
        public string? Descripcion { get; set; }
    }

    public sealed class SaldoInicialJson
    {
        public DateTime Fecha { get; set; }
        public decimal DiasVacaciones { get; set; }
        public decimal Cesantias { get; set; }
        public decimal InteresesCesantias { get; set; }
        public decimal Prima { get; set; }
        public int? DiasPrima { get; set; }
        public int? DiasCesantias { get; set; }
        public string? DigitadoPor { get; set; }
        public DateTime? DigitadoEl { get; set; }
    }

    public sealed class MovimientoJson
    {
        public Guid? Id { get; set; }
        public VacationMovementKind Tipo { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public decimal DiasHabiles { get; set; }
        public int DiasCalendario { get; set; }
        public bool Anulado { get; set; }
        public string? Descripcion { get; set; }
    }

    public sealed class BaseMensualJson
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public decimal VariablesPrestacionales { get; set; }
        public decimal VariablesVacaciones { get; set; }
        public decimal IngresoLaboral { get; set; }
    }

    public sealed class TerminacionJson
    {
        public string Motivo { get; set; } = string.Empty;
        public string? NombreMotivo { get; set; }
        public bool GeneraIndemnizacion { get; set; }
        public decimal? BonificacionRetiro { get; set; }
    }

    public sealed class DeudaJson
    {
        public string Concepto { get; set; } = WellKnownConceptCodes.LoanDeduction;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Propuesto { get; set; }
        public decimal? Aplicado { get; set; }
        public bool ContabilizadaPorOtroModulo { get; set; } = true;
    }

    public sealed class PrimaPagadaJson
    {
        public Guid? Corrida { get; set; }
        public DateTime Hasta { get; set; }
        public decimal Valor { get; set; }
        public int Dias { get; set; }
    }

    public sealed class SalarioPendienteJson
    {
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }
    }

    public sealed class AcumuladoJson
    {
        public decimal RentaExentaUsada { get; set; }
        public decimal DeduccionesUsadas { get; set; }
    }

    public sealed class PoliticasJson
    {
        /// <summary>Al centavo por defecto: los ejemplos de la norma se derivan con decimales (1.124.547,50).</summary>
        public PayrollRounding Redondeo { get; set; } = PayrollRounding.Centavo;
        public SemanaLaboral SemanaLaboral { get; set; } = SemanaLaboral.LunesASabado;
        public bool PagoAnticipadoVacaciones { get; set; } = true;
        public ModoDeTopesAnuales TopesAnuales { get; set; } = ModoDeTopesAnuales.Mensualizado;
        public DateTime? ArranqueNomina { get; set; }
    }

    public sealed class EsperadoJson
    {
        /// <summary>Código de exclusión del empleado (<c>SettlementReasonCodes</c>); nulo = tiene derecho.</summary>
        public string? Excluido { get; set; }

        /// <summary>Código → valor redondeado. Salvo <see cref="SoloLineasListadas"/>, toda línea que no esté aquí debe valer cero.</summary>
        public Dictionary<string, decimal> Lineas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Verdadero cuando el caso prueba una regla y no deriva el resto de la liquidación; entonces tampoco se comparan totales.</summary>
        public bool SoloLineasListadas { get; set; }

        public CasoDorado.TotalesJson? Totales { get; set; }
        public SettlementFlags Banderas { get; set; } = SettlementFlags.None;

        /// <summary>«CONCEPTO:CodigoDeMotivo» que deben aparecer en las omisiones.</summary>
        public List<string> Omitidos { get; set; } = [];

        /// <summary>Fragmentos que deben aparecer en las negativas; vacío = no debe negarse a nada.</summary>
        public List<string> Rechazos { get; set; } = [];

        /// <summary>Fragmentos que deben aparecer en las advertencias.</summary>
        public List<string> Advertencias { get; set; } = [];

        /// <summary>Fragmentos que deben aparecer en la explicación de una línea: «CONCEPTO: texto».</summary>
        public List<string> Explicaciones { get; set; } = [];

        public VacacionesJson? Vacaciones { get; set; }
    }

    public sealed class VacacionesJson
    {
        public decimal? Causados { get; set; }
        public decimal? Pendientes { get; set; }
    }
}

/// <summary>Los parámetros de los casos: semilla 2026 más el archivo de liquidación, por código.</summary>
public static class ParametrosDeLiquidacion
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static List<PayrollLegalParameter> Catalogo()
    {
        var lista = PayrollLegalParametersSeeder.Catalogo().ToList();
        var archivo = JsonSerializer.Deserialize<ArchivoJson>(File.ReadAllText(CasoDoradoLiquidacion.ArchivoDeParametros), Opciones)
            ?? throw new InvalidOperationException("El archivo de parámetros de liquidación está vacío.");

        void Poner(PayrollLegalParameter p)
        {
            lista.RemoveAll(x => x.Code.Equals(p.Code, StringComparison.OrdinalIgnoreCase));
            lista.Add(p);
        }

        foreach (var e in archivo.Escalares)
            Poner(new PayrollLegalParameter { Code = e.Codigo, Name = e.Codigo, Kind = e.Tipo, Value = e.Valor, ValidFrom = archivo.Vigencia, Source = e.Fuente });

        foreach (var t in archivo.Tablas)
        {
            var p = new PayrollLegalParameter
            {
                Code = t.Codigo, Name = t.Nombre ?? t.Codigo, Kind = LegalParameterKind.RangeTable, ValidFrom = archivo.Vigencia, Source = t.Fuente,
                RangeUnitParameterCode = t.Unidad, RangeIsMarginal = t.Marginal,
            };
            var order = 0;
            foreach (var r in t.Tramos)
                p.Ranges.Add(new PayrollLegalParameterRange { FromValue = r.Desde, ToValue = r.Hasta, Rate = r.Tarifa, FixedValue = r.Fijo, Order = ++order });
            Poner(p);
        }
        return lista;
    }

    public sealed class ArchivoJson
    {
        public DateTime Vigencia { get; set; }
        public List<EscalarJson> Escalares { get; set; } = [];
        public List<TablaJson> Tablas { get; set; } = [];
    }

    public sealed class EscalarJson
    {
        public string Codigo { get; set; } = string.Empty;
        public LegalParameterKind Tipo { get; set; }
        public decimal Valor { get; set; }
        public string? Fuente { get; set; }
    }

    public sealed class TablaJson
    {
        public string Codigo { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public string? Unidad { get; set; }
        public bool Marginal { get; set; }
        public string? Fuente { get; set; }
        public List<TramoJson> Tramos { get; set; } = [];
    }

    public sealed class TramoJson
    {
        public decimal Desde { get; set; }
        public decimal? Hasta { get; set; }
        public decimal? Tarifa { get; set; }
        public decimal? Fijo { get; set; }
    }
}

/// <summary>
/// Los conceptos de los casos: la semilla más los de la feature 010 (data-model §1.7) cuando la
/// semilla aún no los trae. Sólo definiciones —naturaleza, bases que alimentan, clases—, ningún
/// valor: los importes los calcula el motor. Cuando <c>PayrollConceptDefinitionsSeeder</c> los
/// incorpore, mandan los de la semilla.
/// </summary>
public static class ConceptosDeLiquidacion
{
    private static readonly DateTime Vigencia = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static List<PayrollConceptDefinition> Catalogo()
    {
        var lista = PayrollConceptDefinitionsSeeder.Catalogo().ToList();

        void Falta(string code, string name, ConceptNature nature, Action<PayrollConceptDefinition>? configurar = null)
        {
            if (lista.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))) return;
            var d = new PayrollConceptDefinition
            {
                Code = code, Name = name, Nature = nature, CalculationKind = CalculationKind.FixedAmount,
                Origin = ConceptOrigin.Seed, ValidFrom = Vigencia, IsActive = true, IsAutomatic = true,
            };
            configurar?.Invoke(d);
            lista.Add(d);
        }

        Falta(WellKnownConceptCodes.ServiceBonus, "Prima de servicios", ConceptNature.Earning, d => d.AffectsWithholdingBase = true);
        Falta(WellKnownConceptCodes.Severance, "Cesantías", ConceptNature.Earning);
        Falta(WellKnownConceptCodes.SeveranceInterest, "Intereses a las cesantías", ConceptNature.Earning);
        Falta(WellKnownConceptCodes.VacationPayout, "Vacaciones disfrutadas (liquidación)", ConceptNature.Earning, d => d.AffectsWithholdingBase = true);
        Falta(WellKnownConceptCodes.VacationCompensation, "Vacaciones compensadas en dinero", ConceptNature.Earning, d =>
        {
            d.AffectsContributionBase = true; d.AffectsWithholdingBase = true;
        });
        Falta(WellKnownConceptCodes.Indemnity, "Indemnización por despido sin justa causa", ConceptNature.Earning);
        Falta(WellKnownConceptCodes.RetirementBonus, "Bonificación por retiro", ConceptNature.Earning, d => d.IsAutomatic = false);
        Falta(WellKnownConceptCodes.PendingSalary, "Salario de los días pendientes", ConceptNature.Earning, d =>
        {
            d.AffectsSalaryBase = d.AffectsContributionBase = d.AffectsBenefitsBase = d.AffectsWithholdingBase = true;
        });
        Falta(WellKnownConceptCodes.ServiceBonusProvisionAdjustment, "Ajuste de provisión de prima", ConceptNature.Provision);
        Falta(WellKnownConceptCodes.SeveranceProvisionAdjustment, "Ajuste de provisión de cesantías", ConceptNature.Provision);
        Falta(WellKnownConceptCodes.SeveranceInterestProvisionAdjustment, "Ajuste de provisión de intereses a las cesantías", ConceptNature.Provision);
        Falta(WellKnownConceptCodes.VacationProvisionAdjustment, "Ajuste de provisión de vacaciones", ConceptNature.Provision);
        Falta(WellKnownConceptCodes.WithholdingOnServiceBonus, "Retención en la fuente sobre la prima", ConceptNature.Deduction, d =>
        {
            d.CalculationKind = CalculationKind.RangeTable; d.TableParameterCode = LegalParameterCodes.WithholdingTableUvt;
        });
        Falta(WellKnownConceptCodes.WithholdingOnSeverance, "Retención en la fuente sobre cesantías e intereses", ConceptNature.Deduction);
        Falta(WellKnownConceptCodes.WithholdingOnIndemnity, "Retención en la fuente sobre la indemnización", ConceptNature.Deduction);
        return lista;
    }
}
