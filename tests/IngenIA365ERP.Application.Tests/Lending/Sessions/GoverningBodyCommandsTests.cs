using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Sessions;

/// <summary>
/// Feature 013, T0247 (US17; T64, FR-039, FR-040, F7, T42; api.md §3.6; data-model §3.5–§3.7): los órganos de decisión por
/// <see cref="CreateGoverningBodyCommand"/>, <see cref="AddGoverningBodyRulesVersionCommand"/>, <see cref="AddGoverningBodyMemberCommand"/>,
/// <see cref="EndGoverningBodyMemberCommand"/> y <see cref="DeactivateGoverningBodyCommand"/>. Código duplicado →
/// <c>Catalogo.CodigoDuplicado</c> con el nombre del existente; un segundo consejo activo → rechazo; la fracción de vinculados bajo
/// <c>VINCULADOS_FRACCION_CALIFICADA</c> en una cooperativa con actividad financiera → <c>Lending.Parameter.BelowLegalFloor</c> con
/// <c>data { limitCode, limitValue, source }</c>; vigencias cruzadas de la misma persona → <c>MemberOverlaps</c>; un miembro que no es
/// persona viva del maestro → <c>Core.Person.NotFound</c>; desactivar un órgano que nombra un nivel vigente → <c>InUse</c> con
/// <c>data.matrixVersion</c>.
/// </summary>
public sealed class GoverningBodyCommandsTests : IDisposable
{
    private readonly GobiernoDePrueba _g = new();

    public GoverningBodyCommandsTests() => _g.ActuarComo(_g.Usuario(_g.Persona("Ana", "Administradora", "1001"), "admin@coop.test"));

    public void Dispose() => _g.Dispose();

    private static ReglasDeOrganoInput Reglas(FraccionInput? vinculados = null, DateOnly? desde = null) =>
        new(new FraccionInput(1, 2), new FraccionInput(1, 2), vinculados, desde ?? GobiernoDePrueba.Hoy, "Estatutos vigentes", "Estatutos, art. 40");

    private static CreateGoverningBodyCommand Crear(string codigo, GoverningBodyKind clase, ReglasDeOrganoInput? reglas = null, string nombre = "Órgano") =>
        new(codigo, nombre, clase, DecidesCredit: true, codigo + "-", reglas ?? Reglas()) { OperationKey = Guid.NewGuid() };

    private Task<Result<GoverningBodyDto>> Enviar(CreateGoverningBodyCommand c) =>
        new CreateGoverningBodyCommandHandler(_g.Db, new ReglasDeVinculados(_g.Valores, _g.Perfil), _g.Reloj).Handle(c, default);

    private Task<Result<GoverningBodyRulesDto>> Enviar(AddGoverningBodyRulesVersionCommand c) =>
        new AddGoverningBodyRulesVersionCommandHandler(_g.Db, new ReglasDeVinculados(_g.Valores, _g.Perfil), _g.Reloj).Handle(c, default);

    private Task<Result<GoverningBodyMemberDto>> Enviar(AddGoverningBodyMemberCommand c) => new AddGoverningBodyMemberCommandHandler(_g.Db).Handle(c, default);

    // ----------------------------------------------------------------------------------------------------------- crear --

    [Fact]
    public async Task Crea_el_organo_con_su_primera_regla_y_el_numero_de_la_proxima_acta()
    {
        var r = await Enviar(Crear("cc", GoverningBodyKind.CreditCommittee, nombre: "Comité de crédito"));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Code.Should().Be("CC", "el código se guarda en mayúsculas");
        r.Value.CurrentRules!.Version.Should().Be(1);
        r.Value.CurrentRules.Quorum.Should().Be(new FraccionDto(1, 2, FractionComparison.MoreThan));
        r.Value.NextMinutesNumber.Should().Be("CC-000001");
        (await _g.Db.GoverningBodyRuleVersions.SingleAsync()).GoverningBodyId.Should().Be((await _g.Db.GoverningBodies.SingleAsync()).Id);
    }

