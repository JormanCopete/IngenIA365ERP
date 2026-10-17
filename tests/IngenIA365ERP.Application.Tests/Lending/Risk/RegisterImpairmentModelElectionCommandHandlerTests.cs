using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K6a, T1017 (FR-105; data-model §23.8; R15): la adopción y el desistimiento voluntarios del modelo de pérdida esperada. Una obligada no
/// adopta ni desiste (<c>Obligated</c>); un crédito vivo de un segmento cubierto sin historial completo niega la adopción con la lista
/// (<c>IncompleteHistory</c>); rige no antes del aviso más la antelación sellada de la regla (<c>NoticeOutOfTerm</c> con <c>earliestValidFrom</c>, pregunta
/// E2) ni antes del último cierre; adopción y desistimiento alternan; el punto del acta debe ser del consejo sobre esta propuesta
/// (<c>Lending.Session.ItemMismatch</c>); quien registra necesita persona (<c>Lending.Security.UserWithoutPerson</c>); sin la PI voluntaria en el Anexo 2
/// vigente no se adopta (<c>VoluntaryPdMissing</c>: la semilla no la trae). Registrada, audita con módulo Lending y no toca ningún cierre. Sin K6a
/// desplegada respondía <c>NotAvailable</c>: con <c>EntregaVigente = K6a</c> esa rama no se alcanza (la cubre el selector, T1014).
/// </summary>
public class RegisterImpairmentModelElectionCommandHandlerTests : IAsyncLifetime
{
    private static readonly DateOnly Enero = new(2027, 1, 1);
    private static readonly DateOnly Aviso = new(2026, 12, 1);

    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await PerdidaEsperadaDePrueba.ReglaAsync(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1));
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_MULTIACTIVA");
        _c.G.ActuarComo(_c.Jefe);
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private Task<Result<ImpairmentModelElectionDto>> RegistrarAsync(ImpairmentModelElectionKind clase = ImpairmentModelElectionKind.VoluntaryAdoption,
        DateOnly? desde = null, DateOnly? aviso = null, Guid? propuesta = null, Guid? punto = null)
    {
        var id = propuesta ?? Guid.NewGuid();
        return new RegisterImpairmentModelElectionCommandHandler(_c.Db, new LectorDeDocumentoNormativo(_c.Db), _c.G.Perfil, new PuntoDeActaCerrado(_c.Db),
                _c.G.Intervenciones(), _c.G.Auditoria, _c.G.Reloj)
            .Handle(new RegisterImpairmentModelElectionCommand(id, clase, desde ?? Enero, null, punto ?? PerdidaEsperadaDePrueba.Acta(_c.Db, CierreDePrueba.Hoy, id),
                Guid.NewGuid(), aviso ?? Aviso, Guid.NewGuid(), "Decisión del consejo") { OperationKey = Guid.NewGuid() }, default);
    }

    private Task AnexoConVoluntariaAsync() => PerdidaEsperadaDePrueba.Anexo2Async(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1), conVoluntaria: true);

    private static void Exito(Result<ImpairmentModelElectionDto> r) =>
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);

    [Fact]
    public async Task Adoptar_registra_la_eleccion_con_la_antelacion_sellada_audita_y_no_toca_cierres()
    {
        await AnexoConVoluntariaAsync();
        var cierres = await _c.Db.MonthlyCloses.CountAsync();

        var r = await RegistrarAsync();

        Exito(r);
        r.Value.Should().BeEquivalentTo(new
        {
            Kind = nameof(ImpairmentModelElectionKind.VoluntaryAdoption), ValidFrom = Enero, NoticeLeadDays = 15, SupervisorNoticeDate = Aviso,
        }, o => o.ExcludingMissingMembers());
        r.Value.Segments.Should().BeEquivalentTo(ReglaDePerdidaEsperada.SegmentosConocidos, "por defecto, los tres segmentos");
        var guardada = await _c.Db.ImpairmentModelElections.AsNoTracking().SingleAsync();
        guardada.IncompleteHistoryCheckJson.Should().Be("[]");
        guardada.RegisteredByUserId.Should().Be(_c.Jefe.Id);
        (await _c.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingImpairmentModelElectionRegistered);
        (await _c.Db.MonthlyCloses.CountAsync()).Should().Be(cierres, "registrar no recalcula ningún cierre");
        (await _c.Db.CaseParticipations.AsNoTracking().CountAsync(p => p.CaseRootPublicId == guardada.PublicId)).Should().Be(1, "quien registra queda como radicador");
    }

    [Fact]
    public async Task Sin_la_PI_voluntaria_en_el_Anexo_2_vigente_no_se_adopta()
    {
        await PerdidaEsperadaDePrueba.Anexo2Async(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1));

        var r = await RegistrarAsync();

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloSinPiVoluntaria);
        GobiernoDePrueba.Datos(r.Error).GetProperty("segments").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Una_cooperativa_obligada_no_adopta_ni_desiste()
    {
        await AnexoConVoluntariaAsync();
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_AHORRO_CREDITO");

        (await RegistrarAsync()).Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloObligada);
        (await RegistrarAsync(ImpairmentModelElectionKind.VoluntaryWithdrawal)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloObligada);
    }

    [Fact]
    public async Task Rige_no_antes_del_aviso_mas_la_antelacion_de_la_regla()
    {
        await AnexoConVoluntariaAsync();

        var r = await RegistrarAsync(aviso: new DateOnly(2026, 12, 25));

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloAvisoFueraDePlazo);
        GobiernoDePrueba.Datos(r.Error).GetProperty("earliestValidFrom").GetString().Should().Be("2027-01-09");
    }

    [Fact]
    public async Task No_rige_desde_una_fecha_ya_cerrada()
    {
        await AnexoConVoluntariaAsync();

        (await RegistrarAsync(desde: new DateOnly(2026, 11, 20), aviso: new DateOnly(2026, 10, 1))).Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloRetroactivo,
            "el cierre base es del 2026-11-30: los cierres no se recalculan");
    }

    [Fact]
    public async Task Adopcion_y_desistimiento_alternan()
    {
        await AnexoConVoluntariaAsync();

        (await RegistrarAsync(ImpairmentModelElectionKind.VoluntaryWithdrawal)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloNoAlterna,
            "sin adopción no hay de qué desistir");
        Exito(await RegistrarAsync());
        (await RegistrarAsync(desde: new DateOnly(2027, 3, 1))).Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloNoAlterna, "dos adopciones seguidas");
        Exito(await RegistrarAsync(ImpairmentModelElectionKind.VoluntaryWithdrawal, desde: new DateOnly(2027, 3, 1), aviso: new DateOnly(2027, 2, 1)));

        var lista = await new ListImpairmentModelElectionsQueryHandler(_c.Db).Handle(new ListImpairmentModelElectionsQuery(), default);
        lista.Value.Select(e => e.Kind).Should().Equal(nameof(ImpairmentModelElectionKind.VoluntaryWithdrawal), nameof(ImpairmentModelElectionKind.VoluntaryAdoption));
    }

    [Fact]
    public async Task El_punto_del_acta_debe_ser_del_cambio_del_modelo_sobre_esta_propuesta()
    {
        await AnexoConVoluntariaAsync();
        var propuesta = Guid.NewGuid();

        var deOtroSujeto = await RegistrarAsync(propuesta: propuesta,
            punto: PerdidaEsperadaDePrueba.Acta(_c.Db, CierreDePrueba.Hoy, propuesta, LendingDecisionSubjects.RiskPolicyVersion));
        var deOtraPropuesta = await RegistrarAsync(propuesta: propuesta, punto: PerdidaEsperadaDePrueba.Acta(_c.Db, CierreDePrueba.Hoy, Guid.NewGuid()));

        deOtroSujeto.Error.Code.Should().Be("Lending.Session.ItemMismatch");
        deOtraPropuesta.Error.Code.Should().Be("Lending.Session.ItemMismatch");
    }

    [Fact]
    public async Task Quien_registra_sin_persona_no_registra()
    {
        await AnexoConVoluntariaAsync();
        _c.G.ActuarComo(_c.D.O.SinPersona);

        (await RegistrarAsync()).Error.Code.Should().Be(RegistroDeIntervenciones.CodigoSinPersona);
        (await _c.Db.ImpairmentModelElections.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_credito_vivo_cubierto_sin_historial_completo_niega_la_adopcion_con_la_lista()
    {
        await AnexoConVoluntariaAsync();
        var carga = new MigrationLoad { Number = 9, CutoffDate = CierreDePrueba.CorteDeLaActivacion, Status = MigrationLoadStatus.Activated, CreatedByUserId = _c.Jefe.Id };
        _c.Db.MigrationLoads.Add(carga);
        await _c.Db.SaveChangesAsync();
        var credito = await _c.Db.Loans.SingleAsync(x => x.Id == _c.Credito.Id);
        credito.MigrationLoadId = carga.Id;
        credito.OpenedOn = new DateOnly(2026, 9, 15);
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        var r = await RegistrarAsync();

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoModeloHistorialIncompleto);
        var faltante = GobiernoDePrueba.Datos(r.Error).GetProperty("loans")[0];
        faltante.GetProperty("loanNumber").GetString().Should().Be(credito.Number);
        faltante.GetProperty("missingMonths").EnumerateArray().Select(m => m.GetString()).Should().Equal("2026-09", "2026-10");
    }

    [Fact]
    public void Sin_K6a_desplegada_la_eleccion_no_esta_disponible()
    {
        SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente).Should().BeTrue("la entrega vigente es K6a");
        ErroresDeRiesgo.ModeloNoDisponible().Code.Should().Be("Lending.ImpairmentModel.NotAvailable");
    }
}
