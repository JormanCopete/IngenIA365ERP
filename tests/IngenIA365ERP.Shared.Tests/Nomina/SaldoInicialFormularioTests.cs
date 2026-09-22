using FluentAssertions;
using IngenIA365ERP.Shared.Models.Nomina;
using IngenIA365ERP.Shared.Services.Nomina;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Nomina;

/// <summary>
/// Feature 010, revisión de N1 (pantallas #1): editar un saldo inicial existente arranca con TODO
/// lo guardado. El <c>PUT</c> reemplaza la fila (contracts/api.md §4), así que un formulario armado
/// desde el resumen —que no trae los «días ya contados» ni las notas— los mandaba en nulo y el motor
/// volvía a contar los días por calendario.
/// </summary>
public class SaldoInicialFormularioTests
{
    private static SaldoInicialResumenDto Resumen(bool conSaldo = true) => new(
        Guid.NewGuid(), "Ana Pérez", "1023", new DateTime(2020, 3, 1), HiredBeforeStart: true,
        AsOfDate: conSaldo ? new DateOnly(2025, 12, 31) : null,
        PendingVacationDays: conSaldo ? 7.5m : null,
        AccruedSeverance: conSaldo ? 1_000_000m : null,
        AccruedSeveranceInterest: conSaldo ? 120_000m : null,
        AccruedServiceBonus: conSaldo ? 500_000m : null,
        ConsumedBy: [], IsEditable: true, UpdatedAt: null, UpdatedBy: null);

    private static SaldoInicialFilaDto Vigente() => new(
        Guid.NewGuid(), Kind: 1, new DateOnly(2025, 12, 31), 7.5m, 1_000_000m, 120_000m, 500_000m,
        ServiceBonusDaysAccrued: 120, SeveranceDaysAccrued: 300, Notes: "Del libro de la contadora",
        AdjustsBalancePublicId: null, AdjustmentReason: null, ConsumedBy: null,
        CreatedBy: "contadora", CreatedAt: new DateTime(2026, 1, 5), UpdatedBy: null, UpdatedAt: null);

    [Fact]
    public void Editar_un_saldo_existente_arranca_con_los_dias_ya_contados_y_las_notas_del_vigente()
    {
        var form = SaldoInicialFormulario.Desde(Resumen(), Vigente());

        form.ServiceBonusDaysAccrued.Should().Be(120);
        form.SeveranceDaysAccrued.Should().Be(300);
        form.Notes.Should().Be("Del libro de la contadora");
        form.AsOfDate.Should().Be(new DateTime(2025, 12, 31));
        form.AccruedSeverance.Should().Be(1_000_000m);
    }

    [Fact]
    public void Cambiar_solo_un_valor_y_guardar_conserva_lo_demas_en_el_PUT()
    {
        var form = SaldoInicialFormulario.Desde(Resumen(), Vigente());
        form.AccruedSeverance = 1_250_000m;

        var put = form.AGuardar();

        put.AccruedSeverance.Should().Be(1_250_000m);
        put.ServiceBonusDaysAccrued.Should().Be(120, "el PUT reemplaza la fila: lo que no viaja se pierde");
        put.SeveranceDaysAccrued.Should().Be(300);
        put.Notes.Should().Be("Del libro de la contadora");
        put.AsOfDate.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void El_ajuste_lleva_el_motivo_y_parte_del_saldo_vigente_completo()
    {
        var form = SaldoInicialFormulario.Desde(Resumen(), Vigente());
        form.Reason = "  Faltaban 15 días de prima  ";
        form.ServiceBonusDaysAccrued = 135;

        var ajuste = form.AAjustar();

        ajuste.Reason.Should().Be("Faltaban 15 días de prima");
        ajuste.ServiceBonusDaysAccrued.Should().Be(135);
        ajuste.SeveranceDaysAccrued.Should().Be(300);
        ajuste.Notes.Should().Be("Del libro de la contadora");
    }

    [Fact]
    public void Un_saldo_nuevo_parte_de_hoy_y_ceros_sin_dias_ni_notas()
    {
        var form = SaldoInicialFormulario.Desde(Resumen(conSaldo: false), vigente: null);

        form.AsOfDate.Should().Be(DateTime.Today);
        form.PendingVacationDays.Should().Be(0m);
        form.ServiceBonusDaysAccrued.Should().BeNull();
        form.Notes.Should().BeNull();
        form.AGuardar().Notes.Should().BeNull("una nota en blanco viaja como nulo, no como cadena vacía");
    }
}
