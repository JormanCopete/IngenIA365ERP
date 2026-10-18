using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Lines;

/// <summary>
/// Feature 014, T011 a T013 (research R01; data-model §1.1; FR-027, SC-019): la línea común tiene naturaleza y lo de crédito sólo opera la de
/// colocación. Una línea de captación o de aportes nunca admite créditos, la usura la ignora, no recibe una versión de crédito
/// (<c>Lending.CreditLine.NatureMismatch</c>) y las rutas de líneas de crédito no la leen, ni la cierran, ni la listan. Las líneas de la 013
/// nacen de colocación sin decir nada.
/// </summary>
public sealed class LaLineaDeCreditoExigeColocacionTests : IDisposable
{
    private const string NaturalezaDistinta = "Lending.CreditLine.NatureMismatch";

    private readonly LineasDePrueba _l = new();

    public LaLineaDeCreditoExigeColocacionTests() => _l.NormativaAsync(0.25m).GetAwaiter().GetResult();

    public void Dispose() => _l.Dispose();

    private async Task<CreditLine> LineaDeAsync(string codigo, LineNature naturaleza)
    {
        var linea = CreditLine.Nueva(codigo, $"Línea {codigo}", null, naturaleza);
        linea.Activar();
        _l.Db.CreditLines.Add(linea);
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        return linea;
    }

    [Fact]
    public void Una_linea_sin_naturaleza_es_de_colocacion_como_las_de_la_013()
    {
        var linea = CreditLine.Nueva("conlib", "Consumo libre", null);

        linea.Nature.Should().Be(LineNature.Placement);
        linea.EsDeColocacion.Should().BeTrue();
        linea.ValidarNaturaleza(LineNature.Placement).Should().BeNull();
    }

    [Theory]
    [InlineData(LineNature.Deposit)]
    [InlineData(LineNature.Contribution)]
    public void Una_linea_de_captacion_o_de_aportes_activa_no_admite_creditos_y_la_usura_la_ignora(LineNature naturaleza)
    {
        var linea = CreditLine.Nueva("AHO1", "Ahorro", null, naturaleza);
        linea.Activar();

        linea.Status.Should().Be(CreditLineStatus.Active);
        linea.AdmiteCreditosNuevos.Should().BeFalse("sólo la colocación admite créditos (FR-027)");
        linea.BloquearPorUsura(LineasDePrueba.Hoy, Guid.NewGuid()).Should().BeFalse("la usura es sólo de la colocación");
        linea.UsuryBlockedSince.Should().BeNull();
        linea.DesbloquearPorUsura().Should().BeFalse();

        var problema = linea.ValidarNaturaleza(LineNature.Placement);
        problema.Should().NotBeNull();
        problema!.Codigo.Should().Be(NaturalezaDistinta);
        problema.Datos!["nature"].Should().Be(naturaleza.ToString());
    }

    [Fact]
    public async Task Una_version_de_credito_sobre_una_linea_de_captacion_es_NatureMismatch()
    {
        var linea = await LineaDeAsync("AHOVISTA", LineNature.Deposit);

        var r = await _l.Versionar().Handle(new AddCreditLineVersionCommand(linea.PublicId, LineasDePrueba.Version()) { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(NaturalezaDistinta);
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(0, "CreditLineVersion es sólo de colocación");
    }

    [Fact]
    public async Task El_registro_de_una_version_de_credito_rechaza_otra_naturaleza_aunque_lo_llamen_directo()
    {
        var linea = await LineaDeAsync("APORTES", LineNature.Contribution);
        var seguida = await _l.Db.CreditLines.FirstAsync(l => l.Id == linea.Id);

        var registrar = () => _l.Alta().RegistrarAsync(seguida, CreditLineVersionInput_Borrador(), default);

        (await registrar.Should().ThrowAsync<ReglaDeCarteraException>()).Which.Problema.Codigo.Should().Be(NaturalezaDistinta);
    }

    [Fact]
    public async Task Las_rutas_de_lineas_de_credito_no_leen_ni_cierran_una_de_aportes()
    {
        var linea = await LineaDeAsync("APORTES", LineNature.Contribution);

        (await _l.Leer().Handle(new GetCreditLineQuery(linea.PublicId), default)).Error.Code.Should().Be(NaturalezaDistinta);
        (await _l.Cerrar().Handle(new CloseCreditLineCommand(linea.PublicId, "No aplica") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(NaturalezaDistinta);
        (await _l.Db.CreditLines.SingleAsync()).Status.Should().Be(CreditLineStatus.Active, "la línea de aportes no se tocó");
    }

    [Fact]
    public async Task La_lista_de_lineas_de_credito_sólo_trae_las_de_colocacion()
    {
        await _l.LineaAsync("CONLIB");
        await LineaDeAsync("AHOVISTA", LineNature.Deposit);
        await LineaDeAsync("APORTES", LineNature.Contribution);

        var r = await _l.Listar().Handle(new ListCreditLinesQuery(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Select(l => l.Code).Should().Equal("CONLIB");
    }

    /// <summary>Un borrador de versión de crédito válido (el de la plantilla de pruebas).</summary>
    private static Domain.Lending.Configuration.BorradorDeVersionDeLinea CreditLineVersionInput_Borrador() =>
        LineasDePrueba.Version().Borrador(new Dictionary<Guid, int>());
}
