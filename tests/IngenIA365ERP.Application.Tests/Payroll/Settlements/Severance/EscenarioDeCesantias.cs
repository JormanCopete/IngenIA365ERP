using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Severance;

/// <summary>
/// El escenario de la US2 (tasks.md, «Independent Test»; quickstart §3.2) sobre <see cref="NominaTestData"/>:
/// <b>A</b> 2.500.000 estable todo 2026 con auxilio; <b>F</b> 2.000.000 hasta el 31-10 y 2.400.000 desde el
/// 01-11 (cambio dentro de la ventana de estabilidad); <b>G</b> ingresado el 01-09-2026 con el mínimo;
/// <b>C</b> con salario integral; <b>E</b> retirado el 31-10-2026 con definitiva aprobada. A y G consignan en
/// Porvenir, F en Protección; C y E también tienen fondo para que la exclusión sea la del motor y no
/// la falta de fondo. El reloj queda en enero de 2027 para que el corte del 31-12-2026 sea aprobable.
/// Los valores esperados son los de los casos dorados 03, 04 y 05, con redondeo al centavo como ellos.
/// </summary>
public sealed class EscenarioDeCesantias
{
    public static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    public NominaTestData D { get; } = new();
    public Employee A { get; }
    public Employee F { get; }
    public Employee G { get; }
    public Employee C { get; }
    public Employee E { get; }
    public Person Porvenir { get; }
    public Person Proteccion { get; }
    public SeveranceProvider FondoPorvenir { get; }
    public SeveranceProvider FondoProteccion { get; }

    public EscenarioDeCesantias(bool conProvisiones = false, bool conContabilidad = false)
    {
        // Los casos dorados están al centavo; la política por defecto de la cooperativa redondea al peso.
        // Aquí se fija «Centavo» para comparar contra los mismos números de research R1.
        D.Db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = Application.Payroll.Services.PayrollPolicyReader.RoundingKey, SettingValue = nameof(PayrollRounding.Centavo),
            ValueType = "String", ModulePrefix = "PAY", CreatedBy = "test",
        });
        D.Db.SaveChanges();

        A = D.Empleado("Alba", 2_500_000m, new DateTime(2024, 1, 1));
        F = D.Empleado("Fabio", 2_000_000m, new DateTime(2024, 1, 1));
        D.CambioDeSalario(F, new DateTime(2026, 11, 1), 2_400_000m);
        G = D.Empleado("Gema", 1_750_905m, new DateTime(2026, 9, 1));
        C = D.Empleado("Celso", 20_000_000m, new DateTime(2024, 1, 1), EmployeeClass.IntegralSalary);
        E = D.Empleado("Elena", 2_000_000m, new DateTime(2024, 1, 1), retiro: new DateTime(2026, 10, 31));

        Porvenir = D.FondoDeCesantiasConPersona(A, "Porvenir");
        FondoPorvenir = D.Db.SeveranceProviders.Single(f => f.Name == "Porvenir");
        Proteccion = D.FondoDeCesantiasConPersona(F, "Proteccion");
        FondoProteccion = D.Db.SeveranceProviders.Single(f => f.Name == "Proteccion");
        G.SeveranceFundId = FondoPorvenir.Id;
        C.SeveranceFundId = FondoPorvenir.Id;
        E.SeveranceFundId = FondoProteccion.Id;
        D.Db.SaveChanges();

        // E: retirada con liquidación definitiva aprobada (la corrida Settlement no hace falta para la exclusión: la terminación Settled basta).
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia voluntaria", GeneratesSeverancePay = false, IsSeeded = true, CreatedBy = "test" };
        D.Db.TerminationReasons.Add(motivo);
        D.Db.SaveChanges();
        D.Db.EmploymentTerminations.Add(new EmploymentTermination
        {
            EmployeeId = E.Id, TerminationDate = new DateOnly(2026, 10, 31), TerminationReasonId = motivo.Id, Status = TerminationStatus.Settled, CreatedBy = "test",
        });
        D.Db.SaveChanges();

        if (conProvisiones)
        {
            // Doce meses aprobados de A con provisión de cesantías e intereses: 12 × 229.091 = 2.749.092 (la liquidación da 2.749.095, diferencia +3 al gasto)
            // y 12 × 27.491 = 329.892 de intereses (la liquidación da 329.891,40: liberación de 0,60).
            for (var mes = 1; mes <= 12; mes++)
                D.MesAprobado(2026, mes, A, 2_500_000m, 249_095m, provPrima: 208_333m, provCesantias: 229_091m, provIntereses: 27_491m, provVacaciones: 104_167m);
        }

        if (conContabilidad)
        {
            D.ConfigurarContabilidad();
            D.PeriodoContable(2026, 12);
            D.PeriodoContable(2027, 1);
        }

        D.HoyEs(new DateTime(2027, 1, 15, 10, 0, 0));
    }

    public IReadOnlyList<Guid> Todos => [A.PublicId, F.PublicId, G.PublicId, C.PublicId, E.PublicId];

    public CalculateSeveranceCommandHandler Calculador(ICurrentUserService? quien = null) =>
        new(D.Db, D.SettlementLoader, D.Persistidor(quien), D.Lock, quien ?? D.User, D.AuditEmitter);

    public RecalculateSeveranceCommandHandler Recalculador(ICurrentUserService? quien = null) =>
        new(D.Db, D.SettlementLoader, D.Persistidor(quien), D.Lock, quien ?? D.User, D.AuditEmitter);

    public ApproveSeveranceCommandHandler Aprobador(ICurrentUserService? quien = null) => new(D.Flujo(quien ?? Contadora));
    public ReverseSeveranceCommandHandler Reversor(ICurrentUserService? quien = null) => new(D.Flujo(quien ?? Contadora));
    public DiscardSeveranceCommandHandler Descartador(ICurrentUserService? quien = null) => new(D.Flujo(quien ?? Contadora));
    public MarkFundDepositedCommandHandler Marcador(ICurrentUserService? quien = null) => new(D.Db, D.Clock, quien ?? Contadora, D.AuditEmitter);
    public GetDepositScheduleQueryHandler Relacion() => new(D.Db);
    public ListSeveranceRunsQueryHandler Lista() => new(D.Db);

    public Task<Result<SettlementCalculatedDto>> CalcularAsync(IReadOnlyList<Guid>? empleados = null, DateOnly? corte = null) =>
        Calculador().Handle(new CalculateSeveranceCommand(2026, corte, empleados ?? Todos), CancellationToken.None);

    public Task<Result<SettlementApprovedDto>> AprobarAsync(Guid runId, DateOnly? payDate = null) =>
        Aprobador().Handle(new ApproveSeveranceCommand(runId, Confirm: true, PayDate: payDate), CancellationToken.None);
}
