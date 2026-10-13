using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Lending;
using NSubstitute;
using P = IngenIA365ERP.Application.Accounting.Lending.Reglas.PlantillaDeMatrizDeCartera;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0615 (C70; contracts/contabilidad.md §3.7, §4.5): la plantilla de la matriz de Cartera descargada trae las reglas vigentes y,
/// debajo, <b>filas sugeridas sin cuenta</b> por modalidad y categoría de las líneas activas, marcadas «pendiente de validar»; no se siembra
/// ninguna regla.
/// </summary>
public class GetLendingRulesTemplateQueryTests
{
    private static int Columna(string nombre) => P.Columnas.ToList().IndexOf(nombre);

    [Fact]
    public async Task Trae_filas_sugeridas_sin_cuenta_por_modalidad_y_categoria_de_las_lineas_activas()
    {
        var m = new MatrizDePruebaDeCartera();
        m.Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CatalogoDeDimensionesDeCarteraDto(
            [new("LIB01", "Libranza", "CONSUMO", "ConLibranza", true), new("VIE01", "Vieja", "COMERCIAL", null, false)],
            [new("CONSUMO", "Consumo", ["ConLibranza"]), new("COMERCIAL", "Comercial", [])],
            R.Conceptos, ["Receipts"], [])));
        m.D.Clock.HoyLocal.Returns(new DateOnly(2026, 11, 20));

        var r = await new GetLendingRulesTemplateQueryHandler(m.D.Db, m.Dimensiones, m.D.Clock).Handle(new GetLendingRulesTemplateQuery(), default);

        r.IsSuccess.Should().BeTrue();
        var filas = r.Value.De("Datos");
        filas.Should().NotBeEmpty();
        filas.Should().OnlyContain(f => f[Columna(P.Cuenta)] == null && (string?)f[Columna(P.Notas)] == P.PendienteDeValidar,
            "sin reglas, todo es sugerencia sin cuenta (C70)");
        filas.Should().OnlyContain(f => f.Count == P.Columnas.Count);
        filas.Where(f => (string?)f[Columna(P.Rol)] == R.Capital).Select(f => ((string?)f[Columna(P.Modalidad)], (string?)f[Columna(P.Categoria)]))
            .Should().BeEquivalentTo(new[] { ("CONSUMO", "A"), ("CONSUMO", "B"), ("CONSUMO", "C"), ("CONSUMO", "D"), ("CONSUMO", "E") },
                "por modalidad de las líneas activas y categoría: la comercial sólo tiene una línea cerrada");
        filas.Where(f => (string?)f[Columna(P.Rol)] == R.RecaudoPorAplicar).Select(f => (string?)f[Columna(P.ClaseDeRecaudador)])
            .Should().BeEquivalentTo(["Receipts", "OwnLoanPayoff"], "OwnLoanPayoff siempre (T91)");
        filas.Should().OnlyContain(f => (string?)f[Columna(P.Operacion)] == "*");
        filas[0][Columna(P.VigenteDesde)].Should().Be(new DateOnly(2026, 11, 1));
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(m.D.Db.LendingPostingRules)).Should().Be(0, "no se siembra ninguna regla");
    }

    [Fact]
    public async Task Las_reglas_vigentes_van_con_su_cuenta_y_su_combinacion_ya_no_se_sugiere()
    {
        var m = new MatrizDePruebaDeCartera();
        m.Regla(R.Capital, m.CapitalA, modalidad: "CONSUMO", categoria: "A", empleado: true);
        m.Regla(R.Capital, m.CapitalConsumoA, modalidad: "CONSUMO", categoria: "A");
        m.Regla(R.Capital, m.CapitalLinea, modalidad: "CONSUMO", categoria: "B", hasta: new DateOnly(2026, 1, 31));
        m.D.Clock.HoyLocal.Returns(new DateOnly(2026, 3, 20));

        var filas = (await new GetLendingRulesTemplateQueryHandler(m.D.Db, m.Dimensiones, m.D.Clock).Handle(new GetLendingRulesTemplateQuery(), default))
            .Value.De("Datos");

        var conCuenta = filas.Where(f => f[Columna(P.Cuenta)] is not null).ToList();
        conCuenta.Should().HaveCount(2, "la regla cerrada en enero ya no rige el 20 de marzo");
        conCuenta.Select(f => (string?)f[Columna(P.Empleado)]).Should().BeEquivalentTo(["S", null]);
        filas.Count(f => (string?)f[Columna(P.Rol)] == R.Capital && (string?)f[Columna(P.Categoria)] == "A").Should().Be(2,
            "la combinación capital de consumo A ya tiene regla y no se vuelve a sugerir");
        filas.Should().Contain(f => (string?)f[Columna(P.Rol)] == R.Capital && (string?)f[Columna(P.Categoria)] == "B" && f[Columna(P.Cuenta)] == null,
            "la de B se cerró y vuelve a sugerirse");
    }

    [Fact]
    public void Las_sugeridas_son_de_roles_y_conceptos_del_catalogo_y_pasan_la_forma()
    {
        var sugeridas = GetLendingRulesTemplateQueryHandler.Sugeridas(new CatalogoDeDimensionesDeCarteraDto(
            [new("LIB01", "Libranza", "CONSUMO", "ConLibranza", true)], [new("CONSUMO", "Consumo", ["ConLibranza"])], R.Conceptos, ["Payroll"], []));

        foreach (var s in sugeridas)
        {
            var presentes = ReglasDeLaMatrizDeCartera.Presentes(null, null, s.Modalidad, null, s.Categoria, s.Concepto, null, null, null, null, s.Clase, s.Motivo, false);
            ReglasDeLaMatrizDeCartera.Forma(null, s.Rol, s.Motivo, presentes).Should().BeEmpty($"{s.Rol} {s.Modalidad} {s.Categoria} {s.Concepto} {s.Motivo}");
        }
        sugeridas.Select(s => s.Clave).Should().OnlyHaveUniqueItems();
    }
}
