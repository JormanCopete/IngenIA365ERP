using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (E2, T065, T066; HU4, FR-019, FR-020, D1, D2, D13-A): la recogida con la cuota causada según <c>Recogida.CuotaCausada</c> de la línea del crédito
/// que se recoge. De fábrica («Incluir») todo como antes; con «Excluir» se recoge todo menos la cuota del período en curso, que queda tal como se envió y su
/// canal la cierra al peso; con «Preguntar» el asesor elige en la solicitud. El contrato real (<see cref="PagosDePrueba"/>: Ana, 3.000.000 a 24 cuotas desde
/// el 01-12-2026, la cuota 1 vence el 01-01-2027) cobra exactamente lo liquidado; el desembolso deja la cuota en el crédito viejo y la guarda en su fila; el
/// listado del contrato ofrece la modalidad sólo al recaudador y al crédito que la admiten. (nuevo)
/// </summary>
public class RecogidaConCuotaCausadaTests
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private static readonly DateOnly AMitad = Hoy.AddDays(10);
    private static readonly DateOnly VenceLaPrimera = new(2027, 1, 1);

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private static void Cuota(PagosDePrueba p, string valor, int? linea = null)
    {
        p.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.RecogidaCuotaCausada, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedBy = "test", CreatedAt = p.G.UtcNow.AddDays(-10),
            ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine, ScopeId = linea ?? 0,
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
    }

    private static async Task<LiquidacionDeCreditoRecogido> LiquidarAsync(PagosDePrueba p, DateOnly fecha)
    {
        var credito = await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id);
        var r = await p.D.LiquidacionDeRecogida().LiquidarCreditoAsync(credito, fecha, null, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private static async Task<Domain.Lending.Replay.EstadoDelCredito> EstadoAsync(PagosDePrueba p, DateOnly fecha)
    {
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, fecha, default);
        return MotorDeCartera.Reconstruir(cargado!.Entrada!, fecha);
    }

    private static PayoffInput Total(Loan credito, InstallmentTreatment? cuota = null) =>
        new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Total, InstallmentTreatment: cuota);

    private static CreditApplicationInput Recogiendo(PagosDePrueba p, PayoffInput recogida) =>
        p.D.O.Solicitud(monto: 5_000_000m) with { Payoffs = [recogida], PayoffProjection = new PayoffProjectionInput(AMitad) };

    // ------------------------------------------------------------------------------------------------------- liquidación --

    [Fact]
    public async Task De_fabrica_se_incluye_la_cuota_y_pedir_que_se_deje_no_se_admite()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        await p.CausarHastaAsync(AMitad);

        var l = await LiquidarAsync(p, AMitad);

        (l.CuotaCausadaConfigurada, l.TratamientoPorDefecto, l.CuotaElegible).Should().Be((ParametrosDeCartera.CuotaIncluir, InstallmentTreatment.Include, false));
        l.AdmiteDejarLaCuota.Should().BeTrue("a mitad de la cuota 1 hay cuota causada");
        l.Segun(l.TratamientoPorDefecto).Valor.Should().Be(l.ValorARecoger, "de fábrica nada cambia");
        l.ResolverTratamiento(null).Value.Should().Be(InstallmentTreatment.Include);
        l.ResolverTratamiento(InstallmentTreatment.Exclude).Error.Code.Should().Be(ErroresDeRecogida.CodigoTratamientoNoAdmitido);
    }

    [Fact]
    public async Task Con_Excluir_se_recoge_todo_menos_la_cuota_y_su_canal_la_cierra_al_peso()
    {
        // Con el tope del prepago sembrado: la caja que paga la cuota dejada lo consulta al cancelar. 2026-11-27 (D13 ajustada): fuera del tope (1 SMMLV), la
        // cuota queda tal como se envió; dentro, con el proporcional (la prueba siguiente).
        using var p = await PagosDePrueba.CrearAsync(topeSmmlv: 1m);
        Cuota(p, ParametrosDeCartera.CuotaExcluir, p.Credito.CreditLineId);
        await p.CausarHastaAsync(AMitad);
        var estado = await EstadoAsync(p, AMitad);
        var esperado = AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false);

        var l = await LiquidarAsync(p, AMitad);
        var s = l.Segun(l.TratamientoPorDefecto);

        (s.Tratamiento, s.CuotaDejada, s.Valor, s.ValorDejado).Should().Be((InstallmentTreatment.Exclude, (int?)1, esperado.ParaCancelar, (decimal?)esperado.QuedaEnLaCuota));
        s.Desglose.Total.Should().Be(s.Valor, "el desglose cierra al peso con lo que cobra el contrato");
        s.Desglose.Capital.Should().Be(estado.Plan!.Cuotas.Skip(1).Sum(c => c.Capital), "el capital de las cuotas 2 a 24");
        s.Cuota.Should().Be(new Domain.Lending.Payoffs.CuotaCausadaDeRecogida(1, esperado.QuedaEnLaCuota, VenceLaPrimera));
        s.ExplicacionDeLaCuota.Should().Contain("se deja en el crédito").And.Contain("tal como se envió");
        var proyeccion = l.Proyeccion(InstallmentTreatment.Exclude);
        (proyeccion.TratamientoDeCuota, proyeccion.Desglose.Total, proyeccion.CuotaCausada!.Valor)
            .Should().Be((InstallmentTreatment.Exclude, esperado.ParaCancelar, esperado.QuedaEnLaCuota));

        // El contrato, con la modalidad de la recogida, cobra exactamente eso: el crédito queda vivo sólo con la cuota 1.
        var recogida = await p.AplicarAsync(p.Pago(ModalidadDePago.PayoffExceptCurrentInstallment, s.Valor, AMitad,
            clave: RecogidaEnElDesembolso.Clave(Guid.NewGuid(), p.Credito.PublicId), cuota: 1, medio: ClaseDeMedioDeRecaudo.InternalOffset)
            with { Receipt = new ReferenciaDeRecibo("DS-000001", AMitad, null, null, Application.Lending.Common.InteresesAlCancelar.DocumentoDelDesembolso) },
            recaudador: RecogidaEnElDesembolso.Recaudador);
        recogida.Result.Should().Be(ResultadoDelContrato.Applied, $"{recogida.Code}: {recogida.Message}");
        (recogida.Value!.AppliedAmount, recogida.Value.Surplus, recogida.Value.PaidOff).Should().Be((s.Valor, 0m, false));

        // Su canal la paga al vencer por lo que se dejó: el crédito queda cancelado al peso.
        await p.CausarHastaAsync(VenceLaPrimera);
        var cuota = await p.AplicadoAsync(p.Pago(ModalidadDePago.FullPayoff, esperado.QuedaEnLaCuota, VenceLaPrimera));
        (cuota.PaidOff, cuota.Surplus).Should().Be((true, 0m));
        (await p.Db.Loans.AsNoTracking().SingleAsync(x => x.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        await p.ElLibroCuadraAsync();
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T062a; D13 ajustada por el dueño, Ley 1555 de 2012): dentro del tope legal del prepago la cuota se deja con el interés
    /// proporcional: lo que se recoge es lo mismo, lo que queda en la cuota es menos que tal como se envió, el texto lo explica con lo que sobraría, el contrato
    /// cobra exactamente eso y su canal la cierra al peso.
    /// </summary>
    [Fact]
    public async Task Con_Excluir_dentro_del_tope_la_cuota_se_deja_con_el_interes_proporcional()
    {
        using var p = await PagosDePrueba.CrearAsync();
        Cuota(p, ParametrosDeCartera.CuotaExcluir, p.Credito.CreditLineId);
        await p.CausarHastaAsync(AMitad);
        var estado = await EstadoAsync(p, AMitad);
        var esperado = AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false, proporcional: true);
        var tal = AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false);

        var l = await LiquidarAsync(p, AMitad);
        var s = l.Segun(l.TratamientoPorDefecto);

        (l.SalvoCuota!.Regla.Regla, l.SalvoCuota.Regla.Motivo)
            .Should().Be((AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, Application.Lending.Common.InteresesAlCancelar.MotivoDentroDelTope));
        (s.Valor, s.ValorDejado).Should().Be((tal.ParaCancelar, (decimal?)esperado.QuedaEnLaCuota), "lo que se recoge no cambia; lo que queda en la cuota sí");
        (l.SalvoCuota.QuedaTalComoSeEnvio, l.SalvoCuota.DeMasSiSePagaComoSeEnvio).Should().Be((tal.QuedaEnLaCuota, tal.QuedaEnLaCuota - esperado.QuedaEnLaCuota));
        s.ExplicacionDeLaCuota.Should().Contain("interés proporcional").And.Contain("Ley 1555 de 2012").And.NotContain("tal como se envió")
            .And.Contain(ErroresDeRecogida.Pesos(esperado.QuedaEnLaCuota).TrimStart('$'));
        l.Proyeccion(InstallmentTreatment.Exclude).CuotaCausada!.Valor.Should().Be(esperado.QuedaEnLaCuota);

        var recogida = await p.AplicarAsync(p.Pago(ModalidadDePago.PayoffExceptCurrentInstallment, s.Valor, AMitad,
            clave: RecogidaEnElDesembolso.Clave(Guid.NewGuid(), p.Credito.PublicId), cuota: 1, medio: ClaseDeMedioDeRecaudo.InternalOffset)
            with { Receipt = new ReferenciaDeRecibo("DS-000001", AMitad, null, null, Application.Lending.Common.InteresesAlCancelar.DocumentoDelDesembolso) },
            recaudador: RecogidaEnElDesembolso.Recaudador);
        recogida.Result.Should().Be(ResultadoDelContrato.Applied, $"{recogida.Code}: {recogida.Message}");
        (recogida.Value!.AppliedAmount, recogida.Value.Surplus, recogida.Value.PaidOff).Should().Be((s.Valor, 0m, false));

        await p.CausarHastaAsync(VenceLaPrimera);
        var cuota = await p.AplicadoAsync(p.Pago(ModalidadDePago.FullPayoff, esperado.QuedaEnLaCuota, VenceLaPrimera));
        (cuota.PaidOff, cuota.Surplus).Should().Be((true, 0m), "la cuota dejada con el proporcional se cierra al peso");
        await p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Con_Preguntar_el_asesor_elige_y_sin_eleccion_se_incluye()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Cuota(p, ParametrosDeCartera.CuotaPreguntar);
        await p.CausarHastaAsync(AMitad);

        var l = await LiquidarAsync(p, AMitad);

        l.CuotaElegible.Should().BeTrue();
        l.TratamientoPorDefecto.Should().Be(InstallmentTreatment.Include);
        l.ResolverTratamiento(null).Value.Should().Be(InstallmentTreatment.Include);
        l.ResolverTratamiento(InstallmentTreatment.Exclude).Value.Should().Be(InstallmentTreatment.Exclude);
    }

    [Fact]
    public async Task Sin_cuota_causada_a_la_fecha_se_recoge_completo_aunque_la_linea_pida_dejarla()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Cuota(p, ParametrosDeCartera.CuotaExcluir);

        var l = await LiquidarAsync(p, Hoy);

        (l.AdmiteDejarLaCuota, l.SalvoCuota, l.TratamientoPorDefecto).Should().Be((false, (RecogidaSalvoCuota?)null, InstallmentTreatment.Include),
            "el día del desembolso la cuota 1 no ha empezado su período");
        l.RecogidoCompletoPorque.Should().Be(ErroresDeRecogida.MotivoSinCuotaQueDejar);
        l.ResolverTratamiento(null).Value.Should().Be(InstallmentTreatment.Include);
    }

    // ---------------------------------------------------------------------------------------------------- productos (T066) --

    [Fact]
    public async Task Los_productos_dicen_la_cuota_causada_lo_que_cuesta_dejarla_y_con_Preguntar_que_se_elige()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Cuota(p, ParametrosDeCartera.CuotaPreguntar);
        await p.CausarHastaAsync(AMitad);
        var l = await LiquidarAsync(p, AMitad);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListLoansQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ListLoansQueryHandler(p.Db, p.G.Alcance).Handle(ci.Arg<ListLoansQuery>(), ci.Arg<CancellationToken>()));
        var handler = new GetPayoffCandidatesQueryHandler(p.Db, sender, p.D.LiquidacionDeRecogida(), p.D.O.Capacidad(), p.D.Parametros(), p.G.Alcance,
            p.G.Auditoria, p.G.Reloj, Substitute.For<IBlobStore>());

        var r = await handler.Handle(new GetPayoffCandidatesQuery(p.D.O.Ana.PublicId, ProjectedDisbursementDate: AMitad,
            Selected: [Total(p.Credito, InstallmentTreatment.Exclude)], NewLoan: new PayoffNewLoanInput(5_000_000m)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var producto = r.Value.Products.Single(x => x.ProductPublicId == p.Credito.PublicId);
        var cuota = producto.CurrentInstallment!;
        (cuota.Number, cuota.Choosable, cuota.Treatment, cuota.Parameter).Should().Be((1, true, InstallmentTreatment.Exclude, ParametrosDeCartera.CuotaPreguntar),
            "marcado dejándola, lo marcado se muestra así");
        cuota.Left.Should().Be(l.SalvoCuota!.QuedaEnLaCuota);
        // 2026-11-27 (D13 ajustada): la regla con que se deja y lo que sería tal como se envió.
        (cuota.LeftRule, cuota.LeftAsSent).Should().Be((AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, (decimal?)l.SalvoCuota.QuedaTalComoSeEnvio));
        (cuota.IncludedExplanation, cuota.LeftExplanation).Should().NotBe((null, null));
        producto.ToPayOffExcludingCurrentInstallment.Should().Be(l.SalvoCuota.ValorARecoger);
        producto.SelectedAmount.Should().Be(l.SalvoCuota.ValorARecoger, "con «Preguntar» y la cuota dejada se recoge todo menos ella");
        producto.SelectedInstallmentTreatment.Should().Be(InstallmentTreatment.Exclude);
        r.Value.Summary.NetToDisburse.Should().Be(5_000_000m - l.SalvoCuota.ValorARecoger);
    }

    // ---------------------------------------------------------------------------------------------------- solicitud (T066) --

    [Fact]
    public async Task La_solicitud_guarda_la_eleccion_con_Preguntar_y_no_la_admite_con_Incluir()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var incluir = await p.D.O.Crear().Handle(new CreateCreditApplicationCommand(Recogiendo(p, Total(p.Credito, InstallmentTreatment.Exclude)))
            { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        incluir.Error.Code.Should().Be(ErroresDeRecogida.CodigoTratamientoNoAdmitido, "de fábrica la cuota causada no la elige el asesor");

        Cuota(p, ParametrosDeCartera.CuotaPreguntar);
        var s = await p.D.O.Crear().Handle(new CreateCreditApplicationCommand(Recogiendo(p, Total(p.Credito, InstallmentTreatment.Exclude)))
            { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();

        s.IsSuccess.Should().BeTrue(s.IsFailure ? $"{s.Error.Code}: {s.Error.Message}" : string.Empty);
        var fila = await p.Db.CreditApplicationPayoffs.AsNoTracking().SingleAsync(f => !f.IsDeleted);
        var l = await LiquidarAsync(p, AMitad);
        (fila.InstallmentTreatment, fila.CurrentInstallmentNumber, fila.CurrentInstallmentAmount, fila.ProjectedAmount)
            .Should().Be(((InstallmentTreatment?)InstallmentTreatment.Exclude, (int?)1, (decimal?)l.SalvoCuota!.QuedaEnLaCuota, l.SalvoCuota.ValorARecoger));
        var recogida = s.Value.Payoffs.Should().ContainSingle().Subject;
        recogida.CurrentInstallment!.Treatment.Should().Be(InstallmentTreatment.Exclude);
        recogida.CurrentInstallment.Explanation.Should().Contain("se deja en el crédito");
    }

    // ---------------------------------------------------------------------------------------------------- desembolso (T066) --

    [Fact]
    public async Task El_desembolso_con_Excluir_deja_la_cuota_en_el_credito_viejo_y_la_guarda_en_su_fila()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        Cuota(p, ParametrosDeCartera.CuotaExcluir);
        var s = await p.D.AprobadaAsync(Recogiendo(p, Total(p.Credito)));
        await p.CausarHastaAsync(AMitad);
        var l = await LiquidarAsync(p, AMitad);

        var vista = (await p.D.VistaPrevia().Handle(new PreviewDisbursementQuery(p.D.Entrada(s)), default)).Value;
        p.Db.ChangeTracker.Clear();
        var r = await p.D.DesembolsadoAsync(p.D.Entrada(s) with { AcknowledgedPayoffDifferences = true }, p.Contrato());

        var propuesta = vista.Payoffs.Single();
        (propuesta.Mode, propuesta.ToApply, propuesta.CurrentInstallment!.Treatment, propuesta.CurrentInstallment.Left)
            .Should().Be(("Total", l.SalvoCuota!.ValorARecoger, "Exclude", (decimal?)l.SalvoCuota.QuedaEnLaCuota));
        propuesta.CurrentInstallment.Explanation.Should().Contain("se deja en el crédito");
        // 2026-11-27 (D13 ajustada): sin los valores nacionales para saber el tope del prepago, la cuota se deja con el proporcional, y la vista previa lo dice.
        (propuesta.CurrentInstallment.LeftRule, propuesta.CurrentInstallment.LeftRuleReason)
            .Should().Be((AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional, Application.Lending.Common.InteresesAlCancelar.MotivoTopeDesconocido));
        propuesta.CurrentInstallment.LeftAsSent.Should().BeGreaterThan(propuesta.CurrentInstallment.Left!.Value);
        propuesta.CurrentInstallment.Explanation.Should().Contain("interés proporcional");
        var aplicada = r.Payoffs.Single();
        (aplicada.Applied, aplicada.LeftInstallment, aplicada.LeftInstallmentNumber).Should().Be((l.SalvoCuota.ValorARecoger, (decimal?)l.SalvoCuota.QuedaEnLaCuota, (int?)1));
        r.NetToDisburse.Should().Be(5_000_000m - l.SalvoCuota.ValorARecoger);
        (await p.Db.Loans.AsNoTracking().SingleAsync(x => x.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.Active, "queda vivo sólo con la cuota 1");
        (await p.OperacionAsync(aplicada.ReceiptOperationPublicId)).PaymentModality.Should().Be(PaymentModality.PayoffExceptCurrentInstallment);
        var fila = await p.Db.DisbursementPayoffs.AsNoTracking().SingleAsync();
        (fila.InstallmentTreatment, fila.LeftInstallmentNumber, fila.LeftInstallmentAmount, fila.LeftInstallmentSettledAt)
            .Should().Be(((InstallmentTreatment?)InstallmentTreatment.Exclude, (int?)1, (decimal?)l.SalvoCuota.QuedaEnLaCuota, (DateTime?)null));
        fila.CuotaDejadaPendiente.Should().BeTrue();
        var vinculo = (await VinculosDeRecogida.DeLosRecogidosAsync(p.Db, [p.Credito.Id], default)).Single();
        (vinculo.CuotaDejada, vinculo.ValorDejado).Should().Be(((int?)1, (decimal?)l.SalvoCuota.QuedaEnLaCuota));
        await p.ElLibroCuadraAsync();
    }

    // ------------------------------------------------------------------------------------------------- listado (T065) --

    [Fact]
    public async Task El_listado_ofrece_dejar_la_cuota_solo_al_recaudador_y_al_credito_que_la_admiten()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);

        var alGirar = await p.Contrato().ListarAsync(new SolicitudDeListado(RecogidaEnElDesembolso.Recaudador, p.D.O.Ana.PublicId, Hoy), default);
        await p.CausarHastaAsync(AMitad);
        var aMitad = await p.Contrato().ListarAsync(new SolicitudDeListado(RecogidaEnElDesembolso.Recaudador, p.D.O.Ana.PublicId, AMitad), default);
        var prueba = await p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, p.D.O.Ana.PublicId, AMitad), default);

        Del(alGirar).AdmittedModalities.Should().NotContain(ModalidadDePago.PayoffExceptCurrentInstallment, "el día del desembolso no hay cuota causada");
        Del(aMitad).AdmittedModalities.Should().Contain(ModalidadDePago.PayoffExceptCurrentInstallment);
        Del(prueba).AdmittedModalities.Should().NotContain(ModalidadDePago.PayoffExceptCurrentInstallment, "el recaudador de prueba no la admite");
        Del(aMitad).SentInstallment.Should().BeNull("no hay nada enviado");

        ObligacionListadaDto Del(RespuestaDelContratoDto<ListadoDeObligacionesDto> r)
        {
            r.Result.Should().Be(ResultadoDelContrato.Answered, r.Message);
            return r.Value!.Obligations.Single(o => o.ObligationPublicId == p.Credito.PublicId);
        }
    }

    [Fact]
    public async Task El_listado_dice_la_cuota_enviada_a_la_pagaduria()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        await p.CausarHastaAsync(AMitad);
        var pagaduria = p.D.O.Persona("Pagaduría", "Municipal", "800100300", null, null);
        var convenio = new Domain.Entities.Lending.Payroll.PayrollAgreement { Code = "MUN", PaymasterPersonId = pagaduria.Id, CreatedBy = "test" };
        p.Db.PayrollAgreements.Add(convenio);
        p.Db.SaveChanges();
        var archivo = new Domain.Entities.Lending.Payroll.PayrollDeductionFile
        {
            PayrollAgreementId = convenio.Id, PayrollAgreementVersionId = 1, PeriodYear = 2027, PeriodMonth = 1, Number = "MUN-000001", OutboundFormatId = 1,
            FileName = "MUN-000001.txt", FileSha256 = "x", GeneratedAt = p.G.UtcNow, CreatedBy = "test",
        };
        archivo.MarcarEnviado(p.G.UtcNow, Guid.NewGuid(), null);
        p.Db.PayrollDeductionFiles.Add(archivo);
        p.Db.SaveChanges();
        p.Db.PayrollDeductionFileLines.Add(new Domain.Entities.Lending.Payroll.PayrollDeductionFileLine
        {
            PayrollDeductionFileId = archivo.Id, LineNumber = 1, LoanId = p.Credito.Id, LoanPayrollLinkId = 1, PersonId = p.Credito.DebtorPersonId,
            InstallmentNumber = 1, DueDate = VenceLaPrimera, CurrentAmount = 150_000m, TotalAmount = 150_000m, LibranzaDate = Hoy, CreatedBy = "test",
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();

        var r = await p.Contrato().ListarAsync(new SolicitudDeListado(RecogidaEnElDesembolso.Recaudador, p.D.O.Ana.PublicId, AMitad), default);

        var enviada = r.Value!.Obligations.Single(o => o.ObligationPublicId == p.Credito.PublicId).SentInstallment;
        enviada.Should().NotBeNull();
        (enviada!.State, enviada.InstallmentNumber, enviada.File, enviada.Agreement, enviada.TotalAmount)
            .Should().Be((EstadoDeCuotaEnviada.SentWithoutResponse, (int?)1, "MUN-000001", "MUN", (decimal?)150_000m));
        enviada.Explanation.Should().Contain("ya se envió a la pagaduría");
    }
}
