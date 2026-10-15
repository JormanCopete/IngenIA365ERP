using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common.BankFiles;

/// <summary>
/// Feature 013, K2, T0806 (T78; contracts/adaptadores-y-archivos.md §6.1): <see cref="FlatFileReader"/> es el inverso de
/// <see cref="FlatFileWriter"/>. Lo que el escritor pone en un archivo de ancho fijo o delimitado se lee igual —texto, entero, monto y fecha,
/// con el mapa de equivalencias deshecho—; la cabecera y los totales no son detalle; una línea que no tiene la forma del formato o un valor que
/// no es de su tipo es un error con su línea y su campo; los orígenes de vuelta de la pagaduría (<c>ResultCode</c>, <c>DeductedAmount</c>,
/// <c>DeductionDate</c>) salen tipados.
/// </summary>
public class FlatFileReaderTests
{
    private static BankFileFormat Formato(BankFileFormatDefinition def)
    {
        BankFileFormatValidator.Validar(def).Should().BeEmpty("el formato de la prueba es válido");
        var formato = new BankFileFormat();
        def.AplicarA(formato, bankId: null);
        return formato;
    }

    /// <summary>El archivo de ida de una pagaduría en ancho fijo: cabecera, detalle con mapa y centavos implícitos, y totales.</summary>
    private static BankFileFormatDefinition IdaAnchoFijo() => new()
    {
        Code = "PAG-IDA", Name = "Descuentos de prueba", Scope = nameof(BankFileScope.PayrollDeductionRequest), ValidFrom = new DateOnly(2026, 1, 1),
        Kind = nameof(BankFileKind.FixedWidth), Encoding = "us-ascii", LineEnding = "CRLF", AmountFormat = nameof(BankFileAmountFormat.ImplicitCents),
        FileName = "DESC{GenerationDate:yyyyMMdd}.txt",
        Records = new()
        {
            Header = new() { Enabled = true, Fields =
            [
                new() { Order = 1, Name = "Tipo", Source = "Constant", Value = "1", Length = 1 },
                new() { Order = 2, Name = "NIT", Source = "CompanyNit", Length = 10, Align = "Right", Pad = "0" },
            ] },
            Detail = new() { Enabled = true, Fields =
            [
                new() { Order = 1, Name = "Tipo", Source = "Constant", Value = "2", Length = 1 },
                new() { Order = 2, Name = "Documento", Source = "PayeeDocument", Length = 12, Align = "Right", Pad = "0", Required = true },
                new() { Order = 3, Name = "Empleado", Source = "PayerEmployeeId", Length = 10 },
                new() { Order = 4, Name = "Credito", Source = "LoanNumber", Length = 12, Required = true },
                new() { Order = 5, Name = "Cuota", Source = "InstallmentNumber", Length = 3, Align = "Right", Pad = "0" },
                new() { Order = 6, Name = "Vence", Source = "DueDate", Length = 8 },
                new() { Order = 7, Name = "Valor", Source = "Amount", Length = 13, Align = "Right", Pad = "0", Required = true },
                new() { Order = 8, Name = "Vencido", Source = "PastDueAmount", Length = 13, Align = "Right", Pad = "0" },
                new() { Order = 9, Name = "Novedad", Source = "NoveltyKind", Length = 1, Map = new() { ["New"] = "A", ["Change"] = "M", ["Cancellation"] = "R", ["Unchanged"] = "S" } },
                new() { Order = 10, Name = "Periodo", Source = "PeriodYear", Length = 4 },
                new() { Order = 11, Name = "Mes", Source = "PeriodMonth", Length = 2, Align = "Right", Pad = "0" },
                new() { Order = 12, Name = "Libranza", Source = "LibranzaDate", Length = 10, Format = "dd/MM/yyyy" },
            ] },
            Trailer = new() { Enabled = true, Fields =
            [
                new() { Order = 1, Name = "Tipo", Source = "Constant", Value = "3", Length = 1 },
                new() { Order = 2, Name = "Lineas", Source = "LineCount", Length = 6, Align = "Right", Pad = "0" },
                new() { Order = 3, Name = "Total", Source = "TotalAmount", Length = 15, Align = "Right", Pad = "0" },
            ] },
        },
    };

    private static BankFileLineValues Linea(string documento, string? empleado, string credito, int cuota, DateOnly vence, decimal valor, decimal vencido,
        string novedad, DateOnly libranza)
    {
        var l = new BankFileLineValues();
        l[BankFieldSource.PayeeDocument] = documento;
        l[BankFieldSource.PayerEmployeeId] = empleado;
        l[BankFieldSource.LoanNumber] = credito;
        l[BankFieldSource.InstallmentNumber] = cuota;
        l[BankFieldSource.DueDate] = vence;
        l[BankFieldSource.Amount] = valor;
        l[BankFieldSource.PastDueAmount] = vencido;
        l[BankFieldSource.NoveltyKind] = novedad;
        l[BankFieldSource.PeriodYear] = 2026;
        l[BankFieldSource.PeriodMonth] = 12;
        l[BankFieldSource.LibranzaDate] = libranza;
        return l;
    }

