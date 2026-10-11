using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Disbursements.Files;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0424 (FR-047, T78, C10; contracts/adaptadores-y-archivos.md §6; data-model §7.4, §7.6, §7.7): el archivo plano de desembolsos
/// con el motor común. Las líneas salen de la copia sellada de cada orden (beneficiario, cuenta, neto y el número del crédito como concepto) y el
/// lote es el número del archivo; lo que no cabe queda en pendientes con su motivo; sólo entran órdenes emitidas para pagarse por archivo y sin
/// otro archivo vivo; la cuenta de origen llega por el puerto de Contabilidad y es del banco del formato; el adjunto se sube antes de guardar y
/// cada orden queda con la referencia del archivo; ningún mensaje sale (C10). Anular sólo antes del primer resultado de Tesorería, y libera las
/// órdenes, que siguen para archivo.
/// </summary>
public sealed class ArchivoDeDesembolsosTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<UploadAttachmentCommand> _subidas = [];
    private readonly Guid _adjunto = Guid.NewGuid();
    private Bank _otroBanco = null!;
    private Loan _credito = null!;
    private BankFileFormat _formato = null!;
    private CuentaBancariaDeOrigenDto _cuenta = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _d.Db.Companies.Add(new Company { Name = "Cooperativa de prueba", TaxId = "900123456", TaxIdCheckDigit = "7", CreatedBy = "test" });
        _otroBanco = new Bank { Name = "Otro banco", TransferCode = "0051", CreatedBy = "test" };
        _d.Db.Banks.Add(_otroBanco);
        _d.Db.SaveChanges();
        _credito = _d.O.Credito(_d.O.Ana, 3_000_000m, "CR-000077");
        _formato = Formato(nameof(BankFileScope.LoanDisbursement), banco: null);
        _cuenta = new CuentaBancariaDeOrigenDto(Guid.NewGuid(), "11100501", "Bancos - cuenta de ahorros", _d.Banco.PublicId, _d.Banco.Name, "4561237890", "0007");
        _d.Contabilidad.CuentasBancariasAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success<IReadOnlyList<CuentaBancariaDeOrigenDto>>(
                new[] { _cuenta }.Where(c => ci.Arg<Guid?>() is not { } b || c.BankPublicId == b).ToList())));
        _sender.Send(Arg.Do<UploadAttachmentCommand>(_subidas.Add), Arg.Any<CancellationToken>()).Returns(Result.Success(_adjunto));
        _d.G.ActuarComo(_d.Cajera);
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private PreparacionDeArchivoDeDesembolsos Preparacion() => new(_d.Db, new LoanDisbursementLines(_d.Db), _d.Contabilidad, _d.G.Alcance, _d.G.Reloj);
    private PreviewDisbursementFileQueryHandler VistaPrevia() => new(Preparacion());
    private GenerateLoanDisbursementFileCommandHandler Generar() =>
        new(_d.Db, Preparacion(), _d.Numerador(), _d.O.Cerrojo, _sender, _d.G.Actor, _d.G.Reloj);
    private VoidLoanDisbursementFileCommandHandler Anular() => new(_d.Db, _d.O.Cerrojo, _d.G.Alcance, _d.G.Auditoria, _d.G.Actor, _d.G.Reloj);

    private BankFileFormat Formato(string ambito, Bank? banco, string codigo = "CSV-DESEMBOLSOS")
    {
        var def = new BankFileFormatDefinition
        {
            Code = codigo, Name = "Desembolsos de crédito, CSV genérico", Scope = ambito, ValidFrom = new DateOnly(2026, 1, 1), Kind = nameof(BankFileKind.Delimited),
            Delimiter = ";", Encoding = "utf-8", AmountFormat = nameof(BankFileAmountFormat.Point2), FileName = "DESEMB{PaymentDate:yyyyMMdd}-{BatchReference}.csv",
            Uppercase = true, StripAccents = true,
        };
        def.Records.Header.Enabled = false;
        def.Records.Trailer.Enabled = false;
        def.Records.Detail.Fields =
        [
            new() { Order = 1, Name = "Tipo doc", Source = "PayeeDocumentType", Required = true },
            new() { Order = 2, Name = "Documento", Source = "PayeeDocument", Required = true },
            new() { Order = 3, Name = "Nombre", Source = "PayeeFullName", Required = true },
            new() { Order = 4, Name = "Banco ACH", Source = "PayeeBankCode", Required = true },
            new() { Order = 5, Name = "Tipo cta", Source = "PayeeAccountType", Required = true },
            new() { Order = 6, Name = "Cuenta", Source = "PayeeAccountNumber", Required = true },
            new() { Order = 7, Name = "Valor", Source = "Amount", Required = true },
            new() { Order = 8, Name = "Referencia", Source = "LoanNumber", Required = true },
            new() { Order = 9, Name = "Lote", Source = "BatchReference", Required = true },
        ];
        var formato = new BankFileFormat { CreatedBy = "test" };
        def.AplicarA(formato, banco?.Id);
        _d.Db.BankFileFormats.Add(formato);
        _d.Db.SaveChanges();
        return formato;
    }

    private DisbursementOrder Orden(string numero, decimal neto = 2_500_000m, Bank? banco = null, string? cuenta = "0123456789", bool porArchivo = true,
        DisbursementOrderStatus estado = DisbursementOrderStatus.Issued)
    {
        var o = DisbursementOrder.Emitir(_credito.Id, 900 + _d.Db.DisbursementOrders.Count(), DisbursementForm.Transfer, _d.O.Ana.Id, "CC", "52000001",
            "Ana Solicitante", (banco ?? _d.Banco).Id, 1, cuenta, neto, 0m, DesembolsoDePrueba.Hoy, DesembolsoDePrueba.Hoy.AddDays(3), porArchivo,
            _d.G.UtcNow, _d.Cajera.Id);
        o.Number = numero;
        o.Status = estado;
        _d.Db.DisbursementOrders.Add(o);
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
        return o;
    }

    private async Task<Result<DisbursementFileGeneratedDto>> GenerarAsync(IReadOnlyList<Guid>? ordenes = null, BankFileFormat? formato = null, Guid? cuenta = null)
    {
        var r = await Generar().Handle(new GenerateLoanDisbursementFileCommand((formato ?? _formato).PublicId, cuenta ?? _cuenta.AccountPublicId, ordenes,
            DesembolsoDePrueba.Hoy) { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();
        return r;
    }

    // -------------------------------------------------------------------------------------------------------- líneas --

    [Fact]
    public async Task Cada_linea_sale_de_la_copia_sellada_de_la_orden_con_el_numero_del_credito_y_el_lote_del_archivo()
    {
        var orden = Orden("OG-000001");
        var ana = await _d.Db.People.SingleAsync(p => p.Id == _d.O.Ana.Id);
        ana.FirstName = "Anita";
        await _d.Db.SaveChangesAsync();

        var r = await GenerarAsync([orden.PublicId]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var archivo = await _d.Db.DisbursementFiles.AsNoTracking().SingleAsync();
        var linea = await _d.Db.DisbursementFileLines.AsNoTracking().SingleAsync();
        linea.RecordText.Should().Be($"CC;52000001;ANA SOLICITANTE;0007;1;0123456789;2500000.00;CR-000077;{archivo.Number}");
        (linea.OrderId, linea.Amount, linea.AccountNumber, linea.DestinationBankId, linea.LineNumber).Should()
            .Be((orden.Id, 2_500_000m, "0123456789", _d.Banco.Id, 1));
        archivo.FileName.Should().Be($"DESEMB20261201-{archivo.Number}.csv");
        Encoding.UTF8.GetString(_subidas.Single().Content).Should().StartWith(linea.RecordText);
    }

    [Fact]
    public async Task La_vista_previa_deja_en_pendientes_con_su_motivo_lo_que_no_cabe()
    {
        var sinCodigo = new Bank { Name = "Banco sin ACH", CreatedBy = "test" };
        _d.Db.Banks.Add(sinCodigo);
        _d.Db.SaveChanges();
        var buena = Orden("OG-000010");
        var sinAch = Orden("OG-000011", banco: sinCodigo);
        var sinCuenta = Orden("OG-000012", cuenta: null);
        var enArchivo = Orden("OG-000013");
        var fila = await _d.Db.DisbursementOrders.SingleAsync(o => o.Id == enArchivo.Id);
        fila.CurrentDisbursementFileId = 999;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var r = await VistaPrevia().Handle(new PreviewDisbursementFileQuery(_formato.PublicId, _cuenta.AccountPublicId,
            [buena.PublicId, sinAch.PublicId, sinCuenta.PublicId, enArchivo.PublicId], DesembolsoDePrueba.Hoy), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Lines.Should().ContainSingle();
        r.Value.Total.Should().Be(2_500_000m);
        r.Value.Pending.Select(p => (p.OrderNumber, p.Reason)).Should().BeEquivalentTo(new[]
        {
            ("OG-000011", MotivosDePendienteDeArchivo.SinCodigoDeBanco),
            ("OG-000012", MotivosDePendienteDeArchivo.SinCuenta),
            ("OG-000013", MotivosDePendienteDeArchivo.EnArchivo),
        });
        (await _d.Db.DisbursementFiles.CountAsync()).Should().Be(0, "la vista previa no escribe");
    }

    [Fact]
    public async Task Un_campo_obligatorio_vacio_deja_la_orden_en_pendientes_con_el_nombre_del_campo()
    {
        var orden = Orden("OG-000020");
        var fila = await _d.Db.DisbursementOrders.SingleAsync(o => o.Id == orden.Id);
        fila.BeneficiaryTaxIdType = "";
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var r = await VistaPrevia().Handle(new PreviewDisbursementFileQuery(_formato.PublicId, _cuenta.AccountPublicId, [orden.PublicId], DesembolsoDePrueba.Hoy),
            default);

        var p = r.Value.Pending.Should().ContainSingle().Subject;
        (p.Reason, p.Detail).Should().Be((MotivosDePendienteDeArchivo.CampoVacio, "Sin dato para el campo requerido «Tipo doc» del formato."));
    }

    // ------------------------------------------------------------------------------------------------------- generar --

    [Fact]
    public async Task Solo_entran_ordenes_emitidas_para_archivo_y_sin_otro_archivo_vivo()
    {
        var buena = Orden("OG-000030");
        Orden("OG-000031", porArchivo: false);
        Orden("OG-000032", estado: DisbursementOrderStatus.Disbursed);

        var todas = await GenerarAsync();

        todas.Value.File.Orders.Should().Be(1);
        (await _d.Db.DisbursementFileLines.AsNoTracking().SingleAsync()).OrderId.Should().Be(buena.Id);

        var otraVez = await GenerarAsync([buena.PublicId]);
        otraVez.Error.Code.Should().Be(ErroresDeArchivoDeDesembolsos.CodigoOrdenEnArchivo);
        DesembolsoDePrueba.Datos(otraVez.Error).GetProperty("orders")[0].GetString().Should().Be("OG-000030");
    }

    [Fact]
    public async Task La_cuenta_de_origen_debe_ser_del_banco_del_formato()
    {
        var delOtroBanco = Formato(nameof(BankFileScope.LoanDisbursement), _otroBanco, "OTRO-BANCO");
        var orden = Orden("OG-000040");

        var r = await GenerarAsync([orden.PublicId], delOtroBanco);

        r.Error.Code.Should().Be(ErroresDeArchivoDeDesembolsos.CodigoBancoDistinto);
        (await _d.Db.DisbursementFiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_formato_de_otro_ambito_responde_ScopeMismatch()
    {
        var deNomina = Formato(nameof(BankFileScope.PayrollDisbursement), banco: null, "CSV-NOMINA");
        var orden = Orden("OG-000050");

        var r = await GenerarAsync([orden.PublicId], deNomina);

        r.Error.Code.Should().Be("Core.BankFileFormat.ScopeMismatch");
    }

    [Fact]
    public async Task El_adjunto_se_sube_antes_de_guardar_y_cada_orden_queda_con_la_referencia_del_archivo_sin_mensajes()
    {
        var a = Orden("OG-000060", 1_000_000m);
        var b = Orden("OG-000061", 2_000_000m);

        var r = await GenerarAsync([a.PublicId, b.PublicId]);

        var subida = _subidas.Should().ContainSingle().Subject;
        subida.OwnerEntityType.Should().Be(AdjuntosDeModulo.ArchivoDeDesembolsos);
        var archivo = await _d.Db.DisbursementFiles.AsNoTracking().SingleAsync();
        subida.OwnerEntityPublicId.Should().Be(archivo.PublicId);
        (archivo.FileAttachmentPublicId, archivo.LineCount, archivo.TotalAmount, archivo.BankId).Should().Be((_adjunto, 2, 3_000_000m, _d.Banco.Id));
        archivo.FileSha256.Should().Be(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(subida.Content)).ToLowerInvariant());
        (archivo.SourceAccountPublicId, archivo.SourceAccountNumber, archivo.FormatCode).Should().Be((_cuenta.AccountPublicId, "4561237890", "CSV-DESEMBOLSOS"));
        (await _d.Db.DisbursementOrders.AsNoTracking().Select(o => o.CurrentDisbursementFileId).ToListAsync()).Should().OnlyContain(id => id == archivo.Id);
        (await _d.Db.IntegrationMessages.CountAsync()).Should().Be(0, "el archivo no viaja en ningún mensaje (C10)");
        r.Value.File.Sha256.Should().Be(archivo.FileSha256);
    }

    // -------------------------------------------------------------------------------------------------------- anular --

    [Fact]
    public async Task Anular_antes_de_resultados_libera_las_ordenes_que_siguen_para_archivo()
    {
        var orden = Orden("OG-000070");
        var r = await GenerarAsync([orden.PublicId]);

        var anulado = await Anular().Handle(new VoidLoanDisbursementFileCommand(r.Value.File.PublicId, "Se generó con la fecha equivocada")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? anulado.Error.Message : string.Empty);
        anulado.Value.Status.Should().Be("Voided");
        var archivo = await _d.Db.DisbursementFiles.AsNoTracking().SingleAsync();
        (archivo.VoidReason, archivo.VoidedByUserId).Should().Be(("Se generó con la fecha equivocada", _d.Cajera.Id));
        var liberada = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();
        (liberada.CurrentDisbursementFileId, liberada.PaidByFile).Should().Be((null, true));
        (await GenerarAsync([orden.PublicId])).IsSuccess.Should().BeTrue("la orden liberada va a otro archivo");
    }

    [Fact]
    public async Task Con_un_resultado_de_Tesoreria_el_archivo_no_se_anula()
    {
        var orden = Orden("OG-000080");
        var r = await GenerarAsync([orden.PublicId]);
        _d.Db.DisbursementOrderResults.Add(new DisbursementOrderResult
        {
            OrderId = orden.Id, InboundMessageId = 1, Result = DisbursementOrderStatus.Disbursed, ResultDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow,
        });
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var anulado = await Anular().Handle(new VoidLoanDisbursementFileCommand(r.Value.File.PublicId, "tarde") { OperationKey = Guid.NewGuid() }, default);

        anulado.Error.Code.Should().Be(ErroresDeArchivoDeDesembolsos.CodigoConResultados);
        (await _d.Db.DisbursementFiles.AsNoTracking().SingleAsync()).VoidedAt.Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------------- formato --

    [Fact]
    public void El_validador_rechaza_en_LoanDisbursement_un_origen_fuera_de_su_ambito()
    {
        var def = BankFileFormatDefinition.Desde(_formato);
        def.Records.Detail.Fields.Add(new() { Order = 10, Name = "Fondo", Source = "FundNit" });

        var errores = BankFileFormatValidator.Validar(def);

        errores.Should().ContainSingle(e => e.Field == "Fondo" && e.Message.Contains("LoanDisbursement"));
        BankFileFormatValidator.Validar(BankFileFormatDefinition.Desde(_formato)).Should().BeEmpty();
        BankFileFormatValidator.OrigenesAdmitidos(BankFileScope.LoanDisbursement)!.Should().Contain(BankFieldSource.LoanNumber);
        BankFileFormatValidator.OrigenesAdmitidos(BankFileScope.PayrollDisbursement).Should().BeNull("los ámbitos de la 010 no se restringen");
    }
}
