using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// Implementación de <see cref="ILectorDeParametros"/> (feature 012, T21, T070; data-model §4.1). Es, con
/// <c>AddParameterVersionCommandHandler</c>, el único código que toca <c>COR_ParameterVersions</c>
/// (<c>LosParametrosSeLeenEnUnSoloSitio</c>). Scoped: memoriza cada lectura por petición, así que un documento de
/// cien líneas no consulta cien veces la misma clave.
/// </summary>
/// <remarks>
/// <paramref name="entrega"/> es la entrega con que se interpretan los valores de su línea (por defecto, la vigente de cada línea):
/// las pruebas de una entrega anterior del comercio (I4: sin <c>Peps</c>) la fijan, como <c>EfectosDeClase</c>. Feature 013 (T43):
/// es una <see cref="EntregaDeModulo"/>; <see cref="EntregaDelComercio"/> se convierte sola.
/// </remarks>
public sealed class LectorDeParametros(IApplicationDbContext db, EntregaDeModulo? entrega = null) : ILectorDeParametros
{
    private readonly Dictionary<(string, string, ParameterScopeKind, int, DateOnly, DateTime?), Result<ValorDeParametro>> _memoria = [];
    private readonly Dictionary<(string, string), IReadOnlyList<VigenciaDeParametro>> _vigenciasPorClave = [];
    private readonly Dictionary<(string, string, DateTime), IReadOnlyList<VigenciaDeParametro>> _conocidasPorClave = [];

    public Task<Result<ValorDeParametro>> LeerAsync(string modulo, string clave, DateOnly fecha,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0, CancellationToken ct = default) =>
        LeerAsync(modulo, clave, fecha, null, ambito, ambitoId, ct);

    public async Task<Result<ValorDeParametro>> LeerAsync(string modulo, string clave, DateOnly fecha, DateTime? informacionHasta,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0, CancellationToken ct = default)
    {
        var definicion = CatalogoDeParametros.Buscar(modulo, clave);
        if (definicion is null)
            return Result.Failure<ValorDeParametro>(ErroresDeParametros.ClaveInexistente(modulo, clave));

        // Un ámbito que la clave no admite no puede tener vigencias: se lee el general.
        if (ambito == ParameterScopeKind.None || !definicion.AdmiteAmbito(ambito))
            (ambito, ambitoId) = (ParameterScopeKind.None, 0);

        var llave = (definicion.Modulo, definicion.Clave, ambito, ambitoId, fecha, informacionHasta);
        if (_memoria.TryGetValue(llave, out var memorizado)) return memorizado;

        var vigencias = informacionHasta is { } r
            ? await ConocidasEnAsync(definicion, r, ct)
            : await VigenciasDeLaClaveAsync(definicion, ct);
        var vigente = VigenteA(vigencias, fecha, ambito, ambitoId)
                      ?? (ambito == ParameterScopeKind.None ? null : VigenteA(vigencias, fecha, ParameterScopeKind.None, 0));

        Result<ValorDeParametro> resultado;
        if (vigente is null)
        {
            var defecto = definicion.Interpretar(definicion.DefectoSeguro, entrega);
            resultado = Result.Success(new ValorDeParametro(definicion, defecto.Texto ?? definicion.DefectoSeguro, defecto.Valor, null));
        }
        else
        {
            // Lo guardado que la definición no admite (un valor de una entrega posterior, un catálogo que cambió)
            // es un error nombrado: caer al defecto escondería una configuración que alguien hizo a propósito.
            var interpretado = definicion.Interpretar(vigente.Value, entrega);
            resultado = interpretado.Admitido
                ? Result.Success(new ValorDeParametro(definicion, interpretado.Texto!, interpretado.Valor, vigente))
                : Result.Failure<ValorDeParametro>(ErroresDeParametros.ValorNoAdmitido(definicion, vigente.Value, entrega));
        }

        _memoria[llave] = resultado;
        return resultado;
    }

