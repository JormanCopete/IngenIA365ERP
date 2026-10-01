using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 012, I6 (T861; contracts/plantillas.md §8, filas I6; contracts/contabilidad.md §2.6): la semilla de tipos de documento
/// deja uno por defecto para cada clase de I6 —cotización, pedido, remisión y nota débito con su consecutivo propio (T16: las notas
/// no usan resolución), la factura desde remisiones sin consecutivo (numera por la resolución <c>Invoice</c>) y el ensamble—, sin
/// duplicar por código lo que la cooperativa ya tiene de I1 a I5; y el mapeo contable trae la fila por tipo de la remisión hacia
/// <c>SI</c>.
/// </summary>
public class SemillasDeComercioAmpliadoTests
{
    private static readonly DocumentClass[] ClasesDeI6 =
    [
        DocumentClass.SalesQuote, DocumentClass.SalesOrder, DocumentClass.Shipment, DocumentClass.DebitNote,
        DocumentClass.SalesInvoiceFromShipments, DocumentClass.Assembly,
    ];

    [Fact]
    public void Cada_clase_de_I6_tiene_codigo_propuesto_unico()
    {
        foreach (var clase in ClasesDeI6)
            InventoryDocumentTypesSeeder.Sembrados.Should().ContainKey(clase);
        InventoryDocumentTypesSeeder.Sembrados.Values.Select(v => v.Codigo)
            .Concat(InventoryDocumentTypesSeeder.AjustesDeConteo.Values.Select(v => v.Codigo))
            .Should().OnlyHaveUniqueItems();
        InventoryDocumentTypesSeeder.Sembrados.Values.Should().OnlyContain(v => v.Codigo.Length <= 10);
    }

    [Fact]
    public async Task Con_la_entrega_I6_deja_un_tipo_por_clase_y_sólo_las_numeradas_por_consecutivo_llevan_secuencia()
    {
        using var db = TestDbContextFactory.Create();

        await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I6, default);

        var tipos = await db.InventoryDocumentTypes.Include(t => t.Sequences)
            .Where(t => ClasesDeI6.Contains(t.Class)).ToListAsync();
        tipos.Select(t => t.Class).Should().BeEquivalentTo(ClasesDeI6);
        tipos.Should().OnlyContain(t => t.IsSeeded && t.IsActive && t.AllWarehouses);

        foreach (var clase in new[] { DocumentClass.SalesQuote, DocumentClass.SalesOrder, DocumentClass.Shipment, DocumentClass.DebitNote, DocumentClass.Assembly })
        {
            var tipo = tipos.Single(t => t.Class == clase);
            tipo.Sequences.Should().ContainSingle($"{clase} numera por consecutivo propio (T16)");
            tipo.Sequences.Single().Prefix.Should().BeEmpty();
            tipo.Sequences.Single().NextValue.Should().Be(1);
            tipo.Sequences.Single().ValidFrom.Should().Be(InventoryDocumentTypesSeeder.VigenciaDeLaSemilla);
        }
        tipos.Single(t => t.Class == DocumentClass.SalesInvoiceFromShipments).Sequences
            .Should().BeEmpty("la factura desde remisiones numera por la resolución DIAN, no por consecutivo propio");
    }

    [Fact]
    public async Task Sobre_una_cooperativa_con_los_tipos_de_I1_a_I5_sólo_agrega_los_de_I6_y_es_idempotente()
    {
        using var db = TestDbContextFactory.Create();
        await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I5, default);
        var antes = await db.InventoryDocumentTypes.CountAsync();
        (await db.InventoryDocumentTypes.AnyAsync(t => ClasesDeI6.Contains(t.Class))).Should().BeFalse("mientras la entrega sea I5 no se siembran");

        var insertadas = await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I6, default);

        insertadas.Should().Be(ClasesDeI6.Length);
        (await db.InventoryDocumentTypes.CountAsync()).Should().Be(antes + ClasesDeI6.Length);
        (await InventoryDocumentTypesSeeder.AplicarAsync(db, EntregaDelComercio.I6, default)).Should().Be(0, "idempotente por código");
    }

    [Fact]
    public void El_mapeo_contable_trae_la_fila_por_tipo_de_la_remision_hacia_SI()
    {
        var remision = InventoryDocumentTypesSeeder.Sembrados[DocumentClass.Shipment].Codigo;

        var filas = InventoryVoucherMappingsSeeder.Semillas();

        filas.Should().ContainSingle(s => s.InventoryDocumentTypeCode == remision)
            .Which.Should().Match<InventoryVoucherMappingsSeeder.Semilla>(s => s.Operation == "CostoDeVenta" && s.VoucherTypeCode == "SI");
        filas.Select(s => (s.Operation, s.InventoryDocumentTypeCode)).Should().OnlyHaveUniqueItems();
    }
}
