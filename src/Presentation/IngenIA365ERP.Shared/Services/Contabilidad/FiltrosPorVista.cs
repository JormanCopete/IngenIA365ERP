namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Qué filtros propios muestra cada vista de «Informes contables» (feature 009 E2, US5) y qué pasa
/// con ellos al cambiar de informe. El nivel de detalle es del balance de prueba y del libro mayor;
/// «con terceros», sólo del balance. «Incluir cierre» se muestra en todas: la API lo honra en las seis
/// (decisión 8 del diseño: el cierre queda fuera por defecto y entra sólo si se pide).
///
/// <para>
/// Hasta el 2026-09-20 la casilla de cierre se escondía en «documentos pendientes» y «saldo diario
/// promedio», y al cambiar de vista nada se limpiaba: quien marcaba nivel 4 o «con terceros» en el
/// balance y pasaba al libro diario seguía mandando <c>level=4&amp;withThirdParties=true</c> sin ningún
/// control en pantalla que lo mostrara ni permitiera quitarlo —sólo el contador «Filtros (n)»—.
/// Por eso la regla vive aquí, fuera de la página, y se prueba sola.
/// </para>
/// </summary>
public static class FiltrosPorVista
{
    public static bool MuestraNivel(string vista) =>
        vista is ContabilidadClient.Vistas.BalanceDePrueba or ContabilidadClient.Vistas.LibroMayor;

    public static bool MuestraConTerceros(string vista) => vista == ContabilidadClient.Vistas.BalanceDePrueba;

    /// <summary>
    /// Apaga en el modelo lo que la vista nueva no muestra, para que no viaje a la API a ciegas. Lo
    /// que la vista sí muestra se conserva: cambiar de informe no es perder los filtros.
    /// </summary>
    public static void AjustarAlCambiarVista(FiltrosDeInformeModelo filtros, string vistaNueva)
    {
        if (!MuestraNivel(vistaNueva)) filtros.Level = null;
        if (!MuestraConTerceros(vistaNueva)) filtros.WithThirdParties = false;
    }
}
