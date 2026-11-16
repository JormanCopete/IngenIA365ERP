using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.PersonStatement;

/// <summary>
/// 2026-11-16 (pedido del dueño): las secciones del estado de cuenta de la persona. «Cuotas pendientes de todas las obligaciones» junta las cuotas por
/// vencer y vencidas de sus créditos vivos como deudora —de los estados de cuenta de cada crédito, con su alcance—, ordenadas por vencimiento, con lo que
/// debe de cada una por concepto y la fila de totales; una sección que no existe se rechaza en el validador y en el handler. (nuevo)
/// </summary>
public sealed class SeccionesDelEstadoDeCuentaTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 11, 16);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Person _ana;

    public SeccionesDelEstadoDeCuentaTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _ana = new Person { FirstName = "ANA", LastName = "PÉREZ", TaxId = "52123456", IdType = "C", CreatedBy = "test" };
        _db.People.Add(_ana);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private GetPersonStatementSectionQueryHandler Handler() => new(_db, _sender, _reloj);

    private static LoanListItemDto Credito(string numero, string estado = "Active") => new(Guid.NewGuid(), numero, new RefDto(Guid.NewGuid(), "ANA PÉREZ"),
        new LoanLineDto(Guid.NewGuid(), "CONSUMO", "Consumo"), new RefDto(Guid.NewGuid(), "Principal"), estado, "Origination", 1_000_000m, 800_000m, 0, Hoy,
        null, []);

    private static StatementInstallmentDto Cuota(int numero, DateOnly vence, string estado, decimal valor, Dictionary<string, decimal> debe) =>
        new(numero, vence, vence, estado, 0m, 0m, 0m, valor, debe);

    private void Creditos(params LoanListItemDto[] creditos) =>
        _sender.Send(Arg.Is<ListLoansQuery>(q => q.Role == "Debtor"), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<LoanListItemDto>(creditos, 1, 200, creditos.Length)));

    private void EstadoDe(LoanListItemDto credito, params StatementInstallmentDto[] cuotas) =>
        _sender.Send(Arg.Is<GetLoanStatementQuery>(q => q.LoanPublicId == credito.PublicId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new LoanStatementDto(credito.PublicId, credito.Number, credito.Status, Hoy, null, "sha256:prueba", 0, null, 0, 0m,
                new Dictionary<string, decimal>(), cuotas, [], new StatementMarksDto(false, false, false, 0), [], null, null, null)));

    [Fact]
    public async Task Las_cuotas_pendientes_juntan_las_de_todos_sus_creditos_vivos_por_vencimiento_con_totales()
    {
        var viejo = Credito("CR-000002");
        var nuevo = Credito("CR-000003");
        var cancelado = Credito("CR-000001", "PaidOff");
        Creditos(viejo, nuevo, cancelado);
        EstadoDe(viejo,
            Cuota(1, Hoy.AddDays(-40), EstadosDeCuota.Pagada, 113m, []),
            Cuota(2, Hoy.AddDays(-10), EstadosDeCuota.Vencida, 113m,
                new() { ["Principal"] = 100m, ["CurrentInterest"] = 10m, ["DefaultInterest"] = 2m, ["Insurance"] = 3m, ["Administration"] = 4m, ["CollectionFee"] = 5m }),
            Cuota(3, Hoy.AddDays(20), EstadosDeCuota.PorVencer, 113m, new() { ["CurrentInterest"] = 4m }));
        EstadoDe(nuevo, Cuota(1, Hoy.AddDays(5), EstadosDeCuota.PorVencer, 60m, new() { ["Principal"] = 50m, ["Advance"] = 7m }));

        var r = await Handler().Handle(new GetPersonStatementSectionQuery(_ana.PublicId, SeccionesDelEstadoDeCuenta.Cuotas), default);

        r.IsSuccess.Should().BeTrue();
        var t = r.Value;
        t.Titulo.Should().Be("Cuotas pendientes de todas las obligaciones");
        t.Subtitulo.Should().Contain("ANA PÉREZ").And.Contain("52123456").And.Contain("16/11/2026");
        t.SinOcultas().Columnas.Select(c => c.Nombre).Should()
            .Equal("Crédito", "Línea", "Cuota", "Vence", "Estado", "Capital", "Interés", "Seguro", "Administración", "Otros", "Valor cuota", "Debe");
        t.Columnas.Single(c => c.Nombre == "Debe").Tipo.Should().Be(TipoDeColumna.Moneda);

        t.Filas.Should().HaveCount(3, "la pagada no sale y el crédito cancelado no se consulta");
        t.Filas.Select(f => (string?)f.Valores[0]).Should().Equal("CR-000002", "CR-000003", "CR-000002");
        // 2026-11-16 (feature 024, fase 2): la administración en su columna, aparte del seguro y de los otros.
        t.Filas[0].Valores.Take(12).Should().Equal(["CR-000002", "CONSUMO · Consumo", 2, Hoy.AddDays(-10), "Vencida", 100m, 12m, 3m, 4m, 5m, 113m, 124m]);
        t.Filas[0].Resaltada.Should().BeTrue();
        t.Filas[1].Valores.Take(12).Should().Equal(["CR-000003", "CONSUMO · Consumo", 1, Hoy.AddDays(5), "Por vencer", 50m, 0m, 0m, 0m, 0m, 60m, 50m],
            "el anticipo no se cobra en la cuota");
        t.Filas[2].Valores[4].Should().Be("Por vencer");

        t.Totales.Should().NotBeNull();
        t.Totales!.Valores.Skip(5).Take(7).Should().Equal([150m, 16m, 3m, 4m, 5m, 286m, 178m]);
        await _sender.DidNotReceive().Send(Arg.Is<GetLoanStatementQuery>(q => q.LoanPublicId == cancelado.PublicId), Arg.Any<CancellationToken>());
        await _sender.Received().Send(Arg.Is<GetLoanStatementQuery>(q => q.LoanPublicId == viejo.PublicId && q.Date == Hoy), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_credito_que_no_se_puede_leer_queda_en_las_notas_y_no_tumba_la_seccion()
    {
        var credito = Credito("CR-000009");
        Creditos(credito);
        _sender.Send(Arg.Any<GetLoanStatementQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<LoanStatementDto>(new Error("NationalValues.Missing", "Falta la usura del día.")));

        var r = await Handler().Handle(new GetPersonStatementSectionQuery(_ana.PublicId, SeccionesDelEstadoDeCuenta.Cuotas, Hoy), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Filas.Should().BeEmpty();
        r.Value.Totales.Should().BeNull();
        r.Value.Notas.Should().Contain(n => n.Contains("CR-000009") && n.Contains("Falta la usura del día."));
    }

    [Theory]
    [InlineData("installments", true)]
    [InlineData("messages", true)]
    [InlineData("collection-actions", true)]
    [InlineData("applications", true)]
    [InlineData("novelties", true)]
    [InlineData("credit-limits", true)]
    [InlineData("Installments", false)]
    [InlineData("inventada", false)]
    [InlineData("", false)]
    public void El_validador_solo_admite_las_seis_secciones(string seccion, bool valida)
    {
        var r = new GetPersonStatementSectionQueryValidator().Validate(new GetPersonStatementSectionQuery(Guid.NewGuid(), seccion));

        r.IsValid.Should().Be(valida);
        if (!valida) r.Errors.Should().ContainSingle(e => e.PropertyName == nameof(GetPersonStatementSectionQuery.Section));
    }

    [Fact]
    public async Task Una_seccion_que_no_existe_se_rechaza_tambien_en_el_handler_sin_consultar_nada()
    {
        var r = await Handler().Handle(new GetPersonStatementSectionQuery(_ana.PublicId, "inventada"), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.PersonStatement.SectionInvalid");
        r.Error.Message.Should().Contain("installments");
        _sender.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Una_persona_que_no_existe_es_el_404_de_la_persona()
    {
        var r = await Handler().Handle(new GetPersonStatementSectionQuery(Guid.NewGuid(), SeccionesDelEstadoDeCuenta.Cuotas), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(LecturasDelEstadoDeCuenta.CodigoPersonaInexistente);
    }
}
