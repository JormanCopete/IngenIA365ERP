using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// T181 (T44; contracts/api.md §1.6 y §27): los eventos explícitos de Inventario —la exportación de un informe y la
/// descarga de un catálogo con datos— van por la bandeja de auditoría encadenada (<c>COR_AuditOutbox</c>) al flujo de
/// la cooperativa, que se nombra por su <b>PublicId</b> (<see cref="ICurrentTenantService"/>), nunca por el Id interno
/// del usuario: el defecto que la 009 tuvo en <c>AccountingAuditEmitter</c> y <c>PayrollAuditEmitter</c>.
/// </summary>
public class InventoryAuditEmitterTests
{
    private static (IServiceProvider Servicios, TestApplicationDbContext Db, IAuditService Mongo) Escenario(string? cooperativa)
    {
        var db = TestDbContextFactory.Create();
        var mongo = Substitute.For<IAuditService>();
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(cooperativa);
        var servicios = new ServiceCollection()
            .AddSingleton(mongo)
            .AddSingleton<IApplicationDbContext>(db)
            .AddSingleton(tenant)
            // El usuario del token trae el Id INTERNO de la cooperativa («1»): no es el que nombra la base.
            .AddSingleton(NominaTestData.UsuarioDePrueba("bodega@coop", 7))
            .BuildServiceProvider();
        return (servicios, db, mongo);
    }

    [Fact]
    public async Task La_exportacion_de_un_informe_va_encadenada_al_flujo_de_la_cooperativa_con_vista_formato_y_filas()
    {
        var (servicios, db, mongo) = Escenario(CooperativaDePrueba.PublicIdN);
        var emisor = new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance);

        await emisor.EmitirExportacionAsync("kardex", new { from = "2026-09-01", warehouse = "B01" }, "xlsx", 42, CancellationToken.None);

        await mongo.DidNotReceive().LogAsync(Arg.Any<AuditLogCommand>(), Arg.Any<CancellationToken>());
        var fila = db.AuditOutbox.Should().ContainSingle().Subject;
        fila.Stream.Should().Be($"{CooperativaDePrueba.PublicIdN}:10y", "el flujo se nombra por el PublicId, no por el Id interno «1»");
        fila.Module.Should().Be(InventoryAuditEmitter.Modulo);
        var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
        evento.Action.Should().Be("Inventory.Report.Exported");
        evento.TenantId.Should().Be(CooperativaDePrueba.PublicIdN);
        evento.UserId.Should().Be("7");
        evento.NewValuesJson.Should().Contain("\"vista\":\"kardex\"").And.Contain("\"formato\":\"xlsx\"").And.Contain("\"filas\":42")
            .And.Contain("\"warehouse\":\"B01\"");
    }

    [Fact]
    public async Task La_descarga_de_un_catalogo_con_datos_deja_Inventory_Catalog_Exported_con_catalogo_y_filas()
    {
        var (servicios, db, _) = Escenario(CooperativaDePrueba.PublicIdN);

        await new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance)
            .EmitirExportacionDeCatalogoAsync("inventory.products", 1250, CancellationToken.None);

        var evento = AuditoriaEncadenada.LeerCarga(db.AuditOutbox.Single().PayloadJson!);
        evento.Action.Should().Be(AuditEventTypes.InventoryCatalogExported);
        evento.EntityType.Should().Be("inventory.products");
        evento.NewValuesJson.Should().Contain("\"catalog\":\"inventory.products\"").And.Contain("\"rows\":1250");
    }

    [Fact]
    public async Task Sin_cooperativa_activa_no_inventa_un_flujo_y_va_por_la_auditoria_de_siempre()
    {
        var (servicios, db, mongo) = Escenario(null);
        AuditLogCommand? registrado = null;
        mongo.LogAsync(Arg.Do<AuditLogCommand>(c => registrado = c), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance)
            .EmitirExportacionAsync("stock", new { }, "pdf", 3, CancellationToken.None);

        db.AuditOutbox.Should().BeEmpty("sin cooperativa no hay flujo «1:10y» ni ningún otro nombrado por el Id interno");
        registrado.Should().NotBeNull();
        registrado!.Action.Should().Be(AuditEventTypes.InventoryReportExported);
        registrado.Module.Should().Be("Inventory");
    }

    [Fact]
    public async Task Si_la_auditoria_falla_la_exportacion_no_se_cae()
    {
        var mongo = Substitute.For<IAuditService>();
        mongo.LogAsync(Arg.Any<AuditLogCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Mongo caído")));
        var servicios = new ServiceCollection().AddSingleton(mongo).BuildServiceProvider();

        var acto = () => new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance)
            .EmitirExportacionAsync("valuation", new { }, "docx", 1, CancellationToken.None);

        await acto.Should().NotThrowAsync();
    }

    [Fact]
    public void Los_nombres_de_los_eventos_son_los_del_contrato()
    {
        AuditEventTypes.InventoryReportExported.Should().Be("Inventory.Report.Exported");
        AuditEventTypes.InventoryCatalogExported.Should().Be("Inventory.Catalog.Exported");
        InventoryAuditEmitter.Modulo.Should().Be(ModuloDeAuditoria.Inventory);
    }
}