    public async Task<IReadOnlyList<VigenciaDeParametro>> VigenciasAsync(string? modulo = null, string? clave = null, CancellationToken ct = default)
    {
        // Explícito además del filtro global: una vigencia futura retirada con motivo no cuenta nunca.
        var consulta = db.ParameterVersions.AsNoTracking().Where(v => !v.IsDeleted);
        if (!string.IsNullOrWhiteSpace(modulo))
        {
            var m = modulo.Trim().ToUpperInvariant();
            consulta = consulta.Where(v => v.Module == m);
        }
        if (!string.IsNullOrWhiteSpace(clave))
        {
            var k = clave.Trim();
            consulta = consulta.Where(v => v.Key == k);
        }

        return await consulta
            .OrderBy(v => v.Module).ThenBy(v => v.Key).ThenBy(v => v.ScopeKind).ThenBy(v => v.ScopeId).ThenBy(v => v.ValidFrom)
            .Select(v => new VigenciaDeParametro(v.PublicId, v.Module, v.Key, v.ScopeKind, v.ScopeId, v.Value, v.ValidFrom, v.ValidTo,
                v.Reason, v.LegalSource, v.CreatedBy, v.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>La vigencia de (ámbito, entidad) que cubre la fecha; si hubiera dos, la que empezó después.</summary>
    public static VigenciaDeParametro? VigenteA(IEnumerable<VigenciaDeParametro> vigencias, DateOnly fecha, ParameterScopeKind ambito, int ambitoId) =>
        vigencias
            .Where(v => v.ScopeKind == ambito && v.ScopeId == ambitoId && v.VigenteEn(fecha))
            .OrderByDescending(v => v.ValidFrom)
            .FirstOrDefault();

    /// <summary>
    /// Las vigencias de la clave tal como se conocían en R (feature 013, T38): las creadas hasta R, incluidas las dadas de baja
    /// después de R (una baja posterior a R no cuenta), con el <c>ValidTo</c> guardado ignorado y derivado de la vigencia siguiente
    /// del mismo ámbito y entidad conocida en R (la víspera de su <c>ValidFrom</c>; sin siguiente, abierta).
    /// </summary>
    private async Task<IReadOnlyList<VigenciaDeParametro>> ConocidasEnAsync(DefinicionDeParametro definicion, DateTime r, CancellationToken ct)
    {
        var llave = (definicion.Modulo, definicion.Clave, r);
        if (_conocidasPorClave.TryGetValue(llave, out var cacheadas)) return cacheadas;

        var filas = await db.ParameterVersions.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.Module == definicion.Modulo && v.Key == definicion.Clave && v.CreatedAt <= r
                        && (!v.IsDeleted || (v.DeletedAt != null && v.DeletedAt > r)))
            .Select(v => new VigenciaDeParametro(v.PublicId, v.Module, v.Key, v.ScopeKind, v.ScopeId, v.Value, v.ValidFrom, v.ValidTo,
                v.Reason, v.LegalSource, v.CreatedBy, v.CreatedAt))
            .ToListAsync(ct);

        var conocidas = filas
            .GroupBy(v => (v.ScopeKind, v.ScopeId))
            .SelectMany(serie =>
            {
                var ordenadas = serie.OrderBy(v => v.ValidFrom).ThenBy(v => v.CreatedAt).ToList();
                return ordenadas.Select((v, i) =>
                {
                    var siguiente = ordenadas.Skip(i + 1).FirstOrDefault(s => s.ValidFrom > v.ValidFrom);
                    return v with { ValidTo = siguiente?.ValidFrom.AddDays(-1) };
                });
            })
            .ToList();

        _conocidasPorClave[llave] = conocidas;
        return conocidas;
    }

    private async Task<IReadOnlyList<VigenciaDeParametro>> VigenciasDeLaClaveAsync(DefinicionDeParametro definicion, CancellationToken ct)
    {
        var llave = (definicion.Modulo, definicion.Clave);
        if (_vigenciasPorClave.TryGetValue(llave, out var cacheadas)) return cacheadas;
        var vigencias = await VigenciasAsync(definicion.Modulo, definicion.Clave, ct);
        _vigenciasPorClave[llave] = vigencias;
        return vigencias;
    }
}
