using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 013, T0637 (T54, C6, C79; contracts/contabilidad.md §4.5 y §5): el mapeo por defecto de los hechos de Cartera
/// (<c>LendingVoucherMappingsSeeder</c>, Order 89). Una fila por hecho de negocio, las tres de Nómina en <c>DN</c>, cruce <c>PG</c>, todo
/// sembrado y «pendiente de validar»; idempotente y sin pisar lo que la cooperativa cambió; con un <c>CN</c> de otro uso, el log lo dice y
/// los hechos de novedades quedan sin mapeo.
/// </summary>
public class SemillaDelMapeoDeCarteraTests
{
    [Fact]
    public void La_semilla_cubre_cada_hecho_de_negocio_con_los_tipos_de_la_tabla_de_5()
    {
        var semillas = LendingVoucherMappingsSeeder.Semillas();
        var tiposCar = VoucherTypesSeeder.Semillas().Where(t => t.Module == "CAR").Select(t => t.Code).ToHashSet();

        semillas.Where(s => s.CollectorClass == null).Select(s => s.Operation)
            .Should().BeEquivalentTo(OperacionesDeCartera.DeNegocio.Select(o => o.Codigo), "un hecho de negocio, una fila; los informativos no tienen");
        semillas.Should().OnlyContain(s => tiposCar.Contains(s.VoucherTypeCode));
        // Feature 014 (T046): los de captaciones cruzan con CTA (el número de la cuenta); los de crédito, con PG.
        var deCaptaciones = OperacionesDeCartera.DeCaptaciones.Select(o => o.Codigo).ToHashSet();
        semillas.Where(s => !deCaptaciones.Contains(s.Operation)).Should().OnlyContain(s => s.CrossDocumentTypeCode == "PG");
        semillas.Where(s => deCaptaciones.Contains(s.Operation)).Should().OnlyContain(s => s.CrossDocumentTypeCode == "CTA");
        CrossDocumentTypesSeeder.Semillas().Should().Contain(c => c.Code == "CTA" && c.Name == "Cuenta de captación o aportes");
        semillas.Where(s => s.CollectorClass != null).Select(s => (s.Operation, s.CollectorClass, s.VoucherTypeCode)).Should().BeEquivalentTo(new[]
        {
            ("PagoAplicado", (string?)"Payroll", "DN"), ("PagoReversado", "Payroll", "DN"), ("RecuperacionDeCastigada", "Payroll", "DN"),
            ("ConsignacionEnCuentaAplicada", "Payroll", "DN"), ("AporteRecibido", "Payroll", "DN"),
        }, "lo que recauda Nómina va en DN, la recuperación de castigadas incluida (C79), y en captaciones las consignaciones y los aportes");
        foreach (var s in semillas.Where(s => s.CollectorClass == null))
            s.VoucherTypeCode.Should().Be(OperacionesDeCartera.Buscar(s.Operation)!.Comprobante, s.Operation);
    }

    [Fact]
    public async Task La_semilla_es_idempotente_queda_pendiente_de_validar_y_no_pisa_lo_que_la_cooperativa_cambio()
    {
        using var db = TestDbContextFactory.Create();
        foreach (var t in VoucherTypesSeeder.Catalogo()) db.VoucherTypes.Add(t);
        foreach (var c in CrossDocumentTypesSeeder.Catalogo()) db.CrossDocumentTypes.Add(c);
        await db.SaveChangesAsync();
        var total = LendingVoucherMappingsSeeder.Semillas().Count;

        (await LendingVoucherMappingsSeeder.AplicarAsync(db, Nulo(), CancellationToken.None)).Should().Be(total);
        var todas = await db.LendingVoucherMappings.ToListAsync();
        todas.Should().OnlyContain(m => m.IsSeeded && m.ValidatedAt == null, "pendiente de validar por la contadora (C6)");
        var dn = await db.VoucherTypes.SingleAsync(v => v.Code == "DN");
        (await db.LendingVoucherMappings.SingleAsync(m => m.MappingKey == "PagoAplicado|Payroll")).VoucherTypeId.Should().Be(dn.Id);

        var rc = await db.VoucherTypes.SingleAsync(v => v.Code == "RC");
        var cobro = await db.LendingVoucherMappings.SingleAsync(m => m.MappingKey == "GastoDeCobranzaCausado|*");
        cobro.VoucherTypeId = rc.Id;
        await db.SaveChangesAsync();

        (await LendingVoucherMappingsSeeder.AplicarAsync(db, Nulo(), CancellationToken.None)).Should().Be(0);
        (await db.LendingVoucherMappings.CountAsync()).Should().Be(total);
        (await db.LendingVoucherMappings.SingleAsync(m => m.MappingKey == "GastoDeCobranzaCausado|*")).VoucherTypeId.Should().Be(rc.Id);
    }

    [Fact]
    public async Task Un_CN_que_la_cooperativa_ya_usa_para_otra_cosa_se_registra_y_sus_hechos_no_se_mapean()
    {
        using var db = TestDbContextFactory.Create();
        db.VoucherTypes.Add(new VoucherType { Code = "CN", Name = "Consignaciones", Usage = VoucherUsage.Manual, IsActive = true, NextNumber = 1 });
        await db.SaveChangesAsync();
        var log = new LogEnLista();
        await VoucherTypesSeeder.AplicarAsync(db, log, CancellationToken.None);
        log.Avisos.Should().ContainSingle(a => a.Contains("CN") && a.Contains("Manual"), "VoucherTypesSeeder registra la colisión con un CN de otro uso");
        foreach (var c in CrossDocumentTypesSeeder.Catalogo()) db.CrossDocumentTypes.Add(c);
        await db.SaveChangesAsync();

        var logMapeo = new LogEnLista();
        var mapeadas = await LendingVoucherMappingsSeeder.AplicarAsync(db, logMapeo, CancellationToken.None);

        var conCn = LendingVoucherMappingsSeeder.Semillas().Count(s => s.VoucherTypeCode == "CN");
        mapeadas.Should().Be(LendingVoucherMappingsSeeder.Semillas().Count - conCn);
        logMapeo.Avisos.Should().HaveCount(conCn);
        (await db.LendingVoucherMappings.AnyAsync(m => m.Operation == "DimensionContableCambiada")).Should().BeFalse();
    }

    private static ILogger Nulo() => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

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
