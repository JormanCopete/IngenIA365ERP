using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Costing;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, I5, US16, T824 (FR-043; data-model §3.1, §3.5; api.md §7; mensajes.md §6.10; T21, T42b, T42g) y la escritura PEPS del
/// registro (T836, T837):
/// <list type="bullet">
/// <item><c>Costeo.Metodo = Peps</c> a mitad de período o con movimientos posteriores → <c>Parameters.RequiresPeriodStart</c>; sin
/// <c>Inventory.Costing.Manage</c> → <c>Parameters.PermissionRequired</c>; sin justificación → error de validación; con
/// <c>Costeo.CambioExigeActa</c> y sin acta → <c>Parameters.LegalSourceRequired</c>; con fecha futura →
/// <c>Inventory.Costing.MethodChangeInFuture</c>;</item>
/// <item>registrado bien: un documento <c>CostAdjustment</c> del sistema, confirmado y numerado, con líneas <c>MethodChange</c>
/// (<c>QuantityBase = 0</c>) por producto y ámbito, una capa por producto con la existencia al promedio, <c>CostState.Method = Fifo</c> y
/// ningún mensaje; lo mismo para <c>Costeo.Ambito</c>, que pasa la existencia de cada bodega al ámbito nuevo;</item>
/// <item>con PEPS, el registro escribe capas y consumos (dos capas 10 × 1.000 y 10 × 1.300 y una salida de 15 = 16.500), la anulación
/// de la salida los devuelve, y la verificación de integridad no encuentra diferencias con capas.</item>
/// </list>
/// </summary>
public class CambioDeMetodoTests
{
    private static readonly DateOnly Agosto05 = new(2026, 8, 5);
    private static readonly DateOnly Agosto10 = new(2026, 8, 10);
    private static readonly DateOnly Agosto20 = new(2026, 8, 20);
    private static readonly DateOnly Septiembre01 = new(2026, 9, 1);

