using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// La segregación de la Tesorería nueva por <b>persona</b> (feature 015, T0060; US12; R-31; FR-012, FR-217; D18, D19): quien prepara un lote no lo
/// aprueba ni descarga su archivo; quien prepara o aprueba pagos de una cuenta en el período no la concilia; que una tercera persona registre el resultado
/// es configurable (<c>Lotes.TerceraPersonaParaResultado</c>, sellado en <c>TES_Setup.ResultWithThirdPerson</c> al activar). La comparación de personas es
/// la de <see cref="PersonasDeLosUsuarios"/> (<c>SEC_Users.PersonId</c>), con el usuario de <c>IActorActual</c>, nunca el del token. Los lotes, archivos,
/// resultados y conciliaciones que la llaman nacen en sus fases (7, 8, 17). (nuevo)
/// </summary>
public sealed class SegregacionDeTesoreria(PersonasDeLosUsuarios personas)
{
    /// <summary>Quien aprueba un lote (en cualquier nivel) no es quien lo preparó: <c>Treasury.Batch.SelfApproval</c>.</summary>
    public async Task<Result> AprobarLoteAsync(int preparo, int aprueba, CancellationToken ct = default) =>
        await personas.MismaPersonaAsync(preparo, aprueba, ct) ? Result.Failure(ErroresDeTesoreria.LoteAutoaprobado()) : Result.Success();

    /// <summary>Quien descarga el archivo de un lote no es quien lo preparó ni quien lo aprobó: <c>Treasury.File.SameUserDownload</c>.</summary>
    public async Task<Result> DescargarArchivoAsync(int preparo, IEnumerable<int> aprobaron, int descarga, CancellationToken ct = default) =>
        await personas.AlgunaEsLaMismaAsync(descarga, [preparo, .. aprobaron], ct) ? Result.Failure(ErroresDeTesoreria.DescargaPorQuienPreparo()) : Result.Success();

    /// <summary>
    /// Con <paramref name="terceraPersona"/> (lo sellado en <c>TES_Setup</c>), quien registra el resultado de un lote no es quien lo preparó ni quien lo
    /// aprobó: <c>Treasury.Result.ThirdPersonRequired</c> (nuevo, DT §2.7). Sin la bandera, cualquiera con el permiso.
    /// </summary>
    public async Task<Result> RegistrarResultadoAsync(bool terceraPersona, int preparo, IEnumerable<int> aprobaron, int registra, CancellationToken ct = default) =>
        terceraPersona && await personas.AlgunaEsLaMismaAsync(registra, [preparo, .. aprobaron], ct)
            ? Result.Failure(ErroresDeTesoreria.ResultadoPorTerceraPersona())
            : Result.Success();

    /// <summary>
    /// Quien concilia una cuenta no preparó ni aprobó pagos de ella en el período (FR-217): <c>Treasury.Reconciliation.SegregationConflict</c>.
    /// <paramref name="participantes"/> son los <c>SEC_Users.Id</c> que prepararon o aprobaron lotes de la cuenta en el mes.
    /// </summary>
    public async Task<Result> ConciliarAsync(int concilia, IEnumerable<int> participantes, CancellationToken ct = default) =>
        await personas.AlgunaEsLaMismaAsync(concilia, participantes, ct) ? Result.Failure(ErroresDeTesoreria.ConciliacionConflicto()) : Result.Success();
}
