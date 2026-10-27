using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// Feature 015, T0296 (R-26; FR-148, FR-223): con Recaudo y Tesorería reales los dobles de prueba de la 013 se retiraron. El único escritor del registro
/// (<see cref="EscritorDelRegistroDeIntegracion"/>) rechaza un participante de clase <c>Test</c> o marcado <c>IsTestDouble</c> en <b>todo</b> ambiente
/// (<c>Integration.Participant.TestDoublesRetired</c>; hasta T0308 sólo en producción) y no agrega nada; habilitar o adoptar sobre la fila de un doble
/// —que ya no debería estar abierta, la cierra la migración <c>RetiroDeLosDoblesDePrueba</c>— tampoco reabre un doble; y lo real sigue registrándose
/// igual. Lo demás del retiro lo fijan: los requisitos de Cartera y Captaciones, que ya no admiten la clase <c>Test</c> fuera de producción
/// (<c>RequisitosDeActivacionTests</c>, <c>ActivacionDeCaptacionesSinCargaTests</c>); la fila real <c>TES</c> que abre una vigencia nueva sobre la misma
/// llave lógica del doble (<c>ActivacionMinimaTests</c>; mensajes.md §8); y la migración, sólo de datos e idempotente (<c>RetiroDeLosDoblesDePruebaEsSoloDatos</c>
/// en Architecture). InMemory con ambiente, actor y reloj falsos.
/// </summary>
public class RetiroDeLosDoblesTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public RetiroDeLosDoblesTests()
    {
        _reloj.UtcNow.Returns(new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
        _reloj.HoyLocal.Returns(Hoy);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 3, Guid.NewGuid(), Guid.NewGuid(),
            "Administradora de ensayo", "admin@coop.test", ExecutionChannel.Web, "POST /api/core/integration-participants", "10.0.0.2", null));
    }

    private EscritorDelRegistroDeIntegracion Escritor() => new(_db, _ambiente, _actor, _reloj);

    private static NuevoParticipante Nuevo(string codigo, IntegrationParticipantClass clase, IntegrationParticipantRole rol, string? destino, bool doble = false,
        params VersionAdoptada[] adopciones) =>
        new(codigo, clase, rol, destino, true, doble, adopciones, Hoy, "Alta de prueba");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task El_escritor_rechaza_un_doble_por_su_marca_o_por_su_clase_en_todo_ambiente(bool produccion)
    {
        _ambiente.EsProduccion.Returns(produccion);

        var porMarca = await Escritor().RegistrarAsync(Nuevo("TST", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, doble: true, new VersionAdoptada("OrdenDeGiroEmitida", 1)), default);
        var porClase = await Escritor().RegistrarAsync(Nuevo("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.ObligationsCollector, null,
            adopciones: new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1)), default);

        porMarca.Error.Code.Should().Be(ErroresDeIntegracion.CodigoDoblesRetirados);
        porClase.Error.Code.Should().Be(ErroresDeIntegracion.CodigoDoblesRetirados);
        _db.IntegrationParticipants.Local.Should().BeEmpty("el rechazo no agrega nada a la unidad de trabajo");
    }

    [Fact]
    public async Task Habilitar_la_fila_de_un_doble_que_siguiera_abierta_tampoco_reabre_un_doble()
    {
        _ambiente.EsProduccion.Returns(false);
        var doble = new IntegrationParticipant
        {
            Code = "TES", Class = IntegrationParticipantClass.Test, Role = IntegrationParticipantRole.MessageDestination,
            Destination = IntegrationDestinations.Treasury, IsTestDouble = true, IsEnabled = true, ValidFrom = Hoy.AddMonths(-3), Reason = "doble",
            RegisteredByKind = ActorKind.Process, RegisteredByName = "Prueba",
        };
        _db.IntegrationParticipants.Add(doble);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var r = await Escritor().CambiarHabilitacionAsync("TES", IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, false, Hoy,
            "Apagar el doble", default);

        r.Error.Code.Should().Be(ErroresDeIntegracion.CodigoDoblesRetirados, "registrar otra vigencia de un doble es registrar un doble");
        (await _db.IntegrationParticipants.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Lo_real_se_sigue_registrando_igual()
    {
        _ambiente.EsProduccion.Returns(false);

        var recaudo = await Escritor().RegistrarAsync(Nuevo("REC", IntegrationParticipantClass.Receipts, IntegrationParticipantRole.ObligationsCollector, null,
            adopciones: new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1)), default);
        var tesoreria = await Escritor().RegistrarAsync(Nuevo("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, adopciones: new VersionAdoptada("OrdenDeGiroEmitida", 1)), default);

        recaudo.IsSuccess.Should().BeTrue();
        tesoreria.IsSuccess.Should().BeTrue();
        _db.IntegrationParticipants.Local.Should().OnlyContain(p => !p.IsTestDouble && p.Class != IntegrationParticipantClass.Test);
    }
}
