using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US13 (T145; T143): el asociado simula con las líneas publicadas y obtiene la misma cuota que el simulador del ERP; la solicitud queda en
/// borrador con canal de origen y a nombre del asociado de la petición (no del cuerpo), sin usuario del ERP. Una línea no publicada no está disponible.
/// </summary>
public sealed class CreditoEnLineaTests : IDisposable
{
    private readonly LineasDePrueba _l = new();
    private Person _persona = null!;
    private Guid _linea;

    public void Dispose() => _l.Dispose();

    private async Task PrepararAsync(string? lineaPublicada = null)
    {
        await _l.NormativaAsync();
        _linea = (await _l.LineaAsync("CON")).PublicId;
        await _l.LineaAsync("OTRA");
        var db = _l.Db;
        _persona = _l.G.Persona("Ana", "Núñez", "52000001");
        _persona.IsAssociate = true;
        db.Branches.Add(new Branch { Name = "Principal", TenantBranchPublicId = Guid.NewGuid(), CreatedBy = "test" });
        await db.SaveChangesAsync();
        db.Associates.Add(new Associate { PersonId = _persona.Id, Status = "A", JoinDate = LineasDePrueba.Hoy.AddYears(-2), BranchId = db.Branches.First().Id });
        db.ChannelEnablements.Add(new ChannelEnablement { PersonId = _persona.Id, Status = EnablementStatus.Enabled, EnabledAt = DateTime.UtcNow });
        db.ChannelPublishedConcepts.Add(new ChannelPublishedConcept { Kind = ConceptKind.Loan, LineCode = lineaPublicada ?? "CON" });
        if (!await db.LendingSetups.AnyAsync()) db.LendingSetups.Add(new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica, ActivationDate = LineasDePrueba.Hoy.AddMonths(-1) });
        else (await db.LendingSetups.FirstAsync()).ActivationDate = LineasDePrueba.Hoy.AddMonths(-1);
        await db.SaveChangesAsync();
    }

    private SimulateChannelCreditQueryHandler Simular() => new(_l.Db, new SimuladorDeCredito(_l.Db, _l.Reglas()), _l.G.Reloj);

    private CreateChannelCreditApplicationCommandHandler Crear() => new(_l.Db, new SimuladorDeCredito(_l.Db, _l.Reglas()), _l.G.Perfil, _l.G.Auditoria, _l.G.Actor,
        new AuditoriaDelCanal(new ServiceCollection().AddSingleton(_l.G.Mongo).BuildServiceProvider(), NullLogger<AuditoriaDelCanal>.Instance), _l.G.Reloj);

    [Fact]
    public async Task Solo_las_lineas_publicadas_y_la_misma_cuota_que_el_simulador_del_ERP()
    {
        await PrepararAsync();
        (await new ListPublishedCreditLinesQueryHandler(_l.Db).Handle(new ListPublishedCreditLinesQuery(), default)).Value
            .Should().ContainSingle().Which.Code.Should().Be("CON");

        var delCanal = await Simular().Handle(new SimulateChannelCreditQuery(_persona.PublicId, _linea, 5_000_000m, 24), default);
        delCanal.IsSuccess.Should().BeTrue(delCanal.IsFailure ? delCanal.Error.Message : null);
        var linea = await _l.Db.CreditLines.AsNoTracking().FirstAsync(x => x.PublicId == _linea);
        var perfil = await PersonaDeOriginacion.PerfilAsync(_l.Db, _persona, LineasDePrueba.Hoy, default);
        var delErp = await new SimuladorDeCredito(_l.Db, _l.Reglas()).SimularAsync(linea,
            new ScheduleSimulationInput(_linea, 5_000_000m, 24, PersonPublicId: _persona.PublicId), perfil, LineasDePrueba.Hoy, default);
        delCanal.Value.Installments[0].Total.Should().Be(delErp.Value.Plan.Cuotas[0].Total);
        delCanal.Value.SimulationSha256.Should().Be(delErp.Value.Huella);

        var otra = (await _l.Db.CreditLines.AsNoTracking().FirstAsync(x => x.Code == "OTRA")).PublicId;
        (await Simular().Handle(new SimulateChannelCreditQuery(_persona.PublicId, otra, 5_000_000m, 24), default)).Error.Code
            .Should().Be(ErroresDelCreditoEnLinea.LineaNoPublicada);
    }

    [Fact]
    public async Task La_solicitud_queda_en_borrador_con_canal_de_origen_a_nombre_del_asociado_de_la_peticion()
    {
        await PrepararAsync();
        _l.G.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.AsociadoPorCanal(_persona.PublicId, "canal", "190.0.0.1"));

        var r = await Crear().Handle(new CreateChannelCreditApplicationCommand(_persona.PublicId, _linea, 5_000_000m, 24, "Estudio", 4_000_000m, 1_500_000m)
            { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : null);
        r.Value.Status.Should().Be(nameof(CreditApplicationStatus.Draft));
        var s = await _l.Db.CreditApplications.SingleAsync();
        s.IntakeChannel.Should().Be(ApplicationIntakeChannel.Channel);
        s.CreatedByUserId.Should().BeNull();
        s.CreatedByMemberPublicId.Should().Be(_persona.PublicId);
        s.RequestedAmount.Should().Be(5_000_000m);
        (await new ListChannelCreditApplicationsQueryHandler(_l.Db).Handle(new ListChannelCreditApplicationsQuery(_persona.PublicId), default)).Value
            .Should().ContainSingle().Which.PublicId.Should().Be(s.PublicId);
    }

    [Fact]
    public async Task Otra_persona_no_envia_la_solicitud_por_el_asociado()
    {
        await PrepararAsync();
        _l.G.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.AsociadoPorCanal(Guid.NewGuid(), "canal", "190.0.0.1"));

        var r = await Crear().Handle(new CreateChannelCreditApplicationCommand(_persona.PublicId, _linea, 5_000_000m, 24, null, null, null), default);

        r.Error.Code.Should().Be(ErroresDelCreditoEnLinea.NoEsElAsociado);
        (await _l.Db.CreditApplications.CountAsync()).Should().Be(0);
    }
}
