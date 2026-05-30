using FluentAssertions;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Audit.ExportAuditLogPdf;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit.ExportAuditLogPdf;

/// <summary>
/// Tests del handler T089: resolución de tenant + lookup de entidad
/// para portada + delegación al exporter. La renderización QuestPDF y la
/// firma HMAC se cubren con tests de integración aparte.
/// </summary>
public class ExportAuditLogPdfQueryHandlerTests
{
    private static (ExportAuditLogPdfQueryHandler Handler,
                    IAuditPdfExporter Exporter,
                    TestApplicationDbContext Db,
                    ICurrentUserService Cu)
        Build(string? tenantId = "1", DateTime? now = null)
    {
        var db = TestDbContextFactory.Create();
        var exporter = Substitute.For<IAuditPdfExporter>();
        var cu = Substitute.For<ICurrentUserService>();
        cu.TenantId.Returns(tenantId);
        cu.UserName.Returns("ana@demo");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now ?? new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));

        return (new ExportAuditLogPdfQueryHandler(exporter, db, cu, clock), exporter, db, cu);
    }

    [Fact]
    public async Task Loads_tenant_for_header_and_delegates_to_exporter()
    {
        var (handler, exporter, db, _) = Build();
        db.Tenants.Add(new Tenant
        {
            Id = 0, // EF asignará — el contexto in-memory respeta IDENTITY
            Name = "Cooperativa Demo",
            SchemaName = "dbo",
            Nit = "900000001-1",
            LegalName = "Cooperativa Demo S.A.S.",
            PlanType = "Test",
            IsActive = true
        });
        await db.SaveChangesAsync(default);
        var tenantId = db.Tenants.Single().Id.ToString();

        // Re-build con el TenantId real que asignó EF.
        (handler, exporter, db, _) = BuildWithDb(db, tenantId);

        exporter.ExportAsync(
            Arg.Any<AuditExportFilters>(), Arg.Any<AuditPdfHeader>(), Arg.Any<CancellationToken>())
            .Returns(new AuditPdfExport([1, 2, 3], "audit.pdf", 0, "hmac-base64", "dev-v1"));

        var result = await handler.Handle(
            new ExportAuditLogPdfQuery(null, null, null, null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HmacBase64.Should().Be("hmac-base64");
        result.Value.KeyVersion.Should().Be("dev-v1");

        await exporter.Received(1).ExportAsync(
            Arg.Is<AuditExportFilters>(f => f.TenantId == tenantId),
            Arg.Is<AuditPdfHeader>(h =>
                h.Nit == "900000001-1" &&
                h.LegalName == "Cooperativa Demo S.A.S." &&
                h.TenantName == "Cooperativa Demo" &&
                h.GeneratedByUserName == "ana@demo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_TenantRequired_when_no_tenant_in_context()
    {
        var (handler, exporter, _, _) = Build(tenantId: null);

        var result = await handler.Handle(
            new ExportAuditLogPdfQuery(null, null, null, null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.TenantRequired");
        await exporter.DidNotReceive().ExportAsync(
            Arg.Any<AuditExportFilters>(), Arg.Any<AuditPdfHeader>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_NotFound_when_tenant_entity_missing()
    {
        // Current user says TenantId="9999" pero la BD está vacía.
        var (handler, exporter, _, _) = Build(tenantId: "9999");

        var result = await handler.Handle(
            new ExportAuditLogPdfQuery(null, null, null, null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Generic.NotFound");
        await exporter.DidNotReceive().ExportAsync(
            Arg.Any<AuditExportFilters>(), Arg.Any<AuditPdfHeader>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Falls_back_to_dash_when_nit_or_legal_name_missing()
    {
        var (handler, exporter, db, _) = Build();
        db.Tenants.Add(new Tenant
        {
            Name = "Demo Sin Datos Legales",
            SchemaName = "dbo",
            PlanType = "Test",
            IsActive = true
            // Sin Nit ni LegalName
        });
        await db.SaveChangesAsync(default);
        var tenantId = db.Tenants.Single().Id.ToString();
        (handler, exporter, db, _) = BuildWithDb(db, tenantId);

        exporter.ExportAsync(
            Arg.Any<AuditExportFilters>(), Arg.Any<AuditPdfHeader>(), Arg.Any<CancellationToken>())
            .Returns(new AuditPdfExport([], "audit.pdf", 0, "h", "v"));

        await handler.Handle(
            new ExportAuditLogPdfQuery(null, null, null, null, null, null),
            CancellationToken.None);

        await exporter.Received(1).ExportAsync(
            Arg.Any<AuditExportFilters>(),
            Arg.Is<AuditPdfHeader>(h => h.Nit == "—" && h.LegalName == "Demo Sin Datos Legales"),
            Arg.Any<CancellationToken>());
    }

    private static (ExportAuditLogPdfQueryHandler, IAuditPdfExporter, TestApplicationDbContext,
                    ICurrentUserService)
        BuildWithDb(TestApplicationDbContext existingDb, string tenantId)
    {
        var exporter = Substitute.For<IAuditPdfExporter>();
        var cu = Substitute.For<ICurrentUserService>();
        cu.TenantId.Returns(tenantId);
        cu.UserName.Returns("ana@demo");
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        return (new ExportAuditLogPdfQueryHandler(exporter, existingDb, cu, clock),
            exporter, existingDb, cu);
    }
}
