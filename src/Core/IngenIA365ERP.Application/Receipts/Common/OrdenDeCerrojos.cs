namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// Lleva, dentro de una unidad de trabajo, qué niveles de Recaudo y de Tesorería ya se tomaron, para que los dos cerrojos respeten el orden
/// global de data-model §0.5 (feature 015, T0038; R-12; FR-170; CH16): <b>Recaudo antes que Tesorería</b>, cada módulo en el orden de sus
/// niveles, y la numeración al final. Es de ámbito (una instancia por petición o por unidad del proceso): la comparten
/// <c>CerrojoDeRecaudo</c> y <c>CerrojoDeTesoreria</c>.
///
/// <para>
/// Un nivel igual al último tomado se admite (otra tabla del mismo nivel, o la misma con filas nuevas); uno menor lanza con un mensaje que dice
/// cuál se pidió después de cuál. Pedir una fila de Recaudo después de una de Tesorería lanza: es la dirección Tesorería → Recaudo, que tiene que
/// pasar antes por <c>TomarCerrojosAsync</c> del puerto. La numeración (<see cref="Numeracion"/>) siempre se admite y, una vez tomada, cierra el
/// orden: nada se bloquea después. Un error de orden es un defecto del programa, no del usuario. (nuevo)
/// </para>
/// </summary>
public sealed class OrdenDeCerrojos
{
    /// <summary>La posición de la numeración: la última de todas.</summary>
    public const int Numeracion = 1000;

    private const int BaseDeTesoreria = 100;

    private int _ultimo;
    private string _ultimoNombre = "ninguno";

    /// <summary>La posición global de un nivel de Recaudo.</summary>
    public static int DeRecaudo(NivelDeRecaudo nivel) => (int)nivel;

    /// <summary>La posición global de un nivel de Tesorería: siempre después de todos los de Recaudo.</summary>
    public static int DeTesoreria(Treasury.Common.NivelDeTesoreria nivel) => BaseDeTesoreria + (int)nivel;

    /// <summary>Anota el nivel; lanza si llega fuera de orden.</summary>
    public void Tomar(int posicion, string nombre)
    {
        if (_ultimo == Numeracion && posicion != Numeracion)
            throw new InvalidOperationException($"El cerrojo pidió {nombre} después de la numeración: la numeración va al final (data-model §0.5).");
        if (posicion < _ultimo)
        {
            var direccion = posicion < BaseDeTesoreria && _ultimo >= BaseDeTesoreria && _ultimo != Numeracion
                ? " Una fila de Recaudo después de una de Tesorería: llame antes a TomarCerrojosAsync del puerto (CH16)."
                : string.Empty;
            throw new InvalidOperationException($"El cerrojo pidió {nombre} después de {_ultimoNombre}: va contra el orden fijo de data-model §0.5.{direccion}");
        }
        _ultimo = posicion;
        _ultimoNombre = nombre;
    }

    /// <summary>La última posición tomada (cero si ninguna).</summary>
    public int Ultimo => _ultimo;
}
