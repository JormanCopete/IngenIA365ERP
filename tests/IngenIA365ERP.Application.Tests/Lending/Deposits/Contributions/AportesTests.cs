using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// Feature 014, US3 (T081–T085, T092; contrato-de-cuentas §3.6, §3.8, §3.11, §4; FR-040 a FR-043, FR-049, FR-087, FR-089, FR-094): las cuotas exigibles
/// de aportes que genera el paso de captaciones, su pago por el contrato de cuentas (cuotas en orden de vencimiento, el mínimo primero, el excedente
/// como extraordinario, el no descontado de Nómina), el reverso, la consulta de aportes v2 —el mismo saldo que el libro— y el reparto con el ahorro
/// permanente de un fondo de empleados. La prueba independiente de la historia: la cuota de enero pagada por el recaudador de prueba y la consulta
/// que devuelve lo mismo. (nuevo)
/// </summary>
public sealed class AportesTests : IDisposable
{
    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    private static readonly DateOnly Enero10 = new(2027, 1, 10);

    // ------------------------------------------------------------------------------------------------ T082 cuotas --

    [Fact]
    public async Task El_paso_genera_una_cuota_por_mes_desde_la_apertura_idempotente_y_con_su_vencimiento()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);

        (await _a.GenerarAsync(cuenta, Enero10)).Should().BeEmpty();
        (await _a.GenerarAsync(cuenta, Enero10)).Should().BeEmpty("otra vez el mismo día no genera nada");

        var cuotas = await _a.Db.AccountInstallments.AsNoTracking().OrderBy(c => c.DueDate).ToListAsync();
        cuotas.Select(c => (c.Periodo, c.DueDate, c.Amount)).Should().Equal(
            ("2026-12", new DateOnly(2026, 12, 5), 50_000m),
            ("2027-01", new DateOnly(2027, 1, 5), 50_000m));
        cuotas.Should().AllSatisfy(c => c.GeneratedByProcessRunId.Should().Be(1));
        (await _a.Db.MemberAccountControls.AsNoTracking().SingleAsync()).LastInstallmentPeriod.Should().Be("2027-01-1");
    }

    [Fact]
    public async Task US3_4_en_un_fondo_de_empleados_la_cuota_se_reparte_con_el_ahorro_permanente()
    {
        var permanente = await _a.C.LineaDeCaptacionAsync("AHPERM", CuentasDePrueba.ALaVista() with { AllowsPartialWithdrawals = false }, DepositClass.Permanent);
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version(100_000m,
            permanente: new PermanentSavingsShareInput(permanente.PublicId, 0.4m)));
        await _a.C.AbiertaAsync(_a.C.Apertura(permanente, _a.C.Ana));
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);

        (await _a.GenerarAsync(cuenta, new DateOnly(2026, 12, 10))).Should().BeEmpty();

        var cuotas = await _a.Db.AccountInstallments.AsNoTracking().ToListAsync();
        var deAportes = cuotas.Single(c => c.LinkedInstallmentId is null);
        var delPermanente = cuotas.Single(c => c.LinkedInstallmentId is not null);
        (deAportes.Amount, deAportes.ContributionShare, deAportes.PermanentSavingsShare).Should().Be((60_000m, 60_000m, 40_000m));
        (delPermanente.Amount, delPermanente.LinkedInstallmentId).Should().Be((40_000m, deAportes.Id));

        var exigibles = await _a.ExigiblesAsync(_a.C.Ana, new DateOnly(2026, 12, 10));
        exigibles.Select(e => (e.Nature, e.PayrollConcept, e.Amount)).Should().BeEquivalentTo(new[]
        {
            (NaturalezaDeCuenta.Contribution, ContratoDeCuentasDeCarteraConceptos.Aportes, 60_000m),
            (NaturalezaDeCuenta.Deposit, ContratoDeCuentasDeCarteraConceptos.Ahorros, 40_000m),
        });
    }

    [Fact]
    public async Task Sin_ingreso_registrado_la_cuota_por_fraccion_del_ingreso_falla_y_queda_el_error()
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version(forma: new ContributionInstallmentInput
        {
            Kind = ContributionAmountMode.IncomeFraction, Fraction = 0.03m, DueDay = 5,
        }));
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);

        var fallos = await _a.GenerarAsync(cuenta, new DateOnly(2026, 12, 10));

        fallos.Should().ContainSingle().Which.Codigo.Should().Be("Lending.Contributions.IncomeMissing");
        (await _a.Db.AccountInstallments.CountAsync()).Should().Be(0);
    }

    // -------------------------------------------------------------------------------------------- prueba independiente --

    [Fact]
    public async Task US3_la_cuota_de_enero_pagada_por_el_recaudador_de_prueba_y_la_consulta_devuelve_lo_mismo_que_el_libro()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;

        // US3-1: dos cuotas de 50.000 sin pagar; se paga más y el excedente va como extraordinario.
        var exigibles = await _a.ExigiblesAsync(_a.C.Ana);
        exigibles.Select(e => (e.Period, e.Pending, e.OverpaymentAllowed, e.Order)).Should().Equal(("2026-12", 50_000m, true, 1), ("2027-01", 50_000m, true, 2));

        var c = await _a.ConsignadaAsync(_a.Consignacion(cuenta, 120_000m));
        c.InstallmentsCovered.Select(q => (q.Period, q.Amount)).Should().Equal(("2026-12", 50_000m), ("2027-01", 50_000m));
        c.Transactions.Select(t => (t.Concept, t.Amount)).Should().BeEquivalentTo(new[]
        {
            (ConceptoDeCuenta.ContributionMinimum, 50_000m), (ConceptoDeCuenta.ContributionMinimum, 50_000m), (ConceptoDeCuenta.ContributionExtraordinary, 20_000m),
        }, "el mínimo no reducible (500.000) se completa primero y el excedente es extraordinario");
        (await _a.ExigiblesAsync(_a.C.Ana)).Should().BeEmpty();

        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == AporteRecibidoV1.Type);
        hecho.OriginPublicId.Should().Be(c.OperationPublicId);
        hecho.PayloadJson.Should().Contain("\"overpaymentAs\":\"Extraordinary\"").And.Contain("\"period\":\"2026-12\"");
        (await _a.EventosAsync()).Should().Contain(Application.Common.Audit.AuditEventTypes.LendingAccountContractDeposited);

        // US3-3: la consulta de aportes de la Cartera responde lo mismo que el libro, con el mínimo y sin cuotas en mora.
        var saldos = await _a.Consulta().SaldosAsync(new SolicitudDeSaldoDeAportes([_a.C.Ana.PublicId], Enero10), default);
        saldos.Available.Should().BeTrue();
        var saldo = saldos.Balances.Should().ContainSingle().Subject;
        (saldo.Contributions, saldo.PermanentSavings, saldo.Minimum, saldo.InstallmentsInArrears).Should().Be((120_000m, 0m, 500_000m, 0));
        var libro = await _a.Db.MemberAccountTransactions.AsNoTracking().SumAsync(t => t.Amount);
        saldo.Contributions.Should().Be(libro);
    }

    [Fact]
    public async Task La_misma_clave_devuelve_el_comprobante_original_y_con_otro_cuerpo_se_rechaza()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        var clave = AportesDePrueba.Clave();

        var primera = await _a.ConsignadaAsync(_a.Consignacion(cuenta, 50_000m, clave: clave));
        var segunda = await _a.ConsignarAsync(_a.Consignacion(cuenta, 50_000m, clave: clave));
        var otra = await _a.ConsignarAsync(_a.Consignacion(cuenta, 40_000m, clave: clave));

        segunda.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        segunda.Value!.OperationPublicId.Should().Be(primera.OperationPublicId);
        otra.Code.Should().Be("Lending.Contract.KeyReused");
        (await _a.Db.MemberAccountOperations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Sin_extraordinarios_el_exceso_se_rechaza_y_las_cuotas_especificas_deben_ser_de_la_cuenta()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana, await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version(extraordinarios: false)));
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;

        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 120_000m))).Code.Should().Be(ErroresDeAportes.CodigoExcedenteNoAdmitido);
        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.SpecificInstallments, cuotas: [Guid.NewGuid()])))
            .Code.Should().Be(ErroresDeAportes.CodigoCuotaAjena);

        var enero = await _a.Db.AccountInstallments.AsNoTracking().SingleAsync(c => c.PeriodMonth == 1);
        var c = await _a.ConsignadaAsync(_a.Consignacion(cuenta, 50_000m, ModalidadDeConsignacion.SpecificInstallments, cuotas: [enero.PublicId]));
        c.InstallmentsCovered.Should().ContainSingle().Which.Period.Should().Be("2027-01", "sólo la nombrada, aunque la de diciembre vence antes");
    }

    [Fact]
    public async Task US3_2_lo_que_Nomina_no_alcanza_queda_parcialmente_pagado_con_su_motivo_y_sigue_exigible()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        var diciembre = await _a.Db.AccountInstallments.AsNoTracking().SingleAsync(c => c.PeriodMonth == 12);
        var corrida = Guid.NewGuid();
        var clave = $"NOM:{corrida:N}:{cuenta.PublicId:N}";

        var r = await _a.Contrato().ConsignarAsync(new SolicitudDeConsignacion(Recaudador.Nomina, FormaDeAplicacion.NonInteractive,
            [_a.Consignacion(cuenta, 30_000m, ModalidadDeConsignacion.SpecificInstallments, fechaValor: new DateOnly(2026, 12, 31), clave: clave,
                cuotas: [diciembre.PublicId], medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction)],
            [new NoDescontadoDeCuenta(clave, cuenta.PublicId, diciembre.PublicId, 50_000m, 20_000m, MotivoDeNoDescuento.DeductionCap, "Tope de descuentos",
                null)]), default);
        _a.Db.ChangeTracker.Clear();

        r.Deposits.Single().Result.Should().Be(ResultadoDelContrato.Applied, r.Deposits.Single().Message);
        r.NotDeducted.Single().Result.Should().Be(ResultadoDelContrato.Applied, r.NotDeducted.Single().Message);
        var leida = (await LectorDeCuotas.DeCuentasAsync(_a.Db, [diciembre.MemberAccountId], default)).Single(q => q.Cuota.Id == diciembre.Id);
        (leida.Estado, leida.Pendiente, leida.MotivoDeNoDescuento).Should().Be((EstadoDeCuotaDeCuenta.PartiallyPaid, 20_000m, NonDeductionReason.DeductionCap));
        (await _a.ExigiblesAsync(_a.C.Ana)).Select(e => (e.Period, e.Pending)).Should().Equal(("2026-12", 20_000m), ("2027-01", 50_000m));
        (await _a.Db.AccountInstallmentNonDeductions.AsNoTracking().SingleAsync()).Origin.Should().Be(NonDeductionOrigin.Contract);

        // El reintento de Nómina con la misma clave no duplica el no descontado.
        var otra = await _a.Contrato().ConsignarAsync(new SolicitudDeConsignacion(Recaudador.Nomina, FormaDeAplicacion.NonInteractive, [],
            [new NoDescontadoDeCuenta(clave, cuenta.PublicId, diciembre.PublicId, 50_000m, 20_000m, MotivoDeNoDescuento.DeductionCap, null, null)]), default);
        otra.NotDeducted.Single().Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
    }

    [Fact]
    public async Task El_recaudador_reversa_con_su_clave_las_cuotas_vuelven_a_estar_pendientes_y_un_reverso_no_se_reversa()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        var clave = AportesDePrueba.Clave();
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 100_000m, clave: clave));

        var r = await _a.Contrato().ReversarAsync(new SolicitudDeReversoDeCuenta(AportesDePrueba.Prueba, clave, "Cheque devuelto"), default);
        _a.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        r.Value!.Transactions.Sum(t => t.Amount).Should().Be(-100_000m);
        (await _a.ExigiblesAsync(_a.C.Ana)).Should().HaveCount(2);
        (await _a.Db.MemberAccountTransactions.AsNoTracking().SumAsync(t => t.Amount)).Should().Be(0m);
        (await _a.HechosAsync()).Should().Equal(AporteRecibidoV1.Type, OperacionDeCuentaReversadaV1.Type);

        var otra = await _a.Contrato().ReversarAsync(new SolicitudDeReversoDeCuenta(AportesDePrueba.Prueba, clave, "Otra vez"), default);
        otra.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        var deReverso = await _a.Contrato().ReversarAsync(new SolicitudDeReversoDeCuenta(AportesDePrueba.Prueba, clave + ":reverso", "Del reverso"), default);
        deReverso.Code.Should().Be("Lending.Savings.ReversalOfReversal");
    }

    // ------------------------------------------------------------------------------------------------------ reglas --

    [Fact]
    public async Task Sin_aportes_activados_responde_no_habilitado_y_la_consulta_no_esta_disponible()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        var setup = await _a.Db.LendingSetups.SingleAsync();
        setup.ContributionsActivationDate = null;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free))).Code.Should().Be("Lending.Contributions.NotActivated");
        (await _a.Consulta().SaldosAsync(new SolicitudDeSaldoDeAportes([_a.C.Ana.PublicId], CuentasDePrueba.Hoy), default)).Available.Should().BeFalse();
    }

    [Fact]
    public async Task El_lote_exige_transaccion_y_recaudador_con_ProveedorDeCuentas()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        _a.EnTransaccion = false;
        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free))).Code.Should().Be("Lending.Contract.TransactionRequired");

        _a.EnTransaccion = true;
        _a.Adopciones[ClavesDeRecaudador.Prueba] = [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1)];
        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free))).Code.Should().Be("Lending.Contract.CollectorNotRegistered",
            "adoptar el contrato de obligaciones no habilita las cuentas");
    }

    [Fact]
    public async Task Los_aportes_no_se_retiran_por_el_contrato_ni_Nomina_paga_con_otro_medio()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        var retiro = await _a.Contrato().RetirarAsync(new SolicitudDeRetiro(AportesDePrueba.Prueba, AportesDePrueba.Clave(), cuenta.PublicId, 10m, CuentasDePrueba.Hoy,
            ClaseDeMedioDeRecaudo.Cash, [], _a.C.Ana.PublicId, new ReferenciaDeRecibo("R", CuentasDePrueba.Hoy, null, null, null)), default);
        retiro.Code.Should().Be(ErroresDeAportes.CodigoNoRetirable);

        var nomina = await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, clave: $"NOM:{Guid.NewGuid():N}:{cuenta.PublicId:N}"),
            Recaudador.Nomina);
        nomina.Code.Should().Be(ErroresDeAportes.CodigoClaseDeMedio, "Nómina sólo consigna con su descuento");
    }

    [Fact]
    public async Task El_limite_de_participacion_de_la_linea_rechaza_el_aporte_que_lo_supera()
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: AportesDePrueba.Version() with { MaxParticipationFraction = 0.10m });
        var ana = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        var beto = await _a.CuentaDeAportesAsync(_a.C.Beto, linea);
        await _a.ConsignadaAsync(_a.Consignacion(beto, 1_000_000m, ModalidadDeConsignacion.Free));

        var r = await _a.ConsignarAsync(_a.Consignacion(ana, 200_000m, ModalidadDeConsignacion.Free));

        r.Code.Should().Be(ErroresDeAportes.CodigoLimiteDeParticipacion);
        (await _a.ConsignarAsync(_a.Consignacion(ana, 100_000m, ModalidadDeConsignacion.Free))).Result.Should().Be(ResultadoDelContrato.Applied,
            "100.000 de 1.100.000 es el 9 %");
    }

    [Fact]
    public async Task La_fecha_valor_no_es_futura_ni_anterior_a_la_activacion()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, fechaValor: CuentasDePrueba.Hoy.AddDays(1))))
            .Code.Should().Be(ErroresDeAportes.CodigoFechaValor);
        (await _a.ConsignarAsync(_a.Consignacion(cuenta, 10_000m, ModalidadDeConsignacion.Free, fechaValor: new DateOnly(2026, 10, 31))))
            .Code.Should().Be(ErroresDeAportes.CodigoAntesDelCorte);
    }

    [Fact]
    public async Task Simular_dice_lo_que_cubriria_sin_escribir()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;

        var r = await _a.Contrato().SimularAsync(new SolicitudDeSimulacionDeCuenta(AportesDePrueba.Prueba, "Deposit", cuenta.PublicId, 70_000m, Enero10,
            ClaseDeMedioDeRecaudo.Cash, [], Modality: ModalidadDeConsignacion.Installments), default);

        r.Value!.Allowed.Should().BeTrue(r.Message);
        r.Value.InstallmentsCovered.Select(q => (q.Period, q.Amount)).Should().Equal(("2026-12", 50_000m), ("2027-01", 20_000m));
        (await _a.Db.MemberAccountOperations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Consultar_devuelve_la_cuenta_de_aportes_con_su_saldo_y_sin_retiros()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 30_000m, ModalidadDeConsignacion.Free));

        var r = await _a.Contrato().ConsultarAsync(new SolicitudDeConsultaDeCuentas(AportesDePrueba.Prueba, _a.C.Ana.PublicId, CuentasDePrueba.Hoy), default);

        var consultada = r.Value!.Accounts.Should().ContainSingle().Subject;
        (consultada.Nature, consultada.Available.Balance, consultada.Available.Available, consultada.AcceptsWithdrawals)
            .Should().Be((NaturalezaDeCuenta.Contribution, 30_000m, 0m, false));
        r.Value.ContributionsStatus.Should().Be(EstadoDeCartera.Active);
    }
}

/// <summary>Los conceptos de Nómina que el contrato asigna a cada exigible (E-02).</summary>
internal static class ContratoDeCuentasDeCarteraConceptos
{
    public const string Aportes = Application.Lending.Integration.ContratoDeCuentasDeCartera.ConceptoDeAportes;
    public const string Ahorros = Application.Lending.Integration.ContratoDeCuentasDeCartera.ConceptoDeAhorros;
}