    private static readonly BankFileContext Contexto = new()
    {
        CompanyNit = "900123456", CompanyName = "Cooperativa de prueba", PaymentDate = new DateOnly(2026, 12, 15),
        GeneratedAt = new DateTime(2026, 11, 25, 9, 0, 0),
    };

    [Fact]
    public void Lo_escrito_en_ancho_fijo_se_lee_igual_y_la_cabecera_y_los_totales_no_son_detalle()
    {
        var formato = Formato(IdaAnchoFijo());
        var lineas = new[]
        {
            Linea("1234567", "E-001", "CR-000077", 3, new DateOnly(2026, 12, 15), 355_570.06m, 0m, "New", new DateOnly(2025, 3, 1)),
            Linea("987654321", null, "CR-000102", 12, new DateOnly(2026, 12, 15), 120_000m, 87_500.25m, "Change", new DateOnly(2024, 11, 30)),
        };
        var archivo = FlatFileWriter.Escribir(formato, Contexto, lineas);

        var leido = FlatFileReader.Leer(formato, archivo.Content);

        leido.IsValid.Should().BeTrue(string.Join("; ", leido.Errors.Select(e => e.Message)));
        leido.DetailLineCount.Should().Be(2);
        leido.Rows.Select(r => r.LineNumber).Should().Equal(2, 3);
        var primera = leido.Rows[0];
        primera.Texto(BankFieldSource.PayeeDocument).Should().Be("1234567");
        primera.Texto(BankFieldSource.PayerEmployeeId).Should().Be("E-001");
        primera.Texto(BankFieldSource.LoanNumber).Should().Be("CR-000077");
        primera[BankFieldSource.InstallmentNumber].Should().Be(3);
        primera.Fecha(BankFieldSource.DueDate).Should().Be(new DateOnly(2026, 12, 15));
        primera.Monto(BankFieldSource.Amount).Should().Be(355_570.06m);
        primera.Monto(BankFieldSource.PastDueAmount).Should().Be(0m);
        primera.Texto(BankFieldSource.NoveltyKind).Should().Be("New", "el mapa se deshace");
        primera[BankFieldSource.PeriodYear].Should().Be(2026);
        primera[BankFieldSource.PeriodMonth].Should().Be(12);
        primera.Fecha(BankFieldSource.LibranzaDate).Should().Be(new DateOnly(2025, 3, 1));
        var segunda = leido.Rows[1];
        segunda[BankFieldSource.PayerEmployeeId].Should().BeNull("un campo vacío es nulo");
        segunda.Monto(BankFieldSource.PastDueAmount).Should().Be(87_500.25m);
        segunda.Texto(BankFieldSource.NoveltyKind).Should().Be("Change");
        primera.Valores.Should().NotContainKey(BankFieldSource.Constant);
    }

    [Fact]
    public void Lo_escrito_delimitado_con_comillas_se_lee_igual()
    {
        var def = IdaAnchoFijo();
        def.Code = "PAG-IDA-CSV"; def.Kind = nameof(BankFileKind.Delimited); def.Delimiter = ";"; def.QuoteText = true; def.Encoding = "utf-8";
        def.AmountFormat = nameof(BankFileAmountFormat.Point2); def.LineEnding = "LF";
        foreach (var f in def.Records.Header.Fields.Concat(def.Records.Detail.Fields).Concat(def.Records.Trailer.Fields)) { f.Length = null; f.Pad = " "; }
        def.Records.Header.Enabled = false; def.Records.Header.Fields = [];
        def.Records.Trailer.Enabled = false; def.Records.Trailer.Fields = [];
        def.Uppercase = false;
        var formato = Formato(def);
        var lineas = new[] { Linea("52;123", "Ana \"la de nómina\"", "CR-1", 1, new DateOnly(2027, 1, 5), 1_500.5m, 0m, "Unchanged", new DateOnly(2026, 6, 1)) };

        var leido = FlatFileReader.Leer(formato, FlatFileWriter.Escribir(formato, Contexto, lineas).Content);

        leido.IsValid.Should().BeTrue(string.Join("; ", leido.Errors.Select(e => e.Message)));
        var fila = leido.Rows.Should().ContainSingle().Subject;
        fila.LineNumber.Should().Be(1, "sin cabecera la primera línea es detalle");
        fila.Texto(BankFieldSource.PayeeDocument).Should().Be("52;123", "el separador dentro de comillas no corta");
        fila.Texto(BankFieldSource.PayerEmployeeId).Should().Be("Ana \"la de nomina\"", "las comillas escapadas se leen; las tildes quitadas no vuelven");
        fila.Monto(BankFieldSource.Amount).Should().Be(1_500.50m);
        fila.Texto(BankFieldSource.NoveltyKind).Should().Be("Unchanged");
    }

