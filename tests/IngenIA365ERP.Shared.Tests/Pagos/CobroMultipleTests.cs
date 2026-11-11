using FluentAssertions;
using IngenIA365ERP.Shared.Services.Pagos;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Pagos;

/// <summary>Feature 022 (T062; FR-040 a FR-048): el cobro con varios medios, igual en ventas y en Recaudo.</summary>
public class CobroMultipleTests
{
    private static MedioDisponible Medio(int clase, bool vueltas = false, bool referencia = false, IReadOnlyList<OpcionDeMedio>? datafonos = null,
        IReadOnlyList<OpcionDeMedio>? cuentas = null, CreditoDelMedio? credito = null) =>
        new(Guid.NewGuid(), "M" + clase, "Medio " + clase, clase, null, vueltas, referencia, null, datafonos ?? [], null, cuentas ?? [], credito);

    [Fact]
    public void Agregar_un_medio_lo_agrega_por_lo_que_falta()
    {
        var cobro = new CobroMultiple(100000m);
        var efectivo = Medio(ClasesDeMedio.Efectivo, vueltas: true);

        cobro.Agregar(efectivo)!.Monto.Should().Be(100000m);
        cobro.Falta.Should().Be(0m);
        cobro.Agregar(efectivo).Should().BeNull("sin nada que falte no se agrega otro pago");
    }

