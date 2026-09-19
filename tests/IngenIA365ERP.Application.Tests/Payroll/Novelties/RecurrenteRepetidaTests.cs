using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Novelties.CancelNovelty;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>
/// 2026-09-19: en producción una deducción fija quedó registrada dos veces como recurrente (ocho minutos
/// de diferencia), cada cálculo generaba las dos novedades, y anular una no servía porque el siguiente
/// cálculo la volvía a crear. La misma orden no se registra dos veces, el cálculo materializa una sola y
/// avisa de la sobrante, y una novedad anulada de una recurrente no vuelve a nacer en ese período.
/// </summary>
public class RecurrenteRepetidaTests
{
    private static CalculatePayrollRunCommandHandler Calculador(NominaTestData d) =>
        new(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);

    private static CreateRecurringNoveltyCommandHandler Registrador(NominaTestData d) => new(d.Db, d.Clock, d.User, d.StaleMarker);

    private static async Task<Guid> Crear(NominaTestData d, Employee e, string concepto, decimal valor, DateTime desde, DateTime? hasta = null)
    {
        var r = await Registrador(d).Handle(new CreateRecurringNoveltyCommand(e.PublicId, concepto, null, valor, desde, hasta, null, null), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    /// <summary>Concepto propio de la cooperativa que exige valor y NO admite repetirse en el período (un seguro de vida).</summary>
    private static void ConceptoSinRepeticion(NominaTestData d)
    {
        d.Db.PayrollConceptDefinitions.Add(new PayrollConceptDefinition
        {
            Code = "SEGURO_VIDA", Name = "Seguro de vida", Nature = ConceptNature.Deduction, CalculationKind = CalculationKind.FixedAmount,
            ApplicableClasses = int.MaxValue, Origin = ConceptOrigin.Custom, ValidFrom = new DateTime(2026, 1, 1), IsActive = true,
            RequiresAmount = true, AllowsRepeatInPeriod = false, CreatedBy = "test",
        });
        d.Db.SaveChanges();
    }

    [Fact]
    public async Task No_se_registra_dos_veces_la_misma_recurrente()
    {
        var d = new NominaTestData();
        var primera = await Crear(d, d.Ana, "LIBRANZA", 13_557m, new DateTime(2026, 8, 1));

        // Idéntica y vigente: la misma orden. LIBRANZA admite repetirse en el período, pero no con el mismo valor.
        var repetida = await Registrador(d).Handle(
            new CreateRecurringNoveltyCommand(d.Ana.PublicId, "LIBRANZA", null, 13_557m, new DateTime(2026, 8, 1), null, null, null), CancellationToken.None);

        repetida.IsFailure.Should().BeTrue();
        repetida.Error.Code.Should().Be("Payroll.RecurringNoveltyDuplicate");
        repetida.Error.Message.Should().Contain(13_557m.ToString("N0")).And.Contain($"existingPublicId={primera}").And.Contain("desactive la anterior");
        (await d.Db.PayrollRecurringNovelties.CountAsync(r => r.EmployeeId == d.Ana.Id)).Should().Be(1);

        // Otra vigencia que se cruza pero otro valor sí entra: dos libranzas con dos entidades.
        (await Registrador(d).Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "LIBRANZA", null, 80_000m, new DateTime(2026, 9, 1), null, null, null), CancellationToken.None))
            .IsSuccess.Should().BeTrue("el concepto admite repetirse y el valor es otro");

        // Y una que ya terminó no compite con la nueva.
        var bruno = d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        await Crear(d, bruno, "LIBRANZA", 50_000m, new DateTime(2026, 1, 1), new DateTime(2026, 2, 28));
        (await Registrador(d).Handle(new CreateRecurringNoveltyCommand(bruno.PublicId, "LIBRANZA", null, 50_000m, new DateTime(2026, 3, 1), null, null, null), CancellationToken.None))
            .IsSuccess.Should().BeTrue("la anterior terminó el 28/02");
    }

