using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Implantacion.Cotejo;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Implantacion.Cotejo;

/// <summary>El motor del cotejo con cifras de referencia y la lectura de su archivo (2026-11-28). (nuevo)</summary>
public class MotorDeCotejoTests
{
    private static readonly DefinicionDeCotejo Definicion = new("Prueba", "Cuenta", ["Nombre"],
        [new MedidaDeCotejo("Saldo", ClaseDeMedida.Moneda), new MedidaDeCotejo("Estado", ClaseDeMedida.Texto)], "Clase");

    private static PartidaDeCotejo P(string llave, decimal? saldo, string? estado = null, string grupo = "1 Activo") =>
        new(llave, llave, grupo, [null], [saldo, estado]);

    [Fact]
    public void Clasifica_cada_llave_en_cuadra_diferencia_y_de_un_solo_lado()
    {
        var r = MotorDeCotejo.Comparar(Definicion,
            [P("1105", 100m), P("1110", 50m), P("1305", 10m), P("1405", 0m)],
            [P("1105", 100m), P("1110", 40m), P("1205", 7m), P("1505", 0m)]);

        Estado(r, "1105").Should().Be(EstadoDeCotejo.Cuadra);
        Estado(r, "1110").Should().Be(EstadoDeCotejo.Diferencia);
        r.Partidas.Single(p => p.Llave == "1110").Diferencias[0].Should().Be(10m);
        Estado(r, "1305").Should().Be(EstadoDeCotejo.SoloEnElErp);
        Estado(r, "1205").Should().Be(EstadoDeCotejo.SoloEnLaReferencia);
        r.Partidas.Single(p => p.Llave == "1205").Diferencias[0].Should().Be(-7m, "la diferencia es ERP − referencia");
        Estado(r, "1405").Should().Be(EstadoDeCotejo.Cuadra, "un cero de un lado y una ausencia del otro dicen lo mismo");
        Estado(r, "1505").Should().Be(EstadoDeCotejo.Cuadra);
    }

    [Fact]
    public void Una_medida_que_la_referencia_no_trae_no_se_compara()
    {
        var r = MotorDeCotejo.Comparar(Definicion, [P("1", 5m, "Activo")], [P("1", 5m, null)]);
        Estado(r, "1").Should().Be(EstadoDeCotejo.Cuadra);
    }

    [Fact]
    public void Los_textos_se_comparan_sin_mayusculas_ni_tildes_y_la_tolerancia_cuadra_lo_menor()
    {
        var conTolerancia = Definicion with { Medidas = [new MedidaDeCotejo("Saldo", ClaseDeMedida.Moneda, 1m), new MedidaDeCotejo("Estado", ClaseDeMedida.Texto)] };
        var r = MotorDeCotejo.Comparar(conTolerancia, [P("1", 100.6m, "Categoría A")], [P("1", 100m, "categoria a")]);
        Estado(r, "1").Should().Be(EstadoDeCotejo.Cuadra);

        var r2 = MotorDeCotejo.Comparar(conTolerancia, [P("1", 102m, "A")], [P("1", 100m, "B")]);
        Estado(r2, "1").Should().Be(EstadoDeCotejo.Diferencia);
        r2.Partidas[0].Observacion.Should().Contain("saldo").And.Contain("estado");
    }

