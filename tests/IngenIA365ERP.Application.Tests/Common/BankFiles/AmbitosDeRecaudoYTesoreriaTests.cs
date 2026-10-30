using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common.BankFiles;

/// <summary>
/// Feature 015, T0015 (R-20; FR-169; archivos-y-plantillas.md §1–§4): los ámbitos nuevos y el renombrado del motor común de archivos, y los
/// cuatro formatos genéricos sembrados. <see cref="FlatFileReader"/> lee el extracto (<c>BankStatement = 7</c>) con fecha, signo, referencia y
/// consignante, y la respuesta a pagos (<c>TreasuryPaymentResponse = 9</c>); <see cref="FlatFileWriter"/> escribe el archivo de un lote
/// (<c>TreasuryPayments = 3</c>) con el contexto genérico; dos formatos del mismo ámbito y vigencia chocan.
/// </summary>
public class AmbitosDeRecaudoYTesoreriaTests
{
    private static BankFileFormat Formato(BankFileFormatDefinition def)
    {
        BankFileFormatValidator.Validar(def).Should().BeEmpty($"el genérico {def.Code} es válido");
        var formato = new BankFileFormat();
        def.AplicarA(formato, bankId: null);
        return formato;
    }

    [Fact]
    public void Los_ambitos_conservan_sus_numeros_y_el_tres_se_renombra()
    {
        ((int)BankFileScope.TreasuryPayments).Should().Be(3);
        ((int)BankFileScope.BankStatement).Should().Be(7);
        ((int)BankFileScope.CollectionAgreementFile).Should().Be(8);
        ((int)BankFileScope.TreasuryPaymentResponse).Should().Be(9);
        ((int)BankFileScope.PaymentGatewayFile).Should().Be(10);
        ((int)BankFileScope.AcquirerSettlement).Should().Be(11);
        Enum.GetNames<BankFileScope>().Should().NotContain("SupplierPayments");
    }

    [Fact]
    public void Los_cuatro_genericos_son_validos_sin_banco_y_estan_pendientes_de_validar()
    {
        FormatosGenericosDeRecaudoYTesoreria.Todos.Select(t => t.Codigo).Should().Equal(
            "EXTRACTO-GENERICO", "PAGOS-GENERICO", "RESP-PAGOS-GENERICO", "RESP-PAGADURIA-GEN");
        var ambitos = new Dictionary<string, BankFileScope>
        {
            ["EXTRACTO-GENERICO"] = BankFileScope.BankStatement,
            ["PAGOS-GENERICO"] = BankFileScope.TreasuryPayments,
            ["RESP-PAGOS-GENERICO"] = BankFileScope.TreasuryPaymentResponse,
            ["RESP-PAGADURIA-GEN"] = BankFileScope.PayrollDeductionResult,
        };
        foreach (var (codigo, definicion) in FormatosGenericosDeRecaudoYTesoreria.Todos)
        {
            var formato = Formato(definicion());
            formato.Code.Should().Be(codigo);
            formato.BankId.Should().BeNull();
            formato.Scope.Should().Be(ambitos[codigo]);
            formato.Notes.Should().Contain(FormatosGenericosDeRecaudoYTesoreria.PendienteDeValidar);
        }
    }

    [Fact]
    public void El_extracto_generico_lee_cinco_movimientos_con_fecha_signo_referencia_y_consignante()
    {
        var formato = Formato(FormatosGenericosDeRecaudoYTesoreria.DefinicionDelExtracto());
        var csv = string.Join("\r\n",
            "fecha;valor;referencia;documento;consignante;descripcion;saldo",
            "2026-12-01;150000.00;REF-1;1010;Ana Pérez;Consignación;1150000.00",
            "2026-12-01;-20000.50;;;;Comisión;1129999.50",
            "2026-12-02;300000;REF-3;2020;Luis Gómez;Transferencia;1429999.50",
            "2026-12-02;45000.00;REF-4;;;PSE;1474999.50",
            "2026-12-03;-100000.00;CH-77;;;Cheque pagado;1374999.50") + "\r\n";

        var leido = FlatFileReader.Leer(formato, Encoding.UTF8.GetBytes(csv));

        leido.IsValid.Should().BeTrue(string.Join("; ", leido.Errors.Select(e => e.Message)));
        leido.Rows.Should().HaveCount(5);
        leido.Rows[0].Fecha(BankFieldSource.MovementDate).Should().Be(new DateOnly(2026, 12, 1));
        leido.Rows[0].Monto(BankFieldSource.Amount).Should().Be(150000m);
        leido.Rows[0].Texto(BankFieldSource.MovementReference).Should().Be("REF-1");
        leido.Rows[0].Texto(BankFieldSource.DepositorName).Should().Be("Ana Pérez");
        leido.Rows[1].Monto(BankFieldSource.Amount).Should().Be(-20000.50m, "el signo dice el sentido: negativo es un cargo");
        leido.Rows[4].Monto(BankFieldSource.Balance).Should().Be(1374999.50m);
    }

