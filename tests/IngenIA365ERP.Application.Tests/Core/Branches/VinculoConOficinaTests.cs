using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch;
using IngenIA365ERP.Application.Core.Branches.Commands.UpdateBranch;
using IngenIA365ERP.Application.Core.Branches.Queries;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.Branches;

/// <summary>
/// Feature 009 (R7, FR-035): el alcance de sucursal de un usuario se traduce a las sucursales
/// contables por <c>COR_Branches.TenantBranchPublicId</c>. Hasta el 2026-09-20 la columna existía
/// pero ningún comando la escribía, así que la e2e de informes con usuario restringido no podía
/// ni configurarse; ahora crear y editar una sucursal la llevan, la consulta la devuelve y una
/// oficina no se vincula a dos sucursales.
/// </summary>
public class VinculoConOficinaTests
{
    private static readonly DateTime Ahora = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private static (TestApplicationDbContext Db, IDateTimeService Clock, ICurrentUserService User) Escenario()
    {
        var db = TestDbContextFactory.Create();
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(Ahora);
        var user = Substitute.For<ICurrentUserService>();
        user.UserName.Returns("admin@demo");
        return (db, clock, user);
    }

    [Fact]
    public async Task Crear_guarda_el_vinculo_y_la_consulta_lo_devuelve()
    {
        var (db, clock, user) = Escenario();
        var oficina = Guid.NewGuid();

        var r = await new CreateBranchCommandHandler(db, clock, user)
            .Handle(new CreateBranchCommand { Name = "Norte", ShortName = "NTE", TenantBranchPublicId = oficina }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var dto = await new GetBranchByIdQueryHandler(db).Handle(new GetBranchByIdQuery(r.Value), CancellationToken.None);
        dto.Value.TenantBranchPublicId.Should().Be(oficina);
    }

    [Fact]
    public async Task Editar_pone_y_quita_el_vinculo()
    {
        var (db, clock, user) = Escenario();
        var alta = await new CreateBranchCommandHandler(db, clock, user).Handle(new CreateBranchCommand { Name = "Principal" }, CancellationToken.None);
        var oficina = Guid.NewGuid();
        var editar = new UpdateBranchCommandHandler(db, clock, user);

        (await editar.Handle(new UpdateBranchCommand { PublicId = alta.Value, Name = "Principal", TenantBranchPublicId = oficina }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await db.Branches.AsNoTracking().SingleAsync()).TenantBranchPublicId.Should().Be(oficina);

        (await editar.Handle(new UpdateBranchCommand { PublicId = alta.Value, Name = "Principal", TenantBranchPublicId = null }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await db.Branches.AsNoTracking().SingleAsync()).TenantBranchPublicId.Should().BeNull("nulo quita el vínculo");
    }

    [Fact]
    public async Task Una_oficina_no_se_vincula_a_dos_sucursales()
    {
        var (db, clock, user) = Escenario();
        var oficina = Guid.NewGuid();
        var crear = new CreateBranchCommandHandler(db, clock, user);
        (await crear.Handle(new CreateBranchCommand { Name = "Norte", TenantBranchPublicId = oficina }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var sur = await crear.Handle(new CreateBranchCommand { Name = "Sur" }, CancellationToken.None);

        var repetida = await crear.Handle(new CreateBranchCommand { Name = "Otra", TenantBranchPublicId = oficina }, CancellationToken.None);
        repetida.Error.Code.Should().Be("Branch.OfficeAlreadyLinked");
        repetida.Error.Message.Should().Contain("Norte");

        var editada = await new UpdateBranchCommandHandler(db, clock, user)
            .Handle(new UpdateBranchCommand { PublicId = sur.Value, Name = "Sur", TenantBranchPublicId = oficina }, CancellationToken.None);
        editada.Error.Code.Should().Be("Branch.OfficeAlreadyLinked");

        // Reasignarse la misma oficina a sí misma no es un duplicado.
        var norte = await db.Branches.AsNoTracking().SingleAsync(b => b.Name == "Norte");
        var misma = await new UpdateBranchCommandHandler(db, clock, user)
            .Handle(new UpdateBranchCommand { PublicId = norte.PublicId, Name = "Norte", TenantBranchPublicId = oficina }, CancellationToken.None);
        misma.IsSuccess.Should().BeTrue(misma.Error.Message);
    }
}
