using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La validación previa contable de los <b>procesos</b> de Cartera (feature 013, T0801; A8 = «Bloquear todo», respuesta del dueño del 2026-10-14;
/// contabilidad.md §13.2): la causación diaria, la corrección por valor nacional y el cierre mensual. Con <c>Contabilidad.FaltaDeRegla = Bloquear</c>
/// una tanda cuyos hechos no se pueden contabilizar <b>no avanza</b>: el proceso deshace la tanda (su transacción no confirma), levanta
/// <c>Cartera.HechoNoContabilizable</c> y la corrida queda <c>Failed</c> con <c>Lending.Prevalidation.NotPostable</c> en sus errores; corregida la
/// matriz, «Reintentar» la retoma desde el mismo cursor. Con <c>Advertir</c> fijado (el defecto es <c>Bloquear</c> desde el 2026-10-14), sin contabilidad iniciada o con los hechos en
/// <c>NoPasa</c>, no se pregunta nada y el proceso sigue como antes (§10.1). (nuevo)
/// <para>
/// Lo que se evalúa son los hechos de Cartera que la tanda dejó <b>agregados sin guardar</b> en la unidad de trabajo (los informativos y los de otros
/// destinos los descarta la validación), así que se llama justo antes del <c>SaveChanges</c> de la tanda, dentro de su transacción y sin tiempo
/// máximo.
/// </para>
/// </summary>
public sealed class ValidacionPreviaDeProceso(IApplicationDbContext db, ValidacionPreviaContableDeCartera validacion)
{
    /// <summary>La decisión que detiene la tanda, o nulo si la tanda puede guardar.</summary>
    public async Task<DecisionDeValidacionPrevia?> BloqueoAsync(CancellationToken ct)
    {
        var hechos = db.IntegrationMessages.Local
            .Where(m => db.IntegrationMessages.Entry(m).State == EntityState.Added && m.OriginModule == ModulosDeOrigen.Cartera
                        && m.Kind == IntegrationMessageKind.Business)
            .Select(m =>
            {
                var contenido = MensajesEntrantes.Deserializar(m);
                return new MensajeContableDto(MensajesEntrantes.Sobre(m, contenido), contenido!);
            })
            .ToList();
        return hechos.Count == 0 ? null : await validacion.DecidirEnProcesoAsync(hechos, ct);
    }

    /// <summary>
    /// Después de deshacer la tanda: levanta la alerta y devuelve la excepción que detiene la corrida con el código del bloqueo (quien conduce la anota
    /// en <c>LND_ProcessRunErrors</c> y deja la corrida <c>Failed</c>).
    /// </summary>
    public async Task<ReglaDeCarteraException> DetenerAsync(DecisionDeValidacionPrevia decision, string operacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);
        await validacion.AlertarBloqueoAsync(decision.Resultado, operacion, ct);
        var error = decision.Bloqueo ?? ErroresDeValidacionPreviaDeCartera.NoContabilizable(decision.Resultado.Errors);
        return new ReglaDeCarteraException(new ProblemaDeCartera(error.Code,
            $"La tanda de {operacion} no avanzó: {error.Message} Corregida la matriz, reintente la corrida."));
    }
}
