using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>
/// Lo que la caja captura de cada medio de R1 (feature 015, R1, T0357; FR-023, FR-024, FR-031, FR-041; data-model §3.4): lo usan el paso de medios
/// (<c>UpdateCashOperationMeansCommand</c>) y otra vez la confirmación (<see cref="ConfirmacionDeRecibo"/>), porque entre los dos pasos otro cajero pudo usar el mismo
/// bono.
/// <list type="bullet">
/// <item><b>tarjetas</b> (débito y crédito): el datáfono —activo y del adquirente del medio—, el lote y la aprobación; la red es la del medio. El número de la tarjeta
/// <b>nunca</b> se guarda: una referencia con 13 a 19 dígitos seguidos se rechaza (<c>Receipts.PaymentMeans.CardNumberNotAllowed</c>);</item>
/// <item><b>bono</b>: su número, único en el medio (<c>Receipts.PaymentMeans.VoucherAlreadyUsed</c>), también entre los medios de la misma operación;</item>
/// <item><b>otro</b>: su referencia.</item>
/// </list>
/// Sólo valida; no escribe. (nuevo)
/// </summary>
public static partial class CapturaDeMedios
{
    [GeneratedRegex(@"\d{13,19}", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroDeTarjeta();

    /// <summary>¿El texto lleva algo que parece un número de tarjeta (13 a 19 dígitos seguidos, sin espacios ni guiones)?</summary>
    public static bool PareceNumeroDeTarjeta(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) && NumeroDeTarjeta().IsMatch(texto.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal));

    public static async Task<Result> ValidarAsync(IApplicationDbContext db, ValoresSelladosDeLaSesion sellados, IReadOnlyList<MedioDeCajaDto> medios, CancellationToken ct)
    {
        var bonos = new HashSet<(int, string)>();
        foreach (var m in medios)
        {
            var sellado = sellados.Means.FirstOrDefault(x => x.PublicId == m.PaymentMeansPublicId);
            if (sellado is null) continue; // el medio no ofrecido lo rechaza la imputación con su propio error.
            if (MediosDeRecaudo.EsTarjeta(sellado.Class))
            {
                if (PareceNumeroDeTarjeta(m.Reference) || PareceNumeroDeTarjeta(m.ApprovalCode) || PareceNumeroDeTarjeta(m.TerminalBatch))
                    return Result.Failure(ErroresDeRecaudo.NumeroDeTarjeta(sellado.Name));
                if (m.CardTerminalPublicId is not { } terminal) return Result.Failure(ErroresDeRecaudo.CapturaDeTarjetaIncompleta(sellado.Name, "el datáfono"));
                if (string.IsNullOrWhiteSpace(m.TerminalBatch)) return Result.Failure(ErroresDeRecaudo.CapturaDeTarjetaIncompleta(sellado.Name, "el lote del datáfono"));
                if (string.IsNullOrWhiteSpace(m.ApprovalCode)) return Result.Failure(ErroresDeRecaudo.CapturaDeTarjetaIncompleta(sellado.Name, "el número de aprobación"));
                var adquirente = await db.PaymentMeans.AsNoTracking().Where(x => x.Id == sellado.PaymentMeansId).Select(x => x.CardAcquirerId).FirstOrDefaultAsync(ct);
                var deEse = await db.CardTerminals.AsNoTracking().AnyAsync(t => t.PublicId == terminal && t.IsActive && t.CardAcquirerId == adquirente, ct);
                if (!deEse) return Result.Failure(ErroresDeRecaudo.CapturaDeTarjetaIncompleta(sellado.Name, "un datáfono activo del adquirente del medio"));
                continue;
            }
            if (sellado.Class == PaymentMeansClass.Voucher)
            {
                if (string.IsNullOrWhiteSpace(m.VoucherNumber)) return Result.Failure(ErroresDeRecaudo.ReferenciaRequerida(sellado.Name));
                var bono = m.VoucherNumber.Trim();
                if (!bonos.Add((sellado.PaymentMeansId, bono))
                    || await db.ReceiptMeans.AsNoTracking().AnyAsync(x => x.PaymentMeansId == sellado.PaymentMeansId && x.VoucherNumber == bono, ct))
                    return Result.Failure(ErroresDeRecaudo.BonoUsado(bono));
                continue;
            }
            // «Otro» se identifica siempre por su referencia (lo de R0 —transferencia, consignación— sigue con sus reglas de R0).
            if (sellado.Class == PaymentMeansClass.Other && string.IsNullOrWhiteSpace(m.Reference))
                return Result.Failure(ErroresDeRecaudo.ReferenciaRequerida(sellado.Name));
        }
        return Result.Success();
    }
}
