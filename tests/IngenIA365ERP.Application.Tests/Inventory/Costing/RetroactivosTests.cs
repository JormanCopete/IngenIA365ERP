using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Costing;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, I5, US16, T823 (FR-045, FR-075; T18, T42b, T42g; api.md §9.3; mensajes.md §6.10): el retroactivo general.
/// <list type="bullet">
/// <item>con <c>Costeo.RetroactivosPermitidos = false</c>, <c>Inventory.Costing.RetroactiveNotAllowed</c> nombrando el movimiento
/// posterior (US16-3); más atrás que <c>Costeo.RetroactivosDiasMaximos</c>, <c>.RetroactiveTooOld</c>; en período cerrado,
/// <c>Inventory.Period.Closed</c>; con PEPS, <c>.RetroactiveRequiresWeightedAverage</c> (D6);</item>
/// <item>el saldo inicial de una bodega <c>NotActivated</c> sigue admitido sin el parámetro (excepción de I1, T18);</item>
/// <item><c>cost-impact</c> devuelve lo mismo que luego escribe la confirmación y no guarda nada; la confirmación deja un
/// «AjusteDeCostoReconocido» por documento afectado, con <c>effectiveDate</c> = la fecha de la salida afectada.</item>
/// </list>
/// Las salidas son ajustes negativos (salen del inventario: su diferencia es «vendida»). La entrega se sube a I5 en el fixture.
/// </summary>
public class RetroactivosTests
{
    private static readonly DateOnly D05 = new(2026, 9, 5);
    private static readonly DateOnly D10 = new(2026, 9, 10);
    private static readonly DateOnly D15 = new(2026, 9, 15);
    private static readonly DateOnly D20 = new(2026, 9, 20);
    private static readonly DateOnly D22 = new(2026, 9, 22);

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task<KardexDePrueba> CooperativaAsync(bool permitidos = true, int dias = 30)
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Entrega = EntregaDelComercio.I5;
        k.Parametro(ParametrosDeInventario.CosteoRetroactivosPermitidos, permitidos ? "true" : "false");
        k.Parametro(ParametrosDeInventario.CosteoRetroactivosDiasMaximos, dias.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return k;
    }

    private static async Task<Guid> Confirmado(KardexDePrueba k, string tipo, DateOnly fecha, Guid producto, decimal cantidad, decimal? costo = null)
    {
        var (documento, r) = await k.AjusteAsync(k.Borrador(tipo, causa: tipo == "AJN" ? k.Causa() : null, fecha: fecha, lineas: [k.Linea(producto, cantidad, costo)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return documento;
    }

    /// <summary>Una entrada el 5 y tres salidas (15, 20 y 22) ya confirmadas.</summary>
    private static async Task<(Guid S15, Guid S20, Guid S22)> TresSalidasAsync(KardexDePrueba k)
    {
        await Confirmado(k, "AJP", D05, k.P1, 10m, 1000m);
        var s15 = await Confirmado(k, "AJN", D15, k.P1, 3m);
        var s20 = await Confirmado(k, "AJN", D20, k.P1, 4m);
        var s22 = await Confirmado(k, "AJN", D22, k.P1, 2m);
        k.C.Db.ChangeTracker.Clear();
        return (s15, s20, s22);
    }

    // ------------------------------------------------------------------------------------------ puerta --

    [Fact]
    public async Task Con_el_parametro_apagado_se_rechaza_nombrando_el_movimiento_posterior()
    {
        var k = await CooperativaAsync(permitidos: false);
        await TresSalidasAsync(k);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", fecha: D10, lineas: [k.Linea(k.P1, 10m, 1300m)]));

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Costing.RetroactiveNotAllowed");
        Datos(r.Error).GetProperty("laterMovement").GetProperty("operationDate").GetString().Should().Be("2026-09-15");
        (await k.C.Db.KardexEntries.CountAsync(e => e.OperationDate == D10)).Should().Be(0);
    }

    [Fact]
    public async Task Mas_atras_que_los_dias_maximos_es_RetroactiveTooOld()
    {
        var k = await CooperativaAsync(dias: 5); // hoy 2026-09-25: la primera fecha admitida es el 20
        await TresSalidasAsync(k);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", fecha: D10, lineas: [k.Linea(k.P1, 10m, 1300m)]));

        r.Error.Code.Should().Be("Inventory.Costing.RetroactiveTooOld");
        var datos = Datos(r.Error);
        datos.GetProperty("maxDays").GetInt32().Should().Be(5);
        datos.GetProperty("earliestAllowed").GetString().Should().Be("2026-09-20");
    }

    [Fact]
    public async Task En_un_periodo_cerrado_sigue_siendo_Period_Closed()
    {
        var k = await CooperativaAsync(dias: 60);
        await Confirmado(k, "AJP", D05, k.P1, 10m, 1000m);
        k.C.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 8, 31) });
        await k.C.Db.SaveChangesAsync();

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", fecha: new DateOnly(2026, 8, 20), lineas: [k.Linea(k.P1, 1m, 1000m)]));

        r.Error.Code.Should().Be("Inventory.Period.Closed");
    }

