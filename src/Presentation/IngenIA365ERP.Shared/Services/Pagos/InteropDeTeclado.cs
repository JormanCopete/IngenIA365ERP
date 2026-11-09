namespace IngenIA365ERP.Shared.Services.Pagos;

/// <summary>
/// Feature 022 (Principio IX): una llamada a teclado.js que falla (sin el script, sin almacenamiento, circuito cerrado) no impide trabajar
/// —el campo se toca a mano—, pero tampoco se calla: queda en la consola del navegador con su contexto.
/// </summary>
public static class InteropDeTeclado
{
    public static void Avisar(Exception ex, string contexto) =>
        Console.Error.WriteLine($"[Teclado] {contexto}: {ex.GetType().Name}: {ex.Message}");
}
