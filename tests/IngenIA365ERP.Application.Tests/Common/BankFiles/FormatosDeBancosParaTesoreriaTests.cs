using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common.BankFiles;

/// <summary>
/// 2026-11-15 (pedido del dueño): los archivos de pagos de Tesorería de Bancolombia (PAB) y AV Villas, con el formato público, inactivos y
/// pendientes de validar. Se comprueba que son formatos válidos y que el archivo sale con las posiciones y las equivalencias esperadas.
/// </summary>
public class FormatosDeBancosParaTesoreriaTests
{
    private static BankFileFormat Formato(BankFileFormatDefinition def)
    {
        BankFileFormatValidator.Validar(def).Should().BeEmpty($"el formato {def.Code} es válido");
        var formato = new BankFileFormat();
        def.AplicarA(formato, bankId: 7);
        return formato;
    }

    private static BankFileLineValues Pago(string tipoDoc, string doc, string tipoCuenta, decimal valor)
    {
        var l = new BankFileLineValues();
        l[BankFieldSource.PayeeDocumentType] = tipoDoc;
        l[BankFieldSource.PayeeDocument] = doc;
        l[BankFieldSource.PayeeFullName] = "Ana Pérez";
        l[BankFieldSource.PayeeBankCode] = "1052";
        l[BankFieldSource.PayeeAccountType] = tipoCuenta;
        l[BankFieldSource.PayeeAccountNumber] = "123456789";
        l[BankFieldSource.Amount] = valor;
        l[BankFieldSource.Concept] = "OP-000001";
        return l;
    }

    private static readonly BankFileContext Contexto = new()
    {
        CompanyNit = "900123456", CompanyName = "Cooperativa", PaymentDate = new DateOnly(2026, 12, 2), SourceAccountNumber = "12345678901",
        SourceAccountType = "1", GeneratedAt = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc), Sequence = 1, BatchReference = "LT-000001",
    };

    [Fact]
    public void Los_dos_son_de_tesoreria_inactivos_y_pendientes_de_validar()
    {
        FormatosDeBancosParaTesoreria.Todos.Select(t => t.Codigo).Should().Equal("BANCOLOMBIA-PAB", "AVVILLAS-PAGOS");
        foreach (var (_, _, definicion) in FormatosDeBancosParaTesoreria.Todos)
        {
            var f = Formato(definicion());
            f.Scope.Should().Be(BankFileScope.TreasuryPayments);
            f.IsActive.Should().BeFalse("no se usan hasta probarlos con el banco");
            f.Notes.Should().StartWith("PENDIENTE DE VALIDAR");
        }
    }

    [Fact]
    public void El_pab_de_bancolombia_sale_en_ancho_fijo_con_control_y_detalle()
    {
        var salida = FlatFileWriter.Escribir(Formato(FormatosDeBancosParaTesoreria.DefinicionBancolombia()), Contexto,
            [Pago("N", "800111222", "Ahorros", 250000m), Pago("C", "1010", "Corriente", 1500.50m)]);

        salida.FileName.Should().Be("PAB2026120201.txt");
        var lineas = salida.Text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lineas.Should().HaveCount(3);
        var control = lineas[0];
        control.Should().HaveLength(1 + 15 + 1 + 15 + 3 + 10 + 8 + 2 + 8 + 6 + 17 + 17 + 11 + 1 + 149);
        control.Should().StartWith("1000000900123456I");
        control.Substring(32, 3).Should().Be("220");
        control.Substring(45, 8).Should().Be("20261201");
        control.Substring(53, 2).Should().Be("01");
        control.Substring(55, 8).Should().Be("20261202");
        control.Substring(63, 6).Should().Be("000002");
        control.Substring(86, 17).Should().Be("00000000025150050", "251.500,50 con dos decimales implícitos");
        control.Substring(103, 11).Should().Be("12345678901");
        control.Substring(114, 1).Should().Be("S", "la cuenta de origen es de ahorros");

        var detalle = lineas[1];
        detalle.Should().HaveLength(1 + 15 + 30 + 9 + 17 + 1 + 2 + 17 + 8 + 21 + 1 + 5 + 15 + 80 + 15 + 27);
        detalle[0].Should().Be('6');
        detalle.Substring(1, 15).TrimEnd().Should().Be("800111222");
        detalle.Substring(16, 30).TrimEnd().Should().Be("ANA PEREZ");
        detalle.Substring(46, 9).Should().Be("000001052");
        detalle.Substring(73, 2).Should().Be("37", "ahorros");
        detalle.Substring(75, 17).Should().Be("00000000025000000");
        detalle.Substring(121, 1).Should().Be("3", "NIT");
        lineas[2].Substring(73, 2).Should().Be("27", "corriente");
        lineas[2].Substring(121, 1).Should().Be("1", "cédula");
    }

    [Fact]
    public void El_de_av_villas_sale_delimitado_una_linea_por_pago()
    {
        var salida = FlatFileWriter.Escribir(Formato(FormatosDeBancosParaTesoreria.DefinicionAvVillas()), Contexto,
            [Pago("C", "1010", "Ahorros", 250000m)]);

        salida.FileName.Should().Be("PAGOS_AVVILLAS_20261202_001.txt");
        salida.DetailTexts.Single().Should().Be("CC;1010;ANA PEREZ;1052;AH;123456789;250000;OP-000001");
    }
}
