using FluentAssertions;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Tests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I4 (T692, T694; data-model §18): la resolución nace con <c>LastIssuedNumber = RangeFrom − 1</c> y la versión
/// sólo llena sus artefactos una vez.
/// </summary>
public class EntidadesDeFacturacionElectronicaTests
{
    [Fact]
    public void La_resolucion_nace_sin_numeros_emitidos()
    {
        var r = new DianNumberingResolution { Kind = ResolutionKind.Invoice, RangeFrom = 990000000, RangeTo = 995000000 };

        r.LastIssuedNumber.Should().Be(989999999);
        r.TieneNumerosEmitidos.Should().BeFalse();
        r.Disponibles.Should().Be(5000001);
    }

    [Fact]
    public void Mover_el_rango_sin_emitir_arrastra_el_ultimo_numero_y_con_emitidos_no()
    {
        var r = new DianNumberingResolution { RangeFrom = 1, RangeTo = 10 };
        r.RangeFrom = 5;
        r.LastIssuedNumber.Should().Be(4);

        r.LastIssuedNumber = 6;
        r.RangeFrom = 3;
        r.LastIssuedNumber.Should().Be(6);
        r.TieneNumerosEmitidos.Should().BeTrue();
    }

    [Fact]
    public void La_resolucion_se_agota_al_emitir_el_ultimo_del_rango()
    {
        var r = new DianNumberingResolution { RangeFrom = 1, RangeTo = 10 };
        r.Agotada.Should().BeFalse();
        r.LastIssuedNumber = 10;
        r.Agotada.Should().BeTrue();
        r.Disponibles.Should().Be(0);
    }

    [Fact]
    public void Un_artefacto_de_la_version_se_escribe_una_sola_vez()
    {
        var v = new ElectronicDocumentVersion { VersionNumber = 1, Reason = DocumentVersionReason.Initial };
        var pdf = Guid.NewGuid();

        v.FijarArtefacto(ArtefactoDeVersion.RepresentacionGrafica, pdf).Should().BeTrue();
        v.FijarArtefacto(ArtefactoDeVersion.RepresentacionGrafica, pdf).Should().BeFalse("repetir el mismo es inocuo");
        v.GraphicPdfAttachmentPublicId.Should().Be(pdf);

        FluentActions.Invoking(() => v.FijarArtefacto(ArtefactoDeVersion.RepresentacionGrafica, Guid.NewGuid()))
            .Should().Throw<InvalidOperationException>();
        v.GraphicPdfAttachmentPublicId.Should().Be(pdf);
    }

    [Fact]
    public void Las_cuatro_referencias_de_la_version_son_de_escritura_unica_y_nada_mas()
    {
        var escrituraUnica = typeof(ElectronicDocumentVersion).GetProperties()
            .Where(p => p.IsDefined(typeof(IngenIA365ERP.Domain.Common.EscrituraUnicaAttribute), true))
            .Select(p => p.Name);

        escrituraUnica.Should().BeEquivalentTo(
        [
            nameof(ElectronicDocumentVersion.CanonicalAttachmentPublicId),
            nameof(ElectronicDocumentVersion.SignedXmlAttachmentPublicId),
            nameof(ElectronicDocumentVersion.AttachedDocumentAttachmentPublicId),
            nameof(ElectronicDocumentVersion.GraphicPdfAttachmentPublicId),
        ]);
    }

    [Fact]
    public void Al_numerar_en_contingencia_03_el_documento_la_registra()
    {
        var doc = new ElectronicDocument();
        doc.Iniciar(contingencia03Abierta: true);

        doc.Status.Should().Be(ElectronicDocumentStatus.IssuerContingency);
        doc.ContingencyType.Should().Be(ContingencyType.Issuer03);
    }
}
