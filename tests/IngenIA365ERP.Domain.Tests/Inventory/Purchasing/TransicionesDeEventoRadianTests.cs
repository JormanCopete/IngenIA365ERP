using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T336 (FR-050, US9-4, T42; data-model §9.3): la máquina de los eventos RADIAN de una factura del proveedor.
/// Nacen Pending a crédito y NotApplicable de contado; Pending → RegisteredExternally; el 032 exige el 030; la fecha entre la
/// emisión y hoy; un evento hecho no se vuelve a registrar, pero un registro externo se puede corregir.
/// </summary>
public class TransicionesDeEventoRadianTests
{
    private static readonly DateOnly Emision = new(2026, 9, 10);
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private static List<EventoRadianActual> Eventos(SupplierInvoiceEventStatus e030, SupplierInvoiceEventStatus e032) =>
    [
        new(SupplierInvoiceEventCode.Receipt030, e030, null),
        new(SupplierInvoiceEventCode.GoodsReceived032, e032, null),
    ];

    [Fact]
    public void A_credito_nacen_pendientes_y_de_contado_no_aplican()
    {
        TransicionesDeEventoRadian.Iniciales(true).Should().OnlyContain(e => e.Estado == SupplierInvoiceEventStatus.Pending).And.HaveCount(2);
        TransicionesDeEventoRadian.Iniciales(false).Should().OnlyContain(e => e.Estado == SupplierInvoiceEventStatus.NotApplicable).And.HaveCount(2);
        TransicionesDeEventoRadian.Iniciales(true).Select(e => e.Codigo).Should()
            .BeEquivalentTo([SupplierInvoiceEventCode.Receipt030, SupplierInvoiceEventCode.GoodsReceived032]);
    }

    [Fact]
    public void El_030_pendiente_se_registra_por_fuera()
    {
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Pending),
            SupplierInvoiceEventCode.Receipt030, Hoy, Emision, Hoy, corregir: false).Should().BeNull();
    }

    [Fact]
    public void De_contado_no_aplica()
    {
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.NotApplicable, SupplierInvoiceEventStatus.NotApplicable),
            SupplierInvoiceEventCode.Receipt030, Hoy, Emision, Hoy, false).Should().Be(TransicionesDeEventoRadian.CodigoNoAplica);
    }

    [Fact]
    public void El_032_antes_del_030_esta_fuera_de_orden()
    {
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Pending),
            SupplierInvoiceEventCode.GoodsReceived032, Hoy, Emision, Hoy, false).Should().Be(TransicionesDeEventoRadian.CodigoFueraDeOrden);
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.Pending),
            SupplierInvoiceEventCode.GoodsReceived032, Hoy, Emision, Hoy, false).Should().BeNull();
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.Emitted, SupplierInvoiceEventStatus.Pending),
            SupplierInvoiceEventCode.GoodsReceived032, Hoy, Emision, Hoy, false).Should().BeNull();
    }

    [Theory]
    [InlineData(2026, 9, 9)]
    [InlineData(2026, 9, 26)]
    public void La_fecha_va_entre_la_emision_y_hoy(int anio, int mes, int dia)
    {
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Pending),
            SupplierInvoiceEventCode.Receipt030, new DateOnly(anio, mes, dia), Emision, Hoy, false).Should().Be(TransicionesDeEventoRadian.CodigoFechaInvalida);
    }

    [Fact]
    public void Las_dos_puntas_de_la_fecha_valen()
    {
        var eventos = Eventos(SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Pending);
        TransicionesDeEventoRadian.RegistrarExterno(eventos, SupplierInvoiceEventCode.Receipt030, Emision, Emision, Hoy, false).Should().BeNull();
        TransicionesDeEventoRadian.RegistrarExterno(eventos, SupplierInvoiceEventCode.Receipt030, Hoy, Emision, Hoy, false).Should().BeNull();
    }

    [Fact]
    public void Un_evento_hecho_no_se_repite_pero_un_registro_externo_se_corrige()
    {
        var eventos = Eventos(SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.Pending);
        TransicionesDeEventoRadian.RegistrarExterno(eventos, SupplierInvoiceEventCode.Receipt030, Hoy, Emision, Hoy, corregir: false)
            .Should().Be(TransicionesDeEventoRadian.CodigoYaRegistrado);
        TransicionesDeEventoRadian.RegistrarExterno(eventos, SupplierInvoiceEventCode.Receipt030, Hoy, Emision, Hoy, corregir: true)
            .Should().BeNull();
        // Corregir uno pendiente no tiene sentido; uno emitido por el ERP (I5) no se corrige desde aquí.
        TransicionesDeEventoRadian.RegistrarExterno(eventos, SupplierInvoiceEventCode.GoodsReceived032, Hoy, Emision, Hoy, corregir: true)
            .Should().Be(TransicionesDeEventoRadian.CodigoNoAplica);
        TransicionesDeEventoRadian.RegistrarExterno(Eventos(SupplierInvoiceEventStatus.Emitted, SupplierInvoiceEventStatus.Pending),
            SupplierInvoiceEventCode.Receipt030, Hoy, Emision, Hoy, corregir: true).Should().Be(TransicionesDeEventoRadian.CodigoYaRegistrado);
    }

    [Theory]
    [InlineData(SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.RegisteredExternally, true)]
    [InlineData(SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.RegisteredExternally, true)]
    [InlineData(SupplierInvoiceEventStatus.Pending, SupplierInvoiceEventStatus.Emitted, true)]
    [InlineData(SupplierInvoiceEventStatus.Rejected, SupplierInvoiceEventStatus.Pending, true)]
    [InlineData(SupplierInvoiceEventStatus.NotApplicable, SupplierInvoiceEventStatus.RegisteredExternally, false)]
    [InlineData(SupplierInvoiceEventStatus.Emitted, SupplierInvoiceEventStatus.Pending, false)]
    [InlineData(SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.Pending, false)]
    public void Transiciones(SupplierInvoiceEventStatus de, SupplierInvoiceEventStatus a, bool valida) =>
        TransicionesDeEventoRadian.EsTransicionValida(de, a).Should().Be(valida);

    [Fact]
    public void Queda_pendiente_mientras_alguno_lo_este()
    {
        TransicionesDeEventoRadian.QuedaPendiente(Eventos(SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.Pending)).Should().BeTrue();
        TransicionesDeEventoRadian.QuedaPendiente(Eventos(SupplierInvoiceEventStatus.RegisteredExternally, SupplierInvoiceEventStatus.RegisteredExternally)).Should().BeFalse();
        TransicionesDeEventoRadian.QuedaPendiente(Eventos(SupplierInvoiceEventStatus.NotApplicable, SupplierInvoiceEventStatus.NotApplicable)).Should().BeFalse();
    }
}
