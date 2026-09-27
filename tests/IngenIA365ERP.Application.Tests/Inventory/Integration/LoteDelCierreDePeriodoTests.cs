using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Tests.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, T522 (T12; contracts/contabilidad.md §5.4): un tipo por lotes con disparador <c>CierreDePeriodo</c> sella en su
/// entrega la clave del período (<c>Period:{aaaa-mm}</c>), y <c>CloseInventoryPeriodCommand</c> crea en su transacción el lote
/// <c>PeriodClose</c> con esas entregas —y sólo ésas—.
/// </summary>
public class LoteDelCierreDePeriodoTests
{
    private static readonly DateOnly Julio10 = new(2026, 7, 10);

    [Fact]
    public async Task El_cierre_crea_el_lote_PeriodClose_con_las_entregas_que_lo_esperan()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        var ajp = p.K.Tipo("AJP").Id;
        p.K.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", ParameterScopeKind.DocumentType, ajp);
        p.K.Parametro(ParametrosDeInventario.ContabilidadDisparadorDeLote, ClavesDeLote.CierreDePeriodo, ParameterScopeKind.DocumentType, ajp);
        p.K.Parametro(ParametrosDeInventario.ContabilidadGranularidad, "Resumido", ParameterScopeKind.DocumentType, ajp);
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);
        await p.AjusteAsync("AJN", Julio10, p.K.P1, 2m);

        var porLotes = await p.K.C.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(d => d.Mode == DeliveryMode.Batch);
        porLotes.BatchScopeKey.Should().Be("Period:2026-07");
        porLotes.Status.Should().Be(DeliveryStatus.InBatch);
        porLotes.BatchId.Should().BeNull();

        var r = await p.CerrarAsync(2026, 7);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var lote = await p.K.C.Db.IntegrationBatches.AsNoTracking().SingleAsync();
        lote.Trigger.Should().Be(BatchTrigger.PeriodClose);
        lote.PeriodYear.Should().Be((short)2026);
        lote.PeriodMonth.Should().Be((byte)7);
        lote.Granularity.Should().Be(PostingGranularity.Summarized);
        lote.MessageCount.Should().Be(1, "el ajuste negativo va en línea");
        r.Value.BatchPublicId.Should().Be(lote.PublicId);
        (await p.K.C.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(d => d.Id == porLotes.Id)).BatchId.Should().Be(lote.Id);
    }

    [Fact]
    public async Task Sin_entregas_que_esperen_el_cierre_no_hay_lote()
    {
        var p = await PeriodosDePrueba.CrearAsync();
        await p.AjusteAsync("AJP", Julio10, p.K.P1, 10m, 1000m);

        var r = await p.CerrarAsync(2026, 7);

        r.Value.BatchPublicId.Should().BeNull();
        (await p.K.C.Db.IntegrationBatches.CountAsync()).Should().Be(0);
    }
}
