using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Treasury.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Treasury.Security;

/// <summary>
/// Feature 015, T0053 (US12; R-30; FR-106; data-model §9.3): el alcance de Tesorería es por cuenta bancaria propia. Sin filas no hay cuentas (falla
/// cerrado), una fila retirada no cuenta, el maestro global, <c>Treasury.Scope.All</c> y el segundo plano ven todo, y sin usuario resuelto no hay nada. Lo que está fuera responde
/// <c>Generic.NotFound</c> en cada consulta de Tesorería (fases 7 en adelante, sobre <see cref="AlcanceDeTesoreria.IncluyeCuenta"/>).
/// </summary>
public class AlcanceDeTesoreriaTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();

    public AlcanceDeTesoreriaTests() => _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

    private void Usuario(int? id) => _actor.ObtenerAsync(Arg.Any<CancellationToken>())
        .Returns(new Actor(ActorKind.Person, id, Guid.NewGuid(), Guid.NewGuid(), "Tesorera", null, ExecutionChannel.Web, "pruebas", null, null));

    [Fact]
    public async Task Sin_filas_no_hay_cuentas()
    {
        Usuario(10);
        var alcance = await new AlcanceDeTesoreriaDelActor(_actor, _permisos, _db).ObtenerAsync();
        alcance.Total.Should().BeFalse();
        alcance.IncluyeCuenta(1).Should().BeFalse();
    }

    [Fact]
    public async Task Las_cuentas_son_las_de_sus_filas_vivas()
    {
        Usuario(10);
        _db.UserBankAccountScopes.AddRange(
            new UserBankAccountScope { UserId = 10, BankAccountId = 1 },
            new UserBankAccountScope { UserId = 10, BankAccountId = 2, IsDeleted = true },
            new UserBankAccountScope { UserId = 11, BankAccountId = 3 });
        await _db.SaveChangesAsync();

        var alcance = await new AlcanceDeTesoreriaDelActor(_actor, _permisos, _db).ObtenerAsync();

        alcance.IncluyeCuenta(1).Should().BeTrue();
        alcance.IncluyeCuenta(2).Should().BeFalse("la fila retirada ya no cuenta");
        alcance.IncluyeCuenta(3).Should().BeFalse("es de otro usuario");
    }

    [Fact]
    public async Task Con_Treasury_Scope_All_ve_todas_las_cuentas()
    {
        Usuario(10);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { AlcanceDeTesoreriaDelActor.TodoElAlcance });
        (await new AlcanceDeTesoreriaDelActor(_actor, _permisos, _db).ObtenerAsync()).Total.Should().BeTrue("decisión del dueño del 2026-10-23");
    }

    [Fact]
    public async Task El_maestro_ve_todo_y_sin_usuario_no_hay_nada()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        Usuario(10);
        (await new AlcanceDeTesoreriaDelActor(_actor, _permisos, _db).ObtenerAsync()).Total.Should().BeTrue();

        var otros = Substitute.For<ICurrentUserPermissions>();
        Usuario(null);
        (await new AlcanceDeTesoreriaDelActor(_actor, otros, _db).ObtenerAsync()).Should().Be(AlcanceDeTesoreria.Vacio);
    }

    [Fact]
    public async Task En_segundo_plano_el_alcance_es_total()
    {
        Usuario(10);
        using (ContextoAmbiental.Fijar(new TenantDirectoryEntry(5, Guid.NewGuid(), "coop_x", "Coop X", DatabaseName: "coop_x"),
                   Actor.ProcesoDeIntegracion("Tarea:prueba"), "Tarea:prueba"))
            (await new AlcanceDeTesoreriaDelActor(_actor, _permisos, _db).ObtenerAsync()).Total.Should().BeTrue();
    }
}
