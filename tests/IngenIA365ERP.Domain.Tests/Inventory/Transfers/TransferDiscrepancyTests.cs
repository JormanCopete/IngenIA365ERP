using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Tests.Inventory.Transfers;

/// <summary>
/// Feature 012, T365 (FR-039, US10-2; data-model §7.1): la diferencia de un traslado. Pendiente → resolución pedida (con su
/// documento en aprobación) → resuelta cuando ese documento se confirma, o de vuelta a pendiente si se rechaza. El faltante se
/// resuelve devolviéndolo al origen, dándolo de baja desde el tránsito o recibiéndolo tarde; el sobrante, con un ajuste positivo
/// en el destino. Una aprobación parcial resuelve sólo su cantidad: lo demás sigue pendiente.
/// </summary>
public class TransferDiscrepancyTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);

    private static TransferDiscrepancy Faltante(decimal cantidad = 1m) => new()
    {
        DispatchDocumentId = 1, ReceiptDocumentId = 2, DispatchLineId = 3, ProductId = 4,
        Kind = TransferDiscrepancyKind.Shortage, QuantityBase = cantidad, UnitCost = 1_150m,
    };

    private static TransferDiscrepancy Sobrante() => new()
    {
        DispatchDocumentId = 1, ReceiptDocumentId = 2, DispatchLineId = 3, ProductId = 4,
        Kind = TransferDiscrepancyKind.Surplus, QuantityBase = 1m,
    };

    [Fact]
    public void El_faltante_admite_tres_salidas_y_el_sobrante_una()
    {
        TransferDiscrepancy.ResolucionesAdmitidas(TransferDiscrepancyKind.Shortage).Should().Equal(
            TransferDiscrepancyResolution.ReturnToOrigin, TransferDiscrepancyResolution.WriteOffFromTransit, TransferDiscrepancyResolution.LateReceipt);
        TransferDiscrepancy.ResolucionesAdmitidas(TransferDiscrepancyKind.Surplus).Should().Equal(TransferDiscrepancyResolution.SurplusAdjustment);

        Faltante().Admite(TransferDiscrepancyResolution.SurplusAdjustment).Should().BeFalse();
        Sobrante().Admite(TransferDiscrepancyResolution.WriteOffFromTransit).Should().BeFalse();
        Sobrante().Admite(TransferDiscrepancyResolution.SurplusAdjustment).Should().BeTrue();
    }

    [Fact]
    public void La_baja_y_el_sobrante_exigen_la_causa()
    {
        TransferDiscrepancy.ExigeCausa(TransferDiscrepancyResolution.WriteOffFromTransit).Should().BeTrue();
        TransferDiscrepancy.ExigeCausa(TransferDiscrepancyResolution.SurplusAdjustment).Should().BeTrue();
        TransferDiscrepancy.ExigeCausa(TransferDiscrepancyResolution.ReturnToOrigin).Should().BeFalse();
        TransferDiscrepancy.ExigeCausa(TransferDiscrepancyResolution.LateReceipt).Should().BeFalse();
    }

    [Fact]
    public void Nace_pendiente_con_toda_su_cantidad()
    {
        var d = Faltante(3m);

        d.Estado.Should().Be(TransferDiscrepancy.EstadoPendiente);
        d.Pendiente().Should().Be(3m);
        d.ResolvedAt.Should().BeNull();
        d.Resolution.Should().BeNull();
    }

    [Fact]
    public void Pedir_la_resolucion_la_deja_en_aprobacion_con_quien_la_pidio_y_su_documento()
    {
        var d = Faltante();

        d.PedirResolucion(TransferDiscrepancyResolution.WriteOffFromTransit, 1m, 9, Ahora, "  reclamación al transportador ", 5, 77);

        d.Estado.Should().Be(TransferDiscrepancy.EstadoEnAprobacion);
        d.Resolution.Should().Be(TransferDiscrepancyResolution.WriteOffFromTransit);
        d.ResolutionRequestedByUserId.Should().Be(9);
        d.ResolutionRequestedAt.Should().Be(Ahora);
        d.ResolutionReason.Should().Be("reclamación al transportador");
        d.AdjustmentCauseId.Should().Be(5);
        d.ResolutionDocumentId.Should().Be(77);
        d.ResolutionQuantityBase.Should().Be(1m);
    }

    [Fact]
    public void No_se_pide_dos_veces_ni_una_salida_que_no_corresponde_ni_mas_de_lo_pendiente()
    {
        var d = Faltante(2m);
        var otraSalida = () => d.PedirResolucion(TransferDiscrepancyResolution.SurplusAdjustment, 1m, 9, Ahora, "x", 5, 77);
        otraSalida.Should().Throw<InvalidOperationException>();
        var demasiado = () => d.PedirResolucion(TransferDiscrepancyResolution.LateReceipt, 3m, 9, Ahora, "x", null, 77);
        demasiado.Should().Throw<InvalidOperationException>();

        d.PedirResolucion(TransferDiscrepancyResolution.LateReceipt, 2m, 9, Ahora, "llegó", null, 77);
        var otraVez = () => d.PedirResolucion(TransferDiscrepancyResolution.ReturnToOrigin, 1m, 9, Ahora, "x", null, 78);
        otraVez.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rechazada_vuelve_a_pendiente_sin_rastro_de_la_resolucion()
    {
        var d = Faltante();
        d.PedirResolucion(TransferDiscrepancyResolution.ReturnToOrigin, 1m, 9, Ahora, "volvió", null, 77);

        d.VolverAPendiente();

        d.Estado.Should().Be(TransferDiscrepancy.EstadoPendiente);
        d.Resolution.Should().BeNull();
        d.ResolutionDocumentId.Should().BeNull();
        d.ResolutionQuantityBase.Should().BeNull();
        d.Pendiente().Should().Be(1m);
    }

    [Fact]
    public void Aprobada_queda_resuelta()
    {
        var d = Faltante();
        d.PedirResolucion(TransferDiscrepancyResolution.WriteOffFromTransit, 1m, 9, Ahora, "hurto", 5, 77);

        d.Resolver(1m, Ahora);

        d.Estado.Should().Be(TransferDiscrepancy.EstadoResuelta);
        d.ResolvedAt.Should().Be(Ahora);
        d.Pendiente().Should().Be(0m);
        d.Resolution.Should().Be(TransferDiscrepancyResolution.WriteOffFromTransit);
        d.ResolutionDocumentId.Should().Be(77);
    }

    [Fact]
    public void Aprobada_por_una_parte_resuelve_esa_parte_y_lo_demas_sigue_pendiente()
    {
        var d = Faltante(3m);
        d.PedirResolucion(TransferDiscrepancyResolution.LateReceipt, 2m, 9, Ahora, "llegaron dos", null, 77);

        d.Resolver(2m, Ahora);

        d.Estado.Should().Be(TransferDiscrepancy.EstadoPendiente);
        d.ResolvedAt.Should().BeNull();
        d.ResolvedQuantityBase.Should().Be(2m);
        d.Pendiente().Should().Be(1m);
        d.Resolution.Should().BeNull();

        d.PedirResolucion(TransferDiscrepancyResolution.WriteOffFromTransit, 1m, 9, Ahora, "se perdió", 5, 78);
        d.Resolver(1m, Ahora);
        d.Estado.Should().Be(TransferDiscrepancy.EstadoResuelta);
    }

    [Fact]
    public void Sin_resolucion_pedida_no_se_resuelve_ni_se_devuelve()
    {
        var d = Faltante();
        var resolver = () => d.Resolver(1m, Ahora);
        resolver.Should().Throw<InvalidOperationException>();
        var devolver = () => d.VolverAPendiente();
        devolver.Should().Throw<InvalidOperationException>();
    }
}
