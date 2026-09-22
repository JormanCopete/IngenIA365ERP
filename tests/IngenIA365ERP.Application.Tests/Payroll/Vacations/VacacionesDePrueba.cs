using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Settlements.Vacation;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Vacations;

/// <summary>
/// Lo que las pruebas de US4 comparten (feature 010): el escenario de <see cref="NominaTestData"/>
/// con el calendario de festivos sembrado y los servicios de vacaciones armados sobre él.
/// Ana ingresó el 15-01-2025: al 14-07-2026 lleva 540 días comerciales → 22,5 hábiles causados.
/// </summary>
public static class VacacionesDePrueba
{
    /// <summary>Fecha en que Ana cumple 540 días (18 meses comerciales) desde su ingreso del 15-01-2025.</summary>
    public static readonly DateOnly CorteDe540Dias = new(2026, 7, 14);

    public static NominaTestData Escenario(DateTime? hoy = null)
    {
        var d = new NominaTestData();
        d.Db.Holidays.AddRange(HolidaysSeeder.Catalogo());
        d.Db.SaveChanges();
        d.HoyEs(hoy ?? CorteDe540Dias.ToDateTime(new TimeOnly(12, 0)));
        return d;
    }

    public static VacationBalanceCalculator Calculador(NominaTestData d) => new(d.Db);

    public static VacationNoveltyPlanner Planificador(NominaTestData d, ICurrentUserService? quien = null) =>
        new(d.Db, d.Policies, d.StaleMarker, new CarryOverNoveltiesService(d.Db, d.Clock, quien ?? d.User), d.Clock, quien ?? d.User);

    public static CalculateVacationCommandHandler Registrar(NominaTestData d, ICurrentUserService? quien = null)
    {
        var u = quien ?? d.User;
        return new CalculateVacationCommandHandler(d.Db, Calculador(d), Planificador(d, u), d.Policies, d.SettlementLoader, d.Persistidor(u), d.Clock, u,
            new PayrollAuditEmitter(d.Audit, u, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));
    }

    public static RecalculateVacationCommandHandler Recalcular(NominaTestData d, ICurrentUserService? quien = null)
    {
        var u = quien ?? d.User;
        return new RecalculateVacationCommandHandler(d.Db, Planificador(d, u), d.SettlementLoader, d.Persistidor(u),
            new PayrollAuditEmitter(d.Audit, u, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));
    }

    public static ApproveVacationCommandHandler Aprobar(NominaTestData d, ICurrentUserService quien) =>
        new(d.Db, d.Flujo(quien), Planificador(d, quien), d.Policies, d.Clock, quien);

    public static ReverseVacationCommandHandler Reversar(NominaTestData d, ICurrentUserService quien) =>
        new(d.Db, d.Flujo(quien), Planificador(d, quien), d.Clock, quien);

    public static DiscardVacationCommandHandler Descartar(NominaTestData d, ICurrentUserService? quien = null)
    {
        var u = quien ?? d.User;
        return new DiscardVacationCommandHandler(d.Db, d.Flujo(u), Planificador(d, u), d.Clock, u,
            new PayrollAuditEmitter(d.Audit, u, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));
    }

    public static CancelVacationMovementCommandHandler Cancelar(NominaTestData d, ICurrentUserService? quien = null)
    {
        var u = quien ?? d.User;
        return new CancelVacationMovementCommandHandler(d.Db, d.Flujo(u), Planificador(d, u), d.Clock, u,
            new PayrollAuditEmitter(d.Audit, u, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));
    }

    /// <summary>Una suspensión del contrato (informativa que reduce días) con fechas, registrada en el período dado.</summary>
    public static PayrollNovelty Suspension(NominaTestData d, Employee e, PayPeriod periodo, DateTime desde, DateTime hasta)
    {
        var def = d.Db.PayrollConceptDefinitions.First(c => c.Code == "SUSPENSION");
        var n = new PayrollNovelty
        {
            PayPeriodId = periodo.Id, EmployeeId = e.Id, ConceptDefinitionId = def.Id, ConceptCode = def.Code,
            StartDate = desde, EndDate = hasta, DaysInPeriod = 0, Status = NoveltyStatus.Active, Origin = NoveltyOrigin.Manual, CreatedBy = "test",
        };
        d.Db.PayrollNovelties.Add(n);
        d.Db.SaveChanges();
        return n;
    }

    public static CalculateVacationCommand Disfrute(Employee e, DateOnly desde, DateOnly hasta, bool aceptarRetroactivo = false) =>
        new(e.PublicId, VacationMovementKind.Enjoyment, desde, hasta, AcceptRetroactive: aceptarRetroactivo);

    public static CalculateVacationCommand Compensacion(Employee e, decimal dias) =>
        new(e.PublicId, VacationMovementKind.Compensation, CompensationDays: dias);
}
