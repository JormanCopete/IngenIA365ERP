using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.ServiceBonus;

/// <summary>
/// El escenario del «Independent Test» de la US1 (tasks.md, quickstart §3.1) sobre <see cref="NominaTestData"/>:
/// <b>A</b> con 2.000.000 fijo todo el semestre (caso dorado 01: 1.124.547,50), <b>B</b> con ingreso el
/// 15-09-2026, 1.750.905 hasta el 31-10 y 2.000.000 desde el 01-11 (caso 02: 630.404,72), <b>C</b> con
/// salario integral, <b>D</b> aprendiz en etapa lectiva, <b>P</b> pasante y <b>E</b> retirado el 30-09 con
/// definitiva aprobada que ya le pagó la prima (caso 17). Los handlers se arman con las dependencias
/// del escenario, como lo haría el contenedor.
/// </summary>
public static class PrimaDePrueba
{
    public sealed class Escenario
    {
        public required NominaTestData D { get; init; }
        public required Employee A { get; init; }
        public required Employee B { get; init; }
        public required Employee C { get; init; }
        public required Employee Aprendiz { get; init; }
        public required Employee Pasante { get; init; }
        public required Employee E { get; init; }
        public required PayrollRun DefinitivaDeE { get; init; }
    }

    /// <summary>
    /// La cooperativa redondea al centavo (<c>Payroll.Rounding = Centavo</c>), como los casos dorados: así
    /// 1.124.547,50 y 630.404,72 salen al peso con centavos. Con el defecto (<c>Peso</c>) serían 1.124.548 y 630.405.
    /// </summary>
    public static void RedondearAlCentavo(NominaTestData d)
    {
        d.Db.SystemSettings.Add(new Domain.Entities.Core.SystemSetting
        {
            SettingKey = PayrollPolicyReader.RoundingKey, SettingValue = PayrollRounding.Centavo.ToString(), ValueType = "String", ModulePrefix = "PAY", CreatedBy = "test",
        });
        d.Db.SaveChanges();
    }

    /// <summary>Segundo semestre de 2026, con el reloj ya en enero de 2027 para poder aprobar al corte (31-12).</summary>
    public static Escenario SegundoSemestre2026()
    {
        var d = new NominaTestData();
        RedondearAlCentavo(d);
        // Ana (la de la semilla) ingresó en 2025 y también liquida; no estorba a los valores de A y B.
        var a = d.Empleado("Alba", 2_000_000m, new DateTime(2020, 3, 1));
        var b = d.Empleado("Bruno", 1_750_905m, new DateTime(2026, 9, 15));
        d.CambioDeSalario(b, new DateTime(2026, 11, 1), 2_000_000m);
        var c = d.Empleado("Celia", 20_000_000m, new DateTime(2019, 1, 10), EmployeeClass.IntegralSalary);
        var aprendiz = d.Empleado("Dario", 1_000_000m, new DateTime(2026, 8, 1), EmployeeClass.Apprentice);
        aprendiz.ApprenticeStage = ApprenticeStage.Lective;
        var pasante = d.Empleado("Paula", 1_000_000m, new DateTime(2026, 8, 1), EmployeeClass.Intern);
        pasante.ApprenticeStage = ApprenticeStage.Practical;
        d.Db.SaveChanges();

        var e = d.Empleado("Eva", 2_000_000m, new DateTime(2020, 3, 1), retiro: new DateTime(2026, 9, 30));
        var definitiva = DefinitivaAprobada(d, e, new DateOnly(2026, 9, 30), primaPagada: 562_273.75m, dias: 90);

        d.HoyEs(new DateTime(2027, 1, 5, 12, 0, 0));
        return new Escenario { D = d, A = a, B = b, C = c, Aprendiz = aprendiz, Pasante = pasante, E = e, DefinitivaDeE = definitiva };
    }

