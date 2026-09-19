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
        AuditEmitter = new PayrollAuditEmitter(Audit, User, Clock, NullLogger<PayrollAuditEmitter>.Instance);
        Policies = new PayrollPolicyReader(Db);
        Loader = new CalculationInputLoader(Db, Policies);
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
}
