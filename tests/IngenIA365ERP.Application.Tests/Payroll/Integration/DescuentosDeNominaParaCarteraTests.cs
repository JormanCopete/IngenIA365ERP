using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Integration;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Integration;

/// <summary>
/// Feature 028 (T064a; R6 «Nómina propia», FR-021): <see cref="DescuentosDeNominaParaCartera"/> dice, por obligación, si la nómina propia ya tiene un
/// descuento de cartera vivo —la novedad del período abierto (generado), la corrida en borrador que lo calculó (calculado) o la aprobada que lo aplicó
/// (aprobado, mientras el período no haya terminado a la fecha)—, con el período, la corrida y el valor. El escenario es <see cref="NominaTestData"/>: marzo de
/// 2026 abierto y dos cuotas de Ana (150.000 que cabe y 2.500.000 que cabe en parte). (nuevo)
/// </summary>
public class DescuentosDeNominaParaCarteraTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
    private static readonly Guid Cabe = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid NoCabe = new("00000000-0000-0000-0000-000000000001");
    private static readonly DateOnly AMitad = new(2026, 3, 15);

    private static NominaTestData ConCuotas()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var persona = d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId;
        d.Obligaciones.AgregarCuota(new CuotaDeNominaDto(persona, Cabe, "A-1", "CONSUMO", 3, new DateOnly(2026, 3, 31), 150_000m, 0m, 1));
        d.Obligaciones.AgregarCuota(new CuotaDeNominaDto(persona, NoCabe, "B-1", "CONSUMO", 9, new DateOnly(2026, 3, 31), 2_500_000m, 0m, 2));
        return d;
    }

    private static async Task<Guid> CalcularAsync(NominaTestData d)
    {
        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.CuotasDeCartera, d.CuotasDeCuentas, d.Lock, d.Clock, d.User,
                NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        d.Db.ChangeTracker.Clear();
        return calc.Value.RunPublicId;
    }

    private static Task<IReadOnlyList<DescuentoDeNominaVivoDto>> VivosAsync(NominaTestData d, DateOnly fecha, params Guid[] obligaciones) =>
        new DescuentosDeNominaParaCartera(d.Db).VivosAsync(obligaciones, fecha, CancellationToken.None);

    [Fact]
    public async Task Sin_descuento_de_la_obligacion_no_hay_nada()
    {
        var d = ConCuotas();

        (await VivosAsync(d, AMitad, Cabe)).Should().BeEmpty("la nómina todavía no materializa las cuotas");
        (await VivosAsync(d, AMitad)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_novedad_del_periodo_abierto_sin_calcular_es_generado()
    {
        var d = ConCuotas();
        await d.CuotasDeCartera.MaterializeAsync(d.Marzo, CancellationToken.None);
        await d.Db.SaveChangesAsync();
        d.Db.ChangeTracker.Clear();

        var vivo = (await VivosAsync(d, AMitad, Cabe, Guid.NewGuid())).Should().ContainSingle().Subject;

        vivo.Should().BeEquivalentTo(new
        {
            ObligationPublicId = Cabe, State = EstadoDelDescuentoDeNomina.Generated, PeriodPublicId = d.Marzo.PublicId, PeriodStart = new DateOnly(2026, 3, 1),
            PeriodEnd = new DateOnly(2026, 3, 31), RunPublicId = (Guid?)null, InstallmentNumber = (int?)3, Amount = 150_000m,
        });
        vivo.Period.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task La_corrida_en_borrador_que_lo_calculo_es_calculado_con_lo_que_descuenta()
    {
        var d = ConCuotas();
        var corrida = await CalcularAsync(d);
        var filas = await d.Db.PayrollLoanDeductions.AsNoTracking().ToListAsync();

        var vivos = await VivosAsync(d, AMitad, Cabe, NoCabe);

        vivos.Should().HaveCount(2).And.OnlyContain(v => v.State == EstadoDelDescuentoDeNomina.Calculated && v.RunPublicId == corrida && v.RunVersion != null);
        vivos.Single(v => v.ObligationPublicId == Cabe).Amount.Should().Be(150_000m);
        vivos.Single(v => v.ObligationPublicId == NoCabe).Amount.Should().Be(filas.Single(f => f.ObligationPublicId == NoCabe).DeductedAmount,
            "el valor es lo que la corrida descuenta, no la cuota");
        vivos.Single(v => v.ObligationPublicId == Cabe).InstallmentNumber.Should().Be(3, "la cuota la nombra la novedad");
    }

    [Fact]
    public async Task La_corrida_aprobada_es_aprobado_mientras_el_periodo_no_termine()
    {
        var d = ConCuotas();
        var corrida = await CalcularAsync(d);
        var aprobada = await new ApprovePayrollRunCommandHandler(d.Db, d.Contabilizador(Contadora), d.Policies, d.Permissions, d.Clock, Contadora,
                new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance, CooperativaDePrueba.Actual), d.StaleMarker, d.Recaudo,
                d.Obligaciones, d.RecaudoDeCuentas)
            .Handle(new ApprovePayrollRunCommand(corrida, Confirm: true), CancellationToken.None);
        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        d.Db.ChangeTracker.Clear();

        var vivo = (await VivosAsync(d, AMitad, Cabe)).Should().ContainSingle().Subject;

        (vivo.State, vivo.RunPublicId, vivo.Amount).Should().Be((EstadoDelDescuentoDeNomina.Approved, (Guid?)corrida, 150_000m));
        (await VivosAsync(d, new DateOnly(2026, 4, 1), Cabe)).Should().BeEmpty("lo aprobado de un período ya terminado es historia");
    }
}