    /// <summary>Una terminación liquidada y su corrida <c>Settlement</c> aprobada con la línea <c>PRIMA</c> que ya pagó (FR-009).</summary>
    public static PayrollRun DefinitivaAprobada(NominaTestData d, Employee e, DateOnly retiro, decimal primaPagada, int dias)
    {
        var motivo = d.Db.TerminationReasons.FirstOrDefault(r => r.Code == "RENUNCIA");
        if (motivo is null)
        {
            motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia voluntaria", GeneratesSeverancePay = false, IsSeeded = true, CreatedBy = "test" };
            d.Db.TerminationReasons.Add(motivo);
            d.Db.SaveChanges();
        }
        d.Db.EmploymentTerminations.Add(new EmploymentTermination
        {
            EmployeeId = e.Id, TerminationDate = retiro, TerminationReasonId = motivo.Id, Status = TerminationStatus.Settled, CreatedBy = "test",
        });

        var definicion = d.Db.PayrollConceptDefinitions.Single(c => c.Code == "PRIMA");
        var run = new PayrollRun
        {
            Kind = PayrollRunKind.Settlement, PayPeriodId = null, CutoffDate = retiro, EmployeeId = e.Id, Version = 1, Status = PayrollRunStatus.Approved,
            CalculatedAt = NominaTestData.Ahora, CalculatedBy = "ana@demo", ApprovedAt = NominaTestData.Ahora, ApprovedBy = "contadora@demo",
            InputsHash = new string('c', 64), CreatedBy = "test", EmployeeCount = 1, TotalEarnings = primaPagada, TotalNet = primaPagada,
        };
        var fila = new PayrollRunEmployee { EmployeeId = e.Id, PayrollPlanId = d.Plan.Id, DaysWorked = dias, EmployeeClass = e.EmployeeClass, TotalEarnings = primaPagada, NetPay = primaPagada, CreatedBy = "test" };
        fila.Lines.Add(new PayrollRunLine
        {
            ConceptDefinitionId = definicion.Id, ConceptCode = definicion.Code, ConceptName = definicion.Name, Nature = ConceptNature.Earning,
            Amount = primaPagada, Quantity = dias, AffectsAccounting = true, Order = 1, ExplanationJson = "{}", CreatedBy = "test",
        });
        run.Employees.Add(fila);
        d.Db.PayrollRuns.Add(run);
        d.Db.SaveChanges();
        return run;
    }

    // ------------------------------------------------------------------ handlers --

    public static CalculateServiceBonusCommandHandler Calcular(NominaTestData d, ICurrentUserService? quien = null) =>
        new(d.Db, d.SettlementLoader, d.Persistidor(quien), d.Lock, quien ?? d.User, Emisor(d, quien), NullLogger<CalculateServiceBonusCommandHandler>.Instance);

    public static RecalculateServiceBonusCommandHandler Recalcular(NominaTestData d, ICurrentUserService? quien = null) =>
        new(d.Db, d.SettlementLoader, d.Persistidor(quien), d.Lock, quien ?? d.User, Emisor(d, quien), NullLogger<RecalculateServiceBonusCommandHandler>.Instance);

    public static GetServiceBonusExclusionsQueryHandler Excluidos(NominaTestData d) => new(d.Db, d.SettlementLoader, NullLogger<GetServiceBonusExclusionsQueryHandler>.Instance);

    public static ApproveServiceBonusCommandHandler Aprobar(NominaTestData d, ICurrentUserService quien) => new(d.Flujo(quien));

    public static ReverseServiceBonusCommandHandler Reversar(NominaTestData d, ICurrentUserService quien) => new(d.Flujo(quien));

    public static DiscardServiceBonusCommandHandler Descartar(NominaTestData d, ICurrentUserService quien) => new(d.Flujo(quien));

    public static ListServiceBonusRunsQueryHandler Listar(NominaTestData d) => new(d.Db);

    private static PayrollAuditEmitter Emisor(NominaTestData d, ICurrentUserService? quien) =>
        quien is null ? d.AuditEmitter : new PayrollAuditEmitter(d.Audit, quien, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);

    /// <summary>La línea de un concepto en la corrida para un empleado.</summary>
    public static async Task<PayrollRunLine?> LineaAsync(NominaTestData d, Guid runPublicId, Employee e, string code)
    {
        var run = await d.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.PublicId == runPublicId);
        return await (
            from l in d.Db.PayrollRunLines.AsNoTracking()
            join re in d.Db.PayrollRunEmployees.AsNoTracking() on l.PayrollRunEmployeeId equals re.Id
            where re.PayrollRunId == run.Id && re.EmployeeId == e.Id && l.ConceptCode == code
            select l).FirstOrDefaultAsync();
    }
}
