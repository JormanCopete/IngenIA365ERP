using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 012, T484 y T485 (contracts/contabilidad.md §2.6 y §9; decisiones-transversales §2.14, T28): los cinco tipos de
/// comprobante nuevos de Inventario y el mapeo por defecto de cada operación de la matriz. La semilla del mapeo es
/// idempotente y no pisa lo que la cooperativa cambió; la de tipos de comprobante ya no se salta en silencio un código
/// que la cooperativa tiene con otro uso: lo deja en el log.
/// </summary>
public class SemillasDeIntegracionContableTests
{
    /// <summary>El catálogo fijo de §2.7 (<c>OperacionesDeInventario</c>): la semilla trae una fila por operación.</summary>
    private static readonly string[] Operaciones =
    [
        "Venta", "CostoDeVenta", "Compra", "FacturaProveedor", "DevolucionAProveedor", "DevolucionDeCliente", "NotaCredito",
        "NotaDebito", "AjustePositivo", "AjusteNegativo", "ConsumoInterno", "RetiroGravado", "Baja", "Ensamble",
        "DespachoTraslado", "RecepcionTraslado", "AjusteDeCosto", "Reclasificacion", "MovimientoDeCaja", "DiferenciaDeArqueo",
    ];

    [Fact]
    public void Los_tipos_de_comprobante_suman_los_cinco_de_inventario()
    {
        var tipos = VoucherTypesSeeder.Semillas();
        tipos.Should().HaveCount(23);
        tipos.Where(t => t.Module == "INV").Select(t => t.Code).Should().BeEquivalentTo(["FV", "EI", "SI", "NV", "CP", "TR", "AC", "CJ"]);
        tipos.Where(t => t.Module == "INV").Should().OnlyContain(t => t.Usage == VoucherUsage.Module);
        tipos.Single(t => t.Code == "NV").Name.Should().Be("Notas de venta");
        tipos.Single(t => t.Code == "CJ").Name.Should().Be("Caja POS");
    }

    [Fact]
    public void El_mapeo_por_defecto_cubre_cada_operacion_con_un_tipo_de_inventario_y_cruces_que_existen()
    {
        var mapeo = InventoryVoucherMappingsSeeder.Semillas();
        var tiposInv = VoucherTypesSeeder.Semillas().Where(t => t.Module == "INV").Select(t => t.Code).ToHashSet();
        var cruces = CrossDocumentTypesSeeder.Semillas().Select(c => c.Code).ToHashSet();

        mapeo.Select(m => m.Operation).Should().BeEquivalentTo(Operaciones);
        mapeo.Should().OnlyContain(m => m.InventoryDocumentTypeCode == null, "la semilla sólo deja filas por operación; las excepciones por tipo son de la cooperativa");
        mapeo.Should().OnlyContain(m => tiposInv.Contains(m.VoucherTypeCode));
        mapeo.Where(m => m.CrossDocumentTypeCode != null).Should().OnlyContain(m => cruces.Contains(m.CrossDocumentTypeCode!));

        string Tipo(string op) => mapeo.Single(m => m.Operation == op).VoucherTypeCode;
        string? Cruce(string op) => mapeo.Single(m => m.Operation == op).CrossDocumentTypeCode;
        Tipo("Venta").Should().Be("FV"); Cruce("Venta").Should().Be("FV");
        Tipo("Compra").Should().Be("EI"); Cruce("Compra").Should().BeNull();
        Tipo("DevolucionAProveedor").Should().Be("SI"); Cruce("DevolucionAProveedor").Should().Be("FC");
        Tipo("NotaCredito").Should().Be("NV"); Cruce("NotaCredito").Should().Be("NC");
        Tipo("NotaDebito").Should().Be("NV"); Cruce("NotaDebito").Should().Be("ND");
        Tipo("FacturaProveedor").Should().Be("CP"); Cruce("FacturaProveedor").Should().Be("FC");
        Tipo("DespachoTraslado").Should().Be("TR");
        Tipo("AjusteDeCosto").Should().Be("AC");
        Tipo("Reclasificacion").Should().Be("AC");
        Tipo("DiferenciaDeArqueo").Should().Be("CJ");
    }

