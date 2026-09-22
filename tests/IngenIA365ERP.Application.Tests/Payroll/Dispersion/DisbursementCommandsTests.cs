using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Application.Payroll.Dispersion;
using IngenIA365ERP.Application.Payroll.Runs.ReversePayrollRun;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Payroll.Dispersion;

public class DisbursementCommandsTests
{
    [Fact]
    public async Task Generar_deja_dos_lineas_un_excluido_sin_cuenta_y_el_archivo_del_contrato()
    {
        var p = new DispersionDePrueba();

        var r = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.LineCount.Should().Be(2);
        r.Value.TotalAmount.Should().Be(1_754_952.22m);
        r.Value.Excluded.Should().ContainSingle().Which.Should().Match<DisbursementExcludedDto>(x => x.EmployeePublicId == p.Beatriz.PublicId && x.ReasonCode == "NoBankAccount");
        r.Value.FileName.Should().Be("DEMO20261215.txt");

        var archivo = await p.D.Db.BankDisbursementFiles.Include(a => a.Lines).SingleAsync();
        archivo.Status.Should().Be(BankDisbursementFileStatus.Generated);
        archivo.FormatCode.Should().Be("DEMO-ANCHOFIJO");
        archivo.LineCount.Should().Be(2);
        archivo.ExcludedCount.Should().Be(1);
        archivo.Lines.Select(l => l.LineNumber).Should().Equal(1, 2);
        archivo.Lines.Sum(l => l.Amount).Should().Be(archivo.TotalAmount);
        archivo.FileAttachmentPublicId.Should().NotBeNull();

        p.Subidas.Should().ContainSingle();
        var subida = p.Subidas[0];
        subida.OwnerEntityType.Should().Be("BankDisbursementFile");
        subida.OwnerEntityPublicId.Should().Be(archivo.PublicId);
        Encoding.ASCII.GetString(subida.Content).Should().Be(
            "108903000010000000123456789020261215PRIMA2026II \r\n" +
            "21000001234567890ANA MARIA LOPEZ PEREZ                   0052S00000009876543210000000112454750PRIMA 2026-II       \r\n" +
            "21000000987654321CARLOS RUIZ                             0007D00000001234567890000000063040472PRIMA 2026-II       \r\n" +
            "300000200000000175495222\r\n",
            "es la salida de contracts/archivos.md §2.2, byte a byte");
        archivo.FileSha256.Should().Be(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(subida.Content)).ToLowerInvariant());
    }

    [Fact]
    public async Task Solo_una_corrida_aprobada_se_dispersa()
    {
        var p = new DispersionDePrueba();
        p.Prima.Status = PayrollRunStatus.Draft;
        p.D.Db.SaveChanges();

        var r = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Disbursement.RunNotApproved");
        p.Subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task El_pagado_a_mano_y_el_que_ya_esta_en_otro_archivo_quedan_fuera()
    {
        var p = new DispersionDePrueba();
        var primero = await p.Generador().Handle(p.ComandoDePrima(empleados: [p.Ana.PublicId]), CancellationToken.None);
        primero.IsSuccess.Should().BeTrue(primero.Error.Message);
        var reCarlos = p.Prima.Employees.Single(x => x.EmployeeId == p.Carlos.Id);
        p.D.Db.PayrollPayments.Add(new Domain.Entities.Payroll.PayrollPayment { PayrollRunEmployeeId = reCarlos.Id, PaidAt = NominaTestData.Ahora, PaymentMethod = PayrollPaymentMethod.Cash, PaidBy = "x", CreatedBy = "test" });
        p.D.Db.SaveChanges();

        var segundo = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);

        segundo.IsFailure.Should().BeTrue("Ana ya está en un archivo, Carlos pagado a mano y Beatriz sin cuenta");
        segundo.Error.Code.Should().Be("Payroll.Disbursement.NothingToPay");
    }

    [Fact]
    public async Task Sin_formato_indicado_toma_el_vigente_a_la_fecha_de_pago_y_no_uno_futuro()
    {
        var p = new DispersionDePrueba();
        // El de demostración es genérico (sin banco) y vigente desde el 01-12-2026; uno nuevo desde mañana no toca el archivo de hoy.
        p.Formato.ValidTo = new DateOnly(2026, 12, 15);
        var futuro = FlatFileWriterTests.CargarDemoAnchoFijo();
        futuro.Code = "DEMO-FUTURO"; futuro.ValidFrom = new DateOnly(2026, 12, 16); futuro.CreatedBy = "test";
        p.D.Db.BankFileFormats.Add(futuro);
        p.D.Db.SaveChanges();

        var r = await p.Generador().Handle(p.ComandoDePrima(formato: Guid.Empty) with { FormatPublicId = null }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await p.D.Db.BankDisbursementFiles.SingleAsync()).FormatCode.Should().Be("DEMO-ANCHOFIJO");

        var manana = await p.Generador().Handle(new GenerateDisbursementFileCommand(p.Prima.PublicId, null, new DateOnly(2026, 12, 20), p.CuentaOrigen.PublicId, "X", [p.Beatriz.PublicId]), CancellationToken.None);
        manana.Error.Code.Should().Be("Payroll.Disbursement.NothingToPay", "a esa fecha rige DEMO-FUTURO, y Beatriz sigue sin cuenta");
    }

    [Fact]
    public async Task Marcar_enviado_deja_pagados_a_los_del_archivo_con_la_misma_referencia_y_bloquea_la_reversa()
    {
        var p = new DispersionDePrueba();
        var generado = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);
        generado.IsSuccess.Should().BeTrue(generado.Error.Message);

        var enviado = await p.Enviador().Handle(new MarkDisbursementSentCommand(generado.Value.FilePublicId, new DateTime(2026, 12, 15, 9, 0, 0), "AVV-778899"), CancellationToken.None);

        enviado.IsSuccess.Should().BeTrue(enviado.Error.Message);
        enviado.Value.MarkedPaid.Should().Be(2);
        enviado.Value.AlreadyMarked.Should().BeEmpty();
        var pagos = await p.D.Db.PayrollPayments.Where(x => !x.IsReverted).ToListAsync();
        pagos.Should().HaveCount(2);
        pagos.Should().OnlyContain(x => x.PaymentMethod == PayrollPaymentMethod.Transfer && x.Reference == "AVV-778899" && x.PaidAt == new DateTime(2026, 12, 15));
        var archivo = await p.D.Db.BankDisbursementFiles.Include(a => a.Lines).SingleAsync();
        archivo.Status.Should().Be(BankDisbursementFileStatus.Sent);
        archivo.BankReference.Should().Be("AVV-778899");
        archivo.Lines.Should().OnlyContain(l => l.PayrollPaymentId != null, "cada línea sabe qué marca dejó");
        pagos.Should().OnlyContain(x => x.BankDisbursementFileId == archivo.Id, "cada marca sabe de qué archivo vino");
        var beatriz = p.Prima.Employees.Single(x => x.EmployeeId == p.Beatriz.Id);
        pagos.Should().NotContain(x => x.PayrollRunEmployeeId == beatriz.Id, "la excluida sigue en pendientes");

        // Enviar dos veces no vale, y la prima ya no se reversa: hay pagos marcados.
        var otraVez = await p.Enviador().Handle(new MarkDisbursementSentCommand(generado.Value.FilePublicId, NominaTestData.Ahora, "X"), CancellationToken.None);
        otraVez.Error.Code.Should().Be("Payroll.Disbursement.NotGenerated");
        p.D.ConfigurarContabilidad();
        var reversa = await new ReverseServiceBonusCommandHandler(p.D.Flujo(p.D.User)).Handle(new ReverseServiceBonusCommand(p.Prima.PublicId, "prueba"), CancellationToken.None);
        reversa.IsFailure.Should().BeTrue();
        reversa.Error.Code.Should().Be("Payroll.PaymentBlocksReversal");
    }

    [Fact]
    public async Task Marcar_enviado_respeta_la_marca_manual_previa_y_lo_avisa()
    {
        var p = new DispersionDePrueba();
        var generado = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);
        var reAna = p.Prima.Employees.Single(x => x.EmployeeId == p.Ana.Id);
        p.D.Db.PayrollPayments.Add(new Domain.Entities.Payroll.PayrollPayment { PayrollRunEmployeeId = reAna.Id, PaidAt = NominaTestData.Ahora, PaymentMethod = PayrollPaymentMethod.Check, Reference = "CHQ-1", PaidBy = "x", CreatedBy = "test" });
        p.D.Db.SaveChanges();

        var enviado = await p.Enviador().Handle(new MarkDisbursementSentCommand(generado.Value.FilePublicId, NominaTestData.Ahora, "REF"), CancellationToken.None);

        enviado.IsSuccess.Should().BeTrue(enviado.Error.Message);
        enviado.Value.MarkedPaid.Should().Be(1);
        enviado.Value.AlreadyMarked.Should().Equal(p.Ana.PublicId);
        (await p.D.Db.PayrollPayments.SingleAsync(x => x.PayrollRunEmployeeId == reAna.Id)).Reference.Should().Be("CHQ-1", "la marca previa no se pisa");
    }

    [Fact]
    public async Task Anular_solo_un_generado_y_nada_queda_pagado()
    {
        var p = new DispersionDePrueba();
        var generado = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);

        var anulado = await p.Anulador().Handle(new CancelDisbursementFileCommand(generado.Value.FilePublicId, "Se generó con la fecha equivocada"), CancellationToken.None);

        anulado.IsSuccess.Should().BeTrue(anulado.Error.Message);
        var archivo = await p.D.Db.BankDisbursementFiles.SingleAsync();
        archivo.Status.Should().Be(BankDisbursementFileStatus.Voided);
        archivo.VoidReason.Should().Be("Se generó con la fecha equivocada");
        (await p.D.Db.PayrollPayments.CountAsync()).Should().Be(0);

        // Anulado, sus empleados vuelven a poder ir a otro archivo.
        var otro = await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None);
        otro.IsSuccess.Should().BeTrue(otro.Error.Message);
        otro.Value.LineCount.Should().Be(2);
        (await p.Anulador().Handle(new CancelDisbursementFileCommand(generado.Value.FilePublicId, "x"), CancellationToken.None)).Error.Code.Should().Be("Payroll.Disbursement.NotGenerated");
    }

    [Fact]
    public async Task Un_formato_que_ya_genero_archivos_no_cambia_de_estructura()
    {
        var p = new DispersionDePrueba();
        (await p.Generador().Handle(p.ComandoDePrima(), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var editor = new UpdateBankFileFormatCommandHandler(p.D.Db, p.D.Clock, p.D.User);

        var def = BankFileFormatDefinition.Desde(p.Formato);
        def.Records.Detail.Fields.RemoveAt(def.Records.Detail.Fields.Count - 1);
        var estructura = await editor.Handle(new UpdateBankFileFormatCommand(p.Formato.PublicId, def), CancellationToken.None);
        estructura.IsFailure.Should().BeTrue();
        estructura.Error.Code.Should().Be("Core.BankFileFormat.InUse");

        var soloNombre = BankFileFormatDefinition.Desde(p.Formato);
        soloNombre.Name = "Demostración (renombrada)"; soloNombre.ValidTo = new DateOnly(2027, 6, 30); soloNombre.Notes = "cerrada";
        var cabecera = await editor.Handle(new UpdateBankFileFormatCommand(p.Formato.PublicId, soloNombre), CancellationToken.None);
        cabecera.IsSuccess.Should().BeTrue(cabecera.Error.Message);
        var guardado = await p.D.Db.BankFileFormats.Include(f => f.Fields).SingleAsync(f => f.Id == p.Formato.Id);
        guardado.Name.Should().Be("Demostración (renombrada)");
        guardado.ValidTo.Should().Be(new DateOnly(2027, 6, 30));
        guardado.Fields.Count(f => !f.IsDeleted).Should().Be(17, "la estructura quedó intacta");
    }

    [Fact]
    public async Task Crear_un_formato_valida_el_codigo_duplicado_y_la_vigencia_solapada_del_mismo_banco()
    {
        var p = new DispersionDePrueba();
        var creador = new CreateBankFileFormatCommandHandler(p.D.Db, p.D.Clock, p.D.User);

        var duplicado = await creador.Handle(new CreateBankFileFormatCommand(BankFileFormatDefinition.Desde(p.Formato)), CancellationToken.None);
        duplicado.Error.Code.Should().Be("Core.BankFileFormat.CodeDuplicate");

        var avVillas = BankFileFormatDefinition.Desde(p.Formato);
        avVillas.Code = "AVVILLAS-PAGOS"; avVillas.BankPublicId = p.AvVillas.PublicId; avVillas.ValidFrom = new DateOnly(2026, 12, 1);
        (await creador.Handle(new CreateBankFileFormatCommand(avVillas), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var solapado = BankFileFormatDefinition.Desde(p.Formato);
        solapado.Code = "AVVILLAS-V2"; solapado.BankPublicId = p.AvVillas.PublicId; solapado.ValidFrom = new DateOnly(2027, 1, 1);
        var r = await creador.Handle(new CreateBankFileFormatCommand(solapado), CancellationToken.None);
        r.Error.Code.Should().Be("Core.BankFileFormat.Overlaps", "AVVILLAS-PAGOS sigue vigente sin fin");

        var invalido = BankFileFormatDefinition.Desde(p.Formato);
        invalido.Code = "MALO"; invalido.Records.Detail.Fields[0].Length = null;
        (await creador.Handle(new CreateBankFileFormatCommand(invalido), CancellationToken.None)).Error.Code.Should().Be("Core.BankFileFormat.Invalid");
    }

    [Fact]
    public async Task La_vista_previa_escribe_las_lineas_sin_persistir_y_con_un_formato_propuesto()
    {
        var p = new DispersionDePrueba();
        var def = BankFileFormatDefinition.Desde(p.Formato);
        def.Code = "PROPUESTO";
        var handler = new PreviewDisbursementFileCommandHandler(p.D.Db, p.Lineas, p.D.Clock);

        var r = await handler.Handle(new PreviewDisbursementFileCommand(p.Prima.PublicId, null, new DateOnly(2026, 12, 15), p.CuentaOrigen.PublicId, "PRIMA2026II", def), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Lines.Should().HaveCount(4, "cabecera, dos detalles y totales");
        r.Value.Lines[1].Should().StartWith("21000001234567890ANA MARIA LOPEZ PEREZ");
        r.Value.Excluded.Should().ContainSingle(x => x.ReasonCode == "NoBankAccount");
        (await p.D.Db.BankDisbursementFiles.CountAsync()).Should().Be(0);
        p.Subidas.Should().BeEmpty();
    }
}
