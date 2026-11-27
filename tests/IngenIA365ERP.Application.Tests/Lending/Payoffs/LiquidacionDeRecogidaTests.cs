using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T009, T010, T011, T050; R3, R5; HU5, FR-003, FR-024): <see cref="LiquidacionDeRecogida"/> liquida un crédito recogido a (D, R) con el modo de
/// <c>Recogida.InteresesProporcionales</c> —«ComoAlCancelar» (de fábrica: lo de la caja), «Si» (el corrido) o «No» (la cuota completa con las excepciones
/// legales)— y el período calendario de la línea, lo desglosa por concepto y dice lo que impide recogerlo; y lo que liquida es, al peso, lo que aplica el
/// contrato con el <c>FullPayoff</c> de la recogida del desembolso (recaudador <c>CAR</c>/<c>OwnLoanPayoff</c>, documento <c>LoanDisbursement</c>): cancela
/// sin excedente. El cruce del retiro sigue liquidando con el modo de la caja. El escenario es el de <see cref="PagosDePrueba"/> (Ana, 3.000.000 a 24
/// cuotas desde el 01-12-2026, vencen el primero de cada mes); se recoge el 11-12, a mitad de la cuota 1.
/// </summary>
public class LiquidacionDeRecogidaTests
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private static readonly DateOnly AMitad = Hoy.AddDays(10);

    // ------------------------------------------------------------------------------------------------------------ piezas --

    /// <summary>Arma el escenario; con <paramref name="fueraDelTope"/>, el tope legal del prepago en una vez el SMMLV (menos que los 3.000.000).</summary>
    private static async Task<PagosDePrueba> EscenarioAsync(bool fueraDelTope = true, CausacionDePrueba? causacion = null)
    {
        var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: fueraDelTope, causacion: causacion);
        if (fueraDelTope)
        {
            await CopiasDePrueba.SembrarAsync(p.Db, p.G.UtcNow.AddDays(-40),
                CopiasDePrueba.Copia(CodigosDeValorNacional.Smmlv, string.Empty, new DateOnly(2026, 1, 1), 1_750_905m, p.G.UtcNow.AddDays(-40), unidad: "Pesos"),
                CopiasDePrueba.Copia(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PrepagoTopeSmmlv, new DateOnly(2026, 1, 1), 1m,
                    p.G.UtcNow.AddDays(-40), unidad: "Veces"));
            p.Db.ChangeTracker.Clear();
        }
        return p;
    }

    private static void Vigencia(TestApplicationDbContext db, string clave, string valor, int? linea = null)
    {
        db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
            ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine, ScopeId = linea ?? 0,
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    private static void CajaEnCuotaCompleta(PagosDePrueba p) =>
        Vigencia(p.Db, ParametrosDeCartera.PagosInteresesAlPagarAntesDelVencimiento, ParametrosDeCartera.InteresesDeLaCuotaCompleta);

    private static void Proporcionales(PagosDePrueba p, string valor, bool porLinea = true) =>
        Vigencia(p.Db, ParametrosDeCartera.RecogidaInteresesProporcionales, valor, porLinea ? p.Credito.CreditLineId : null);

    private static LiquidacionDeRecogida Liquidacion(PagosDePrueba p) => new(p.Db, p.C.Cargador(), p.Intereses(), p.G.Reloj);

    private static async Task<LiquidacionDeCreditoRecogido> LiquidarAsync(PagosDePrueba p, DateOnly fecha, DateTime? r = null, Loan? credito = null)
    {
        var c = credito ?? await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id);
        var liquidada = await Liquidacion(p).LiquidarCreditoAsync(c, fecha, r, default);
        liquidada.IsSuccess.Should().BeTrue(liquidada.IsFailure ? $"{liquidada.Error.Code}: {liquidada.Error.Message}" : string.Empty);
        return liquidada.Value;
    }

    /// <summary>El estado reconstruido a la fecha: lo causado (el corrido) y lo que falta por causar de la cuota en curso.</summary>
    private static async Task<(decimal Corrido, decimal PorCausar)> EstadoAsync(PagosDePrueba p, DateOnly fecha)
    {
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, fecha, default);
        var estado = MotorDeCartera.Reconstruir(cargado!.Entrada!, fecha);
        return (estado.ValorParaCancelar, estado.InteresPorCausarDeLaCuotaEnCurso().Sum(l => l.Monto));
    }

    /// <summary>El pago que manda la recogida del desembolso: <c>CAR</c>/<c>OwnLoanPayoff</c>, cruce interno y el documento del desembolso.</summary>
    private static PagoAAplicar Recogida(PagosDePrueba p, decimal valor, DateOnly fecha, string documento = InteresesAlCancelar.DocumentoDelDesembolso) =>
        p.Pago(ModalidadDePago.FullPayoff, valor, fecha, clave: RecogidaEnElDesembolso.Clave(Guid.NewGuid(), p.Credito.PublicId),
            medio: ClaseDeMedioDeRecaudo.InternalOffset) with
        {
            Receipt = new ReferenciaDeRecibo("DS-000001", fecha, null, null, documento),
        };

    /// <summary>Aplica la recogida por el contrato real y comprueba que cancela al peso, sin excedente; devuelve la decisión sellada.</summary>
    private static async Task<(ComprobanteDeAplicacionDto Comprobante, DecisionDePago Decision)> RecogeAlPesoAsync(PagosDePrueba p, decimal valor, DateOnly fecha,
        string documento = InteresesAlCancelar.DocumentoDelDesembolso)
    {
        var r = await p.AplicarAsync(Recogida(p, valor, fecha, documento), recaudador: RecogidaEnElDesembolso.Recaudador);
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        r.Value!.PaidOff.Should().BeTrue("lo liquidado cancela el crédito");
        r.Value.Surplus.Should().Be(0m, "lo liquidado no sobra");
        var decision = DecisionDePago.Leer((await p.OperacionAsync(r.Value.OperationPublicId)).Payload)!;
        return (r.Value, decision);
    }

    // ------------------------------------------------------------------------------------------- los tres modos (T011) --

    /// <summary>
    /// Los tres valores de <c>Recogida.InteresesProporcionales</c>, con la caja en «CuotaCompleta» y el crédito fuera del tope legal, por cada momento y
    /// período de causación: el modo efectivo es el esperado, el desglose suma lo que se recoge, el gemelo de <see cref="InteresesAlCancelar"/> dice lo mismo
    /// y el contrato, con el <c>FullPayoff</c> de la recogida, cobra exactamente eso y lo sella para que la reconstrucción lo repita.
    /// </summary>
    [Theory]
    [InlineData(ParametrosDeCartera.ProporcionalesComoAlCancelar, ParametrosDeCartera.CausacionDiaria, false, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesSi, ParametrosDeCartera.CausacionDiaria, false, ParametrosDeCartera.InteresesCorridos)]
    [InlineData(ParametrosDeCartera.ProporcionalesNo, ParametrosDeCartera.CausacionDiaria, false, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesComoAlCancelar, ParametrosDeCartera.CausacionAlInicioDelCiclo, false, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesSi, ParametrosDeCartera.CausacionAlInicioDelCiclo, false, ParametrosDeCartera.InteresesCorridos)]
    [InlineData(ParametrosDeCartera.ProporcionalesNo, ParametrosDeCartera.CausacionAlInicioDelCiclo, false, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesComoAlCancelar, ParametrosDeCartera.CausacionDiaria, true, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesSi, ParametrosDeCartera.CausacionDiaria, true, ParametrosDeCartera.InteresesCorridos)]
    [InlineData(ParametrosDeCartera.ProporcionalesNo, ParametrosDeCartera.CausacionDiaria, true, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesComoAlCancelar, ParametrosDeCartera.CausacionAlInicioDelCiclo, true, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    [InlineData(ParametrosDeCartera.ProporcionalesSi, ParametrosDeCartera.CausacionAlInicioDelCiclo, true, ParametrosDeCartera.InteresesCorridos)]
    [InlineData(ParametrosDeCartera.ProporcionalesNo, ParametrosDeCartera.CausacionAlInicioDelCiclo, true, ParametrosDeCartera.InteresesDeLaCuotaCompleta)]
    public async Task Cada_modo_liquida_lo_que_el_contrato_cobra_al_recoger(string proporcionales, string momento, bool periodoCalendario, string esperado)
    {
        CausacionDePrueba? causacion = null;
        if (momento == ParametrosDeCartera.CausacionAlInicioDelCiclo)
        {
            causacion = await CausacionDePrueba.CrearAsync();
            await causacion.SembrarTiposDeHechoAsync();
        }
        using var p = await EscenarioAsync(causacion: causacion);
        CajaEnCuotaCompleta(p);
        Proporcionales(p, proporcionales);
        Vigencia(p.Db, ParametrosDeCartera.CausacionMomento, momento);
        if (periodoCalendario) Vigencia(p.Db, ParametrosDeCartera.CausacionPeriodo, ParametrosDeCartera.PeriodoCalendario, p.Credito.CreditLineId);
        await p.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(p, AMitad);
        porCausar.Should().BePositive("a mitad de la cuota 1 falta por causar el interés de los días que quedan");

        var l = await LiquidarAsync(p, AMitad);

        (l.Proporcionales, l.Intereses.Efectivo, l.PeriodoCalendario).Should().Be((proporcionales, esperado, periodoCalendario));
        l.Fuente.Should().Be(FuenteDeLaLiquidacion.Reconstruccion);
        l.Recogible.Should().BeTrue();
        l.Desglose.Total.Should().Be(l.ValorARecoger, "el desglose suma lo que se recoge");
        l.Desglose.Capital.Should().Be(3_000_000m);
        l.Huella.Should().StartWith("sha256:").And.HaveLength(71);
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, AMitad, default);
        (await p.Intereses().ValorParaRecogerAsync(p.Credito, MotorDeCartera.Reconstruir(cargado!.Entrada!, AMitad), default))
            .Should().Be(l.ValorARecoger, "el gemelo de ValorParaCancelarAsync dice lo mismo");
        if (periodoCalendario)
        {
            // La cuota 1 vence el 01-01-2027: su tramo (enero) no ha empezado el 11-12, así que lo causado de ella no se cobra y la cuota completa tampoco.
            l.Desglose.InteresDevuelto.Should().Be(l.Desglose.InteresCorriente).And.BePositive();
            l.Desglose.InteresCobradoPorProporcionales.Should().Be(0m);
            l.ValorARecoger.Should().Be(3_000_000m);
            l.Avisos.Should().Contain(a => a.Contains("período calendario"));
        }
        else if (esperado == ParametrosDeCartera.InteresesDeLaCuotaCompleta)
        {
            l.Desglose.InteresCobradoPorProporcionales.Should().Be(porCausar);
            l.ValorARecoger.Should().Be(corrido + porCausar);
            l.Avisos.Should().Contain(a => a.Contains("interés completo de la cuota 1"));
        }
        else
        {
            (l.Desglose.InteresDevuelto, l.Desglose.InteresCobradoPorProporcionales).Should().Be((0m, 0m));
            l.ValorARecoger.Should().Be(corrido);
        }

        var (comprobante, decision) = await RecogeAlPesoAsync(p, l.ValorARecoger, AMitad);

        comprobante.AppliedAmount.Should().Be(l.ValorARecoger);
        decision.InterestBeforeDueDate.Should().Be(proporcionales == ParametrosDeCartera.ProporcionalesSi ? null : esperado,
            "se sella lo que pidió la recogida (con «Si» no se pide la cuota completa: nada que sellar, como el corrido de fábrica)");
        decision.AccrualPeriod.Should().Be(periodoCalendario ? ParametrosDeCartera.PeriodoCalendario : null);
        await p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task De_fabrica_la_recogida_cobra_el_corrido_como_la_caja_y_como_el_cruce_del_retiro()
    {
        using var p = await EscenarioAsync();
        await p.CausarHastaAsync(AMitad);
        var (corrido, _) = await EstadoAsync(p, AMitad);

        var l = await LiquidarAsync(p, AMitad);

        (l.Proporcionales, l.Intereses).Should().Be((ParametrosDeCartera.ProporcionalesComoAlCancelar, ModoDeInteresesAlCancelar.Corrido));
        l.ExplicacionDeIntereses.Should().BeNull("con lo de fábrica no hay nada que explicar");
        l.ValorARecoger.Should().Be(corrido);
        l.Desglose.InteresCorriente.Should().Be(corrido - 3_000_000m);
        (await p.D.MotorDelCredito().LiquidarAsync(p.Credito.Id, AMitad, default)).ParaCancelar.Should().Be(l.ValorARecoger,
            "de fábrica el desembolso y el cruce del retiro dicen lo mismo");
        l.CuotaCausada.Should().NotBeNull();
        (l.CuotaCausada!.Numero, l.CuotaCausada.Vencimiento).Should().Be((1, new DateOnly(2027, 1, 1)));
        l.CuotaCausada.Valor.Should().BePositive();

        var (_, decision) = await RecogeAlPesoAsync(p, l.ValorARecoger, AMitad);
        decision.InterestBeforeDueDate.Should().BeNull("de fábrica la decisión se sella como siempre");
    }

    // -------------------------------------------------------------------------------------------------- T050 y el retiro --

    [Fact]
    public async Task Con_Si_el_cruce_del_retiro_y_un_pago_que_no_es_del_desembolso_siguen_con_el_modo_de_la_caja()
    {
        using var p = await EscenarioAsync();
        CajaEnCuotaCompleta(p);
        Proporcionales(p, ParametrosDeCartera.ProporcionalesSi);
        await p.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(p, AMitad);

        (await LiquidarAsync(p, AMitad)).ValorARecoger.Should().Be(corrido, "la recogida con «Si» cobra el corrido");
        (await p.D.MotorDelCredito().LiquidarAsync(p.Credito.Id, AMitad, default)).ParaCancelar.Should().Be(corrido + porCausar,
            "el cruce del retiro sigue con el modo de la caja: no lee Recogida.InteresesProporcionales");

        // OwnLoanPayoff sin el documento del desembolso (como el cruce del retiro): la caja manda y cobra la cuota completa.
        var (comprobante, decision) = await RecogeAlPesoAsync(p, corrido + porCausar, AMitad, documento: "MembershipWithdrawal");
        comprobante.AppliedAmount.Should().Be(corrido + porCausar);
        decision.InterestBeforeDueDate.Should().Be(ParametrosDeCartera.InteresesDeLaCuotaCompleta);
    }

    [Fact]
    public void Solo_la_recogida_del_desembolso_usa_el_modo_de_la_recogida()
    {
        var recogida = new PagoAAplicar("CAR:x", Guid.NewGuid(), ModalidadDePago.FullPayoff, 1m, Hoy, ClaseDeMedioDeRecaudo.InternalOffset, Guid.NewGuid(),
            new ReferenciaDeRecibo("DS-1", Hoy, null, null, InteresesAlCancelar.DocumentoDelDesembolso));

        AplicacionDePagos.EsRecogidaDelDesembolso(RecogidaEnElDesembolso.Recaudador, recogida).Should().BeTrue();
        AplicacionDePagos.EsRecogidaDelDesembolso(RecogidaEnElDesembolso.Recaudador, recogida with { Receipt = recogida.Receipt with { DocumentKind = "Recibo" } })
            .Should().BeFalse("el cruce del retiro y el débito automático pagan con OwnLoanPayoff y otro documento");
        AplicacionDePagos.EsRecogidaDelDesembolso(PagosDePrueba.Prueba, recogida).Should().BeFalse("otro recaudador no es la recogida");
    }

    // ------------------------------------------------------------------------------------------- excepciones legales --

    [Fact]
    public async Task Con_No_dentro_del_tope_legal_se_cobra_el_proporcional_y_dice_por_que()
    {
        using var p = await EscenarioAsync(fueraDelTope: false);
        Proporcionales(p, ParametrosDeCartera.ProporcionalesNo);
        await p.CausarHastaAsync(AMitad);
        var (corrido, _) = await EstadoAsync(p, AMitad);

        var l = await LiquidarAsync(p, AMitad);

        l.Intereses.Should().Be(new ModoDeInteresesAlCancelar(ParametrosDeCartera.InteresesDeLaCuotaCompleta, ParametrosDeCartera.InteresesCorridos,
            InteresesAlCancelar.MotivoDentroDelTope));
        l.ValorARecoger.Should().Be(corrido);
        l.ExplicacionDeIntereses.Should().Contain("Ley 1555");
        l.Avisos.Should().Contain(l.ExplicacionDeIntereses!);

        var (_, decision) = await RecogeAlPesoAsync(p, l.ValorARecoger, AMitad);
        (decision.InterestBeforeDueDate, decision.InterestBeforeDueDateReason)
            .Should().Be((ParametrosDeCartera.InteresesCorridos, InteresesAlCancelar.MotivoDentroDelTope));
    }

    [Fact]
    public async Task El_modo_al_recoger_lee_la_linea_con_caida_al_general_y_respeta_la_vivienda_y_el_tope_desconocido()
    {
        using var p = await EscenarioAsync();
        var intereses = p.Intereses();
        var linea = p.Credito.CreditLineId;

        (await intereses.RecogidaConfiguradaAsync(AMitad, linea, default)).Should().Be(ParametrosDeCartera.ProporcionalesComoAlCancelar, "de fábrica");
        (await intereses.ModoAlRecogerAsync(p.Credito, VersionesDeReglas.Vigente, AMitad, default)).Should().Be(ModoDeInteresesAlCancelar.Corrido);

        Proporcionales(p, ParametrosDeCartera.ProporcionalesNo, porLinea: false);
        Proporcionales(p, ParametrosDeCartera.ProporcionalesSi);
        intereses = p.Intereses(); // el lector de parámetros guarda lo leído en su ámbito: uno nuevo, como en otra petición
        (await intereses.RecogidaConfiguradaAsync(AMitad, linea, default)).Should().Be(ParametrosDeCartera.ProporcionalesSi, "la línea manda");
        (await intereses.RecogidaConfiguradaAsync(AMitad, linea + 1000, default)).Should().Be(ParametrosDeCartera.ProporcionalesNo, "las demás, el general");
        (await intereses.ModoAlRecogerAsync(p.Credito, VersionesDeReglas.Vigente, AMitad, default)).Should().Be(ModoDeInteresesAlCancelar.Corrido);

        var otraLinea = new Loan { Number = "CR-OTRA", CreditLineId = linea + 1000, ApprovedAmount = 3_000_000m, OpenedOn = Hoy };
        (await intereses.ModoAlRecogerAsync(otraLinea, VersionesDeReglas.Vigente, AMitad, default)).CuotaCompleta.Should().BeTrue("«No», fuera del tope");
        (await intereses.ModoAlRecogerAsync(otraLinea, VersionesDeReglas.Vivienda, AMitad, default)).Should().Be(new ModoDeInteresesAlCancelar(
            ParametrosDeCartera.InteresesDeLaCuotaCompleta, ParametrosDeCartera.InteresesCorridos, InteresesAlCancelar.MotivoVivienda),
            "Ley 546 de 1999: la vivienda se prepaga sin penalidad");

        using var sinTope = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Proporcionales(sinTope, ParametrosDeCartera.ProporcionalesNo);
        (await sinTope.Intereses().ModoAlRecogerAsync(sinTope.Credito, VersionesDeReglas.Vigente, AMitad, default)).Motivo
            .Should().Be(InteresesAlCancelar.MotivoTopeDesconocido, "sin el SMMLV o el tope no se sabe: nunca se cobra de más por un dato que falta");
    }

    // ---------------------------------------------------------------------------------- anticipo, saldo a favor, D y R --

    [Fact]
    public async Task El_anticipo_se_descuenta_D_futura_se_proyecta_y_con_R_anterior_no_se_ve_el_pago()
    {
        using var p = await EscenarioAsync();
        await p.CausarHastaAsync(AMitad);
        var r = p.G.UtcNow;
        var futura = AMitad.AddDays(10);
        var antes = await LiquidarAsync(p, futura, r);
        var liquidacion = await new GetLoanSettlementQueryHandler(p.Db, p.C.Cargador(), p.G.Alcance, p.G.Auditoria, p.G.Reloj, p.Intereses())
            .Handle(new GetLoanSettlementQuery(p.Credito.PublicId, futura, r), default);
        antes.ValorARecoger.Should().Be(liquidacion.Value.ToPayOff, "una D futura se proyecta como la liquidación del crédito");
        (await LiquidarAsync(p, AMitad, r)).ValorARecoger.Should().BeLessThan(antes.ValorARecoger, "a una D posterior, más interés");

        p.G.UtcNow = r.AddMinutes(10);
        (await p.AplicadoAsync(p.Pago(ModalidadDePago.Advance, 100_000m, AMitad))).AdvanceAmount.Should().Be(100_000m);

        var conR = await LiquidarAsync(p, futura, r);
        (conR.Huella, conR.ValorARecoger).Should().Be((antes.Huella, antes.ValorARecoger), "con R de antes no se ve el anticipo");
        var ahora = await LiquidarAsync(p, futura);
        ahora.Desglose.Anticipo.Should().Be(100_000m);
        ahora.ValorARecoger.Should().Be(antes.ValorARecoger - 100_000m, "el anticipo del mismo crédito se descuenta: la cancelación lo usa primero");
        ahora.Huella.Should().NotBe(antes.Huella);

        var hoy = await LiquidarAsync(p, AMitad);
        hoy.Desglose.Total.Should().Be(hoy.ValorARecoger);
        await RecogeAlPesoAsync(p, hoy.ValorARecoger, AMitad);
        await p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task El_saldo_a_favor_no_se_descuenta_y_se_avisa()
    {
        using var p = await EscenarioAsync();
        await p.CausarHastaAsync(AMitad);
        // Un abono sin preferencia (revocada) queda como saldo a favor (D24).
        p.Db.PrepaymentPreferences.Add(Domain.Entities.Lending.Origination.PrepaymentPreference.Registrar(p.D.O.Ana.Id, p.Credito.Id, null, null, 2,
            p.G.UtcNow.AddMinutes(-1), PreferenceOrigin.DebtorRequest, null));
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        (await p.AplicadoAsync(p.Pago(ModalidadDePago.ExtraordinaryPrincipal, 500_000m, AMitad))).CreditBalanceAmount.Should().Be(500_000m);
        var (corrido, _) = await EstadoAsync(p, AMitad);

        var l = await LiquidarAsync(p, AMitad);

        l.ValorARecoger.Should().Be(corrido, "D7: la cancelación no usa el saldo a favor");
        (l.Desglose.SaldoAFavor, l.SaldoAFavorSinDescontar).Should().Be((0m, 500_000m));
        l.Avisos.Should().Contain(a => a.Contains("saldo a favor") && a.Contains("no descuenta"));
        var (comprobante, _) = await RecogeAlPesoAsync(p, l.ValorARecoger, AMitad);
        comprobante.BalancesAfter.CreditBalance.Should().Be(500_000m, "queda a favor del deudor");
    }

    // --------------------------------------------------------------------------------------------------------- bloqueos --

    [Fact]
    public async Task En_mora_castigado_en_insolvencia_o_cancelado_no_se_recoge_y_dice_por_que()
    {
        using var p = await EscenarioAsync();
        var credito = await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id);

        var alDia = await LiquidarAsync(p, AMitad, credito: credito);
        alDia.Recogible.Should().BeTrue();

        // El 20-01-2027 la cuota 1 (vence el 01-01) sigue sin pagar: la proyección lo ve.
        var enMora = await LiquidarAsync(p, new DateOnly(2027, 1, 20), credito: credito);
        enMora.DiasDeMora.Should().BePositive();
        enMora.Bloqueo!.Should().Match<BloqueoDeRecogida>(b => b.Codigo == ErroresDeDesembolso.CodigoRequiereReestructuracion
                                                               && b.Motivo == ErroresDeRecogida.MotivoEnMora && b.Mensaje.Contains("Novedades"));

        credito.Status = LoanStatus.WrittenOff;
        (await LiquidarAsync(p, AMitad, credito: credito)).Bloqueo!.Should()
            .Match<BloqueoDeRecogida>(b => b.Codigo == ErroresDeRecogida.CodigoNoPagable && b.Motivo == ErroresDeRecogida.MotivoCastigado);
        credito.Status = LoanStatus.PaidOff;
        (await LiquidarAsync(p, AMitad, credito: credito)).Bloqueo!.Motivo.Should().Be(ErroresDeRecogida.MotivoNoActivo);
        credito.Status = LoanStatus.Active;

        p.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = p.D.O.Ana.Id, StartDate = Hoy, Status = InsolvencyProceedingStatus.Negotiating });
        await p.Db.SaveChangesAsync();
        var insolvencia = await LiquidarAsync(p, AMitad, credito: credito);
        insolvencia.Bloqueos.Should().ContainSingle().Which.Should()
            .Match<BloqueoDeRecogida>(b => b.Codigo == ErroresDeRecogida.CodigoNoPagable && b.Motivo == ErroresDeRecogida.MotivoInsolvencia);
        insolvencia.ValorARecoger.Should().Be(alDia.ValorARecoger, "el bloqueo no cambia lo que costaría");
    }

    // ------------------------------------------------------------------------------- sin condiciones selladas (caída) --

    [Fact]
    public async Task Sin_condiciones_selladas_cae_a_la_posicion_diaria_o_al_libro_como_el_motor_del_desembolso()
    {
        using var p = await EscenarioAsync();
        var viejo = p.D.O.Credito(p.D.O.Ana, 1_000_000m, "CR-VIEJO");

        var delLibro = await LiquidarAsync(p, Hoy, credito: viejo);
        (delLibro.Fuente, delLibro.ValorARecoger, delLibro.Desglose.Capital, delLibro.DiasDeMora).Should().Be((FuenteDeLaLiquidacion.Libro, 1_000_000m, 1_000_000m, 0));
        delLibro.Intereses.Should().Be(ModoDeInteresesAlCancelar.Corrido);
        delLibro.CuotaCausada.Should().BeNull();

        p.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = viejo.Id, PositionDate = Hoy, Version = 1, RegisteredAt = p.G.UtcNow, Status = LoanStatus.Active, BranchId = p.D.O.Norte.Id,
            CreditLineId = viejo.CreditLineId, ModalityCode = "CONSUMO", Principal = 1_000_000m, CurrentInterest = 30_000m, PayoffAmount = 1_050_000m,
            DaysPastDue = 0,
        });
        await p.Db.SaveChangesAsync();

        var dePosicion = await LiquidarAsync(p, Hoy, credito: viejo);
        (dePosicion.Fuente, dePosicion.ValorARecoger).Should().Be((FuenteDeLaLiquidacion.PosicionDiaria, 1_050_000m));
        dePosicion.Desglose.Total.Should().Be(1_050_000m);
        (dePosicion.Desglose.InteresCorriente, dePosicion.Desglose.OtrosCargos).Should().Be((30_000m, 20_000m), "lo que la posición no desglosa va en otros");
        (await p.D.MotorDelCredito().LiquidarAsync(viejo.Id, Hoy, default)).ParaCancelar.Should().Be(dePosicion.ValorARecoger);
    }
}