    [Fact]
    public async Task La_semilla_del_mapeo_es_idempotente_y_no_pisa_lo_que_la_cooperativa_cambio()
    {
        using var db = TestDbContextFactory.Create();
        await SembrarTiposAsync(db);

        var primera = await InventoryVoucherMappingsSeeder.AplicarAsync(db, NullLogger(), CancellationToken.None);
        primera.Should().Be(Operaciones.Length);

        // La cooperativa lleva los ajustes de costo a Salidas.
        var si = await db.VoucherTypes.SingleAsync(v => v.Code == "SI");
        var ajuste = await db.InventoryVoucherMappings.SingleAsync(m => m.MappingKey == "AjusteDeCosto|*");
        ajuste.VoucherTypeId = si.Id;
        await db.SaveChangesAsync();

        var segunda = await InventoryVoucherMappingsSeeder.AplicarAsync(db, NullLogger(), CancellationToken.None);
        segunda.Should().Be(0);
        (await db.InventoryVoucherMappings.CountAsync()).Should().Be(Operaciones.Length);
        (await db.InventoryVoucherMappings.SingleAsync(m => m.MappingKey == "AjusteDeCosto|*")).VoucherTypeId.Should().Be(si.Id);

        var fv = await db.InventoryVoucherMappings.SingleAsync(m => m.MappingKey == "Venta|*");
        fv.VoucherTypeId.Should().Be((await db.VoucherTypes.SingleAsync(v => v.Code == "FV")).Id);
        fv.CrossDocumentTypeId.Should().Be((await db.CrossDocumentTypes.SingleAsync(c => c.Code == "FV")).Id);
    }

    [Fact]
    public async Task Un_codigo_que_la_cooperativa_ya_usa_para_otra_cosa_se_registra_y_no_se_mapea()
    {
        using var db = TestDbContextFactory.Create();
        // La cooperativa ya tenía un «TR» manual (transferencias de tesorería) antes de la feature.
        db.VoucherTypes.Add(new VoucherType { Code = "TR", Name = "Transferencias", Usage = VoucherUsage.Manual, IsActive = true, NextNumber = 1 });
        await db.SaveChangesAsync();
        var log = new LogEnLista();

        var insertadas = await VoucherTypesSeeder.AplicarAsync(db, log, CancellationToken.None);
        insertadas.Should().Be(VoucherTypesSeeder.Semillas().Count - 1);
        log.Avisos.Should().ContainSingle(a => a.Contains("TR") && a.Contains("Manual"));
        (await db.VoucherTypes.SingleAsync(v => v.Code == "TR")).Usage.Should().Be(VoucherUsage.Manual, "la semilla no cambia lo existente");

        // Una segunda pasada no repite el aviso de lo que sí sembró, sólo el del choque.
        var otroLog = new LogEnLista();
        (await VoucherTypesSeeder.AplicarAsync(db, otroLog, CancellationToken.None)).Should().Be(0);
        otroLog.Avisos.Should().ContainSingle();

        foreach (var c in CrossDocumentTypesSeeder.Catalogo()) db.CrossDocumentTypes.Add(c);
        await db.SaveChangesAsync();
        var logMapeo = new LogEnLista();
        var mapeadas = await InventoryVoucherMappingsSeeder.AplicarAsync(db, logMapeo, CancellationToken.None);
        mapeadas.Should().Be(Operaciones.Length - 2, "DespachoTraslado y RecepcionTraslado apuntan a TR, que no es de Inventario");
        logMapeo.Avisos.Should().NotBeEmpty();
        (await db.InventoryVoucherMappings.AnyAsync(m => m.Operation == "DespachoTraslado")).Should().BeFalse();
    }

    private static async Task SembrarTiposAsync(TestApplicationDbContext db)
    {
        foreach (var t in VoucherTypesSeeder.Catalogo()) db.VoucherTypes.Add(t);
        foreach (var c in CrossDocumentTypesSeeder.Catalogo()) db.CrossDocumentTypes.Add(c);
        await db.SaveChangesAsync();
    }

    private static ILogger NullLogger() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    private sealed class LogEnLista : ILogger
    {
        public List<string> Avisos { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Avisos.Add(formatter(state, exception));
        }
    }
}
