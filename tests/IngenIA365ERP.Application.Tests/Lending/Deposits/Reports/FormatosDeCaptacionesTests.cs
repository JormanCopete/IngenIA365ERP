using System.Runtime.CompilerServices;
using FluentAssertions;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Application.Lending.Regulatory.Captaciones;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Reports;

/// <summary>
/// Feature 014, US17 (T220, T223; contracts/plantillas-y-archivos.md §8): los siete layouts de captaciones embebidos —SICSES 9026, 9068, 9013, 169 y 9067;
/// exógena 1019 y 1020— cargan, son dato con todos sus campos <c>verified = false</c> y su fuente «por cotejar», se eligen por formato y fecha, y cada ruta
/// del layout la entrega la fila; los archivos dorados de cada formato se comparan <b>byte a byte</b> con <c>Casos/*.esperado.txt</c> (regenerables sólo a
/// propósito con <c>CARTERA_ESCRIBIR_ESPERADO=1</c>, molde de la 013); un tipo de inversión sin código y un documento sin equivalente de la DIAN bloquean.
/// </summary>
public class FormatosDeCaptacionesTests
{
    private static readonly IdentificacionDePersona Ana = new("C", "52123456");
    private static readonly IdentificacionDePersona Beto = new("C", "79876543");
    private static readonly IdentificacionDePersona Menor = new("T", "1023456789");
    private static readonly IdentificacionDePersona Empresa = new("N", "900123456");

    [Fact]
    public void Los_siete_layouts_de_captaciones_cargan_como_dato_sin_cotejar()
    {
        var catalogo = CatalogoDeLayoutsDeCartera.Embebido;
        catalogo.Formatos.Should().BeEquivalentTo(FormatosDeCaptaciones.Todos.Select(f => f.Codigo));
        foreach (var formato in FormatosDeCaptaciones.Todos)
        {
            var fin = formato.FinDelPeriodo(2026, formato.Anual ? null : 12);
            var l = catalogo.VigenteDelFormato(formato.Codigo, fin);
            l.Should().NotBeNull($"{formato.Codigo} tiene layout vigente al {fin}");
            l!.Kind.Should().Be(formato.Clase);
            l.FormatCode.Should().Be(formato.Codigo);
            l.BureauCode.Should().BeNull();
            l.Layout.Source.Should().Contain("por cotejar");
            l.Layout.UnverifiedFields.Should().Be(l.Layout.TotalFields, $"{l.Code}: ningún campo cotejado (T235)");
            l.Notes.Should().NotBeEmpty();
            l.Layout.Registro(FilasDeCaptaciones.Detalle).Should().NotBeNull();
        }
        catalogo.VigenteDelFormato(FormatosDeCaptaciones.Exogena1019, new DateOnly(2023, 12, 31)).Should().BeNull("la versión 9 rige desde el año gravable 2024");
        catalogo.VigenteDelFormato("sicses-otro", new DateOnly(2026, 12, 31)).Should().BeNull();
        catalogo.Vigente(RegulatoryFileKind.Sicses, null, new DateOnly(2026, 12, 31))!.Code.Should().Be("SICSES-CARTERA-v1",
            "los de captaciones no compiten con el de cartera");
    }

