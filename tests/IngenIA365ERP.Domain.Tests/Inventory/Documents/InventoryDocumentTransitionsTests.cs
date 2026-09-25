using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Exceptions;

namespace IngenIA365ERP.Domain.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T101 (FR-005, FR-006, FR-010; data-model §5.1): el ciclo del documento de inventario.
/// <c>Draft → PendingApproval → Confirmed → Voided</c>, <c>PendingApproval → Draft</c> al rechazar o retirar,
/// <c>Draft → Discarded</c> con motivo, y ninguna otra.
/// </summary>
public class InventoryDocumentTransitionsTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);

    private static InventoryDocument Borrador() => new() { Class = DocumentClass.PositiveAdjustment, OperationDate = new DateOnly(2026, 9, 25) };

    private static InventoryDocument En(DocumentStatus estado)
    {
        var d = Borrador();
        switch (estado)
        {
            case DocumentStatus.PendingApproval: d.EnviarAAprobacion(); break;
            case DocumentStatus.Confirmed: d.Confirmar(7, Ahora); break;
            case DocumentStatus.Voided: d.Confirmar(7, Ahora); d.MarcarAnulado(99); break;
            case DocumentStatus.Discarded: d.Descartar(7, Ahora, "sobraba"); break;
        }
        return d;
    }

    [Fact]
    public void Nace_en_borrador_sin_numero()
    {
        var d = Borrador();
        d.Status.Should().Be(DocumentStatus.Draft);
        d.Number.Should().BeNull();
        d.Prefix.Should().BeEmpty();
        d.Currency.Should().Be("COP");
        d.ExchangeRate.Should().Be(1m);
    }

    [Fact]
    public void Sin_niveles_el_borrador_se_confirma_con_quien_y_cuando()
    {
        var d = Borrador();
        d.Confirmar(7, Ahora);
        d.Status.Should().Be(DocumentStatus.Confirmed);
        d.ConfirmedByUserId.Should().Be(7);
        d.ConfirmedAt.Should().Be(Ahora);
    }

    [Fact]
    public void Con_niveles_pasa_por_aprobacion_y_la_ultima_aprobacion_lo_confirma()
    {
        var d = Borrador();
        d.EnviarAAprobacion();
        d.Status.Should().Be(DocumentStatus.PendingApproval);
        d.Confirmar(8, Ahora);
        d.Status.Should().Be(DocumentStatus.Confirmed);
        d.ConfirmedByUserId.Should().Be(8, "confirma quien dio la última aprobación");
    }

    [Fact]
    public void Rechazar_o_retirar_la_aprobacion_lo_devuelve_a_borrador()
    {
        var d = En(DocumentStatus.PendingApproval);
        d.DevolverABorrador();
        d.Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public void Descartar_exige_motivo_y_lo_registra()
    {
        var sinMotivo = () => Borrador().Descartar(7, Ahora, "  ");
        sinMotivo.Should().Throw<ArgumentException>();

        var d = Borrador();
        d.Descartar(7, Ahora, "  duplicado ");
        d.Status.Should().Be(DocumentStatus.Discarded);
        d.DiscardedByUserId.Should().Be(7);
        d.DiscardedAt.Should().Be(Ahora);
        d.DiscardReason.Should().Be("duplicado");
        d.Number.Should().BeNull("descartar no consume número");
    }

    [Fact]
    public void Anular_lo_pone_su_documento_contrario()
    {
        var d = En(DocumentStatus.Confirmed);
        d.MarcarAnulado(99);
        d.Status.Should().Be(DocumentStatus.Voided);
        d.VoidedByDocumentId.Should().Be(99);
    }

    public static TheoryData<DocumentStatus, DocumentStatus> Validas => new()
    {
        { DocumentStatus.Draft, DocumentStatus.PendingApproval },
        { DocumentStatus.Draft, DocumentStatus.Confirmed },
        { DocumentStatus.Draft, DocumentStatus.Discarded },
        { DocumentStatus.PendingApproval, DocumentStatus.Confirmed },
        { DocumentStatus.PendingApproval, DocumentStatus.Draft },
        { DocumentStatus.Confirmed, DocumentStatus.Voided },
    };

    [Fact]
    public void Solo_existen_las_seis_transiciones_del_ciclo()
    {
        var validas = Validas.Select(f => ((DocumentStatus)f[0], (DocumentStatus)f[1])).ToHashSet();
        foreach (var de in Enum.GetValues<DocumentStatus>())
        foreach (var a in Enum.GetValues<DocumentStatus>())
            InventoryDocument.EsTransicionValida(de, a).Should().Be(validas.Contains((de, a)), $"{de} → {a}");
    }

    [Theory]
    [InlineData(DocumentStatus.PendingApproval)]
    [InlineData(DocumentStatus.Confirmed)]
    [InlineData(DocumentStatus.Voided)]
    [InlineData(DocumentStatus.Discarded)]
    public void Descartar_solo_desde_borrador(DocumentStatus estado)
    {
        var d = En(estado);
        var accion = () => d.Descartar(7, Ahora, "motivo");
        accion.Should().Throw<InvalidDocumentTransitionException>()
            .Where(e => e.From == estado && e.To == DocumentStatus.Discarded);
        d.Status.Should().Be(estado);
    }

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.PendingApproval)]
    [InlineData(DocumentStatus.Voided)]
    [InlineData(DocumentStatus.Discarded)]
    public void Solo_se_anula_un_confirmado(DocumentStatus estado)
    {
        var accion = () => En(estado).MarcarAnulado(99);
        accion.Should().Throw<InvalidDocumentTransitionException>();
    }

    [Theory]
    [InlineData(DocumentStatus.Confirmed)]
    [InlineData(DocumentStatus.Voided)]
    [InlineData(DocumentStatus.Discarded)]
    public void Confirmado_anulado_o_descartado_no_vuelve_atras_ni_se_reconfirma(DocumentStatus estado)
    {
        var d = En(estado);
        ((Action)(() => d.Confirmar(7, Ahora))).Should().Throw<InvalidDocumentTransitionException>();
        ((Action)(() => d.EnviarAAprobacion())).Should().Throw<InvalidDocumentTransitionException>();
        ((Action)(() => d.DevolverABorrador())).Should().Throw<InvalidDocumentTransitionException>();
    }

    [Fact]
    public void Un_borrador_no_vuelve_a_borrador()
    {
        ((Action)(() => Borrador().DevolverABorrador())).Should().Throw<InvalidDocumentTransitionException>();
    }

    [Fact]
    public void Queda_fijo_al_confirmar()
    {
        IInmutableTrasConfirmar.EstaFijo(DocumentStatus.Confirmed).Should().BeTrue();
        IInmutableTrasConfirmar.EstaFijo(DocumentStatus.Voided).Should().BeTrue();
        IInmutableTrasConfirmar.EstaFijo(DocumentStatus.Draft).Should().BeFalse();
        IInmutableTrasConfirmar.EstaFijo(DocumentStatus.PendingApproval).Should().BeFalse();
        IInmutableTrasConfirmar.PropiedadesMutablesTrasConfirmar.Should().BeEquivalentTo(
            ["Status", "VoidedByDocumentId", "FiscalNumberReleased", "UpdatedAt", "UpdatedBy", "RowVersion"]);
        typeof(IInmutableTrasConfirmar).IsAssignableFrom(typeof(InventoryDocument)).Should().BeTrue();
        typeof(IHechoInmutable).IsAssignableFrom(typeof(DocumentPartySnapshot)).Should().BeTrue();
        typeof(IHechoInmutable).IsAssignableFrom(typeof(DocumentTaxLine)).Should().BeTrue();
    }
}
