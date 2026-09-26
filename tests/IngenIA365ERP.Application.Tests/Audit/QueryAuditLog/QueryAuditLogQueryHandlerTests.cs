using FluentAssertions;
using IngenIA365ERP.Application.Audit.QueryAuditLog;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit.QueryAuditLog;

/// <summary>
/// T081 — Tests del handler T087.
/// Cubre filtros, paginación, mapeo y la regla de aislamiento por tenant
/// (el handler NUNCA confía en un TenantId del cliente; lo toma del JWT
/// via <see cref="ICurrentTenantService.TenantId"/>).
///
/// El validator se prueba aparte de las reglas funcionales del handler
/// para mantener facts pequeños y focalizados.
/// </summary>
public class QueryAuditLogQueryHandlerTests
{
    private static (QueryAuditLogQueryHandler Handler, IAuditService Audit, ICurrentTenantService Cu)
        Build(string? tenantId = "demo")
    {
        var audit = Substitute.For<IAuditService>();
        var cu = Substitute.For<ICurrentTenantService>();
        cu.TenantId.Returns(tenantId);
        return (new QueryAuditLogQueryHandler(audit, cu), audit, cu);
    }

    private static PagedList<AuditLogEntry> EmptyPage(int page, int pageSize) =>
        new(Array.Empty<AuditLogEntry>(), 0, page, pageSize);