    [Fact]
    public void Un_layout_de_captaciones_sin_formato_o_con_central_no_carga()
    {
        const string campo = """{ "number": 1, "name": "A", "order": 1, "length": 5, "kind": "A", "format": "Text", "path": "X" }""";
        static string Json(string extra) => $$"""
            { "code": "X-v1", {{extra}} "version": "1", "source": "s", "validFrom": "2027-01-01", "fileKind": "Delimited", "delimiter": "|",
              "encoding": "utf-8", "lineEnding": "CRLF", "records": [ { "code": "Detail", "fields": [ {{campo}} ] } ] }
            """;
        var sinFormato = () => CatalogoDeLayoutsDeCartera.Cargar([("sicses-x-1-2027-01-01.json", Json("\"kind\": \"DepositsSicses\","))]);
        sinFormato.Should().Throw<FormatException>().WithMessage("*formatCode*");
        var prefijo = () => CatalogoDeLayoutsDeCartera.Cargar([("exogena-x-1-2027-01-01.json", Json("\"kind\": \"Exogenous\", \"formatCode\": \"sicses-x\","))]);
        prefijo.Should().Throw<FormatException>().WithMessage("*exogena-*");
        var central = () => CatalogoDeLayoutsDeCartera.Cargar([("sicses-x-1-2027-01-01.json",
            Json("\"kind\": \"DepositsSicses\", \"formatCode\": \"sicses-x\", \"bureauCode\": \"X\","))]);
        central.Should().Throw<FormatException>().WithMessage("*bureauCode*");
        var archivo = () => CatalogoDeLayoutsDeCartera.Cargar([("otro-1-2027-01-01.json", Json("\"kind\": \"DepositsSicses\", \"formatCode\": \"sicses-x\","))]);
        archivo.Should().Throw<FormatException>().WithMessage("*sicses-x-*");
        var cartera = () => CatalogoDeLayoutsDeCartera.Cargar([("sicses-cartera-9-2027-01-01.json",
            Json("\"kind\": \"Sicses\", \"formatCode\": \"sicses-cartera\","))]);
        cartera.Should().Throw<FormatException>().WithMessage("*no es de captaciones*");
        var bien = CatalogoDeLayoutsDeCartera.Cargar([("sicses-x-1-2027-01-01.json", Json("\"kind\": \"DepositsSicses\", \"formatCode\": \"sicses-x\","))]);
        bien.VigenteDelFormato("SICSES-X", new DateOnly(2027, 1, 31))!.Code.Should().Be("X-v1");
    }

    [Theory]
    [InlineData(FormatosDeCaptaciones.Captaciones, "sicses-captaciones-202612")]
    [InlineData(FormatosDeCaptaciones.Cotitulares, "sicses-cotitulares-202612")]
    [InlineData(FormatosDeCaptaciones.Aportes, "sicses-aportes-202612")]
    [InlineData(FormatosDeCaptaciones.SaldosDiarios, "sicses-saldos-diarios-202612")]
    [InlineData(FormatosDeCaptaciones.FondoDeLiquidez, "sicses-fondo-liquidez-202612")]
    [InlineData(FormatosDeCaptaciones.Exogena1019, "exogena-1019-2026")]
    [InlineData(FormatosDeCaptaciones.Exogena1020, "exogena-1020-2026")]
    public void Cada_formato_es_su_dorado_y_cada_ruta_del_layout_sale_de_la_fila(string formato, string caso)
    {
        var f = FormatosDeCaptaciones.Buscar(formato)!;
        var layout = CatalogoDeLayoutsDeCartera.Embebido.VigenteDelFormato(formato, f.FinDelPeriodo(2026, f.Anual ? null : 12))!.Layout;
        var filas = Filas(formato);

        foreach (var fila in filas)
            foreach (var campo in layout.Registro(fila.Registro)!.Fields.Where(c => c.Path is not null))
                fila.Valores.Should().ContainKey(campo.Path!, $"{layout.Code}, campo {campo.Number} «{campo.Name}»: la fila entrega su ruta");
        var escritura = EscritorPorLayout.Escribir(layout, filas);

        escritura.Bloqueantes.Should().Be(0, string.Join(" | ", escritura.Inconsistencias.Select(i => i.Mensaje)));
        EscritorPorLayout.Escribir(layout, Filas(formato)).Sha256.Should().Be(escritura.Sha256, "las mismas filas, los mismos bytes");
        Comparar(caso, escritura.Contenido!);
    }

