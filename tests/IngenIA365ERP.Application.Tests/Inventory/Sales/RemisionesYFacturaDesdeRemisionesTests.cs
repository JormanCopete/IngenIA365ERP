using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I6, T865 (US14-2, US14-3, FR-052, FR-075, T9; contracts/api.md §18.3, §18.4): la remisión descarga al promedio y reconoce el
/// costo; la factura desde remisiones no vuelve a descargar, hereda el modo de sus remisiones y no pasa de lo pendiente; su nota crédito no
/// mueve existencia y deja la remisión otra vez pendiente; anular una remisión depende de si ya se facturó. P1: 100 a 1.000 en PRIN.
/// </summary>
public class RemisionesYFacturaDesdeRemisionesTests
{
    [Fact]
    public async Task La_remision_sale_al_promedio_y_emite_solo_el_costo_de_venta()
    {
        var c = await CicloComercialDePrueba.CrearAsync();

        var remision = await c.RemisionAsync(5m);

        var salida = c.KardexDe(remision).Should().ContainSingle().Subject;
        (salida.Kind, salida.QuantityBase, salida.UnitCost, salida.TotalCost).Should().Be((KardexEntryKind.Exit, -5m, 1000m, -5000m));
        remision.Lines.Single(l => !l.IsDeleted).UnitCost.Should().Be(1000m, "la línea guarda el costo con que salió");
        c.MensajesDe(remision.PublicId).Should().Equal(CostoDeVentaReconocidoV1.Type);
        var pendiente = await c.Vinculos().PendientePorFacturarAsync(remision.Lines.Where(l => !l.IsDeleted).ToList(), 0, default);
        pendiente.Values.Should().Equal(5m);
    }

    [Fact]
    public async Task La_factura_desde_dos_remisiones_no_escribe_kardex_hereda_su_modo_y_sale_sin_costo()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var r1 = await c.RemisionAsync(5m);
        var r2 = await c.RemisionAsync(3m);

