using FluentAssertions;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Audit.ExportAuditLogCsv;
using IngenIA365ERP.Application.Common.Interfaces;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit.ExportAuditLogCsv;

/// <summary>
/// Tests del handler T088: tenant resolution + delegación al exporter.
/// La lógica CsvHelper en sí se cubre con un test de integración aparte
/// (requiere I/O real para validar el contenido del CSV).
/// </summary>
public class ExportAuditLogCsvQueryHandlerTests
{
    private static (ExportAuditLogCsvQueryHandler Handler, IAuditCsvExporter Exporter,
                    ICurrentTenantService Cu)
        Build(string? tenantId = "demo")
    {
        var exporter = Substitute.For<IAuditCsvExporter>();
        var cu = Substitute.For<ICurrentTenantService>();
        cu.TenantId.Returns(tenantId);
        return (new ExportAuditLogCsvQueryHandler(exporter, cu), exporter, cu);
    }

    [Fact]
    public async Task Resolves_tenant_from_current_user_and_delegates_to_exporter()
    {
        var (handler, exporter, _) = Build(tenantId: "tenant-demo");
        exporter.ExportAsync(Arg.Any<AuditExportFilters>(), Arg.Any<CancellationToken>())
            .Returns(new AuditCsvExport([1, 2, 3], "audit.csv", 0));

        var result = await handler.Handle(
            new ExportAuditLogCsvQuery(
                UserId: "u-1", EntityType: "User", Module: "Security",
                Action: "Created", From: null, To: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await exporter.Received(1).ExportAsync(
            Arg.Is<AuditExportFilters>(f =>
                f.TenantId == "tenant-demo" &&
                f.UserId == "u-1" &&
                f.EntityType == "User" &&
                f.Module == "Security" &&
                f.Action == "Created"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_with_TenantRequired_when_no_tenant_in_current_user()
    {
        var (handler, exporter, _) = Build(tenantId: null);

        var result = await handler.Handle(
            new ExportAuditLogCsvQuery(null, null, null, null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.TenantRequired");
        await exporter.DidNotReceive().ExportAsync(
            Arg.Any<AuditExportFilters>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Normalizes_blank_filters_to_null()
    {
        var (handler, exporter, _) = Build();
        exporter.ExportAsync(Arg.Any<AuditExportFilters>(), Arg.Any<CancellationToken>())
            .Returns(new AuditCsvExport([], "audit.csv", 0));

        await handler.Handle(
            new ExportAuditLogCsvQuery(
                UserId: "   ", EntityType: "", Module: null,
                Action: "  ", From: null, To: null),
            CancellationToken.None);

        await exporter.Received(1).ExportAsync(
            Arg.Is<AuditExportFilters>(f =>
                f.UserId == null && f.EntityType == null &&
                f.Module == null && f.Action == null),
            Arg.Any<CancellationToken>());
    }
}
