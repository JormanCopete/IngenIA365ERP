using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Dashboard;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, US17, T948 (FR-088, SC-022; contracts/api.md §28): el tablero de inventario. La cooperativa es la de <see cref="VentasDePrueba"/>
/// con las ventas del 25 de septiembre de 2026 de <see cref="VistasAvanzadasTests"/> (venta neta 46.000, costo 25.000; P1 queda en 87 a
/// 1.000 y P3 en 48 a 6.000 en PRIN), la política 10 / 50 / 15 de P1 en PRIN (posición 87: nada) y de P2 en B2 (posición 0: reorden y
/// quiebre). Cada ficha lleva su <c>key</c>, su <c>unit</c>, su <c>previousValue</c> cuando compara y su <c>link</c>; las de valor exigen
/// <c>Inventory.Costs.Read</c>; el alcance por bodega recorta todas; las de mensajes, lotes, DIAN y alertas leen sólo las consultas de su área
/// y salen sólo con el permiso de esa área.
/// </summary>
public class GetInventoryDashboardQueryTests
{
    private sealed class Tablero(VentasDePrueba v)
    {
        public VentasDePrueba V { get; } = v;
        public ICurrentUserPermissions Usuario { get; } = Substitute.For<ICurrentUserPermissions>();

        public GetInventoryDashboardQueryHandler Handler()
        {
            Usuario.EsMaestroGlobal.Returns(true);
            V.Compras.C.Reloj.AhoraLocal.Returns(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.FromHours(-5)));
            var analitica = new AnaliticaDeInventario(V.Db, new ValorizadoALaFecha(V.Db, V.K.Lector()));
            var visibilidad = new VisibilidadDeAlertas(V.Db, Usuario, V.K.Actor, V.K.Alcance,
                Substitute.For<IAsignacionesDeBodega>(), Substitute.For<IAsignacionesDePuntoDeVenta>());
            return new GetInventoryDashboardQueryHandler(V.Db, V.K.Alcance, V.K.Permisos, analitica,
                new EvaluacionDeReposicion(V.Db, new PosicionDeReposicion(V.Db)), visibilidad, Array.Empty<IConsultaDeFuenteElectronica>(),
                V.K.Lector(), V.Compras.C.Reloj);
        }

