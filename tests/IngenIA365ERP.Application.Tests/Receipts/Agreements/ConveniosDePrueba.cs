using System.Text;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.Agreements;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Agreements;

/// <summary>
/// El escenario de las pruebas de la fase 38 (feature 015, R2, T0429–T0431) sobre <see cref="BancosDePrueba"/>: un formato de archivo de convenio delimitado
/// (fecha;valor;referencia;secuencia;documento;nombre), el convenio <c>CONV01</c> con referencia por producto y dígito módulo 10 sobre la cuenta <c>RECAUDO</c>, los
/// tipos de recibo de convenio y la búsqueda de Cartera que encuentra los dos créditos de la asociada por su número. Las pruebas se escriben ahora y se corren al final
/// del plan. (nuevo)
/// </summary>
internal sealed class ConveniosDePrueba
{
    public BancosDePrueba B { get; } = new();
    public ISender Sender { get; } = Substitute.For<ISender>();
    public ICuentasBancariasPropias Cuentas { get; } = Substitute.For<ICuentasBancariasPropias>();
    public BankFileFormat Formato { get; }
    public CollectionAgreement Convenio { get; }
    public CollectionAgreementVersion Version { get; }
    public const string NumeroA = "100234";
    public const string NumeroB = "100235";

    public ConveniosDePrueba()
    {
        var e = B.K.C.E;
        Formato = new BankFileFormat();
        BankFileFormatDefinition.Parse("""
            { "code": "CONV-PRUEBA", "name": "Convenio de prueba", "scope": "CollectionAgreementFile", "validFrom": "2026-01-01", "kind": "Delimited",
              "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF", "uppercase": false, "stripAccents": false, "amountFormat": "Point2",
              "fileName": "CONVENIO.csv", "contentType": "text/csv",
              "records": {
                "header": { "enabled": false, "fields": [] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Fecha", "source": "MovementDate", "format": "yyyy-MM-dd", "required": true },
                  { "order": 2, "name": "Valor", "source": "Amount", "required": true },
                  { "order": 3, "name": "Referencia", "source": "MovementReference" },
                  { "order": 4, "name": "Secuencia", "source": "BankSequence" },
                  { "order": 5, "name": "Documento", "source": "DepositorTaxId" },
                  { "order": 6, "name": "Nombre", "source": "DepositorName" } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """)!.AplicarA(Formato, bankId: null);
        e.Db.BankFileFormats.Add(Formato);
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RV", Name = "Recibo de convenio", Kind = ReceiptDocumentKind.AgreementReceipt });
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RL", Name = "Recibo en línea", Kind = ReceiptDocumentKind.OnlineReceipt });
        e.Db.SaveChanges();
        Convenio = new CollectionAgreement
        {
            Code = "CONV01", Name = "Convenio de prueba", Kind = CollectionAgreementKind.Bank, OwnBankAccountPublicId = B.Cuenta.PublicId, BranchId = e.OficinaA.Id,
        };
        e.Db.CollectionAgreements.Add(Convenio);
        e.Db.SaveChanges();
        Version = new CollectionAgreementVersion
        {
            CollectionAgreementId = Convenio.Id, ValidFrom = EscenarioDeRecaudo.Hoy.AddDays(-30), ReferenceMode = PaymentReferenceMode.ByProduct, ReferenceLength = 0,
            CheckDigit = CheckDigitAlgorithm.Modulo10, BankFileFormatId = Formato.Id,
        };
        e.Db.CollectionAgreementVersions.Add(Version);
        e.Db.SaveChanges();
        B.K.Busqueda.PorNumeroAsync(Arg.Any<BusquedaPorNumero>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<BusquedaPorNumero>().Number.TrimStart('0') switch
        {
            NumeroA => [new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, B.K.CreditoA, NumeroA, B.K.Asociado.PublicId, "Asociada", B.K.Asociado.TaxId, "Active")],
            NumeroB => [new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, B.K.CreditoB, NumeroB, B.K.Asociado.PublicId, "Asociada", B.K.Asociado.TaxId, "Active")],
            _ => [],
        });
        Sender.Send(Arg.Any<Application.Attachments.UploadAttachment.UploadAttachmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Application.Common.Models.Result.Success(Guid.NewGuid()));
    }

    /// <summary>La referencia del crédito con su dígito (la que imprime el extracto).</summary>
    public static string Referencia(string numero) => DigitoDeVerificacion.Componer(numero, CheckDigitAlgorithm.Modulo10);

    public static byte[] Archivo(params string[] lineas) => Encoding.UTF8.GetBytes(string.Join("\r\n", lineas));

    public ReferenciasDePagoDeRecaudo Referencias => new(B.K.C.E.Db, B.K.C.E.Parametros, B.K.Busqueda, B.K.C.E.Reloj);

    public LoadCollectionFileCommandHandler Cargar() => new(B.K.C.E.Db, B.K.C.E.Actor, B.K.C.E.Alcance, B.K.C.Cerrojo, B.K.C.Numerador, Referencias, B.K.Obligaciones,
        B.Aplicacion, B.Partidas, B.Auditoria, Sender, B.K.C.E.Reloj);

    public ReconcileCollectionFileCommandHandler Conciliar() =>
        new(B.K.C.E.Db, B.K.C.E.Actor, B.K.C.E.Alcance, B.K.C.Cerrojo, B.K.C.Numerador, B.Movimientos, B.K.C.E.Parametros, B.Emision, B.Auditoria, B.K.C.E.Reloj);

    public ApproveAgreementReconciliationCommandHandler Aprobar() => new(B.K.C.E.Db, B.K.C.E.Actor, B.K.C.E.Alcance, B.Emision, B.Auditoria, B.K.C.E.Reloj);
}
