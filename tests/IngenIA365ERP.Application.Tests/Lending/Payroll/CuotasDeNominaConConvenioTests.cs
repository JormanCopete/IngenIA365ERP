using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0814 (interfaces.md §6.1, C32): las cuotas que Nómina descuenta, con el convenio de la nómina propia.
/// <list type="bullet">
/// <item>con un convenio de nómina propia que lleva lo vencido (o cuyo seguimiento es incluirlo en el siguiente), <c>CuotasDeNominaAsync</c> devuelve
/// <c>PastDueIncluded</c> con lo no descontado del período anterior; sin él, como en K1 (<c>PastDueIncluded = 0</c>);</item>
/// <item><c>Order</c> es la prelación por <c>LibranzaDate</c>; sólo obligaciones vivas como deudor con vínculo <c>OwnPayroll</c>.</item>
/// </list>
/// </summary>
public sealed class CuotasDeNominaConConvenioTests
{
    /// <summary>La escena: la nómina propia con la versión dada, el crédito de Ana por nómina y la causación hasta la segunda cuota, con la primera impaga.</summary>
    private static async Task<(LibranzaDePrueba L, ConsultaDeCuotasDeNomina Consulta)> EscenaAsync(bool llevaLoVencido = false,
        NonDeductionFollowUpAction? regla = null, bool conConvenio = true)
    {
        var l = await LibranzaDePrueba.CrearAsync(conConvenio: false);
        if (conConvenio)
        {
            l.Convenio = await l.ConvenioAsync("NOMINA", l.Cooperativa, nominaPropia: true,
                version: l.Version() with { IncludesPastDue = llevaLoVencido, NotAppliedAction = regla });
            l.Vincular(l.Credito, l.Convenio, null, RepaymentChannel.OwnPayroll);
        }
        else
        {
            var v = l.Db.LoanPayrollLinks.Single(x => x.LoanId == l.Credito.Id && x.ValidTo == null);
            v.RepaymentChannel = RepaymentChannel.OwnPayroll;
            v.IsOwnPayroll = true;
            v.PaymasterPersonId = l.Credito.DebtorPersonId;
            v.LibranzaDate = v.ValidFrom;
            await l.Db.SaveChangesAsync();
            l.Db.ChangeTracker.Clear();
        }
        var cuotas = await CuotasAsync(l, l.Credito);
        await l.P.CausarHastaAsync(cuotas[1].DueDate);
        return (l, new ConsultaDeCuotasDeNomina([l.Ana.PublicId], cuotas[1].DueDate.AddDays(-14), cuotas[1].DueDate));
    }

    private static async Task<List<RepaymentInstallment>> CuotasAsync(LibranzaDePrueba l, Loan credito)
    {
        var version = await l.Db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == credito.Id).OrderByDescending(v => v.VersionNumber).FirstAsync();
        return await l.Db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == version.Id).OrderBy(i => i.InstallmentNumber).ToListAsync();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, NonDeductionFollowUpAction.IncludedInNextFile)]
    public async Task Con_el_convenio_que_lleva_lo_vencido_la_cuota_trae_lo_no_descontado_antes(bool llevaLoVencido, NonDeductionFollowUpAction? regla)
    {
        var (l, consulta) = await EscenaAsync(llevaLoVencido, regla);
        using var _ = l;

        var r = await l.P.Obligaciones().CuotasDeNominaAsync(consulta, default);

        var cuota = r.Installments.Should().ContainSingle().Subject;
        (cuota.InstallmentNumber, cuota.Order).Should().Be(((int?)2, 1));
        cuota.PastDueIncluded.Should().BeGreaterThan(0m, "la primera cuota quedó impaga");
        cuota.Amount.Should().BeGreaterThan(cuota.PastDueIncluded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sin_ese_convenio_es_como_en_K1(bool conConvenio)
    {
        var (l, consulta) = await EscenaAsync(conConvenio: conConvenio);
        using var _ = l;

        var r = await l.P.Obligaciones().CuotasDeNominaAsync(consulta, default);

        r.Installments.Should().ContainSingle().Which.PastDueIncluded.Should().Be(0m, "sin el convenio que lo lleve, lo vencido no va (C32)");
    }

    [Fact]
    public async Task El_orden_es_la_prelacion_por_la_fecha_de_la_libranza_y_solo_entran_las_de_nomina_propia()
    {
        var l = await LibranzaDePrueba.CrearAsync(conConvenio: false);
        using var _ = l;
        l.Convenio = await l.ConvenioAsync("NOMINA", l.Cooperativa, nominaPropia: true);
        l.Vincular(l.Credito, l.Convenio, null, RepaymentChannel.OwnPayroll);
        var segundo = await l.OtroCreditoAsync(l.Ana, 1_000_000m, "N/A", l.Convenio);
        var v = await l.Db.LoanPayrollLinks.SingleAsync(x => x.LoanId == segundo.Id && x.ValidTo == null);
        v.RepaymentChannel = RepaymentChannel.OwnPayroll;
        v.IsOwnPayroll = true;
        v.PaymasterPersonId = segundo.DebtorPersonId;
        v.LibranzaDate = new DateOnly(2026, 6, 1);
        await l.Db.SaveChangesAsync();
        l.Db.ChangeTracker.Clear();
        // Un tercero por caja: no entra.
        var deCaja = await l.OtroCreditoAsync(l.Ana, 500_000m, "N/A", l.Convenio);
        l.Vincular(deCaja, l.Convenio, null, RepaymentChannel.Cashier);
        var primera = (await CuotasAsync(l, l.Credito))[0].DueDate;

        var r = await l.P.Obligaciones().CuotasDeNominaAsync(new ConsultaDeCuotasDeNomina([l.Ana.PublicId], primera.AddDays(-14), primera), default);

        r.Installments.Select(c => (c.ObligationPublicId, c.Order)).Should().Equal((segundo.PublicId, 1), (l.Credito.PublicId, 2));
        r.Installments.Should().NotContain(c => c.ObligationPublicId == deCaja.PublicId);
    }
}