    [Fact]
    public async Task Si_el_concepto_no_admite_repetirse_no_cabe_otra_recurrente_ni_con_otro_valor()
    {
        var d = new NominaTestData();
        ConceptoSinRepeticion(d);
        await Crear(d, d.Ana, "SEGURO_VIDA", 20_000m, new DateTime(2026, 1, 1));

        var otra = await Registrador(d).Handle(
            new CreateRecurringNoveltyCommand(d.Ana.PublicId, "SEGURO_VIDA", null, 25_000m, new DateTime(2026, 3, 1), null, null, null), CancellationToken.None);

        otra.Error.Code.Should().Be("Payroll.RecurringNoveltyDuplicate");
        otra.Error.Message.Should().Contain("no admite repetirse");

        // Agotada la anterior (todas sus cuotas emitidas), la nueva sí entra.
        var vieja = await d.Db.PayrollRecurringNovelties.SingleAsync(r => r.EmployeeId == d.Ana.Id);
        vieja.TotalInstallments = 2; vieja.InstallmentsIssued = 2;
        await d.Db.SaveChangesAsync();
        (await Registrador(d).Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "SEGURO_VIDA", null, 25_000m, new DateTime(2026, 3, 1), null, null, null), CancellationToken.None))
            .IsSuccess.Should().BeTrue("la anterior ya emitió todas sus cuotas");
    }

    [Fact]
    public async Task Anular_la_novedad_de_una_recurrente_no_la_vuelve_a_generar_al_recalcular()
    {
        var d = new NominaTestData();
        var id = await Crear(d, d.Ana, "LIBRANZA", 50_000m, new DateTime(2026, 1, 1));
        (await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var rec = await d.Db.PayrollRecurringNovelties.SingleAsync(x => x.PublicId == id);
        var novedad = await d.Db.PayrollNovelties.SingleAsync(n => n.RecurringNoveltyId == rec.Id);

        var anulada = await new CancelNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver)
            .Handle(new CancelNoveltyCommand(novedad.PublicId, "Este mes no se descuenta"), CancellationToken.None);
        anulada.IsSuccess.Should().BeTrue(anulada.Error.Message);

        // Hasta el 2026-09-19 el siguiente cálculo la creaba otra vez y anular no servía de nada.
        var calc = await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        var delPeriodo = await d.Db.PayrollNovelties.Where(n => n.RecurringNoveltyId == rec.Id && n.PayPeriodId == d.Marzo.Id).ToListAsync();
        delPeriodo.Should().ContainSingle().Which.Status.Should().Be(NoveltyStatus.Cancelled);
        var corrida = await d.Db.PayrollRuns.SingleAsync(r => r.PublicId == calc.Value.RunPublicId);
        (await d.Db.PayrollRunLines.AnyAsync(l => l.ConceptCode == "LIBRANZA" && l.RunEmployee!.PayrollRunId == corrida.Id)).Should().BeFalse("la corrida no la liquidó");

        // La recurrente sigue viva y entra en el período siguiente.
        var abril = d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        (await Calculador(d).Handle(new CalculatePayrollRunCommand(abril.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await d.Db.PayrollNovelties.CountAsync(n => n.RecurringNoveltyId == rec.Id && n.PayPeriodId == abril.Id && n.Status == NoveltyStatus.Active)).Should().Be(1);
    }

    [Fact]
    public async Task Dos_recurrentes_identicas_ya_registradas_generan_una_sola_novedad_y_el_calculo_avisa()
    {
        // Datos anteriores a la regla: la misma deducción registrada dos veces con minutos de diferencia.
        var d = new NominaTestData();
        PayrollRecurringNovelty Orden(DateTime creada) => new()
        {
            EmployeeId = d.Ana.Id, ConceptCode = "LIBRANZA", Amount = 13_557m, StartDate = new DateTime(2026, 1, 1),
            IsActive = true, CreatedAt = creada, CreatedBy = "contadora@demo",
        };
        var primera = Orden(new DateTime(2026, 9, 19, 16, 17, 0));
        var segunda = Orden(new DateTime(2026, 9, 19, 16, 25, 0));
        d.Db.PayrollRecurringNovelties.AddRange(primera, segunda);
        await d.Db.SaveChangesAsync();

        var calc = await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        var generadas = await d.Db.PayrollNovelties.Where(n => n.PayPeriodId == d.Marzo.Id && n.Origin == NoveltyOrigin.Recurring).ToListAsync();
        generadas.Should().ContainSingle().Which.RecurringNoveltyId.Should().Be(primera.Id, "entra la más antigua");
        calc.Value.Warnings.Should().ContainSingle(w => w.Contains("la misma orden") && w.Contains("16:25") && w.Contains("Desactive la sobrante"));

        // Recalcular no cambia nada: sigue una sola, y el aviso se repite mientras la sobrante viva.
        var otra = await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        (await d.Db.PayrollNovelties.CountAsync(n => n.PayPeriodId == d.Marzo.Id && n.Origin == NoveltyOrigin.Recurring)).Should().Be(1);
        otra.Value.Warnings.Should().ContainSingle(w => w.Contains("la misma orden"));
        segunda.Id.Should().NotBe(primera.Id);
    }

    [Fact]
    public async Task Una_recurrente_no_entra_si_el_concepto_no_admite_repetirse_y_ya_hay_una_novedad_activa()
    {
        var d = new NominaTestData();
        ConceptoSinRepeticion(d);
        await Crear(d, d.Ana, "SEGURO_VIDA", 20_000m, new DateTime(2026, 1, 1));
        var manual = await new RegisterNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver).Handle(new RegisterNoveltyCommand
        {
            PeriodPublicId = d.Marzo.PublicId, EmployeePublicId = d.Ana.PublicId, ConceptCode = "SEGURO_VIDA", Amount = 22_000m,
        }, CancellationToken.None);
        manual.IsSuccess.Should().BeTrue(manual.Error.Message);

        var calc = await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        (await d.Db.PayrollNovelties.CountAsync(n => n.PayPeriodId == d.Marzo.Id && n.ConceptCode == "SEGURO_VIDA" && n.Status == NoveltyStatus.Active)).Should().Be(1, "la digitada");
        (await d.Db.PayrollNovelties.AnyAsync(n => n.PayPeriodId == d.Marzo.Id && n.Origin == NoveltyOrigin.Recurring)).Should().BeFalse();
        calc.Value.Warnings.Should().ContainSingle(w => w.Contains("Seguro de vida") && w.Contains("no admite repetirse"));
    }
}
