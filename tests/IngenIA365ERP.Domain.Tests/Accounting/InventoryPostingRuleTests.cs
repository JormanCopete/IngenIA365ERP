using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;

namespace IngenIA365ERP.Domain.Tests.Accounting;

/// <summary>
/// Feature 012, T478 (data-model §20; contracts/contabilidad.md §2.1 y §2.3; T27): la regla de la matriz calcula su
/// <c>DimensionKey</c> y su <c>SpecificityWeight</c> en el constructor. La clave es la de <c>UK (DimensionKey, ValidFrom)</c>:
/// normalizada, con <c>*</c> donde la regla no fija valor, porque un único con columnas nulas se comporta distinto en
/// SQL Server y PostgreSQL. El peso ordena la resolución: bodega o punto 16, centro 8, sucursal 4, grupo 2.
/// </summary>
public class InventoryPostingRuleTests
{
    private static readonly DateOnly Desde = new(2026, 12, 1);

    [Fact]
    public void Una_regla_sin_dimensiones_opcionales_lleva_asterisco_en_todas()
    {
        var regla = new InventoryPostingRule("FacturaProveedor", "CuentaPorPagar", accountId: 7, validFrom: Desde);

        regla.DimensionKey.Should().Be("FacturaProveedor|CuentaPorPagar|G:*|W:*|P:*|M:*|T:*|R:*|B:*|C:*");
        regla.SpecificityWeight.Should().Be(0);
        regla.AccountId.Should().Be(7);
        regla.ValidFrom.Should().Be(Desde);
        regla.ValidTo.Should().BeNull();
    }

    [Fact]
    public void Los_codigos_se_normalizan_y_la_clave_los_lleva_en_orden_fijo()
    {
        var regla = new InventoryPostingRule(" Venta ", " Ingreso ", accountId: 1, validFrom: Desde,
            accountingGroupCode: " abarrotes ", warehouseCode: "bpv1", branchId: 3, costCenterId: 9);

        regla.Operation.Should().Be("Venta");
        regla.Role.Should().Be("Ingreso");
        regla.AccountingGroupCode.Should().Be("ABARROTES");
        regla.WarehouseCode.Should().Be("BPV1");
        regla.DimensionKey.Should().Be("Venta|Ingreso|G:ABARROTES|W:BPV1|P:*|M:*|T:*|R:*|B:3|C:9");
        regla.SpecificityWeight.Should().Be(16 + 8 + 4 + 2);
    }

    [Fact]
    public void La_tarifa_va_en_la_clave_como_codigo_arroba_fraccion_invariante()
    {
        var regla = new InventoryPostingRule("Venta", "Impuesto", accountId: 1, validFrom: Desde,
            taxRateCode: "iva19", taxRate: 0.190000m);

        regla.TaxRateCode.Should().Be("IVA19");
        regla.TaxRate.Should().Be(0.19m);
        regla.DimensionKey.Should().Be("Venta|Impuesto|G:*|W:*|P:*|M:*|T:IVA19@0.19|R:*|B:*|C:*");

        // Por mil (ICA): seis decimales de fracción, sin redondear a cuatro (C8).
        new InventoryPostingRule("FacturaProveedor", "Retencion", accountId: 1, validFrom: Desde, taxRateCode: "ICA966", taxRate: 0.00966m)
            .DimensionKey.Should().Contain("T:ICA966@0.00966");
    }

    [Fact]
    public void El_motivo_no_cambia_de_mayusculas_porque_nombra_enumeraciones()
    {
        var regla = new InventoryPostingRule("DiferenciaDeArqueo", "Faltante", accountId: 1, validFrom: Desde,
            reasonCode: " ShortageToCashier ", pointOfSaleCode: "pv01");

        regla.ReasonCode.Should().Be("ShortageToCashier");
        regla.DimensionKey.Should().Be("DiferenciaDeArqueo|Faltante|G:*|W:*|P:PV01|M:*|T:*|R:ShortageToCashier|B:*|C:*");
        regla.SpecificityWeight.Should().Be(16);
    }