    [Fact]
    public async Task Con_PEPS_vigente_se_rechaza_por_D6()
    {
        var k = await CooperativaAsync();
        k.Parametro(ParametrosDeInventario.CosteoMetodo, "Peps");
        await TresSalidasAsync(k);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJP", fecha: D10, lineas: [k.Linea(k.P1, 10m, 1300m)]));

        r.Error.Code.Should().Be("Inventory.Costing.RetroactiveRequiresWeightedAverage");
        Datos(r.Error).GetProperty("productCode").GetString().Should().Be("P1");
    }

    [Fact]
    public async Task El_saldo_inicial_de_una_bodega_no_activa_sigue_admitido_sin_el_parametro()
    {
        var k = await CooperativaAsync(permitidos: false);
        await Confirmado(k, "AJP", D05, k.P1, 10m, 1000m);
        await Confirmado(k, "AJN", D20, k.P1, 5m);

        var (documento, bodega) = await SaldoInicialAsync(k, D10, k.P1, 10m, 1300m);
        var r = await k.Registro().RegistrarAsync(documento,
            [new MovimientoDeKardex(documento.Lines.Single(), bodega.Id, 10m, ValoracionDelMovimiento.AlCostoIndicado, 1300m)], default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.AjustesRetroactivos.Should().ContainSingle().Which.Vendida.Should().Be(-750m);
    }

    // ------------------------------------------------------------------------- impacto y confirmación --

    [Fact]
    public async Task Cost_impact_muestra_lo_mismo_que_escribe_la_confirmacion_y_no_guarda_nada()
    {
        var k = await CooperativaAsync();
        var (s15, s20, s22) = await TresSalidasAsync(k);
        var guardado = await k.GuardarAsync(k.Borrador("AJP", fecha: D10, lineas: [k.Linea(k.P1, 10m, 1300m)]));
        guardado.IsSuccess.Should().BeTrue();
        k.C.Db.ChangeTracker.Clear();
        var filasAntes = await k.C.Db.KardexEntries.CountAsync();
        var costoAntes = await k.C.Db.CostStates.AsNoTracking().SingleAsync();

        var registro = k.Registro();
        var impacto = await new GetDocumentCostImpactQueryHandler(k.C.Db, k.Vista(), k.Efectos(registro), registro)
            .Handle(new GetDocumentCostImpactQuery(guardado.Value.PublicId), default);

        impacto.IsSuccess.Should().BeTrue(impacto.IsFailure ? $"{impacto.Error.Code}: {impacto.Error.Message}" : string.Empty);
        impacto.Value.Retroactive.Should().BeTrue();
        // 10 × 1.000 + 10 × 1.300 = 23.000 / 20 = 1.150: las salidas de 3, 4 y 2 pasan de 1.000 a 1.150.
        impacto.Value.Affected.Select(a => (a.DocumentPublicId, a.InventoryAmount, a.SoldAmount))
            .Should().Equal((s15, 0m, -450m), (s20, 0m, -600m), (s22, 0m, -300m));
        impacto.Value.Total.Should().Be(-1350m);
        (await k.C.Db.KardexEntries.CountAsync()).Should().Be(filasAntes, "cost-impact es una consulta: no escribe");
        (await k.C.Db.CostStates.AsNoTracking().SingleAsync()).Value.Should().Be(costoAntes.Value);
        (await k.C.Db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.PublicId == guardado.Value.PublicId)).Status.Should().Be(DocumentStatus.Draft);

        k.C.Db.ChangeTracker.Clear();
        var r = await k.ConfirmarAsync(guardado.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var ajustes = await k.C.Db.KardexEntries.AsNoTracking().Where(e => e.Reason == KardexReason.Retroactive).OrderBy(e => e.OperationDate).ToListAsync();
        ajustes.Select(a => (a.OperationDate, a.TotalCost)).Should().Equal((D15, -450m), (D20, -600m), (D22, -300m));
        ajustes.Should().OnlyContain(a => a.Kind == KardexEntryKind.CostAdjustment && a.QuantityBase == 0m && a.AffectsEntryId != null);
        var salidas = await k.C.Db.KardexEntries.AsNoTracking().Where(e => e.Kind == KardexEntryKind.Exit).ToListAsync();
        salidas.Should().OnlyContain(s => s.UnitCost == 1000m, "las líneas de las salidas no cambian: se agregan ajustes");

        var mensajes = await k.C.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == AjusteDeCostoReconocidoV1.Type).ToListAsync();
        mensajes.Select(m => m.RelatedPublicId).Should().BeEquivalentTo([s15, s20, s22], "un AjusteDeCostoReconocido por documento afectado");
        foreach (var m in mensajes)
        {
            var raiz = JsonDocument.Parse(m.PayloadJson).RootElement;
            var esperada = m.RelatedPublicId == s15 ? "2026-09-15" : m.RelatedPublicId == s20 ? "2026-09-20" : "2026-09-22";
            raiz.GetProperty("effectiveDate").GetString().Should().Be(esperada, "fechado en la salida afectada");
        }
    }

