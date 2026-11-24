namespace IngenIA365ERP.Application.Identity.Profile.Preferencias;

/// <summary>
/// Preferencias de interfaz del usuario autenticado, ya resueltas para la
/// cooperativa activa: las de apariencia salen del ámbito global y las de
/// navegación del ámbito de esa cooperativa.
/// </summary>
/// <param name="Tema">claro | oscuro. <c>null</c> = seguir al sistema operativo.</param>
/// <param name="Densidad">comoda | compacta.</param>
/// <param name="Escala">normal | grande | muy-grande.</param>
/// <param name="Contraste">normal | alto.</param>
/// <param name="PaginaInicio">Ruta a la que entrar tras iniciar sesión.</param>
/// <param name="Favoritos">Rutas marcadas por el usuario, en su orden.</param>
/// <param name="Accesos">2026-11-10: los accesos directos del tablero, en su orden (vacío si no hay).</param>
/// <param name="EnterAvanza">Feature 022 (T031): si Enter pasa al siguiente campo. Sin fila guardada, encendido (Q3).</param>
/// <param name="TamanoTextoEstadoDeCuenta">2026-11-16: pequeno | normal | grande en el estado de cuenta y el detalle de la caja.</param>
/// <param name="DecimalesEstadoDeCuenta">2026-11-16: si los valores del estado de cuenta llevan centavos.</param>
/// <param name="ImpresionReciboCaja">2026-11-23: preguntar | tirilla | carta, cómo imprime la caja el recibo al confirmarlo.</param>
public sealed record PreferenciasUsuarioDto(
    string? Tema,
    string? Densidad,
    string? Escala,
    string? Contraste,
    string? PaginaInicio,
    IReadOnlyList<string> Favoritos,
    bool EnterAvanza = true,
    IReadOnlyList<AccesoDirectoDto>? Accesos = null,
    IReadOnlyList<string>? ColumnasEstadoDeCuenta = null,
    IReadOnlyList<string>? ColumnasLibroAuxiliar = null,
    bool LibroAuxiliarSoloMovimiento = false,
    string TamanoTextoEstadoDeCuenta = "normal",
    bool DecimalesEstadoDeCuenta = false,
    string ImpresionReciboCaja = "preguntar");

/// <summary>Un acceso directo del tablero: la ruta de la opción y, si el usuario los eligió, su nombre, ícono y color.</summary>
public sealed record AccesoDirectoDto(string Ruta, string? Nombre = null, string? Icono = null, string? Color = null);
