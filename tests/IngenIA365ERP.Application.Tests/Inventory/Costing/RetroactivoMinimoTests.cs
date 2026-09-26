using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, T273 (FR-042, FR-045; T18; data-model §3.1 «retroactivo»; contracts/api.md §7): el retroactivo mínimo de I1 en
/// <see cref="RegistroDeKardex"/> y las reglas de <c>Costeo.*</c> del alta de parámetros.
/// <list type="bullet">
/// <item>un documento de otra clase que deja un movimiento anterior a otro del mismo producto y ámbito se rechaza con
/// <c>Inventory.Costing.RetroactiveNotAllowed</c> nombrando el posterior (siempre hasta I5);</item>
/// <item>el saldo inicial de una bodega <c>NotActivated</c> (y su anulación) y el ajuste enlazado <c>CountAdjustmentOf</c> entran en
/// su fecha: el primero recalcula las salidas posteriores del ámbito con líneas <c>Retroactive</c> y un
/// <c>AjusteDeCostoReconocido</c> por documento afectado; un saldo intermedio bajo cero con el negativo prohibido es
/// <c>Inventory.Stock.Insufficient</c>;</item>
/// <item><c>Costeo.Metodo</c>/<c>Costeo.Ambito</c> sólo desde el primer día de un período abierto sin movimientos posteriores
/// (<c>Parameters.RequiresPeriodStart</c> con <c>earliestAllowed</c>) y <c>Peps</c> rechazado hasta I5.</item>
/// </list>
/// </summary>
public class RetroactivoMinimoTests
{
    private static readonly DateOnly D05 = new(2026, 9, 5);
    private static readonly DateOnly D10 = new(2026, 9, 10);
    private static readonly DateOnly D15 = new(2026, 9, 15);
    private static readonly DateOnly D20 = new(2026, 9, 20);
    private static readonly DateOnly D22 = new(2026, 9, 22);

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task Confirmado(KardexDePrueba k, string tipo, DateOnly fecha, Guid producto, decimal cantidad, decimal? costo = null)
    {
        var (_, r) = await k.AjusteAsync(k.Borrador(tipo, causa: tipo == "AJN" ? k.Causa() : null, fecha: fecha, lineas: [k.Linea(producto, cantidad, costo)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    // ------------------------------------------------------------------------------------- otra clase --

    [Fact]
    public async Task Un_ajuste_con_fecha_anterior_a_un_movimiento_registrado_se_rechaza_nombrando_el_posterior()
    {
        var k = await KardexDePrueba.CrearAsync();
        await Confirmado(k, "AJP", D10, k.P1, 10m, 1000m);
        await Confirmado(k, "AJN", D20, k.P1, 4m);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", fecha: D15, lineas: [k.Linea(k.P1, 2m, 1200m)]));

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Costing.RetroactiveNotAllowed");
        var datos = Datos(r.Error);
        datos.GetProperty("productCode").GetString().Should().Be("P1");
        datos.GetProperty("laterMovement").GetProperty("operationDate").GetString().Should().Be("2026-09-20");
        datos.GetProperty("laterMovement").GetProperty("displayNumber").GetString().Should().NotBeNullOrEmpty();
        (await k.C.Db.KardexEntries.CountAsync()).Should().Be(2, "el rechazo no deja nada escrito");
    }

    [Fact]
    public async Task El_parametro_de_retroactivos_no_lo_habilita_en_I1()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Parametro(ParametrosDeInventario.CosteoRetroactivosPermitidos, "true");
        await Confirmado(k, "AJP", D20, k.P1, 10m, 1000m);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", fecha: D15, lineas: [k.Linea(k.P1, 2m, 1200m)]));

        r.Error.Code.Should().Be("Inventory.Costing.RetroactiveNotAllowed", "el retroactivo general es de I5");
    }

    [Fact]
    public async Task La_misma_fecha_no_es_retroactiva_y_otro_ambito_tampoco()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Parametro(ParametrosDeInventario.CosteoAmbito, "Bodega");
        await Confirmado(k, "AJP", D20, k.P1, 10m, 1000m);

        var (_, mismaFecha) = await k.AjusteAsync(k.Borrador("AJP", fecha: D20, lineas: [k.Linea(k.P1, 1m, 1000m)]));
        var (_, otraBodega) = await k.AjusteAsync(k.Borrador("AJP", k.Segunda, fecha: D15, lineas: [k.Linea(k.P1, 1m, 1000m)]));

        mismaFecha.IsSuccess.Should().BeTrue("a igual fecha lo nuevo va después de lo registrado");
        otraBodega.IsSuccess.Should().BeTrue("con ámbito por bodega, la otra bodega es otro ámbito");
    }

    // ------------------------------------------------------------------------------ ajuste de conteo --

    [Fact]
    public async Task El_ajuste_de_un_conteo_entra_en_su_fecha_sin_cambiar_el_costo_de_las_salidas_posteriores()
    {
        var k = await KardexDePrueba.CrearAsync();
        await Confirmado(k, "AJP", D10, k.P1, 10m, 1000m);
        await Confirmado(k, "AJN", D20, k.P1, 4m);

        var ajuste = await AjusteDeConteoAsync(k, "AJP", D15, k.P1, 2m);
        var r = await k.ConfirmarAsync(ajuste);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var p1 = k.ProductoId(k.P1);
        var entrada = await k.C.Db.KardexEntries.SingleAsync(e => e.OperationDate == D15);
        entrada.Kind.Should().Be(KardexEntryKind.Entry);
        entrada.UnitCost.Should().Be(1000m, "entra al promedio de la fecha de la foto");
        (await k.C.Db.KardexEntries.AnyAsync(e => e.Reason == KardexReason.Retroactive)).Should().BeFalse(
            "con promedio ponderado un ajuste al promedio de su fecha no mueve el promedio");
        var costo = await k.C.Db.CostStates.SingleAsync(c => c.ProductId == p1);
        costo.Quantity.Should().Be(8m);
        costo.Value.Should().Be(8000m);
    }

    [Fact]
    public async Task El_ajuste_de_un_conteo_que_deja_un_saldo_intermedio_negativo_es_Stock_Insufficient()
    {
        var k = await KardexDePrueba.CrearAsync();
        await Confirmado(k, "AJP", D10, k.P1, 10m, 1000m);
        await Confirmado(k, "AJN", D20, k.P1, 8m);
        await Confirmado(k, "AJP", D22, k.P1, 10m, 1000m);

        var ajuste = await AjusteDeConteoAsync(k, "AJN", D15, k.P1, 5m);
        var r = await k.ConfirmarAsync(ajuste);

        r.IsFailure.Should().BeTrue("hoy hay 12, pero el 20 quedarían −3");
        r.Error.Code.Should().Be("Inventory.Stock.Insufficient");
        (await k.C.Db.KardexEntries.CountAsync(e => e.OperationDate == D15)).Should().Be(0);
    }

    // -------------------------------------------------------------------------------- saldo inicial --

    [Fact]
    public async Task El_saldo_inicial_de_una_bodega_no_activa_recalcula_las_salidas_posteriores_del_ambito()
    {
        var k = await KardexDePrueba.CrearAsync();
        await Confirmado(k, "AJP", D05, k.P1, 10m, 1000m);
        await Confirmado(k, "AJN", D20, k.P1, 5m);
        var salida = await k.C.Db.KardexEntries.SingleAsync(e => e.Kind == KardexEntryKind.Exit);

        var (documento, bodega) = await SaldoInicialAsync(k, D10, k.P1, 10m, 1300m);
        var registro = k.Registro();
        var r = await registro.RegistrarAsync(documento, [new MovimientoDeKardex(documento.Lines.Single(), bodega.Id, 10m, ValoracionDelMovimiento.AlCostoIndicado, 1300m)], default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        await k.C.Db.SaveChangesAsync();

        // 10 × 1.000 + 10 × 1.300 = 23.000 / 20 = 1.150: la salida de 5 del día 20 pasa de 5.000 a 5.750.
        var ajuste = await k.C.Db.KardexEntries.SingleAsync(e => e.Reason == KardexReason.Retroactive);
        ajuste.Kind.Should().Be(KardexEntryKind.CostAdjustment);
        ajuste.QuantityBase.Should().Be(0m);
        ajuste.TotalCost.Should().Be(-750m);
        ajuste.AffectsEntryId.Should().Be(salida.Id);
        ajuste.OperationDate.Should().Be(D20, "fechado en la salida afectada");
        ajuste.WarehouseId.Should().Be(k.Principal.Id, "en la bodega de la salida");
        ajuste.DocumentId.Should().Be(documento.Id, "bajo el documento que lo causa");

        var porDocumento = r.Value.AjustesRetroactivos.Should().ContainSingle().Subject;
        porDocumento.AffectedDocumentId.Should().Be(salida.DocumentId);
        porDocumento.Vendida.Should().Be(-750m);
        porDocumento.EnExistencia.Should().Be(0m);

        var costo = await k.C.Db.CostStates.SingleAsync(c => c.ProductId == k.ProductoId(k.P1));
        costo.Quantity.Should().Be(15m);
        costo.Value.Should().Be(17250m);
        costo.AverageCost.Should().Be(1150m);

        var mensajes = await new EmisionDeInventario(k.C.Db).AjustesRetroactivosAsync(r.Value, default);
        var mensaje = mensajes.Should().ContainSingle().Subject;
        mensaje.Reason.Should().Be(KardexReason.Retroactive);
        mensaje.EffectiveDate.Should().Be(D20);
        mensaje.Lines.Should().ContainSingle().Which.SoldAmount.Should().Be(-750m);
    }

    [Fact]
    public async Task El_saldo_inicial_y_su_anulacion_de_una_bodega_no_activa_estan_exentos_y_de_una_activa_no()
    {
        var k = await KardexDePrueba.CrearAsync();
        var (saldo, bodega) = await SaldoInicialAsync(k, D10, k.P1, 10m, 1300m);
        var anulacion = new InventoryDocument
        {
            Class = DocumentClass.Voiding, DocumentTypeId = k.Tipo("ANU").Id, OperationDate = D22, WarehouseId = bodega.Id, BranchId = k.Sucursal.Id,
            CreatedByUserId = KardexDePrueba.Usuario, VoidsDocumentId = saldo.Id,
        };
        k.C.Db.InventoryDocuments.Add(anulacion);
        await k.C.Db.SaveChangesAsync();
        var registro = k.Registro();

        (await registro.EsExentoAsync(saldo, default)).Should().BeTrue();
        (await registro.EsExentoAsync(anulacion, default)).Should().BeTrue();

        bodega.Activar(D10, KardexDePrueba.Usuario, DateTimeOffset.UtcNow);
        await k.C.Db.SaveChangesAsync();
        (await registro.EsExentoAsync(saldo, default)).Should().BeFalse("una bodega ya activa no admite saldo inicial retroactivo");
    }

    // ------------------------------------------------------------------------------- Costeo.* (§7) --

    [Fact]
    public async Task Costeo_Metodo_fuera_del_inicio_de_un_periodo_sin_movimientos_posteriores_es_RequiresPeriodStart()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.C.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 7, 31) });
        await k.C.Db.SaveChangesAsync();
        await Confirmado(k, "AJP", D10, k.P1, 10m, 1000m);

