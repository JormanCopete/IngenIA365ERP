using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using H = IngenIA365ERP.Application.Lending.Integration.HechosRepresentativosDeCaptaciones;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Feature 014, pendiente de la fase 17 (US14; D23; research E202, E206): la validación previa contable de las pantallas del ciclo de vida de las cuentas,
/// igual que la de las demás pantallas de captaciones (<see cref="ValidacionPreviaDeCaptaciones.EnPantallaAsync"/>, regida por
/// <c>Contabilidad.FaltaDeRegla</c>): la aprobación de la entrega por fallecimiento, el traslado a mano a no reclamados y el pago de un saldo no reclamado.
/// Con <c>Bloquear</c> y una regla faltante la pantalla responde el 422 <c>Lending.Prevalidation.NotPostable</c> antes de mover nada; con <c>Advertir</c>,
/// sigue y alerta. Sin contabilidad iniciada o con el hecho en <c>NoPasa</c> no pregunta. Los hechos son los representativos de lo que la operación emite:
/// la liquidación a la fecha con su retención si la cuenta causó alguna vez, y la entrega (giro o traslado) o el traslado a no reclamados. (nuevo)
/// </summary>
public static class ValidacionPreviaDelCicloDeVida
{
    /// <summary>
    /// La aprobación de la entrega por fallecimiento: por cada cuenta que se entrega, su liquidación (si causó) y una entrega por la forma de cada
    /// destinatario —devolución por pagar con motivo <c>DeceasedDelivery</c> o traslado a la cuenta del destinatario—. Nulo si puede seguir.
    /// </summary>
    public static async Task<Error?> EntregaPorFallecimientoAsync(ValidacionPreviaDeCaptaciones validacion, IApplicationDbContext db,
        IReadOnlyCollection<int> cuentas, IReadOnlyCollection<int> cuentasDestino, bool hayGiros, DateOnly fecha, string caso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(validacion);
        if (cuentas.Count == 0) return null;
        var dimensiones = await validacion.DimensionesAsync(cuentas.Concat(cuentasDestino).Distinct().ToList(), fecha, ct);
        var hechos = new List<MensajeContableDto>();
        foreach (var id in cuentas)
        {
            if (!dimensiones.TryGetValue(id, out var d)) continue;
            hechos.AddRange(await LiquidacionSiCausoAsync(db, id, d, fecha, ct));
            if (hayGiros) hechos.AddRange(H.Unidad(fecha, d, H.Devolucion(d, nameof(PayoutPurpose.DeceasedDelivery))));
            foreach (var destino in cuentasDestino)
                if (dimensiones.TryGetValue(destino, out var dd)) hechos.AddRange(H.Unidad(fecha, d, H.Traslado(d, dd)));
        }
        return await DecidirAsync(validacion, hechos, $"la entrega por fallecimiento del caso {caso}", ct);
    }

    /// <summary>El traslado a no reclamados de unas cuentas, cada una con su origen: la liquidación a la fecha (si causó) y el traslado. Nulo si puede seguir.</summary>
    public static async Task<Error?> TrasladoANoReclamadosAsync(ValidacionPreviaDeCaptaciones validacion, IApplicationDbContext db,
        IReadOnlyList<(int Cuenta, string Origen)> cuentas, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(validacion);
        ArgumentNullException.ThrowIfNull(cuentas);
        if (cuentas.Count == 0) return null;
        var dimensiones = await validacion.DimensionesAsync(cuentas.Select(c => c.Cuenta).Distinct().ToList(), fecha, ct);
        var hechos = new List<MensajeContableDto>();
        foreach (var (id, origen) in cuentas)
        {
            if (!dimensiones.TryGetValue(id, out var d)) continue;
            hechos.AddRange(await LiquidacionSiCausoAsync(db, id, d, fecha, ct));
            hechos.AddRange(H.Unidad(fecha, d, H.TrasladoANoReclamados(d, origen)));
        }
        return await DecidirAsync(validacion, hechos, "el traslado a no reclamados", ct);
    }

    /// <summary>El pago de un saldo no reclamado desde la cuenta de origen: por giro o por traslado a <paramref name="destino"/>. Nulo si puede seguir.</summary>
    public static async Task<Error?> PagoDeNoReclamadoAsync(ValidacionPreviaDeCaptaciones validacion, int cuentaOrigen, MemberAccount? destino, DateOnly fecha,
        string numeroDeCuenta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(validacion);
        var ids = destino is null ? new List<int> { cuentaOrigen } : [cuentaOrigen, destino.Id];
        var dimensiones = await validacion.DimensionesAsync(ids, fecha, ct);
        if (!dimensiones.TryGetValue(cuentaOrigen, out var d)) return null;
        object hecho = destino is not null && dimensiones.TryGetValue(destino.Id, out var dd)
            ? H.PagoDeNoReclamadoPorTraslado(d, dd)
            : H.PagoDeNoReclamadoPorGiro(d);
        return await DecidirAsync(validacion, H.Unidad(fecha, d, hecho), $"el pago del saldo no reclamado de la cuenta {numeroDeCuenta}", ct);
    }

    private static async Task<IReadOnlyList<MensajeContableDto>> LiquidacionSiCausoAsync(IApplicationDbContext db, int cuenta, H.CuentaRepresentativa d,
        DateOnly fecha, CancellationToken ct) =>
        await db.MemberAccountTransactions.AsNoTracking().AnyAsync(t => t.MemberAccountId == cuenta && t.Concept == MemberAccountConcept.InterestPayable, ct)
            ? H.Unidad(fecha, d, H.Liquidacion(d), H.Retencion(d))
            : [];

    private static async Task<Error?> DecidirAsync(ValidacionPreviaDeCaptaciones validacion, IReadOnlyList<MensajeContableDto> hechos, string operacion,
        CancellationToken ct)
    {
        if (hechos.Count == 0) return null;
        var decision = await validacion.EnPantallaAsync(hechos, operacion, ct);
        return decision.Bloqueo;
    }
}
