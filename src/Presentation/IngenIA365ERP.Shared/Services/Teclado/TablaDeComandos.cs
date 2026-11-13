namespace IngenIA365ERP.Shared.Services.Teclado;

/// <summary>
/// 2026-11-12 (revisión comercial, S5; pedido del dueño): un comando de una pantalla de trabajo —su tecla, lo que dice la barra de
/// atajos, qué hace y cuándo aplica—. La misma fila la usan el despachador de teclas de la pantalla y el botón de la barra
/// (<c>BarraDeAtajos</c> con <c>Comandos</c>), así una tecla y su botón no pueden decir cosas distintas.
/// <para>
/// <paramref name="Tecla"/> es el nombre con que llega la tecla (<c>F2</c>, <c>Delete</c>, <c>Escape</c>, <c>Ctrl+Alt+M</c>);
/// <paramref name="TeclaVisible"/> lo que se pinta si es otro («Supr», «Esc»). Sin <paramref name="Accion"/> es una <b>pista</b>
/// («3*» multiplica): se muestra pero no se pulsa. <paramref name="Aplica"/> dice si el comando aplica ahora (sin él, siempre); cuando
/// no aplica, el botón queda deshabilitado y la tecla no hace nada, salvo que <paramref name="PorQueNo"/> diga por qué: entonces la
/// pantalla lo avisa en vez de quedarse callada.
/// </para>
/// </summary>
public sealed record ComandoDeTeclado(
    string Tecla,
    string Etiqueta,
    Func<Task>? Accion = null,
    Func<bool>? Aplica = null,
    string? TeclaVisible = null,
    Func<string?>? PorQueNo = null)
{
    /// <summary>Una pista de la barra: se ve, no se pulsa.</summary>
    public static ComandoDeTeclado Pista(string tecla, string etiqueta) => new(tecla, etiqueta);

    public bool EsPista => Accion is null;

    /// <summary>Lo que se pinta en la tecla.</summary>
    public string Rotulo => TeclaVisible ?? Tecla;

    /// <summary>Si se puede ejecutar ahora: tiene acción y aplica.</summary>
    public bool Disponible => Accion is not null && (Aplica?.Invoke() ?? true);

    /// <summary>Por qué no se puede ahora (para el aviso y el título del botón), o nulo.</summary>
    public string? Motivo => Disponible ? null : PorQueNo?.Invoke();
}

/// <summary>Lo que pasó al pedir un comando por su tecla.</summary>
public enum EstadoDeComando
{
    /// <summary>Se ejecutó.</summary>
    Ejecutado,

    /// <summary>Existe pero no aplica ahora (o es una pista).</summary>
    NoAplica,

    /// <summary>La tabla no tiene esa tecla.</summary>
    Desconocido,
}

/// <summary>El resultado de <see cref="TablaDeComandos.EjecutarAsync"/>: el estado y, si no aplica y el comando lo dice, por qué.</summary>
public readonly record struct EjecucionDeComando(EstadoDeComando Estado, string? Aviso = null);

/// <summary>
/// La tabla de comandos de una pantalla, en el orden en que los pinta la barra. Una tecla aparece una sola vez: dos filas con la misma
/// tecla serían dos comportamientos para una tecla, que es justo lo que la tabla evita. Pura y sin estado propio: las condiciones y las
/// acciones son de la pantalla. (nuevo)
/// </summary>
public sealed class TablaDeComandos
{
    private readonly IReadOnlyList<ComandoDeTeclado> _comandos;
    private readonly Dictionary<string, ComandoDeTeclado> _porTecla;

    public TablaDeComandos(IEnumerable<ComandoDeTeclado> comandos)
    {
        _comandos = [.. comandos];
        _porTecla = new Dictionary<string, ComandoDeTeclado>(StringComparer.Ordinal);
        foreach (var c in _comandos)
        {
            if (string.IsNullOrWhiteSpace(c.Tecla)) throw new ArgumentException("Un comando sin tecla no se puede despachar.", nameof(comandos));
            if (!_porTecla.TryAdd(c.Tecla, c)) throw new ArgumentException($"La tecla «{c.Tecla}» está dos veces en la tabla de comandos.", nameof(comandos));
        }
    }

    /// <summary>Los comandos en el orden de la barra.</summary>
    public IReadOnlyList<ComandoDeTeclado> Comandos => _comandos;

    /// <summary>El comando de esa tecla, o nulo.</summary>
    public ComandoDeTeclado? Buscar(string tecla) => _porTecla.GetValueOrDefault(tecla);

    /// <summary>Ejecuta el comando de la tecla si aplica. Lo usan por igual el teclado y el botón de la barra.</summary>
    public async Task<EjecucionDeComando> EjecutarAsync(string tecla)
    {
        if (Buscar(tecla) is not { } comando) return new(EstadoDeComando.Desconocido);
        if (!comando.Disponible) return new(EstadoDeComando.NoAplica, comando.Motivo);
        await comando.Accion!();
        return new(EstadoDeComando.Ejecutado);
    }
}
