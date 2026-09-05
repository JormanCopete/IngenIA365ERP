using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
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
    public PayrollAccountingPoster Poster { get; }

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
        Poster = new PayrollAccountingPoster(Db, Clock, User);

        Plan = new PayrollPlan { Code = "DEFAULT", Name = "Nómina general", Periodicity = PayrollPeriodicity.Monthly, IsDefault = true, IsActive = true, CreatedBy = "system:seed" };
        Db.PayrollPlans.Add(Plan);
        Db.PayrollConceptDefinitions.AddRange(PayrollConceptDefinitionsSeeder.Catalogo());
        Db.PayrollLegalParameters.AddRange(PayrollLegalParametersSeeder.Catalogo());
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

    public PayPeriod Periodo(DateTime desde, DateTime hasta, PayPeriodStatus estado, int planilla = 0)
    {
        var p = new PayPeriod
        {
            PlanId = planilla == 0 ? Db.PayPeriods.Count() + 1 : planilla,
            PayrollCompanyId = 1,
            PayrollPlanId = Plan.Id,
            Description = $"{desde:MMMM yyyy}",
            StartDate = desde,
            EndDate = hasta,
            Periodicity = 30,
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

    public Employee Empleado(string nombre, decimal salario, DateTime ingreso, EmployeeClass clase = EmployeeClass.Standard,
        DateTime? retiro = null, byte procedimientoRetencion = 1)
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
            WorkRiskRateId = 1,
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

    /// <summary>Lo que la aprobación necesita: sucursal, centro de costo, comprobante NM, período contable abierto y cuentas para todos los conceptos de la semilla.</summary>
    public void ConfigurarContabilidad(bool periodoContableAbierto = true)
    {
        Db.Branches.Add(new Branch { Name = "Principal", CreatedBy = "test" });
        Db.CostCenters.Add(new CostCenter { LegacyCode = "01", Name = "Administración", CreatedBy = "test" });
        Db.VoucherTypes.Add(new VoucherType { Code = "NM", Name = "Nómina", ModuleCode = "NOM", UpdatesAccounting = true, NextSequenceNumber = 0, CreatedBy = "test" });
        Db.AccountingPeriods.Add(new AccountingPeriod
        {
            ModuleCode = "CNT", Year = 2026, PeriodNumber = 3, StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2026, 3, 31),
            Status = periodoContableAbierto ? "O" : "C", CreatedBy = "test",
        });
        var i = 0;
        foreach (var code in Db.PayrollConceptDefinitions.Select(c => c.Code).Distinct().ToList())
        {
            i++;
            Db.PayrollConceptDefinitionAccounts.Add(new PayrollConceptDefinitionAccount
            {
                ConceptCode = code, CostCenterId = null, DebitAccountId = 1000 + i, CreditAccountId = 2000 + i, CreatedBy = "test",
            });
        }
        Db.SaveChanges();
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
