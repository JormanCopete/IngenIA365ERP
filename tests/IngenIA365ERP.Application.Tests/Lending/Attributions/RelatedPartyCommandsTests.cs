using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.ColumnasDeVinculados;

namespace IngenIA365ERP.Application.Tests.Lending.Attributions;

/// <summary>
/// Feature 013, T0250 (US17; FR-040, T42; api.md §3.7; plantillas.md §2): las personas vinculadas por <see cref="RegisterRelatedPartyCommand"/>,
/// <see cref="EndRelatedPartyCommand"/>, <see cref="ListRelatedPartiesQuery"/> e <see cref="ImportRelatedPartiesCommand"/>. Cónyuge o
/// pariente sin la persona del vínculo → <c>LinkRequired</c>; vigencias cruzadas del mismo vínculo → <c>Duplicate</c>; grado por encima del
/// límite legal (<c>VINCULADOS_GRADO_PARENTESCO</c>) → rechazo; listar deja <c>Lending.RelatedParty.Viewed</c> por
/// <c>LendingAuditEmitter</c>; la plantilla con un documento inexistente es error de fila y columna y no guarda nada; lo importado prevalece
/// sobre la parte <c>related-parties</c> de la migración para la misma persona y fundamento.
/// </summary>
public sealed class RelatedPartyCommandsTests : IDisposable
{
    private static readonly string[] Encabezados =
        [C.Persona, C.Fundamento, C.PersonaRelacionada, C.ClaseDeParentesco, C.Grado, C.PorcentajeDeAportes, C.VigenteDesde, C.VigenteHasta, C.Soporte];

    private readonly GobiernoDePrueba _g = new();
    private readonly Person _consejero;
    private readonly Person _hijo;
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private TablaLeida _tabla = new([], [], "xlsx");

    public RelatedPartyCommandsTests()
    {
        _consejero = _g.Persona("Carlos", "Consejero", "8001");
        _hijo = _g.Persona("Héctor", "Hijo", "8002");
        _g.ActuarComo(_g.Usuario(_g.Persona("Olga", "Oficial", "8003"), "oficial@coop.test"));
        _g.Permisos.EsMaestroGlobal.Returns(true);
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
    }

    public void Dispose() => _g.Dispose();

    private AltaDeVinculados Alta() => new(_g.Db, new ReglasDeVinculados(_g.Valores, _g.Perfil), _g.Actor, _g.Reloj);

    private Task<Result<RelatedPartyDto>> Registrar(RegisterRelatedPartyCommand c) => new RegisterRelatedPartyCommandHandler(_g.Db, Alta()).Handle(c, default);

    private static RegisterRelatedPartyCommand Vinculo(Person p, RelatedPartyBasis fundamento, DateOnly? desde = null, Person? por = null,
        KinshipKind? clase = null, byte? grado = null) =>
        new(p.PublicId, fundamento, desde ?? new DateOnly(2026, 3, 1), por?.PublicId, clase, grado, Support: "Acta de asamblea 45") { OperationKey = Guid.NewGuid() };

    // ------------------------------------------------------------------------------------------------------- registrar --