    [Fact]
    public void Una_inversion_sin_tipo_de_titulo_y_un_documento_sin_equivalente_de_la_DIAN_bloquean()
    {
        var fondo = Layout(FormatosDeCaptaciones.FondoDeLiquidez);
        var filas = FilasDeCaptaciones.FondoDeLiquidez(new EncabezadoDelFondoDeLiquidez(1m, 0m, 0m, 0m, false),
            [new TituloDelFondoDeLiquidez("860000001-1", "Banco", LiquidityHoldingKind.Other, "X-1", 10m, 0m, null, null, false)]);
        EscritorPorLayout.Escribir(fondo, filas).Inconsistencias.Should().Contain(i => i.Codigo == CodigosDeLayout.CampoObligatorioVacio && i.Campo == 3);

        var exogena = Layout(FormatosDeCaptaciones.Exogena1019);
        var rara = Cuenta1019(new IdentificacionDePersona("ZZ", "123"), "AH-9", true);
        EscritorPorLayout.Escribir(exogena, FilasDeCaptaciones.Exogena1019([rara])).Inconsistencias
            .Should().Contain(i => i.Codigo == CodigosDeLayout.CampoObligatorioVacio && i.Campo == 1);
    }

    [Fact]
    public void Las_traducciones_siguen_el_instructivo_y_la_tabla_de_la_DIAN()
    {
        TraduccionesDeCaptaciones.IdentificacionSicses("CC").Should().Be("C");
        TraduccionesDeCaptaciones.IdentificacionSicses("NIT").Should().Be("N");
        TraduccionesDeCaptaciones.IdentificacionSicses("RC").Should().Be("R");
        TraduccionesDeCaptaciones.IdentificacionSicses("XYZ").Should().Be("O");
        TraduccionesDeCaptaciones.IdentificacionAportes("TI").Should().Be("I", "en el 9013 la tarjeta de identidad es I");
        TraduccionesDeCaptaciones.DocumentoDian("C").Should().Be(13);
        TraduccionesDeCaptaciones.DocumentoDian("N").Should().Be(31);
        TraduccionesDeCaptaciones.DocumentoDian("CE").Should().Be(22);
        TraduccionesDeCaptaciones.DocumentoDian("PPT").Should().Be(48);
        TraduccionesDeCaptaciones.TipoDeAhorro(DepositClass.TermDeposit).Should().Be(2);
        TraduccionesDeCaptaciones.TipoDeCuenta(false, HandlingMode.Joint).Should().Be(1);
        TraduccionesDeCaptaciones.TipoDeCuenta(true, HandlingMode.Joint).Should().Be(2);
        TraduccionesDeCaptaciones.TipoDeCuenta(true, HandlingMode.Indistinct).Should().Be(3);
        TraduccionesDeCaptaciones.TituloDelFondo(LiquidityHoldingKind.BankDeposit).Should().Be(4);
    }

    // ---------------------------------------------------------------------------------------------------------- datos --

    private static LayoutDeArchivo Layout(string formato)
    {
        var f = FormatosDeCaptaciones.Buscar(formato)!;
        return CatalogoDeLayoutsDeCartera.Embebido.VigenteDelFormato(formato, f.FinDelPeriodo(2026, f.Anual ? null : 12))!.Layout;
    }

