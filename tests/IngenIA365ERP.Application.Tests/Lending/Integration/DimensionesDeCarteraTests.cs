using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0629 (contabilidad.md §16.2; enmienda A1, C27, T62): <see cref="DimensionesDeCartera"/>, la implementación del puerto que
/// Contabilidad usa para validar, plantillar y medir la matriz. <c>CatalogoAsync</c> trae las líneas (también cerradas) con su modalidad, las
/// modalidades con sus sub-segmentos del <c>CatalogoDeModalidades</c> vigente, los conceptos, las clases de recaudador y los tipos de hecho;
/// <c>CombinacionesEnUsoAsync(fecha)</c> las combinaciones con saldo o movimiento a la fecha, sin alcance de oficinas.
/// </summary>
public class DimensionesDeCarteraTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    public async Task InitializeAsync() => _d = await DesembolsoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private DimensionesDeCartera Dimensiones() =>
        new(_d.Db, _d.O.L.Normativa(), new SaldosContablesDeCartera(_d.Db, _d.Contabilidad), _d.G.Reloj);

    [Fact]
    public void Implementa_el_puerto_con_sus_dos_metodos()
    {
        typeof(IDimensionesDeCartera).IsAssignableFrom(typeof(DimensionesDeCartera)).Should().BeTrue();
        typeof(IDimensionesDeCartera).GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal)
            .Should().Equal("CatalogoAsync", "CombinacionesEnUsoAsync");
    }

    [Fact]
    public async Task El_catalogo_trae_lineas_tambien_cerradas_modalidades_conceptos_clases_y_tipos_de_hecho()
    {
        var linea = await _d.Db.CreditLines.SingleAsync(l => l.Code == "CONLIB");
        linea.Cerrar(CreditLineClosedReason.Manual, "prueba", null, DateTime.UtcNow);
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var c = await Dimensiones().CatalogoAsync(default);

        var conlib = c.CreditLines.Should().ContainSingle(l => l.Code == "CONLIB").Subject;
        conlib.Active.Should().BeFalse("una línea cerrada sigue en el catálogo: sus créditos siguen vivos");
        conlib.ModalityCode.Should().NotBeNullOrEmpty();
        c.Modalities.Should().Contain(m => m.Code == conlib.ModalityCode);
        c.Modalities.Should().Contain(m => m.SubsegmentCodes.Count > 0, "las modalidades llevan sus sub-segmentos del catálogo vigente");
        c.Concepts.Should().BeEquivalentTo(Enum.GetNames<LoanConcept>());
        c.CollectorClasses.Should().BeEquivalentTo(Enum.GetNames<ClaseDeRecaudador>());
        c.FactTypes.Should().HaveCount(35).And.Contain(CreditoDesembolsadoV1.Type).And.NotContain(OrdenDeGiroEmitidaV1.Type);
    }

    [Fact]
    public async Task Las_combinaciones_en_uso_son_las_que_tienen_saldo_o_movimiento_a_la_fecha_sin_alcance_de_oficinas()
    {
        (await Dimensiones().CombinacionesEnUsoAsync(Hoy, default)).Should().BeEmpty("sin créditos no hay combinaciones");

        var s = await _d.AprobadaAsync();
        await _d.DesembolsadoAsync(_d.Entrada(s));
        var credito = await _d.Db.Loans.AsNoTracking().SingleAsync();
        var sucursal = await _d.Db.Branches.AsNoTracking().Where(b => b.Id == credito.BranchId).Select(b => b.PublicId).SingleAsync();

        var enUso = await Dimensiones().CombinacionesEnUsoAsync(Hoy, default);

        enUso.Should().ContainSingle(c => c.Role == "Capital").Which.Should().BeEquivalentTo(new
        {
            CreditLineCode = "CONLIB", RiskCategory = "A", ConceptCode = (string?)null, InGracePeriod = false, Restructured = false,
            BranchPublicId = sucursal, LastUsedAt = Hoy,
        });
        (await Dimensiones().CombinacionesEnUsoAsync(Hoy.AddDays(-1), default)).Should().BeEmpty("antes de la fecha contable del desembolso");
        enUso.Should().NotContain(c => c.Role == "DesembolsoPorGirar", "los puentes los mide la completitud aparte");
    }
}
