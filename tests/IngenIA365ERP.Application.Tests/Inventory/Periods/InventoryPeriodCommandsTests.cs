using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Periods;

/// <summary>
/// Feature 012, T276 (FR-047; contracts/api.md §13.4; data-model §6.1–§6.3): cerrar y reabrir un mes de inventario. Sólo el
/// siguiente al último cerrado (<c>Inventory.Period.NotNext</c> con <c>nextToClose</c>) y un mes terminado en hora de Colombia
/// (<c>.NotEnded</c>); un conteo abierto con foto en el mes bloquea (<c>.OpenCounts</c>, US11, T401) y uno cerrado o descartado no; borradores y
/// mensajes avisan (<c>.WarningsNotAcknowledged</c>) y con <c>acknowledgeWarnings</c> se cierra guardando los avisos; el cierre
/// fija <c>LastClosedDate</c>, <c>CloseVersion</c> y el valorizado por producto × bodega con el grupo a la fecha, sin filas en
/// cero y con Σ por ámbito = <c>CostState.Value</c>, y emite <c>PeriodoInventarioCerrado</c> (<c>Close:{versión}</c>). Reabrir
/// sólo el último (<c>.NotLastClosed</c>, <c>.NotClosed</c>) deja <c>Superseded</c> su valorizado, retrocede
/// <c>LastClosedDate</c> y emite <c>PeriodoInventarioReabierto</c> (<c>Reopen:{versión}</c>).
/// </summary>
public class InventoryPeriodCommandsTests
{
    private static readonly DateOnly Julio10 = new(2026, 7, 10);
    private static readonly DateOnly Julio12 = new(2026, 7, 12);

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Solo_se_cierra_el_siguiente_al_ultimo_cerrado_y_un_mes_terminado()
    {
        var p = await PeriodosDePrueba.CrearAsync();

        var agosto = await p.CerrarAsync(2026, 8, reconocer: false);
        agosto.Error.Code.Should().Be("Inventory.Period.NotNext");
        var siguiente = Datos(agosto.Error).GetProperty("nextToClose");
        (siguiente.GetProperty("year").GetInt32(), siguiente.GetProperty("month").GetInt32()).Should().Be((2026, 7));

        var q = await PeriodosDePrueba.CrearAsync(ultimoCierre: new DateOnly(2026, 8, 31));
        var septiembre = await q.CerrarAsync(2026, 9);
        septiembre.Error.Code.Should().Be("Inventory.Period.NotEnded", "hoy es 25 de septiembre en Colombia");
    }

    [Fact]
    public async Task Sin_puesta_en_marcha_no_hay_meses_que_cerrar()
    {
        var k = await Kardex.KardexDePrueba.CrearAsync();
        var r = await new CloseInventoryPeriodCommandHandler(k.C.Db, new RevisionDeCierre(k.C.Db, k.C.Reloj), new ValorizadoALaFecha(k.C.Db, k.Lector()),
                k.Cerrojo, new EmisorDeMensajes(k.C.Db, k.Actor, k.C.Reloj), k.Actor, k.Permisos, k.C.Reloj)
            .Handle(new CloseInventoryPeriodCommand(2026, 7), default);

        r.Error.Code.Should().Be("Inventory.Period.NotStarted");
    }

    [Fact]
    public async Task Los_borradores_y_los_mensajes_del_mes_avisan_y_con_reconocerlos_se_cierra()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        var borrador = await p.K.GuardarAsync(p.K.Borrador("AJP", fecha: Julio12, lineas: [p.K.Linea(p.K.P1, 1m, 1000m)]));
        borrador.IsSuccess.Should().BeTrue();

        var revision = await p.RevisarAsync(2026, 7);
        revision.Value.CanClose.Should().BeTrue();
        revision.Value.Blockers.OpenCounts.Should().BeEmpty("no hay conteos abiertos en julio");
        revision.Value.Warnings.Drafts.Should().Be(1);
        revision.Value.Warnings.Messages.Pending.Should().Be(1, "el ajuste confirmado dejó su mensaje pendiente para Contabilidad");

