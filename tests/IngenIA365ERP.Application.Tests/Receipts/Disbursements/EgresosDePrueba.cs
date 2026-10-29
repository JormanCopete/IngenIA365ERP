using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Application.Receipts.Disbursements;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Disbursements;

/// <summary>
/// El arnés de los egresos de caja (feature 015, R1, fase 26): la caja con contratos (<see cref="CajaConContratos"/>, sesión de C1 con 500.000 de base), los tipos
/// de documento <c>EG</c> y <c>AN</c>, una cuenta de ahorro que Captaciones deja retirar (liquidando su GMF) y un crédito con saldo a favor que Cartera resuelve por el
/// recaudador. Los contratos son dobles: lo que se prueba es lo que hace Recaudo. (nuevo)
/// </summary>
internal sealed class EgresosDePrueba
{
    public CajaConContratos K { get; } = new();
    public Guid Ahorros { get; } = Guid.NewGuid();
    public Guid Credito { get; } = Guid.NewGuid();
    public decimal GmfDeCaptaciones { get; set; } = 480m;
    public decimal? AEntregarAlCancelar { get; set; } = 300_000m;

    /// <summary>Si no es nulo, Captaciones responde «pendiente de aprobación» con esta solicitud la primera vez.</summary>
    public Guid? AprobacionDeCaptaciones { get; set; }

    public EgresosDePrueba()
    {
        K.C.E.Db.ReceiptDocumentTypes.AddRange(
            new ReceiptDocumentType { Code = "EG", Name = "Egreso de caja", Kind = ReceiptDocumentKind.CashDisbursement },
            new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        K.C.E.Db.SaveChanges();
        K.Cuentas.SimularAsync(Arg.Any<SolicitudDeSimulacionDeCuenta>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacionDeCuenta>();
            return new RespuestaDelContratoDto<SimulacionDeCuentaDto>(ResultadoDelContrato.Answered, "OK", "", new SimulacionDeCuentaDto("sim-retiro", true,
                CajaConContratos.Disponible(), CajaConContratos.Disponible(), Gmf(s.Amount), [], [], null, false, [], [],
                s.Operation == "Close" ? AEntregarAlCancelar : null));
        });
        K.Cuentas.RetirarAsync(Arg.Any<SolicitudDeRetiro>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var r = ci.Arg<SolicitudDeRetiro>();
            if (AprobacionDeCaptaciones is { } a && r.ApprovalPublicId != a)
                return new RespuestaDelContratoDto<ComprobanteDeCuentaDto>(ResultadoDelContrato.Rejected,
                    CodigosDelContratoDeCuentas.PendienteDeAprobacion, "Sobre la atribución", null,
                    new Dictionary<string, object?> { ["approvalPublicId"] = a });
            return new RespuestaDelContratoDto<ComprobanteDeCuentaDto>(ResultadoDelContrato.Applied, "OK", "", new ComprobanteDeCuentaDto(Guid.NewGuid(), "RET-1",
                r.AccountPublicId, "AH-1", r.IdempotencyKey, r.CloseAccount ? "Close" : "Withdrawal", r.ValueDate, r.ValueDate, DateTime.UtcNow, r.Amount, [], Gmf(r.Amount), [], [],
                CajaConContratos.Disponible(), new AutorizacionDeEfectivoDto("AUT-1", r.Amount, "Ana Asociada", r.SignerPersonPublicIds), null, false, "h", "DEP-2026.1", []));
        });
        K.Cuentas.ReversarAsync(Arg.Any<SolicitudDeReversoDeCuenta>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>(ResultadoDelContrato.Applied, "OK", "", new ComprobanteDeReversoDeCuentaDto(Guid.NewGuid(), "REV-1",
                Guid.NewGuid(), "RET-1", [Ahorros], EscenarioDeRecaudo.Hoy, DateTime.UtcNow, 0m, [], false, [])));
        K.Obligaciones.ResolverSaldoAFavorAsync(Arg.Any<SolicitudDeResolucion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeResolucion>();
            return new RespuestaDelContratoDto<ComprobanteDeResolucionDto>(ResultadoDelContrato.Applied, "OK", "", new ComprobanteDeResolucionDto(Guid.NewGuid(), "RES-1",
                s.ObligationPublicId, s.Concept, s.Form, s.Amount, null, CajaConContratos.Saldos()));
        });
    }

    private GmfCalculadoDto Gmf(decimal valor) => new(valor, 0m, 0m, valor, 0.004m, GmfDeCaptaciones, "Liquidado", "El GMF del retiro");

    public AuditoriaDeRecaudo Auditoria => new(K.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    public RegistroDeEfectivo Registro => new(K.C.E.Db, K.C.E.Lector, K.C.E.Alertas, K.C.E.Reloj);

    public RegisterCashDisbursementCommandHandler Registrar() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, K.C.E.Reloj, K.C.Cerrojo, K.C.Numerador, K.C.E.Parametros,
        K.C.Presente, K.Cuentas, K.Obligaciones, K.Listas, K.C.Emision, Auditoria, Registro);

    public VoidCashDisbursementCommandHandler Anular() => new(K.C.E.Db, K.C.E.Alcance, K.C.E.Actor, K.C.E.Reloj, K.C.Cerrojo, K.C.Numerador, K.C.Presente, K.Cuentas,
        K.Obligaciones, K.C.Emision, Auditoria, Registro);

    public RegisterCashDisbursementCommand Retiro(decimal valor, Guid? clave = null) =>
        new(K.Sesion.PublicId, CashDisbursementKind.SavingsWithdrawal, K.Asociado.PublicId, valor, Ahorros, SignerPersonPublicIds: [K.Asociado.PublicId])
        {
            OperationKey = clave ?? Guid.NewGuid(),
        };

    /// <summary>El efectivo esperado de la sesión (sin pasar por el arqueo ciego).</summary>
    public async Task<decimal> EfectivoEsperadoAsync() =>
        await Application.Receipts.Sessions.SesionesDeCaja.EfectivoAsync(K.C.E.Db,
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(K.C.E.Db.ReceiptsCashSessions, s => s.PublicId == K.Sesion.PublicId), default);
}
