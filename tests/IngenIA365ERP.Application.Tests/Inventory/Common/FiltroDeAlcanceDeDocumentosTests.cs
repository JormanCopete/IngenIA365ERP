using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, T140 (T35, FR-009; data-model §5.2): la visibilidad de <c>INV_Documents</c> sobre las entidades reales
/// —origen <b>o</b> destino; sin bodega, por las bodegas de sus orígenes (<c>INV_DocumentLinks</c>)—, con EF en memoria.
/// Falla cerrado.
/// </summary>
public class FiltroDeAlcanceDeDocumentosTests
{
    private const int Norte = 1, Sur = 2;

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private InventoryDocument Doc(string nota, int? origen, int? destino = null)
    {
        var d = new InventoryDocument { Class = DocumentClass.PurchaseReceipt, WarehouseId = origen, DestinationWarehouseId = destino, Notes = nota };
        _db.InventoryDocuments.Add(d);
        _db.SaveChanges();
        return d;
    }

    private void Vincular(InventoryDocument origen, InventoryDocument destino)
    {
        _db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = origen.Id, TargetDocumentId = destino.Id, Kind = DocumentLinkKind.InvoiceOfReceipt });
        _db.SaveChanges();
    }

    private static AlcanceDeInventario Solo(params int[] bodegas) => AlcanceDeInventario.Vacio with { Bodegas = bodegas.ToHashSet() };

    private List<string?> Visibles(AlcanceDeInventario alcance) =>
        _db.InventoryDocuments.DocumentosVisibles(alcance, _db.DocumentLinks, _db.InventoryDocuments)
            .OrderBy(d => d.Id).Select(d => d.Notes).ToList();

    [Fact]
    public async Task Por_origen_o_destino_y_sin_bodega_por_sus_origenes()
    {
        var recepNorte = Doc("REC-N", Norte);
        var recepSur = Doc("REC-S", Sur);
        Doc("TR-SN", Sur, Norte);
        var facturaNorte = Doc("FP-N", null);
        var facturaAmbas = Doc("FP-NS", null);
        Doc("CAJA", null);
        Vincular(recepNorte, facturaNorte);
        Vincular(recepNorte, facturaAmbas);
        Vincular(recepSur, facturaAmbas);

        Visibles(Solo(Norte)).Should().Equal("REC-N", "TR-SN", "FP-N", "FP-NS");
        Visibles(Solo(Sur)).Should().Equal("REC-S", "TR-SN", "FP-NS");
        Visibles(AlcanceDeInventario.Vacio).Should().BeEmpty("sin asignaciones falla cerrado");
        Visibles(AlcanceDeInventario.Total).Should().HaveCount(6);

        var bodegas = await FiltroDeAlcance.BodegasDeSusOrigenesAsync(_db, facturaAmbas.Id);
        bodegas.Should().BeEquivalentTo([Norte, Sur]);
        FiltroDeAlcance.DocumentoVisible(Solo(Sur), facturaAmbas, bodegas).Should().BeTrue();
        FiltroDeAlcance.DocumentoSinBodegaOperable(Solo(Sur), bodegas).Should().BeFalse("se opera sólo si todas sus bodegas están en el alcance");
        FiltroDeAlcance.DocumentoSinBodegaOperable(Solo(Norte, Sur), bodegas).Should().BeTrue();
    }

    [Fact]
    public void Un_vinculo_dado_de_baja_no_da_visibilidad()
    {
        var recep = Doc("REC-N", Norte);
        var factura = Doc("FP", null);
        _db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = recep.Id, TargetDocumentId = factura.Id, Kind = DocumentLinkKind.InvoiceOfReceipt, IsDeleted = true });
        _db.SaveChanges();

        // El filtro de borrado del modelo real deja fuera los vínculos de baja; aquí se aplica explícitamente.
        _db.InventoryDocuments.DocumentosVisibles(Solo(Norte), _db.DocumentLinks.Where(l => !l.IsDeleted), _db.InventoryDocuments)
            .Select(d => d.Notes).Should().Equal("REC-N");
    }

    [Fact]
    public void DocumentoVisible_con_bodega_mira_origen_y_destino()
    {
        var traslado = new InventoryDocument { WarehouseId = Sur, DestinationWarehouseId = Norte };
        FiltroDeAlcance.DocumentoVisible(Solo(Norte), traslado, []).Should().BeTrue();
        FiltroDeAlcance.DocumentoVisible(Solo(3), traslado, []).Should().BeFalse();
        FiltroDeAlcance.DocumentoVisible(AlcanceDeInventario.Vacio, new InventoryDocument(), []).Should().BeFalse();
    }
}