    [Fact]
    public void Un_texto_vacio_es_como_no_fijar_la_dimension()
    {
        var regla = new InventoryPostingRule("Venta", "MedioDePago", accountId: 1, validFrom: Desde,
            paymentMeansCode: "efectivo", pointOfSaleCode: "  ", warehouseCode: "");

        regla.PointOfSaleCode.Should().BeNull();
        regla.WarehouseCode.Should().BeNull();
        regla.DimensionKey.Should().Be("Venta|MedioDePago|G:*|W:*|P:*|M:EFECTIVO|T:*|R:*|B:*|C:*");
    }

    [Theory]
    [InlineData(null, null, null, null, null, 0)]
    [InlineData("G", null, null, null, null, 2)]
    [InlineData(null, null, null, 5, null, 4)]
    [InlineData(null, null, null, null, 6, 8)]
    [InlineData(null, "W", null, null, null, 16)]
    [InlineData(null, null, "P", null, null, 16)]
    [InlineData("G", "W", null, 5, 6, 30)]
    public void El_peso_suma_potencias_de_dos(string? grupo, string? bodega, string? punto, int? sucursal, int? centro, int esperado)
    {
        InventoryPostingRule.PesoDe(grupo, bodega, punto, sucursal, centro).Should().Be((short)esperado);
    }

    [Fact]
    public void La_clave_estatica_es_la_misma_que_calcula_el_constructor()
    {
        var regla = new InventoryPostingRule("Compra", "Inventario", accountId: 1, validFrom: Desde, accountingGroupCode: "ABA", branchId: 2);

        InventoryPostingRule.ClaveDe("Compra", "Inventario", "aba", null, null, null, null, null, null, 2, null)
            .Should().Be(regla.DimensionKey);
    }

    [Fact]
    public void La_vigencia_se_cierra_la_vispera_y_nunca_antes_de_empezar()
    {
        var regla = new InventoryPostingRule("Compra", "Inventario", accountId: 1, validFrom: Desde, accountingGroupCode: "ABA");

        regla.VigenteEn(Desde).Should().BeTrue();
        regla.VigenteEn(Desde.AddDays(-1)).Should().BeFalse();

        regla.CerrarVigencia(new DateOnly(2027, 1, 31));
        regla.ValidTo.Should().Be(new DateOnly(2027, 1, 31));
        regla.VigenteEn(new DateOnly(2027, 1, 31)).Should().BeTrue();
        regla.VigenteEn(new DateOnly(2027, 2, 1)).Should().BeFalse();

        // Desactivar antes de que empiece la deja sin tramo vigente (ValidTo = víspera de ValidFrom); más atrás no.
        var otra = new InventoryPostingRule("Compra", "Inventario", accountId: 1, validFrom: Desde, accountingGroupCode: "ABA");
        otra.CerrarVigencia(Desde.AddDays(-1));
        otra.VigenteEn(Desde).Should().BeFalse();
        var invalida = () => new InventoryPostingRule("Compra", "Inventario", accountId: 1, validFrom: Desde, accountingGroupCode: "ABA")
            .CerrarVigencia(Desde.AddDays(-2));
        invalida.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Operacion_rol_y_cuenta_son_obligatorios()
    {
        ((Action)(() => new InventoryPostingRule(" ", "Inventario", 1, Desde))).Should().Throw<ArgumentException>();
        ((Action)(() => new InventoryPostingRule("Compra", "", 1, Desde))).Should().Throw<ArgumentException>();
        ((Action)(() => new InventoryPostingRule("Compra", "Inventario", 0, Desde))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void El_mapeo_tiene_su_clave_por_operacion_y_tipo_de_documento()
    {
        new InventoryVoucherMapping("Venta", null, voucherTypeId: 10).MappingKey.Should().Be("Venta|*");
        var excepcion = new InventoryVoucherMapping("AjusteNegativo", " rem ", voucherTypeId: 12, crossDocumentTypeId: 4);
        excepcion.InventoryDocumentTypeCode.Should().Be("REM");
        excepcion.MappingKey.Should().Be("AjusteNegativo|REM");
        excepcion.VoucherTypeId.Should().Be(12);
        excepcion.CrossDocumentTypeId.Should().Be(4);
        InventoryVoucherMapping.ClaveDe("AjusteNegativo", "rem").Should().Be("AjusteNegativo|REM");
    }
}
