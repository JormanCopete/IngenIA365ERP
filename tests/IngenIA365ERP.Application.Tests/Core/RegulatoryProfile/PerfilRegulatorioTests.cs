using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Core.RegulatoryProfile;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.RegulatoryProfile;

/// <summary>
/// T0035 (feature 013; FR-018, FR-108; decisiones-transversales T50; data-model parte 3 §4.5; contracts/interfaces.md §11.1, api.md §21.4):
/// el perfil regulatorio. El alta exige un tipo de organización del documento <c>TiposDeOrganizacion</c> vigente en <c>ValidFrom</c>
/// (<c>Core.RegulatoryProfile.OrganizationTypeUnknown</c>) y el número de inscripción si es operador de libranza; corregir es una fila
/// nueva con <c>SupersedesPublicId</c>; <c>IPerfilRegulatorio.VigenteAsync(fecha, informacionHasta)</c> aplica la regla bitemporal
/// (<c>CreatedAt</c> como R, el <c>Id</c> desempata); un grupo NIIF distinto del de la contabilidad alerta
/// <c>Contabilidad.GrupoNiifDistinto</c>.
/// </summary>
public class PerfilRegulatorioTests
{
    private const string Documento = """
        {
          "employeeFundCategories": [ { "code": "CAT_A", "name": "Categoría A" }, { "code": "CAT_B", "name": "Categoría B" } ],
          "organizationTypes": [
            { "code": "COOP_AHORRO_CREDITO", "name": "Cooperativa de ahorro y crédito", "isEmployeeFund": false, "expectedLoss": "Admitted" },
            { "code": "COOP_MULTIACTIVA", "name": "Cooperativa multiactiva con sección de ahorro", "isEmployeeFund": false, "expectedLoss": "NotAdmitted" },
            { "code": "FONDO_EMPLEADOS", "name": "Fondo de empleados", "isEmployeeFund": true, "expectedLoss": "NotAdmitted" }
          ]
        }
        """;

