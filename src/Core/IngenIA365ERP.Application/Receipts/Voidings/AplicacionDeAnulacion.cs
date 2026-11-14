using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Voidings;

/// <summary>A dónde va el dinero de un recibo anulado.</summary>
public enum DineroDeLaAnulacion
{
    /// <summary>La regla de la 015: con la sesión abierta, el efectivo y el cheque bajan lo esperado; si no, saldo a favor del pagador.</summary>
    SigueLaSesion = 1,

    /// <summary>Feature 021 (FR-052): la pasarela devolvió el pago antes del corte; el asiento es el espejo exacto del recibo (sale del medio).</summary>
    DevueltoPorLaPasarela = 2,

    /// <summary>Feature 021 (FR-052): después del corte el dinero ya está en el banco; nace un saldo a favor del pagador que Tesorería devuelve.</summary>
    SaldoAFavor = 3,
}

/// <summary>
/// La aplicación de una anulación de recibo, compartida por la anulación aprobada de Recaudo (feature 015, T0175) y el reverso de un pago del canal
/// digital (feature 021, T117). (nuevo)
/// </summary>
public sealed class AplicacionDeAnulacion(
    IApplicationDbContext db,
    NumeradorDeRecaudo numerador,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    EmisionDeRecaudo emision,
    IDateTimeService reloj,
    Compliance.RegistroDeEfectivo? registroDeEfectivo = null)
{
    /// <summary>
    /// Numera la anulación, reversa cada porción vigente por su contrato (obligaciones antes que cuentas, todo o nada), lleva el dinero según
    /// <paramref name="dinero"/> y emite <c>ReciboAnulado</c> espejo. Sin aprobación: quien llama ya la tiene. Guarda en la unidad de trabajo de quien llama.
    /// </summary>
    public async Task<Result> AplicarAsync(ReceiptVoiding anulacion, Receipt recibo, Domain.Entities.Receipts.Sessions.CashSession? sesion, int usuario, string? motivo,
        DineroDeLaAnulacion dinero, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var aplicaciones = await db.ReceiptApplications.Where(a => a.ReceiptId == recibo.Id).OrderBy(a => a.Ordinal).ToListAsync(ct);
        var ids = aplicaciones.Select(a => a.Id).ToList();
        var porciones = await db.ReceiptApplicationPortions.Where(p => ids.Contains(p.ReceiptApplicationId)).OrderBy(p => p.Id).ToListAsync(ct);
        var medios = await db.ReceiptMeans.Where(m => m.ReceiptId == recibo.Id).ToDictionaryAsync(m => m.Id, ct);
        var omitidas = aplicaciones.Where(a => a.Status == ApplicationState.ReversedByModule).ToList();
        var vigentes = porciones.Where(p => p.Status == PortionState.Applied && aplicaciones.First(a => a.Id == p.ReceiptApplicationId).Status == ApplicationState.Applied)
            .ToList();

        // Numeración antes de los contratos (el contrato guarda el número del documento): en la misma transacción, un rechazo no consume número.
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.Voiding, recibo.CollectionPointId, ct);
        await numerador.NumerarAsync(anulacion, tipo.Code, prefijo, ct);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == recibo.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var referencia = new ReferenciaDeRecibo(anulacion.Number, hoy, oficina, recibo.Number, nameof(ReceiptDocumentKind.Voiding));
        var razon = $"Anulación del recibo {recibo.Number}: {anulacion.Reason}";
        var rechazos = new List<object>();

        // Feature 015, R1 (T0363): un recaudo pendiente que ya se aplicó en su módulo impide anular; el vigente se anula con el recibo.
        var pendientes = await db.PendingApplications.Where(x => x.ReceiptId == recibo.Id).ToListAsync(ct);
        if (pendientes.Any(x => x.Status == PendingApplicationState.Applied)) return Result.Failure(CashDesk.ErroresDelPendiente.YaAplicado());

        // Obligaciones antes que cuentas (FR-170), una vez por porción.
        foreach (var p in vigentes.Where(p => Destino(aplicaciones, p) == ApplicationTarget.Loan))
        {
            var r = await obligaciones.ReversarAsync(new SolicitudDeReverso(ClavesDeRecaudo.Recaudador, p.IdempotencyKey, razon, referencia), ct);
            if (r.Result == ResultadoDelContrato.Rejected) rechazos.Add(new { idempotencyKey = p.IdempotencyKey, code = r.Code, message = r.Message });
        }
        if (rechazos.Count == 0)
            foreach (var p in vigentes.Where(p => Destino(aplicaciones, p) == ApplicationTarget.Account))
            {
                var r = await cuentas.ReversarAsync(new SolicitudDeReversoDeCuenta(ClavesDeRecaudo.Recaudador, p.IdempotencyKey, razon, referencia), ct);
                if (r.Result == ResultadoDelContrato.Rejected) rechazos.Add(new { idempotencyKey = p.IdempotencyKey, code = r.Code, message = r.Message });
            }
        if (rechazos.Count > 0) return Result.Failure(ErroresDeRecaudo.AnulacionRechazadaPorContrato(rechazos));

        // El dinero sigue a la sesión: abierta, el efectivo y el cheque bajan lo esperado; cerrada (o un medio electrónico ya abonado), saldo a favor del pagador.
        var sesionAbierta = sesion?.Status == CashSessionState.Open;
        var reversadas = new List<(ReceiptApplicationPortion, ReceiptApplication, ReceiptMeans, bool)>();
        foreach (var p in vigentes)
        {
            p.Status = PortionState.Reversed;
            p.ReversalKey = ClavesDeRecaudo.Reverso(p.IdempotencyKey);
            var medio = medios[p.ReceiptMeansId];
            var aSaldo = dinero switch
            {
                DineroDeLaAnulacion.DevueltoPorLaPasarela => false,
                DineroDeLaAnulacion.SaldoAFavor => true,
                _ => !sesionAbierta || medio.MeansClass is not (PaymentMeansClass.Cash or PaymentMeansClass.Check),
            };
            reversadas.Add((p, aplicaciones.First(a => a.Id == p.ReceiptApplicationId), medio, aSaldo));
        }
        foreach (var a in aplicaciones.Where(a => a.Status == ApplicationState.Applied)) a.Status = ApplicationState.Voided;
        foreach (var x in pendientes.Where(x => x.Status == PendingApplicationState.Pending)) x.Status = PendingApplicationState.Voided;
        recibo.Status = ReceiptState.Voided;
        anulacion.Status = ReceiptVoidingState.Applied;
        anulacion.ApprovedByUserId = usuario;
        anulacion.DecidedAt = ahora;
        anulacion.DecisionReason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        anulacion.OmittedApplicationsJson = omitidas.Count == 0 ? null : AnulacionesDeRecibos.Omitidas(omitidas.Select(o => o.ProductNumber ?? o.Ordinal.ToString()));

        var aFavor = reversadas.Where(x => x.Item4).Sum(x => x.Item1.Amount);
        if (aFavor > 0m)
        {
            var saldo = SaldosAFavor.Reconocer(db, recibo.PayerPersonId, PayerCreditBalanceOrigin.VoidAfterSessionClose, anulacion.PublicId, anulacion.Number, recibo.BranchId,
                aFavor, hoy, usuario, ahora, recibo.OwnBankAccountPublicId);
            await db.SaveChangesAsync(ct);
            SaldosAFavor.Reconocido(db, saldo, usuario, ahora);
            anulacion.PayerCreditBalanceId = saldo.Id;
        }
        await db.SaveChangesAsync(ct);
        if (sesion is not null && sesionAbierta)
        {
            await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
            await db.SaveChangesAsync(ct);
        }
        await emision.ReciboAnuladoAsync(anulacion, recibo, reversadas, dinero == DineroDeLaAnulacion.SaldoAFavor || !sesionAbierta, tipo.Code, ct);
        // Feature 015, US14 (T0241): la anulación escribe su fila con signo contrario en el registro de efectivo.
        if (registroDeEfectivo is not null) await registroDeEfectivo.AlAnularAsync(recibo, anulacion.PublicId, anulacion.Number, ct);
        return Result.Success();
    }

    private static ApplicationTarget Destino(IReadOnlyList<ReceiptApplication> aplicaciones, ReceiptApplicationPortion p) =>
        aplicaciones.First(a => a.Id == p.ReceiptApplicationId).Target;
}
