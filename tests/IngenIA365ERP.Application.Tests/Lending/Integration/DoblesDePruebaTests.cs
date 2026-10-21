using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US5, T0525 (api.md §20; mensajes.md §18; FR-089, T20, C12, C35): el recaudador de prueba llama al contrato con el recaudador <c>TST</c> de
/// clase <c>Test</c>, aplica con la clave <c>TST:{guid:N}</c> y acepta una repetida, reversa por la clave y resuelve saldos; consume
/// <c>PagoReversadoPorCartera</c> sin estado propio (responde <c>Processed</c>) y lista lo recibido desde la bandeja. Sin el doble registrado, el 404
/// genérico. Los dos dobles se registran sólo fuera de producción y el escritor del registro rechaza las filas <c>TST</c> en producción.
/// </summary>
public class DoblesDePruebaTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private readonly IReadOnlyList<IDestinoDeMensajes> _registrados = [new RecaudadorDePrueba(), new TesoreriaDePrueba()];

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private ApplyTestCollectorPaymentCommandHandler Aplicar(bool registrado = true) => new(_p.Db, _p.Contrato(), registrado ? _registrados : []);

    [Fact]
    public async Task El_recaudador_de_prueba_aplica_con_TST_clase_Test_y_acepta_la_clave_repetida()
    {
        var clave = RecaudadorDePrueba.Clave(Guid.NewGuid());
        clave.Should().MatchRegex("^TST:[0-9a-f]{32}$");
        var pago = _p.Pago(ModalidadDePago.FreeAmount, 50_000m, clave: clave);

        var r = await Aplicar().Handle(new ApplyTestCollectorPaymentCommand([pago], Interactive: false) { OperationKey = Guid.NewGuid() }, default);
        _p.Db.ChangeTracker.Clear();
        var otra = await Aplicar().Handle(new ApplyTestCollectorPaymentCommand([pago], Interactive: false) { OperationKey = Guid.NewGuid() }, default);

        r.Value.Payments.Should().ContainSingle().Which.Result.Should().Be(ResultadoDelContrato.Applied);
        otra.Value.Payments.Should().ContainSingle().Which.Result.Should().Be(ResultadoDelContrato.AlreadyApplied, "C35: la pantalla deja repetir la clave");
        var op = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.IdempotencyKey == clave);
        (op.CollectorCode, op.CollectorClass).Should().Be(("TST", "Test"));
    }

    [Fact]
    public async Task Reversa_por_la_clave_y_resuelve_saldos_por_el_contrato()
    {
        var clave = RecaudadorDePrueba.Clave(Guid.NewGuid());
        await _p.AplicadoAsync(_p.Pago(ModalidadDePago.Advance, 80_000m, clave: clave));

        var resuelto = await new ResolveTestCollectorCreditBalanceCommandHandler(_p.Db, _p.Contrato(), _registrados, _p.G.Reloj).Handle(
            new ResolveTestCollectorCreditBalanceCommand(_p.Credito.PublicId, FormaDeResolucion.RefundThroughCollector, RecaudadorDePrueba.Clave(Guid.NewGuid()),
                Concept: ConceptoDeObligacion.Advance) { OperationKey = Guid.NewGuid() }, default);
        resuelto.Value.Result.Should().Be(ResultadoDelContrato.Applied, resuelto.Value.Message);
        resuelto.Value.Value!.Amount.Should().Be(80_000m, "sin valor, todo lo disponible del concepto");
        _p.Db.ChangeTracker.Clear();

        var otraClave = RecaudadorDePrueba.Clave(Guid.NewGuid());
        await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m, clave: otraClave));
        var reversado = await new ReverseTestCollectorPaymentCommandHandler(_p.Db, _p.Contrato(), _registrados).Handle(
            new ReverseTestCollectorPaymentCommand(otraClave, "Prueba de reverso") { OperationKey = Guid.NewGuid() }, default);
        reversado.Value.Result.Should().Be(ResultadoDelContrato.Applied, reversado.Value.Message);
    }

    [Fact]
    public async Task Lista_y_simula_por_el_contrato()
    {
        var lista = await new ListTestCollectorObligationsQueryHandler(_p.Contrato(), _registrados, _p.G.Reloj)
            .Handle(new ListTestCollectorObligationsQuery(_p.D.O.Ana.PublicId), default);
        lista.Value.Result.Should().Be(ResultadoDelContrato.Answered);
        lista.Value.Value!.Obligations.Should().ContainSingle();

        var simulada = await new SimulateTestCollectorPaymentQueryHandler(_p.Contrato(), _registrados)
            .Handle(new SimulateTestCollectorPaymentQuery(_p.Credito.PublicId, ModalidadDePago.FullPayoff, 3_000_000m, PagosDePrueba.Hoy), default);
        simulada.Value.Result.Should().Be(ResultadoDelContrato.Answered);
    }

    [Fact]
    public async Task Ve_los_PagoReversadoPorCartera_que_recibe_y_los_consume_sin_estado_propio()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 70_000m));
        var vista = await _p.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_p.Credito.PublicId, c.OperationPublicId), default);
        (await _p.Reversar().Handle(new ReverseLoanPaymentCommand(_p.Credito.PublicId, c.OperationPublicId, "Cheque devuelto", vista.Value.PreviewSha256)
            { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
        _p.Db.ChangeTracker.Clear();

        var recibidos = await new ListTestCollectorReversalsQueryHandler(_p.Db, _registrados).Handle(new ListTestCollectorReversalsQuery(), default);

        var aviso = recibidos.Value.Should().ContainSingle().Subject;
        (aviso.Reason, aviso.ReversedAmount, aviso.PaymentNumber).Should().Be(("Cheque devuelto", 70_000m, c.Number));
        aviso.DeliveryStatus.Should().NotBeNull();

        var doble = new RecaudadorDePrueba();
        doble.Destino.Should().Be(IntegrationDestinations.Receipts);
        doble.Acepta(PagoReversadoPorCarteraV1.Type, 1).Should().BeTrue();
        doble.Acepta(PagoReversadoPorCarteraV1.Type, 2).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_el_doble_registrado_cada_operacion_responde_el_404_generico()
    {
        (await Aplicar(registrado: false).Handle(new ApplyTestCollectorPaymentCommand([_p.Pago(ModalidadDePago.FreeAmount, 1m)], false), default))
            .Error.Code.Should().Be("Generic.NotFound");
        (await new ListTestCollectorReversalsQueryHandler(_p.Db, [new TesoreriaDePrueba()]).Handle(new ListTestCollectorReversalsQuery(), default))
            .Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public void Los_dos_dobles_se_registran_solo_fuera_de_produccion()
    {
        var servicios = new ServiceCollection();
        DoblesDePruebaDeCarteraEnDi.Registrar(servicios, pedidos: true, esProduccion: false).Should().BeTrue();
        servicios.Where(s => s.ServiceType == typeof(IDestinoDeMensajes)).Select(s => s.ImplementationType)
            .Should().BeEquivalentTo([typeof(TesoreriaDePrueba), typeof(RecaudadorDePrueba)]);

        var enProduccion = () => DoblesDePruebaDeCarteraEnDi.Registrar(new ServiceCollection(), pedidos: true, esProduccion: true);
        enProduccion.Should().Throw<InvalidOperationException>().WithMessage("*producción*");
    }

    [Fact]
    public async Task El_escritor_del_registro_rechaza_las_filas_TST_en_produccion()
    {
        var ambiente = Substitute.For<IAmbienteDeDespliegue>();
        ambiente.EsProduccion.Returns(true);
        var escritor = new EscritorDelRegistroDeIntegracion(_p.Db, ambiente, _p.G.Actor, _p.G.Reloj);

        var r = await escritor.RegistrarAsync(new NuevoParticipante("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Receipts, true, true, [], PagosDePrueba.Hoy, "Recaudador de prueba"), default);

        r.Error.Code.Should().Be("Integration.Participant.TestDoubleInProduction");
    }
}
