using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Auth.MfaReset;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Auth;

/// <summary>
/// Feature 003 (T035) — Tests del listado de solicitudes de reset MFA:
/// filtro por status, paginación y proyección de nombres del afectado
/// y del solicitante.
/// </summary>
public class ListMfaResetRequestsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    private static (ListMfaResetRequestsQueryHandler Handler, TestApplicationDbContext Db)
        Build(int? currentUserId = 1)
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserId.Returns(currentUserId);
        return (new ListMfaResetRequestsQueryHandler(db, cu), db);
    }

    private static void Seed(TestApplicationDbContext db)
    {
        var gina = new User { Id = 10, PublicId = Guid.NewGuid(), Username = "gina", Email = "gina@coop.co" };
        var ana = new User { Id = 20, PublicId = Guid.NewGuid(), Username = "ana", Email = "ana@coop.co" };
        db.Users.AddRange(gina, ana);

        db.MfaResetRequests.AddRange(
            new MfaResetRequest
            {
                UserId = 10, User = gina, RequestedBy = 20,
                Reason = "Perdió el teléfono", RequestedAt = Now.AddHours(-1),
                ExpiresAt = Now.AddHours(23), Status = MfaResetStatus.Pending,
            },
            new MfaResetRequest
            {
                UserId = 10, User = gina, RequestedBy = 20, FirstApproverId = 30,
                Reason = "Caso viejo", RequestedAt = Now.AddDays(-3),
                ExpiresAt = Now.AddDays(-2), Status = MfaResetStatus.Executed,
            });
        db.SaveChanges();
    }

    [Fact]
    public async Task Filtra_por_status_Pending_y_proyecta_nombres()
    {
        var (handler, db) = Build();
        Seed(db);

        var result = await handler.Handle(
            new ListMfaResetRequestsQuery(MfaResetStatus.Pending), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        var item = result.Value.Items.Single();
        item.TargetUserName.Should().Be("gina");
        item.TargetEmail.Should().Be("gina@coop.co");
        item.RequestedByUserName.Should().Be("ana");
        item.Status.Should().Be("Pending");
        item.HasFirstApproval.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_filtro_devuelve_todas_ordenadas_por_fecha_desc()
    {
        var (handler, db) = Build();
        Seed(db);

        var result = await handler.Handle(new ListMfaResetRequestsQuery(), default);

        result.Value.TotalCount.Should().Be(2);
        result.Value.Items[0].Status.Should().Be("Pending");   // la más reciente primero
        result.Value.Items[1].Status.Should().Be("Executed");
        result.Value.Items[1].HasFirstApproval.Should().BeTrue();
    }

    [Fact]
    public async Task Paginacion_respeta_page_y_pageSize()
    {
        var (handler, db) = Build();
        Seed(db);

        var result = await handler.Handle(
            new ListMfaResetRequestsQuery(Status: null, Page: 2, PageSize: 1), default);

        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Page.Should().Be(2);
        result.Value.Items[0].Status.Should().Be("Executed");
    }

    [Fact]
    public async Task Sin_autenticacion_devuelve_Unauthorized()
    {
        var (handler, _) = Build(currentUserId: null);

        var result = await handler.Handle(new ListMfaResetRequestsQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Generic.Unauthorized");
    }
}