        public async Task<Result<InventoryDashboardDto>> ConsultarAsync(GetInventoryDashboardQuery? q = null) =>
            await Handler().Handle(q ?? new GetInventoryDashboardQuery(), default);
    }

    private static async Task<Tablero> CrearAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        await VentaAsync(v, v.Ana, v.Luz.PublicId, v.Linea(v.P1, 10m), v.Linea(v.P3, 2m));
        await VentaAsync(v, null, null, v.Linea(v.P1, 5m));
        var aAna = v.Db.InventoryDocuments.First(d => d.CounterpartyPersonId == v.Ana.Id && d.Status == Domain.Enums.Inventory.DocumentStatus.Confirmed);
        var linea = v.Db.InventoryDocumentLines.First(l => l.DocumentId == aAna.Id && !l.IsDeleted && l.ProductId == v.K.ProductoId(v.P1));
        var nota = await v.NotaAsync(new CreditNoteDraftInput(aAna.PublicId, "Devuelve dos", false, true, [new CreditNoteLineInput(linea.PublicId, 2m)], []));
        (await v.ConfirmarAsync(nota.Value.PublicId)).IsSuccess.Should().BeTrue();

        v.Db.ReorderPolicies.AddRange(
            new ReorderPolicy { ProductId = v.K.ProductoId(v.P1), WarehouseId = v.K.Principal.Id, MinimumQuantity = 10m, MaximumQuantity = 50m, ReorderPoint = 15m },
            new ReorderPolicy { ProductId = v.K.ProductoId(v.K.P2), WarehouseId = v.K.Segunda.Id, MinimumQuantity = 10m, MaximumQuantity = 50m, ReorderPoint = 15m });
        await v.Db.SaveChangesAsync();
        return new Tablero(v);
    }

    private static async Task VentaAsync(VentasDePrueba v, Domain.Entities.Core.Person? cliente, Guid? vendedor, params SalesLineInput[] lineas)
    {
        var borrador = await v.GuardarAsync(v.Venta(cliente: cliente, vendedor: vendedor, lineas: lineas));
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        await v.GuardarAsync(v.Venta(cliente: cliente, vendedor: vendedor, pagos: [v.Pago(v.Efectivo, total)], lineas: lineas), borrador.Value.PublicId);
        (await v.ConfirmarAsync(borrador.Value.PublicId)).IsSuccess.Should().BeTrue();
    }

    private static DashboardTileDto Ficha(InventoryDashboardDto t, string key) => t.Tiles.Single(x => x.Key == key);

    [Fact]
    public async Task Con_todos_los_permisos_salen_las_fichas_de_28_con_su_unidad_y_su_enlace()
    {
        var t = (await (await CrearAsync()).ConsultarAsync()).Value;

        t.AsOf.Should().Be(Catalog.CatalogoDePrueba.Hoy);
        t.Tiles.Select(x => x.Key).Should().BeEquivalentTo(
            "inventoryValue", "turnover", "inventoryDays", "grossMargin", "salesToday", "salesMonth", "belowReorder", "stockouts", "expiringSoon",
            "messagesPending", "messagesRejected", "lateBatches", "dianPending", "dianRejected", "fiscalTypesNotPosted", "alertsPending");

        (Ficha(t, "inventoryValue").Value, Ficha(t, "inventoryValue").Unit).Should().Be((375000m, "Money"));
        Ficha(t, "inventoryValue").Link.Should().BeEquivalentTo(new DashboardLinkDto("/inventario/informes",
            new Dictionary<string, string> { ["vista"] = "valuation", ["includeTransit"] = "true" }));
        // Septiembre al 25: costo 25.000 sobre un promedio de 187.500 → 0,13 veces y 187,5 días.
        (Ficha(t, "turnover").Value, Ficha(t, "turnover").Unit).Should().Be((0.13m, "Quantity"));
        (Ficha(t, "inventoryDays").Value, Ficha(t, "inventoryDays").Unit).Should().Be((187.50m, "Days"));
        Ficha(t, "turnover").Link.Query["vista"].Should().Be("turnover");
        (Ficha(t, "grossMargin").Value, Ficha(t, "grossMargin").Unit).Should().Be((45.65m, "Percent"));
        Ficha(t, "grossMargin").Link.Query.Should().BeEquivalentTo(new Dictionary<string, string> { ["vista"] = "margin", ["by"] = "category" });
        (Ficha(t, "salesToday").Value, Ficha(t, "salesToday").PreviousValue, Ficha(t, "salesToday").Unit).Should().Be((46000m, 0m, "Money"));
        (Ficha(t, "salesMonth").Value, Ficha(t, "salesMonth").PreviousValue).Should().Be((46000m, 0m));
        Ficha(t, "salesMonth").Link.Query["vista"].Should().Be("sales-by-register");
        (Ficha(t, "belowReorder").Value, Ficha(t, "belowReorder").Unit, Ficha(t, "belowReorder").Severity).Should().Be((1m, "Count", "Warning"));
        (Ficha(t, "stockouts").Value, Ficha(t, "stockouts").Severity).Should().Be((1m, "Critical"));
        Ficha(t, "stockouts").Link.Query["vista"].Should().Be("reorder-alerts");
        Ficha(t, "expiringSoon").Link.Query["vista"].Should().Be("expiring");
        Ficha(t, "messagesPending").Link.Should().BeEquivalentTo(new DashboardLinkDto("/inventario/bandeja-de-mensajes",
            new Dictionary<string, string> { ["status"] = "Pending" }));
        Ficha(t, "messagesRejected").Link.Query["status"].Should().Be("Rejected");
        Ficha(t, "lateBatches").Link.Query["vista"].Should().Be("accounting-batches");
        Ficha(t, "dianPending").Link.Should().BeEquivalentTo(new DashboardLinkDto("/ventas/documentos-electronicos",
            new Dictionary<string, string> { ["status"] = "Pending" }));
        Ficha(t, "fiscalTypesNotPosted").Link.Page.Should().Be("/inventario/parametros");
        Ficha(t, "alertsPending").Link.Page.Should().Be("/inventario/alertas");
        t.Scope.Warehouses.Select(w => w.Code).Should().Contain(["PRIN", "B2"]);
    }

    [Fact]
    public async Task Sin_permiso_de_costos_no_salen_las_fichas_de_valor()
    {
        var x = await CrearAsync();
        x.V.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var t = (await x.ConsultarAsync()).Value;

        t.Tiles.Select(f => f.Key).Should().NotContain(["inventoryValue", "turnover", "inventoryDays", "grossMargin"]);
        Ficha(t, "salesToday").Value.Should().Be(46000m, "las ventas no son un costo");
    }

    [Fact]
    public async Task El_alcance_por_bodega_recorta_todas_las_fichas()
    {
        var x = await CrearAsync();
        x.V.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(
            new AlcanceDeInventario(false, new HashSet<int> { x.V.K.Segunda.Id }, null, false, new HashSet<int>(), null));

        var t = (await x.ConsultarAsync()).Value;

        Ficha(t, "inventoryValue").Value.Should().Be(0m, "la existencia está en PRIN");
        Ficha(t, "salesToday").Value.Should().Be(0m, "las ventas son de PRIN");
        Ficha(t, "belowReorder").Value.Should().Be(1m, "P2 en B2 sí está en el alcance");
        t.Scope.Warehouses.Select(w => w.Code).Should().Equal("B2");
        (await x.ConsultarAsync(new GetInventoryDashboardQuery(Warehouse: x.V.K.Principal.PublicId))).Error.Code
            .Should().Be(ErroresDeAlcance.CodigoBodegaInexistente, "PRIN está fuera del alcance: el 404 de la bodega");
    }

    [Fact]
    public async Task El_filtro_de_bodega_recorta_las_fichas_a_esa_bodega()
    {
        var x = await CrearAsync();

        var t = (await x.ConsultarAsync(new GetInventoryDashboardQuery(Warehouse: x.V.K.Principal.PublicId))).Value;

        Ficha(t, "belowReorder").Value.Should().Be(0m, "en PRIN, P1 está sobre su punto");
        Ficha(t, "inventoryValue").Value.Should().Be(375000m);
    }

    [Fact]
    public async Task Sin_destinatario_lista_los_tipos_levantados_sin_destinatario_pendientes()
    {
        var x = await CrearAsync();
        Alert Alerta(string tipo, bool sinDestinatario, AlertStatus estado) => new()
        {
            TypeCode = tipo, AlertTypeId = 1, Module = "Inventario", Severity = AlertSeverity.Warning, Subject = tipo, Body = tipo, DedupKey = Guid.NewGuid().ToString(),
            Status = estado, RaisedAt = DateTime.UtcNow, RaisedByKind = ActorKind.Process, RaisedByName = "Proceso", LastOccurredAt = DateTime.UtcNow,
            WithoutRecipient = sinDestinatario,
        };
        x.V.Db.Alerts.AddRange(
            Alerta("Inventario.Reorden", true, AlertStatus.Pending), Alerta("Inventario.Reorden", true, AlertStatus.Pending),
            Alerta("Inventario.Quiebre", true, AlertStatus.Attended), Alerta("Inventario.Quiebre", false, AlertStatus.Pending));
        await x.V.Db.SaveChangesAsync();

        var t = (await x.ConsultarAsync()).Value;

        t.WithoutRecipient.Select(w => w.AlertTypeCode).Should().Equal("Inventario.Reorden");
        Ficha(t, "alertsPending").Value.Should().Be(3m);
    }

    [Fact]
    public async Task Las_fichas_de_mensajes_lotes_DIAN_y_alertas_leen_su_area_y_exigen_su_permiso()
    {
        var x = await CrearAsync();
        x.V.K.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", ParameterScopeKind.DocumentType, x.V.K.Tipo("FV").Id);

        var con = (await x.ConsultarAsync()).Value;
        var pendientes = VistaDeMensajes.Visibles(x.V.Db, AlcanceDeInventario.Total).Count(d => d.Status == DeliveryStatus.Pending);
        Ficha(con, "messagesPending").Value.Should().Be(pendientes);
        Ficha(con, "dianPending").Value.Should().Be(0m, "la cooperativa de prueba no factura electrónicamente");
        Ficha(con, "fiscalTypesNotPosted").Value.Should().Be(1m, "FV es fiscal y está en NoPasa");
        Ficha(con, "fiscalTypesNotPosted").Severity.Should().Be("Warning");

        foreach (var permiso in new[] { "Inventory.Messages.View", "ElectronicInvoicing.Documents.View", "Inventory.Alerts.View", "Inventory.DocumentTypes.View" })
            x.V.K.Permisos.HasPermissionAsync(permiso, Arg.Any<CancellationToken>()).Returns(false);
        var sin = (await x.ConsultarAsync()).Value;

        sin.Tiles.Select(f => f.Key).Should().NotContain(
            ["messagesPending", "messagesRejected", "lateBatches", "dianPending", "dianRejected", "alertsPending", "fiscalTypesNotPosted"]);
        sin.WithoutRecipient.Should().BeEmpty();
    }
}
