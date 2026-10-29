using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.PaymentFiles;
using IngenIA365ERP.Application.Treasury.Results;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Results;

/// <summary>
/// Lo común de las pruebas de US11 (feature 015, T0152, T0153): un lote enviado con su archivo y una línea por orden, el formato genérico de respuesta sembrado y un
/// soporte del registro manual. Sobre <see cref="EscenarioDeTesoreria"/>. (nuevo)
/// </summary>
internal sealed class EscenarioDeResultados
{
    public EscenarioDeTesoreria T { get; } = new();
    public ICalendarioHabil Calendario { get; } = Substitute.For<ICalendarioHabil>();

    public AuditoriaDeTesoreria Auditoria => new(T.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    public EscenarioDeResultados()
    {
        var respuesta = new BankFileFormat();
        IngenIA365ERP.Application.Common.BankFiles.FormatosGenericosDeRecaudoYTesoreria.DefinicionDeRespuestaDePagos().AplicarA(respuesta, bankId: null);
        T.E.Db.BankFileFormats.Add(respuesta);
        var pagos = new BankFileFormat();
        IngenIA365ERP.Application.Common.BankFiles.FormatosGenericosDeRecaudoYTesoreria.DefinicionDePagos().AplicarA(pagos, bankId: null);
        T.E.Db.BankFileFormats.Add(pagos);
        T.E.Db.SaveChanges();
        FormatoDePagos = pagos;
        Calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new CalendarioHabil([], DiasDeDescanso.SabadoYDomingo));
        T.E.Como(T.E.Supervisor);
    }

    public BankFileFormat FormatoDePagos { get; }

    /// <summary>Un lote enviado de las órdenes, con su archivo enviado y una línea por orden (con la huella del documento, banco, cuenta y valor).</summary>
    public (PaymentBatch Lote, PaymentFile Archivo) LoteEnviado(params PaymentOrder[] ordenes)
    {
        var lote = new PaymentBatch
        {
            Number = $"LP-{T.E.Db.PaymentBatches.Count() + 1:0000}", BankAccountId = T.Pagos.Id, Form = PaymentBatchForm.File, BankFileFormatId = FormatoDePagos.Id,
            PreparedByUserId = T.E.Cajera.Id, PayDate = EscenarioDeRecaudo.Hoy, Status = PaymentBatchState.Sent, OrderCount = ordenes.Length,
            Total = ordenes.Sum(o => o.NetAmount), InputsHash = "x", SentAt = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc),
        };
        T.E.Db.PaymentBatches.Add(lote);
        T.E.Db.SaveChanges();
        var archivo = new PaymentFile
        {
            PaymentBatchId = lote.Id, Version = 1, BankFileFormatId = FormatoDePagos.Id, FileAttachmentPublicId = Guid.NewGuid(), FileName = "PAGOS.csv",
            Sha256 = new string('a', 64), LineCount = ordenes.Length, Total = lote.Total, GeneratedByUserId = T.E.Cajera.Id, GeneratedAt = lote.SentAt!.Value,
            SentAt = lote.SentAt, SentByUserId = T.E.Cajera.Id, UploadedAtBankBy = "Carga", AuthorizedAtBankBy = "Autoriza",
        };
        T.E.Db.PaymentFiles.Add(archivo);
        T.E.Db.SaveChanges();
        var n = 0;
        foreach (var o in ordenes)
        {
            o.Status = PaymentOrderState.Sent;
            var item = new PaymentBatchItem { PaymentBatchId = lote.Id, PaymentOrderId = o.Id, Amount = o.NetAmount };
            T.E.Db.PaymentBatchItems.Add(item);
            T.E.Db.SaveChanges();
            T.E.Db.PaymentFileLines.Add(new PaymentFileLine
            {
                PaymentFileId = archivo.Id, PaymentBatchItemId = item.Id, LineNumber = ++n, BeneficiaryTaxId = o.BeneficiaryTaxId, BankCode = o.DestinationBankCode ?? "",
                AccountNumber = o.DestinationAccountNumber ?? "", Amount = o.NetAmount,
                LineSha256 = LineasDelArchivo.HuellaDeLinea(o.BeneficiaryTaxId, o.DestinationBankCode ?? "", o.DestinationAccountNumber ?? "", o.NetAmount),
                RecordText = "linea",
            });
        }
        T.E.Db.SaveChanges();
        return (lote, archivo);
    }

    /// <summary>Un soporte disponible del registro manual de la orden.</summary>
    public Guid Soporte(Guid dueno)
    {
        var a = new Attachment
        {
            OwnerEntityType = Application.Attachments.Common.AdjuntosDeModulo.SoporteDeResultadoDelGiro, OwnerEntityPublicId = dueno, FileName = "constancia.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('b', 64), StoragePath = "x", Format = FormatoDeAdjunto.Direct,
        };
        T.E.Db.Attachments.Add(a);
        T.E.Db.SaveChanges();
        return a.PublicId;
    }

    /// <summary>La respuesta genérica (CSV con «;»): línea, documento, cuenta, valor, resultado, causal y fecha.</summary>
    public static byte[] Respuesta(params string[] lineas) =>
        System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", new[] { "linea;documento;cuenta;valor;resultado;causal;fecha" }.Concat(lineas)) + "\r\n");

    public RegisterDisbursementResultCommandHandler Registrar() => new(T.E.Db, T.Alcance, T.E.Actor, T.Cerrojo, T.Segregacion, T.Aprobacion, T.Resultados, Auditoria);
}