        var factura = await c.FacturaAsync("FVR", [r1.PublicId, r2.PublicId]);
        var confirmada = await c.ConfirmarAsync(factura);

        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? $"{confirmada.Error.Code}: {confirmada.Error.Message}" : null);
        var documento = c.Doc(factura);
        documento.Lines.Where(l => !l.IsDeleted).Select(l => l.QuantityBase).Should().BeEquivalentTo([5m, 3m]);
        c.KardexDe(documento).Should().BeEmpty("lo facturado ya salió por las remisiones");
        c.MensajesDe(factura).Should().Equal(VentaFacturadaV1.Type);
        documento.PostingMode.Should().Be(r1.PostingMode, "un derivado copia el modo de sus remisiones (T9)");
        c.Fisico(c.P1).Should().Be(92m);
        var pendiente = await c.Vinculos().PendientePorFacturarAsync(c.Doc(r1.PublicId).Lines.Concat(c.Doc(r2.PublicId).Lines).ToList(), 0, default);
        pendiente.Values.Should().AllSatisfy(v => v.Should().Be(0m));
    }

    [Fact]
    public async Task Remisiones_con_modos_de_paso_distintos_no_se_facturan_juntas()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var r1 = await c.RemisionAsync(2m);
        var r2 = await c.RemisionAsync(2m);
        c.Db.InventoryDocuments.Single(d => d.Id == r2.Id).PostingMode = r1.PostingMode == PostingMode.Online ? PostingMode.NotPosted : PostingMode.Online;
        await c.Db.SaveChangesAsync();

        var r = await c.ConfirmarAsync(await c.FacturaAsync("FVR", [r1.PublicId, r2.PublicId]));

        r.Error.Code.Should().Be("Inventory.PostingMode.ChainMismatch");
    }

    [Fact]
    public async Task Facturar_mas_de_lo_pendiente_responde_ya_facturado_con_las_lineas()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var remision = await c.RemisionAsync(5m);
        var primera = await c.FacturaAsync("FVR", [remision.PublicId]);
        var segunda = await c.FacturaAsync("FVR", [remision.PublicId]);
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(primera));

        var r = await c.ConfirmarAsync(segunda);

        r.Error.Code.Should().Be("Inventory.Shipment.AlreadyInvoiced");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new
        {
            lines = new[] { new { ShipmentLinePublicId = remision.Lines.Single(l => !l.IsDeleted).PublicId, Pending = 0m, Requested = 5m } },
        });
    }

    [Fact]
    public async Task La_nota_credito_de_la_factura_desde_remisiones_no_devuelve_mercancia_y_deja_la_remision_pendiente()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var remision = await c.RemisionAsync(4m);
        var factura = await c.FacturaAsync("FVR", [remision.PublicId]);
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(factura));
        c.E.Estado(factura, ElectronicDocumentStatus.Validated, "CUFE-FVR-1");

        var nota = CicloComercialDePrueba.Exito(await c.Nota().Handle(new SaveCreditNoteDraftCommand(null,
            new CreditNoteDraftInput(factura, "Se factura otra vez", TotalVoid: true, WithReturn: true, [], [], CorrectionConceptCode: "2")), default));
        var confirmada = await c.ConfirmarAsync(nota.PublicId);

        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? $"{confirmada.Error.Code}: {confirmada.Error.Message}" : null);
        var documento = c.Doc(nota.PublicId);
        documento.ReturnsGoods.Should().BeFalse("la mercancía remisionada vuelve anulando la remisión");
        c.KardexDe(documento).Should().BeEmpty();
        c.Fisico(c.P1).Should().Be(96m);
        var pendiente = await c.Vinculos().PendientePorFacturarAsync(c.Doc(remision.PublicId).Lines.ToList(), 0, default);
        pendiente.Values.Should().Equal(4m);
    }

    [Fact]
    public async Task La_nota_credito_sin_devolucion_de_una_factura_directa_no_toca_la_existencia_y_con_devolucion_entra_al_costo_de_salida()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var factura = await c.FacturaAsync("FV", [], c.Linea(c.P1, 4m));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(factura));
        c.E.Estado(factura, ElectronicDocumentStatus.Validated, "CUFE-FV-1");
        var linea = c.Doc(factura).Lines.Single(l => !l.IsDeleted);
        await c.V.K.EntradaAsync(c.P1, 4m, 3000m); // el promedio cambia después de la venta

        var rebaja = CicloComercialDePrueba.Exito(await c.Nota().Handle(new SaveCreditNoteDraftCommand(null,
            new CreditNoteDraftInput(factura, "Rebaja", false, false, [new CreditNoteLineInput(linea.PublicId, Amount: 1000m)], [], CorrectionConceptCode: "3")), default));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(rebaja.PublicId));
        c.KardexDe(c.Doc(rebaja.PublicId)).Should().BeEmpty();

        var devolucion = CicloComercialDePrueba.Exito(await c.Nota().Handle(new SaveCreditNoteDraftCommand(null,
            new CreditNoteDraftInput(factura, "Devuelve dos", false, true, [new CreditNoteLineInput(linea.PublicId, 2m)], [], CorrectionConceptCode: "1")), default));
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(devolucion.PublicId));
        c.KardexDe(c.Doc(devolucion.PublicId)).Should().ContainSingle(k => k.QuantityBase == 2m && k.UnitCost == 1000m, "entra al costo con que salió");
    }

    [Fact]
    public async Task Anular_una_remision_facturada_tiene_dependientes_y_una_sin_facturar_vuelve_al_costo_con_que_salio()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var facturada = await c.RemisionAsync(3m);
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(await c.FacturaAsync("FVR", [facturada.PublicId])));
        var libre = await c.RemisionAsync(2m);
        await c.V.K.EntradaAsync(c.P1, 5m, 3000m); // el promedio cambia después de la remisión

        var conDependiente = await c.AnularAsync(facturada.PublicId);
        var anulada = await c.AnularAsync(libre.PublicId);

        conDependiente.Error.Code.Should().Be("Inventory.Document.HasDependents");
        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? $"{anulada.Error.Code}: {anulada.Error.Message}" : null);
        var contrario = c.Doc(anulada.Value.VoidingDocumentPublicId);
        c.KardexDe(contrario).Should().ContainSingle(k => k.Kind == KardexEntryKind.Entry && k.QuantityBase == 2m && k.UnitCost == 1000m);
        c.MensajesDe(contrario.PublicId).Should().Contain(DocumentoAnuladoV1.Type);
        c.Db.InventoryDocuments.AsNoTracking().Single(d => d.Id == libre.Id).Status.Should().Be(DocumentStatus.Voided);
    }
}
