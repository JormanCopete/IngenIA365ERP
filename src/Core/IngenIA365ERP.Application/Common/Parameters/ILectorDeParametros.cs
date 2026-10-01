using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// Una vigencia guardada, tal como la ven las consultas (sin la entidad). (nuevo)
/// </summary>
public sealed record VigenciaDeParametro(
    Guid PublicId,
    string Module,
    string Key,
    ParameterScopeKind ScopeKind,
    int ScopeId,
    string Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? LegalSource,
    string? CreatedBy,
    DateTime CreatedAt)
{
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>
/// Un valor de parámetro leído a una fecha: el texto canónico, el valor tipado según la definición y de dónde
/// salió (<see cref="Vigencia"/> nula = el defecto seguro). (nuevo)
/// </summary>
public sealed record ValorDeParametro(DefinicionDeParametro Definicion, string Texto, object? Valor, VigenciaDeParametro? Vigencia)
{
    public bool EsDefecto => Vigencia is null;

    /// <summary>El valor tipado; <c>default</c> si la clave admite vacío y está vacía.</summary>
    public T? Como<T>() => Valor is T t ? t : default;
}

/// <summary>
/// El único lector de los parámetros con vigencia (feature 012, T21, T070; <c>LosParametrosSeLeenEnUnSoloSitio</c>).
/// Lee a una fecha con caída ámbito → general → defecto seguro de la <see cref="DefinicionDeParametro"/>; un valor
/// guardado que la definición no admite es <c>Parameters.ValueNotAllowed</c>, nunca el defecto. Memoriza por
/// petición (se registra Scoped). (nuevo)
/// </summary>
public interface ILectorDeParametros
{
    /// <summary>
    /// El valor de (módulo, clave) a <paramref name="fecha"/>. Con <paramref name="ambito"/> distinto de
    /// <see cref="ParameterScopeKind.None"/> busca primero la excepción de esa entidad (<paramref name="ambitoId"/>);
    /// si la clave no admite ese ámbito, lee el general. Clave fuera del catálogo: <c>Parameters.KeyNotFound</c>.
    /// </summary>
    Task<Result<ValorDeParametro>> LeerAsync(string modulo, string clave, DateOnly fecha,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0, CancellationToken ct = default);

    /// <summary>
    /// Todas las vigencias vivas guardadas, filtradas por módulo y clave si vienen (para la pantalla de parámetros
    /// y el historial). No interpreta los valores.
    /// </summary>
    Task<IReadOnlyList<VigenciaDeParametro>> VigenciasAsync(string? modulo = null, string? clave = null, CancellationToken ct = default);
}

public static class LectorDeParametrosExtensions
{
    /// <summary>El valor tipado de (módulo, clave) a la fecha; el error del lector si no se puede leer.</summary>
    public static async Task<Result<T?>> LeerComoAsync<T>(this ILectorDeParametros lector, string modulo, string clave, DateOnly fecha,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0, CancellationToken ct = default)
    {
        var r = await lector.LeerAsync(modulo, clave, fecha, ambito, ambitoId, ct);
        return r.IsFailure ? Result.Failure<T?>(r.Error) : Result.Success(r.Value.Como<T>());
    }
}
