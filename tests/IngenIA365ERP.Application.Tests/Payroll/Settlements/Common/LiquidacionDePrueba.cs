using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;

/// <summary>
/// El camino completo de una liquidación especial en pruebas: cargar (<see cref="SettlementInputLoader"/>),
/// calcular (<see cref="SettlementCalculationEngine"/>, puro) y persistir el borrador
/// (<see cref="SettlementRunPersister"/>). Es lo que los comandos por tipo hacen; aquí sirve para que
/// el contabilizador y el ciclo de vida se prueben sobre una corrida real, no armada a mano.
/// </summary>
public static class LiquidacionDePrueba
{
    /// <summary>Seis meses aprobados de 2026 para Ana, con provisiones, y el reloj en julio: lo que una prima del primer semestre necesita.</summary>
    public static NominaTestData ConPrimerSemestre(decimal provPrimaMensual = 187_424m)
    {
        var d = new NominaTestData();
        for (var mes = 1; mes <= 6; mes++)
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: provPrimaMensual, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m);
        d.HoyEs(new DateTime(2026, 7, 10, 12, 0, 0));
        return d;
    }

    /// <summary>Contabilidad iniciada con cuentas para todos los conceptos y los períodos contables de junio y julio de 2026.</summary>
    public static void ConContabilidad(NominaTestData d)
    {
        d.ConfigurarContabilidad();
        d.PeriodoContable(2026, 6);
        d.PeriodoContable(2026, 7);
    }

    /// <summary>Calcula y persiste el borrador de una liquidación; devuelve la corrida ya guardada.</summary>
    public static async Task<PayrollRun> CalcularAsync(NominaTestData d, SettlementLoadRequest request, SettlementRunKey key, bool recalculo = false,
        Domain.Entities.Payroll.EmploymentTermination? terminacion = null)
    {
        var batch = await d.SettlementLoader.LoadAsync(request, CancellationToken.None);
        batch.MissingRequiredParameters.Should().BeEmpty();
        var engine = new SettlementCalculationEngine();
        var calculados = batch.Employees.Select(e => new SettlementCalculatedEmployee(e, engine.Calculate(e.Input))).ToList();

        var persistidor = d.Persistidor();
        var anteriores = await persistidor.CorridasDeAsync(key, CancellationToken.None);
        SettlementRunPersister.Duplicado(anteriores, key, recalculo).Should().BeNull();
        var run = persistidor.CrearBorrador(batch, key, calculados, anteriores, terminationId: terminacion?.Id);
        await d.Db.SaveChangesAsync();
        return run;
    }

    public static Task<PayrollRun> PrimaAsync(NominaTestData d, bool recalculo = false) =>
        CalcularAsync(d, SettlementLoadRequest.Prima(2026, 1), SettlementRunKey.Prima(2026, 1), recalculo);

    public static Task<PayrollRun> CesantiasAsync(NominaTestData d, bool recalculo = false) =>
        CalcularAsync(d, SettlementLoadRequest.Cesantias(2026, new DateOnly(2026, 6, 30)), SettlementRunKey.Cesantias(2026, new DateOnly(2026, 6, 30)), recalculo);

    /// <summary>Las filas de la corrida con sus líneas y su ficha, como las recibe el contabilizador.</summary>
    public static async Task<List<(PayrollRunEmployee RunEmployee, Domain.Entities.Payroll.Employee Employee, IReadOnlyList<PayrollRunLine> Lines)>> FilasAsync(NominaTestData d, PayrollRun run)
    {
        var filas = await d.Db.PayrollRunEmployees.Include(e => e.Lines).Include(e => e.Employee).Where(e => e.PayrollRunId == run.Id).ToListAsync();
        return filas.Select(e => (e, e.Employee!, (IReadOnlyList<PayrollRunLine>)e.Lines.ToList())).ToList();
    }
}