    [Fact]
    public void Una_llave_repetida_en_un_lado_es_un_defecto_del_que_llama()
    {
        var act = () => MotorDeCotejo.Comparar(Definicion, [P("1", 1m), P("1", 2m)], []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void El_resumen_suma_por_grupo_y_el_detalle_filtra_lo_que_no_cuadra()
    {
        var r = MotorDeCotejo.Comparar(Definicion,
            [P("1105", 100m), P("2105", 30m, grupo: "2 Pasivo")],
            [P("1105", 90m), P("2105", 30m, grupo: "2 Pasivo")]);

        var resumen = MotorDeCotejo.Resumen(r, "corte", []);
        resumen.Filas.Should().HaveCount(2);
        var activo = resumen.Filas.Single(f => (string?)f.Valores[0] == "1 Activo");
        activo.Valores[4].Should().Be(1, "una partida con diferencia");
        activo.Valores[7].Should().Be(100m);
        activo.Valores[8].Should().Be(90m);
        activo.Valores[9].Should().Be(10m);
        resumen.Columnas.Last().Clave.Should().Be(MotorDeCotejo.ClaveGrupo);

        MotorDeCotejo.Detalle(r, "corte", [], soloDiferencias: true, grupo: null).Filas.Should().ContainSingle();
        MotorDeCotejo.Detalle(r, "corte", [], soloDiferencias: false, grupo: "2 Pasivo").Filas.Should().ContainSingle()
            .Which.Valores[1].Should().Be("2105");
    }

    [Theory]
    [InlineData("1500000", 1500000)]
    [InlineData("1.500.000", 1500000)]
    [InlineData("1,500,000.50", 1500000.50)]
    [InlineData("1.500.000,50", 1500000.50)]
    [InlineData("150.000", 150000)]
    [InlineData("1234.5", 1234.5)]
    [InlineData("1234,56", 1234.56)]
    [InlineData("$ 2.000", 2000)]
    [InlineData("(350)", -350)]
    [InlineData("-350", -350)]
    [InlineData("350-", -350)]
    [InlineData("", 0)]
    public void Los_importes_se_leen_como_los_exportan_las_hojas(string texto, decimal esperado) =>
        LecturaDeReferencia.Numero(texto).Should().Be(esperado);

    [Fact]
    public void Un_importe_ilegible_es_nulo() => LecturaDeReferencia.Numero("doce").Should().BeNull();

    [Theory]
    [InlineData("900.123.456-7", "900123456")]
    [InlineData(" 0016123456 ", "16123456")]
    [InlineData("pa-ab123", "PAAB123")]
    public void Los_documentos_se_comparan_sin_puntos_ceros_ni_digito_de_verificacion(string texto, string esperado) =>
        LecturaDeReferencia.Documento(texto).Should().Be(esperado);

    [Fact]
    public void La_lectura_reconoce_alias_y_reporta_errores_por_fila()
    {
        var columnas = new[]
        {
            new ColumnaDeReferencia("Cuenta", true, ClaseDeColumnaDeReferencia.Texto, "", "", "Código"),
            new ColumnaDeReferencia("Saldo", true, ClaseDeColumnaDeReferencia.Importe, "", "", "Saldo final"),
            new ColumnaDeReferencia("Días", false, ClaseDeColumnaDeReferencia.Entero, "", ""),
        };
        var bien = new TablaLeida(["Código", "Saldo final"], [new FilaLeida(2, ["1105", "10"]), new FilaLeida(3, ["1110", "5,5"])], "xlsx");
        var r = LecturaDeReferencia.Leer(bien, columnas);
        r.IsSuccess.Should().BeTrue();
        r.Value.Trae("Días").Should().BeFalse();
        r.Value.Filas[1].Importe("Saldo").Should().Be(5.5m);

        var mal = new TablaLeida(["Cuenta", "Saldo", "Días"], [new FilaLeida(2, ["", "x", "1.5"])], "xlsx");
        var r2 = LecturaDeReferencia.Leer(mal, columnas);
        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be(LecturaDeReferencia.ArchivoInvalidoCodigo);

        var sinColumna = new TablaLeida(["Cuenta"], [new FilaLeida(2, ["1105"])], "xlsx");
        LecturaDeReferencia.Leer(sinColumna, columnas).Error.Code.Should().Be("Archivo.ColumnaFaltante");
    }

    private static EstadoDeCotejo Estado(ResultadoDeCotejo r, string llave) => r.Partidas.Single(p => p.Llave == llave).Estado;
}
