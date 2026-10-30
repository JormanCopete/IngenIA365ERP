using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 015, R1, T0346 (contratos-v2.md §1; R-29; E-01 a; FR-164 a): la versión 2 del contrato de obligaciones, con la partida bancaria citada.
/// <list type="bullet">
/// <item>la partida es de la v2: un recaudador que sólo adoptó la v1 y la manda recibe <c>Integration.Participant.VersionNotSupported</c>;</item>
/// <item>con <c>ValueDate = MovementDate</c> se exime la ventana de <c>Pagos.DiasMaximosFechaValor</c>, pero no el corte de la carga ni el permiso con monto de un mes
/// cerrado; con otra fecha, <c>Lending.Payment.BankItemDateMismatch</c>;</item>
/// <item>la operación guarda <c>BankItemPublicId</c> y la partida entra en la huella; sin partida, la huella es la de la v1;</item>
/// <item>el despliegue implementa las dos versiones (la activación de Cartera sigue aceptando un recaudador con sólo la v1).</item>
/// </list>
/// Prueba escrita, sin correr (estrategia de R1).
/// </summary>
public sealed class ContratoDeObligacionesV2Tests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private PagosDePrueba _p = null!;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private static PartidaBancariaCitada Partida(DateOnly movimiento) => new(Guid.NewGuid(), movimiento, Guid.NewGuid());

    [Fact]
    public async Task Un_recaudador_con_solo_la_v1_que_cita_la_partida_recibe_VersionNotSupported()
    {
        _p.C.Ir(Hoy.AddDays(2));
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m, Hoy.AddDays(1)) with { BankItem = Partida(Hoy.AddDays(1)) });

        r.Code.Should().Be(ErroresDePagos.CodigoVersionNoSoportada);
        r.Data.Should().ContainKey("required");
    }

    [Fact]
    public async Task Con_la_v2_y_la_fecha_del_movimiento_se_exime_la_ventana_y_la_operacion_guarda_la_partida()
    {
        _p.Versiones[PagosDePrueba.Prueba.Code] = 2;
        _p.C.Ir(Hoy.AddDays(25));
        var movimiento = Hoy.AddDays(5);

        var sinPartida = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m, movimiento));
        sinPartida.Code.Should().Be(ErroresDePagos.CodigoFechaValorFueraDeRango, "veinte días atrás pasa la ventana de cinco días");

        var partida = Partida(movimiento);
        var conPartida = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m, movimiento) with { BankItem = partida });
        conPartida.Result.Should().Be(ResultadoDelContrato.Applied, $"{conPartida.Code}: {conPartida.Message}");
        conPartida.Value!.ValueDate.Should().Be(movimiento, "la fecha valor es la del movimiento: sin mora indebida");

        var op = await _p.OperacionAsync(conPartida.Value.OperationPublicId);
        op.BankItemPublicId.Should().Be(partida.BankItemPublicId);
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Con_otra_fecha_que_la_del_movimiento_responde_BankItemDateMismatch()
    {
        _p.Versiones[PagosDePrueba.Prueba.Code] = 2;
        _p.C.Ir(Hoy.AddDays(3));

        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m, Hoy.AddDays(2)) with { BankItem = Partida(Hoy.AddDays(1)) });

        r.Code.Should().Be(ErroresDePagos.CodigoPartidaConOtraFecha);
        r.Data!["movementDate"].Should().Be(Hoy.AddDays(1));
    }

    [Fact]
    public async Task La_partida_no_exime_el_corte_de_la_carga_ni_el_permiso_con_monto_del_mes_cerrado()
    {
        _p.Versiones[PagosDePrueba.Prueba.Code] = 2;
        var corte = (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(_p.Db.LendingSetups)).ActivationDate!.Value.AddDays(-1);
        var antesDelCorte = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, corte) with { BankItem = Partida(corte) });
        antesDelCorte.Code.Should().Be(ErroresDePagos.CodigoAntesDelCorte);

        _p.C.Ir(Hoy.AddDays(3));
        _p.CerradoHasta = Hoy.AddDays(2);
        _p.G.Conceder();
        var mesCerrado = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, Hoy.AddDays(1)) with { BankItem = Partida(Hoy.AddDays(1)) });
        mesCerrado.Code.Should().Be(ErroresDePagos.CodigoFechaValorAntesDelCierre, "el permiso con monto del mes cerrado sigue (contratos-v2.md §1.2)");
    }

    [Fact]
    public void La_partida_entra_en_la_huella_y_sin_ella_la_huella_es_la_de_la_v1()
    {
        var pago = _p.Pago(ModalidadDePago.FreeAmount, 10_000m, Hoy, clave: "TST:huella");
        var v1 = AplicacionDePagos.Huella(PagosDePrueba.Prueba, FormaDeAplicacion.NonInteractive, pago);
        var canonico = HuellaDeOperacion.Canonico(new { payment = HuellasDeLaVersion2.SinPartidaNula(pago, sinPartida: true) });
        canonico.Should().NotContain("bankItem", "un pago sin partida se mide como en la v1: una clave de antes conserva su huella");

        var v2 = AplicacionDePagos.Huella(PagosDePrueba.Prueba, FormaDeAplicacion.NonInteractive, pago with { BankItem = Partida(Hoy) });
        v2.Should().NotBe(v1);
    }

    [Fact]
    public void El_despliegue_implementa_las_dos_versiones_y_la_v1_sigue_admitida()
    {
        IContratoDeObligaciones.VersionDelContrato.Should().Be(2);
        ContratosDeIntegracion.VersionesDelContratoDeObligaciones.Should().Equal(1, 2);
        ContratosDeIntegracion.VersionesSoportadas(ContratosDeIntegracion.ProveedorDeObligaciones).Should().Contain(1,
            "la activación de Cartera sigue aceptando un recaudador con sólo la v1 (FR-164)");
    }
}
