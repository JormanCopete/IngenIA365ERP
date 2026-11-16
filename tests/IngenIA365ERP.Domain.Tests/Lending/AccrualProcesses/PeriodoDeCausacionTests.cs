using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;

namespace IngenIA365ERP.Domain.Tests.Lending.AccrualProcesses;

/// <summary>
/// Feature 024, fase 3 (2026-11-16): el rango que propone cada periodicidad del proceso de causación —mensual, quincenal (primera o segunda), decadal,
/// semanal y diaria—, lo que <see cref="PeriodoDeCausacion.Validar"/> rechaza y las transiciones del proceso (<see cref="AccrualProcess"/>).
/// </summary>
public class PeriodoDeCausacionTests
{
    private static readonly DateOnly Hoy = new(2027, 3, 31);

    [Theory]
    [InlineData(AccrualProcessPeriodicity.Monthly, "2026-11-17", null, "2026-11-01", "2026-11-30")]
    [InlineData(AccrualProcessPeriodicity.Biweekly, "2026-11-17", null, "2026-11-16", "2026-11-30")]
    [InlineData(AccrualProcessPeriodicity.Biweekly, "2026-11-17", (byte)1, "2026-11-01", "2026-11-15")]
    [InlineData(AccrualProcessPeriodicity.Biweekly, "2027-02-03", (byte)2, "2027-02-16", "2027-02-28")]
    [InlineData(AccrualProcessPeriodicity.TenDay, "2026-12-31", null, "2026-12-21", "2026-12-31")]
    [InlineData(AccrualProcessPeriodicity.TenDay, "2026-12-05", (byte)2, "2026-12-11", "2026-12-20")]
    [InlineData(AccrualProcessPeriodicity.Weekly, "2026-11-19", null, "2026-11-16", "2026-11-22")]
    [InlineData(AccrualProcessPeriodicity.Weekly, "2026-11-22", null, "2026-11-16", "2026-11-22")]
    [InlineData(AccrualProcessPeriodicity.Daily, "2026-11-19", null, "2026-11-19", "2026-11-19")]
    public void Cada_periodicidad_propone_su_rango(AccrualProcessPeriodicity periodicidad, string referencia, byte? subperiodo, string desde, string hasta)
    {
        var (d, h) = PeriodoDeCausacion.Proponer(periodicidad, DateOnly.Parse(referencia), subperiodo);

        d.Should().Be(DateOnly.Parse(desde));
        h.Should().Be(DateOnly.Parse(hasta));
        PeriodoDeCausacion.Validar(periodicidad, subperiodo, d, h, Hoy).Should().BeNull("lo propuesto siempre es válido");
    }

    [Fact]
    public void Validar_rechaza_futuro_largo_de_mas_cruce_de_mes_y_subperiodo_que_no_es_de_la_periodicidad()
    {
        var mes = AccrualProcessPeriodicity.Monthly;
        PeriodoDeCausacion.Validar(mes, null, new DateOnly(2027, 4, 1), new DateOnly(2027, 4, 1), Hoy)!.Mensaje.Should().Contain("hoy");
        PeriodoDeCausacion.Validar(AccrualProcessPeriodicity.Weekly, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 8), Hoy)!.Codigo
            .Should().Be(PeriodoDeCausacion.CodigoRangoInvalido);
        PeriodoDeCausacion.Validar(mes, null, new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 5), Hoy)!.Mensaje.Should().Contain("mismo mes");
        PeriodoDeCausacion.Validar(mes, 1, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), Hoy)!.Mensaje.Should().Contain("quincena");
        PeriodoDeCausacion.Validar(AccrualProcessPeriodicity.Biweekly, 3, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 15), Hoy)
            .Should().NotBeNull();
        PeriodoDeCausacion.Validar(AccrualProcessPeriodicity.Daily, null, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 1), Hoy)
            .Should().NotBeNull("termina antes de empezar");
        PeriodoDeCausacion.Validar(AccrualProcessPeriodicity.Weekly, null, new DateOnly(2026, 11, 28), new DateOnly(2026, 12, 4), Hoy)
            .Should().BeNull("una semana puede cruzar el mes");
    }

    [Fact]
    public void El_proceso_pasa_de_borrador_a_firme_con_aprobacion_y_uno_en_firme_no_se_anula()
    {
        var p = new AccrualProcess
        {
            Number = "PCA-000001", Periodicity = AccrualProcessPeriodicity.Monthly, DateFrom = new DateOnly(2026, 11, 1), DateTo = new DateOnly(2026, 11, 30),
            Concepts = AccrualProcessConcepts.CurrentInterest | AccrualProcessConcepts.DefaultInterest, CreatedByUserId = 7,
        };
        p.SellarCalculo(new string('a', 64), "{}", false, 3, 2, 1, 100m, 10m, 7, DateTime.UtcNow);
        p.PedirAprobacion("Cierre de noviembre", Guid.NewGuid(), 7, DateTime.UtcNow);
        p.Status.Should().Be(AccrualProcessStatus.PendingApproval);

        // Recalcular vuelve a borrador (lo aprobado cambió).
        p.SellarCalculo(new string('b', 64), "{}", false, 3, 2, 1, 100m, 10m, 7, DateTime.UtcNow);
        p.Status.Should().Be(AccrualProcessStatus.Draft);
        p.ApprovalRequestPublicId.Should().BeNull();
        p.CalculationCount.Should().Be(2);

        var solicitud = Guid.NewGuid();
        p.PedirAprobacion("Cierre de noviembre", solicitud, 7, DateTime.UtcNow);
        p.PonerEnFirme(9, "aprobador", ApprovalMethod.InPersonPasskey, Guid.NewGuid(), 12, DateTime.UtcNow);
        p.Status.Should().Be(AccrualProcessStatus.Confirmed);
        p.ExigirAbierto()!.Codigo.Should().Be(AccrualProcess.CodigoNoBorrador);
        p.Invoking(x => x.Anular("ya no", 7, DateTime.UtcNow)).Should().Throw<ReglaDeCarteraException>();
        p.MarcarReprocesado(2);
        p.Status.Should().Be(AccrualProcessStatus.Reprocessed);

        p.CompartenConcepto(AccrualProcessConcepts.DefaultInterest).Should().BeTrue();
        p.CompartenConcepto(AccrualProcessConcepts.Insurance).Should().BeFalse();
        p.SeCruzaCon(new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 15)).Should().BeTrue();
        p.SeCruzaCon(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 15)).Should().BeFalse();
    }
}
