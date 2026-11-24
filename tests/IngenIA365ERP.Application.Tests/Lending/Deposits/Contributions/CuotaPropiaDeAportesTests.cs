using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// 2026-11-24 (pedido del dueño): la cuota propia de una cuenta de aportes. Con <c>Aportes.OrigenDeLaCuota = DeLaLinea</c> (de fábrica) rige la de la línea
/// y sólo se elige el canal; con <c>DeLaCuenta</c> se digitan cuota, periodicidad, fecha del primer pago y canal, el generador las usa y se corrigen con
/// motivo hacia adelante. El canal decide quién descuenta: la nómina sólo ve las cuentas sin canal o con canal «nómina».
/// </summary>
public sealed class CuotaPropiaDeAportesTests : IDisposable
{
    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    private async Task CuotaDeLaCuentaAsync()
    {
        _a.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.AportesOrigenDeLaCuota, Value = ParametrosDeCartera.CuotaDeLaCuenta,
            ValidFrom = CuentasDePrueba.Hoy.AddDays(-1), Reason = "prueba",
        });
        await _a.Db.SaveChangesAsync();
    }

    private static ContributionAccountInstallmentInput Propia(decimal cuota = 80_000m, DateOnly? primerPago = null,
        RepaymentChannel canal = RepaymentChannel.Cashier) =>
        new(cuota, RepaymentPeriodicity.Monthly, primerPago ?? new DateOnly(2026, 12, 20), canal);

    [Fact]
    public async Task De_fabrica_rige_la_cuota_de_la_linea_y_solo_se_elige_el_canal()
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());

        (await _a.C.AbrirAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia() })).Error.Code
            .Should().Be(CuotaPropiaDeAportes.CodigoCuotaDeLaLinea);
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana) with
        {
            ContributionInstallment = new ContributionAccountInstallmentInput(PaymentChannel: RepaymentChannel.OwnPayroll),
        });

        cuenta.ContributionInstallment.Should().Be(new ContributionAccountInstallmentDto(false, null, null, null, RepaymentChannel.OwnPayroll));
        await _a.GenerarAsync(cuenta, new DateOnly(2026, 12, 10));
        (await _a.Db.AccountInstallments.AsNoTracking().SingleAsync()).Amount.Should().Be(50_000m, "la de la línea");
    }

    [Fact]
    public async Task Con_la_cuota_de_la_cuenta_se_piden_los_cuatro_datos_y_el_generador_los_usa()
    {
        await CuotaDeLaCuentaAsync();
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());

        var sinNada = await _a.C.AbrirAsync(_a.C.Apertura(linea, _a.C.Ana));
        sinNada.Error.Code.Should().Be(CuotaPropiaDeAportes.CodigoCuotaRequerida);
        (await _a.C.AbrirAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia(primerPago: new DateOnly(2026, 11, 30)) }))
            .Error.Code.Should().Be(CuotaPropiaDeAportes.CodigoCuotaInvalida, "el primer pago no va antes de la apertura");
        (await _a.C.AbrirAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia() with { PaymentChannel = RepaymentChannel.DirectDebit } }))
            .Error.Code.Should().Be(CuotaPropiaDeAportes.CodigoCanalNoAdmitido);

        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia(83_333m) });
        cuenta.ContributionInstallment.Should().Be(new ContributionAccountInstallmentDto(true, 83_333m, RepaymentPeriodicity.Monthly,
            new DateOnly(2026, 12, 20), RepaymentChannel.Cashier));

        await _a.GenerarAsync(cuenta, new DateOnly(2027, 1, 10));
        var cuotas = await _a.Db.AccountInstallments.AsNoTracking().OrderBy(c => c.DueDate).ToListAsync();
        cuotas.Select(c => (c.DueDate, c.Amount)).Should().Equal(
            (new DateOnly(2026, 12, 20), 83_333m),
            (new DateOnly(2027, 1, 20), 83_333m));
    }

    [Fact]
    public async Task La_primera_cuota_espera_la_fecha_del_primer_pago()
    {
        await CuotaDeLaCuentaAsync();
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia(primerPago: new DateOnly(2027, 2, 15)) });

        await _a.GenerarAsync(cuenta, new DateOnly(2027, 1, 31));
        (await _a.Db.AccountInstallments.CountAsync()).Should().Be(0, "diciembre y enero son anteriores al primer pago");

        await _a.GenerarAsync(cuenta, new DateOnly(2027, 2, 1));
        (await _a.Db.AccountInstallments.AsNoTracking().SingleAsync()).DueDate.Should().Be(new DateOnly(2027, 2, 15));
    }

    [Fact]
    public async Task La_nomina_no_descuenta_la_cuenta_que_se_paga_por_caja()
    {
        await CuotaDeLaCuentaAsync();
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());
        var porCaja = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia(10_000m) });
        var porNomina = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia(20_000m, canal: RepaymentChannel.OwnPayroll) });
        await _a.GenerarAsync(porCaja, new DateOnly(2026, 12, 21));
        await _a.GenerarAsync(porNomina, new DateOnly(2026, 12, 21));

        var nomina = await _a.Contrato().ListarExigiblesAsync(
            new SolicitudDeExigibles(Recaudador.Nomina, [_a.C.Ana.PublicId], new DateOnly(2026, 12, 21)), default);
        nomina.Value!.Installments.Select(i => i.AccountPublicId).Should().Equal(porNomina.PublicId);

        (await _a.ExigiblesAsync(_a.C.Ana, new DateOnly(2026, 12, 21))).Select(i => i.AccountPublicId)
            .Should().BeEquivalentTo([porCaja.PublicId, porNomina.PublicId], "la caja recibe las dos");
    }

    [Fact]
    public async Task La_cuota_se_corrige_con_motivo_y_rige_para_las_que_faltan()
    {
        await CuotaDeLaCuentaAsync();
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version());
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana) with { ContributionInstallment = Propia() });
        await _a.GenerarAsync(cuenta, new DateOnly(2026, 12, 21));

        var handler = new SetContributionInstallmentCommandHandler(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.C.Parametros(), _a.G.Reloj);
        var r = await handler.Handle(new SetContributionInstallmentCommand(cuenta.PublicId, Propia(95_000m, canal: RepaymentChannel.PaymasterDeduction),
            "El asociado cambió de vehículo"), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.ContributionInstallment!.Amount.Should().Be(95_000m);
        _a.Db.ChangeTracker.Clear();

        await _a.GenerarAsync(cuenta, new DateOnly(2027, 1, 21));
        var cuotas = await _a.Db.AccountInstallments.AsNoTracking().OrderBy(c => c.DueDate).ToListAsync();
        cuotas.Select(c => c.Amount).Should().Equal(80_000m, 95_000m);
    }

    [Fact]
    public void Validar_pide_lo_que_falta_y_no_admite_el_debito_automatico()
    {
        var hoy = CuentasDePrueba.Hoy;
        CuotaPropiaDeAportes.Validar(null, deLaCuenta: false, hoy).IsSuccess.Should().BeTrue();
        CuotaPropiaDeAportes.Validar(new ContributionAccountInstallmentInput(PaymentChannel: RepaymentChannel.Cashier), false, hoy).IsSuccess.Should().BeTrue();
        CuotaPropiaDeAportes.Validar(new ContributionAccountInstallmentInput(PaymentChannel: RepaymentChannel.DirectDebit), false, hoy).Error.Code
            .Should().Be(CuotaPropiaDeAportes.CodigoCanalNoAdmitido);
        CuotaPropiaDeAportes.Validar(new ContributionAccountInstallmentInput(Amount: 0m, Periodicity: RepaymentPeriodicity.Monthly, FirstDueDate: hoy,
            PaymentChannel: RepaymentChannel.Cashier), true, hoy).Error.Code.Should().Be(CuotaPropiaDeAportes.CodigoCuotaInvalida);
        CuotaPropiaDeAportes.Validar(new ContributionAccountInstallmentInput(10m, RepaymentPeriodicity.SinglePayment, hoy, RepaymentChannel.Cashier), true, hoy)
            .Error.Code.Should().Be(CuotaPropiaDeAportes.CodigoCuotaInvalida);
        CuotaPropiaDeAportes.Validar(new ContributionAccountInstallmentInput(Amount: 10m), true, hoy).Error.Message
            .Should().Contain("la periodicidad").And.Contain("la fecha del primer pago").And.Contain("el canal de pago");
    }
}
