using FluentAssertions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T051, T052; HU5 escenarios 1–3, FR-024): la liquidación de la recogida explica el interés renglón a renglón —lo corrido hasta la fecha del
/// desembolso, la cuota completa, el proporcional por la ley, lo devuelto por período calendario y lo reconocido que se libera— con los días que salen de
/// la reconstrucción (las fechas del plan y la convención sellada), sin cambiar lo que se recoge. Los valores de cada modo ya los prueba
/// <see cref="LiquidacionDeRecogidaTests"/>; aquí van los textos y los días. El escenario es el de <see cref="PagosDePrueba"/> (Ana, 3.000.000 a 24 cuotas
/// desde el 01-12-2026, vencen el primero de cada mes); se recoge el 11-12, a mitad de la cuota 1. (nuevo)
/// </summary>
public class InteresesProporcionalesTests
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private static readonly DateOnly AMitad = Hoy.AddDays(10);
    private static readonly DateOnly VenceLaCuota1 = new(2027, 1, 1);

    // ------------------------------------------------------------------------------------------------------------ piezas --

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

    private static void Proporcionales(PagosDePrueba p, string valor) =>
        Vigencia(p.Db, ParametrosDeCartera.RecogidaInteresesProporcionales, valor, p.Credito.CreditLineId);

    private static async Task<LiquidacionDeCreditoRecogido> LiquidarAsync(PagosDePrueba p, DateOnly fecha)
    {
        var credito = await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id);
        var liquidada = await new LiquidacionDeRecogida(p.Db, p.C.Cargador(), p.Intereses(), p.G.Reloj, p.D.Parametros(), p.D.O.CuotaEnviada())
            .LiquidarCreditoAsync(credito, fecha, null, default);
        liquidada.IsSuccess.Should().BeTrue(liquidada.IsFailure ? $"{liquidada.Error.Code}: {liquidada.Error.Message}" : string.Empty);
        return liquidada.Value;
    }

    private static async Task<EstadoDelCredito> EstadoAsync(PagosDePrueba p, DateOnly fecha)
    {
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, fecha, default);
        return MotorDeCartera.Reconstruir(cargado!.Entrada!, fecha);
    }

    /// <summary>Los días de la convención sellada del plan entre dos fechas: los mismos con que el motor causa.</summary>
    private static int Dias(EstadoDelCredito estado, DateOnly desde, DateOnly hasta) => ContadorDeDias.Dias(estado.Plan!.Entrada.Convencion, desde, hasta);

    // -------------------------------------------------------------------------- HU5-1: «Sí» y lo causado más allá de D --

    [Fact]
    public async Task Con_Si_y_periodo_calendario_se_devuelven_los_dias_no_corridos_y_el_valor_los_descuenta()
    {
        using var p = await EscenarioAsync();
        Proporcionales(p, ParametrosDeCartera.ProporcionalesSi);
        Vigencia(p.Db, ParametrosDeCartera.CausacionPeriodo, ParametrosDeCartera.PeriodoCalendario, p.Credito.CreditLineId);
        await p.CausarHastaAsync(AMitad);
        var estado = await EstadoAsync(p, AMitad);

        var l = await LiquidarAsync(p, AMitad);

        // La cuota 1 vence el 01-01-2027: su período (enero) no ha empezado el 11-12, así que lo causado de ella se devuelve.
        var devuelto = l.ExplicacionDelInteres.Should().ContainSingle(r => r.Clase == ClasesDeRenglonDeInteres.Devuelto).Subject;
        var dias = Dias(estado, Hoy, AMitad);
        (devuelto.Cuota, devuelto.Dias, devuelto.Valor).Should().Be((1, (int?)dias, l.Desglose.InteresDevuelto));
        devuelto.Valor.Should().BePositive();
        devuelto.Texto.Should().StartWith($"Se devuelve el interés de {dias} días no corridos de la cuota 1")
            .And.Contain("01/01/2027").And.Contain("período calendario");
        l.ValorARecoger.Should().Be(l.Desglose.Total).And.Be(3_000_000m, "el valor a recoger descuenta lo devuelto");
        l.ExplicacionDelInteres.Should().NotContain(r => r.Clase == ClasesDeRenglonDeInteres.Corrido, "no se cobra nada de un período sin empezar");
        l.Avisos.Should().NotContain(a => a.Contains("período calendario"), "lo devuelto va en la explicación, no repetido en los avisos");
    }

    [Fact]
    public async Task Con_Si_y_causacion_al_inicio_del_ciclo_lo_reconocido_que_no_se_causo_se_libera_aparte_sin_cambiar_el_valor()
    {
        var causacion = await CausacionDePrueba.CrearAsync();
        await causacion.SembrarTiposDeHechoAsync();
        using var p = await EscenarioAsync(causacion: causacion);
        Proporcionales(p, ParametrosDeCartera.ProporcionalesSi);
        Vigencia(p.Db, ParametrosDeCartera.CausacionMomento, ParametrosDeCartera.CausacionAlInicioDelCiclo);
        await p.CausarHastaAsync(AMitad);
        var estado = await EstadoAsync(p, AMitad);
        var pendiente = estado.ReconocidoPendiente();
        pendiente.Should().BePositive("la cuota 1 se reconoció completa al inicio del ciclo y a mitad le falta por causar");

        var l = await LiquidarAsync(p, AMitad);

        l.ValorARecoger.Should().Be(estado.ValorParaCancelar, "lo reconocido y no causado nunca fue deuda: no cambia lo que se recoge");
        l.ReconocidoQueSeLibera.Should().Be(pendiente);
        var liberado = l.ExplicacionDelInteres.Should().ContainSingle(r => r.Clase == ClasesDeRenglonDeInteres.ReconocidoQueSeLibera).Subject;
        (liberado.Cuota, liberado.Dias, liberado.Valor).Should().Be((1, (int?)Dias(estado, AMitad, VenceLaCuota1), pendiente));
        liberado.Texto.Should().StartWith("Reconocido que se libera").And.Contain("no cambia lo que se recoge").And.Contain("01/01/2027");

        var corrido = l.ExplicacionDelInteres.Should().ContainSingle(r => r.Clase == ClasesDeRenglonDeInteres.Corrido).Subject;
        (corrido.Cuota, corrido.Dias, corrido.Valor).Should().Be((1, (int?)Dias(estado, Hoy, AMitad), l.Desglose.InteresCorriente));
    }

    // ------------------------------------------------------------------- HU5-2: «Sí» y la causación hecha sólo hasta antes --

    [Fact]
    public async Task Con_Si_y_la_causacion_atrasada_se_cobra_el_interes_hasta_la_fecha_del_desembolso_con_sus_dias()
    {
        using var p = await EscenarioAsync();
        Proporcionales(p, ParametrosDeCartera.ProporcionalesSi);
        var hastaDondeCauso = AMitad.AddDays(-5);
        await p.CausarHastaAsync(hastaDondeCauso);
        var enElLibro = await p.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == p.Credito.Id && t.Concept == LoanConcept.CurrentInterest).SumAsync(t => t.Amount);
        var estado = await EstadoAsync(p, AMitad);

        var l = await LiquidarAsync(p, AMitad);

        l.Desglose.InteresCorriente.Should().BeGreaterThan(enElLibro, "la recogida cobra también los días que el proceso diario todavía no causó");
        l.ValorARecoger.Should().Be(estado.ValorParaCancelar, "con «Sí» se cobra el corrido a la fecha del desembolso");
        var corrido = l.ExplicacionDelInteres.Should().ContainSingle().Subject;
        var dias = Dias(estado, Hoy, AMitad);
        (corrido.Clase, corrido.Cuota, corrido.Dias, corrido.Valor).Should()
            .Be((ClasesDeRenglonDeInteres.Corrido, (int?)1, (int?)dias, l.Desglose.InteresCorriente));
        corrido.Texto.Should().Be($"Se cobra el interés de {dias} días de la cuota 1 hasta la fecha del desembolso (del 02/12/2026 al 11/12/2026): "
            + $"{Pesos(l.Desglose.InteresCorriente)}.");
    }

    // ------------------------------------------------------------- HU5-3: «No» dentro del tope legal, y «No» fuera de él --

    [Fact]
    public async Task Con_No_dentro_del_tope_legal_se_cobra_el_proporcional_y_dice_por_que_y_cuantos_dias_no_se_cobran()
    {
        using var p = await EscenarioAsync(fueraDelTope: false);
        Proporcionales(p, ParametrosDeCartera.ProporcionalesNo);
        await p.CausarHastaAsync(AMitad);
        var estado = await EstadoAsync(p, AMitad);
        var porCausar = estado.InteresPorCausarDeLaCuotaEnCurso().Sum(x => x.Monto);

        var l = await LiquidarAsync(p, AMitad);

        l.Intereses.Motivo.Should().Be(InteresesAlCancelar.MotivoDentroDelTope);
        l.ValorARecoger.Should().Be(estado.ValorParaCancelar, "se cobra el proporcional: el corrido");
        var proporcional = l.ExplicacionDelInteres.Should().ContainSingle(r => r.Clase == ClasesDeRenglonDeInteres.Proporcional).Subject;
        var faltan = Dias(estado, AMitad, VenceLaCuota1);
        (proporcional.Cuota, proporcional.Dias, proporcional.Valor).Should().Be(((int?)1, (int?)faltan, porCausar));
        proporcional.Texto.Should().StartWith("Se cobra el proporcional: el crédito está dentro del tope legal del prepago")
            .And.Contain("Ley 1555").And.Contain($"No se cobran los {faltan} días que faltan de la cuota 1");
        l.ExplicacionDelInteres.Should().Contain(r => r.Clase == ClasesDeRenglonDeInteres.Corrido);
        l.ExplicacionDelInteres.Should().NotContain(r => r.Clase == ClasesDeRenglonDeInteres.CuotaCompleta);
    }

    [Fact]
    public async Task Con_No_fuera_del_tope_se_cobra_la_cuota_completa_con_los_dias_que_le_faltan()
    {
        using var p = await EscenarioAsync();
        Proporcionales(p, ParametrosDeCartera.ProporcionalesNo);
        await p.CausarHastaAsync(AMitad);
        var estado = await EstadoAsync(p, AMitad);

        var l = await LiquidarAsync(p, AMitad);

        l.Intereses.CuotaCompleta.Should().BeTrue();
        var completa = l.ExplicacionDelInteres.Should().ContainSingle(r => r.Clase == ClasesDeRenglonDeInteres.CuotaCompleta).Subject;
        var faltan = Dias(estado, AMitad, VenceLaCuota1);
        (completa.Cuota, completa.Dias, completa.Valor).Should().Be(((int?)1, (int?)faltan, l.Desglose.InteresCobradoPorProporcionales));
        completa.Texto.Should().StartWith("Se cobra el interés completo de la cuota 1").And.Contain($"los {faltan} días que faltan").And.Contain("01/01/2027");
        l.ExplicacionDelInteres.Select(r => r.Clase).Should().Equal(ClasesDeRenglonDeInteres.Corrido, ClasesDeRenglonDeInteres.CuotaCompleta);
        l.ExplicacionDelInteres.Where(r => r.Clase != ClasesDeRenglonDeInteres.ReconocidoQueSeLibera).Sum(r => r.Valor)
            .Should().Be(l.Desglose.InteresCorriente + l.Desglose.InteresCobradoPorProporcionales, "los renglones explican el interés que se cobra");
    }

    // ------------------------------------------------------------------------------------------------- de fábrica y la ley --

    [Fact]
    public async Task De_fabrica_solo_se_explica_lo_corrido_y_sin_plan_no_hay_explicacion()
    {
        using var p = await EscenarioAsync();
        await p.CausarHastaAsync(AMitad);

        var l = await LiquidarAsync(p, AMitad);

        l.ExplicacionDelInteres.Should().ContainSingle().Which.Clase.Should().Be(ClasesDeRenglonDeInteres.Corrido);
        l.ReconocidoQueSeLibera.Should().Be(0m);

        var viejo = p.D.O.Credito(p.D.O.Ana, 1_000_000m, "CR-VIEJO");
        var delLibro = await new LiquidacionDeRecogida(p.Db, p.C.Cargador(), p.Intereses(), p.G.Reloj, p.D.Parametros(), p.D.O.CuotaEnviada())
            .LiquidarCreditoAsync(viejo, Hoy, null, default);
        delLibro.Value.ExplicacionDelInteres.Should().BeEmpty("sin reconstrucción no hay días que contar");
    }

    [Theory]
    [InlineData(InteresesAlCancelar.MotivoVivienda, "vivienda", "Ley 546")]
    [InlineData(InteresesAlCancelar.MotivoDentroDelTope, "tope legal del prepago", "Ley 1555")]
    [InlineData(InteresesAlCancelar.MotivoTopeDesconocido, "salario mínimo", "fuera del tope legal")]
    public void La_razon_legal_del_proporcional_se_dice_en_palabras(string motivo, string dice, string tambien)
    {
        ExplicacionDeInteresesDeRecogida.RazonLegal(motivo).Should().Contain(dice).And.Contain(tambien);
        ExplicacionDeInteresesDeRecogida.RazonLegal("Otro").Should().BeNull();
    }

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
}
