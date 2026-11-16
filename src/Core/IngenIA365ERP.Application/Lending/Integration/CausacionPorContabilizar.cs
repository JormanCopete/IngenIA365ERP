using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Lo causado de un concepto en modo manual que espera ser contabilizado, por día y hecho (feature 024, fase 1): cuántos hechos (partes), cuántos
/// créditos y por cuánto. (nuevo)
/// </summary>
public sealed record CausacionPendienteDeContabilizar(DateOnly Dia, string Hecho, int Hechos, int Creditos, decimal Total);

/// <summary>
/// 2026-11-16 (pedido del dueño; feature 024, fase 1): la <b>causación pendiente de contabilizar</b> de los conceptos en modo manual
/// (<c>Causacion.Modo* = Manual</c>): las entregas a Contabilidad <c>InBatch</c>, sin lote, con el alcance <c>CausacionManual:{día}</c> que les sella
/// <see cref="ModoDePasoDeCartera"/>. Es el punto por donde el proceso de causación de la fase 3 las toma —por concepto (el tipo de hecho) y rango de
/// días causados— y por donde se informa lo que espera. Mientras la fase 3 no exista, un lote manual de Cartera (<c>/contabilidad/cartera/lotes</c>,
/// <c>OrderIntegrationBatchCommand</c> con <c>CAR</c>) las toma igual que a cualquier entrega sin lote de su rango; el cierre contable del mes las
/// cuenta en <c>Accounting.Period.LendingPending</c>. No escribe. (nuevo)
/// </summary>
public sealed class CausacionPorContabilizar(IApplicationDbContext db)
{
    /// <summary>
    /// Las entregas que esperan de lo causado de <paramref name="desde"/> a <paramref name="hasta"/> (días causados, incluidos), de los hechos de
    /// <paramref name="hechos"/> si vienen (<c>InteresCausado</c>, <c>MoraCausada</c>, <c>SeguroCausado</c>, <c>CorreccionMonetariaUvrCausada</c>),
    /// en orden de emisión.
    /// </summary>
    public IQueryable<IntegrationMessageDelivery> Entregas(DateOnly desde, DateOnly hasta, IReadOnlyCollection<string>? hechos = null)
    {
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var primero = ModoDePasoDeCartera.AlcanceDeCausacionManual(desde);
        var ultimo = ModoDePasoDeCartera.AlcanceDeCausacionManual(hasta);
        var consulta = db.IntegrationMessageDeliveries
            .Where(d => d.Destination == IntegrationDestinations.Accounting && d.Status == DeliveryStatus.InBatch && d.BatchId == null
                        && d.BatchScopeKey != null && d.BatchScopeKey.StartsWith(ModoDePasoDeCartera.PrefijoDeCausacionManual)
                        && string.Compare(d.BatchScopeKey, primero) >= 0 && string.Compare(d.BatchScopeKey, ultimo) <= 0);
        if (hechos is { Count: > 0 })
        {
            var lista = hechos.ToList();
            consulta = consulta.Where(d => lista.Contains(d.Message!.Type));
        }
        return consulta.OrderBy(d => d.MessageId);
    }

    /// <summary>
    /// Lo que espera, por día causado y hecho: las partes (<c>LND_AccountingFactParts</c>) cuyo mensaje sigue sin lote, con sus créditos y su total de
    /// control. En orden de día y hecho.
    /// </summary>
    public async Task<IReadOnlyList<CausacionPendienteDeContabilizar>> ResumenAsync(DateOnly desde, DateOnly hasta, IReadOnlyCollection<string>? hechos,
        CancellationToken ct)
    {
        var pendientes = await Entregas(desde, hasta, hechos).AsNoTracking()
            .Select(d => new { d.BatchScopeKey, d.Message!.Type, d.Message.PublicId })
            .ToListAsync(ct);
        if (pendientes.Count == 0) return [];
        var mensajes = pendientes.Select(p => p.PublicId).Distinct().ToList();
        var partes = (await db.AccountingFactParts.AsNoTracking()
                .Where(p => p.MessagePublicId != null && mensajes.Contains(p.MessagePublicId.Value))
                .Select(p => new { p.MessagePublicId, p.LoanCount, p.TotalAmount })
                .ToListAsync(ct))
            .GroupBy(p => p.MessagePublicId!.Value).ToDictionary(g => g.Key, g => (Creditos: g.Sum(x => x.LoanCount), Total: g.Sum(x => x.TotalAmount)));
        return pendientes
            .GroupBy(p => (Dia: DateOnly.ParseExact(p.BatchScopeKey![ModoDePasoDeCartera.PrefijoDeCausacionManual.Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture), p.Type))
            .OrderBy(g => g.Key.Dia).ThenBy(g => g.Key.Type, StringComparer.Ordinal)
            .Select(g => new CausacionPendienteDeContabilizar(g.Key.Dia, g.Key.Type, g.Count(),
                g.Sum(p => partes.GetValueOrDefault(p.PublicId).Creditos), g.Sum(p => partes.GetValueOrDefault(p.PublicId).Total)))
            .ToList();
    }
}
