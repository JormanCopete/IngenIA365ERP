using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0003 (R-22; FR-145; contracts/contabilidad.md; data-model §21 <c>Accounting.Document.ModuleOwned</c>): un comprobante que
/// nació de un hecho de Recaudo o de Tesorería <b>no se reversa ni se edita desde Comprobantes</b>; se corrige en el módulo dueño con un
/// documento nuevo (anulación del recibo, reverso de la aplicación, resultado del giro), que emite su propio hecho. Molde:
/// <see cref="LoDeCarteraSeCorrigeConComprobanteNuevo"/>.
///
/// <para>
/// Árbol que recorre: <see cref="Guardas"/>, los archivos de Contabilidad donde vive la guarda del módulo dueño, con el código del módulo
/// que cada uno debe nombrar. Nace vacía en el Setup y la llena la fase 16 (US8) cuando los hechos <c>REC</c> y <c>TES</c> lleguen a
/// Contabilidad (antes no hay comprobantes de estos módulos que proteger).
/// </para>
/// </summary>
public class LoDeRecaudoYTesoreriaNoSeReversaDesdeComprobantes
{
    /// <summary>(archivo relativo a <c>src/</c>, código del módulo que la guarda tiene que nombrar).</summary>
    private static readonly (string Archivo, string Modulo)[] Guardas =
    [
    ];

    [Fact]
    public void La_guarda_del_modulo_dueno_nombra_a_Recaudo_y_Tesoreria()
    {
        var infracciones = new List<string>();
        foreach (var (archivo, modulo) in Guardas)
        {
            var texto = FuenteSinComentarios.Leer(ReglaSobreFuente.ArchivosNombrados([archivo]).Single());
            if (!texto.Contains(modulo, StringComparison.Ordinal)) infracciones.Add($"{archivo}: no nombra el módulo {modulo}");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Lo de Recaudo o Tesorería se podría reversar desde Comprobantes (FR-145)");
    }
}
