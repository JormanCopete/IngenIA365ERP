using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// <b>El seguimiento de la cuota dejada a su canal</b> (feature 028, E2, T070; HU4-7, HU8, FR-020): la recogida con «todo menos la cuota causada» deja en el
/// crédito viejo la cuota del período tal como se envió (<c>LND_DisbursementPayoffs.LeftInstallmentNumber</c>) para que la pague su canal —la pagaduría, la
/// nómina propia o la caja—. Este sitio
/// <list type="bullet">
/// <item>la marca pagada (<c>LeftInstallmentSettledAt</c>) cuando un pago, por cualquier canal, la deja sin nada que deber o cancela el crédito
/// (<see cref="MarcarPagadasAsync"/>; lo llama el núcleo de los pagos después de registrar, en su misma transacción: no escribe el libro);</item>
/// <item>avisa al asesor que radicó la solicitud del crédito nuevo cuando llega un «no descontado» de esa cuota —de la pagaduría o de la nómina propia—
/// (<see cref="AvisarNoDescontadoAsync"/>): la cuota sigue la regla del convenio como cualquier otra y él sabe que la recogida contaba con ese descuento.</item>
/// </list>
/// Un reverso del pago que la cerró no la vuelve a abrir (la marca es «una vez», <c>DisbursementPayoff.MarcarCuotaDejadaPagada</c>): queda como pendiente.
/// (nuevo)
/// </summary>
public sealed class SeguimientoDeCuotaDejada(IApplicationDbContext db, ISender? sender = null, ILogger<SeguimientoDeCuotaDejada>? logger = null)
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>
    /// Marca pagadas las cuotas dejadas de <paramref name="creditoId"/> que el pago dejó sin deuda (<see cref="CuotaPagada"/>) o todas si el pago canceló el
    /// crédito. Sin guardar: lo guarda quien llama. Devuelve cuántas marcó.
    /// </summary>
    public static async Task<int> MarcarPagadasAsync(IApplicationDbContext db, int creditoId, EstadoDelCredito despues, bool cancela, DateTime instante,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(despues);
        var pendientes = await db.DisbursementPayoffs
            .Where(f => f.LoanId == creditoId && f.LeftInstallmentNumber != null && f.LeftInstallmentSettledAt == null && f.ReversedAt == null && !f.IsDeleted)
            .ToListAsync(ct);
        var marcadas = 0;
        foreach (var f in pendientes.Where(f => cancela || CuotaPagada(despues, f.LeftInstallmentNumber!.Value)))
        {
            f.MarcarCuotaDejadaPagada(instante);
            marcadas++;
        }
        return marcadas;
    }

    /// <summary>
    /// ¿La cuota <paramref name="cuota"/> ya no debe nada en <paramref name="estado"/>? Sin capital pendiente y sin saldo de sus conceptos (el anticipo y el
    /// saldo a favor no cuentan). Una cuota que el plan ya no tiene, también. (nuevo)
    /// </summary>
    public static bool CuotaPagada(EstadoDelCredito estado, int cuota)
    {
        ArgumentNullException.ThrowIfNull(estado);
        var deLaCuota = estado.Cuotas.FirstOrDefault(c => c.Numero == cuota);
        if (deLaCuota is null) return estado.Plan?.Cuotas.All(c => c.Numero != cuota) ?? true;
        return deLaCuota.CapitalPendiente <= 0m
               && estado.Saldos.Where(s => s.Cuota == cuota && s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance)).Sum(s => s.Valor) <= 0m;
    }

    /// <summary>
    /// Si <paramref name="cuota"/> de <paramref name="creditoId"/> es una cuota dejada a su canal por una recogida y todavía se debe, avisa al asesor que radicó
    /// la solicitud del crédito nuevo (o, sin él, a quien la creó) que no se descontó, con una notificación de la aplicación y por correo. Sin cuota (nula),
    /// cualquier cuota dejada pendiente del crédito. Devuelve a cuántos avisó; sin el despachador (pruebas antiguas) o sin asesor, a nadie. Nunca hace caer
    /// a quien registra el no descontado: un aviso que no sale queda en el log.
    /// </summary>
    public async Task<int> AvisarNoDescontadoAsync(int creditoId, int? cuota, decimal noDescontado, string origen, CancellationToken ct)
    {
        var dejadas = await (from f in db.DisbursementPayoffs.AsNoTracking()
                             join o in db.LoanOperations.AsNoTracking() on f.DisbursementOperationId equals o.Id
                             join nuevo in db.Loans.AsNoTracking().IgnoreQueryFilters() on o.LoanId equals nuevo.Id
                             where f.LoanId == creditoId && f.LeftInstallmentNumber != null && f.LeftInstallmentSettledAt == null && f.ReversedAt == null
                                   && !f.IsDeleted && (cuota == null || f.LeftInstallmentNumber == cuota)
                             select new { f.LeftInstallmentNumber, f.LeftInstallmentAmount, f.ProductNumber, Nuevo = nuevo.Number, nuevo.ApplicationId })
            .ToListAsync(ct);
        if (dejadas.Count == 0 || sender is null) return 0;
        var avisados = 0;
        foreach (var d in dejadas)
        {
            var asesor = d.ApplicationId is { } solicitud
                ? await (from s in db.CreditApplications.AsNoTracking().IgnoreQueryFilters()
                         join u in db.Users.AsNoTracking().IgnoreQueryFilters() on (s.SubmittedByUserId ?? s.CreatedByUserId) equals (int?)u.Id
                         where s.Id == solicitud
                         select (Guid?)u.PublicId).FirstOrDefaultAsync(ct)
                : null;
            if (asesor is null) continue;
            var valor = noDescontado.ToString("C0", Colombia);
            try
            {
                var r = await sender.Send(new SendNotificationCommand(new NotificationPayload(asesor.Value, NotificationType.Generic,
                    $"No se descontó la cuota {d.LeftInstallmentNumber} del crédito {d.ProductNumber}",
                    $"El crédito {d.Nuevo} recogió el crédito {d.ProductNumber} y dejó su cuota {d.LeftInstallmentNumber} "
                    + $"({(d.LeftInstallmentAmount ?? 0m).ToString("C0", Colombia)}) para que la pagara su canal. {origen} no descontó {valor}: la cuota sigue "
                    + "en el crédito viejo y se cobra con la regla del convenio, como cualquier cuota no descontada.",
                    NotificationChannels.InApp | NotificationChannels.Email)), ct);
                if (r.IsSuccess) avisados++;
                else _log.LogInformation("[Cartera.CuotaDejadaNoDescontada] No se avisó al asesor del crédito {Credito}: {Codigo} {Mensaje}", d.Nuevo, r.Error.Code,
                    r.Error.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "[Cartera.CuotaDejadaNoDescontada] El aviso al asesor del crédito {Credito} falló.", d.Nuevo);
            }
        }
        return avisados;
    }
}
