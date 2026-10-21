using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Integration;

/// <summary>
/// Lo común de los consumidores contables (feature 012, T512; feature 013, T0644, C82; contracts/contabilidad.md §7.1 de la 013 y §3.1 de
/// la 012): la revisión de versión y de moneda (pasos 2 y 3), el original que todavía espera en la bandeja (paso 5), la colisión del índice
/// único del recibo (paso 11) y el rechazo con sus datos. Se extrajo de <c>ConsumoDeInventario</c> sin cambiar lo que responde a Inventario;
/// <c>ConsumoDeCartera</c> lo usa con sus propios recibos (<c>ACC_LendingPostings</c>) y sus propios códigos. No guarda ni escribe tablas de la
/// plataforma: lee las entregas sólo para saber si un original sigue pendiente (T11). (nuevo)
/// </summary>
public static class RecibosDeContabilidad
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>¿El choque es contra el índice único <paramref name="indice"/> del recibo? (otra réplica ganó).</summary>
    public static bool EsColision(DbUpdateException ex, string indice)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
            if (actual.Message.Contains(indice, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Pasos 2 y 3: el primer mensaje cuya versión no acepta <paramref name="modulo"/> (<c>Integration.VersionNotAccepted</c>) o cuya moneda no
    /// es <c>COP</c> con tasa 1 (el código de <paramref name="monedaNoAdmitida"/>) da el rechazo; nulo si todos pasan.
    /// </summary>
    public static ResultadoDeConsumo.Rejected? RevisarVersionYMoneda(
        IEnumerable<MensajeEntrante> unidad, string modulo, Func<string, decimal, Error> monedaNoAdmitida)
    {
        foreach (var m in unidad)
        {
            if (!VersionesAceptadas.Acepta(modulo, m.Type, m.Version))
                return Rechazo(ErroresDeIntegracion.VersionNoAceptada(m.Type, m.Version));
            if (!string.Equals(m.Envelope.Currency, "COP", StringComparison.Ordinal) || m.Envelope.ExchangeRate != 1m)
                return Rechazo(monedaNoAdmitida(m.Envelope.Currency, m.Envelope.ExchangeRate));
        }
        return null;
    }

    /// <summary>
    /// Paso 5: el primer mensaje de los orígenes <paramref name="origenes"/> cuya entrega a Contabilidad sigue <c>Pending</c>, <c>InBatch</c> o
    /// <c>Rejected</c> (un original <c>NotApplicable</c> no retiene); nulo si ninguno.
    /// </summary>
    public static Task<Guid?> OriginalPendienteAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> origenes, CancellationToken ct) =>
        (from d in db.IntegrationMessageDeliveries.AsNoTracking()
         join mm in db.IntegrationMessages.AsNoTracking() on d.MessageId equals mm.Id
         where origenes.Contains(mm.OriginPublicId) && d.Destination == IntegrationDestinations.Accounting
               && (d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.InBatch || d.Status == DeliveryStatus.Rejected)
         orderby mm.Id
         select (Guid?)mm.PublicId)
        .FirstOrDefaultAsync(ct);

    /// <summary>El rechazo de un error, con sus datos en JSON si los trae.</summary>
    public static ResultadoDeConsumo.Rejected Rechazo(Error error) =>
        new(error.Code, error.Message, error is ErrorConDatos conDatos ? JsonSerializer.Serialize(conDatos.Data, Json) : null);
}
