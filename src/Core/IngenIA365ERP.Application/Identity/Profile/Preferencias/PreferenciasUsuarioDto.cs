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
public sealed record PreferenciasUsuarioDto(
    string? Tema,
    string? Densidad,
    string? Escala,
    string? Contraste,
    string? PaginaInicio,
    IReadOnlyList<string> Favoritos);