    [Fact]
    public void Partir_el_pago_deja_lo_demas_para_el_siguiente_medio()
    {
        var cobro = new CobroMultiple(100000m);
        cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true))!.Monto = 40000m;

        cobro.Falta.Should().Be(60000m);
        cobro.Agregar(Medio(ClasesDeMedio.Transferencia))!.Monto.Should().Be(60000m);
    }

    [Fact]
    public void Lo_recibido_de_mas_en_efectivo_son_vueltas_y_no_un_pago_de_mas()
    {
        var cobro = new CobroMultiple(43000m);
        var pago = cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true))!;
        pago.Recibido = 50000m;

        cobro.Vueltas.Should().Be(7000m);
        cobro.Sobra.Should().Be(0m);
        cobro.Problema().Should().BeNull();
    }

    [Fact]
    public void La_unica_cuenta_propia_y_el_unico_datafono_se_proponen()
    {
        var cuenta = new OpcionDeMedio(Guid.NewGuid(), "Banco · 1234");
        var datafono = new OpcionDeMedio(Guid.NewGuid(), "D1");
        var cobro = new CobroMultiple(10m);

        cobro.Agregar(Medio(ClasesDeMedio.Transferencia, cuentas: [cuenta]))!.CuentaPropia.Should().Be(cuenta.PublicId);
        new CobroMultiple(10m).Agregar(Medio(ClasesDeMedio.TarjetaDebito, datafonos: [datafono]))!.Datafono.Should().Be(datafono.PublicId);
    }

    [Fact]
    public void Con_varias_cuentas_habilitadas_la_cuenta_es_obligatoria()
    {
        var cobro = new CobroMultiple(10m);
        cobro.Agregar(Medio(ClasesDeMedio.Consignacion, cuentas: [new(Guid.NewGuid(), "A"), new(Guid.NewGuid(), "B")]));

        cobro.Problema().Should().Contain("cuenta propia");
    }

    [Fact]
    public void El_cheque_exige_sus_datos_y_un_cliente_identificado()
    {
        var cobro = new CobroMultiple(10m);
        var pago = cobro.Agregar(Medio(ClasesDeMedio.Cheque))!;

        cobro.Problema().Should().Contain("banco");
        pago.Cheque!.Banco = Guid.NewGuid();
        pago.Cheque.Numero = "123";
        pago.Cheque.Cuenta = "9876";
        cobro.Problema().Should().BeNull();
        cobro.Problema(clienteIdentificado: false).Should().Contain("cliente identificado");
    }

    [Fact]
    public void De_la_tarjeta_solo_cuatro_digitos()
    {
        var cobro = new CobroMultiple(10m);
        cobro.Agregar(Medio(ClasesDeMedio.TarjetaCredito))!.Last4 = "41111";

        cobro.Problema().Should().Contain("cuatro");
    }

    [Fact]
    public void El_bono_exige_su_numero_y_el_credito_sus_condiciones()
    {
        var bono = new CobroMultiple(10m);
        bono.Agregar(Medio(ClasesDeMedio.Bono, referencia: true));
        bono.Problema().Should().Contain("bono");

        var credito = new CobroMultiple(10m);
        credito.Agregar(Medio(ClasesDeMedio.CreditoCliente, credito: new CreditoDelMedio(3, 12, 90, 180, 30, "L1")));
        credito.Problema().Should().BeNull();
        credito.Problema(clienteIdentificado: false).Should().Contain("consumidor final");
    }

    // ---------------------------------------------------------------- abonos (rediseño del 2026-11-09) --

    [Fact]
    public void Un_abono_menor_deja_el_resto_para_otro_medio()
    {
        var cobro = new CobroMultiple(100000m);
        cobro.Agregar(Medio(ClasesDeMedio.TarjetaDebito), 30000m, out var aviso)!.Monto.Should().Be(30000m);
        aviso.Should().BeNull();
        cobro.Agregar(Medio(ClasesDeMedio.Transferencia), 20000m, out _)!.Monto.Should().Be(20000m);
        cobro.Falta.Should().Be(50000m);
        cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true), null, out _)!.Monto.Should().Be(50000m, "sin valor, por lo que falta");
        cobro.Falta.Should().Be(0m);
    }

    [Fact]
    public void Un_abono_mayor_en_efectivo_es_lo_recibido_y_sale_en_vueltas()
    {
        var cobro = new CobroMultiple(43000m);
        var pago = cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true), 50000m, out var aviso)!;

        aviso.Should().BeNull();
        pago.Monto.Should().Be(43000m);
        pago.Recibido.Should().Be(50000m);
        cobro.Vueltas.Should().Be(7000m);
        cobro.Sobra.Should().Be(0m);
        cobro.Problema().Should().BeNull();
    }

    [Fact]
    public void Un_abono_mayor_en_un_medio_sin_vueltas_se_ajusta_a_lo_que_falta_y_lo_avisa()
    {
        var cobro = new CobroMultiple(80000m);
        cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true), 30000m, out _);
        var pago = cobro.Agregar(Medio(ClasesDeMedio.Transferencia), 90000m, out var aviso)!;

        pago.Monto.Should().Be(50000m);
        pago.Recibido.Should().BeNull();
        aviso.Should().Contain("no da vueltas");
        cobro.Sobra.Should().Be(0m);
    }

    [Fact]
    public void Corregir_el_valor_de_un_pago_registrado_respeta_lo_que_le_cabe()
    {
        var cobro = new CobroMultiple(100000m);
        var tarjeta = cobro.Agregar(Medio(ClasesDeMedio.TarjetaCredito), 60000m, out _)!;
        var efectivo = cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true), null, out _)!;

        cobro.FijarMonto(tarjeta, 30000m).Should().BeNull();
        cobro.Falta.Should().Be(30000m);
        cobro.FijarMonto(efectivo, 100000m).Should().BeNull();
        efectivo.Monto.Should().Be(70000m);
        efectivo.Recibido.Should().Be(100000m);
        cobro.Vueltas.Should().Be(30000m);
        cobro.FijarMonto(efectivo, 20000m);
        efectivo.Recibido.Should().BeNull("bajar el valor deja de ser lo recibido de más");
        cobro.Falta.Should().Be(50000m);
    }

    [Fact]
    public void Cambiar_el_medio_conserva_el_lugar_y_el_valor()
    {
        var cobro = new CobroMultiple(100000m);
        var primero = cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true), 40000m, out _)!;
        cobro.Agregar(Medio(ClasesDeMedio.Transferencia), null, out _);
        var bono = Medio(ClasesDeMedio.Bono);

        var nuevo = cobro.CambiarMedio(primero, bono, out var aviso)!;

        aviso.Should().BeNull();
        cobro.Pagos[0].Should().BeSameAs(nuevo);
        nuevo.Medio.Should().BeSameAs(bono);
        nuevo.Monto.Should().Be(40000m);
        cobro.Pagado.Should().Be(100000m);
        cobro.CambiarMedio(nuevo, bono, out _).Should().BeNull("el mismo medio no cambia nada");
    }

    [Fact]
    public void Cambiar_efectivo_con_vueltas_a_un_medio_sin_vueltas_ajusta_el_valor()
    {
        var cobro = new CobroMultiple(43000m);
        var efectivo = cobro.Agregar(Medio(ClasesDeMedio.Efectivo, vueltas: true), 50000m, out _)!;

        var tarjeta = cobro.CambiarMedio(efectivo, Medio(ClasesDeMedio.TarjetaDebito), out var aviso)!;

        tarjeta.Monto.Should().Be(43000m);
        tarjeta.Recibido.Should().BeNull();
        aviso.Should().Contain("no da vueltas");
        cobro.Vueltas.Should().Be(0m);
    }
}
