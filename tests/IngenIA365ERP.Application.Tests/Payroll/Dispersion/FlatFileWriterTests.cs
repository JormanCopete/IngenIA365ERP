using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Payroll.Dispersion;

/// <summary>
/// El escritor produce byte a byte la salida de <c>contracts/archivos.md</c> §2.2 con el formato
/// ficticio <c>DEMO-ANCHOFIJO</c> (feature 010, US8): cabecera de 48, detalle de 114 y totales de
/// 24 posiciones, relleno, mapa, centavos implícitos, mayúsculas sin tildes y CRLF.
/// </summary>
public class FlatFileWriterTests
{
    public static BankFileFormat CargarDemoAnchoFijo()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Payroll", "Dispersion", "Formatos", "demo-anchofijo.json");
        var def = BankFileFormatDefinition.Parse(File.ReadAllText(ruta))!;
        BankFileFormatValidator.Validar(def).Should().BeEmpty("el formato de demostración del contrato es válido");
        var formato = new BankFileFormat();
        def.AplicarA(formato, bankId: null);
        return formato;
    }

    public static BankFileContext ContextoDeLaPrima() => new()
    {
        CompanyNit = "890300001", CompanyNitDv = "7", CompanyName = "Cooperativa de prueba",
        SourceAccountNumber = "1234567890", SourceAccountType = "1", SourceBankCode = "0052",
        PaymentDate = new DateOnly(2026, 12, 15), GeneratedAt = new DateTime(2026, 12, 14, 10, 30, 0),
        Sequence = 1, BatchReference = "PRIMA2026II",
    };

    public static BankFileLineValues Linea(string tipoDoc, string doc, string nombre, string banco, int tipoCuenta, string cuenta, decimal valor, string concepto)
    {
        var l = new BankFileLineValues();
        l[BankFieldSource.PayeeDocumentType] = tipoDoc;
        l[BankFieldSource.PayeeDocument] = doc;
        l[BankFieldSource.PayeeFullName] = nombre;
        l[BankFieldSource.PayeeBankCode] = banco;
        l[BankFieldSource.PayeeAccountType] = tipoCuenta.ToString();
        l[BankFieldSource.PayeeAccountNumber] = cuenta;
        l[BankFieldSource.Amount] = valor;
        l[BankFieldSource.Concept] = concepto;
        return l;
    }

    [Fact]
    public void Escribe_byte_a_byte_la_salida_del_contrato_con_DEMO_ANCHOFIJO()
    {
        var formato = CargarDemoAnchoFijo();
        var lineas = new[]
        {
            Linea("CC", "1234567890", "Ana María López Pérez", "0052", 1, "9876543210", 1_124_547.50m, "PRIMA 2026-II"),
            Linea("CC", "987654321", "Carlos Ruiz", "0007", 2, "1234567890", 630_404.72m, "PRIMA 2026-II"),
        };

        var salida = FlatFileWriter.Escribir(formato, ContextoDeLaPrima(), lineas);

        var esperado =
            "108903000010000000123456789020261215PRIMA2026II \r\n" +
            "21000001234567890ANA MARIA LOPEZ PEREZ                   0052S00000009876543210000000112454750PRIMA 2026-II       \r\n" +
            "21000000987654321CARLOS RUIZ                             0007D00000001234567890000000063040472PRIMA 2026-II       \r\n" +
            "300000200000000175495222\r\n";
        Encoding.ASCII.GetString(salida.Content).Should().Be(esperado);
        salida.Content.Should().Equal(Encoding.ASCII.GetBytes(esperado), "la codificación es us-ascii y el fin de línea CRLF");
        salida.DetailTexts.Select(l => l.Length).Should().Equal(114, 114);
        salida.LineCount.Should().Be(2);
        salida.TotalAmount.Should().Be(1_754_952.22m);
        salida.FileName.Should().Be("DEMO20261215.txt");
        salida.Sha256.Should().HaveLength(64);
    }

    [Fact]
    public void Un_texto_que_no_cabe_y_no_se_recorta_rechaza_la_linea_con_su_campo()
    {
        var formato = CargarDemoAnchoFijo();
        var nombre = formato.Fields.Single(f => f.Record == BankFileRecord.Detail && f.Order == 4);
        nombre.Truncate = false;
        var lineas = new[] { Linea("CC", "1", "Un nombre larguísimo que supera de sobra las cuarenta posiciones del campo", "0052", 1, "1", 10m, "X") };

        var acto = () => FlatFileWriter.Escribir(formato, ContextoDeLaPrima(), lineas);

        acto.Should().Throw<BankFileWriteException>().Which.Should().Match<BankFileWriteException>(e => e.LineNumber == 1 && e.Field == "Nombre");
    }

    [Fact]
    public void Un_numero_que_no_cabe_nunca_se_recorta()
    {
        var formato = CargarDemoAnchoFijo();
        var lineas = new[] { Linea("CC", "1", "Ana", "0052", 1, "1", 12_345_678_901_234m, "X") }; // 16 dígitos en centavos > 15

        var acto = () => FlatFileWriter.Escribir(formato, ContextoDeLaPrima(), lineas);

        acto.Should().Throw<BankFileWriteException>().Which.Field.Should().Be("Valor");
    }

    [Fact]
    public void Los_requeridos_vacios_se_nombran_para_excluir_la_linea_con_motivo()
    {
        var formato = CargarDemoAnchoFijo();
        var sinCuenta = Linea("CC", "55", "Beatriz", "", 1, "", 10m, "X");

        FlatFileWriter.RequeridosVacios(formato, sinCuenta, ContextoDeLaPrima()).Should().Equal("Banco destino", "Cuenta");
        FlatFileWriter.RequeridosVacios(formato, Linea("CC", "55", "Beatriz", "0052", 1, "9", 10m, "X"), ContextoDeLaPrima()).Should().BeEmpty();
    }

    [Fact]
    public void Delimitado_escribe_separador_sin_relleno_y_con_el_formato_de_montos_del_formato()
    {
        var def = BankFileFormatDefinition.Parse("""
            { "code": "CSV-PRUEBA", "name": "csv", "kind": "Delimited", "delimiter": ";", "encoding": "utf-8", "lineEnding": "LF",
              "amountFormat": "Point2", "fileName": "PAGO{PaymentDate:yyyyMMdd}_{Sequence:000}.csv", "validFrom": "2026-01-01",
              "records": {
                "header": { "enabled": false, "fields": [] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Documento", "source": "PayeeDocument" },
                  { "order": 2, "name": "Nombre", "source": "PayeeFullName", "length": 5 },
                  { "order": 3, "name": "Valor", "source": "Amount" },
                  { "order": 4, "name": "Fecha", "source": "PaymentDate", "format": "dd/MM/yyyy" } ] },
                "trailer": { "enabled": true, "fields": [
                  { "order": 1, "name": "Marca", "source": "Constant", "value": "T" },
                  { "order": 2, "name": "Total", "source": "TotalAmount" } ] } } }
            """)!;
        BankFileFormatValidator.Validar(def).Should().BeEmpty();
        var formato = new BankFileFormat(); def.AplicarA(formato, null);

        var salida = FlatFileWriter.Escribir(formato, ContextoDeLaPrima(), [Linea("CC", "77", "Ñandú Élite", "0052", 1, "1", 1_250_000m, "X")]);

        Encoding.UTF8.GetString(salida.Content).Should().Be("77;NANDU;1250000.00;15/12/2026\nT;1250000.00\n");
        salida.FileName.Should().Be("PAGO20261215_001.csv");
    }

    [Fact]
    public void El_validador_nombra_registro_orden_y_campo_de_cada_regla()
    {
        var def = BankFileFormatDefinition.Parse("""
            { "code": "mal codigo", "name": "", "kind": "FixedWidth", "encoding": "ebcdic", "lineEnding": "CR", "amountFormat": "Integer",
              "fileName": "X{Inventado}.txt", "validFrom": "2026-01-01",
              "records": {
                "header": { "enabled": true, "fields": [
                  { "order": 1, "name": "Neto en cabecera", "source": "NetAmount", "length": 5 },
                  { "order": 3, "name": "Salto", "source": "Constant", "value": "", "length": 1 } ] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Sin largo", "source": "PayeeDocument" },
                  { "order": 2, "name": "Total en detalle", "source": "TotalAmount", "length": 5 },
                  { "order": 3, "name": "Mapa en monto", "source": "Amount", "length": 5, "map": { "1": "2" } } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """)!;

        var errores = BankFileFormatValidator.Validar(def);

        errores.Should().Contain(e => e.Field == "code").And.Contain(e => e.Field == "name").And.Contain(e => e.Field == "encoding")
            .And.Contain(e => e.Field == "lineEnding").And.Contain(e => e.Field == "fileName");
        errores.Should().Contain(e => e.Record == "header" && e.Order == 1 && e.Message.Contains("sólo va en el detalle"));
        errores.Should().Contain(e => e.Record == "header" && e.Message.Contains("sin huecos"));
        errores.Should().Contain(e => e.Record == "header" && e.Order == 3 && e.Message.Contains("constante"));
        errores.Should().Contain(e => e.Record == "detail" && e.Order == 1 && e.Message.Contains("largo"));
        errores.Should().Contain(e => e.Record == "detail" && e.Order == 2 && e.Message.Contains("no en el detalle"));
        errores.Should().Contain(e => e.Record == "detail" && e.Order == 3 && e.Message.Contains("texto"));
    }

    [Fact]
    public void La_definicion_va_y_vuelve_de_la_entidad_sin_perder_nada()
    {
        var formato = CargarDemoAnchoFijo();
        var def = BankFileFormatDefinition.Desde(formato);

        def.Records.Detail.Fields.Should().HaveCount(9);
        def.Records.Detail.Fields.Single(f => f.Order == 2).Map.Should().ContainKey("CC").WhoseValue.Should().Be("1");
        def.Records.Detail.Fields.Single(f => f.Order == 2).Source.Should().Be("PayeeDocumentType", "el origen se guarda con su nombre genérico");
        def.AmountFormat.Should().Be("ImplicitCents");
        def.LineEnding.Should().Be("CRLF");
    }
}
