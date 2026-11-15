namespace IngenIA365ERP.Shared.Layout;

/// <summary>
/// 2026-11-12 (revisión comercial, pedido del dueño): el menú lateral del layout de pantalla completa (<see cref="PosLayout"/>), que se
/// abre sobre la pantalla con el botón de su barra. Lo crea el layout y lo comparte en cascada: la pantalla pinta el botón y, mientras
/// el menú está abierto, no deja que sus atajos caigan en lo que quedó tapado; al cerrarse devuelve el foco a su campo. Cerrado al
/// entrar, siempre: el menú no se abre solo por el ancho de la pantalla. (nuevo)
/// </summary>
public sealed class MenuLateralDelPos
{
    public bool Abierto { get; private set; }

    /// <summary>Se dispara cada vez que el menú se abre o se cierra.</summary>
    public event Action? Cambio;

    public void Alternar() => Fijar(!Abierto);

    public void Cerrar() => Fijar(false);

    public void Fijar(bool abierto)
    {
        if (Abierto == abierto) return;
        Abierto = abierto;
        Cambio?.Invoke();
    }
}
