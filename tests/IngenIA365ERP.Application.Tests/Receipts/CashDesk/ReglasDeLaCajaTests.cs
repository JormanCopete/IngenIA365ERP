using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// 2026-11-16 (pedido del dueño): las reglas de la caja que el servidor vuelve a validar al guardar los ítems y al confirmar (<see cref="ReglasDeLaCaja"/>):
/// <list type="bullet">
/// <item>con algo exigible y <c>Caja.ConMoraSoloExigible</c> (de fábrica Sí), sólo la cancelación, lo exigible o la cuota específica de una cuota exigible;</item>
/// <item>sin nada exigible y <c>Caja.AnticipoMinimoUnaCuota</c> (de fábrica Sí), la cuota del ciclo completa y la cuota específica futura por lo que cuesta
/// anticiparla;</item>
/// <item>la modalidad que el crédito no admite y el crédito bloqueado se rechazan (antes sólo los respetaba la pantalla);</item>
/// <item>con <c>Caja.PrelacionPorClase</c>, nada de una clase posterior mientras lo exigible de una anterior no quede cubierto;</item>
/// <item>F4 entra por lo exigible y, con una factura entre los ítems, ya no falla por la consulta de Contabilidad.</item>
/// </list>
/// </summary>
public sealed class ReglasDeLaCajaTests
{
    private static readonly DateOnly Hoy = EscenarioDeRecaudo.Hoy;
    private static readonly ModalidadDePago[] Todas = Enum.GetValues<ModalidadDePago>();

    private readonly CajaConContratos _k = new();
    private readonly IContabilidadParaRecaudo _contabilidad = Substitute.For<IContabilidadParaRecaudo>();
    private readonly Guid _venta = Guid.NewGuid();

    public ReglasDeLaCajaTests()
    {
        _contabilidad.CuentasPorCobrarComercialesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([new CuentaPorCobrarComercialDto(_venta, "FV-100", Hoy.AddDays(-20), 150_000m)]);
    }

    private static ObligacionListadaDto Obligacion(Guid id, string numero, decimal exigible, decimal cuota = 100_000m, decimal anticipo = 0m,
        IReadOnlyList<ModalidadDePago>? admitidas = null, string? bloqueo = null) =>
        new(id, numero, "CONLIB", "Consumo", RolEnLaObligacion.Debtor, EstadoDeObligacion.Active, exigible > 0m ? 15 : 0, exigible, exigible + cuota, exigible,
            2_000_000m, anticipo, 0m, admitidas ?? Todas, bloqueo is null ? [] : [bloqueo], false, false);

