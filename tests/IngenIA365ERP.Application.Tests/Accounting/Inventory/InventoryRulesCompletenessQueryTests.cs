using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T456 (T516; contracts/contabilidad.md §7.1; api.md §26.3; FR-082, SC-024): la completitud de la matriz.
/// Combinaciones en uso sin regla (el tránsito pide <c>Transito</c>), medios de pago activos sin <c>MedioDePago</c>, reglas con
/// cuenta no elegible, tarifas distintas en algún tramo (C8), operaciones sin mapeo y los avisos de §7.1, con su resumen.
/// </summary>
public class InventoryRulesCompletenessQueryTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private readonly EscenarioContable E = new();
    private readonly ILectorDeParametros Parametros = Substitute.For<ILectorDeParametros>();

    public InventoryRulesCompletenessQueryTests()
    {
        // Sin Costeo.Ambito guardado rige el defecto (por cooperativa).
        Parametros.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<Domain.Enums.Parameters.ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<ValorDeParametro>(new Error("Parameters.KeyNotFound", "x"))));
        E.Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CatalogoDeDimensionesDto(
            [new("ABARROTES", "Abarrotes"), new("CARNES", "Carnes")],
            [new("B01", "Principal", EscenarioContable.Principal, null, WarehouseBehavior.Operational),
             new("TR01", "Tránsito", EscenarioContable.Principal, null, WarehouseBehavior.Transit)],
            [], [], [new("CO", "Compra")],
            [new("EFECTIVO", "Efectivo", PaymentMeansClass.Cash), new("NEQUI", "Nequi", PaymentMeansClass.Transfer)])));
        E.Dimensiones.CombinacionesEnUsoAsync(Fecha, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<CombinacionEnUsoDto>>(
        [
            new("Compra", "ABARROTES", "B01", ["CO"], Fecha),
            new("Compra", "CARNES", "B01", ["CO"], Fecha),
            new("CostoDeVenta", "ABARROTES", "TR01", ["DEPOS"], Fecha),
        ]));
    }

    private async Task<CompletitudDeLaMatrizDto> ConsultarAsync()
    {
        var r = await new InventoryRulesCompletenessQueryHandler(E.D.Db, E.Dimensiones, new TiposDeComprobanteDeInventario(E.D.Db), Parametros)
            .Handle(new InventoryRulesCompletenessQuery(Fecha), default);
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    [Fact]
    public async Task Lista_las_combinaciones_en_uso_sin_regla_y_los_medios_sin_cuenta()
    {
        var c = await ConsultarAsync();

        c.MissingRules.Select(m => (m.Operation, m.Role, m.AccountingGroupCode, m.WarehouseCode)).Should().BeEquivalentTo(new[]
        {
            ("Compra", "Inventario", (string?)"CARNES", (string?)"B01"),
            ("CostoDeVenta", "Transito", (string?)"ABARROTES", (string?)"TR01"),
        });
        c.MissingRules.Should().OnlyContain(m => m.UsedByDocumentTypes.Count > 0 && m.LastUsedAt == Fecha);
        c.PaymentMeansWithoutAccount.Select(m => m.PaymentMeansCode).Should().Equal("NEQUI");
        c.IneligibleRules.Should().BeEmpty();
        c.TaxRateMismatches.Should().BeEmpty();
        c.UnmappedOperations.Should().BeEmpty();
        c.Summary.Total.Should().Be(3);
        c.Summary.ByKind["missingRules"].Should().Be(2);
    }

    [Fact]
    public async Task Lista_las_reglas_con_cuenta_no_elegible_y_las_operaciones_sin_mapeo()
    {
        E.Cuenta("53050501").IsActive = false;
        E.D.Db.InventoryVoucherMappings.RemoveRange(E.D.Db.InventoryVoucherMappings.Where(m => m.Operation == "Ensamble"));
        E.D.Db.SaveChanges();

        var c = await ConsultarAsync();

        c.IneligibleRules.Should().ContainSingle().Which.Should().Match<ReglaNoElegibleDto>(r =>
            r.Operation == "AjusteDeCosto" && r.Role == "Redondeo" && r.Account == "53050501" && r.Reason == "está inactiva");
        c.UnmappedOperations.Should().Equal(new OperacionSinMapeoDto("Ensamble", null));
    }

    [Fact]
    public async Task Lista_los_tramos_en_que_la_cuenta_tiene_otra_tarifa()
    {
        E.Cuenta("24080502").TaxRates.Add(new AccountTaxRate { ValidFrom = new DateOnly(2026, 6, 1), Rate = 0.18m, CreatedBy = "test" });
        E.D.Db.SaveChanges();

        var c = await ConsultarAsync();

        c.TaxRateMismatches.Should().NotBeEmpty().And.OnlyContain(t =>
            t.TaxRateCode == "IVA19" && t.CatalogRate == 0.19m && t.Account == "24080502" && t.AccountRate == 0.18m
            && t.From == new DateOnly(2026, 6, 1) && t.To == null);
    }

    [Fact]
    public void Una_cuenta_sin_tarifa_en_el_primer_tramo_tambien_se_lista()
    {
        var cuenta = new ChartOfAccount { Code = "24089901" };
        cuenta.TaxRates.Add(new AccountTaxRate { ValidFrom = new DateOnly(2026, 4, 1), Rate = 0.19m });
        var regla = new InventoryPostingRule("Venta", "Impuesto", 1, new DateOnly(2026, 1, 1), taxRateCode: "IVA19", taxRate: 0.19m, notes: "x");

        var tramos = InventoryRulesCompletenessQueryHandler.TramosDistintos(regla, cuenta).ToList();

        tramos.Should().ContainSingle().Which.Should().Be(new TarifaDistintaDto("IVA19", 0.19m, "24089901", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public async Task Avisa_impuesto_por_unidad_con_base_y_mercancia_por_facturar_con_cruce()
    {
        var definicion = new TaxDefinition { Code = "INCB", Name = "Impuesto a las bolsas", Kind = TaxKind.Inc, CreatedBy = "test" };
        E.D.Db.TaxDefinitions.Add(definicion);
        E.D.Db.TaxRates.Add(new TaxRate { TaxDefinition = definicion, Code = "BOLSA", Name = "Bolsa", AmountPerUnit = 66m, ValidFrom = new DateOnly(2026, 1, 1), CreatedBy = "test" });
        E.D.Db.InventoryPostingRules.Add(new InventoryPostingRule("Venta", "Impuesto", E.Cuenta("24080502").Id, new DateOnly(2026, 1, 1), taxRateCode: "BOLSA", notes: "x") { CreatedBy = "test" });
        E.Cuenta("22050501").RequiresCrossDocument = true;
        E.D.Db.SaveChanges();

        var c = await ConsultarAsync();

        c.Warnings!.Should().Contain(a => a.Kind == InventoryRulesCompletenessQueryHandler.AvisoImpuestoPorUnidad && a.Account == "24080502");
        c.Warnings!.Should().Contain(a => a.Kind == InventoryRulesCompletenessQueryHandler.AvisoMercanciaConCruce && a.Account == "22050501");
        c.Summary.Total.Should().Be(3, "los avisos no suman al resumen");
    }

    [Fact]
    public async Task Con_costo_por_cooperativa_avisa_el_grupo_con_varias_cuentas_de_inventario()
    {
        var c = await ConsultarAsync();

        c.Warnings!.Should().ContainSingle(a => a.Kind == InventoryRulesCompletenessQueryHandler.AvisoGrupoConVariasCuentas)
            .Which.Message.Should().Contain("ABARROTES").And.Contain("14350503");
    }
}