    [Fact]
    public async Task Passes_filters_through_to_audit_service()
    {
        var (handler, audit, _) = Build();
        audit.QueryAsync(Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage(1, 50));

        var from = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc);
        var query = new QueryAuditLogQuery(
            UserId: "u-1", EntityType: "User", EntityId: "abc",
            Module: "Security", Action: "Created",
            From: from, To: to,
            Paging: new PageRequest(2, 25));

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await audit.Received(1).QueryAsync(
            Arg.Is<AuditQueryParameters>(p =>
                p.UserId == "u-1" &&
                p.EntityType == "User" &&
                p.EntityId == "abc" &&
                p.Module == "Security" &&
                p.Action == "Created" &&
                p.From == from && p.To == to &&
                p.PageNumber == 2 && p.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scopes_query_to_current_user_tenant_ignoring_other_sources()
    {
        var (handler, audit, _) = Build(tenantId: "tenant-demo");
        audit.QueryAsync(Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage(1, 50));

        await handler.Handle(
            new QueryAuditLogQuery(null, null, null, null, null, null, null, new PageRequest()),
            CancellationToken.None);

        await audit.Received(1).QueryAsync(
            Arg.Is<AuditQueryParameters>(p => p.TenantId == "tenant-demo"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_when_current_user_has_no_tenant()
    {
        var (handler, audit, _) = Build(tenantId: null);

        var result = await handler.Handle(
            new QueryAuditLogQuery(null, null, null, null, null, null, null, new PageRequest()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.TenantRequired");
        await audit.DidNotReceive().QueryAsync(
            Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clamps_page_size_to_safe_maximum()
    {
        var (handler, audit, _) = Build();
        audit.QueryAsync(Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(EmptyPage(1, PageRequest.MaxPageSize));

        await handler.Handle(
            new QueryAuditLogQuery(null, null, null, null, null, null, null,
                new PageRequest(1, PageSize: 5000)),
            CancellationToken.None);

        await audit.Received(1).QueryAsync(
            Arg.Is<AuditQueryParameters>(p => p.PageSize == PageRequest.MaxPageSize),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Maps_audit_log_entry_to_dto_preserving_json_payloads()
    {
        var (handler, audit, _) = Build();
        var entry = new AuditLogEntry(
            Id: "objectid-1",
            TenantId: "demo",
            UserId: "42",
            UserName: "ana@demo",
            Action: "Updated",
            EntityType: "User",
            EntityId: "user-abc",
            Module: "Security",
            OldValues: "{\"Name\":\"Ana\"}",
            NewValues: "{\"Name\":\"Ana M.\"}",
            ChangedFields: ["Name"],
            IpAddress: "10.0.0.1",
            Endpoint: "/api/users/abc",
            DurationMs: 42,
            Timestamp: new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        audit.QueryAsync(Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(new PagedList<AuditLogEntry>(new[] { entry }, 1, 1, 50));

        var result = await handler.Handle(
            new QueryAuditLogQuery(null, null, null, null, null, null, null, new PageRequest()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Items.Single();
        dto.Id.Should().Be("objectid-1");
        dto.UserName.Should().Be("ana@demo");
        dto.OldValuesJson.Should().Be("{\"Name\":\"Ana\"}");
        dto.NewValuesJson.Should().Be("{\"Name\":\"Ana M.\"}");
        dto.ChangedFields.Should().BeEquivalentTo(new[] { "Name" });
        dto.HttpMethod.Should().BeNull("la proyección legacy no lo materializa todavía");
    }

    [Fact]
    public void Validator_rejects_range_over_six_months()
    {
        var validator = new QueryAuditLogQueryValidator();
        var query = new QueryAuditLogQuery(
            null, null, null, null, null,
            From: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Paging: new PageRequest(1, 50));

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("6 meses"));
    }

    [Fact]
    public void Validator_accepts_range_exactly_at_six_months()
    {
        var validator = new QueryAuditLogQueryValidator();
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var query = new QueryAuditLogQuery(
            null, null, null, null, null,
            From: from,
            To: from.Add(QueryAuditLogQueryValidator.MaxInteractiveRange),
            Paging: new PageRequest(1, 50));

        validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_rejects_To_earlier_than_From()
    {
        var validator = new QueryAuditLogQueryValidator();
        var query = new QueryAuditLogQuery(
            null, null, null, null, null,
            From: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Paging: new PageRequest(1, 50));

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("posterior"));
    }

    [Fact]
    public void Validator_accepts_null_range()
    {
        var validator = new QueryAuditLogQueryValidator();
        var query = new QueryAuditLogQuery(
            null, null, null, null, null, null, null,
            new PageRequest(1, 50));

        validator.Validate(query).IsValid.Should().BeTrue();
    }

    // ----------------------------------------------------------------- feature 012, T423 (FR-007) --

    [Fact]
    public async Task Pasa_los_modulos_encadenados_y_el_resultado_al_servicio()
    {
        var (handler, audit, _) = Build();
        audit.QueryAsync(Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>()).Returns(EmptyPage(1, 50));

        await handler.Handle(new QueryAuditLogQuery(null, null, null, null, null, null, null, new PageRequest())
        {
            Modules = ["Inventory", " Approvals ", "", "Inventory"],
            Outcome = "Rejected",
        }, CancellationToken.None);

        await audit.Received(1).QueryAsync(
            Arg.Is<AuditQueryParameters>(p => p.Modules!.SequenceEqual(new[] { "Inventory", "Approvals" }) && p.Outcome == "Rejected"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Proyecta_canal_actor_motivo_rechazo_clave_y_posicion_en_la_cadena()
    {
        var (handler, audit, _) = Build();
        var rechazo = new AuditLogEntry("id-1", "demo", "42", "bodega.a", "Rejected", "DecideApprovalCommand", null, "Approvals",
            null, "{}", null, "10.0.0.9", "POST /api/inventory/approvals/x/decide", 12, new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, string>
            {
                ["Channel"] = "web", ["ActorKind"] = "Person", ["Origin"] = "POST /api/inventory/approvals/x/decide",
                ["Reason"] = "No cuadra", ["ErrorCode"] = "Approvals.SelfApprovalForbidden", ["OperationKey"] = "k-1",
            },
            ChainSeq: 17);
        var proceso = new AuditLogEntry("id-2", "demo", "system", "Proceso de integración", "Inventory.Reorder.Reviewed", "Warehouse", null,
            "Inventory", null, null, null, null, null, 0, new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc),
            new Dictionary<string, string> { ["Channel"] = "process", ["ActorKind"] = "Process" });
        audit.QueryAsync(Arg.Any<AuditQueryParameters>(), Arg.Any<CancellationToken>())
            .Returns(new PagedList<AuditLogEntry>(new[] { rechazo, proceso }, 2, 1, 50));

        var r = await handler.Handle(new QueryAuditLogQuery(null, null, null, null, null, null, null, new PageRequest()), CancellationToken.None);

        var (a, b) = (r.Value.Items[0], r.Value.Items[1]);
        (a.Channel, a.ActorKind, a.Reason, a.Result, a.ErrorCode, a.OperationKey, a.ChainSeq, a.IpAddress)
            .Should().Be(("web", "Person", "No cuadra", "Rejected", "Approvals.SelfApprovalForbidden", "k-1", 17L, "10.0.0.9"));
        (b.Channel, b.ActorKind, b.Result, b.ErrorCode, b.ChainSeq).Should().Be(("process", "Process", "Accepted", (string?)null, (long?)null));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Rejected", true)]
    [InlineData("Accepted", true)]
    [InlineData("Otro", false)]
    public void Validator_solo_admite_Rejected_o_Accepted(string? resultado, bool valido)
    {
        new QueryAuditLogQueryValidator()
            .Validate(new QueryAuditLogQuery(null, null, null, null, null, null, null, new PageRequest()) { Outcome = resultado })
            .IsValid.Should().Be(valido);
    }
}
