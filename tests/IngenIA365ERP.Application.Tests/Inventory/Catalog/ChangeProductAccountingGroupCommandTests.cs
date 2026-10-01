using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Tests.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// Feature 012, T275 (FR-027; contracts/api.md §3.6.4; data-model §1.10): el cambio de grupo contable de un producto. Con
/// existencia deja <c>ProductAccountingGroupChange</c> con cantidad, valor y desglose por bodega y emite
/// <c>GrupoContableReclasificado</c> (<c>Reclassification</c>, modo de paso general); sin existencia sólo la fila. Sus errores
/// (<c>Inventory.Product.NotInventoriable</c>, <c>.AccountingGroupUnchanged</c>, <c>Inventory.AccountingGroup.Inactive</c>,
/// <c>Inventory.Period.Closed</c>, <c>Inventory.Document.DateInFuture</c>, <c>Inventory.Product.MovementsAfterEffectiveDate</c>
/// con <c>lastMovementDate</c>), el grupo de un producto a una fecha (§1.10) y el historial con el valor sólo con
/// <c>Inventory.Costs.Read</c>.
/// </summary>
public class ChangeProductAccountingGroupCommandTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task<(PeriodosDePrueba P, AccountingGroup Aseo)> ConOtroGrupoAsync()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        var aseo = new AccountingGroup { Code = "ASEO", Name = "Aseo", IsActive = true };
        p.K.C.Db.AccountingGroups.Add(aseo);
        await p.K.C.Db.SaveChangesAsync();
        return (p, aseo);
    }

    [Fact]
    public async Task Con_existencia_deja_el_cambio_por_bodega_y_emite_GrupoContableReclasificado()
    {
        var (p, aseo) = await ConOtroGrupoAsync();
        var k = p.K;
        await p.AjusteAsync("AJP", new DateOnly(2026, 9, 10), k.P1, 10m, 1000m);
        await p.AjusteAsync("AJP", new DateOnly(2026, 9, 12), k.P1, 5m, 1300m, k.Segunda);

        var r = await p.ReclasificarAsync(k.P1, aseo.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.EffectiveDate.Should().Be(Hoy, "por defecto, hoy");
        r.Value.From.Code.Should().Be("ABARR");
        r.Value.To.Code.Should().Be("ASEO");
        r.Value.ByWarehouse.Select(b => (b.WarehouseCode, b.Quantity, b.Value)).Should().BeEquivalentTo(
            [("PRIN", 10m, (decimal?)11000m), ("B2", 5m, (decimal?)5500m)]);

        var cambio = await k.C.Db.ProductAccountingGroupChanges.SingleAsync();
        cambio.Quantity.Should().Be(15m);
        cambio.Value.Should().Be(16500m);
        cambio.FromAccountingGroupId.Should().Be(k.C.GrupoAbarrotes.Id);
        cambio.ToAccountingGroupId.Should().Be(aseo.Id);
        cambio.Reason.Should().Be("Cambio de línea");
        var detalle = JsonDocument.Parse(cambio.DetailJson).RootElement.EnumerateArray().ToList();
        detalle.Should().HaveCount(2);
        detalle.Select(d => d.GetProperty("warehouseCode").GetString()).Should().BeEquivalentTo(["PRIN", "B2"]);
        (await k.C.Db.Products.SingleAsync(x => x.PublicId == k.P1)).AccountingGroupId.Should().Be(aseo.Id);

        var mensaje = await k.C.Db.IntegrationMessages.SingleAsync(m => m.Type == GrupoContableReclasificadoV1.Type);
        mensaje.OriginKind.Should().Be(MessageOriginKind.Operation);
        mensaje.OriginPublicId.Should().Be(cambio.PublicId);
        mensaje.OriginEventKey.Should().Be(ClavesDeEvento.Reclasificacion);
        mensaje.OperationDate.Should().Be(Hoy);
        r.Value.MessagePublicId.Should().Be(mensaje.PublicId);
        var entrega = await k.C.Db.IntegrationMessageDeliveries.SingleAsync(e => e.MessageId == mensaje.Id);
        entrega.Mode.Should().Be(DeliveryMode.Online, "sigue el valor general del modo de paso (EnLinea por defecto)");
        mensaje.PayloadJson.Should().Contain("\"fromAccountingGroupCode\":\"ABARR\"").And.Contain("\"toAccountingGroupCode\":\"ASEO\"");
    }

    [Fact]
    public async Task Sin_existencia_solo_deja_la_fila_y_el_historial_muestra_el_valor_solo_con_permiso_de_costos()
    {
        var (p, aseo) = await ConOtroGrupoAsync();
        var r = await p.ReclasificarAsync(p.K.P2, aseo.PublicId);

        r.IsSuccess.Should().BeTrue();
        r.Value.MessagePublicId.Should().BeNull();
        r.Value.ByWarehouse.Should().BeEmpty();
        (await p.K.C.Db.IntegrationMessages.CountAsync()).Should().Be(0);

        p.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);
        var historial = await new GetProductAccountingGroupHistoryQueryHandler(p.K.C.Db, p.K.Permisos)
            .Handle(new GetProductAccountingGroupHistoryQuery(p.K.P2), default);
        var fila = historial.Value.Should().ContainSingle().Subject;
        fila.From.Code.Should().Be("ABARR");
        fila.To.Code.Should().Be("ASEO");
        fila.Value.Should().BeNull();
        fila.MessagePublicId.Should().BeNull();
    }

    [Fact]
    public async Task Los_errores_del_cambio_de_grupo()
    {
        var (p, aseo) = await ConOtroGrupoAsync();
        var k = p.K;
        var inactivo = new AccountingGroup { Code = "VIEJO", Name = "Viejo", IsActive = false };
        k.C.Db.AccountingGroups.Add(inactivo);
        await k.C.Db.SaveChangesAsync();
        var servicio = (await k.C.ProductoAsync(k.C.Alta("S1", "Transporte", ProductKind.Service))).PublicId;
        await p.AjusteAsync("AJP", new DateOnly(2026, 9, 20), k.P1, 10m, 1000m);

        (await p.ReclasificarAsync(servicio, aseo.PublicId)).Error.Code.Should().Be("Inventory.Product.NotInventoriable");
        (await p.ReclasificarAsync(k.P1, k.C.GrupoAbarrotes.PublicId)).Error.Code.Should().Be("Inventory.Product.AccountingGroupUnchanged");
        (await p.ReclasificarAsync(k.P1, inactivo.PublicId)).Error.Code.Should().Be("Inventory.AccountingGroup.Inactive");
        (await p.ReclasificarAsync(k.P1, aseo.PublicId, Hoy.AddDays(1))).Error.Code.Should().Be("Inventory.Document.DateInFuture");

        var anterior = await p.ReclasificarAsync(k.P1, aseo.PublicId, new DateOnly(2026, 9, 15));
        anterior.Error.Code.Should().Be("Inventory.Product.MovementsAfterEffectiveDate");
        Datos(anterior.Error).GetProperty("lastMovementDate").GetString().Should().Be("2026-09-20");

        p.Setup.LastClosedDate = new DateOnly(2026, 8, 31);
        await k.C.Db.SaveChangesAsync();
        (await p.ReclasificarAsync(k.P2, aseo.PublicId, new DateOnly(2026, 8, 20))).Error.Code.Should().Be("Inventory.Period.Closed");

        (await k.C.Db.ProductAccountingGroupChanges.CountAsync()).Should().Be(0);
    }

    // --------------------------------------------------------------------------------- grupo a una fecha --

    [Fact]
    public void El_grupo_a_una_fecha_sigue_el_historial()
    {
        var cambios = new[]
        {
            new ProductAccountingGroupChange { ProductId = 1, FromAccountingGroupId = 10, ToAccountingGroupId = 20, EffectiveDate = new DateOnly(2026, 8, 1) },
            new ProductAccountingGroupChange { ProductId = 1, FromAccountingGroupId = 20, ToAccountingGroupId = 30, EffectiveDate = new DateOnly(2026, 9, 1) },
        };

        GrupoContableALaFecha.De(30, [], new DateOnly(2026, 7, 1)).Should().Be(30, "sin cambios, el actual");
        GrupoContableALaFecha.De(30, cambios, new DateOnly(2026, 7, 31)).Should().Be(10, "antes del primer cambio, su From");
        GrupoContableALaFecha.De(30, cambios, new DateOnly(2026, 8, 1)).Should().Be(20, "desde la fecha efectiva, su To");
        GrupoContableALaFecha.De(30, cambios, new DateOnly(2026, 8, 31)).Should().Be(20);
        GrupoContableALaFecha.De(30, cambios, new DateOnly(2026, 9, 25)).Should().Be(30);
    }

    [Fact]
    public async Task Los_mensajes_de_un_documento_llevan_el_grupo_de_su_fecha()
    {
        var (p, aseo) = await ConOtroGrupoAsync();
        await p.AjusteAsync("AJP", new DateOnly(2026, 9, 10), p.K.P1, 10m, 1000m);
        (await p.ReclasificarAsync(p.K.P1, aseo.PublicId)).IsSuccess.Should().BeTrue();

        var grupos = await GrupoContableALaFecha.CodigosAsync(p.K.C.Db, [p.K.ProductoId(p.K.P1)], new DateOnly(2026, 9, 10), default);

        grupos[p.K.ProductoId(p.K.P1)].Should().Be("ABARR", "el 10 de septiembre el producto estaba en abarrotes");
        var ajuste = await p.K.C.Db.IntegrationMessages.FirstAsync(m => m.Type == AjusteInventarioAprobadoV1.Type);
        ajuste.PayloadJson.Should().Contain("\"accountingGroupCode\":\"ABARR\"");
    }
}