        var primero = await p.CerrarAsync(2026, 7, reconocer: false);
        primero.Error.Code.Should().Be("Inventory.Period.WarningsNotAcknowledged");
        Datos(primero.Error).GetProperty("warnings").GetProperty("drafts").GetInt32().Should().Be(1);

        var cerrado = await p.CerrarAsync(2026, 7, reconocer: true);
        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? cerrado.Error.Message : string.Empty);
        var periodo = await p.K.C.Db.InventoryPeriods.SingleAsync();
        periodo.CloseWarningsJson.Should().Contain("\"drafts\":1");
    }

    [Fact]
    public async Task El_cierre_fija_el_valorizado_por_producto_y_bodega_y_emite_el_mensaje()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        await p.AjusteAsync("AJP", Julio12, p.K.P1, 5m, 1300m, p.K.Segunda);
        await p.AjusteAsync("AJP", Julio12, p.K.P2, 3m, 500m);
        await p.AjusteAsync("AJN", Julio12, p.K.P2, 3m);

        var r = await p.CerrarAsync(2026, 7);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.ClosingVersion.Should().Be(1);
        var setup = await p.K.C.Db.InventorySetups.SingleAsync();
        setup.LastClosedDate.Should().Be(new DateOnly(2026, 7, 31));
        var periodo = await p.K.C.Db.InventoryPeriods.SingleAsync();
        periodo.Status.Should().Be(InventoryPeriodStatus.Closed);
        periodo.CloseVersion.Should().Be(1);
        periodo.ClosedByUserId.Should().Be(Kardex.KardexDePrueba.Usuario);

        // 10 × 1.000 + 5 × 1.300 = 16.500 / 15 = 1.100 en toda la cooperativa: 11.000 en la principal y 5.500 en la segunda.
        var saldos = await p.K.C.Db.PeriodClosingBalances.OrderBy(b => b.WarehouseId).ToListAsync();
        saldos.Should().HaveCount(2, "P2 quedó en cero y no se guarda");
        saldos.Should().OnlyContain(b => b.Version == 1 && !b.Superseded && b.AccountingGroupId == p.K.C.GrupoAbarrotes.Id);
        saldos.Select(b => (b.WarehouseId, b.Quantity, b.Value)).Should().Equal(
            (p.K.Principal.Id, 10m, 11000m), (p.K.Segunda.Id, 5m, 5500m));
        var costo = await p.K.C.Db.CostStates.SingleAsync(c => c.ProductId == p.K.ProductoId(p.K.P1));
        saldos.Sum(b => b.Value).Should().Be(costo.Value);

        var mensaje = await p.K.C.Db.IntegrationMessages.SingleAsync(m => m.Type == PeriodoInventarioCerradoV1.Type);
        mensaje.OriginEventKey.Should().Be(ClavesDeEvento.Cierre(1));
        mensaje.OriginPublicId.Should().Be(periodo.PublicId);
        mensaje.OperationDate.Should().Be(new DateOnly(2026, 7, 31));
        r.Value.MessagePublicId.Should().Be(mensaje.PublicId);
        r.Value.Valuation.Select(v => (v.Warehouse.Code, v.Quantity, v.Value)).Should().BeEquivalentTo([("PRIN", 10m, (decimal?)11000m), ("B2", 5m, (decimal?)5500m)]);
        r.Value.Total.Should().Be(16500m);
    }

    [Fact]
    public async Task Sin_permiso_de_costos_el_valorizado_del_cierre_sale_sin_valores()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        p.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var r = await p.CerrarAsync(2026, 7);

        r.Value.Valuation.Should().OnlyContain(v => v.Value == null);
        r.Value.Total.Should().BeNull();
    }

    [Fact]
    public async Task Cerrado_un_mes_un_documento_fechado_en_el_se_rechaza_nombrando_el_periodo()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();

        var (_, r) = await p.K.AjusteAsync(p.K.Borrador("AJP", fecha: Julio12, lineas: [p.K.Linea(p.K.P1, 1m, 1000m)]));

        r.Error.Code.Should().Be("Inventory.Period.Closed");
        var datos = Datos(r.Error);
        (datos.GetProperty("year").GetInt32(), datos.GetProperty("month").GetInt32()).Should().Be((2026, 7));
        datos.GetProperty("lastClosedDate").GetString().Should().Be("2026-07-31");
    }

    [Fact]
    public async Task Solo_se_reabre_el_ultimo_cerrado_y_la_reapertura_deja_superseded_su_valorizado()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();

        (await p.ReabrirAsync(2026, 8)).Error.Code.Should().Be("Inventory.Period.NotClosed");
        (await p.CerrarAsync(2026, 8)).IsSuccess.Should().BeTrue();
        var julio = await p.ReabrirAsync(2026, 7);
        julio.Error.Code.Should().Be("Inventory.Period.NotLastClosed");
        var ultimo = Datos(julio.Error).GetProperty("lastClosed");
        (ultimo.GetProperty("year").GetInt32(), ultimo.GetProperty("month").GetInt32()).Should().Be((2026, 8));

        var agosto = await p.ReabrirAsync(2026, 8);

        agosto.IsSuccess.Should().BeTrue(agosto.IsFailure ? agosto.Error.Message : string.Empty);
        agosto.Value.SupersededClosingVersion.Should().Be(1);
        var periodo = await p.K.C.Db.InventoryPeriods.SingleAsync(x => x.Month == 8);
        periodo.Status.Should().Be(InventoryPeriodStatus.Open);
        periodo.ReopenReason.Should().Be("Ajuste de costo tardío");
        (await p.K.C.Db.PeriodClosingBalances.Where(b => b.PeriodId == periodo.Id).ToListAsync()).Should().OnlyContain(b => b.Superseded);
        (await p.K.C.Db.PeriodClosingBalances.Where(b => b.PeriodId != periodo.Id).ToListAsync()).Should().OnlyContain(b => !b.Superseded);
        (await p.K.C.Db.InventorySetups.SingleAsync()).LastClosedDate.Should().Be(new DateOnly(2026, 7, 31));
        var mensaje = await p.K.C.Db.IntegrationMessages.SingleAsync(m => m.Type == PeriodoInventarioReabiertoV1.Type);
        mensaje.OriginEventKey.Should().Be(ClavesDeEvento.Reapertura(1));

        var recierre = await p.CerrarAsync(2026, 8);
        recierre.Value.ClosingVersion.Should().Be(2, "cada recierre sube la versión");
        (await p.K.C.Db.IntegrationMessages.CountAsync(m => m.Type == PeriodoInventarioCerradoV1.Type)).Should().Be(3);
    }

    [Fact]
    public async Task Reabrir_el_primer_mes_deja_sin_ultimo_cierre_y_la_lista_muestra_los_meses_desde_el_inicio()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();
        (await p.ReabrirAsync(2026, 7)).IsSuccess.Should().BeTrue();
        (await p.K.C.Db.InventorySetups.SingleAsync()).LastClosedDate.Should().BeNull();
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();

        var lista = await new ListInventoryPeriodsQueryHandler(p.K.C.Db, p.K.C.Reloj).Handle(new ListInventoryPeriodsQuery(), default);

        lista.Value.StartDate.Should().Be(PeriodosDePrueba.Inicio);
        lista.Value.LastClosedDate.Should().Be(new DateOnly(2026, 7, 31));
        lista.Value.Periods.Select(x => (x.Month, x.Status)).Should().Equal(
            (7, InventoryPeriodStatus.Closed), (8, InventoryPeriodStatus.Open), (9, InventoryPeriodStatus.Open));
        var julio = lista.Value.Periods[0];
        julio.ClosingVersion.Should().Be(2);
        julio.ReopenReason.Should().Be("Ajuste de costo tardío");
    }

    // ------------------------------------------------------------------------ US10 (T377): diferencias de traslado --

    /// <summary>Un despacho y su recepción de julio, confirmados, con una diferencia de <paramref name="cantidad"/>.</summary>
    private static async Task<(Domain.Entities.Inventory.Documents.InventoryDocument Despacho, Domain.Entities.Inventory.Documents.TransferDiscrepancy Diferencia)>
        TrasladoConDiferenciaAsync(PeriodosDePrueba p, decimal cantidad)
    {
        var db = p.K.C.Db;
        var tipo = p.K.Tipo("AJP").Id;
        var despacho = new Domain.Entities.Inventory.Documents.InventoryDocument
        {
            Class = DocumentClass.TransferDispatch, DocumentTypeId = tipo, OperationDate = Julio10, WarehouseId = p.K.Principal.Id,
            DestinationWarehouseId = p.K.Segunda.Id, TransitWarehouseId = p.K.Transito.Id, BranchId = p.K.Sucursal.Id, Prefix = "TRD", Number = 1,
        };
        var recepcion = new Domain.Entities.Inventory.Documents.InventoryDocument
        {
            Class = DocumentClass.TransferReceipt, DocumentTypeId = tipo, OperationDate = Julio12, WarehouseId = p.K.Principal.Id,
            DestinationWarehouseId = p.K.Segunda.Id, TransitWarehouseId = p.K.Transito.Id, BranchId = p.K.Sucursal.Id, Prefix = "TRR", Number = 1,
        };
        var linea = new Domain.Entities.Inventory.Documents.InventoryDocumentLine { Document = despacho, LineNumber = 1, ProductId = p.K.ProductoId(p.K.P1), QuantityBase = 10m, Quantity = 10m };
        despacho.Lines.Add(linea);
        despacho.Confirmar(Kardex.KardexDePrueba.Usuario, DateTime.UtcNow);
        recepcion.Confirmar(Kardex.KardexDePrueba.Usuario, DateTime.UtcNow);
        db.InventoryDocuments.AddRange(despacho, recepcion);
        db.DocumentLinks.Add(new Domain.Entities.Inventory.Documents.DocumentLink { SourceDocument = despacho, TargetDocument = recepcion, Kind = DocumentLinkKind.ReceiptOf });
        await db.SaveChangesAsync();
        var diferencia = new Domain.Entities.Inventory.Documents.TransferDiscrepancy
        {
            DispatchDocumentId = despacho.Id, ReceiptDocumentId = recepcion.Id, DispatchLineId = linea.Id, ProductId = linea.ProductId,
            Kind = TransferDiscrepancyKind.Shortage, QuantityBase = cantidad, UnitCost = 1000m,
        };
        db.TransferDiscrepancies.Add(diferencia);
        await db.SaveChangesAsync();
        return (despacho, diferencia);
    }

    [Fact]
    public async Task Una_diferencia_de_traslado_sin_resolver_avisa_y_con_reconocerla_se_cierra()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        var (despacho, _) = await TrasladoConDiferenciaAsync(p, 1m);

        var revision = await p.RevisarAsync(2026, 7);
        var aviso = revision.Value.Warnings.UnresolvedTransfers.Should().ContainSingle().Subject;
        (aviso.DispatchPublicId, aviso.DisplayNumber, aviso.PendingBase).Should().Be((despacho.PublicId, "TRD1", 1m));

        var primero = await p.CerrarAsync(2026, 7, reconocer: false);
        primero.Error.Code.Should().Be("Inventory.Period.WarningsNotAcknowledged");

        var cerrado = await p.CerrarAsync(2026, 7, reconocer: true);
        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? $"{cerrado.Error.Code}: {cerrado.Error.Message}" : string.Empty);
        (await p.K.C.Db.InventoryPeriods.SingleAsync()).CloseWarningsJson.Should().Contain(despacho.PublicId.ToString());
    }

    [Fact]
    public async Task Una_diferencia_de_traslado_resuelta_no_avisa()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        var (_, diferencia) = await TrasladoConDiferenciaAsync(p, 1m);
        diferencia.PedirResolucion(TransferDiscrepancyResolution.LateReceipt, 1m, 9, DateTime.UtcNow, "llegó", null, 999);
        diferencia.Resolver(1m, DateTime.UtcNow);
        await p.K.C.Db.SaveChangesAsync();

        var revision = await p.RevisarAsync(2026, 7);

        revision.Value.Warnings.UnresolvedTransfers.Should().BeEmpty();
        revision.Value.Warnings.Any.Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------- US11 (T401): conteos abiertos --

    /// <summary>Un conteo de PRIN con foto el 10 de julio, en el estado pedido.</summary>
    private static async Task<Domain.Entities.Inventory.Documents.InventoryDocument> ConteoConFotoAsync(PeriodosDePrueba p, DocumentStatus estado)
    {
        var db = p.K.C.Db;
        var tipo = new Domain.Entities.Inventory.Documents.InventoryDocumentType { Code = "CON", Name = "Conteo", Class = DocumentClass.PhysicalCount, IsActive = true };
        db.InventoryDocumentTypes.Add(tipo);
        var conteo = new Domain.Entities.Inventory.Documents.InventoryDocument
        {
            Class = DocumentClass.PhysicalCount, DocumentType = tipo, OperationDate = Julio10, WarehouseId = p.K.Principal.Id, BranchId = p.K.Sucursal.Id,
            CountKind = CountKind.Total, CountScope = CountScope.All, CountSnapshotAt = new DateTime(2026, 7, 10, 15, 0, 0, DateTimeKind.Utc), CountRound = 1,
        };
        if (estado == DocumentStatus.Confirmed) conteo.Confirmar(Kardex.KardexDePrueba.Usuario, DateTime.UtcNow);
        if (estado == DocumentStatus.Discarded) conteo.Descartar(Kardex.KardexDePrueba.Usuario, DateTime.UtcNow, "se reprograma");
        db.InventoryDocuments.Add(conteo);
        await db.SaveChangesAsync();
        return conteo;
    }

    [Fact]
    public async Task Un_conteo_abierto_con_foto_en_el_mes_bloquea_el_cierre_y_lo_nombra()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        var conteo = await ConteoConFotoAsync(p, DocumentStatus.Draft);

        var revision = await p.RevisarAsync(2026, 7);
        revision.Value.CanClose.Should().BeFalse();
        revision.Value.Blockers.OpenCounts.Should().ContainSingle().Which.Should().Be(new InventoryErrors.ConteoAbierto(conteo.PublicId, null, "PRIN", Julio10));

        var r = await p.CerrarAsync(2026, 7);
        r.Error.Code.Should().Be("Inventory.Period.OpenCounts");
        Datos(r.Error).GetProperty("counts").EnumerateArray().Single().GetProperty("countPublicId").GetGuid().Should().Be(conteo.PublicId);
        (await p.K.C.Db.InventorySetups.SingleAsync()).LastClosedDate.Should().BeNull();

        (await p.RevisarAsync(2026, 8)).Value.Blockers.OpenCounts.Should().BeEmpty("la foto es de julio");
    }

    [Theory]
    [InlineData(DocumentStatus.Confirmed)]
    [InlineData(DocumentStatus.Discarded)]
    public async Task Un_conteo_cerrado_o_descartado_no_bloquea(DocumentStatus estado)
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await ConteoConFotoAsync(p, estado);

        (await p.RevisarAsync(2026, 7)).Value.Blockers.OpenCounts.Should().BeEmpty();
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();
    }
}