    [Fact]
    public async Task Registra_un_consejero_y_un_pariente_dentro_del_grado_legal()
    {
        var consejero = await Registrar(Vinculo(_consejero, RelatedPartyBasis.BoardMember));
        var hijo = await Registrar(Vinculo(_hijo, RelatedPartyBasis.Relative, por: _consejero, clase: KinshipKind.Consanguinity, grado: 1));

        consejero.IsSuccess.Should().BeTrue(consejero.IsFailure ? consejero.Error.Message : string.Empty);
        hijo.IsSuccess.Should().BeTrue(hijo.IsFailure ? hijo.Error.Message : string.Empty);
        hijo.Value.LinkedTo!.PersonPublicId.Should().Be(_consejero.PublicId);
        hijo.Value.Person.DocumentNumber.Should().Be("8002");
        (await _g.Db.RelatedParties.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(RelatedPartyBasis.Spouse)]
    [InlineData(RelatedPartyBasis.Relative)]
    public async Task Conyuge_o_pariente_sin_la_persona_del_vinculo_es_LinkRequired(RelatedPartyBasis fundamento)
    {
        var r = await Registrar(Vinculo(_hijo, fundamento, clase: KinshipKind.Consanguinity, grado: 1));

        r.Error.Code.Should().Be("Lending.RelatedParty.LinkRequired");
    }

    [Fact]
    public async Task Vigencias_cruzadas_del_mismo_vinculo_es_Duplicate()
    {
        await Registrar(Vinculo(_consejero, RelatedPartyBasis.BoardMember));

        var r = await Registrar(Vinculo(_consejero, RelatedPartyBasis.BoardMember, new DateOnly(2026, 6, 1)));

        r.Error.Code.Should().Be("Lending.RelatedParty.Duplicate");
    }

    [Fact]
    public async Task Un_grado_por_encima_del_limite_legal_se_rechaza()
    {
        var r = await Registrar(Vinculo(_hijo, RelatedPartyBasis.Relative, por: _consejero, clase: KinshipKind.Consanguinity, grado: 3));

        r.Error.Code.Should().Be("Lending.RelatedParty.KinshipDegreeExceeded");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("limitCode").GetString().Should().Be("VINCULADOS_GRADO_PARENTESCO");
        datos.GetProperty("limitValue").GetDecimal().Should().Be(2m);
        (await _g.Db.RelatedParties.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_persona_inexistente_es_Core_Person_NotFound()
    {
        var r = await Registrar(new RegisterRelatedPartyCommand(Guid.NewGuid(), RelatedPartyBasis.BoardMember, new DateOnly(2026, 3, 1)) { OperationKey = Guid.NewGuid() });

        r.Error.Code.Should().Be("Core.Person.NotFound");
    }

    [Fact]
    public async Task Terminar_cierra_la_vigencia_con_quien_y_por_que()
    {
        var v = await Registrar(Vinculo(_consejero, RelatedPartyBasis.BoardMember));

        var fin = await new EndRelatedPartyCommandHandler(_g.Db, _g.Actor).Handle(
            new EndRelatedPartyCommand(v.Value.PublicId, new DateOnly(2026, 12, 31), "Terminó su período") { OperationKey = Guid.NewGuid() }, default);
        var otro = await new EndRelatedPartyCommandHandler(_g.Db, _g.Actor).Handle(
            new EndRelatedPartyCommand(Guid.NewGuid(), new DateOnly(2026, 12, 31), "x") { OperationKey = Guid.NewGuid() }, default);

        fin.IsSuccess.Should().BeTrue();
        var guardado = await _g.Db.RelatedParties.SingleAsync();
        guardado.ValidTo.Should().Be(new DateOnly(2026, 12, 31));
        guardado.EndReason.Should().Be("Terminó su período");
        guardado.EndedByUserId.Should().NotBeNull();
        otro.Error.Code.Should().Be("Lending.RelatedParty.NotFound");
    }

    // ---------------------------------------------------------------------------------------------------------- listar --

    [Fact]
    public async Task Listar_filtra_y_deja_Lending_RelatedParty_Viewed()
    {
        await Registrar(Vinculo(_consejero, RelatedPartyBasis.BoardMember));
        await Registrar(Vinculo(_hijo, RelatedPartyBasis.Relative, por: _consejero, clase: KinshipKind.Consanguinity, grado: 1));
        var listar = new ListRelatedPartiesQueryHandler(_g.Db, _g.Auditoria);

        var todos = await listar.Handle(new ListRelatedPartiesQuery(), default);
        var parientes = await listar.Handle(new ListRelatedPartiesQuery(Basis: RelatedPartyBasis.Relative), default);
        var antes = await listar.Handle(new ListRelatedPartiesQuery(Date: new DateOnly(2026, 2, 1)), default);

        todos.Value.TotalCount.Should().Be(2);
        parientes.Value.Items.Should().ContainSingle().Which.Person.PersonPublicId.Should().Be(_hijo.PublicId);
        antes.Value.Items.Should().BeEmpty();
        (await _g.EventosAsync()).Should().HaveCount(3).And.OnlyContain(e => e == AuditEventTypes.LendingRelatedPartyViewed);
    }

    // -------------------------------------------------------------------------------------------------------- plantilla --

    private void Archivo(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Fila(string persona, string fundamento, string? relacionada = null, string? clase = null, string? grado = null,
        string desde = "2026-03-01") => [persona, fundamento, relacionada, clase, grado, null, desde, null, "Acta de asamblea 45"];

    private async Task<Result<ImportResultDto>> Importar(ModoDeImportacion modo, string motivo = "Carga de vinculados de la asamblea")
    {
        var servicios = new ServiceCollection().AddSingleton(_g.Mongo).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_g.Db, _lector, _g.Permisos, servicios);
        var r = await new ImportRelatedPartiesCommandHandler(_g.Db, ejecutor, Alta())
            .Handle(new ImportRelatedPartiesCommand(modo, new ArchivoDeImportacion("vinculados.xlsx", [1, 2, 3]), motivo), default);
        _g.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public void La_plantilla_tiene_las_columnas_de_plantillas_2_y_es_del_modulo_Lending()
    {
        var d = CatalogoDePlantillasDeCartera.Vinculados;

        d.Clave.Should().Be("lending.related-parties");
        d.Modulo.Should().Be(ModuloDeAuditoria.Lending);
        d.EsDeUnaHoja.Should().BeTrue();
        d.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(Encabezados);
    }

    [Fact]
    public async Task Aplica_las_filas_con_las_reglas_del_alta()
    {
        Archivo(Fila("8001", "BoardMember"), Fila("8002", "pariente", "8001", "consanguinidad", "1"));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await _g.Db.RelatedParties.CountAsync()).Should().Be(2);
        (await _g.Db.RelatedParties.ToListAsync()).Should().OnlyContain(v => v.Source == RelatedPartySource.Import);
    }

    [Fact]
    public async Task Un_documento_inexistente_es_error_de_fila_y_columna_y_no_guarda_nada()
    {
        Archivo(Fila("8001", "BoardMember"), Fila("8002", "Relative", "999999", "Consanguinity", "1"));

        var r = await Importar(ModoDeImportacion.Apply);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var errores = r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;
        errores.Should().ContainSingle(e => e.Row == 3 && e.Column == C.PersonaRelacionada && e.Code == ImportErrors.CellNotFound);
        (await _g.Db.RelatedParties.CountAsync()).Should().Be(0, "apply es todo o nada");
    }

    [Fact]
    public async Task Lo_importado_prevalece_sobre_lo_migrado_para_la_misma_persona_y_fundamento()
    {
        _g.Db.RelatedParties.Add(new RelatedParty
        {
            PersonId = _consejero.Id, Basis = RelatedPartyBasis.BoardMember, ValidFrom = new DateOnly(2025, 1, 1), Source = RelatedPartySource.Migration,
        });
        await _g.Db.SaveChangesAsync();
        _g.Db.ChangeTracker.Clear();
        Archivo(Fila("8001", "BoardMember", desde: "2026-03-01"));

        var r = await Importar(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var filas = await _g.Db.RelatedParties.OrderBy(v => v.ValidFrom).ToListAsync();
        filas.Should().HaveCount(2);
        filas[0].Source.Should().Be(RelatedPartySource.Migration);
        filas[0].ValidTo.Should().Be(new DateOnly(2026, 2, 28), "la migrada termina la víspera de la importada");
        filas[0].EndReason.Should().Be(AltaDeVinculados.MotivoDePrevalencia);
        filas[1].Source.Should().Be(RelatedPartySource.Import);
    }
}
