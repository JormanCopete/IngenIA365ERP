using FluentAssertions;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents.Queries;
using IngenIA365ERP.Application.Inventory.Transfers;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Transfers;

/// <summary>
/// Feature 012, T363 (US2-5 aplicado a traslados, <c>FiltroDeAlcance</c>; contracts/api.md §11, §17.2): quien sólo tiene alcance sobre
/// PV2 ve los traslados que llegan a PV2 —en la lista genérica, en la de traslados y en el detalle— y no los demás documentos de
/// PRIN; los destinos de un traslado son todas las bodegas operativas y activas de la cooperativa, con sólo código, nombre y
/// sucursal, para quien tiene <c>Inventory.Transfers.Create</c>.
/// </summary>
public class AlcanceDeTrasladosTests
{
    [Fact]
    public async Task Quien_solo_ve_el_destino_ve_el_traslado_que_le_llega_y_no_lo_demas_del_origen()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        var ajuste = await t.Db.InventoryDocuments.FirstAsync(d => d.Class == DocumentClass.PositiveAdjustment);

        t.SoloEn(t.PV2);
        var documentos = await new ListInventoryDocumentsQueryHandler(t.Db, t.K.Maestros(), t.K.Alcance, t.K.Vista())
            .Handle(new ListInventoryDocumentsQuery(new FiltrosDeDocumentos(), new PageRequest()), default);
        documentos.Value.Items.Select(d => d.PublicId).Should().Contain(despacho).And.NotContain(ajuste.PublicId);

        var traslados = await new ListTransfersQueryHandler(t.Db, t.K.Alcance, t.K.Maestros(), t.K.Vista(), t.Vista())
            .Handle(new ListTransfersQuery(), default);
        var fila = traslados.Value.Items.Should().ContainSingle().Subject;
        fila.DispatchPublicId.Should().Be(despacho);
        fila.State.Should().Be(EstadosDeTraslado.EnTransito);
        fila.QuantityDispatched.Should().Be(10m);

        var detalle = await new GetTransferQueryHandler(t.Db, t.K.Vista(), t.Vista()).Handle(new GetTransferQuery(despacho), default);
        detalle.IsSuccess.Should().BeTrue(detalle.IsFailure ? detalle.Error.Code : string.Empty);
        detalle.Value.Lines.Single().PendingBase.Should().Be(10m);
        detalle.Value.Dispatch.AllowedActions.Should().Contain("Receive").And.NotContain("Void", "anular es de quien opera el origen");

        t.SoloEn(t.K.Segunda);
        var ajeno = await new GetTransferQueryHandler(t.Db, t.K.Vista(), t.Vista()).Handle(new GetTransferQuery(despacho), default);
        t.Codigo(ajeno).Should().Be("Inventory.Document.NotFound");
    }

    [Fact]
    public async Task El_estado_y_las_diferencias_se_ven_desde_la_lista()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        (await t.RecibirAsync(despacho, null, t.Recibida(despacho, 9m))).IsSuccess.Should().BeTrue();

        var traslados = await new ListTransfersQueryHandler(t.Db, t.K.Alcance, t.K.Maestros(), t.K.Vista(), t.Vista())
            .Handle(new ListTransfersQuery(EstadosDeTraslado.RecibidoConDiferencias), default);
        var fila = traslados.Value.Items.Should().ContainSingle().Subject;
        (fila.QuantityReceived, fila.PendingDiscrepancies).Should().Be((9m, 1));

        var diferencias = await new ListTransferDiscrepanciesQueryHandler(t.Db, t.K.Alcance, t.K.Maestros(), t.Vista())
            .Handle(new ListTransferDiscrepanciesQuery("Pending"), default);
        var diferencia = diferencias.Value.Items.Should().ContainSingle().Subject;
        diferencia.Kind.Should().Be(TransferDiscrepancyKind.Shortage);
        diferencia.Transfer.DispatchPublicId.Should().Be(despacho);
        diferencia.Value.Should().Be(1_000m, "con Inventory.Costs.Read");

        var detalle = await new GetTransferQueryHandler(t.Db, t.K.Vista(), t.Vista()).Handle(new GetTransferQuery(despacho), default);
        var linea = detalle.Value.Lines.Single();
        (linea.DispatchedBase, linea.ReceivedBase, linea.ShortageBase, linea.PendingBase).Should().Be((10m, 9m, 1m, 1m));
        detalle.Value.Receipts.Should().ContainSingle();
    }

    [Fact]
    public async Task Los_destinos_son_todas_las_operativas_activas_para_quien_crea_traslados()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        t.Db.Warehouses.Add(new Warehouse
        {
            Code = "PV3", Name = "Sin activar", BranchId = t.Florida.Id, WarehouseTypeId = t.PV2.WarehouseTypeId,
            Behavior = WarehouseBehavior.Operational, ActivationStatus = WarehouseActivationStatus.NotActivated, IsActive = true,
        });
        await t.Db.SaveChangesAsync();
        t.SoloEn(t.PRIN);

        var r = await new ListTransferDestinationsQueryHandler(t.Db, t.K.Permisos).Handle(new ListTransferDestinationsQuery(), default);

        r.Value.Select(d => d.Code).Should().BeEquivalentTo(["PRIN", "B2", "PV2"], "fuera del alcance, sin tránsitos ni bodegas sin activar");
        r.Value.Single(d => d.Code == "PV2").Branch.Name.Should().Be("Florida");

        t.K.Permisos.HasPermissionAsync(ListTransferDestinationsQuery.PermisoRequerido, Arg.Any<CancellationToken>()).Returns(false);
        var sinPermiso = await new ListTransferDestinationsQueryHandler(t.Db, t.K.Permisos).Handle(new ListTransferDestinationsQuery(), default);
        t.Codigo(sinPermiso).Should().Be("Inventory.Warehouse.NotFound");
    }
}
