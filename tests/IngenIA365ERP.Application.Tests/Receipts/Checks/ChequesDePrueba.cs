using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Checks;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Checks;

/// <summary>
/// El escenario de las pruebas de la fase 13 (feature 015, US15, T0213–T0214) sobre <see cref="CajaConContratos"/>: el medio cheque <c>CH</c> ofrecido en el punto, un banco
/// librado, las causales de devolución y los reversos de los contratos simulados que anotan su orden. <c>Caja.AdmiteCheque</c> se enciende con <see cref="AdmitirCheques"/>
/// antes de abrir la sesión. Las pruebas se escriben ahora y se corren al final del plan.
/// </summary>
internal sealed class ChequesDePrueba
{
    public CajaConContratos K { get; } = new();
    public PaymentMeans Cheque { get; }
    public Bank Banco { get; }
    public List<string> Orden { get; } = [];

    public ChequesDePrueba()
    {
        var e = K.C.E;
        Cheque = e.Medio("CH", PaymentMeansClass.Check, 0);
        e.Ofrecer(K.C.Punto, Cheque);
        Banco = new Bank { Name = "Banco librado", TransferCode = "1051" };
        e.Db.Banks.Add(Banco);
        e.Db.CheckReturnCauses.AddRange(new CheckReturnCause { Code = "01", Description = "Fondos insuficientes" },
            new CheckReturnCause { Code = "05", Description = "Cheque mal girado", CountsForDrawerList = false });
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = Domain.Enums.Receipts.ReceiptDocumentKind.Voiding });
        e.Db.SaveChanges();
        K.Obligaciones.ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            Orden.Add("obligacion:" + ci.ArgAt<SolicitudDeReverso>(0).PaymentIdempotencyKey);
            return new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Applied, "OK", "", null);
        });
        K.Cuentas.ReversarAsync(Arg.Any<SolicitudDeReversoDeCuenta>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            Orden.Add("cuenta:" + ci.ArgAt<SolicitudDeReversoDeCuenta>(0).IdempotencyKey);
            return new RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>(ResultadoDelContrato.Applied, "OK", "", null);
        });
    }

    public void AdmitirCheques(bool deTercero = true)
    {
        K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaAdmiteCheque, "true");
        if (!deTercero) K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaAdmiteChequeDeTercero, "false");
    }

    /// <summary>Un medio cheque por <paramref name="valor"/>, fechado <paramref name="diasAtras"/> días antes de hoy.</summary>
    public MedioDeCajaDto Medio(decimal valor, int diasAtras = 0, string numero = "000123", string? girador = null, bool mismaPlaza = true) =>
        new(Cheque.PublicId, valor, Check: new ChequeDeCajaDto(Banco.PublicId, numero, "4410001", EscenarioDeRecaudo.Hoy.AddDays(-diasAtras), mismaPlaza,
            DrawerName: girador));

    public AuditoriaDeRecaudo Auditoria => new(K.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    public ReturnReceivedCheckCommandHandler Devolver() =>
        new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, K.C.Cerrojo, K.C.Numerador, K.Obligaciones, K.Cuentas, K.C.Emision, Auditoria, K.C.E.Alertas, K.C.E.Reloj);

    public TransicionesDeChequeHandler Transiciones() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, K.C.Cerrojo, Auditoria, K.C.E.Reloj);

    public RegisterCheckInCustodyCommandHandler Custodia() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, K.C.E.Parametros, Auditoria, K.C.E.Reloj);

    /// <summary>Un recibo confirmado con los ítems y los medios dados; devuelve el cheque que quedó.</summary>
    public async Task<ReceivedCheck> ReciboAsync(IReadOnlyList<ItemPedidoDto> items, IReadOnlyList<MedioDeCajaDto> medios)
    {
        await K.AbrirAsync();
        var op = await K.PreparadaAsync(items, medios);
        var r = await K.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        if (r.IsFailure) throw new InvalidOperationException(r.Error.Code);
        return K.C.E.Db.ReceivedChecks.Single();
    }
}