    [Fact]
    public async Task Cost_impact_de_un_borrador_no_retroactivo_no_trae_afectados()
    {
        var k = await CooperativaAsync();
        await TresSalidasAsync(k);
        var guardado = await k.GuardarAsync(k.Borrador("AJP", fecha: new DateOnly(2026, 9, 24), lineas: [k.Linea(k.P1, 1m, 1000m)]));
        var registro = k.Registro();

        var impacto = await new GetDocumentCostImpactQueryHandler(k.C.Db, k.Vista(), k.Efectos(registro), registro)
            .Handle(new GetDocumentCostImpactQuery(guardado.Value.PublicId), default);

        impacto.Value.Retroactive.Should().BeFalse();
        impacto.Value.Affected.Should().BeEmpty();
        impacto.Value.Total.Should().Be(0m);
    }

    // ------------------------------------------------------------------------------------------- apoyo --

    /// <summary>Un saldo inicial en una bodega nueva <c>NotActivated</c>, guardado como borrador.</summary>
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

        var documento = new InventoryDocument
        {
            Class = DocumentClass.OpeningBalance, DocumentTypeId = tipo.Id, OperationDate = fecha, WarehouseId = bodega.Id, BranchId = k.Sucursal.Id,
            CreatedByUserId = KardexDePrueba.Usuario,
        };
        documento.Lines.Add(new InventoryDocumentLine
        {
            Document = documento, LineNumber = 1, ProductId = k.ProductoId(producto), UnitId = k.C.Producto(producto).BaseUnitId,
            Quantity = cantidad, QuantityBase = cantidad, UnitCost = costo,
        });
        k.C.Db.InventoryDocuments.Add(documento);
        await k.C.Db.SaveChangesAsync();
        return (documento, bodega);
    }
}
