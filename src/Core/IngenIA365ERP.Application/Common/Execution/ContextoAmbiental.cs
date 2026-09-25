using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// La cooperativa, el actor y el origen de un trabajo que corre <b>fuera</b> de una petición HTTP
/// (feature 012, T5; decisiones-transversales §2.16). Lo fija sólo
/// <see cref="IEjecutorEnCooperativa"/> y lo leen, cuando no hay <c>HttpContext</c>, los accesores
/// singleton que en una petición leen de ella: <c>TenantContextAccessor</c>,
/// <c>CurrentUserService</c> (sólo <c>UserName</c> y <c>TenantId</c>),
/// <c>CurrentCentralUserContextAccessor</c>, <c>IpAddressAccessor</c> (nula),
/// <c>CentralIdentityLogEnricher</c>, la fábrica de <c>ErpTenantInfo</c> y la auditoría.
///
/// <para>
/// Por qué un <see cref="AsyncLocal{T}"/> y no un servicio con ámbito: los accesores y la auditoría
/// son singletons, y lo único que les llega sin cambiarles la firma es el contexto de ejecución.
/// Fluye por cada <c>await</c> y cada <c>Task.Run</c> lanzado dentro, y no se filtra a los flujos
/// que empezaron antes. Nada de fabricar un <c>HttpContext</c> falso: le diría a quien pregunte
/// que hay una petición y un usuario que no existen.
/// </para>
/// </summary>
public static class ContextoAmbiental
{
    private sealed record Estado(TenantDirectoryEntry Cooperativa, Actor Actor, string Origen);

    private static readonly AsyncLocal<Estado?> Actual = new();

    /// <summary>Hay un trabajo de fondo en curso en este flujo.</summary>
    public static bool Activo => Actual.Value is not null;

    /// <summary>La cooperativa del trabajo en curso; nula si no hay contexto.</summary>
    public static TenantDirectoryEntry? Cooperativa => Actual.Value?.Cooperativa;

    /// <summary>Quién actúa en el trabajo en curso; nulo si no hay contexto.</summary>
    public static Actor? Actor => Actual.Value?.Actor;

    /// <summary><c>Mensaje:{id}</c>, <c>Lote:{número}</c> o <c>Tarea:{nombre}</c>; nulo si no hay contexto.</summary>
    public static string? Origen => Actual.Value?.Origen;

    /// <summary>
    /// Fija el contexto para este flujo y lo restaura —al que hubiera antes, no a vacío— al liberar
    /// el resultado. Liberar dos veces no hace nada la segunda.
    /// </summary>
    public static IDisposable Fijar(TenantDirectoryEntry cooperativa, Actor actor, string origen)
    {
        ArgumentNullException.ThrowIfNull(cooperativa);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);

        var anterior = Actual.Value;
        Actual.Value = new Estado(cooperativa, actor, origen);
        return new Restaurador(anterior);
    }

    private sealed class Restaurador(Estado? anterior) : IDisposable
    {
        private int _liberado;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _liberado, 1) == 1) return;
            Actual.Value = anterior;
        }
    }
}
