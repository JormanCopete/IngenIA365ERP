using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>Un cierre mensual anterior del crédito y la categoría con que quedó.</summary>
public sealed record CierreParaInsolvencia(DateOnly FechaDeCierre, RatingCategory Categoria);

/// <summary>Un proceso de insolvencia del deudor (<c>LND_InsolvencyProceedings</c>): estado, inicio y, si fracasó, desde cuándo.</summary>
public sealed record ProcesoParaCalificar(InsolvencyProceedingStatus Estado, DateOnly Inicio, DateOnly? FracasoEl = null);

/// <summary>
/// Lo que la insolvencia le pasa al motor de calificación a una fecha de corte (feature 013, K3, T0859; FR-098, data-model §25.8):
/// <list type="bullet">
/// <item>Antes de <see cref="ProcesoParaCalificar.Inicio"/>, nada: la insolvencia todavía no existe.</item>
/// <item>En negociación —o fracasado después del corte, porque a esa fecha se negociaba—, la calificación <b>se mantiene</b> en la del
/// último cierre anterior al inicio; sin cierre anterior, la que se indique (la del crédito al iniciar).</item>
/// <item>Fracasado desde <see cref="ProcesoParaCalificar.FracasoEl"/>, la que fija la norma (el motor la toma del Anexo 1).</item>
/// <item>Con acuerdo, nada: el acuerdo es una reestructuración y entra como tal (FR-090).</item>
/// </list>
/// La categoría mantenida no se guarda (se deriva, data-model §25.8). Puro. (nuevo)
/// </summary>
public static class CalificacionDeInsolvencia
{
    /// <param name="proceso">El proceso del deudor.</param>
    /// <param name="corte">La fecha a la que se califica.</param>
    /// <param name="cierres">Los cierres del crédito (o de su cadena) con su categoría; sólo cuentan los anteriores al inicio.</param>
    /// <param name="sinCierreAnterior">La categoría si no hay cierre anterior al inicio.</param>
    public static InsolvenciaParaCalificar? Para(ProcesoParaCalificar proceso, DateOnly corte, IEnumerable<CierreParaInsolvencia> cierres,
        RatingCategory sinCierreAnterior)
    {
        ArgumentNullException.ThrowIfNull(proceso);
        ArgumentNullException.ThrowIfNull(cierres);
        if (corte < proceso.Inicio || proceso.Estado == InsolvencyProceedingStatus.Agreement) return null;
        var mantenida = cierres.Where(c => c.FechaDeCierre < proceso.Inicio).OrderByDescending(c => c.FechaDeCierre)
            .Select(c => (RatingCategory?)c.Categoria).FirstOrDefault() ?? sinCierreAnterior;
        var fracaso = proceso.Estado == InsolvencyProceedingStatus.Failed && (proceso.FracasoEl is not { } f || f <= corte);
        return new InsolvenciaParaCalificar(fracaso ? InsolvencyProceedingStatus.Failed : InsolvencyProceedingStatus.Negotiating, mantenida);
    }
}
