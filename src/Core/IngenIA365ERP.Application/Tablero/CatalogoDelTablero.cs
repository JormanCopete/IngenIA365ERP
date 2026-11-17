using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.Application.Tablero;

/// <summary>Un indicador como lo ve la pantalla del tablero o la del rol (sin calcular).</summary>
public sealed record IndicadorDisponibleDto(
    string Codigo,
    string Titulo,
    string Grupo,
    string Descripcion,
    bool AdmiteSoloLoMio,
    TipoDeIndicador Tipo,
    FormatoDeIndicador Formato,
    string? Ruta,
    string Permiso);

/// <summary>
/// Los indicadores y pendientes registrados, y los que puede ver quien hace la petición (por sus permisos, ya recortados por los
/// módulos apagados de la cooperativa). Es la única puerta: ninguna consulta del tablero calcula un indicador sin pasar por aquí.
/// </summary>
public sealed class CatalogoDelTablero(
    IEnumerable<IIndicadorDelTablero> indicadores,
    IEnumerable<IPendienteDelTablero> pendientes,
    ICurrentUserPermissions permisos,
    ICurrentUserService usuario,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private IReadOnlySet<string>? _permisos;
    private bool _maestro;

    public IReadOnlyList<IIndicadorDelTablero> Indicadores { get; } = indicadores.OrderBy(i => i.Definicion.Grupo).ThenBy(i => i.Definicion.Titulo).ToList();

    public IReadOnlyList<IPendienteDelTablero> Pendientes { get; } = pendientes.OrderBy(p => p.Definicion.Grupo).ThenBy(p => p.Definicion.Titulo).ToList();

    public IIndicadorDelTablero? Buscar(string codigo) =>
        Indicadores.FirstOrDefault(i => string.Equals(i.Definicion.Codigo, codigo, StringComparison.Ordinal));

    public async Task<bool> PuedeVerAsync(string permiso, CancellationToken ct)
    {
        if (_permisos is null)
        {
            _maestro = permisos.EsMaestroGlobal;
            _permisos = new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        }
        return _maestro || _permisos.Contains(permiso);
    }

    public async Task<IReadOnlyList<IIndicadorDelTablero>> VisiblesAsync(CancellationToken ct)
    {
        var visibles = new List<IIndicadorDelTablero>();
        foreach (var i in Indicadores)
            if (await PuedeVerAsync(i.Definicion.Permiso, ct)) visibles.Add(i);
        return visibles;
    }

    public async Task<IReadOnlyList<IPendienteDelTablero>> PendientesVisiblesAsync(CancellationToken ct)
    {
        var visibles = new List<IPendienteDelTablero>();
        foreach (var p in Pendientes)
            if (await PuedeVerAsync(p.Definicion.Permiso, ct)) visibles.Add(p);
        return visibles;
    }

    /// <summary>A quién se le calcula: el usuario de la cooperativa (<c>SEC_Users.Id</c>), el nombre de auditoría y la fecha local.</summary>
    public async Task<ContextoDeIndicador> ContextoAsync(bool soloLoMio, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        return new ContextoDeIndicador(soloLoMio, actor.UserId, usuario.UserName, reloj.HoyLocal);
    }

    public static IndicadorDisponibleDto Dto(DefinicionDeIndicador d) =>
        new(d.Codigo, d.Titulo, d.Grupo, d.Descripcion, d.AdmiteSoloLoMio, d.Tipo, d.Formato, d.Ruta, d.Permiso);
}
