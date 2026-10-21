using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0681 (FR-099, FR-101; api.md §12.2; data-model §23.6): la calificación por análisis sólo empeora la que dan las reglas
/// (<c>NotWorse</c> con <c>data.ruleCategory</c>), hay una vigente por crédito (<c>AlreadyActive</c>) y se levanta con motivo (<c>NotActive</c> la
/// segunda vez); la consulta del crédito y la lista diaria la muestran como <b>informativa</b> con la del análisis encima de las reglas, y el
/// ciclo diario copia la categoría del día en la posición.
/// </summary>
public class AnalysisRatingCommandsTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        _c.G.ActuarComo(_c.Jefe);
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private Task<Result<AnalysisRatingDto>> PonerAsync(RatingCategory categoria, RatingSubcategory? sub = null) =>
        new SetAnalysisRatingCommandHandler(_c.Db, _c.Informativa(), _c.G.Alcance, _c.G.Auditoria, _c.G.Actor, _c.G.Reloj)
            .Handle(new SetAnalysisRatingCommand(_c.Credito.PublicId, categoria, sub, "Deudor reportado en insolvencia por otra entidad") { OperationKey = Guid.NewGuid() }, default);

    private Task<Result> LevantarAsync(Guid analisis) =>
        new LiftAnalysisRatingCommandHandler(_c.Db, _c.G.Alcance, _c.G.Auditoria, _c.G.Actor, _c.G.Reloj)
            .Handle(new LiftAnalysisRatingCommand(analisis, "Se normalizó") { OperationKey = Guid.NewGuid() }, default);

    private Task<Result<LoanRatingDto>> ConsultarAsync() =>
        new GetLoanRatingQueryHandler(_c.Db, _c.Informativa(), _c.G.Alcance, _c.G.Auditoria, _c.G.Reloj).Handle(new GetLoanRatingQuery(_c.Credito.PublicId), default);

    [Fact]
    public async Task Igual_o_mejor_que_las_reglas_responde_NotWorse_con_la_categoria_de_las_reglas()
    {
        var r = await PonerAsync(RatingCategory.A);

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoAnalisisNoEmpeora);
        GobiernoDePrueba.Datos(r.Error).GetProperty("ruleCategory").GetString().Should().Be("A");
    }

    [Fact]
    public async Task Peor_que_las_reglas_queda_vigente_y_manda_en_la_consulta_del_credito()
    {
        var r = await PonerAsync(RatingCategory.C);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Should().BeEquivalentTo(new { Category = "C", Subcategory = (string?)null, Since = CierreDePrueba.Hoy, LiftedAt = (DateTime?)null },
            o => o.ExcludingMissingMembers());

        var consulta = await ConsultarAsync();
        consulta.IsSuccess.Should().BeTrue(consulta.IsFailure ? $"{consulta.Error.Code}: {consulta.Error.Message}" : string.Empty);
        consulta.Value.Label.Should().Be(CalificacionInformativa.Rotulo);
        consulta.Value.Category.Should().Be("C");
        consulta.Value.Cause.Should().Be(nameof(RatingCause.Analysis));
        consulta.Value.Analysis!.PublicId.Should().Be(r.Value.PublicId);
        consulta.Value.Official.Should().BeNull("todavía no hay cierre mensual con línea del crédito");
    }

    [Fact]
    public async Task Con_una_vigente_otra_responde_AlreadyActive_y_levantarla_dos_veces_NotActive()
    {
        var primera = (await PonerAsync(RatingCategory.C)).Value;

        var otra = await PonerAsync(RatingCategory.D);
        otra.Error.Code.Should().Be(ErroresDeRiesgo.CodigoAnalisisYaVigente);
        GobiernoDePrueba.Datos(otra.Error).GetProperty("analysisRatingPublicId").GetGuid().Should().Be(primera.PublicId);

        (await LevantarAsync(primera.PublicId)).IsSuccess.Should().BeTrue();
        (await LevantarAsync(primera.PublicId)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoAnalisisNoVigente);
        (await PonerAsync(RatingCategory.D)).IsSuccess.Should().BeTrue("levantada la anterior, se puede poner otra");
        (await _c.Db.AnalysisRatings.AsNoTracking().CountAsync()).Should().Be(2, "la levantada no se borra");
    }

    [Fact]
    public async Task Levantar_una_que_no_existe_responde_NotFound()
    {
        (await LevantarAsync(Guid.NewGuid())).Error.Code.Should().Be(ErroresDeRiesgo.CodigoAnalisisInexistente);
    }

    [Fact]
    public async Task La_lista_diaria_trae_la_calificacion_informativa_de_cada_credito_y_filtra_por_categoria()
    {
        await PonerAsync(RatingCategory.C);
        var handler = new ListDailyRatingsQueryHandler(_c.Db, _c.Informativa(), _c.G.Alcance, _c.G.Reloj);

        var todas = await handler.Handle(new ListDailyRatingsQuery(), default);
        var deB = await handler.Handle(new ListDailyRatingsQuery(Category: RatingCategory.B), default);

        todas.IsSuccess.Should().BeTrue(todas.IsFailure ? $"{todas.Error.Code}: {todas.Error.Message}" : string.Empty);
        var fila = todas.Value.Items.Should().ContainSingle().Subject;
        (fila.LoanPublicId, fila.Category, fila.Cause, fila.Label, fila.OfficialCategory).Should()
            .Be((_c.Credito.PublicId, "C", nameof(RatingCause.Analysis), CalificacionInformativa.Rotulo, (string?)null));
        deB.Value.Items.Should().BeEmpty();
        deB.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task El_ciclo_diario_con_la_calificacion_deja_la_categoria_del_dia_en_la_posicion()
    {
        var ciclo = new CicloDiarioDeCartera(_c.Db, _c.D.O.Cerrojo, _c.C.Cargador(), _c.D.Registro(), _c.C.Partes(), _c.C.Reajuste(), _c.C.Verificacion(),
            _c.D.Parametros(), _c.D.Alertas, _c.C.Tanda, _c.G.Reloj, NullLogger<CicloDiarioDeCartera>.Instance,
            calificacion: new CalificacionDelCiclo(_c.Db, _c.Contextos()));
        var dia = new DateOnly(2026, 12, 2);
        _c.C.Ir(dia);
        var corrida = await _c.C.CicloAsync(dia);

        for (var i = 0; i < 20; i++)
        {
            var r = await new RunDailyAccrualCommandHandler(_c.Db, ciclo, _c.G.Reloj).Handle(new RunDailyAccrualCommand(corrida.PublicId), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
            if (r.Value == Application.Lending.Processes.AvanceDeCorrida.Terminada) break;
        }

        var posicion = await _c.Db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == _c.Credito.Id && p.PositionDate == dia).SingleAsync();
        posicion.DailyRating.Should().Be(RatingCategory.A);
    }
}