    [Fact]
    public void Una_linea_corta_un_valor_mal_tipado_o_un_obligatorio_vacio_son_errores_con_su_linea_y_campo()
    {
        var formato = Formato(IdaAnchoFijo());
        var buena = FlatFileWriter.Escribir(formato, Contexto,
            [Linea("1", "E", "CR-1", 1, new DateOnly(2026, 12, 15), 10m, 0m, "New", new DateOnly(2025, 1, 1))]).Text.Split("\r\n");
        var detalle = buena[1];
        var corta = detalle[..20];
        var malMonto = detalle[..46] + "00000ABC00000" + detalle[59..];
        var sinCredito = detalle[..23] + new string(' ', 12) + detalle[35..];
        var texto = string.Join("\r\n", buena[0], detalle, corta, malMonto, sinCredito, buena[2]) + "\r\n";

        var leido = FlatFileReader.Leer(formato, Encoding.ASCII.GetBytes(texto));

        leido.IsValid.Should().BeFalse();
        leido.DetailLineCount.Should().Be(4);
        leido.Rows.Select(r => r.LineNumber).Should().Equal(2);
        leido.Errors.Should().ContainEquivalentOf(new { LineNumber = 3, Field = (string?)null });
        leido.Errors.Should().Contain(e => e.LineNumber == 4 && e.Field == "Valor" && e.Message.Contains("monto"));
        leido.Errors.Should().Contain(e => e.LineNumber == 5 && e.Field == "Credito" && e.Message.Contains("obligatorio"));
    }

    [Fact]
    public void Los_origenes_de_vuelta_de_la_pagaduria_salen_tipados()
    {
        var def = new BankFileFormatDefinition
        {
            Code = "PAG-VUELTA", Name = "Respuesta de prueba", Scope = nameof(BankFileScope.PayrollDeductionResult), ValidFrom = new DateOnly(2026, 1, 1),
            Kind = nameof(BankFileKind.Delimited), Delimiter = ",", Encoding = "utf-8", LineEnding = "CRLF", AmountFormat = nameof(BankFileAmountFormat.Integer),
            FileName = "RESP.csv",
            Records = new()
            {
                Header = new() { Enabled = false },
                Detail = new() { Enabled = true, Fields =
                [
                    new() { Order = 1, Name = "Documento", Source = "PayeeDocument" },
                    new() { Order = 2, Name = "Credito", Source = "LoanNumber", Required = true },
                    new() { Order = 3, Name = "Codigo", Source = "ResultCode", Required = true },
                    new() { Order = 4, Name = "Descontado", Source = "DeductedAmount" },
                    new() { Order = 5, Name = "Fecha", Source = "DeductionDate", Format = "yyyy-MM-dd" },
                ] },
                Trailer = new() { Enabled = false },
            },
        };
        var formato = Formato(def);
        var archivo = "1234567,CR-000077,00,355570,2026-12-05\r\n987654321,CR-000102,R03,0,\r\n";

        var leido = FlatFileReader.Leer(formato, Encoding.UTF8.GetBytes(archivo));

        leido.IsValid.Should().BeTrue(string.Join("; ", leido.Errors.Select(e => e.Message)));
        leido.Rows.Should().HaveCount(2);
        leido.Rows[0][BankFieldSource.ResultCode].Should().BeOfType<string>().Which.Should().Be("00");
        leido.Rows[0][BankFieldSource.DeductedAmount].Should().BeOfType<decimal>().Which.Should().Be(355_570m);
        leido.Rows[0][BankFieldSource.DeductionDate].Should().BeOfType<DateOnly>().Which.Should().Be(new DateOnly(2026, 12, 5));
        leido.Rows[1].Texto(BankFieldSource.ResultCode).Should().Be("R03");
        leido.Rows[1].Monto(BankFieldSource.DeductedAmount).Should().Be(0m);
        leido.Rows[1].Fecha(BankFieldSource.DeductionDate).Should().BeNull();

        var mala = FlatFileReader.Leer(formato, Encoding.UTF8.GetBytes("1,CR-1,00,10,05/12/2026\r\n1,CR-2,00\r\n"));
        mala.Errors.Should().Contain(e => e.LineNumber == 1 && e.Field == "Fecha");
        mala.Errors.Should().Contain(e => e.LineNumber == 2 && e.Message.Contains("3 campos"));
    }
}