    private static async Task<KardexDePrueba> CooperativaAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Entrega = EntregaDelComercio.I5;
        var tipo = new InventoryDocumentType { Code = "AJC", Name = "Ajuste de costo", Class = DocumentClass.CostAdjustment, IsActive = true };
        tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = "AJC", NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        k.C.Db.InventoryDocumentTypes.Add(tipo);
        await k.C.Db.SaveChangesAsync();
        return k;
    }

    private static async Task<Guid> Confirmado(KardexDePrueba k, string tipo, DateOnly fecha, Guid producto, decimal cantidad, decimal? costo = null,
        Domain.Entities.Inventory.Warehousing.Warehouse? bodega = null)
    {
        var (documento, r) = await k.AjusteAsync(k.Borrador(tipo, bodega, causa: tipo == "AJN" ? k.Causa() : null, fecha: fecha, lineas: [k.Linea(producto, cantidad, costo)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return documento;
    }

    /// <summary>Agosto: 10 × 1.000 y 10 × 1.300 entran (promedio 1.150) y salen 5: quedan 15 por 17.250.</summary>
    private static async Task AgostoAsync(KardexDePrueba k)
    {
        await Confirmado(k, "AJP", Agosto05, k.P1, 10m, 1000m);
        await Confirmado(k, "AJP", Agosto10, k.P1, 10m, 1300m);
        await Confirmado(k, "AJN", Agosto20, k.P1, 5m);
        k.C.Db.ChangeTracker.Clear();
    }

    private static Task<Result<AddParameterVersionResponse>> AltaAsync(KardexDePrueba k, string clave, string valor, DateOnly desde, string motivo = "Da información más fiable: rotación por lotes",
        string? acta = null)
    {
        var lector = k.Lector();
        var reglas = new ReglasDePlataformaDeInventario(k.C.Db, k.Alcance, lector, k.Permisos, k.C.Reloj);
        var registro = k.Registro();
        var cambio = new CambioDeMetodoDeCosteo(k.C.Db, lector, registro, new Numerador(k.C.Db, k.Cerrojo), k.Cerrojo, k.Actor, k.C.Reloj);
        return new AddParameterVersionCommandHandler(k.C.Db, k.Permisos, reglas, reglas, [cambio], EntregaDelComercio.I5).Handle(
            new AddParameterVersionCommand(ParametrosDeInventario.Modulo, clave, ParameterScopeKind.None, null, null, valor, desde, motivo, acta), default);
    }

    // ------------------------------------------------------------------------------------------ reglas --

    [Fact]
    public async Task A_mitad_de_periodo_o_con_movimientos_posteriores_es_RequiresPeriodStart()
    {
        var k = await CooperativaAsync();
        await AgostoAsync(k);

        var aMitad = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", new DateOnly(2026, 9, 15));
        var conPosteriores = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", new DateOnly(2026, 8, 1));

        aMitad.Error.Code.Should().Be("Parameters.RequiresPeriodStart");
        conPosteriores.Error.Code.Should().Be("Parameters.RequiresPeriodStart");
        (await k.C.Db.KardexEntries.AnyAsync(e => e.Reason == KardexReason.MethodChange)).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_permiso_de_costeo_es_PermissionRequired_y_sin_justificacion_es_invalido()
    {
        var k = await CooperativaAsync();
        k.Permisos.HasPermissionAsync(ParametrosDeInventario.PermisoDeCosteo, Arg.Any<CancellationToken>()).Returns(false);

        var sinPermiso = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", Septiembre01);
        var sinMotivo = new AddParameterVersionCommandValidator().Validate(
            new AddParameterVersionCommand(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoMetodo, ParameterScopeKind.None, null, null, "Peps", Septiembre01, "", null));

        sinPermiso.Error.Code.Should().Be("Parameters.PermissionRequired");
        sinMotivo.IsValid.Should().BeFalse("la justificación (reason) es obligatoria");
    }

    [Fact]
    public async Task Con_acta_exigida_y_sin_acta_es_LegalSourceRequired_y_una_fecha_futura_no_se_programa()
    {
        var k = await CooperativaAsync();
        await AgostoAsync(k);
        k.Parametro(ParametrosDeInventario.CosteoCambioExigeActa, "true");

        var sinActa = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", Septiembre01);
        var futura = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", new DateOnly(2026, 10, 1), acta: "Acta 12 del consejo");

        sinActa.Error.Code.Should().Be("Parameters.LegalSourceRequired");
        futura.Error.Code.Should().Be("Inventory.Costing.MethodChangeInFuture");
    }

    // ---------------------------------------------------------------------------------------- cambio --

    [Fact]
    public async Task A_PEPS_genera_el_ajuste_de_costo_del_sistema_con_una_capa_al_promedio_y_sin_mensaje()
    {
        var k = await CooperativaAsync();
        await AgostoAsync(k);
        var mensajesAntes = await k.C.Db.IntegrationMessages.CountAsync();

        var r = await AltaAsync(k, ParametrosDeInventario.CosteoMetodo, "Peps", Septiembre01, acta: "Acta 12");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var documento = await k.C.Db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.Class == DocumentClass.CostAdjustment);
        documento.Status.Should().Be(DocumentStatus.Confirmed);
        documento.Number.Should().NotBeNull("se numera como todo documento confirmado");
        documento.OperationDate.Should().Be(Septiembre01);
        var lineas = await k.C.Db.KardexEntries.AsNoTracking().Where(e => e.DocumentId == documento.Id).ToListAsync();
        var linea = lineas.Should().ContainSingle("una línea MethodChange por producto y ámbito con existencia").Subject;
        linea.Reason.Should().Be(KardexReason.MethodChange);
        linea.Kind.Should().Be(KardexEntryKind.CostAdjustment);
        linea.QuantityBase.Should().Be(0m);
        linea.TotalCost.Should().Be(0m, "15 × 1.150 = 17.250: la capa vale lo mismo que el ámbito");
        var capa = await k.C.Db.CostLayers.AsNoTracking().SingleAsync();
        (capa.OriginalQuantity, capa.RemainingQuantity, capa.UnitCost, capa.EntryKardexEntryId).Should().Be((15m, 15m, 1150m, linea.Id));
        var estado = await k.C.Db.CostStates.AsNoTracking().SingleAsync();
        (estado.Method, estado.Quantity, estado.Value).Should().Be((CostMethod.Fifo, 15m, 17250m));
        (await k.C.Db.IntegrationMessages.CountAsync()).Should().Be(mensajesAntes, "el cambio de método no emite mensaje (§6.10)");
        (await k.C.Db.ParameterVersions.AsNoTracking().SingleAsync(v => v.Key == ParametrosDeInventario.CosteoMetodo)).LegalSource.Should().Be("Acta 12");

        // Desde el cambio, las salidas consumen la capa única y las entradas abren la suya.
        await Confirmado(k, "AJN", new DateOnly(2026, 9, 2), k.P1, 5m);
        await Confirmado(k, "AJP", new DateOnly(2026, 9, 3), k.P1, 10m, 1300m);
        await Confirmado(k, "AJN", new DateOnly(2026, 9, 4), k.P1, 15m);
        var ultima = await k.C.Db.KardexEntries.AsNoTracking().Where(e => e.Kind == KardexEntryKind.Exit).OrderByDescending(e => e.Id).FirstAsync();
        ultima.TotalCost.Should().Be(-(10m * 1150m + 5m * 1300m), "PEPS: primero lo que queda de la capa del cambio");
        (await Verificar(k)).Incidentes.Should().BeEmpty();
    }

    [Fact]
    public async Task Costeo_Ambito_a_bodega_pasa_la_existencia_de_cada_bodega_al_promedio_y_cuadra()
    {
        var k = await CooperativaAsync();
        await Confirmado(k, "AJP", Agosto05, k.P1, 10m, 1000m);
        await Confirmado(k, "AJP", Agosto10, k.P1, 10m, 1300m, k.Segunda);
        k.C.Db.ChangeTracker.Clear();

        var r = await AltaAsync(k, ParametrosDeInventario.CosteoAmbito, "Bodega", Septiembre01);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var lineas = await k.C.Db.KardexEntries.AsNoTracking().Where(e => e.Reason == KardexReason.MethodChange).ToListAsync();
        lineas.Should().HaveCount(4, "por bodega, sale del ámbito cooperativa y entra al de la bodega");
        var estados = await k.C.Db.CostStates.AsNoTracking().ToListAsync();
        estados.Single(e => e.ScopeWarehouseId == 0).Quantity.Should().Be(0m);
        estados.Single(e => e.ScopeWarehouseId == 0).Value.Should().Be(0m);
        estados.Single(e => e.ScopeWarehouseId == k.Principal.Id).Value.Should().Be(11500m, "10 × el promedio de la cooperativa, 1.150");
        estados.Single(e => e.ScopeWarehouseId == k.Segunda.Id).Value.Should().Be(11500m);
        (await k.C.Db.StockBalances.AsNoTracking().SingleAsync(s => s.WarehouseId == k.Principal.Id)).Physical.Should().Be(10m, "la existencia de la bodega no cambia");
        (await Verificar(k)).Incidentes.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------- PEPS en el registro --

    [Fact]
    public async Task Con_PEPS_el_registro_escribe_capas_y_consumos_y_la_anulacion_los_devuelve()
    {
        var k = await CooperativaAsync();
        k.Parametro(ParametrosDeInventario.CosteoMetodo, "Peps");
        await Confirmado(k, "AJP", new DateOnly(2026, 9, 5), k.P1, 10m, 1000m);
        await Confirmado(k, "AJP", new DateOnly(2026, 9, 10), k.P1, 10m, 1300m);
        var venta = await Confirmado(k, "AJN", new DateOnly(2026, 9, 15), k.P1, 15m);
        k.C.Db.ChangeTracker.Clear();

        var salida = await k.C.Db.KardexEntries.AsNoTracking().SingleAsync(e => e.Kind == KardexEntryKind.Exit);
        salida.TotalCost.Should().Be(-16500m, "US16-1: 10 × 1.000 + 5 × 1.300");
        var consumos = await k.C.Db.LayerConsumptions.AsNoTracking().Where(c => c.ExitKardexEntryId == salida.Id).OrderBy(c => c.Id).ToListAsync();
        consumos.Select(c => (c.Quantity, c.UnitCost)).Should().Equal((10m, 1000m), (5m, 1300m));
        var capas = await k.C.Db.CostLayers.AsNoTracking().OrderBy(c => c.OperationDate).ToListAsync();
        capas.Select(c => (c.OriginalQuantity, c.RemainingQuantity, c.UnitCost)).Should().Equal((10m, 0m, 1000m), (10m, 5m, 1300m));
        (await Verificar(k)).Incidentes.Should().BeEmpty("Σ valor de las capas = valor del ámbito y restante = original − consumos");

        var anulada = await k.Anular().Handle(new VoidInventoryDocumentCommand(venta, DocumentClassGroup.Adjustments, "Salida mal registrada"), default);
        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? $"{anulada.Error.Code}: {anulada.Error.Message}" : string.Empty);
        k.C.Db.ChangeTracker.Clear();

        (await k.C.Db.CostLayers.AsNoTracking().OrderBy(c => c.OperationDate).Select(c => c.RemainingQuantity).ToListAsync()).Should().Equal(10m, 10m);
        (await k.C.Db.LayerConsumptions.AsNoTracking().Where(c => c.Quantity < 0m).SumAsync(c => c.Quantity)).Should().Be(-15m, "la anulación devuelve bajo su propia línea");
        (await k.C.Db.CostStates.AsNoTracking().SingleAsync()).Value.Should().Be(23000m);
        (await Verificar(k)).Incidentes.Should().BeEmpty();
    }

    [Fact]
    public async Task La_verificacion_detecta_una_capa_que_no_cuadra_y_la_reconstruccion_la_corrige()
    {
        var k = await CooperativaAsync();
        k.Parametro(ParametrosDeInventario.CosteoMetodo, "Peps");
        await Confirmado(k, "AJP", new DateOnly(2026, 9, 5), k.P1, 10m, 1000m);
        await Confirmado(k, "AJN", new DateOnly(2026, 9, 6), k.P1, 4m);
        var capa = await k.C.Db.CostLayers.SingleAsync();
        capa.Reconstruir(9m); // un restante que los consumos no respaldan
        await k.C.Db.SaveChangesAsync();
        k.C.Db.ChangeTracker.Clear();

        var incidentes = (await Verificar(k)).Incidentes;
        incidentes.Should().Contain(i => i.Kind == TiposDeIncidente.CostLayer && i.Field == "RemainingQuantity" && i.Expected == 6m && i.Actual == 9m);
        incidentes.Should().Contain(i => i.Kind == TiposDeIncidente.CostLayer && i.Field == "Value");

        var r = await new RebuildInventoryProjectionsCommandHandler(k.C.Db, k.Alcance, k.Cerrojo, k.C.Reloj)
            .Handle(new RebuildInventoryProjectionsCommand(null, null, "Capa descuadrada"), default);
        r.IsSuccess.Should().BeTrue();
        r.Value.Rows.CostLayers.Should().Be(1);
        k.C.Db.ChangeTracker.Clear();
        (await Verificar(k)).Incidentes.Should().BeEmpty();
    }

    private static Task<ResultadoDeVerificacion> Verificar(KardexDePrueba k) =>
        new VerificacionDeIntegridad(k.C.Db, k.Lector(), k.C.Reloj).VerificarAsync(AlcanceDeVerificacion.Todo, default);
}
