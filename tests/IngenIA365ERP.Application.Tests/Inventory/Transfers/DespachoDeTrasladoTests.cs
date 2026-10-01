using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Transfers;

/// <summary>
/// Feature 012, T359 (FR-039, US10-1; contracts/api.md §11; mensajes.md §6.6): el despacho sale del origen y entra al tránsito de
/// la <b>sucursal de origen</b> al costo de origen, sin cambiar el valor total; el destino lo ve «en tránsito» y no disponible; emite
/// <c>TrasladoDespachado</c> v1 y sella el modo de la cadena <c>Transfers</c>. Origen igual al destino, destino no activo, origen de
/// tránsito y origen fuera del alcance se rechazan; el destino puede estar fuera del alcance de quien despacha.
/// </summary>
public class DespachoDeTrasladoTests
{
    [Fact]
    public async Task Sale_del_origen_y_entra_al_transito_de_su_sucursal_al_mismo_costo()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);
        await t.K.EntradaAsync(t.K.P1, 10m, 1_300m);
        var valorAntes = t.ValorTotal(t.K.P1);

        var (despacho, r) = await t.TrasladoAsync(t.PRIN, t.PV2, t.K.Linea(t.K.P1, 10m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.DisplayNumber.Should().Be("TRD1");
        var documento = t.Documento(despacho);
        documento.TransitWarehouseId.Should().Be(t.TR01.Id, "el tránsito es el de la sucursal de ORIGEN, no el de Florida");
        t.Fisico(t.K.P1, t.PRIN).Should().Be(10m);
        t.Fisico(t.K.P1, t.TR01).Should().Be(10m);
        t.Fisico(t.K.P1, t.PV2).Should().Be(0m, "el destino no lo tiene disponible");
        t.ValorTotal(t.K.P1).Should().Be(valorAntes, "un traslado no cambia el valor total");

        var kardex = await t.Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).OrderBy(k => k.Id).ToListAsync();
        kardex.Where(k => k.Kind != KardexEntryKind.CostAdjustment).Select(k => (k.WarehouseId, k.QuantityBase, k.UnitCost, k.TotalCost)).Should().Equal(
            (t.PRIN.Id, -10m, 1_150m, -11_500m), (t.TR01.Id, 10m, 1_150m, 11_500m));
        documento.Lines.Single().UnitCost.Should().Be(1_150m);
        documento.CostTotal.Should().Be(11_500m);

        var costo = await t.Db.CostStates.AsNoTracking().SingleAsync(c => c.ProductId == t.K.ProductoId(t.K.P1));
        costo.LastUnitCost.Should().Be(1_300m, "moverla de lugar no es una compra: el último costo no cambia");

        var enTransito = await new PosicionDeReposicion(t.Db).EnTransitoAsync([t.K.ProductoId(t.K.P1)], [t.PV2.Id], default);
        enTransito.Should().ContainSingle().Which.Quantity.Should().Be(10m);
    }

    [Fact]
    public async Task Emite_TrasladoDespachado_y_sella_el_modo_de_la_cadena()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);

        t.Documento(despacho).PostingMode.Should().Be(PostingMode.Online);
        var mensaje = await t.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == despacho);
        mensaje.Type.Should().Be(TrasladoDespachadoV1.Type);
        using var json = JsonDocument.Parse(mensaje.PayloadJson);
        var raiz = json.RootElement;
        raiz.GetProperty("operation").GetString().Should().Be("DespachoTraslado");
        raiz.GetProperty("transitWarehouseCode").GetString().Should().Be("TR01");
        raiz.GetProperty("destinationWarehouseCode").GetString().Should().Be("PV2");
        var linea = raiz.GetProperty("lines").EnumerateArray().Single();
        linea.GetProperty("fromWarehouseCode").GetString().Should().Be("PRIN");
        linea.GetProperty("toWarehouseCode").GetString().Should().Be("TR01");
        linea.GetProperty("quantityBase").GetDecimal().Should().Be(10m);
        linea.GetProperty("cost").GetDecimal().Should().Be(10_000m);
        linea.GetProperty("accountingGroupCode").GetString().Should().Be("ABARR");
    }

    [Fact]
    public async Task Origen_igual_al_destino_se_rechaza()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);

        var (_, r) = await t.TrasladoAsync(t.PRIN, t.PRIN, t.K.Linea(t.K.P1, 1m));

        t.Codigo(r).Should().Be("Inventory.Transfer.SameWarehouse");
    }

    [Fact]
    public async Task Un_destino_no_activo_se_rechaza()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);
        var nueva = new Warehouse
        {
            Code = "PV3", Name = "Sin activar", BranchId = t.Florida.Id, WarehouseTypeId = t.PV2.WarehouseTypeId,
            Behavior = WarehouseBehavior.Operational, ActivationStatus = WarehouseActivationStatus.NotActivated, IsActive = true,
        };
        t.Db.Warehouses.Add(nueva);
        await t.Db.SaveChangesAsync();

        var (_, r) = await t.TrasladoAsync(t.PRIN, nueva, t.K.Linea(t.K.P1, 1m));

        t.Codigo(r).Should().Be("Inventory.Warehouse.NotActive");
    }

    [Fact]
    public async Task El_transito_nunca_despacha()
    {
        var t = await TrasladosDePrueba.CrearAsync();

        var (_, r) = await t.TrasladoAsync(t.TR01, t.PV2, t.K.Linea(t.K.P1, 1m));

        t.Codigo(r).Should().Be("Inventory.Document.TransitNotAllowed");
    }

    [Fact]
    public async Task Sin_existencia_en_el_origen_no_despacha()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 3m, 1_000m);

        var (_, r) = await t.TrasladoAsync(t.PRIN, t.PV2, t.K.Linea(t.K.P1, 5m));

        t.Codigo(r).Should().Be("Inventory.Stock.Insufficient");
        t.Fisico(t.K.P1, t.TR01).Should().Be(0m);
    }

    [Fact]
    public async Task Exige_alcance_sobre_el_origen_y_admite_un_destino_fuera_de_el()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);

        t.SoloEn(t.PV2);
        var fuera = await t.GuardarDespachoAsync(t.Despacho(t.PRIN, t.PV2, null, t.K.Linea(t.K.P1, 1m)));
        t.Codigo(fuera).Should().Be(ErroresDeAlcance.CodigoBodegaInexistente, "el origen está fuera del alcance: el mismo 404");

        t.SoloEn(t.PRIN);
        var (_, r) = await t.TrasladoAsync(t.PRIN, t.PV2, t.K.Linea(t.K.P1, 1m));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    [Fact]
    public async Task Por_la_ruta_de_traslados_no_se_arma_una_recepcion()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var borrador = t.Despacho(t.PRIN, t.PV2, null, t.K.Linea(t.K.P1, 1m)) with { DocumentTypePublicId = t.Tipo("TRR") };

        var r = await t.GuardarDespachoAsync(borrador);

        t.Codigo(r).Should().Be("Inventory.Document.TypeNotForRoute");
    }
}
