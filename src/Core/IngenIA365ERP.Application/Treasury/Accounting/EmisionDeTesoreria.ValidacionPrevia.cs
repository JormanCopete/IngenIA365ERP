using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;

namespace IngenIA365ERP.Application.Treasury.Accounting;

/// <summary>
/// La validación previa contable de las pantallas de Tesorería (feature 015, US8, pendiente de T0250 cerrado el 2026-10-24; R-23; FR-143, FR-221;
/// contabilidad.md §6), con el molde de la caja de Recaudo (<c>ConfirmacionDeRecibo</c>, paso 3): antes de tomar los cerrojos y de tocar nada, pregunta a
/// Contabilidad por <see cref="IContabilidadParaTesoreria.EvaluarAsync"/> si los hechos que la operación va a emitir tienen su regla; si falta alguna y la política
/// de Tesorería (<c>Contabilidad.FaltaDeRegla</c>) es «Bloquear», la pantalla no confirma y responde <c>Treasury.Prevalidation.NotPostable</c> con
/// <c>data.errors[] { factType, role, combination, whoFixes }</c>. Sin contabilidad iniciada o con el modo de paso en «no pasa», nada se bloquea. Tesorería nunca ve
/// una cuenta: pregunta por tipo de hecho y por la cuenta propia (su <c>PublicId</c>), que resuelve los roles por vínculo. (nuevo)
/// </summary>
public sealed partial class EmisionDeTesoreria
{
    /// <summary>La política que bloquea en pantalla (el defecto de <c>Contabilidad.FaltaDeRegla</c>).</summary>
    public const string PoliticaBloquear = "Bloquear";

    /// <summary>Pregunta por los hechos que va a emitir la operación. Sin guardar ni escribir nada.</summary>
    public async Task<Result> ValidacionPreviaAsync(IReadOnlyCollection<string> tiposDeHecho, OwnBankAccount? cuenta, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tiposDeHecho);
        if (tiposDeHecho.Count == 0) return Result.Success();
        var dimensiones = cuenta is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [nameof(TreasuryFactDimensionsV1.OwnBankAccountPublicId)] = cuenta.PublicId.ToString() };
        var evaluacion = await contabilidad.EvaluarAsync(
            [.. tiposDeHecho.Distinct().Select(t => new CombinacionDeHecho(ModulosDeOrigen.Tesoreria, t, dimensiones, fecha))], ct);
        if (!evaluacion.Postable && evaluacion.Policy == PoliticaBloquear)
            return Result.Failure(ErroresDeTesoreria.NoContabilizable(evaluacion.Missing));
        return Result.Success();
    }
}