    [Fact]
    public void El_archivo_de_pago_generico_se_escribe_con_el_contexto_comun()
    {
        var formato = Formato(FormatosGenericosDeRecaudoYTesoreria.DefinicionDePagos());
        var linea = new BankFileLineValues();
        linea[BankFieldSource.PayeeDocument] = "1010";
        linea[BankFieldSource.PayeeFullName] = "Ana Pérez";
        linea[BankFieldSource.PayeeBankCode] = "1052";
        linea[BankFieldSource.PayeeAccountType] = "Ahorros";
        linea[BankFieldSource.PayeeAccountNumber] = "123456789";
        linea[BankFieldSource.Amount] = 250000m;
        linea[BankFieldSource.Concept] = "OP-000001 devolución";
        var ctx = new BankFileContext
        {
            CompanyNit = "900123456", CompanyName = "Cooperativa", PaymentDate = new DateOnly(2026, 12, 2),
            GeneratedAt = new DateTime(2026, 12, 2, 10, 0, 0, DateTimeKind.Utc), Sequence = 1, BatchReference = "LT-000001",
        };

        var salida = FlatFileWriter.Escribir(formato, ctx, [linea]);

        salida.LineCount.Should().Be(1);
        salida.TotalAmount.Should().Be(250000m);
        salida.FileName.Should().Be("PAGOS_20261202_001.csv");
        salida.Text.ToLowerInvariant().Should().StartWith("documento;nombre;banco;tipo_cuenta;cuenta;valor;concepto");
        salida.DetailTexts.Single().Should().Be("1010;ANA PEREZ;1052;AHORROS;123456789;250000.00;OP-000001 DEVOLUCION");
    }

    [Fact]
    public void La_respuesta_generica_a_pagos_se_lee_con_su_resultado_y_causal()
    {
        var formato = Formato(FormatosGenericosDeRecaudoYTesoreria.DefinicionDeRespuestaDePagos());
        var csv = "linea;documento;cuenta;valor;resultado;causal;fecha\n1;1010;123456789;250000.00;Pagado;;2026-12-03\n2;2020;987;10000;Rechazado;Cuenta inexistente;2026-12-03\n";

        var leido = FlatFileReader.Leer(formato, Encoding.UTF8.GetBytes(csv));

        leido.IsValid.Should().BeTrue();
        leido.Rows.Should().HaveCount(2);
        leido.Rows[0].Texto(BankFieldSource.Outcome).Should().Be("Pagado");
        leido.Rows[1].Texto(BankFieldSource.BankCause).Should().Be("Cuenta inexistente");
        leido.Rows[1].Fecha(BankFieldSource.BankDate).Should().Be(new DateOnly(2026, 12, 3));
        leido.Rows[1].Entero(BankFieldSource.LineNumber).Should().Be(2);
    }

    [Fact]
    public void Un_origen_de_lectura_no_sirve_en_un_ambito_de_escritura_ajeno()
    {
        var def = FormatosGenericosDeRecaudoYTesoreria.DefinicionDelExtracto();
        def.Scope = nameof(BankFileScope.PayrollDisbursement);

        BankFileFormatValidator.Validar(def).Should().Contain(e => e.Message.Contains("origen de lectura", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dos_formatos_genericos_del_mismo_ambito_y_vigencia_se_cruzan()
    {
        var db = TestDbContextFactory.Create();
        var existente = new BankFileFormat();
        FormatosGenericosDeRecaudoYTesoreria.DefinicionDelExtracto().AplicarA(existente, bankId: null);
        db.BankFileFormats.Add(existente);
        await db.SaveChangesAsync();

        var solapado = await CreateBankFileFormatCommandHandler.SolapadoAsync(db, null, BankFileScope.BankStatement, new DateOnly(2026, 6, 1), null, null, CancellationToken.None);
        var otroAmbito = await CreateBankFileFormatCommandHandler.SolapadoAsync(db, null, BankFileScope.TreasuryPaymentResponse, new DateOnly(2026, 6, 1), null, null, CancellationToken.None);

        solapado.Should().NotBeNull("Core.BankFileFormat.Overlaps: dos extractos genéricos vigentes a la vez");
        otroAmbito.Should().BeNull();
    }
}
