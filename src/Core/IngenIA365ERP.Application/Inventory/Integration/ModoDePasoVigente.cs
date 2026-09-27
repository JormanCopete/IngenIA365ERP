using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// El <c>Contabilidad.ModoDePaso</c> que rige a una fecha, en un solo sitio (feature 012; decisión del dueño del 2026-09-26). Es el
/// valor guardado si hay una vigencia —del tipo o la general—; si no la hay, el defecto del catálogo («en línea») salvo que
/// <b>falte iniciar la contabilidad</b>: entonces «no pasa». Sin esa regla, una cooperativa nueva no confirmaba ningún documento,
/// porque la validación previa rechazaba todo con <c>Accounting.NotInitialized</c>. Lo confirmado así queda <c>NotPosted</c> y se
/// envía después con «Enviar lo que no pasaba» (<c>SendNotApplicableMessagesCommand</c>). Un modo guardado a propósito se respeta
/// aunque la contabilidad no esté iniciada: quien pidió «en línea» recibe el rechazo que explica qué falta. (nuevo)
/// </summary>
public static class ModoDePasoVigente
{
    public const string EnLinea = "EnLinea";
    public const string PorLotes = "PorLotes";
    public const string NoPasa = "NoPasa";

    /// <summary>
    /// El texto del modo (<see cref="EnLinea"/>, <see cref="PorLotes"/> o <see cref="NoPasa"/>) a <paramref name="fecha"/>, con la
    /// caída ámbito → general → defecto del lector. Sin puerto contable registrado se queda con lo leído.
    /// </summary>
    public static async Task<Result<string>> LeerAsync(
        ILectorDeParametros parametros,
        IContabilidadParaInventario? contabilidad,
        DateOnly fecha,
        ParameterScopeKind ambito,
        int ambitoId,
        CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, fecha,
            ambito, ambitoId, ct);
        if (leido.IsFailure) return Result.Failure<string>(leido.Error);
        if (leido.Value.EsDefecto && contabilidad is not null && await contabilidad.SinIniciarAsync(ct)) return Result.Success(NoPasa);
        return Result.Success(leido.Value.Texto);
    }
}