    private static readonly DateTime Ahora = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Enero = new(2026, 1, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly INormativaVigente _normativa = Substitute.For<INormativaVigente>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public PerfilRegulatorioTests()
    {
        var tipos = TiposDeOrganizacion.Leer(Documento, 1);
        tipos.EsValido.Should().BeTrue(string.Join("; ", tipos.Errores.Select(e => e.ToString())));
        _normativa.TiposDeOrganizacionAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DocumentoNormativoVigente<TiposDeOrganizacion>(Guid.NewGuid(), Guid.NewGuid(),
                RegulatoryDocumentKind.TiposDeOrganizacion, "TIPOS-2026", Enero, 1, false, new string('a', 64), EstadoDeRevision.PendienteDeValidar,
                tipos.Documento!)));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(),
            "Rafaela Contadora", null, ExecutionChannel.Web, "POST /api/core/regulatory-profile/versions", null, null));
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(DateOnly.FromDateTime(Ahora));
        _alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    private AddRegulatoryProfileVersionCommandHandler Alta() => new(_db, _normativa, _actor, _reloj, _alertas);

    private static AddRegulatoryProfileVersionCommand Version(string tipo = "COOP_AHORRO_CREDITO", DateOnly? desde = null, byte? niif = 2,
        string? categoria = null, bool operador = false, string? inscripcion = null, Guid? corrige = null) =>
        new(tipo, true, 2, categoria, niif, operador, inscripcion, operador ? new DateOnly(2025, 6, 1) : null, desde ?? Enero,
            "Certificado de existencia 2026", null, "Perfil inicial", corrige) { OperationKey = Guid.NewGuid() };

    private Task<Result<RegulatoryProfileVersionCreatedDto>> Registrar(AddRegulatoryProfileVersionCommand c) => Alta().Handle(c, CancellationToken.None);

    private void Contabilidad(byte grupo)
    {
        _db.AccountingSetups.Add(new AccountingSetup { NiifGroup = grupo, MovementLevel = 5, FirstFiscalYear = 2026, InitializedBy = "test", CreatedBy = "test" });
        _db.SaveChanges();
    }

    /// <summary>Una versión guardada con su instante de registro (R), como la dejaría el alta.</summary>
    private RegulatoryProfileVersion Guardada(DateOnly desde, DateTime registradaEn, string tipo = "COOP_AHORRO_CREDITO", Guid? corrige = null)
    {
        var v = new RegulatoryProfileVersion
        {
            OrganizationTypeCode = tipo, FinancialActivityAuthorized = true, SupervisionLevel = 2, ValidFrom = desde, Source = "Acta",
            Reason = "prueba", SupersedesPublicId = corrige, RegisteredByUserId = 7, RegisteredByName = "Rafaela", CreatedAt = registradaEn,
        };
        _db.RegulatoryProfileVersions.Add(v);
        _db.SaveChanges();
        return v;
    }

    // -------------------------------------------------------------------------------------------------- el alta --

    [Fact]
    public async Task Registra_la_version_con_quien_la_firma()
    {
        var r = await Registrar(Version());

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        var guardada = await _db.RegulatoryProfileVersions.SingleAsync();
        guardada.PublicId.Should().Be(r.Value.VersionPublicId);
        guardada.OrganizationTypeCode.Should().Be("COOP_AHORRO_CREDITO");
        guardada.RegisteredByUserId.Should().Be(7);
        guardada.RegisteredByName.Should().Be("Rafaela Contadora");
        guardada.CreatedAt.Should().Be(Ahora);
        r.Value.ExpectedLoss.Obligated.Should().BeNull("la obligación sobrevenida la conecta US9");
        r.Value.ExpectedLoss.Basis.Should().Be(ExpectedLossEvaluationDto.SinEvaluar);
    }

    [Fact]
    public async Task Un_tipo_de_organizacion_que_no_esta_en_el_documento_vigente_es_OrganizationTypeUnknown()
    {
        var r = await Registrar(Version(tipo: "BANCO"));

        r.Error.Code.Should().Be(ErroresDelPerfilRegulatorio.CodigoTipoDesconocido);
        await _normativa.Received().TiposDeOrganizacionAsync(Enero, null, Arg.Any<CancellationToken>());
        (await _db.RegulatoryProfileVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_documento_de_tipos_vigente_responde_el_error_del_lector()
    {
        _normativa.TiposDeOrganizacionAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DocumentoNormativoVigente<TiposDeOrganizacion>>(new Error("NationalValues.Missing", "Falta el documento.")));

        (await Registrar(Version())).Error.Code.Should().Be("NationalValues.Missing");
    }

    [Fact]
    public async Task Un_fondo_de_empleados_declara_su_categoria_y_los_demas_ninguna()
    {
        (await Registrar(Version(tipo: "FONDO_EMPLEADOS"))).Error.Code.Should().Be(Error.Validation.Code);
        (await Registrar(Version(tipo: "COOP_AHORRO_CREDITO", categoria: "CAT_A"))).Error.Code.Should().Be(Error.Validation.Code);
        (await Registrar(Version(tipo: "FONDO_EMPLEADOS", categoria: "CAT_B"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Operador_de_libranza_exige_el_numero_de_la_inscripcion()
    {
        var validador = new AddRegulatoryProfileVersionCommandValidator();

        validador.Validate(Version(operador: true)).Errors.Should().Contain(e => e.PropertyName == "LibranzaOperatorRegistrationNumber");
        validador.Validate(Version(operador: true, inscripcion: "OL-2025-0091")).IsValid.Should().BeTrue();
        validador.Validate(Version() with { Reason = " " }).IsValid.Should().BeFalse("el motivo es obligatorio");
        validador.Validate(Version() with { SupervisionLevel = 4 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task La_inscripcion_como_operador_se_guarda()
    {
        await Registrar(Version(operador: true, inscripcion: "OL-2025-0091"));

        var v = await _db.RegulatoryProfileVersions.SingleAsync();
        v.LibranzaOperatorRegistered.Should().BeTrue();
        v.LibranzaOperatorRegistrationNumber.Should().Be("OL-2025-0091");
        v.LibranzaOperatorRegistrationDate.Should().Be(new DateOnly(2025, 6, 1));
    }

    [Fact]
    public async Task Dos_versiones_no_empiezan_el_mismo_dia_sin_ser_correccion()
    {
        await Registrar(Version());

        var r = await Registrar(Version(tipo: "COOP_MULTIACTIVA"));

        r.Error.Code.Should().Be(ErroresDelPerfilRegulatorio.CodigoSeCruza);
    }

    [Fact]
    public async Task Corregir_es_una_fila_nueva_y_una_version_se_corrige_una_sola_vez()
    {
        var original = (await Registrar(Version())).Value.VersionPublicId;

        var correccion = await Registrar(Version(tipo: "COOP_MULTIACTIVA", corrige: original));
        correccion.IsSuccess.Should().BeTrue(correccion.IsFailure ? correccion.Error.Message : "");
        (await _db.RegulatoryProfileVersions.CountAsync()).Should().Be(2, "la original no se toca");
        (await _db.RegulatoryProfileVersions.SingleAsync(v => v.PublicId == original)).OrganizationTypeCode.Should().Be("COOP_AHORRO_CREDITO");

        (await Registrar(Version(corrige: original))).Error.Code.Should().Be(ErroresDelPerfilRegulatorio.CodigoSeCruza);
        (await Registrar(Version(corrige: Guid.NewGuid()))).Error.Code.Should().Be(ErroresDelPerfilRegulatorio.CodigoNoEncontrado);

        // Corregir la corrección sí se admite.
        (await Registrar(Version(tipo: "COOP_AHORRO_CREDITO", corrige: correccion.Value.VersionPublicId))).IsSuccess.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------- la lectura bitemporal --

    [Fact]
    public async Task VigenteAsync_toma_la_version_que_cubre_la_fecha_con_lo_registrado_hasta_R()
    {
        var enero = Guardada(Enero, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        var julio = Guardada(new DateOnly(2026, 7, 1), new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc), "COOP_MULTIACTIVA");
        var perfil = new LectorDePerfilRegulatorio(_db);

        (await perfil.VigenteAsync(new DateOnly(2026, 3, 1), null, default))!.VersionPublicId.Should().Be(enero.PublicId);
        (await perfil.VigenteAsync(new DateOnly(2026, 8, 1), null, default))!.VersionPublicId.Should().Be(julio.PublicId);
        (await perfil.VigenteAsync(new DateOnly(2026, 8, 1), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), default))!
            .VersionPublicId.Should().Be(enero.PublicId, "en junio todavía no se conocía la versión de julio");
        (await perfil.VigenteAsync(new DateOnly(2025, 12, 31), null, default)).Should().BeNull("antes de toda versión no hay perfil");
    }

    [Fact]
    public async Task Una_correccion_reemplaza_a_la_que_corrige_desde_que_se_conoce()
    {
        var original = Guardada(Enero, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        var correccion = Guardada(Enero, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), "COOP_MULTIACTIVA", original.PublicId);
        var perfil = new LectorDePerfilRegulatorio(_db);

        (await perfil.VigenteAsync(new DateOnly(2026, 2, 1), null, default))!.VersionPublicId.Should().Be(correccion.PublicId);
        (await perfil.VigenteAsync(new DateOnly(2026, 2, 1), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), default))!
            .OrganizationTypeCode.Should().Be("COOP_AHORRO_CREDITO", "en marzo la corrección no existía");
    }

    [Fact]
    public async Task Con_el_mismo_ValidFrom_y_sin_correccion_desempata_el_Id()
    {
        var registrada = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        Guardada(Enero, registrada);
        var segunda = Guardada(Enero, registrada, "COOP_MULTIACTIVA");

        (await new LectorDePerfilRegulatorio(_db).VigenteAsync(new DateOnly(2026, 2, 1), null, default))!
            .VersionPublicId.Should().Be(segunda.PublicId);
    }

    // ------------------------------------------------------------------------------------------------ grupo NIIF --

    [Fact]
    public async Task Sin_grupo_NIIF_se_propone_el_de_la_contabilidad_sin_alerta()
    {
        Contabilidad(2);

        var r = await Registrar(Version(niif: null));

        (await _db.RegulatoryProfileVersions.SingleAsync()).NiifGroup.Should().Be(2);
        r.Value.NiifGroupMismatch.Should().BeFalse();
        await _alertas.DidNotReceive().LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_grupo_NIIF_distinto_del_de_la_contabilidad_alerta_GrupoNiifDistinto()
    {
        Contabilidad(2);

        var r = await Registrar(Version(niif: 1));

        r.IsSuccess.Should().BeTrue();
        r.Value.NiifGroupMismatch.Should().BeTrue();
        r.Value.AccountingNiifGroup.Should().Be(2);
        await _alertas.Received(1).LevantarAsync(
            Arg.Is<AlertaALevantar>(a => a.TypeCode == "Contabilidad.GrupoNiifDistinto" && a.EntityPublicId == r.Value.VersionPublicId),
            Arg.Any<CancellationToken>());

        var consulta = await new GetRegulatoryProfileQueryHandler(_db, new LectorDePerfilRegulatorio(_db), _reloj)
            .Handle(new GetRegulatoryProfileQuery(), CancellationToken.None);
        consulta.Value.NiifGroupMismatch.Should().BeTrue();
        consulta.Value.Current!.NiifGroup.Should().Be(1);
        consulta.Value.ExpectedLoss.Obligated.Should().BeNull();
    }

    [Fact]
    public async Task Si_la_alerta_no_se_puede_levantar_el_perfil_igual_queda_registrado()
    {
        Contabilidad(3);
        _alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AlertaLevantada>(new Error("Alerts.Type.NotFound", "Tipo sin registrar.")));

        (await Registrar(Version(niif: 1))).IsSuccess.Should().BeTrue();
        (await _db.RegulatoryProfileVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task La_consulta_lista_las_versiones_la_mas_reciente_primero()
    {
        Guardada(Enero, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        Guardada(new DateOnly(2026, 7, 1), new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc), "COOP_MULTIACTIVA");

        var r = await new GetRegulatoryProfileQueryHandler(_db, new LectorDePerfilRegulatorio(_db), _reloj)
            .Handle(new GetRegulatoryProfileQuery(new DateOnly(2026, 3, 1)), CancellationToken.None);

        r.Value.Current!.OrganizationTypeCode.Should().Be("COOP_AHORRO_CREDITO");
        r.Value.Versions.Select(v => v.ValidFrom).Should().Equal(new DateOnly(2026, 7, 1), Enero);
        r.Value.NiifGroupMismatch.Should().BeNull("sin contabilidad ni grupo no hay con qué comparar");
    }
}
