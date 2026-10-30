using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Personas;

/// <summary>
/// El único sitio que encuentra personas por su documento (feature 017, research R3). Desde que la
/// llave de la persona es el usuario derivado (D-13), el número solo puede corresponder a dos personas
/// (CC 123456 y CE 123456): con tipo se busca exacto por usuario; sin tipo, sirve si hay una sola y si
/// hay varias se responde <c>Person.DocumentAmbiguous</c> pidiendo el tipo. Lo vigila
/// <c>NadieBuscaPersonasPorNumeroSolo</c>.
/// </summary>
public static class ResolutorDePersonaPorDocumento
{
    public const string CodigoAmbiguo = "Person.DocumentAmbiguous";

    /// <summary>
    /// El usuario de un tipo (código de persona «C», «CE»… o de acceso «CC», «PPT»…) y un número.
    /// Nulo si el tipo viene vacío.
    /// </summary>
    public static string? UsuarioDe(string? tipo, string numero)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            return null;
        var t = tipo.Trim().ToUpperInvariant();
        var deAcceso = TiposDeDocumento.PorCodigo(t);
        var codigoDePersona = TiposDeDocumento.PorCodigoDePersona(t) is not null ? t : deAcceso?.CodigoDePersona ?? t;
        return UsuarioDeDocumento.DerivarParaPersona(codigoDePersona, numero);
    }

    /// <summary>
    /// Filtra una consulta de personas por documento: por usuario si hay tipo, por número normalizado
    /// si no (puede devolver más de una; el que llama decide con <see cref="Elegir{T}"/>).
    /// </summary>
    public static IQueryable<Person> Filtrar(IQueryable<Person> personas, string? tipo, string? numero)
    {
        var n = UsuarioDeDocumento.NormalizarNumero(numero);
        var usuario = UsuarioDe(tipo, n);
        if (usuario is not null)
            return personas.Where(p => p.Username == usuario);
        var crudo = numero?.Trim() ?? string.Empty;
        return personas.Where(p => p.TaxId == n || p.TaxId == crudo);
    }

    /// <summary>
    /// De lo encontrado, la persona si es una sola; <c>null</c> si no hay; error de ambigüedad con los
    /// tipos que existen si hay varias.
    /// </summary>
    public static Result<T?> Elegir<T>(IReadOnlyList<T> encontradas, Func<T, string> tipoDe) where T : class
    {
        if (encontradas.Count == 0) return Result.Success<T?>(null);
        if (encontradas.Count == 1) return Result.Success<T?>(encontradas[0]);
        var tipos = string.Join(", ", encontradas.Select(tipoDe).Distinct().Select(NombreDelTipo));
        return Result.Failure<T?>(new Error(CodigoAmbiguo,
            $"Hay varias personas con ese número de documento ({tipos}). Indique el tipo de documento."));
    }

    /// <summary>Busca una persona viva por documento. Sin tipo y con varias, responde la ambigüedad.</summary>
    public static async Task<Result<Person?>> BuscarAsync(
        IApplicationDbContext context, string? tipo, string? numero, CancellationToken ct, bool soloLectura = true)
    {
        var consulta = Filtrar(context.People.Where(p => !p.IsDeleted), tipo, numero);
        if (soloLectura) consulta = consulta.AsNoTracking();
        var encontradas = await consulta.Take(5).ToListAsync(ct);
        return Elegir(encontradas, p => p.IdType);
    }

    /// <summary>
    /// Para importaciones: arma un diccionario por clave de documento sin lanzar ante números repetidos.
    /// La clave es el usuario cuando la fila trae tipo y el número normalizado cuando no; un número
    /// que corresponde a varias personas queda marcado como ambiguo y la fila que lo use sin tipo se
    /// rechaza con <see cref="CodigoAmbiguo"/>.
    /// </summary>
    public static IndiceDePersonas<T> Indexar<T>(IEnumerable<T> personas, Func<T, string> usuarioDe, Func<T, string> numeroDe) =>
        new(personas, usuarioDe, numeroDe);

    private static string NombreDelTipo(string idType) =>
        TiposDeDocumento.PorCodigoDePersona(idType)?.Codigo ?? idType;
}

/// <summary>Un índice de personas por documento para importaciones (feature 017, research R3).</summary>
public sealed class IndiceDePersonas<T>
{
    private readonly Dictionary<string, T> _porUsuario = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<T>> _porNumero = new(StringComparer.Ordinal);

    internal IndiceDePersonas(IEnumerable<T> personas, Func<T, string> usuarioDe, Func<T, string> numeroDe)
    {
        foreach (var p in personas)
        {
            _porUsuario.TryAdd(usuarioDe(p), p);
            var n = UsuarioDeDocumento.NormalizarNumero(numeroDe(p));
            if (!_porNumero.TryGetValue(n, out var lista))
                _porNumero[n] = lista = [];
            lista.Add(p);
        }
    }

    /// <summary>
    /// La persona de un tipo (opcional) y un número. <paramref name="ambigua"/> es true si no traía tipo
    /// y el número corresponde a varias personas.
    /// </summary>
    public bool TryGet(string? tipo, string? numero, out T persona, out bool ambigua)
    {
        ambigua = false;
        var n = UsuarioDeDocumento.NormalizarNumero(numero);
        var usuario = ResolutorDePersonaPorDocumento.UsuarioDe(tipo, n);
        if (usuario is not null)
            return _porUsuario.TryGetValue(usuario, out persona!);

        if (_porNumero.TryGetValue(n, out var lista))
        {
            if (lista.Count == 1)
            {
                persona = lista[0];
                return true;
            }
            ambigua = true;
        }
        persona = default!;
        return false;
    }

    /// <summary>Como <see cref="TryGet"/> sin tipo; para quien no distingue ambigüedad de ausencia.</summary>
    public bool TryGetValue(string? numero, out T persona) => TryGet(null, numero, out persona, out _);

    /// <summary>Si existe (sin tipo: una sola o varias).</summary>
    public bool ContainsKey(string? numero) => _porNumero.ContainsKey(UsuarioDeDocumento.NormalizarNumero(numero));
}
