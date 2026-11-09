using IngenIA365ERP.Domain.Platform;

namespace IngenIA365ERP.Application.Platform.Modules;

/// <summary>
/// Qué módulos habilitables tiene encendidos cada cooperativa (feature 020, R1). Lee <c>ADM_TenantModules</c> con una caché de 60 s
/// que se invalida al escribir en la misma réplica; en otra réplica el cambio se ve a lo sumo un minuto después. Es la única
/// lectura de esa tabla fuera del comando que la escribe.
/// </summary>
public interface IModulosHabilitados
{
    /// <summary>Los códigos encendidos de la cooperativa (por su <c>PublicId</c>).</summary>
    Task<IReadOnlySet<string>> HabilitadosAsync(Guid tenantPublicId, CancellationToken ct);

    /// <summary>¿Tiene la cooperativa el módulo encendido?</summary>
    Task<bool> EstaHabilitadoAsync(Guid tenantPublicId, string codigo, CancellationToken ct);

    /// <summary>Los encendidos de la cooperativa de la petición o del trabajo en curso; vacío si no hay cooperativa.</summary>
    Task<IReadOnlySet<string>> DeLaCooperativaActualAsync(CancellationToken ct);

    /// <summary>Olvida lo guardado de una cooperativa (lo llama el comando que habilita o deshabilita).</summary>
    void Invalidar(Guid tenantPublicId);
}

/// <summary>
/// Retira de una lista de permisos los de los módulos apagados (feature 020, R1, capa 2): con Transporte apagado, ningún
/// <c>Transport.*</c> llega a la sesión ni a <c>/permissions/mine</c>, aunque el rol los tenga o tenga <c>*</c>.
/// </summary>
public static class RecorteDePermisosPorModulo
{
    public static IReadOnlyCollection<string> Recortar(IReadOnlyCollection<string> permisos, IReadOnlySet<string> habilitados)
    {
        ArgumentNullException.ThrowIfNull(permisos);
        ArgumentNullException.ThrowIfNull(habilitados);
        if (permisos.Count == 0) return permisos;

        var apagados = ModulosHabilitables.Todos.Where(m => !habilitados.Contains(m.Codigo)).ToList();
        if (apagados.Count == 0) return permisos;

        return permisos
            .Where(p => !apagados.Any(m => p.StartsWith(m.PrefijoDePermisos, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
