using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// 2026-11-16 (pedido del dueño; decisiones del 2026-11-16): con <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> en «CuotaCompleta», la cancelación por
/// el contrato real cobra el interés completo de la cuota en curso —sólo fuera del tope legal del prepago sin penalidad y nunca en vivienda—: lo
/// sella en la decisión del pago, lo causa en la fecha valor (fila <c>UnaccruedInstallmentInterest</c>) y lo manda a Contabilidad en el acompañante
/// <c>AjusteDeCausacionPorFechaValor</c>; el reverso lo deshace con el mismo acompañante en negativo, aparte de lo restituido. El listado, la liquidación
/// y la recogida del desembolso ofrecen para cancelar lo mismo que cobra la caja, y un pago anterior sin el modo sellado se sigue reconstruyendo con el
/// corrido. El escenario es el de <see cref="PagosDePrueba"/> (Ana, 3.000.000 a 24 cuotas desde el 01-12-2026); se cancela el 11-12, a mitad de la
/// cuota 1, que vence el 01-01-2027.
/// </summary>
public class CancelacionConLaCuotaCompletaTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private static readonly DateOnly AMitad = Hoy.AddDays(10);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private PagosDePrueba _p = null!;

    /// <summary>Fuera del tope legal: el tope del prepago va en una vez el SMMLV (1.750.905), menos que los 3.000.000 del crédito.</summary>
    public async Task InitializeAsync()
    {
        _p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        await SembrarTopeAsync(_p, veces: 1m);
        CuotaCompleta(_p.Db);
    }

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private static async Task SembrarTopeAsync(PagosDePrueba p, decimal veces)
    {
        await CopiasDePrueba.SembrarAsync(p.Db, p.G.UtcNow.AddDays(-40),
            CopiasDePrueba.Copia(CodigosDeValorNacional.Smmlv, string.Empty, new DateOnly(2026, 1, 1), 1_750_905m, p.G.UtcNow.AddDays(-40), unidad: "Pesos"),
            CopiasDePrueba.Copia(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PrepagoTopeSmmlv, new DateOnly(2026, 1, 1), veces, p.G.UtcNow.AddDays(-40),
                unidad: "Veces"));
        p.Db.ChangeTracker.Clear();
    }

    private static void CuotaCompleta(TestApplicationDbContext db)
    {
        db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.PagosInteresesAlPagarAntesDelVencimiento,
            Value = ParametrosDeCartera.InteresesDeLaCuotaCompleta, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", ScopeKind = ParameterScopeKind.None,
            CreatedBy = "test",
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }

    /// <summary>El estado a la fecha con lo que falta por causar de la cuota en curso.</summary>
    private async Task<(decimal ParaCancelar, decimal PorCausar)> EstadoAsync(PagosDePrueba p, DateOnly fecha)
    {
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, fecha, default);
        var estado = MotorDeCartera.Reconstruir(cargado!.Entrada!, fecha);
        return (estado.ValorParaCancelar, estado.InteresPorCausarDeLaCuotaEnCurso().Sum(l => l.Monto));
    }

    [Fact]
    public async Task Fuera_del_tope_la_cancelacion_cobra_la_cuota_en_curso_la_sella_y_la_lleva_a_Contabilidad_como_causacion()
    {
        await _p.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(_p, AMitad);
        porCausar.Should().BePositive("a mitad de la cuota 1 falta por causar el interés de los días que quedan hasta su vencimiento");

        // El listado de la caja ofrece para cancelar lo mismo que cobrará el FullPayoff.
        var listado = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, AMitad), default);
        var paraCancelar = listado.Value!.Obligations.Single().PayoffAmount;
        paraCancelar.Should().Be(corrido + porCausar);

        var pago = _p.Pago(ModalidadDePago.FullPayoff, paraCancelar, AMitad);
        var c = await _p.AplicadoAsync(pago);
        var repetido = await _p.AplicarAsync(pago);
        repetido.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        repetido.Value!.Warnings.Should().Contain(AplicadorDePagos.AvisoInteresDeLaCuotaCompleta, "el comprobante original repetido también lo dice");

        c.PaidOff.Should().BeTrue();
        c.Surplus.Should().Be(0m);
        c.Warnings.Should().Contain(AplicadorDePagos.AvisoInteresDeLaCuotaCompleta, "el deudor sabe que se cobró la cuota en curso completa");
        var op = await _p.OperacionAsync(c.OperationPublicId);
        var decision = DecisionDePago.Leer(op.Payload)!;
        decision.InterestBeforeDueDate.Should().Be(ParametrosDeCartera.InteresesDeLaCuotaCompleta);
        decision.InterestBeforeDueDateReason.Should().BeNull();
        decision.WithinLegalPrepaymentCap.Should().BeFalse();
        decision.Contenido().CuotaCompleta.Should().BeTrue("la reconstrucción lo repite desde la decisión sellada");

        var filas = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync();
        var causada = filas.Should().ContainSingle(t => t.Kind == LoanTransactionKind.UnaccruedInstallmentInterest).Subject;
        (causada.Concept, causada.InstallmentNumber, causada.Amount, causada.ValueDate).Should().Be((LoanConcept.CurrentInterest, 1, porCausar, AMitad));

        var unidad = await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == op.PublicId).ToListAsync();
        var ajuste = unidad.Should().ContainSingle(m => m.Type == AjusteDeCausacionPorFechaValorV1.Type).Subject;
        causada.MessagePublicId.Should().Be(ajuste.PublicId);
        var contenido = JsonSerializer.Deserialize<AjusteDeCausacionPorFechaValorV1>(ajuste.PayloadJson, Web)!;
        contenido.Lines.Single().Amounts.Should().ContainSingle()
            .Which.Should().Match<ConceptAmountV1>(a => a.Concept == nameof(LoanConcept.CurrentInterest) && a.InstallmentNumber == 1 && a.Amount == porCausar);
        var aplicado = JsonSerializer.Deserialize<PagoAplicadoV1>(unidad.Single(m => m.Type == PagoAplicadoV1.Type).PayloadJson, Web)!;
        aplicado.Amounts.Single(a => a.Concept == nameof(LoanConcept.CurrentInterest) && a.InstallmentNumber == 1).Amount
            .Should().Be(filas.Where(t => t.Kind == LoanTransactionKind.PaymentAllocation && t.Concept == LoanConcept.CurrentInterest).Sum(t => -t.Amount),
                "el pago cobra lo causado y lo que faltaba; la causación de lo que faltaba va en el acompañante");
        aplicado.Amounts.Sum(a => a.Amount).Should().Be(paraCancelar);

        (await _p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task El_reverso_quita_lo_cobrado_de_la_cuota_en_curso_con_el_acompanante_en_negativo_y_reabre_el_credito()
    {
        await _p.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(_p, AMitad);
        var pago = _p.Pago(ModalidadDePago.FullPayoff, corrido + porCausar, AMitad);
        await _p.AplicadoAsync(pago);

        var r = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey, "Cheque devuelto"), default);
        _p.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        var reverso = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.PaymentReversal);
        var unidad = await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == reverso.PublicId).ToListAsync();
        var reversado = JsonSerializer.Deserialize<PagoReversadoV1>(unidad.Single(m => m.Type == PagoReversadoV1.Type).PayloadJson, Web)!;
        reversado.Amounts.Sum(a => a.Amount).Should().Be(corrido + porCausar, "PagoReversado restituye lo que el pago aplicó, ni más ni menos");
        var ajuste = unidad.Should().ContainSingle(m => m.Type == AjusteDeCausacionPorFechaValorV1.Type).Subject;
        JsonSerializer.Deserialize<AjusteDeCausacionPorFechaValorV1>(ajuste.PayloadJson, Web)!.Lines.Single().Amounts
            .Should().ContainSingle(a => a.Concept == nameof(LoanConcept.CurrentInterest) && a.InstallmentNumber == 1 && a.Amount == -porCausar,
                "lo que se causó al cancelar se deshace como causación, aparte de lo restituido");
        var espejo = await _p.Db.LoanTransactions.AsNoTracking()
            .SingleAsync(t => t.OperationId == reverso.Id && t.Kind == LoanTransactionKind.UnaccruedInstallmentInterest);
        (espejo.Amount, espejo.MessagePublicId).Should().Be((-porCausar, ajuste.PublicId));

        (await _p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _p.Credito.Id)).Status.Should().Be(LoanStatus.Active, "el reverso reabre el crédito");
        (await EstadoAsync(_p, AMitad)).ParaCancelar.Should().Be(corrido, "como si la cancelación no hubiera existido");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_liquidacion_y_la_recogida_del_desembolso_ofrecen_lo_mismo_que_cobra_la_caja_y_la_recogida_cancela()
    {
        await _p.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(_p, AMitad);

        var liquidacion = await new GetLoanSettlementQueryHandler(_p.Db, _p.C.Cargador(), _p.G.Alcance, _p.G.Auditoria, _p.G.Reloj, _p.Intereses())
            .Handle(new GetLoanSettlementQuery(_p.Credito.PublicId, AMitad), default);
        liquidacion.IsSuccess.Should().BeTrue(liquidacion.IsFailure ? liquidacion.Error.Message : string.Empty);
        liquidacion.Value.InterestBeforeDueDate.Should().Be(ParametrosDeCartera.InteresesDeLaCuotaCompleta);
        liquidacion.Value.CurrentInstallmentInterestToAccrue.Should().Be(porCausar);
        liquidacion.Value.ToPayOff.Should().Be(corrido, "la liquidación de siempre no cambia");
        liquidacion.Value.ToPayOffWithCurrentInstallment.Should().Be(corrido + porCausar);
        liquidacion.Value.Warnings.Should().Contain(w => w.Contains("interés completo de la cuota 1"));

        var recoger = await _p.D.MotorDelCredito().LiquidarAsync(_p.Credito.Id, AMitad, default);
        recoger.ParaCancelar.Should().Be(corrido + porCausar);

        var recogida = _p.Pago(ModalidadDePago.FullPayoff, recoger.ParaCancelar, AMitad, clave: RecogidaEnElDesembolso.Clave(Guid.NewGuid(), _p.Credito.PublicId),
            medio: ClaseDeMedioDeRecaudo.InternalOffset);
        var r = await _p.AplicarAsync(recogida, recaudador: RecogidaEnElDesembolso.Recaudador);
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        r.Value!.PaidOff.Should().BeTrue("la recogida paga lo que ofreció la liquidación y cancela");
        r.Value.Surplus.Should().Be(0m);
        DecisionDePago.Leer((await _p.OperacionAsync(r.Value.OperationPublicId)).Payload)!.InterestBeforeDueDate
            .Should().Be(ParametrosDeCartera.InteresesDeLaCuotaCompleta, "la recogida también cancela con la cuota completa (decisión del dueño)");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_definitiva_de_nomina_propone_descontar_con_la_cuota_completa_y_su_descuento_cancela()
    {
        await _p.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(_p, AMitad);

        var obligaciones = await _p.Obligaciones().ObligacionesAsync(new ConsultaDeObligaciones([_p.D.O.Ana.PublicId], AMitad, IncludeAsCoDebtor: false), default);
        var o = obligaciones.People.Single().Obligations.Single();
        o.CurrentInstallmentInterestToAccrue.Should().Be(porCausar);
        o.TotalBalance.Should().Be(corrido, "el saldo total sigue siendo la deuda: lo de la cuota en curso sólo se debe si se cancela");

        var propuesta = await new Application.Payroll.Settlements.PropuestaDeDescuentosDeCartera(_p.Obligaciones(), _p.G.Reloj,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Application.Payroll.Settlements.PropuestaDeDescuentosDeCartera>.Instance)
            .VivasAsync(_p.D.O.Ana.PublicId, Application.Payroll.Services.DeduccionAlRetiro.SaldoTotal, default);
        var deuda = propuesta.Deudas.Should().ContainSingle().Subject;
        deuda.Proposed.Should().Be(corrido + porCausar);

        var descuento = _p.Pago(ModalidadDePago.FullPayoff, deuda.Proposed, AMitad, clave: $"{ClavesDeRecaudador.Nomina}:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}",
            medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction);
        var r = await _p.AplicarAsync(descuento, recaudador: new Recaudador(ClavesDeRecaudador.Nomina, ClaseDeRecaudador.Payroll));
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        r.Value!.PaidOff.Should().BeTrue("el descuento de la definitiva cancela el crédito con la cuota en curso completa");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Dentro_del_tope_legal_se_cobra_el_corrido_aunque_la_empresa_pida_la_cuota_completa()
    {
        using var p = await PagosDePrueba.CrearAsync();
        CuotaCompleta(p.Db);
        await p.CausarHastaAsync(AMitad);
        var (corrido, _) = await EstadoAsync(p, AMitad);

        var listado = await p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, p.D.O.Ana.PublicId, AMitad), default);
        listado.Value!.Obligations.Single().PayoffAmount.Should().Be(corrido);
        var liquidacion = (await new GetLoanSettlementQueryHandler(p.Db, p.C.Cargador(), p.G.Alcance, p.G.Auditoria, p.G.Reloj, p.Intereses())
            .Handle(new GetLoanSettlementQuery(p.Credito.PublicId, AMitad), default)).Value;
        liquidacion.InterestBeforeDueDate.Should().Be(ParametrosDeCartera.InteresesCorridos);
        liquidacion.CurrentInstallmentInterestToAccrue.Should().BeNull();
        liquidacion.ToPayOffWithCurrentInstallment.Should().BeNull();
        liquidacion.Warnings.Should().Contain(w => w.Contains("Ley 1555"), "la liquidación dice por qué se cobra el corrido");

        var c = await p.AplicadoAsync(p.Pago(ModalidadDePago.FullPayoff, 4_000_000m, AMitad));

        c.Warnings.Should().NotContain(AplicadorDePagos.AvisoInteresDeLaCuotaCompleta);
        c.AppliedAmount.Should().Be(corrido);
        var decision = DecisionDePago.Leer((await p.OperacionAsync(c.OperationPublicId)).Payload)!;
        (decision.InterestBeforeDueDate, decision.InterestBeforeDueDateReason, decision.WithinLegalPrepaymentCap)
            .Should().Be((ParametrosDeCartera.InteresesCorridos, InteresesAlCancelar.MotivoDentroDelTope, true), "Ley 1555 de 2012");
        (await p.Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.UnaccruedInstallmentInterest)).Should().BeFalse();
    }

    [Fact]
    public async Task En_vivienda_y_sin_los_valores_del_tope_se_cobra_el_corrido_y_con_el_parametro_de_fabrica_no_se_sella_nada()
    {
        var intereses = _p.Intereses();

        (await intereses.ModoAsync(_p.Credito, VersionesDeReglas.Vivienda, AMitad, default))
            .Should().Be(new ModoDeInteresesAlCancelar(ParametrosDeCartera.InteresesDeLaCuotaCompleta, ParametrosDeCartera.InteresesCorridos,
                InteresesAlCancelar.MotivoVivienda), "Ley 546 de 1999: la vivienda se prepaga sin penalidad");
        (await intereses.ModoAsync(_p.Credito, VersionesDeReglas.Vigente, AMitad, default)).CuotaCompleta.Should().BeTrue("fuera del tope");

        using var sinTope = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        CuotaCompleta(sinTope.Db);
        (await sinTope.Intereses().ModoAsync(sinTope.Credito, VersionesDeReglas.Vigente, AMitad, default)).Motivo
            .Should().Be(InteresesAlCancelar.MotivoTopeDesconocido, "sin el SMMLV o el tope no se sabe: nunca se cobra de más por un dato que falta");

        using var deFabrica = await PagosDePrueba.CrearAsync();
        (await deFabrica.Intereses().ModoAsync(deFabrica.Credito, VersionesDeReglas.Vigente, AMitad, default)).Should().Be(ModoDeInteresesAlCancelar.Corrido);
        await deFabrica.CausarHastaAsync(AMitad);
        var c = await deFabrica.AplicadoAsync(deFabrica.Pago(ModalidadDePago.FullPayoff, 4_000_000m, AMitad));
        var payload = (await deFabrica.OperacionAsync(c.OperationPublicId)).Payload;
        payload.Should().NotContain("interestBeforeDueDate", "con el corrido de fábrica la decisión se sella como siempre");
    }

    [Fact]
    public async Task Un_pago_sin_el_modo_sellado_se_sigue_reconstruyendo_con_el_corrido_aunque_el_parametro_cambie()
    {
        DecisionDePago.Leer("{\"modality\":\"FullPayoff\",\"received\":100,\"applied\":100,\"valueDate\":\"2026-12-11\"}")!.Contenido().CuotaCompleta
            .Should().BeFalse("un pago anterior no trae el modo: es el corrido");

        using var antes = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        await SembrarTopeAsync(antes, veces: 1m);
        await antes.CausarHastaAsync(AMitad);
        var (corrido, porCausar) = await EstadoAsync(antes, AMitad);
        porCausar.Should().BePositive();
        var c = await antes.AplicadoAsync(antes.Pago(ModalidadDePago.FullPayoff, 4_000_000m, AMitad));
        c.AppliedAmount.Should().Be(corrido, "con el parámetro de fábrica se cobró el corrido");

        CuotaCompleta(antes.Db);
        var cargado = await antes.C.Cargador().CargarAsync(antes.Credito.Id, null, AMitad.AddDays(30), default);
        var despues = MotorDeCartera.Reconstruir(cargado!.Entrada!, AMitad.AddDays(30));
        despues.Pagos.Values.Single(d => d.Modalidad == PaymentModality.FullPayoff).InteresPorCausar
            .Should().BeEmpty("lo que se aplicó con el corrido no se vuelve a juzgar al reconstruir");
        despues.ValorParaCancelar.Should().Be(0m);
        await antes.ElLibroCuadraAsync();
    }
}