        var aMitadDeMes = await AltaAsync(k, ParametrosDeInventario.CosteoAmbito, "Bodega", new DateOnly(2026, 10, 15));
        var conMovimientos = await AltaAsync(k, ParametrosDeInventario.CosteoAmbito, "Bodega", new DateOnly(2026, 9, 1));
        var enElSiguiente = await AltaAsync(k, ParametrosDeInventario.CosteoAmbito, "Bodega", new DateOnly(2026, 10, 1));

        aMitadDeMes.Error.Code.Should().Be("Parameters.RequiresPeriodStart");
        Datos(aMitadDeMes.Error).GetProperty("earliestAllowed").GetString().Should().Be("2026-11-01");
        conMovimientos.Error.Code.Should().Be("Parameters.RequiresPeriodStart");
        Datos(conMovimientos.Error).GetProperty("earliestAllowed").GetString().Should().Be("2026-10-01",
            "hay kardex el 10 de septiembre: el primer mes sin movimientos posteriores es octubre");
        enElSiguiente.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Costeo_Metodo_en_un_periodo_cerrado_es_ValidFromInClosedPeriod_y_Peps_no_se_admite_hasta_I5()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.C.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 8, 31) });
        await k.C.Db.SaveChangesAsync();

        var cerrado = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "PromedioPonderado", new DateOnly(2026, 8, 1));
        var peps = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", new DateOnly(2026, 10, 1));

        cerrado.Error.Code.Should().Be("Parameters.ValidFromInClosedPeriod");
        Datos(cerrado.Error).GetProperty("lastClosedDate").GetString().Should().Be("2026-08-31");
        peps.Error.Code.Should().Be("Parameters.ValueNotAllowed");
    }

    // ------------------------------------------------------------------------------------------- apoyo --

    private static Task<Result<AddParameterVersionResponse>> AltaAsync(KardexDePrueba k, string clave, string valor, DateOnly desde)
    {
        var reglas = new ReglasDePlataformaDeInventario(k.C.Db, k.Alcance, k.Lector(), k.Permisos);
        return new AddParameterVersionCommandHandler(k.C.Db, k.Permisos, reglas, reglas).Handle(
            new AddParameterVersionCommand(ParametrosDeInventario.Modulo, clave, ParameterScopeKind.None, null, null, valor, desde, "Cambio aprobado", null),
            default);
    }

    /// <summary>Un borrador de ajuste enlazado <c>CountAdjustmentOf</c> a un conteo confirmado (lo que genera US11).</summary>
    private static async Task<Guid> AjusteDeConteoAsync(KardexDePrueba k, string tipo, DateOnly fecha, Guid producto, decimal cantidad)
    {
        var tipoDeConteo = new InventoryDocumentType { Code = "CON", Name = "Conteo", Class = DocumentClass.PhysicalCount, IsActive = true };
        k.C.Db.InventoryDocumentTypes.Add(tipoDeConteo);
        var conteo = new InventoryDocument
        {
            Class = DocumentClass.PhysicalCount, DocumentType = tipoDeConteo, OperationDate = fecha, WarehouseId = k.Principal.Id, BranchId = k.Sucursal.Id,
            CreatedByUserId = KardexDePrueba.Usuario,
        };
        k.C.Db.InventoryDocuments.Add(conteo);
        await k.C.Db.SaveChangesAsync();

        var guardado = await k.GuardarAsync(k.Borrador(tipo, causa: k.Causa(), fecha: fecha, lineas: [k.Linea(producto, cantidad)]));
        guardado.IsSuccess.Should().BeTrue(guardado.IsFailure ? guardado.Error.Message : string.Empty);
        var ajuste = await k.C.Db.InventoryDocuments.SingleAsync(d => d.PublicId == guardado.Value.PublicId);
        k.C.Db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = conteo.Id, TargetDocumentId = ajuste.Id, Kind = DocumentLinkKind.CountAdjustmentOf });
        await k.C.Db.SaveChangesAsync();
        return ajuste.PublicId;
    }

    /// <summary>Un saldo inicial en una bodega nueva <c>NotActivated</c> (lo que carga US4), guardado como borrador.</summary>
    private static async Task<(InventoryDocument Documento, Warehouse Bodega)> SaldoInicialAsync(KardexDePrueba k, DateOnly fecha, Guid producto, decimal cantidad, decimal costo)
    {
        var bodega = new Warehouse
        {
            Code = "B3", Name = "Tercera", BranchId = k.Sucursal.Id, WarehouseTypeId = k.Principal.WarehouseTypeId,
            Behavior = WarehouseBehavior.Operational, ActivationStatus = WarehouseActivationStatus.NotActivated, IsActive = true, CutoffDate = fecha,
        };
        bodega.Locations.Add(new WarehouseLocation
        {
            Warehouse = bodega, Code = WarehouseLocation.CodigoPorDefecto, Name = WarehouseLocation.NombrePorDefecto, IsDefault = true, IsActive = true,
        });
        var tipo = new InventoryDocumentType { Code = "SIN", Name = "Saldo inicial", Class = DocumentClass.OpeningBalance, IsActive = true };
        k.C.Db.AddRange(bodega, tipo);
        await k.C.Db.SaveChangesAsync();

        var productoId = k.ProductoId(producto);
        var unidad = k.C.Producto(producto).BaseUnitId;
        var documento = new InventoryDocument
        {
            Class = DocumentClass.OpeningBalance, DocumentTypeId = tipo.Id, OperationDate = fecha, WarehouseId = bodega.Id, BranchId = k.Sucursal.Id,
            CreatedByUserId = KardexDePrueba.Usuario,
        };
        documento.Lines.Add(new InventoryDocumentLine { Document = documento, LineNumber = 1, ProductId = productoId, UnitId = unidad, Quantity = cantidad, QuantityBase = cantidad, UnitCost = costo });
        k.C.Db.InventoryDocuments.Add(documento);
        await k.C.Db.SaveChangesAsync();
        return (documento, bodega);
    }
}
