using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
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
/// 2026) y un empleado estándar. Los servicios reales que no hacen IO externo
/// (marcador de Stale, traslados) se usan de verdad; reloj y usuario son sustitutos.
/// </summary>
public sealed class NominaTestData
{
    public TestApplicationDbContext Db { get; }
    public IDateTimeService Clock { get; }
    public ICurrentUserService User { get; }
    public IAuditAppendOnlyWriter Audit { get; }
    public IPayrollRunStaleMarker StaleMarker { get; }
    public CarryOverNoveltiesService CarryOver { get; }
    public PayrollAuditEmitter AuditEmitter { get; }

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
        User = Substitute.For<ICurrentUserService>();
        User.UserName.Returns("ana@demo");
        User.UserId.Returns(7);
        User.TenantId.Returns("1");
        User.IsAuthenticated.Returns(true);
        Audit = Substitute.For<IAuditAppendOnlyWriter>();

        StaleMarker = new PayrollRunStaleMarker(Db, NullLogger<PayrollRunStaleMarker>.Instance);
        CarryOver = new CarryOverNoveltiesService(Db, Clock, User);
        AuditEmitter = new PayrollAuditEmitter(Audit, User, Clock, NullLogger<PayrollAuditEmitter>.Instance);

        Plan = new PayrollPlan { Code = "DEFAULT", Name = "Nómina general", Periodicity = PayrollPeriodicity.Monthly, IsDefault = true, IsActive = true, CreatedBy = "system:seed" };
        Db.PayrollPlans.Add(Plan);
        Db.PayrollConceptDefinitions.AddRange(PayrollConceptDefinitionsSeeder.Catalogo());
        Db.PayrollLegalParameters.AddRange(PayrollLegalParametersSeeder.Catalogo());
        Db.SaveChanges();

        Marzo = Periodo(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31), PayPeriodStatus.Open);
        Ana = Empleado("Ana", 2_000_000m, new DateTime(2025, 1, 15));
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

    public Employee Empleado(string nombre, decimal salario, DateTime ingreso, EmployeeClass clase = EmployeeClass.Standard, DateTime? retiro = null)
    {
        var e = new Employee
        {
            PersonId = Db.Employees.Count() + 100,
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
            WithholdingProcedure = 1,
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
}
