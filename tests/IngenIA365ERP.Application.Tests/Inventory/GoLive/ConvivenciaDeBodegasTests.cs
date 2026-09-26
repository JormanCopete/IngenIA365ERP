using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// Feature 012, T299 (FR-091, US4-4): la convivencia con SOLIDO. Una bodega <c>NotActivated</c> sólo admite su saldo inicial y su
/// anulación: cualquier otra clase de I1 con bodega responde 422 <c>Inventory.Warehouse.NotActive</c> con
/// <c>data.allowedClasses</c>; guardar el borrador no falla pero lo avisa; y en una bodega activa el saldo inicial responde
/// <c>Inventory.OpeningBalance.WarehouseActive</c>. Recorre todas las clases de I1 que llevan bodega (<see cref="ClasesDeDocumento"/>).
/// </summary>
public class ConvivenciaDeBodegasTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private static readonly BodegaDelDocumento NoActiva = new(10, Guid.NewGuid(), "B3", "Tercera", 1, false, Activa: false, Inactiva: false);
    private static readonly BodegaDelDocumento Activa = NoActiva with { Code = "PRIN", Activa = true };

    public static TheoryData<DocumentClass> ClasesDeI1ConBodega()
    {
        var datos = new TheoryData<DocumentClass>();
        foreach (var clase in ClasesDeDocumento.Todas.Where(c => c.AvailableFrom == EntregaDelComercio.I1 && c.Warehouses != AdmittedWarehouses.None))
            datos.Add(clase.Class);
        return datos;
    }

    private static IReadOnlyList<Error> Evaluar(DocumentClass clase, BodegaDelDocumento bodega)
    {
        var tipo = new InventoryDocumentType { Code = "T", Name = "T", Class = clase, IsActive = true, AllWarehouses = true };
        var documento = new InventoryDocument
        {
            Class = clase, OperationDate = Hoy, WarehouseId = bodega.Id, Reason = "motivo", CostCenterId = 1, CounterpartyPersonId = 1,
            ExternalReference = "R-1", DestinationWarehouseId = 11, VoidsDocumentId = clase == DocumentClass.Voiding ? 99 : null,
        };
        documento.Lines.Add(new InventoryDocumentLine { Document = documento, LineNumber = 1, ProductId = 1, UnitId = 1, Quantity = 1m, QuantityBase = 1m });
        return ReglasDelDocumento.Evaluar(documento, tipo, bodega, CorteDeInventario.SinCorte, Hoy);
    }

    [Theory]
    [MemberData(nameof(ClasesDeI1ConBodega))]
    public void En_una_bodega_no_activa_solo_entran_el_saldo_inicial_y_la_anulacion(DocumentClass clase)
    {
        var errores = Evaluar(clase, NoActiva);

        if (clase is DocumentClass.OpeningBalance or DocumentClass.Voiding)
        {
            errores.Should().NotContain(e => e.Code == "Inventory.Warehouse.NotActive");
            return;
        }
        var error = errores.Should().ContainSingle(e => e.Code == "Inventory.Warehouse.NotActive").Subject;
        var datos = JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);
        datos.GetProperty("allowedClasses").EnumerateArray().Select(x => x.GetString()).Should().Equal("OpeningBalance", "Voiding");
        datos.GetProperty("warehouseCode").GetString().Should().Be("B3");
    }

    [Fact]
    public void En_una_bodega_activa_el_saldo_inicial_responde_WarehouseActive()
    {
        Evaluar(DocumentClass.OpeningBalance, Activa).Should().ContainSingle(e => e.Code == GoLiveErrors.OpeningBalanceWarehouseActiveCode);
        Evaluar(DocumentClass.PositiveAdjustment, Activa).Should().NotContain(e => e.Code.StartsWith("Inventory.Warehouse", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Guardar_un_ajuste_en_la_bodega_no_activa_no_falla_pero_avisa_y_confirmarlo_responde_NotActive()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var k = p.K;

        var guardado = await p.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            k.Borrador("AJP", p.B3, lineas: [k.Linea(k.P1, 1m, 100m)])), default);

        guardado.IsSuccess.Should().BeTrue(guardado.IsFailure ? guardado.Error.Message : string.Empty);
        guardado.Value.Warnings.Should().ContainSingle(w => w.Code == "Inventory.Warehouse.NotActive");

        var confirmado = await p.Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(guardado.Value.PublicId, DocumentClassGroup.Adjustments), default);
        confirmado.IsFailure.Should().BeTrue();
        confirmado.Error.Code.Should().Be("Inventory.Warehouse.NotActive");
    }
}
