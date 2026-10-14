using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 013, US8, T0723 (FR-142, T84; api.md §19.3 <c>ActivationReadinessDto</c>; data-model §30.1): cada requisito de la activación con su estado y
/// quién lo arregla, en producción y fuera de ella (<see cref="IAmbienteDeDespliegue"/> doble en los dos valores). Fuera de producción los dobles de
/// Recaudo y Tesorería cuentan, la falta de la pérdida esperada de una obligada no bloquea (marca <c>ObligatoryModelUnavailable</c>) y los requisitos de
/// producción que faltan quedan <c>Waived</c>; en producción bloquean. Los avisos no bloquean. InMemory con la normativa, la usura y el perfil reales
/// (sus lectores únicos) y Contabilidad y el registro de integración dobles. (nuevo)
/// </summary>
public sealed class RequisitosDeActivacionTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = LineasDePrueba.Hoy;
    private static readonly DateOnly Corte = Hoy.AddDays(-1);

    private readonly LineasDePrueba _l = new();
    private readonly IRegistroDeIntegracion _registro = Substitute.For<IRegistroDeIntegracion>();
    private readonly IContabilidadParaCartera _contabilidad = Substitute.For<IContabilidadParaCartera>();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly List<ParticipanteVigente> _participantes = [];
    private ParticipanteVigente? _tesoreria;
    private Result<ResultadoDeEvaluacionContableDto> _evaluacion = Result.Success(new ResultadoDeEvaluacionContableDto(true, [], []));
    private MigrationLoad _carga = null!;

    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => _l.Db;

    public async Task InitializeAsync()
    {
        _ambiente.EsProduccion.Returns(false);
        _registro.QueCuentanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<IReadOnlyList<ParticipanteVigente>>(_participantes));
        _registro.DestinoAsync(IntegrationDestinations.Treasury, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_tesoreria));
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, ci.Arg<DateOnly>()))));
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_evaluacion));
        await _l.NormativaAsync();
        _carga = new MigrationLoad
        {
            Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, ActivationDeadline = Corte.AddDays(5), IsBalanced = true, SalesReconciled = true,
            CreatedByUserId = _l.Jefe.Id, CurrentCheckVersion = 1,
        };
        Db.MigrationLoads.Add(_carga);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private RequisitosDeActivacion Requisitos() =>
        new(_registro, _contabilidad, _ambiente, Db, _l.Normativa(), new LectorDePerfilRegulatorio(Db), _l.Valores());

    private async Task<EvaluacionDeActivacion> EvaluarAsync(MigrationLoad? carga = null) =>
        await Requisitos().EvaluarConAvisosAsync(carga ?? _carga, Hoy, Hoy, default);

    private static RequisitoDeActivacionDto De(EvaluacionDeActivacion e, string codigo) => e.Requisitos.Single(r => r.Code == codigo);

    // ------------------------------------------------------------------------------------------------------------ armado --

    private void Recaudador(IntegrationParticipantClass clase, bool adoptado = true) =>
        _participantes.Add(new ParticipanteVigente(Guid.NewGuid(), clase == IntegrationParticipantClass.Test ? "TST" : "REC", clase,
            IntegrationParticipantRole.ObligationsCollector, string.Empty, clase == IntegrationParticipantClass.Test, new DateOnly(1900, 1, 1), null,
            adoptado ? [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1)] : []));

    private void Tesoreria(bool doble, bool adoptado = true) =>
        _tesoreria = new ParticipanteVigente(Guid.NewGuid(), doble ? "TES" : "TSR", doble ? IntegrationParticipantClass.Test : IntegrationParticipantClass.Treasury,
            IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, doble, new DateOnly(1900, 1, 1), null,
            adoptado ? [new VersionAdoptada(OrdenDeGiroEmitidaV1.Type, 1)] : []);

    private async Task AnexoAsync()
    {
        Db.NationalValueReplications.Add(Replica(RegulatoryDocumentKind.SarcAnexo1, "ANEXO1", PlatformNationalValuesSeeder.Contenido("sarc-anexo-1.json"), 90_001));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    private async Task ReglaDePerdidaEsperadaAsync()
    {
        Db.NationalValueReplications.Add(Replica(RegulatoryDocumentKind.ReglaDePerdidaEsperada, "CE100-2026",
            PlatformNationalValuesSeeder.Contenido("regla-de-perdida-esperada.json"), 90_002));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    private static NationalValueReplication Replica(RegulatoryDocumentKind clase, string codigo, string contenido, long secuencia)
    {
        var replica = new NationalValueReplication
        {
            FromSequence = secuencia, ToSequence = secuencia, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = LineasDePrueba.Ahora.AddDays(-1), FinishedAt = LineasDePrueba.Ahora.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new RegulatoryDocument
        {
            Kind = clase.ToString(), Code = codigo, ValidFrom = new DateOnly(2026, 1, 1), Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = false,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = secuencia,
            CentralRegisteredAt = LineasDePrueba.Ahora.AddDays(-1), KnownAt = LineasDePrueba.Ahora.AddDays(-1),
        });
        return replica;
    }

    private async Task PerfilAsync(string tipo, DateOnly desde)
    {
        Db.RegulatoryProfileVersions.Add(new RegulatoryProfileVersion
        {
            OrganizationTypeCode = tipo, FinancialActivityAuthorized = true, SupervisionLevel = 1, ValidFrom = desde, Source = "Certificado",
            Reason = "Perfil", RegisteredByUserId = _l.Jefe.Id, RegisteredByName = "Jefe", CreatedBy = "test",
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    private async Task RolesAsync(int faltantes = 0)
    {
        foreach (var plantilla in PerfilesSugeridos.DelModulo(PerfilesSugeridos.ModuloCartera).Skip(faltantes))
            Db.Roles.Add(new Role { Code = CreateSuggestedLendingRolesCommand.CodigoDeRol(plantilla.Key), Name = plantilla.Actor, CreatedBy = "test" });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Una carga con un crédito en una oficina vinculada o no a una sucursal contable.</summary>
    private async Task CreditoEnOficinaAsync(bool vinculada)
    {
        var oficina = new Branch { Name = vinculada ? "Vinculada" : "Suelta", TenantBranchPublicId = vinculada ? Guid.NewGuid() : null, CreatedBy = "test" };
        Db.Branches.Add(oficina);
        await Db.SaveChangesAsync();
        var parte = new MigrationLoadPart { MigrationLoadId = _carga.Id, PartKey = "loans", BatchNumber = 1, FileName = "loans.xlsx", FileSha256 = Guid.NewGuid().ToString("N"),
            ImportedByUserId = _l.Jefe.Id, ImportedAt = LineasDePrueba.Ahora, RowCount = 1 };
        Db.MigrationLoadParts.Add(parte);
        await Db.SaveChangesAsync();
        Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = "CR-1", PersonId = _l.Aseguradora.Id,
            CreditLineCode = "CONLIB", OfficePublicId = oficina.PublicId, DisbursementDate = Corte.AddMonths(-3), OriginalAmount = 1_000_000m });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Todo lo de producción en su sitio: Recaudo y Tesorería reales, el Anexo 1, los roles y una línea con la usura del día.</summary>
    private async Task TodoListoParaProduccionAsync()
    {
        Recaudador(IntegrationParticipantClass.Receipts);
        Tesoreria(doble: false);
        await AnexoAsync();
        await RolesAsync();
        await _l.LineaAsync("CONLIB", LineasDePrueba.Version(desde: Hoy));
        Db.Branches.Add(new Branch { Name = "Principal", TenantBranchPublicId = Guid.NewGuid(), CreatedBy = "test" });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------------------------------------- las pruebas --

    [Fact]
    public async Task Fuera_de_produccion_los_dobles_cuentan_y_lo_de_produccion_no_bloquea()
    {
        Recaudador(IntegrationParticipantClass.Test);
        Tesoreria(doble: true);

        var e = await EvaluarAsync();

        e.Faltantes.Should().BeEmpty();
        De(e, RequisitosDeActivacion.CargaAplicada).Status.Should().Be(EstadosDeRequisito.Met);
        De(e, RequisitosDeActivacion.RecaudadorRegistrado).Status.Should().Be(EstadosDeRequisito.Met);
        De(e, RequisitosDeActivacion.TesoreriaRegistrada).Status.Should().Be(EstadosDeRequisito.Met);
        De(e, RequisitosDeActivacion.Anexo1Vigente).Should().BeEquivalentTo(new { Met = true, Status = EstadosDeRequisito.Waived }, o => o.ExcludingMissingMembers());
        De(e, RequisitosDeActivacion.Anexo1Vigente).WhoFixes.Should().NotBeNull();
        De(e, RequisitosDeActivacion.RolesDeCarteraCreados).Status.Should().Be(EstadosDeRequisito.Waived);
        De(e, RequisitosDeActivacion.ParametrosDePerdidaEsperada).Status.Should().Be(EstadosDeRequisito.NotApplicable);
        De(e, RequisitosDeActivacion.HistorialDeMoraCompleto).Status.Should().Be(EstadosDeRequisito.NotApplicable);
        e.Requisitos.Select(r => r.Code).Should().BeEquivalentTo(
        [
            RequisitosDeActivacion.CargaAplicada, RequisitosDeActivacion.DiferenciasAceptadas, RequisitosDeActivacion.VentasConciliadas,
            RequisitosDeActivacion.FechaEnPlazo, RequisitosDeActivacion.MesContableAbierto, RequisitosDeActivacion.RecaudadorRegistrado,
            RequisitosDeActivacion.TesoreriaRegistrada, RequisitosDeActivacion.PerdidaEsperadaDisponible, RequisitosDeActivacion.ParametrosDePerdidaEsperada,
            RequisitosDeActivacion.HistorialDeMoraCompleto, RequisitosDeActivacion.Anexo1Vigente, RequisitosDeActivacion.UsuraDeLineasActivas,
            RequisitosDeActivacion.RolesDeCarteraCreados, RequisitosDeActivacion.OficinasVinculadas, RequisitosDeActivacion.PuenteDeNominaResuelto,
        ]);
    }

    [Fact]
    public async Task En_produccion_los_dobles_no_cuentan_y_lo_de_produccion_bloquea_con_quien_lo_arregla()
    {
        _ambiente.EsProduccion.Returns(true);
        Recaudador(IntegrationParticipantClass.Test);
        Tesoreria(doble: true);

        var e = await EvaluarAsync();

        e.Faltantes.Select(f => f.Code).Should().BeEquivalentTo(
        [
            RequisitosDeActivacion.RecaudadorRegistrado, RequisitosDeActivacion.TesoreriaRegistrada, RequisitosDeActivacion.Anexo1Vigente,
            RequisitosDeActivacion.RolesDeCarteraCreados,
        ]);
        e.Faltantes.Should().OnlyContain(f => f.Status == EstadosDeRequisito.Missing && f.WhoFixes != null);
        De(e, RequisitosDeActivacion.RecaudadorRegistrado).WhoFixes!.Permission.Should().Be("Core.IntegrationParticipants.Manage");
        De(e, RequisitosDeActivacion.RolesDeCarteraCreados).WhoFixes!.Permission.Should().Be("Security.Roles.Create");
    }

    [Fact]
    public async Task En_produccion_un_participante_real_sin_la_adopcion_no_cuenta()
    {
        _ambiente.EsProduccion.Returns(true);
        Recaudador(IntegrationParticipantClass.Receipts, adoptado: false);
        Tesoreria(doble: false, adoptado: false);

        var e = await EvaluarAsync();

        De(e, RequisitosDeActivacion.RecaudadorRegistrado).Met.Should().BeFalse();
        De(e, RequisitosDeActivacion.TesoreriaRegistrada).Met.Should().BeFalse();
    }

    [Fact]
    public async Task En_produccion_con_todo_en_su_sitio_no_falta_nada()
    {
        _ambiente.EsProduccion.Returns(true);
        await TodoListoParaProduccionAsync();
        await CreditoEnOficinaAsync(vinculada: true);

        var e = await EvaluarAsync();

        e.Faltantes.Should().BeEmpty();
        De(e, RequisitosDeActivacion.UsuraDeLineasActivas).Status.Should().Be(EstadosDeRequisito.Met);
        De(e, RequisitosDeActivacion.OficinasVinculadas).Status.Should().Be(EstadosDeRequisito.Met);
        De(e, RequisitosDeActivacion.PuenteDeNominaResuelto).Status.Should().Be(EstadosDeRequisito.Met);
        De(e, RequisitosDeActivacion.Anexo1Vigente).Status.Should().Be(EstadosDeRequisito.Met);
        e.ObligatoryModelUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task Una_linea_activa_sin_la_usura_de_su_modalidad_a_la_fecha_falta_en_produccion()
    {
        _ambiente.EsProduccion.Returns(true);
        await TodoListoParaProduccionAsync();
        // Desde hoy el catálogo lleva el consumo sin libranza a otra clave de usura, que todavía no tiene valor certificado.
        await _l.CatalogoAsync(LineasDePrueba.Catalogo.Replace("\"subsegment\":\"SinLibranza\",\"sarcSegment\":\"CONSUMO\",\"expectedLossSegment\":\"CONSUMO_SIN_LIBRANZA\",\"usuryKey\":\"CONSUMO_Y_ORDINARIO\"",
            "\"subsegment\":\"SinLibranza\",\"sarcSegment\":\"CONSUMO\",\"expectedLossSegment\":\"CONSUMO_SIN_LIBRANZA\",\"usuryKey\":\"CONSUMO_ESPECIAL\"", StringComparison.Ordinal), Hoy);

        var e = await EvaluarAsync();

        De(e, RequisitosDeActivacion.UsuraDeLineasActivas).Should().BeEquivalentTo(new { Met = false, Status = EstadosDeRequisito.Missing },
            o => o.ExcludingMissingMembers());
        De(e, RequisitosDeActivacion.UsuraDeLineasActivas).Detail.Should().Contain("CONLIB");
        _ambiente.EsProduccion.Returns(false);
        De(await EvaluarAsync(), RequisitosDeActivacion.UsuraDeLineasActivas).Status.Should().Be(EstadosDeRequisito.Waived);
    }

    [Fact]
    public async Task Una_oficina_con_creditos_sin_sucursal_contable_falta_en_produccion()
    {
        _ambiente.EsProduccion.Returns(true);
        await TodoListoParaProduccionAsync();
        await CreditoEnOficinaAsync(vinculada: false);

        var e = await EvaluarAsync();

        e.Faltantes.Select(f => f.Code).Should().Equal(RequisitosDeActivacion.OficinasVinculadas);
        De(e, RequisitosDeActivacion.OficinasVinculadas).WhoFixes!.Module.Should().Be("Contabilidad");
    }

    [Fact]
    public async Task Sin_la_regla_del_recaudo_por_nomina_falta_en_produccion()
    {
        _ambiente.EsProduccion.Returns(true);
        await TodoListoParaProduccionAsync();
        _evaluacion = Result.Success(new ResultadoDeEvaluacionContableDto(false,
            [new HallazgoDeCarteraDto("PagoAplicado", RequisitosDeActivacion.ReglaDelPuente, null, null, "RuleMissing", "Sin regla",
                new QuienCorrigeDto("Contabilidad", null, null))], []));

        var e = await EvaluarAsync();

        e.Faltantes.Select(f => f.Code).Should().Equal(RequisitosDeActivacion.PuenteDeNominaResuelto);
        await _contabilidad.Received().EvaluarAsync(Arg.Is<IReadOnlyList<MensajeContableDto>>(h =>
            ((PagoAplicadoV1)h[0].Payload).Dimensions.CollectorClass == "Payroll"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_cooperativa_obligada_sin_la_perdida_esperada_desplegada_bloquea_en_produccion_y_fuera_activa_con_la_marca()
    {
        await ReglaDePerdidaEsperadaAsync();
        await PerfilAsync("COOP_AHORRO_CREDITO", new DateOnly(2026, 1, 1));
        Recaudador(IntegrationParticipantClass.Test);
        Tesoreria(doble: true);

        var fuera = await EvaluarAsync();
        _ambiente.EsProduccion.Returns(true);
        await TodoListoParaProduccionAsync();
        var dentro = await EvaluarAsync();

        De(fuera, RequisitosDeActivacion.PerdidaEsperadaDisponible).Status.Should().Be(EstadosDeRequisito.Waived);
        fuera.ObligatoryModelUnavailable.Should().BeTrue();
        fuera.Faltantes.Should().BeEmpty();
        dentro.Faltantes.Select(f => f.Code).Should().Equal(RequisitosDeActivacion.PerdidaEsperadaDisponible);
        dentro.ObligatoryModelUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task Obligada_desde_una_fecha_posterior_por_su_perfil_tambien_cuenta()
    {
        _ambiente.EsProduccion.Returns(true);
        await TodoListoParaProduccionAsync();
        await ReglaDePerdidaEsperadaAsync();
        await PerfilAsync("COOP_MULTIACTIVA", new DateOnly(2026, 1, 1));
        await PerfilAsync("COOP_AHORRO_CREDITO", Hoy.AddMonths(3));

        var e = await EvaluarAsync();

        De(e, RequisitosDeActivacion.PerdidaEsperadaDisponible).Should().BeEquivalentTo(new { Met = false }, o => o.ExcludingMissingMembers());
        De(e, RequisitosDeActivacion.PerdidaEsperadaDisponible).Detail.Should().Contain(Hoy.AddMonths(3).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Una_cooperativa_no_obligada_cumple_la_perdida_esperada()
    {
        await ReglaDePerdidaEsperadaAsync();
        await PerfilAsync("COOP_MULTIACTIVA", new DateOnly(2026, 1, 1));

        De(await EvaluarAsync(), RequisitosDeActivacion.PerdidaEsperadaDisponible).Status.Should().Be(EstadosDeRequisito.Met);
    }

    [Fact]
    public async Task Las_diferencias_y_las_ventas_sin_aceptar_faltan_y_dicen_quien_las_acepta()
    {
        var sinCuadrar = new MigrationLoad { Number = 2, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, ActivationDeadline = Corte.AddDays(5),
            IsBalanced = false, SalesReconciled = false, CreatedByUserId = _l.Jefe.Id };

        var e = await EvaluarAsync(sinCuadrar);

        De(e, RequisitosDeActivacion.DiferenciasAceptadas).Should().BeEquivalentTo(new { Met = false, Status = EstadosDeRequisito.Missing },
            o => o.ExcludingMissingMembers());
        De(e, RequisitosDeActivacion.VentasConciliadas).Met.Should().BeFalse();
        De(e, RequisitosDeActivacion.DiferenciasAceptadas).WhoFixes!.Permission.Should().Be("Lending.Migration.AcceptDifferences");
        RequisitosDeActivacion.ConCodigoPropio.Should().BeEquivalentTo([RequisitosDeActivacion.DiferenciasAceptadas, RequisitosDeActivacion.VentasConciliadas]);
    }

    [Fact]
    public async Task Fuera_del_plazo_o_con_el_mes_cerrado_faltan_la_fecha_y_el_mes()
    {
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, null))));

        var e = await Requisitos().EvaluarConAvisosAsync(_carga, Hoy, Corte.AddDays(6), default);

        De(e, RequisitosDeActivacion.FechaEnPlazo).Met.Should().BeFalse();
        De(e, RequisitosDeActivacion.MesContableAbierto).Met.Should().BeFalse();
        De(e, RequisitosDeActivacion.MesContableAbierto).WhoFixes!.Module.Should().Be("Contabilidad");
    }

    [Fact]
    public async Task Los_avisos_no_bloquean_y_cuentan_los_usuarios_de_Cartera_sin_alcance()
    {
        Recaudador(IntegrationParticipantClass.Test);
        Tesoreria(doble: true);
        await RolesAsync();
        var rol = await Db.Roles.FirstAsync(r => r.Code.StartsWith("CARTERA-"));
        Db.UserRoles.Add(new UserRole { UserId = _l.Jefe.Id, RoleId = rol.Id, AssignedAt = LineasDePrueba.Ahora, AssignedBy = "test", CreatedBy = "test" });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        var e = await EvaluarAsync();

        e.Faltantes.Should().BeEmpty();
        e.Avisos.Select(a => a.Code).Should().BeEquivalentTo(
        [
            RequisitosDeActivacion.AvisoParametrosComerciales, RequisitosDeActivacion.AvisoCuentasDeNomina, RequisitosDeActivacion.AvisoUsuariosSinAlcance,
        ]);
        e.Avisos.Single(a => a.Code == RequisitosDeActivacion.AvisoUsuariosSinAlcance).Detail.Should().StartWith("1 ");
    }

    [Fact]
    public async Task Sin_las_dependencias_de_US8_sus_requisitos_no_aplican()
    {
        Recaudador(IntegrationParticipantClass.Test);
        Tesoreria(doble: true);
        _ambiente.EsProduccion.Returns(true);

        var e = await new RequisitosDeActivacion(_registro, _contabilidad, _ambiente).EvaluarConAvisosAsync(_carga, Hoy, Hoy, default);

        e.Requisitos.Where(r => r.Code is RequisitosDeActivacion.Anexo1Vigente or RequisitosDeActivacion.UsuraDeLineasActivas
                or RequisitosDeActivacion.RolesDeCarteraCreados or RequisitosDeActivacion.OficinasVinculadas or RequisitosDeActivacion.PuenteDeNominaResuelto
                or RequisitosDeActivacion.PerdidaEsperadaDisponible)
            .Should().OnlyContain(r => r.Status == EstadosDeRequisito.NotApplicable);
    }
}
