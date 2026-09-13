namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Bandera de «estoy trabajando» para una pantalla o una zona de la pantalla, pensada para
/// enlazarse a <c>IndicadorDeCarga</c>. Se enciende con <see cref="Iniciar"/> y se apaga sola al
/// salir del bloque <c>using</c>, pase lo que pase:
/// <code>
/// private readonly EstadoDeCarga _carga = new();
/// private async Task CargarAsync()
/// {
///     using var carga = _carga.Iniciar();
///     _items = await Http.GetListAsync&lt;Item&gt;("/api/...");
/// }
/// </code>
/// Cuenta anidamientos: dos cargas solapadas (una lista y sus catálogos con
/// <c>Task.WhenAll</c>) mantienen el indicador hasta que termina la última. Con una bandera
/// booleana suelta la primera en terminar lo apagaba con la otra todavía en vuelo, y un
/// <c>return</c> temprano o una excepción lo dejaban encendido para siempre.
/// </summary>
public sealed class EstadoDeCarga
{
    private int _enCurso;

    /// <summary>Verdadero mientras haya al menos un trabajo iniciado y no terminado.</summary>
    public bool Activa => _enCurso > 0;

    /// <summary>Marca el comienzo de un trabajo; el objeto devuelto lo da por terminado al desecharse.</summary>
    public IDisposable Iniciar()
    {
        _enCurso++;
        return new Fin(this);
    }

    private sealed class Fin(EstadoDeCarga dueno) : IDisposable
    {
        private bool _hecho;
        public void Dispose()
        {
            if (_hecho) return;
            _hecho = true;
            dueno._enCurso = Math.Max(0, dueno._enCurso - 1);
        }
    }
}
