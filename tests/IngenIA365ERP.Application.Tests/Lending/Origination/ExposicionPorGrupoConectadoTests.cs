using FluentAssertions;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0371 (FR-041, SC-022; DT §7.10 C112): la exposición por grupo conectado, con un patrimonio técnico vigente de 1.000.000.000 y el
/// límite individual en 0,10 (100.000.000). El grupo de una persona es la unión transitiva, sin repetir, de los grupos registrados vigentes, los
/// vinculados por la misma persona y los codeudores comunes; la concentración se mide por deudor y por grupo con el mismo límite, y con garantía
/// admisible rige el suyo para los dos. Los comandos de grupos exigen fundamento y permiso; la consulta es lectura auditada. También T0395: la vista
/// previa del enrutamiento suma la exposición de la persona.
/// </summary>
public sealed class ExposicionPorGrupoConectadoTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private DateOnly Hoy => OriginacionDePrueba.Hoy;

    private ConnectedGroup Grupo(string nombre, ConnectedGroupBasis fundamento, params Person[] miembros)
    {
        var g = ConnectedGroup.Registrar(nombre, fundamento, "Comparten la misma empresa familiar", null, new DateOnly(2026, 1, 1), _o.Asesora.Id);
        _o.Db.ConnectedGroups.Add(g);
        _o.Db.SaveChanges();
        foreach (var m in miembros) _o.Db.ConnectedGroupMembers.Add(ConnectedGroupMember.Nuevo(g.Id, m.Id, new DateOnly(2026, 1, 1)));
        _o.Db.SaveChanges();
        return g;
    }

    [Fact]
    public async Task Caso_grupo_la_persona_pasa_y_el_grupo_excede()
    {
        _o.Credito(_o.Ana, 40_000_000m, "CR-A");
        _o.Credito(_o.Beto, 45_000_000m, "CR-B");
        var grupo = Grupo("Familia Pérez", ConnectedGroupBasis.EconomicLink, _o.Ana, _o.Beto);

        var r = await _o.Concentracion().MedirAsync(_o.Ana.Id, 20_000_000m, false, Hoy);

        r.IsSuccess.Should().BeTrue();
        r.Value.PorDeudor.Actual.Should().Be(40_000_000m);
        r.Value.PorDeudor.Excede.Should().BeFalse("60.000.000 caben en 100.000.000");
        var g = r.Value.PorGrupo!;
        g.Actual.Should().Be(85_000_000m);
        g.Excede.Should().BeTrue("105.000.000 pasan el límite");
        g.Exceso.Should().Be(5_000_000m);

        var validado = await _o.Concentracion().ValidarAsync(_o.Ana.Id, 20_000_000m, false, Hoy);
        validado.Error.Code.Should().Be("Lending.Concentration.LimitExceeded");
        var datos = OriginacionDePrueba.Datos(validado.Error);
        datos.GetProperty("scope").GetString().Should().Be("Group");
        datos.GetProperty("groupPublicId").GetGuid().Should().Be(grupo.PublicId);
        datos.GetProperty("limit").GetDecimal().Should().Be(100_000_000m);
        datos.GetProperty("current").GetDecimal().Should().Be(85_000_000m);
        datos.GetProperty("requested").GetDecimal().Should().Be(20_000_000m);
        datos.GetProperty("excess").GetDecimal().Should().Be(5_000_000m);
        datos.GetProperty("base").GetDecimal().Should().Be(1_000_000_000m);
        datos.GetProperty("members").EnumerateArray().Select(m => (m.GetProperty("personPublicId").GetGuid(), m.GetProperty("exposure").GetDecimal()))
            .Should().BeEquivalentTo([(_o.Ana.PublicId, 40_000_000m), (_o.Beto.PublicId, 45_000_000m)]);
        validado.Error.Message.Should().NotContain("Lending.");

        (await _o.Concentracion().ValidarAsync(_o.Ana.Id, 10_000_000m, false, Hoy)).IsSuccess.Should().BeTrue("95.000.000 caben");
    }

    [Fact]
    public async Task Caso_codeudor_comun_suma_los_creditos_de_los_dos_deudores()
    {
        _o.Credito(_o.Ana, 30_000_000m, "CR-A", _o.Carla);
        _o.Credito(_o.Dario, 25_000_000m, "CR-D", _o.Carla);

        var grupo = await _o.Grupos().DeAsync(_o.Dario.Id, Hoy);
        grupo.Miembros.Should().BeEquivalentTo([_o.Dario.Id, _o.Ana.Id], "comparten a Carla de codeudora, sin grupo registrado");
        grupo.Caminos.Should().ContainSingle(c => c.Fundamento == ConnectedGroupBasis.CommonCoDebtor && c.Hacia == _o.Ana.Id);
        grupo.GrupoPublicId.Should().BeNull();

        var r = await _o.Concentracion().MedirAsync(_o.Dario.Id, 50_000_000m, false, Hoy);
        r.Value.PorGrupo!.Actual.Should().Be(55_000_000m, "lo de Ana y lo de Darío; la codeuda de Carla no se cuenta dos veces");
        r.Value.PorGrupo.Excede.Should().BeTrue();
        r.Value.PorDeudor.Excede.Should().BeFalse();
    }

    [Fact]
    public async Task Con_garantia_admisible_rige_su_limite_tambien_para_el_grupo()
    {
        _o.Credito(_o.Ana, 40_000_000m, "CR-A");
        _o.Credito(_o.Beto, 45_000_000m, "CR-B");
        Grupo("Familia Pérez", ConnectedGroupBasis.EconomicLink, _o.Ana, _o.Beto);

        var r = await _o.Concentracion().ValidarAsync(_o.Ana.Id, 20_000_000m, conGarantiaAdmisible: true, Hoy);

        r.IsSuccess.Should().BeTrue("con garantía el límite es 0,20: 200.000.000");
        r.Value.PorGrupo!.Limite.Should().Be(200_000_000m);
        r.Value.PorGrupo.ConGarantiaAdmisible.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_patrimonio_tecnico_el_grupo_responde_igual_que_la_persona()
    {
        Grupo("Familia Pérez", ConnectedGroupBasis.EconomicLink, _o.Ana, _o.Beto);
        _o.PatrimonioTecnico = null;

        var r = await _o.Concentracion().ValidarAsync(_o.Ana.Id, 1_000_000m, false, Hoy);

        r.Error.Code.Should().Be("Lending.Concentration.TechnicalCapitalUnavailable");
        _o.L.Levantadas.Should().Contain(a => a.TypeCode == "Cartera.PatrimonioTecnicoNoDisponible");
    }

    [Fact]
    public async Task El_grupo_es_transitivo_sin_repetir_y_un_miembro_retirado_no_cuenta()
    {
        // Ana y Beto en un grupo; Beto es pariente del consejero Darío; Carla salió hoy del grupo de Ana.
        var g = Grupo("Familia Pérez", ConnectedGroupBasis.EconomicLink, _o.Ana, _o.Beto, _o.Carla);
        _o.Db.RelatedParties.Add(new RelatedParty
        {
            PersonId = _o.Beto.Id, Basis = RelatedPartyBasis.Relative, RelatedThroughPersonId = _o.Dario.Id, KinshipKind = KinshipKind.Consanguinity,
            KinshipDegree = 1, ValidFrom = new DateOnly(2026, 1, 1),
        });
        // Ana y Beto también comparten codeudor: llegan por dos caminos y se cuentan una vez.
        var codeudor = _o.Persona("Elena", "Codeudora", "30000005", null, null);
        _o.Credito(_o.Ana, 1_000_000m, "CR-A", codeudor);
        _o.Credito(_o.Beto, 1_000_000m, "CR-B", codeudor);
        var carla = await _o.Db.ConnectedGroupMembers.SingleAsync(m => m.GroupId == g.Id && m.PersonId == _o.Carla.Id);
        carla.Retirar(_o.G.UtcNow);
        await _o.Db.SaveChangesAsync();

        var grupo = await _o.Grupos().DeAsync(_o.Ana.Id, Hoy);

        grupo.Miembros.Should().BeEquivalentTo([_o.Ana.Id, _o.Beto.Id, _o.Dario.Id]);
        grupo.Miembros.Should().OnlyHaveUniqueItems();
        grupo.GruposRegistrados.Should().Equal(g.PublicId);
        grupo.Caminos.Should().Contain(c => c.Fundamento == ConnectedGroupBasis.RelatedParty && c.Hacia == _o.Dario.Id);

        // Ayer Carla todavía estaba.
        (await _o.Grupos().DeAsync(_o.Ana.Id, Hoy.AddDays(-1))).Miembros.Should().Contain(_o.Carla.Id);
    }

    [Fact]
    public async Task Registrar_exige_fundamento_y_permiso_y_los_miembros_se_agregan_y_retiran_con_motivo()
    {
        var validador = new RegisterConnectedGroupCommandValidator();
        validador.Validate(new RegisterConnectedGroupCommand("Grupo", ConnectedGroupBasis.LegalLink, " ", [_o.Ana.PublicId, _o.Beto.PublicId]))
            .IsValid.Should().BeFalse("sin fundamento no hay grupo");
        validador.Validate(new RegisterConnectedGroupCommand("Grupo", ConnectedGroupBasis.LegalLink, "Mismo representante", [_o.Ana.PublicId]))
            .IsValid.Should().BeFalse("un grupo tiene al menos dos personas");

        var comando = new RegisterConnectedGroupCommand("Grupo Andes", ConnectedGroupBasis.LegalLink, "Mismo representante legal",
            [_o.Ana.PublicId, _o.Beto.PublicId]) { OperationKey = Guid.NewGuid() };
        _o.G.Conceder();
        var sinPermiso = await new RegisterConnectedGroupCommandHandler(_o.Db, _o.G.Permisos, _o.G.Actor, _o.G.Reloj).Handle(comando, default);
        sinPermiso.Error.Code.Should().Be("Generic.NotFound");

        _o.G.Conceder(ErroresDeGruposConectados.PermisoDeGestion);
        var r = await new RegisterConnectedGroupCommandHandler(_o.Db, _o.G.Permisos, _o.G.Actor, _o.G.Reloj).Handle(comando, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Members.Should().HaveCount(2);
        r.Value.ValidFrom.Should().Be(Hoy);

        var agregado = await new AddConnectedGroupMemberCommandHandler(_o.Db, _o.G.Permisos, _o.G.Reloj)
            .Handle(new AddConnectedGroupMemberCommand(r.Value.PublicId, _o.Dario.PublicId, "Socio de la empresa") { OperationKey = Guid.NewGuid() }, default);
        agregado.Value.Members.Should().HaveCount(3);
        var repetido = await new AddConnectedGroupMemberCommandHandler(_o.Db, _o.G.Permisos, _o.G.Reloj)
            .Handle(new AddConnectedGroupMemberCommand(r.Value.PublicId, _o.Dario.PublicId, "Otra vez") { OperationKey = Guid.NewGuid() }, default);
        repetido.Error.Code.Should().Be("Lending.ConnectedGroup.MemberDuplicate");

        var retirado = await new EndConnectedGroupMemberCommandHandler(_o.Db, _o.G.Permisos, _o.G.Reloj)
            .Handle(new EndConnectedGroupMemberCommand(r.Value.PublicId, _o.Dario.PublicId, "Vendió su parte") { OperationKey = Guid.NewGuid() }, default);
        retirado.Value.Members.Single(m => m.Person.PersonPublicId == _o.Dario.PublicId).EndedAt.Should().NotBeNull();
        new EndConnectedGroupMemberCommandValidator().Validate(new EndConnectedGroupMemberCommand(r.Value.PublicId, _o.Dario.PublicId, " "))
            .IsValid.Should().BeFalse("el retiro lleva motivo");

        var terminado = await new EndConnectedGroupCommandHandler(_o.Db, _o.G.Permisos, _o.G.Reloj)
            .Handle(new EndConnectedGroupCommand(r.Value.PublicId, "Se disolvió la sociedad") { OperationKey = Guid.NewGuid() }, default);
        terminado.Value.EndedAt.Should().NotBeNull();
        terminado.Value.EndReason.Should().Be("Se disolvió la sociedad");
    }

    [Fact]
    public async Task La_consulta_de_grupos_es_lectura_sensible_auditada()
    {
        var g = Grupo("Familia Pérez", ConnectedGroupBasis.EconomicLink, _o.Ana, _o.Beto);

        var lista = await new ListConnectedGroupsQueryHandler(_o.Db, _o.G.Auditoria).Handle(new ListConnectedGroupsQuery(_o.Ana.PublicId), default);
        var detalle = await new GetConnectedGroupQueryHandler(_o.Db, _o.G.Auditoria).Handle(new GetConnectedGroupQuery(g.PublicId), default);

        lista.Value.Should().ContainSingle(x => x.PublicId == g.PublicId);
        detalle.Value.Members.Should().HaveCount(2);
        (await _o.G.EventosAsync()).Count(e => e == "Lending.RelatedParty.Viewed").Should().Be(2);
        (await new GetConnectedGroupQueryHandler(_o.Db, _o.G.Auditoria).Handle(new GetConnectedGroupQuery(Guid.NewGuid()), default))
            .Error.Code.Should().Be("Lending.ConnectedGroup.NotFound");
    }

    [Fact]
    public async Task La_vista_previa_del_enrutamiento_suma_la_exposicion_de_la_persona()
    {
        _o.Credito(_o.Ana, 8_000_000m, "CR-A");
        var vista = new PreviewAttributionRoutingQueryHandler(_o.Db, new MatrizVigenteDeAtribuciones(_o.Db), new VinculadosVigentes(_o.Db), _o.G.Alcance,
            _o.G.Auditoria, _o.G.Reloj, _o.Exposicion());

        var sinPersona = await vista.Handle(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, 15_000_000m), default);
        var conPersona = await vista.Handle(new PreviewAttributionRoutingQuery(LendingDecisionSubjects.CreditApplication, 15_000_000m,
            PersonPublicId: _o.Ana.PublicId), default);

        sinPersona.Value.EffectiveLevel.Code.Should().Be("GERENCIA");
        conPersona.Value.ConsolidatedAmount.Should().Be(23_000_000m);
        conPersona.Value.EffectiveLevel.Code.Should().Be("COMITE");
    }
}