    private static IReadOnlyList<FilaDeLayout> Filas(string formato) => formato switch
    {
        FormatosDeCaptaciones.Captaciones => FilasDeCaptaciones.Captaciones(
        [
            new CuentaDelSicses
            {
                Numero = "AH-0000002", Titular = Ana, Clase = DepositClass.Demand, Linea = "Ahorro a la vista", Oficina = "001", Estado = MemberAccountStatus.Active,
                FechaDeApertura = new DateOnly(2025, 3, 10), TasaEfectivaAnual = 0.025m, Capital = 12_345_678.9m, InteresPorPagar = 1_234.5678m,
                FechaDelUltimoMovimiento = new DateOnly(2026, 12, 28), ExentaDeGmf = true, TieneCotitulares = true, Manejo = HandlingMode.Indistinct,
            },
            new CuentaDelSicses
            {
                Numero = "CD-0000001", Titular = Beto, Clase = DepositClass.TermDeposit, Linea = "CDAT", Oficina = "002", Estado = MemberAccountStatus.Active,
                FechaDeApertura = new DateOnly(2026, 10, 1), PlazoEnDias = 180, FechaDeVencimiento = new DateOnly(2027, 3, 30), PeriodicidadDeInteresesEnDias = 30,
                TasaEfectivaAnual = 0.1025m, Capital = 5_000_000m, InteresPorPagar = 40_123.45m, Embargada = true,
            },
            new CuentaDelSicses
            {
                Numero = "CT-0000003", Titular = Menor, Clase = DepositClass.Contractual, Linea = "Ahorro navideño", Oficina = "001",
                Estado = MemberAccountStatus.Inactive, FechaDeApertura = new DateOnly(2026, 1, 15), PlazoEnDias = 330, FechaDeVencimiento = new DateOnly(2026, 12, 11),
                TasaEfectivaAnual = 0.06m, Capital = 600_000m, ValorDeLaCuota = 50_000m, FechaDelUltimoMovimiento = new DateOnly(2026, 6, 15),
            },
            new CuentaDelSicses
            {
                Numero = "PE-0000004", Titular = Empresa, Clase = DepositClass.Permanent, Linea = "Ahorro permanente", Oficina = "001",
                Estado = MemberAccountStatus.Closing, FechaDeApertura = new DateOnly(2024, 2, 1), Capital = 1_000_000m,
            },
        ]),
        FormatosDeCaptaciones.Cotitulares => FilasDeCaptaciones.Cotitulares(
        [
            new CotitularDelSicses(Ana, Beto, DepositClass.Demand, "AH-0000002"),
            new CotitularDelSicses(Ana, Menor, DepositClass.Demand, "AH-0000002"),
            new CotitularDelSicses(Beto, Ana, DepositClass.TermDeposit, "CD-0000001"),
        ]),
        FormatosDeCaptaciones.Aportes => FilasDeCaptaciones.Aportes(
        [
            new AsociadoDelSicses
            {
                Asociado = Ana, Minimos = 100_000m, Ordinarios = 1_200_000m, Extraordinarios = 300_000m, Revalorizados = 45_678.9m, CuotaMensual = 50_000m,
                PromedioDelEjercicio = 1_498_765.4321m, UltimaFechaDePago = new DateOnly(2026, 12, 5),
            },
            new AsociadoDelSicses { Asociado = Menor, Minimos = 100_000m, CuotaMensual = 20_000m, PromedioDelEjercicio = 95_000m },
        ]),
        FormatosDeCaptaciones.SaldosDiarios => FilasDeCaptaciones.SaldosDiarios(
            EstadisticasDeSaldos.SerieDiaria([(new DateOnly(2026, 11, 30), 10_000_000m), (new DateOnly(2026, 12, 15), 2_500_000.5m),
                (new DateOnly(2026, 12, 20), -1_000_000m)], new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31))),
        FormatosDeCaptaciones.FondoDeLiquidez => FilasDeCaptaciones.FondoDeLiquidez(
            new EncabezadoDelFondoDeLiquidez(12_347_000.12m, 5_040_123.45m, 600_000m, 1_000_000m, false),
            [
                new TituloDelFondoDeLiquidez("860002964-4", "Banco de Bogotá", LiquidityHoldingKind.BankDeposit, "CTA-998877", 1_500_000m, 0m, null, null, false),
                new TituloDelFondoDeLiquidez("860034313-7", "Davivienda", LiquidityHoldingKind.TermInvestment, "CDT-445566", 500_000m, 0m, 90,
                    new DateOnly(2027, 2, 15), false),
            ]),
        FormatosDeCaptaciones.Exogena1019 => FilasDeCaptaciones.Exogena1019(
        [
            Cuenta1019(Ana, "AH-0000002", true),
            Cuenta1019(Beto, "AH-0000002", false),
            Cuenta1019(Empresa, "PE-0000004", true, DepositClass.Permanent),
        ]),
        _ => FilasDeCaptaciones.Exogena1020(
        [
            Titulo1020(Beto, 10, MovimientoDeCdatEnElMes.Emision, 0m, 5_000_000m, 13_500.4m, 0m, 0m, 5_000_000m),
            Titulo1020(Beto, 11, MovimientoDeCdatEnElMes.Vigente, 5_000_000m, 0m, 40_100m, 0m, 0m, 5_000_000m),
            Titulo1020(Beto, 12, MovimientoDeCdatEnElMes.Renovacion, 5_000_000m, 0m, 41_000m, 94_600.4m, 3_784m, 5_000_000m),
            Titulo1020(Ana, 12, MovimientoDeCdatEnElMes.Cancelacion, 2_000_000m, 0m, 1_500.5m, 30_000m, 1_200m, 0m),
        ]),
    };

    private static PersonaDeLaExogena Persona(IdentificacionDePersona id) => new()
    {
        Identificacion = id, EsJuridica = id.TipoDeDocumento == "N",
        PrimerApellido = id == Ana ? "Gómez" : "Pérez", SegundoApellido = id == Ana ? "Ruiz" : null, PrimerNombre = id == Ana ? "Ana" : "Alberto",
        OtrosNombres = id == Ana ? "María" : null, RazonSocial = id.TipoDeDocumento == "N" ? "Distribuidora Ñandú S.A.S." : null,
        Direccion = "Calle 10 # 20-30", Divipola = id == Beto ? null : "05001",
    };

    private static CuentaDeLaExogena1019 Cuenta1019(IdentificacionDePersona id, string numero, bool principal, DepositClass clase = DepositClass.Demand) => new()
    {
        Persona = Persona(id), Numero = numero, Clase = clase, EsTitularPrincipal = principal, ExentaDeGmf = clase == DepositClass.Demand,
        Saldos = EstadisticasDeSaldos.Resumir([10_000_000m, 12_500_000.5m, 11_500_000m]), SaldoFinal = 11_500_000m,
        Creditos = EstadisticasDeSaldos.Resumir([2_500_000.5m, 40_000m]), Debitos = EstadisticasDeSaldos.Resumir([1_000_000m]),
    };

    private static TituloDeLaExogena1020 Titulo1020(IdentificacionDePersona id, int mes, MovimientoDeCdatEnElMes movimiento, decimal inicial, decimal inversion,
        decimal causados, decimal pagados, decimal retencion, decimal final) => new()
    {
        Persona = Persona(id), Numero = id == Ana ? "CD-0000009" : "CD-0000001", Mes = mes, Movimiento = movimiento, SaldoInicial = inicial,
        ValorDeLaInversion = inversion, InteresesCausados = causados, InteresesPagados = pagados, Retencion = retencion, SaldoFinal = final,
    };

    private static void Comparar(string caso, byte[] contenido, [CallerFilePath] string fuente = "")
    {
        var ruta = Path.Combine(Path.GetDirectoryName(fuente)!, "Casos", caso + ".esperado.txt");
        // Regenerar a propósito (tras cotejar a mano): CARTERA_ESCRIBIR_ESPERADO=1 escribe el archivo junto al código fuente.
        if (Environment.GetEnvironmentVariable("CARTERA_ESCRIBIR_ESPERADO") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllBytes(ruta, contenido);
        }
        File.Exists(ruta).Should().BeTrue($"falta el archivo dorado {ruta}: regénerelo con CARTERA_ESCRIBIR_ESPERADO=1");
        contenido.Should().Equal(File.ReadAllBytes(ruta), $"el archivo debe ser byte a byte el de {Path.GetFileName(ruta)}");
    }
}
