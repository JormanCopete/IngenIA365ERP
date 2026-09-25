using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Domain.Tests.Taxes;

/// <summary>
/// Feature 012, T098 (FR-044; research R21): el IVA de una compra es descontable o va al costo en el orden de FR-044
/// —cooperativa no responsable de IVA, tipo de compra marcado <c>VatNonDeductible</c>, producto que se vende
/// excluido—; si nada de eso, <see cref="TaxTreatment.Deductible"/>. El INC siempre va al costo.
/// </summary>
public class IvaDescontableTests
{
    [Fact]
    public void Si_la_cooperativa_no_es_responsable_de_IVA_todo_va_al_costo()
    {
        foreach (var tratamiento in Enum.GetValues<VatSaleTreatment>())
            IvaDescontable.Determinar(TaxKind.Iva, cooperativaResponsableIva: false, tipoIvaNoDescontable: false, tratamiento)
                .Should().Be(TaxTreatment.AddedToCost, $"producto {tratamiento}");
    }

    [Fact]
    public void Un_tipo_de_compra_con_IVA_no_descontable_lo_lleva_al_costo()
    {
        IvaDescontable.Determinar(TaxKind.Iva, cooperativaResponsableIva: true, tipoIvaNoDescontable: true, VatSaleTreatment.Taxed)
            .Should().Be(TaxTreatment.AddedToCost);
    }

    [Fact]
    public void El_producto_que_se_vende_excluido_lleva_su_IVA_al_costo()
    {
        IvaDescontable.Determinar(TaxKind.Iva, true, false, VatSaleTreatment.Excluded).Should().Be(TaxTreatment.AddedToCost);
    }

    [Theory]
    [InlineData(VatSaleTreatment.Taxed)]
    [InlineData(VatSaleTreatment.Exempt)]
    public void Si_nada_lo_impide_el_IVA_es_descontable(VatSaleTreatment tratamiento)
    {
        // El exento sí genera derecho a descontar (a diferencia del excluido).
        IvaDescontable.Determinar(TaxKind.Iva, true, false, tratamiento).Should().Be(TaxTreatment.Deductible);
    }

    [Theory]
    [InlineData(true, false, VatSaleTreatment.Taxed)]
    [InlineData(false, false, VatSaleTreatment.Taxed)]
    [InlineData(true, true, VatSaleTreatment.Exempt)]
    public void El_INC_siempre_va_al_costo(bool responsable, bool noDescontable, VatSaleTreatment tratamiento)
    {
        IvaDescontable.Determinar(TaxKind.Inc, responsable, noDescontable, tratamiento).Should().Be(TaxTreatment.AddedToCost);
    }

    [Fact]
    public void El_orden_de_FR_044_manda_la_primera_razon()
    {
        var pasos = IvaDescontable.Explicar(TaxKind.Iva, cooperativaResponsableIva: false, tipoIvaNoDescontable: true, VatSaleTreatment.Excluded);
        pasos.Should().StartWith("La cooperativa no es responsable de IVA");
    }
}
