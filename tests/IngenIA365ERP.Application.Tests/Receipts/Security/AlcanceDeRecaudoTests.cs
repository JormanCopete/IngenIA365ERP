using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Security;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Security;

/// <summary>
/// Feature 015, T0052 (US12; R-30; FR-011, FR-175; data-model §1.11): el alcance de Recaudo por oficina, punto, caja y pagaduría. Sin filas y sin
/// <c>Receipts.Scope.All</c> falla cerrado; lo que queda fuera responde <c>Generic.NotFound</c> igual que lo inexistente; un punto trae sus cajas; el cajero
/// vigente opera su caja; las pagadurías son de sus filas. Las reglas de los canales sin caja (cuenta propia) y del cobro entre oficinas
/// (<c>Caja.CobroSoloDeSuOficina</c>) las prueban la caja y los recibos de banco en sus fases.
/// </summary>
public class AlcanceDeRecaudoTests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly ICurrentTenantService _cooperativa = Substitute.For<ICurrentTenantService>();

    public AlcanceDeRecaudoTests() => _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<string>());

    private AlcanceDeRecaudoDelActor Actor() =>
        new(_e.Actor, _permisos, _cooperativa, TestAdminDbContext.Create(), _e.Db, new OficinasDelUsuario(_e.Db, TestAdminDbContext.Create()), _e.Reloj);

    [Fact]
    public async Task Sin_filas_ni_alcance_total_no_ve_nada()
    {
        var alcance = await Actor().ObtenerAsync();

        alcance.Total.Should().BeFalse();
        alcance.IncluyePunto(1).Should().BeFalse();
        alcance.IncluyeCaja(1, 1).Should().BeFalse();
        alcance.IncluyeOficina(_e.OficinaA.Id).Should().BeFalse();
        alcance.IncluyePagaduria(_e.PersonaSupervisor.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Con_Receipts_Scope_All_o_el_maestro_ve_todo()
    {
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { AlcanceDeRecaudoDelActor.TodoElAlcance });
        (await Actor().ObtenerAsync()).Total.Should().BeTrue();

        var maestro = Substitute.For<ICurrentUserPermissions>();
        maestro.EsMaestroGlobal.Returns(true);
        var actor = new AlcanceDeRecaudoDelActor(_e.Actor, maestro, _cooperativa, TestAdminDbContext.Create(), _e.Db,
            new OficinasDelUsuario(_e.Db, TestAdminDbContext.Create()), _e.Reloj);
        (await actor.ObtenerAsync()).Total.Should().BeTrue();
    }

    [Fact]
    public async Task Un_punto_trae_sus_cajas_y_el_cajero_vigente_opera_la_suya()
    {
        var norte = _e.Punto("NORTE");
        var sur = _e.Punto("SUR");
        var cajaNorte = _e.Caja(norte, "C1");
        var cajaSur = _e.Caja(sur, "C2");
        var otraSur = _e.Caja(sur, "C3");
        _e.Db.UserCollectionPointScopes.Add(new UserCollectionPointScope { UserId = _e.Cajera.Id, CollectionPointId = norte.Id });
        _e.AsignarCajero(cajaSur, _e.Cajera);

        var alcance = await Actor().ObtenerAsync();

        alcance.IncluyePunto(norte.Id).Should().BeTrue();
        alcance.IncluyeCaja(cajaNorte.Id, norte.Id).Should().BeTrue("la caja es de un punto en alcance");
        alcance.IncluyeCaja(cajaSur.Id, sur.Id).Should().BeTrue("es su cajera vigente");
        alcance.IncluyeCaja(otraSur.Id, sur.Id).Should().BeFalse("el punto sur no está en su alcance");
        alcance.IncluyePunto(sur.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Las_pagadurias_son_las_de_sus_filas()
    {
        _e.Db.UserPaymasterScopes.Add(new UserPaymasterScope { UserId = _e.Cajera.Id, PaymasterPersonId = _e.PersonaSupervisor.Id });
        await _e.Db.SaveChangesAsync();

        var alcance = await Actor().ObtenerAsync();

        alcance.IncluyePagaduria(_e.PersonaSupervisor.Id).Should().BeTrue();
        alcance.IncluyePagaduria(_e.PersonaOtroCajero.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Lo_que_esta_fuera_del_alcance_responde_igual_que_lo_inexistente()
    {
        var a = _e.Punto("PA");
        var b = _e.Punto("PB", _e.OficinaB);
        _e.Alcance.SoloOficinas(_e.OficinaA.Id);

        var fuera = await new UpdateCollectionPointCommandHandler(_e.Db, _e.Alcance)
            .Handle(new UpdateCollectionPointCommand(b.PublicId, "Otro", null, false, null), CancellationToken.None);
        var inexistente = await new UpdateCollectionPointCommandHandler(_e.Db, _e.Alcance)
            .Handle(new UpdateCollectionPointCommand(Guid.NewGuid(), "Otro", null, false, null), CancellationToken.None);
        var dentro = await new UpdateCollectionPointCommandHandler(_e.Db, _e.Alcance)
            .Handle(new UpdateCollectionPointCommand(a.PublicId, "Nuevo nombre", null, false, null), CancellationToken.None);

        fuera.Error.Code.Should().Be(ErroresDeRecaudo.CodigoNoEncontrado);
        fuera.Error.Should().BeEquivalentTo(inexistente.Error, "fuera del alcance y lo inexistente son el mismo 404 (SC-018)");
        dentro.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_lista_de_puntos_solo_muestra_los_del_alcance()
    {
        var a = _e.Punto("PA");
        _e.Punto("PB", _e.OficinaB);
        _e.Alcance.SoloPuntos(a.Id);

        var lista = await new ListCollectionPointsQueryHandler(_e.Db, _e.Alcance).Handle(new ListCollectionPointsQuery(), CancellationToken.None);

        lista.Value.Select(p => p.Code).Should().Equal("PA");
    }

    [Fact]
    public void El_alcance_vacio_y_el_cerrado_fallan_cerrado()
    {
        AlcanceDeRecaudo.Vacio.IncluyeOficina(1).Should().BeFalse();
        new AlcanceDeRecaudoCerrado().ObtenerAsync().Result.Should().Be(AlcanceDeRecaudo.Vacio);
        AlcanceDeRecaudo.Todo.IncluyeCaja(7, 9).Should().BeTrue();
    }
}
