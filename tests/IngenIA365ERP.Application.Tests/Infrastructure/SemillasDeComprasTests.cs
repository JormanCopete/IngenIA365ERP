using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 012, I5 (T785): la semilla de tipos de documento deja uno por defecto, con su consecutivo, para la solicitud de
/// compra, la orden de compra y los costos adicionales, sin duplicar por código los que la cooperativa ya tiene de I1 a I4.
/// </summary>
public class SemillasDeComprasTests
{
    private static readonly DocumentClass[] ClasesDeI5 = [DocumentClass.PurchaseRequest, DocumentClass.PurchaseOrder, DocumentClass.LandedCost];

    [Fact]
    public void Las_clases_de_compras_de_I5_tienen_codigo_propuesto()
    {
        InventoryDocumentTypesSeeder.Sembrados[DocumentClass.PurchaseRequest].Codigo.Should().Be("SOC");
        InventoryDocumentTypesSeeder.Sembrados[DocumentClass.PurchaseOrder].Codigo.Should().Be("ORC");
        InventoryDocumentTypesSeeder.Sembrados[DocumentClass.LandedCost].Codigo.Should().Be("CAD");
        InventoryDocumentTypesSeeder.Sembrados.Values.Select(v => v.Codigo)
            .Concat(InventoryDocumentTypesSeeder.AjustesDeConteo.Values.Select(v => v.Codigo))
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Con_la_entrega_I5_vigente_deja_los_tres_tipos_con_su_consecutivo()
    {
        using var db = TestDbContextFactory.Create();

        await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I5, default);

        var tipos = await db.InventoryDocumentTypes.Include(t => t.Sequences)
            .Where(t => ClasesDeI5.Contains(t.Class)).ToListAsync();
        tipos.Select(t => t.Code).Should().BeEquivalentTo(["SOC", "ORC", "CAD"]);
        tipos.Should().OnlyContain(t => t.IsSeeded && t.IsActive && t.AllWarehouses && t.Sequences.Count == 1
            && t.Sequences.Single().Prefix == string.Empty && t.Sequences.Single().NextValue == 1
            && t.Sequences.Single().ValidFrom == InventoryDocumentTypesSeeder.VigenciaDeLaSemilla);
        tipos.Single(t => t.Code == "SOC").RequiresCounterparty.Should().BeFalse("la solicitud la hace la bodega, sin proveedor (data-model §5.2)");
        tipos.Single(t => t.Code == "ORC").RequiresCounterparty.Should().BeTrue("la orden va a un proveedor");
        tipos.Single(t => t.Code == "CAD").RequiresCounterparty.Should().BeTrue("los costos adicionales son del proveedor del flete");
    }

    [Fact]
    public async Task Sobre_una_cooperativa_que_ya_tiene_los_tipos_anteriores_sólo_agrega_los_de_I5_y_es_idempotente()
    {
        using var db = TestDbContextFactory.Create();
        await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I4, default);
        var antes = await db.InventoryDocumentTypes.Select(t => t.Code).ToListAsync();
        antes.Should().NotContain(["SOC", "ORC", "CAD"]);

        var insertadas = await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I5, default);

        insertadas.Should().Be(3);
        var despues = await db.InventoryDocumentTypes.Select(t => t.Code).ToListAsync();
        despues.Should().OnlyHaveUniqueItems().And.HaveCount(antes.Count + 3);
        (await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I5, default)).Should().Be(0, "idempotente por código");
    }

    [Fact]
    public async Task Mientras_la_entrega_vigente_sea_anterior_a_I5_no_se_siembran_los_tipos_de_compras_de_I5()
    {
        using var db = TestDbContextFactory.Create();

        await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I4, default);

        (await db.InventoryDocumentTypes.AnyAsync(t => ClasesDeI5.Contains(t.Class))).Should().BeFalse();
    }
}
