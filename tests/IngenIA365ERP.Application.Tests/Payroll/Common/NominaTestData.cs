using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Common;

/// <summary>
/// Escenario mínimo de nómina para probar handlers con InMemory: un plan por defecto,
/// los conceptos y parámetros legales de la semilla 2026, un período abierto (marzo
/// 2026) y un empleado estándar con su persona. Los servicios reales que no hacen IO
/// externo (marcador de Stale, traslados, cargador, contabilizador) se usan de verdad;
/// reloj, usuario, lock y permisos son sustitutos.
/// </summary>
public sealed class NominaTestData
{
    public TestApplicationDbContext Db { get; }
    public IDateTimeService Clock { get; }
    public ICurrentUserService User { get; }
    public IAuditAppendOnlyWriter Audit { get; }
    public IDistributedLock Lock { get; }
    public IPermissionChecker Permissions { get; }
    public IPayrollRunStaleMarker StaleMarker { get; }
    public CarryOverNoveltiesService CarryOver { get; }
    public PayrollAuditEmitter AuditEmitter { get; }
    public PayrollPolicyReader Policies { get; }
    public CalculationInputLoader Loader { get; }
    public IUserBranchScope Alcance { get; }
    public PayrollAccountingPoster Poster { get; }
    public RecurringNoveltiesMaterializer Recurrentes { get; }

    // Feature 010: lo que las liquidaciones especiales comparten.
    public ProvisionBalanceReader SaldosDeProvision { get; }
    public SettlementInputLoader SettlementLoader { get; }

    public PayrollPlan Plan { get; }
    public PayPeriod Marzo { get; }
    public Employee Ana { get; }

    public static readonly DateTime Ahora = new(2026, 3, 20, 14, 0, 0, DateTimeKind.Utc);

