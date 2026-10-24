using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.PaymentFiles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.PaymentFiles;

/// <summary>
/// Feature 015, US10, T0131 (R-20; FR-112): el archivo de pago. Se escribe con <c>FlatFileWriter</c> en el ámbito de pagos; el adjunto no borrable se sube antes de
/// guardar la fila; cada línea guarda su huella; marcarlo enviado anota quién lo cargó y quién lo autorizó en el banco y no emite hecho ni resultado; un archivo no
/// enviado se anula y sus órdenes vuelven a la bandeja; quien preparó no lo descarga. Prueba escrita, sin correr.
/// </summary>
public class ArchivoDePagoTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<string> _pasos = [];
    private PaymentBatch _lote = null!;
    private PaymentOrder _orden = null!;

    public ArchivoDePagoTests()
    {
        var generico = new BankFileFormat();
        FormatosGenericosDeRecaudoYTesoreria.DefinicionDePagos().AplicarA(generico, bankId: null);
        _t.E.Db.BankFileFormats.Add(generico);
        _t.E.Db.SaveChanges();
        _orden = _t.Orden(750_000m, PaymentOrderState.Approved);
        _lote = new PaymentBatch
        {
            Number = "LP-0001", BankAccountId = _t.Pagos.Id, Form = PaymentBatchForm.File, BankFileFormatId = generico.Id, PreparedByUserId = _t.E.Cajera.Id,
            PayDate = Receipts.EscenarioDeRecaudo.Hoy, Status = PaymentBatchState.Approved, OrderCount = 1, Total = 750_000m, InputsHash = "x",
        };
        _t.E.Db.PaymentBatches.Add(_lote);
        _t.E.Db.SaveChanges();
        _t.E.Db.PaymentBatchItems.Add(new PaymentBatchItem { PaymentBatchId = _lote.Id, PaymentOrderId = _orden.Id, Amount = 750_000m });
        _t.E.Db.SaveChanges();
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            _pasos.Add(_t.E.Db.PaymentFiles.Any() ? "fila-antes" : "adjunto");
            return Result.Success(Guid.NewGuid());
        });
    }

    private GeneratePaymentFileCommandHandler Generar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _sender,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    [Fact]
    public async Task Genera_el_archivo_con_el_adjunto_antes_de_la_fila_y_la_huella_por_linea()
    {
        var r = await Generar().Handle(new GeneratePaymentFileCommand(_lote.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        _pasos.Should().Equal("adjunto");
        r.Value.LineCount.Should().Be(1);
        r.Value.Total.Should().Be(750_000m);
        r.Value.Sha256.Should().HaveLength(64);
        var linea = _t.E.Db.PaymentFileLines.Single();
        linea.LineSha256.Should().Be(LineasDelArchivo.HuellaDeLinea("2001", "1007", "123456789", 750_000m));
        _lote.Status.Should().Be(PaymentBatchState.FileGenerated);
        await _sender.Received(1).Send(Arg.Is<UploadAttachmentCommand>(c => c.OwnerEntityType == "TreasuryPaymentFile"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_lote_no_aprobado_o_del_portal_no_genera_archivo()
    {
        _lote.Status = PaymentBatchState.Draft;
        _t.E.Db.SaveChanges();
        (await Generar().Handle(new GeneratePaymentFileCommand(_lote.PublicId) { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Treasury.Batch.NotApproved");
        _lote.Form = PaymentBatchForm.BankPortal;
        _t.E.Db.SaveChanges();
        (await Generar().Handle(new GeneratePaymentFileCommand(_lote.PublicId) { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Treasury.Batch.NoFileForm");
    }

    [Fact]
    public async Task Marcarlo_enviado_anota_quien_lo_cargo_y_autorizo_y_no_deja_nada_girado()
    {
        var archivo = (await Generar().Handle(new GeneratePaymentFileCommand(_lote.PublicId) { OperationKey = Guid.NewGuid() }, default)).Value;
        var r = await new MarkPaymentFileSentCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.E.Reloj)
            .Handle(new MarkPaymentFileSentCommand(archivo.PublicId, "Ana Banco", "Luis Banco") { OperationKey = Guid.NewGuid() }, default);

        r.Value.UploadedAtBankBy.Should().Be("Ana Banco");
        _lote.Status.Should().Be(PaymentBatchState.Sent);
        _orden.Status.Should().Be(PaymentOrderState.Sent, "girada sólo llega con la respuesta del banco (FR-119)");
        _t.E.Db.IntegrationMessages.Should().BeEmpty("marcar enviado no emite hecho ni resultado");
    }

    [Fact]
    public async Task Un_archivo_no_enviado_se_anula_y_sus_ordenes_vuelven_listas()
    {
        var archivo = (await Generar().Handle(new GeneratePaymentFileCommand(_lote.PublicId) { OperationKey = Guid.NewGuid() }, default)).Value;
        var r = await new VoidPaymentFileCommandHandler(_t.E.Db, _t.Alcance, _t.Cerrojo, _t.E.Reloj)
            .Handle(new VoidPaymentFileCommand(archivo.PublicId, "formato equivocado") { OperationKey = Guid.NewGuid() }, default);

        r.Value.VoidedAt.Should().NotBeNull();
        _lote.Status.Should().Be(PaymentBatchState.Voided);
        _orden.Status.Should().Be(PaymentOrderState.Ready);
        _t.E.Db.PaymentBatchItems.Single().IsLive.Should().BeFalse();
    }

    [Fact]
    public async Task Quien_preparo_el_lote_no_descarga_el_archivo()
    {
        var segregacion = new SegregacionDeTesoreria(new PersonasDeLosUsuarios(_t.E.Db));
        var r = await segregacion.DescargarArchivoAsync(_t.E.Cajera.Id, [], _t.E.CajeraBis.Id, default);
        r.Error.Code.Should().Be("Treasury.File.SameUserDownload");
        (await segregacion.DescargarArchivoAsync(_t.E.Cajera.Id, [_t.E.Supervisor.Id], _t.E.OtroCajero.Id, default)).IsSuccess.Should().BeTrue();
    }
}
