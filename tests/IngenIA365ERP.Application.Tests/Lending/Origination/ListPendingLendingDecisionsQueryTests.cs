using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0373 (api.md §4.3, T65): «Mis pendientes». Lo que el usuario puede decidir ahora —el permiso del nivel o uno mayor, el alcance de
/// oficina y línea— con <c>canDecide</c> y, si la segregación lo excluye, <c>excludedReason</c>; más lo que está en la agenda de sus órganos (va a
/// sesión). Un asesor de otra oficina no ve lo de la oficina ajena. Las solicitudes y los cupos que esperan decisión.
/// </summary>
public sealed class ListPendingLendingDecisionsQueryTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;
    private Branch _sur = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync();
        _sur = new Branch { Name = "Oficina Sur", CreatedBy = "test" };
        _o.Db.Branches.Add(_sur);
        await _o.Db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<IReadOnlyList<PendingLendingDecisionDto>> PendientesAsync(string? sujeto = null)
    {
        var r = await new ListPendingLendingDecisionsQueryHandler(_o.Db, _o.Atribucion(), _o.G.Intervenciones(), _o.G.Alcance, _o.G.Actor, _o.G.Reloj)
            .Handle(new ListPendingLendingDecisionsQuery(sujeto), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value.Items;
    }

    [Fact]
    public async Task El_analista_ve_lo_de_su_nivel_y_la_gerencia_tambien_lo_de_la_gerencia()
    {
        var analista = await _o.RadicadaAsync();
        _o.Credito(_o.Beto, 12_000_000m, "CR-B");
        var gerencia = await _o.RadicadaAsync(_o.Solicitud(3_000_000m, 24, _o.Beto));

        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
        var delAnalista = await PendientesAsync();
        delAnalista.Should().ContainSingle().Which.Should().Match<PendingLendingDecisionDto>(p =>
            p.SubjectPublicId == analista.PublicId && p.CanDecide && p.ExcludedReason == null && p.Route == RutasDePendiente.Individual
            && p.LevelCode == "ANALISTA" && p.Number == analista.Number);

        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Management");
        (await PendientesAsync()).Select(p => p.SubjectPublicId).Should().BeEquivalentTo([analista.PublicId, gerencia.PublicId]);
    }

    [Fact]
    public async Task Quien_radico_la_ve_pero_excluida()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Asesora);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");

        var item = (await PendientesAsync()).Should().ContainSingle().Subject;

        item.SubjectPublicId.Should().Be(s.PublicId);
        item.CanDecide.Should().BeFalse();
        item.ExcludedReason.Should().Be(RutasDePendiente.ExcluidoPorRadicar);
    }

    [Fact]
    public async Task Sin_permiso_de_decision_no_ve_nada_y_sin_persona_queda_excluido()
    {
        await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read");
        (await PendientesAsync()).Should().BeEmpty();

        _o.G.ActuarComo(_o.SinPersona);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
        (await PendientesAsync()).Should().ContainSingle().Which.ExcludedReason.Should().Be(RutasDePendiente.ExcluidoSinPersona);
    }

    [Fact]
    public async Task Lo_colegiado_aparece_en_la_agenda_de_los_miembros_del_organo()
    {
        _o.Credito(_o.Dario, 40_000_000m, "CR-D");
        var comite = await _o.RadicadaAsync(_o.Solicitud(3_000_000m, 24, _o.Dario));
        var miembro = _o.UsuarioConPersona("Marta", "3003003");
        _o.G.Miembro(_o.Comite, _o.Db.People.Single(p => p.Id == miembro.PersonId));
        _o.G.ActuarComo(miembro);
        _o.G.Conceder("Lending.Applications.Read");

        var item = (await PendientesAsync()).Should().ContainSingle().Subject;

        item.SubjectPublicId.Should().Be(comite.PublicId);
        item.Route.Should().Be(RutasDePendiente.Sesion);
        item.GoverningBody.Should().Be("Comité de crédito");
        item.CanDecide.Should().BeFalse("se decide en la sesión del órgano");

        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Management");
        (await PendientesAsync()).Should().BeEmpty("quien no es miembro del comité no lo tiene en su agenda");
    }

    [Fact]
    public async Task Un_asesor_de_otra_oficina_no_ve_lo_de_la_oficina_ajena()
    {
        var norte = await _o.RadicadaAsync();
        var sur = await _o.RadicadaAsync(_o.Solicitud() with { OfficePublicId = _sur.PublicId });
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");

        _o.G.AlcanceActual = new AlcanceDeCartera(false, new HashSet<int> { _sur.Id }, _sur.Id, true, new HashSet<int>());
        (await PendientesAsync()).Select(p => p.SubjectPublicId).Should().Equal(sur.PublicId);
        _o.G.AlcanceActual = new AlcanceDeCartera(false, new HashSet<int> { _o.Norte.Id }, _o.Norte.Id, true, new HashSet<int>());
        (await PendientesAsync()).Select(p => p.SubjectPublicId).Should().Equal(norte.PublicId);
    }

    [Fact]
    public async Task Los_cupos_que_esperan_decision_tambien_estan_y_se_filtran_por_sujeto()
    {
        var s = await _o.RadicadaAsync();
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        _o.G.ActuarComo(_o.Asesora);
        var cupo = await _o.Asignar().Handle(new AssignCreditLimitCommand(_o.Beto.PublicId, CreditLimitKind.Consolidated, null, CreditLimitOrigin.Assigned,
            2_000_000m, OriginacionDePrueba.Hoy, null, null, "Cupo del asociado") { OperationKey = Guid.NewGuid() }, default);
        cupo.IsSuccess.Should().BeTrue(cupo.IsFailure ? cupo.Error.Message : string.Empty);
        _o.Db.ChangeTracker.Clear();
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");

        (await PendientesAsync()).Select(p => (p.Subject, p.SubjectPublicId)).Should().BeEquivalentTo(
            [(LendingDecisionSubjects.CreditApplication, s.PublicId), (LendingDecisionSubjects.CreditLimit, cupo.Value.PublicId)]);
        (await PendientesAsync(LendingDecisionSubjects.CreditLimit)).Should().ContainSingle().Which.Amount.Should().Be(2_000_000m);
    }
}