    public NominaTestData()
    {
        Db = TestDbContextFactory.Create();
        Clock = Substitute.For<IDateTimeService>();
        Clock.UtcNow.Returns(Ahora);
        Clock.TodayUtc.Returns(DateOnly.FromDateTime(Ahora));
        User = UsuarioDePrueba("ana@demo", 7);
        Audit = Substitute.For<IAuditAppendOnlyWriter>();
        Lock = Substitute.For<IDistributedLock>();
        Lock.TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IDistributedLockHandle?>(Substitute.For<IDistributedLockHandle>()));
        Permissions = Substitute.For<IPermissionChecker>();
        Permissions.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        StaleMarker = new PayrollRunStaleMarker(Db, NullLogger<PayrollRunStaleMarker>.Instance);
        CarryOver = new CarryOverNoveltiesService(Db, Clock, User);
        AuditEmitter = new PayrollAuditEmitter(Audit, User, Clock, NullLogger<PayrollAuditEmitter>.Instance, CooperativaDePrueba.Actual);
        Policies = new PayrollPolicyReader(Db, Clock);
        Loader = new CalculationInputLoader(Db, Policies);
        SaldosDeProvision = new ProvisionBalanceReader(Db);
        SettlementLoader = new SettlementInputLoader(Db, Policies, SaldosDeProvision, NullLogger<SettlementInputLoader>.Instance);
        Alcance = Substitute.For<IUserBranchScope>();
        Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(AlcanceDeSucursales.SinRestriccion));
        Poster = Contabilizador(User);
        Recurrentes = new RecurringNoveltiesMaterializer(Db, Clock, User);

        Plan = new PayrollPlan { Code = "DEFAULT", Name = "Nómina general", Periodicity = PayrollPeriodicity.Monthly, IsDefault = true, IsActive = true, CreatedBy = "system:seed" };
        Db.PayrollPlans.Add(Plan);
        Db.PayrollConceptDefinitions.AddRange(PayrollConceptDefinitionsSeeder.Catalogo());
        Db.PayrollLegalParameters.AddRange(PayrollLegalParametersSeeder.Catalogo());
        // Las cinco clases ARL con Ids que NO coinciden con la clase (una fila de relleno
        // antes): la ficha guarda la fila y la clase es su Code, no su Id.
        Db.WorkRiskRates.Add(new WorkRiskRate { Code = 9, Name = "Relleno", ShortName = "X", Rate = 0m, CreatedBy = "test", IsDeleted = true });
        Db.WorkRiskRates.AddRange(WorkRiskClassesSeeder.Catalogo());
        Db.SaveChanges();

        Marzo = Periodo(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), PayPeriodStatus.Open);
        Ana = Empleado("Ana", 2_000_000m, new DateTime(2025, 1, 15));
    }

    public static ICurrentUserService UsuarioDePrueba(string nombre, int id)
    {
        var u = Substitute.For<ICurrentUserService>();
        u.UserName.Returns(nombre);
        u.UserId.Returns(id);
        u.TenantId.Returns("1");
        u.IsAuthenticated.Returns(true);
        return u;
    }

    public PayPeriod Periodo(DateTime desde, DateTime hasta, PayPeriodStatus estado, int planilla = 0, PayrollPlan? plan = null)
    {
        plan ??= Plan;
        var calendario = IngenIA365ERP.Domain.Payroll.Calculation.PeriodCalendar.Proponer(plan.Periodicity, desde);
        var p = new PayPeriod
        {
            PlanId = planilla == 0 ? Db.PayPeriods.Count() + 1 : planilla,
            PayrollCompanyId = 1,
            PayrollPlanId = plan.Id,
            Description = $"{desde:MMMM yyyy}",
            StartDate = desde,
            EndDate = hasta,
            Periodicity = (int)plan.Periodicity,
            SubPeriodNumber = calendario.SubPeriodNumber,
            ImputationYear = calendario.Year,
            ImputationMonth = calendario.Month,
            Status = estado,
            StatusMessage = string.Empty,
            AdvanceLiquidation = "N",
            AdvanceCrossing = "N",
            CreatedBy = "test",
        };
        Db.PayPeriods.Add(p);
        Db.SaveChanges();
        return p;
    }

    /// <summary>Fila de <c>PAY_WorkRiskRates</c> de una clase (1..5), o 0 si no se quiere clase.</summary>
    public int TarifaArl(int claseArl) =>
        claseArl == 0 ? 0 : Db.WorkRiskRates.Single(r => r.Code == claseArl).Id;

    public Employee Empleado(string nombre, decimal salario, DateTime ingreso, EmployeeClass clase = EmployeeClass.Standard,
        DateTime? retiro = null, byte procedimientoRetencion = 1, int claseArl = 1)
    {
        var n = Db.Employees.Count() + 1;
        var persona = new Person
        {
            FirstName = nombre,
            LastName = "Prueba",
            TaxId = (1_000_000_000L + n).ToString(),
            Email = $"{nombre.ToLowerInvariant()}@coop.test",
            CreatedBy = "test",
        };
        Db.People.Add(persona);
        Db.SaveChanges();

        var e = new Employee
        {
            PersonId = persona.Id,
            PayrollCompanyId = 1,
            PayrollPlanId = Plan.Id,
            Salary = salario,
            JoinDate = ingreso,
            TerminationDate = retiro ?? DateTime.MaxValue.Date,
            Status = retiro is null ? 1 : -1,
            EmployeeClass = clase,
            HealthInsuranceId = 1,
            PensionFundId = 1,
            WorkRiskRateId = TarifaArl(claseArl),
            FamilySubsidyId = 1,
            WithholdingProcedure = procedimientoRetencion,
            CostCenterId = "01",
            CreatedBy = "test",
        };
        Db.Employees.Add(e);
        Db.SaveChanges();
        return e;
    }

    public PayrollRun Borrador(PayPeriod periodo, int version = 1)
    {
        var run = new PayrollRun
        {
            PayPeriodId = periodo.Id,
            Version = version,
            Status = PayrollRunStatus.Draft,
            CalculatedAt = Ahora,
            CalculatedBy = "ana@demo",
            InputsHash = new string('a', 64),
            CreatedBy = "test",
        };
        Db.PayrollRuns.Add(run);
        periodo.Status = PayPeriodStatus.Calculated;
        Db.SaveChanges();
        return run;
    }

    /// <summary>El contabilizador de nómina sobre el contrato (feature 009), con el usuario indicado.</summary>
    public PayrollAccountingPoster Contabilizador(ICurrentUserService quien) =>
        new(Db, new AccountingPoster(Db, Clock, quien, Alcance));

    public Branch Principal { get; private set; } = null!;

    /// <summary>
    /// Lo que la aprobación necesita (modelo de la feature 009): sucursal principal, centro de
    /// costo, configuración contable iniciada, ejercicio 2026 con marzo abierto (o cerrado), el
    /// tipo NM del módulo y un par de cuentas de movimiento habilitadas para Nómina por cada
    /// concepto de la semilla.
    /// </summary>
    public void ConfigurarContabilidad(bool periodoContableAbierto = true)
    {
        Principal = new Branch { Name = "Principal", CreatedBy = "test" };
        Db.Branches.Add(Principal);
        Db.CostCenters.Add(new CostCenter { LegacyCode = "01", Name = "Administración", CreatedBy = "test" });
        Db.VoucherTypes.Add(new VoucherType { Code = "NM", Name = "Nómina", Usage = VoucherUsage.Module, ModuleCode = "NOM", NextNumber = 1, IsSeeded = true, CreatedBy = "test" });
        var catalogo = new AccountCatalog { Code = "PRUEBA", Name = "Catálogo de prueba", Version = "2026", CreatedBy = "test" };
        Db.AccountCatalogs.Add(catalogo);
        Db.SaveChanges();
        Db.AccountingSetups.Add(new AccountingSetup
        {
            CatalogId = catalogo.Id, MovementLevel = 5, NiifGroup = 2, FirstFiscalYear = 2026,
            MainBranchId = Principal.Id, InitializedAt = Ahora, InitializedBy = "test", CreatedBy = "test",
        });
        PeriodoContable(2026, 3, periodoContableAbierto);

        var i = 0;
        foreach (var code in Db.PayrollConceptDefinitions.Select(c => c.Code).Distinct().ToList())
        {
            i++;
            var debito = Cuenta($"5105{i:00}", $"Gasto {code}", AccountNature.Debit);
            var credito = Cuenta($"2505{i:00}", $"Pasivo {code}", AccountNature.Credit);
            Db.PayrollConceptDefinitionAccounts.Add(new PayrollConceptDefinitionAccount
            {
                ConceptCode = code, CostCenterId = null, DebitAccountId = debito.Id, CreditAccountId = credito.Id, CreatedBy = "test",
            });
        }
        Db.SaveChanges();
    }

    /// <summary>Un período mensual del ejercicio (que se crea si no existe), abierto o cerrado.</summary>
    public AccountingPeriod PeriodoContable(int year, int month, bool abierto = true)
    {
        var ejercicio = Db.FiscalYears.FirstOrDefault(f => f.Year == year);
        if (ejercicio is null)
        {
            ejercicio = new FiscalYear { Year = year, CreatedBy = "test" };
            Db.FiscalYears.Add(ejercicio);
            Db.SaveChanges();
        }
        var inicio = new DateOnly(year, month, 1);
        var periodo = new AccountingPeriod
        {
            FiscalYearId = ejercicio.Id, Month = (byte)month, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1),
            Status = abierto ? PeriodStatus.Open : PeriodStatus.Closed, CreatedBy = "test",
        };
        Db.AccountingPeriods.Add(periodo);
        Db.SaveChanges();
        return periodo;
    }

    /// <summary>Cuenta de movimiento activa, habilitada para Contabilidad y Nómina; las reglas se piden aparte.</summary>
    public ChartOfAccount Cuenta(string code, string name, AccountNature nature, bool exigeCentroDeCosto = false, bool exigeTercero = false,
        AccountingModules modulos = AccountingModules.Accounting | AccountingModules.Payroll)
    {
        var c = new ChartOfAccount
        {
            Code = code, Name = name, Level = 5, Nature = nature, NiifItemCode = "X", Origin = AccountOrigin.Company, IsMovement = true, IsActive = true,
            EnabledModules = modulos, RequiresCostCenter = exigeCentroDeCosto, RequiresThirdParty = exigeTercero, CreatedBy = "test",
        };
        Db.ChartOfAccounts.Add(c);
        Db.SaveChanges();
        return c;
    }

    public void PermitirAprobarMismoUsuario()
    {
        Db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = PayrollPolicyReader.AllowSameUserApprovalKey, SettingValue = "true", ValueType = "Bool", ModulePrefix = "PAY", CreatedBy = "test",
        });
        Db.SaveChanges();
    }

    // ============================================================ feature 010 ==

    /// <summary>Mueve «hoy» del reloj de prueba: las liquidaciones fechan el comprobante al corte y el corte no puede ser posterior a hoy.</summary>
    public void HoyEs(DateTime fecha)
    {
        var utc = DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
        Clock.UtcNow.Returns(utc);
        Clock.TodayUtc.Returns(DateOnly.FromDateTime(utc));
    }

    /// <summary>Una política por empresa con vigencia (<c>PAY_CompanyPolicies</c>).</summary>
    public CompanyPolicy Politica(string clave, string valor, DateOnly desde, DateOnly? hasta = null)
    {
        var p = new CompanyPolicy { Key = clave, Value = valor, ValidFrom = desde, ValidTo = hasta, CreatedBy = "test" };
        Db.CompanyPolicies.Add(p);
        Db.SaveChanges();
        return p;
    }

    /// <summary>Un cambio de salario con fecha de efecto; como <c>RegisterSalaryChange</c>, siembra la línea base del ingreso si no había historial.</summary>
    public SalaryChange CambioDeSalario(Employee e, DateTime desde, decimal nuevoSalario)
    {
        if (!Db.SalaryChanges.Any(s => s.EmployeeId == e.Id) && desde > e.JoinDate.Date)
            Db.SalaryChanges.Add(new SalaryChange { PayrollCompanyId = 1, EmployeeId = e.Id, EffectiveDate = e.JoinDate.Date, NewSalary = e.Salary, CreatedBy = "system:baseline" });
        var c = new SalaryChange { PayrollCompanyId = 1, EmployeeId = e.Id, EffectiveDate = desde, NewSalary = nuevoSalario, CreatedBy = "test" };
        Db.SalaryChanges.Add(c);
        e.Salary = nuevoSalario;
        Db.SaveChanges();
        return c;
    }

    /// <summary>El saldo inicial de prestaciones de un empleado a una fecha (R3, FR-007).</summary>
    public EmployeeBenefitOpeningBalance SaldoInicial(Employee e, DateOnly asOf, decimal prima = 0m, decimal cesantias = 0m, decimal intereses = 0m,
        decimal diasVacaciones = 0m, int? diasPrima = null, int? diasCesantias = null)
    {
        var b = new EmployeeBenefitOpeningBalance
        {
            EmployeeId = e.Id, AsOfDate = asOf, Kind = OpeningBalanceKind.Opening,
            AccruedServiceBonus = prima, AccruedSeverance = cesantias, AccruedSeveranceInterest = intereses, PendingVacationDays = diasVacaciones,
            ServiceBonusDaysAccrued = diasPrima, SeveranceDaysAccrued = diasCesantias, CreatedBy = "contadora@demo", CreatedAt = Ahora,
        };
        Db.EmployeeBenefitOpeningBalances.Add(b);
        Db.SaveChanges();
        return b;
    }

    /// <summary>Una línea de una corrida aprobada: código, naturaleza y valor; la definición se busca en la semilla por código.</summary>
    public sealed record Linea(string Code, ConceptNature Nature, decimal Amount, decimal? Quantity = null, bool AffectsAccounting = true);

    /// <summary>
    /// Una corrida ordinaria APROBADA de un mes anterior con las líneas que se indiquen (salario,
    /// variables, provisiones), para que el cargador de liquidaciones tenga bases prestacionales
    /// y provisión acumulada de dónde leer. Crea el período del mes en <c>Approved</c>.
    /// </summary>
    public PayrollRun CorridaAprobada(int año, int mes, Employee e, params Linea[] lineas)
    {
        var desde = new DateTime(año, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var periodo = Db.PayPeriods.FirstOrDefault(p => p.PayrollPlanId == Plan.Id && p.StartDate == desde && p.EndDate == hasta)
                      ?? Periodo(desde, hasta, PayPeriodStatus.Approved);
        periodo.Status = PayPeriodStatus.Approved;

        var run = Db.PayrollRuns.Include(r => r.Employees).FirstOrDefault(r => r.PayPeriodId == periodo.Id && r.Status == PayrollRunStatus.Approved);
        if (run is null)
        {
            run = new PayrollRun
            {
                Kind = PayrollRunKind.Ordinary, PayPeriodId = periodo.Id, Version = 1, Status = PayrollRunStatus.Approved,
                CalculatedAt = hasta, CalculatedBy = "ana@demo", ApprovedAt = hasta, ApprovedBy = "contadora@demo",
                InputsHash = new string('b', 64), CreatedBy = "test",
            };
            Db.PayrollRuns.Add(run);
            periodo.RunPublicId = run.PublicId;
        }

        var definiciones = Db.PayrollConceptDefinitions.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var fila = new PayrollRunEmployee { EmployeeId = e.Id, PayrollPlanId = Plan.Id, DaysWorked = 30, EmployeeClass = e.EmployeeClass, CreatedBy = "test" };
        var orden = 0;
        foreach (var l in lineas)
        {
            var def = definiciones.GetValueOrDefault(l.Code) ?? throw new InvalidOperationException($"La semilla no trae el concepto {l.Code}.");
            fila.Lines.Add(new PayrollRunLine
            {
                ConceptDefinitionId = def.Id, ConceptCode = def.Code, ConceptName = def.Name, Nature = l.Nature, Amount = l.Amount, Quantity = l.Quantity,
                AffectsAccounting = l.AffectsAccounting, Order = ++orden, ExplanationJson = "{}", CreatedBy = "test",
            });
        }
        fila.TotalEarnings = lineas.Where(l => l.Nature == ConceptNature.Earning).Sum(l => l.Amount);
        fila.TotalDeductions = lineas.Where(l => l.Nature == ConceptNature.Deduction).Sum(l => l.Amount);
        fila.TotalEmployerContributions = lineas.Where(l => l.Nature == ConceptNature.EmployerContribution).Sum(l => l.Amount);
        fila.TotalProvisions = lineas.Where(l => l.Nature == ConceptNature.Provision).Sum(l => l.Amount);
        fila.NetPay = fila.TotalEarnings - fila.TotalDeductions;
        run.Employees.Add(fila);
        run.EmployeeCount = run.Employees.Count;
        run.TotalEarnings += fila.TotalEarnings;
        run.TotalDeductions += fila.TotalDeductions;
        run.TotalProvisions += fila.TotalProvisions;
        run.TotalEmployerContributions += fila.TotalEmployerContributions;
        run.TotalNet += fila.NetPay;
        Db.SaveChanges();
        return run;
    }

    /// <summary>
    /// Un mes «típico» aprobado para el empleado: salario y auxilio como devengos y las cuatro
    /// provisiones (prima, cesantías, intereses, vacaciones) con los valores que se indiquen.
    /// </summary>
    public PayrollRun MesAprobado(int año, int mes, Employee e, decimal salario, decimal auxilio, decimal provPrima, decimal provCesantias, decimal provIntereses, decimal provVacaciones, decimal variables = 0m)
    {
        var lineas = new List<Linea>
        {
            new("SALARIO", ConceptNature.Earning, salario, 30m),
            new("PROV_PRIMA", ConceptNature.Provision, provPrima),
            new("PROV_CESANTIAS", ConceptNature.Provision, provCesantias),
            new("PROV_INT_CESANTIAS", ConceptNature.Provision, provIntereses),
            new("PROV_VACACIONES", ConceptNature.Provision, provVacaciones),
        };
        if (auxilio > 0m) lineas.Add(new("AUX_TRANSPORTE", ConceptNature.Earning, auxilio));
        if (variables > 0m) lineas.Add(new("HEX_DIURNA", ConceptNature.Earning, variables, 4m));
        return CorridaAprobada(año, mes, e, lineas.ToArray());
    }

    /// <summary>Un fondo de cesantías con persona vinculada (FR-088), asignado a la ficha; devuelve la persona del fondo.</summary>
    public Person FondoDeCesantiasConPersona(Employee e, string nombre = "Porvenir")
    {
        var persona = new Person { FirstName = nombre, LastName = "S.A.", TaxId = $"8{Db.People.Count():000000000}", CreatedBy = "test" };
        Db.People.Add(persona);
        Db.SaveChanges();
        var fondo = new SeveranceProvider { Code = nombre.ToUpperInvariant()[..Math.Min(10, nombre.Length)], Name = nombre, PersonId = persona.Id, CreatedBy = "test" };
        Db.SeveranceProviders.Add(fondo);
        Db.SaveChanges();
        e.SeveranceFundId = fondo.Id;
        Db.SaveChanges();
        return persona;
    }

    /// <summary>El contabilizador de liquidaciones especiales sobre el de nómina, con el usuario indicado.</summary>
    public SettlementAccountingPoster ContabilizadorDeLiquidaciones(ICurrentUserService quien) =>
        new(Db, Contabilizador(quien), Clock);

    /// <summary>La persistencia y el ciclo de vida comunes de las liquidaciones, con el usuario indicado.</summary>
    public IngenIA365ERP.Application.Payroll.Settlements.Common.SettlementRunPersister Persistidor(ICurrentUserService? quien = null) =>
        new(Db, Clock, quien ?? User);

    public IngenIA365ERP.Application.Payroll.Settlements.Common.SettlementRunWorkflow Flujo(ICurrentUserService quien) =>
        new(Db, ContabilizadorDeLiquidaciones(quien), Policies, Clock, quien,
            new PayrollAuditEmitter(Audit, quien, Clock, NullLogger<PayrollAuditEmitter>.Instance));
}