    [Fact]
    public async Task Codigo_duplicado_es_Catalogo_CodigoDuplicado_con_el_nombre_del_existente()
    {
        await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee, nombre: "Comité de crédito"));

        var r = await Enviar(Crear("cc", GoverningBodyKind.RiskCommittee, nombre: "Comité de riesgos"));

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("Comité de crédito");
        GobiernoDePrueba.Datos(r.Error).GetProperty("existingName").GetString().Should().Be("Comité de crédito");
    }

    [Fact]
    public async Task Un_segundo_consejo_activo_se_rechaza()
    {
        (await Enviar(Crear("CA", GoverningBodyKind.BoardOfDirectors, Reglas(new FraccionInput(4, 5)), "Consejo"))).IsSuccess.Should().BeTrue();

        var r = await Enviar(Crear("CA2", GoverningBodyKind.BoardOfDirectors, Reglas(new FraccionInput(4, 5)), "Otro consejo"));

        r.Error.Code.Should().Be("Lending.GoverningBody.BoardAlreadyActive");
    }

    [Fact]
    public async Task Con_actividad_financiera_la_fraccion_de_vinculados_bajo_cuatro_quintos_es_BelowLegalFloor()
    {
        var r = await Enviar(Crear("CA", GoverningBodyKind.BoardOfDirectors, Reglas(new FraccionInput(3, 4)), "Consejo"));

        r.Error.Code.Should().Be("Lending.Parameter.BelowLegalFloor");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("limitCode").GetString().Should().Be("VINCULADOS_FRACCION_CALIFICADA");
        datos.GetProperty("limitValue").GetDecimal().Should().Be(0.8m);
        datos.GetProperty("source").GetString().Should().Contain("Ley 454");
        (await _g.Db.GoverningBodies.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_cooperativa_puede_endurecer_la_fraccion_y_sin_actividad_financiera_rigen_los_estatutos()
    {
        (await Enviar(Crear("CA", GoverningBodyKind.BoardOfDirectors, Reglas(new FraccionInput(5, 6)), "Consejo"))).IsSuccess.Should().BeTrue();

        _g.ConActividadFinanciera(false);
        var organo = await _g.Db.GoverningBodies.SingleAsync();
        var r = await Enviar(new AddGoverningBodyRulesVersionCommand(organo.PublicId, Reglas(new FraccionInput(2, 3))) { OperationKey = Guid.NewGuid() });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task Con_actividad_financiera_el_consejo_sin_fraccion_de_vinculados_se_rechaza()
    {
        var r = await Enviar(Crear("CA", GoverningBodyKind.BoardOfDirectors, Reglas(), "Consejo"));

        r.Error.Code.Should().Be("Lending.GoverningBody.RulesInvalid");
        GobiernoDePrueba.Datos(r.Error).GetProperty("reason").GetString().Should().Be("RelatedPartyFractionRequired");
    }

    [Fact]
    public async Task Una_regla_que_rige_desde_antes_de_hoy_se_rechaza()
    {
        var r = await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee, Reglas(desde: GobiernoDePrueba.Hoy.AddDays(-1))));

        r.Error.Code.Should().Be("Lending.GoverningBody.RulesInvalid");
        GobiernoDePrueba.Datos(r.Error).GetProperty("reason").GetString().Should().Be("ValidFromInPast");
    }

    [Fact]
    public async Task Una_version_nueva_de_reglas_rige_desde_su_fecha_y_la_anterior_hasta_la_vispera()
    {
        var creado = await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee));
        var r = await Enviar(new AddGoverningBodyRulesVersionCommand(creado.Value.PublicId,
            new ReglasDeOrganoInput(new FraccionInput(2, 3, FractionComparison.AtLeast), new FraccionInput(1, 2), null, GobiernoDePrueba.Hoy.AddDays(5),
                "Reforma de estatutos", "Acta de asamblea 3")) { OperationKey = Guid.NewGuid() });
        r.IsSuccess.Should().BeTrue();
        var get = new GetGoverningBodyQueryHandler(_g.Db, _g.Reloj);

        (await get.Handle(new GetGoverningBodyQuery(creado.Value.PublicId, GobiernoDePrueba.Hoy.AddDays(4)), default)).Value.CurrentRules!.Version.Should().Be(1);
        var despues = (await get.Handle(new GetGoverningBodyQuery(creado.Value.PublicId, GobiernoDePrueba.Hoy.AddDays(5)), default)).Value;
        despues.CurrentRules!.Version.Should().Be(2);
        despues.RulesHistory.Select(v => v.Version).Should().Equal(2, 1);
    }

    // -------------------------------------------------------------------------------------------------------- miembros --

    [Fact]
    public async Task Nombra_miembros_y_rechaza_vigencias_cruzadas_de_la_misma_persona()
    {
        var organo = (await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee))).Value;
        var pedro = _g.Persona("Pedro", "Miembro", "3003");

        var primero = await Enviar(new AddGoverningBodyMemberCommand(organo.PublicId, pedro.PublicId, BodyMemberRole.Chair, true, GobiernoDePrueba.Hoy)
            { OperationKey = Guid.NewGuid() });
        var cruzado = await Enviar(new AddGoverningBodyMemberCommand(organo.PublicId, pedro.PublicId, BodyMemberRole.Member, true, GobiernoDePrueba.Hoy.AddDays(30))
            { OperationKey = Guid.NewGuid() });

        primero.IsSuccess.Should().BeTrue(primero.IsFailure ? primero.Error.Message : string.Empty);
        primero.Value.Person.FullName.Should().Be("Pedro Miembro");
        cruzado.Error.Code.Should().Be("Lending.GoverningBody.MemberOverlaps");
    }

    [Fact]
    public async Task Un_miembro_que_no_es_persona_viva_del_maestro_es_Core_Person_NotFound()
    {
        var organo = (await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee))).Value;
        var eliminada = _g.Persona("Eva", "Eliminada", "4004");
        eliminada.IsDeleted = true;
        await _g.Db.SaveChangesAsync();

        var inexistente = await Enviar(new AddGoverningBodyMemberCommand(organo.PublicId, Guid.NewGuid(), BodyMemberRole.Member, true, GobiernoDePrueba.Hoy)
            { OperationKey = Guid.NewGuid() });
        var deBaja = await Enviar(new AddGoverningBodyMemberCommand(organo.PublicId, eliminada.PublicId, BodyMemberRole.Member, true, GobiernoDePrueba.Hoy)
            { OperationKey = Guid.NewGuid() });

        inexistente.Error.Code.Should().Be("Core.Person.NotFound");
        deBaja.Error.Code.Should().Be("Core.Person.NotFound");
    }

    [Fact]
    public async Task El_suplente_nombra_a_un_principal_del_mismo_organo_y_terminar_cierra_la_vigencia()
    {
        var organo = (await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee))).Value;
        var principal = (await Enviar(new AddGoverningBodyMemberCommand(organo.PublicId, _g.Persona("Pía", "Principal", "5005").PublicId,
            BodyMemberRole.Member, true, GobiernoDePrueba.Hoy) { OperationKey = Guid.NewGuid() })).Value;

        var suplente = await Enviar(new AddGoverningBodyMemberCommand(organo.PublicId, _g.Persona("Saúl", "Suplente", "6006").PublicId,
            BodyMemberRole.Alternate, true, GobiernoDePrueba.Hoy, principal.MemberPublicId) { OperationKey = Guid.NewGuid() });
        suplente.IsSuccess.Should().BeTrue(suplente.IsFailure ? suplente.Error.Message : string.Empty);
        suplente.Value.AlternateForMemberPublicId.Should().Be(principal.MemberPublicId);

        var fin = await new EndGoverningBodyMemberCommandHandler(_g.Db).Handle(
            new EndGoverningBodyMemberCommand(organo.PublicId, principal.MemberPublicId, GobiernoDePrueba.Hoy.AddDays(60), "Renuncia") { OperationKey = Guid.NewGuid() }, default);
        fin.IsSuccess.Should().BeTrue();
        (await _g.Db.GoverningBodyMembers.SingleAsync(m => m.PublicId == principal.MemberPublicId)).ValidTo.Should().Be(GobiernoDePrueba.Hoy.AddDays(60));

        var otro = await new EndGoverningBodyMemberCommandHandler(_g.Db).Handle(
            new EndGoverningBodyMemberCommand(organo.PublicId, Guid.NewGuid(), GobiernoDePrueba.Hoy, "x") { OperationKey = Guid.NewGuid() }, default);
        otro.Error.Code.Should().Be("Lending.GoverningBody.MemberNotFound");
    }

    // ---------------------------------------------------------------------------------------------------- desactivar --

    [Fact]
    public async Task Desactivar_un_organo_que_nombra_un_nivel_vigente_es_InUse_con_la_version()
    {
        var comite = (await Enviar(Crear("CC", GoverningBodyKind.CreditCommittee))).Value;
        var consejo = (await Enviar(Crear("CA", GoverningBodyKind.BoardOfDirectors, Reglas(new FraccionInput(4, 5)), "Consejo"))).Value;
        var riesgos = (await Enviar(Crear("CR", GoverningBodyKind.RiskCommittee, nombre: "Comité de riesgos"))).Value;
        var matriz = await new AddAttributionMatrixVersionCommandHandler(_g.Db, _g.Actor, _g.Reloj).Handle(new AddAttributionMatrixVersionCommand(
            GobiernoDePrueba.Hoy, "Reglamento", null,
            [
                new("ANALISTA", "Analista", 10, AttributionLevelKind.Individual, "Lending.Decisions.Analyst", ConsolidatedAmountCeiling: 5_000_000m,
                    IsExceptionLevel: true),
                new("COMITE", "Comité", 20, AttributionLevelKind.Collegiate, GoverningBodyPublicId: comite.PublicId, ConsolidatedAmountCeiling: 50_000_000m),
                new("CONSEJO", "Consejo", 30, AttributionLevelKind.Collegiate, GoverningBodyPublicId: consejo.PublicId, IsBoardLevel: true),
            ], []) { OperationKey = Guid.NewGuid() }, default);
        matriz.IsSuccess.Should().BeTrue(matriz.IsFailure ? matriz.Error.Message : string.Empty);
        var desactivar = new DeactivateGoverningBodyCommandHandler(_g.Db, _g.Reloj);

        var enUso = await desactivar.Handle(new DeactivateGoverningBodyCommand(comite.PublicId, "Se fusiona") { OperationKey = Guid.NewGuid() }, default);
        var libre = await desactivar.Handle(new DeactivateGoverningBodyCommand(riesgos.PublicId, "Se fusiona") { OperationKey = Guid.NewGuid() }, default);

        enUso.Error.Code.Should().Be("Lending.GoverningBody.InUse");
        GobiernoDePrueba.Datos(enUso.Error).GetProperty("matrixVersion").GetInt32().Should().Be(1);
        libre.IsSuccess.Should().BeTrue();
        (await _g.Db.GoverningBodies.SingleAsync(b => b.PublicId == riesgos.PublicId)).IsActive.Should().BeFalse();

        var lista = await new ListGoverningBodiesQueryHandler(_g.Db, _g.Reloj).Handle(new ListGoverningBodiesQuery(), default);
        lista.Value.Select(b => b.Code).Should().Equal("CA", "CC");
        var todas = await new ListGoverningBodiesQueryHandler(_g.Db, _g.Reloj).Handle(new ListGoverningBodiesQuery(IncludeInactive: true), default);
        todas.Value.Should().HaveCount(3);
    }
}
