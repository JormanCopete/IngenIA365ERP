using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// 2026-11-18 (pedido del dueño): con <c>Contabilidad.CostoDeVenta = AlCierreDelPeriodo</c> la venta pasa a Contabilidad en línea sin su costo,
/// el costo queda en <c>INV_DeferredCosts</c>, la anulación deja su espejo, el cierre del mes lo envía en un solo
/// <c>CostoDelPeriodoReconocido</c>, la reapertura lo reversa y el cierre contable no pasa mientras quede costo sin enviar.
/// </summary>
public class CostoAlCierreEnLaConfirmacionTests
{
    private static async Task<VentasDePrueba> ConCostoAlCierreAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.Parametro(ParametrosDeInventario.ContabilidadCostoDeVenta, ParametrosDeInventario.CostoAlCierreDelPeriodo);
        return v;
    }

    [Fact]
    public async Task Por_defecto_el_costo_viaja_con_la_venta()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 3m)]);

        venta.CostAtPeriodClose.Should().BeFalse();
        v.MensajesDe(venta.PublicId).Should().Contain(CostoDeVentaReconocidoV1.Type);
        (await v.Db.DeferredCosts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_el_parametro_la_venta_pasa_sin_su_costo_y_el_costo_espera_el_cierre()
    {
        var v = await ConCostoAlCierreAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 3m)]);

        venta.CostAtPeriodClose.Should().BeTrue();
        var mensajes = v.MensajesDe(venta.PublicId);
        mensajes.Should().NotContain(CostoDeVentaReconocidoV1.Type);
        mensajes.Should().Contain(m => m.StartsWith("VentaFacturada"), "la venta sigue en línea");

        var diferido = await v.Db.DeferredCosts.SingleAsync();
        diferido.CostDocumentId.Should().Be(venta.Id);
        diferido.SourceDocumentId.Should().Be(venta.Id);
        diferido.ContentType.Should().Be(CostoDeVentaReconocidoV1.Type);
        diferido.PeriodCostPostingId.Should().BeNull();
        ((CostoDeVentaReconocidoV1)CostoAlCierre.Leer(diferido.ContentType, 1, diferido.ContentJson)!).Lines.Sum(l => l.Cost).Should().Be(venta.CostTotal);
    }

    [Fact]
    public async Task La_anulacion_deja_el_espejo_del_costo_y_no_lo_emite()
    {
        var v = await ConCostoAlCierreAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 2m)]);

        var anulada = await v.Anular().Handle(new VoidInventoryDocumentCommand(venta.PublicId, DocumentClassGroup.Sales, "Venta duplicada"), default);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Message : string.Empty);
        var filas = await v.Db.DeferredCosts.OrderBy(d => d.Id).ToListAsync();
        filas.Should().HaveCount(2);
        filas.Should().OnlyContain(f => f.CostDocumentId == venta.Id);
        var neto = filas.Select(f => (CostoDeVentaReconocidoV1)CostoAlCierre.Leer(f.ContentType, 1, f.ContentJson)!).Sum(c => c.Lines.Sum(l => l.Cost));
        neto.Should().Be(0m, "la anulación cancela el costo diferido de la venta");
        var anulacion = await v.Db.InventoryDocuments.SingleAsync(d => d.VoidsDocumentId == venta.Id);
        anulacion.CostAtPeriodClose.Should().BeTrue("la anulación copia la marca del anulado");
    }

    [Fact]
    public async Task El_cierre_envia_el_costo_del_mes_y_la_reapertura_lo_reversa()
    {
        var v = await ConCostoAlCierreAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 3m)]);
        var emisor = new EmisorDeMensajes(v.Db, v.K.Actor, v.Compras.C.Reloj);
        var periodo = new InventoryPeriod { Year = (short)venta.OperationDate.Year, Month = (byte)venta.OperationDate.Month };
        v.Db.InventoryPeriods.Add(periodo);
        var fin = new DateOnly(periodo.Year, periodo.Month, 1).AddMonths(1).AddDays(-1);

        var cierre = await new CostoAlCierre(v.Db).CerrarAsync(emisor, periodo, 1, fin, Guid.NewGuid(), default);
        await v.Db.SaveChangesAsync();

        cierre.Should().NotBeNull();
        (await v.Db.DeferredCosts.SingleAsync()).PeriodCostPostingId.Should().Be(cierre!.Id);
        var mensaje = await v.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == cierre.PublicId);
        mensaje.Type.Should().Be(CostoDelPeriodoReconocidoV1.Type);
        var contenido = JsonSerializer.Deserialize<CostoDelPeriodoReconocidoV1>(mensaje.PayloadJson, OpcionesDeMensajes.Opciones)!;
        contenido.IsReversal.Should().BeFalse();
        Costo(contenido).Should().Be(venta.CostTotal);

        (await new CostoPendienteParaElCierre(v.Db).PendientesAsync(periodo.Year, periodo.Month, default)).Should().BeEmpty("ya se envió");

        var reversion = await new CostoAlCierre(v.Db).ReabrirAsync(emisor, periodo, Guid.NewGuid(), default);
        await v.Db.SaveChangesAsync();

        reversion!.IsReversal.Should().BeTrue();
        reversion.ReversesId.Should().Be(cierre.Id);
        var reverso = await v.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == reversion.PublicId);
        Costo(JsonSerializer.Deserialize<CostoDelPeriodoReconocidoV1>(reverso.PayloadJson, OpcionesDeMensajes.Opciones)!).Should().Be(-venta.CostTotal);
        (await v.Db.DeferredCosts.SingleAsync()).PeriodCostPostingId.Should().BeNull("vuelve a esperar el próximo cierre");

        var pendientes = await new CostoPendienteParaElCierre(v.Db).PendientesAsync(periodo.Year, periodo.Month, default);
        pendientes.Should().ContainSingle().Which.Blocks.Should().BeTrue();
        (await new CostoPendienteParaElCierre(v.Db).PendientesAsync(periodo.Year, periodo.Month == 1 ? 12 : periodo.Month - 1, default))
            .Should().BeEmpty("el mes anterior no tiene costo pendiente");
    }

    [Fact]
    public async Task Sin_costos_diferidos_el_cierre_no_envia_nada()
    {
        var v = await VentasDePrueba.CrearAsync();
        await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 1m)]);
        var periodo = new InventoryPeriod { Year = 2026, Month = 9 };
        v.Db.InventoryPeriods.Add(periodo);

        var cierre = await new CostoAlCierre(v.Db).CerrarAsync(new EmisorDeMensajes(v.Db, v.K.Actor, v.Compras.C.Reloj), periodo, 1, new DateOnly(2026, 9, 30), Guid.NewGuid(), default);

        cierre.Should().BeNull();
    }

    private static decimal Costo(CostoDelPeriodoReconocidoV1 contenido) => contenido.Parts
        .Where(p => p.Type == CostoDeVentaReconocidoV1.Type)
        .Select(p => (CostoDeVentaReconocidoV1)CostoAlCierre.Leer(p.Type, p.Version, ((JsonElement)p.Content!).GetRawText())!)
        .Sum(c => c.Lines.Sum(l => l.Cost));
}