    private void ConObligaciones(params ObligacionListadaDto[] obligaciones) =>
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "",
                new ListadoDeObligacionesDto(EstadoDeCartera.Active, ci.Arg<SolicitudDeListado>().PersonPublicId, Hoy, obligaciones)));

    /// <summary>El detalle de un crédito: la cuota 1 vencida y la 3 por vencer, que cuesta 98.500 anticipar.</summary>
    private void ConDetalle(Guid credito) =>
        _k.Obligaciones.DetalleAsync(Arg.Is<SolicitudDeDetalle>(s => s.ObligationPublicId == credito), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<DetalleDeObligacionDto>(ResultadoDelContrato.Answered, "OK", "", new DetalleDeObligacionDto(credito, "CR-A", "Consumo", "Vigente",
                15, 120_000m, 120_000m, 2_000_000m, 0m, 0m, 100_000m, Hoy.AddDays(20), [],
                [
                    new CuotaDeObligacionDto(1, Hoy.AddDays(-15), "PastDue", 80_000m, 20_000m, 0m, 0m, 100_000m, 120_000m, 0m, 0m),
                    new CuotaDeObligacionDto(2, Hoy.AddDays(20), "Upcoming", 81_000m, 19_000m, 0m, 0m, 100_000m, 100_000m, 0m, 100_000m),
                    new CuotaDeObligacionDto(3, Hoy.AddDays(50), "Upcoming", 82_000m, 16_000m, 500m, 0m, 98_500m, 98_500m, 0m, 98_500m),
                ], Todas, true, [])));

    private UpdateCashOperationItemsCommandHandler Items() => new(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, contabilidad: _contabilidad,
        parametros: _k.C.E.Parametros);

    private async Task<Guid> OperacionAsync()
    {
        await _k.AbrirAsync();
        return (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
    }

    private async Task<Result<CashOperationDto>> GuardarAsync(Guid op, params ItemPedidoDto[] items) =>
        await Items().Handle(new UpdateCashOperationItemsCommand(op, items) { OperationKey = Guid.NewGuid() }, default);

    private static ItemPedidoDto Credito(Guid credito, ModalidadDePago modalidad, decimal valor, int? cuota = null, EleccionDeAbono? eleccion = null) =>
        new(ApplicationTarget.Loan, valor, credito, LoanModality: modalidad, InstallmentNumber: cuota, Choice: eleccion);

    private static string Rechazos(Result<CashOperationDto> r)
    {
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        return JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data);
    }

    // ---------------------------------------------------------------------------------------------------- con mora --

    [Fact]
    public async Task Con_algo_exigible_solo_se_paga_lo_exigible_una_cuota_exigible_o_la_cancelacion()
    {
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 120_000m));
        ConDetalle(_k.CreditoA);
        var op = await OperacionAsync();

        var cuotaDelCiclo = await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.CycleInstallment, 220_000m));
        Rechazos(cuotaDelCiclo).Should().Contain(ErroresDeRecaudo.CodigoSoloLoExigible).And.Contain("CR-A");
        Rechazos(await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.ExtraordinaryPrincipal, 500_000m, eleccion: EleccionDeAbono.ReduceTerm)))
            .Should().Contain(ErroresDeRecaudo.CodigoSoloLoExigible);
        Rechazos(await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.SpecificInstallment, 98_500m, cuota: 3)))
            .Should().Contain(ErroresDeRecaudo.CodigoSoloLoExigible, "la cuota 3 todavía no vence: con mora no se anticipa");

        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.PastDue, 120_000m))).IsSuccess.Should().BeTrue();
        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.FullPayoff, 2_000_000m))).IsSuccess.Should().BeTrue();
        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.SpecificInstallment, 120_000m, cuota: 1))).IsSuccess
            .Should().BeTrue("la cuota 1 está vencida: es exigible");
    }

    [Fact]
    public async Task Sin_la_regla_de_mora_se_admiten_las_demas_modalidades()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaConMoraSoloExigible, "false");
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 120_000m));
        var op = await OperacionAsync();

        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.CycleInstallment, 220_000m))).IsSuccess.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------------- sin mora: una cuota --

    [Fact]
    public async Task Sin_nada_exigible_la_cuota_del_ciclo_va_completa_menos_lo_ya_anticipado()
    {
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 0m, cuota: 100_000m, anticipo: 10_000m));
        var op = await OperacionAsync();

        Rechazos(await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.CycleInstallment, 80_000m)))
            .Should().Contain(ErroresDeRecaudo.CodigoMenosDeUnaCuota);
        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.CycleInstallment, 90_000m))).IsSuccess.Should().BeTrue("100.000 menos los 10.000 anticipados");
        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.ExtraordinaryPrincipal, 10_000m, eleccion: EleccionDeAbono.ReduceTerm))).IsSuccess
            .Should().BeTrue("el abono a capital no tiene mínimo");
        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.FreeAmount, 5_000m))).IsSuccess.Should().BeTrue("el valor libre tampoco");
    }

    [Fact]
    public async Task Anticipar_una_cuota_futura_exige_lo_que_cuesta_anticiparla_completa()
    {
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 0m));
        ConDetalle(_k.CreditoA);
        var op = await OperacionAsync();

        var parte = await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.SpecificInstallment, 50_000m, cuota: 3));
        Rechazos(parte).Should().Contain(ErroresDeRecaudo.CodigoMenosDeUnaCuota).And.Contain("cuota 3");
        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.SpecificInstallment, 98_500m, cuota: 3),
            Credito(_k.CreditoA, ModalidadDePago.SpecificInstallment, 100_000m, cuota: 2))).IsSuccess.Should().BeTrue("una por ítem, cada una completa");
    }

    [Fact]
    public async Task Sin_la_regla_del_minimo_se_paga_una_parte_de_la_cuota()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaAnticipoMinimoUnaCuota, "false");
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 0m));
        var op = await OperacionAsync();

        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.CycleInstallment, 30_000m))).IsSuccess.Should().BeTrue();
    }

    // --------------------------------------------------------------------------------- modalidades y bloqueos --

    [Fact]
    public async Task La_modalidad_que_el_credito_no_admite_y_el_credito_bloqueado_se_rechazan_en_el_servidor()
    {
        ConObligaciones(
            Obligacion(_k.CreditoA, "CR-A", exigible: 0m, admitidas: [ModalidadDePago.FullPayoff, ModalidadDePago.PastDue, ModalidadDePago.CycleInstallment]),
            Obligacion(_k.CreditoB, "CR-B", exigible: 50_000m, bloqueo: "WrittenOff"));
        var op = await OperacionAsync();

        Rechazos(await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.FreeAmount, 10_000m))).Should().Contain(ErroresDeRecaudo.CodigoModalidadNoAdmitida);
        Rechazos(await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.SpecificInstallment, 10_000m, cuota: 2)))
            .Should().Contain(ErroresDeRecaudo.CodigoModalidadNoAdmitida, "la línea no deja pagar una cuota específica");
        Rechazos(await GuardarAsync(op, Credito(_k.CreditoB, ModalidadDePago.PastDue, 50_000m)))
            .Should().Contain(ErroresDeRecaudo.CodigoObligacionBloqueada).And.Contain("castigado");
        await _k.Obligaciones.DidNotReceive().SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------------------------- prelación --

    [Fact]
    public async Task Con_prelacion_nada_de_una_clase_posterior_mientras_lo_exigible_anterior_no_quede_cubierto()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaPrelacionPorClase, "Aportes, Creditos");
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 0m));
        _k.Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "OK", "", new ExigiblesDeCuentasDto(EstadoDeCartera.Active,
                EstadoDeCartera.Active,
                [
                    new ExigibleDeCuentaDto(_k.Asociado.PublicId, _k.Aportes, "AP-1", NaturalezaDeCuenta.Contribution, null, "APORTES", Guid.NewGuid(), "2026-11",
                        Hoy.AddDays(-5), 40_000m, 40_000m, false, "Saldo", "DESC_APORTES", 1),
                ], [])));
        var op = await OperacionAsync();
        var credito = Credito(_k.CreditoA, ModalidadDePago.FreeAmount, 50_000m);
        var concepto = new ItemPedidoDto(ApplicationTarget.MiscConcept, 5_000m, MiscConceptPublicId: _k.Concepto.PublicId);

        var sinAportes = Rechazos(await GuardarAsync(op, credito, concepto));
        sinAportes.Should().Contain(ErroresDeRecaudo.CodigoPrelacionNoRespetada).And.Contain("aportes").And.Contain("40");
        Rechazos(await GuardarAsync(op, _k.CuotaDeAportes(20_000m), credito)).Should().Contain(ErroresDeRecaudo.CodigoPrelacionNoRespetada,
            "veinte mil no cubren los cuarenta mil exigibles de aportes");

        (await GuardarAsync(op, _k.CuotaDeAportes(40_000m), credito, concepto)).IsSuccess.Should().BeTrue("los aportes quedan cubiertos");
    }

    [Fact]
    public async Task Sin_prelacion_el_orden_es_libre()
    {
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 0m));
        var op = await OperacionAsync();

        (await GuardarAsync(op, Credito(_k.CreditoA, ModalidadDePago.FreeAmount, 50_000m))).IsSuccess.Should().BeTrue();
        await _k.Cuentas.DidNotReceive().ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------------- lo que puede pagar --

    [Fact]
    public async Task Lo_que_puede_pagar_trae_las_reglas_vigentes_de_la_caja()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaPrelacionPorClase, "Aportes,Creditos,Facturas");
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaConMoraSoloExigible, "false");
        var pagables = new GetPayablesQueryHandler(_k.C.E.Db, _k.Obligaciones, _k.Cuentas, _k.Busqueda, _k.C.E.Reloj, new AuditoriaDeRecaudo(_k.C.E.Servicios,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditoriaDeRecaudo>.Instance), _k.C.E.Parametros);

        var r = await pagables.Handle(new GetPayablesQuery(_k.Asociado.PublicId), default);

        r.Value.PriorityClasses.Should().Equal("Aportes", "Creditos", "Facturas");
        r.Value.OverdueOnlyDue.Should().BeFalse();
        r.Value.MinimumOneInstallment.Should().BeTrue("de fábrica Sí");
        (await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId), default)).Value.PriorityClasses.Should().BeEmpty("vacía de fábrica: libre");
    }

    [Fact]
    public void La_prelacion_se_lee_con_su_nombre_canonico_sin_repetir_y_el_patron_rechaza_las_repetidas()
    {
        PoliticaDeCaja.Clases(" aportes , Creditos,Otra,Aportes").Should().Equal("Aportes", "Creditos");
        var definicion = ParametrosDeRecaudoYTesoreria.Definiciones.Single(d => d.Clave == ParametrosDeRecaudoYTesoreria.CajaPrelacionPorClase);
        definicion.Interpretar("Aportes, Creditos, Facturas").Admitido.Should().BeTrue();
        definicion.Interpretar(string.Empty).Admitido.Should().BeTrue("vacía = libre");
        definicion.Interpretar("Aportes,Creditos,Aportes").Admitido.Should().BeFalse("sin repetir");
        definicion.Interpretar("Aportes,Cartera").Admitido.Should().BeFalse("sólo las cinco clases");
    }

    // ------------------------------------------------------------------------------------------------------- F4 --

    [Fact]
    public async Task F4_con_una_factura_entre_los_items_ya_no_falla_por_la_consulta_de_Contabilidad()
    {
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 120_000m));
        var op = await OperacionAsync();
        (await GuardarAsync(op, new ItemPedidoDto(ApplicationTarget.CommercialReceivable, 150_000m, CrossDocument: "FV-100"))).IsSuccess.Should().BeTrue();

        var r = await new PreselectPayablesCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, contabilidad: _contabilidad,
                parametros: _k.C.E.Parametros)
            .Handle(new PreselectPayablesCommand(op) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Items.Should().ContainSingle(i => i.Target == ApplicationTarget.CommercialReceivable, "la factura que ya estaba se conserva");
        r.Value.Items.Should().ContainSingle(i => i.ProductPublicId == _k.CreditoA).Which.LoanModality.Should().Be(ModalidadDePago.PastDue);
    }

    [Fact]
    public async Task F4_con_las_facturas_en_la_prelacion_las_trae_para_no_rechazarse_a_si_mismo()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaPrelacionPorClase, "Facturas,Creditos");
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 120_000m));
        var op = await OperacionAsync();

        var r = await new PreselectPayablesCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, contabilidad: _contabilidad,
                parametros: _k.C.E.Parametros)
            .Handle(new PreselectPayablesCommand(op) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Items.Should().ContainSingle(i => i.Target == ApplicationTarget.CommercialReceivable && i.CrossDocument == "FV-100" && i.Amount == 150_000m);
    }

    // ------------------------------------------------------------------------------------------- al confirmar --

    [Fact]
    public async Task Al_confirmar_las_reglas_se_vuelven_a_validar()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([Credito(_k.CreditoA, ModalidadDePago.CycleInstallment, 100_000m)], [_k.Efectivo(100_000m)]);
        // Entre los dos pasos el crédito cayó en mora.
        ConObligaciones(Obligacion(_k.CreditoA, "CR-A", exigible: 120_000m));

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data).Should().Contain(ErroresDeRecaudo.CodigoSoloLoExigible);
        await _k.Obligaciones.DidNotReceive().AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------------------------------ el detalle --

    [Fact]
    public async Task El_detalle_de_la_obligacion_sale_del_contrato_y_sin_Cartera_dice_que_no_esta_disponible()
    {
        ConDetalle(_k.CreditoA);
        var consulta = new GetPayableObligationDetailQueryHandler(_k.Obligaciones, _k.C.E.Reloj);

        var r = await consulta.Handle(new GetPayableObligationDetailQuery(_k.CreditoA), default);

        r.Value.Installments.Should().HaveCount(3);
        r.Value.Installments.Single(q => q.Number == 3).ValueToAnticipate.Should().Be(98_500m);
        await _k.Obligaciones.Received().DetalleAsync(Arg.Is<SolicitudDeDetalle>(s => s.Date == Hoy && s.Collector.Code == "REC"), Arg.Any<CancellationToken>());

        var sinCartera = await new GetPayableObligationDetailQueryHandler(new ContratoDeObligacionesSinActivar(), _k.C.E.Reloj)
            .Handle(new GetPayableObligationDetailQuery(_k.CreditoB), default);
        sinCartera.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDetalleNoDisponible);
    }
}
