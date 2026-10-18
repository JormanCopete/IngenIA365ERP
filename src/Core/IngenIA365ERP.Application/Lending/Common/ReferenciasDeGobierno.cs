using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// Una persona como la nombra el gobierno de Cartera (api.md §1.7): su <c>PublicId</c> y su nombre completo. De los miembros de órganos,
/// las respuestas <c>View</c> sólo llevan esto (C84); el documento sólo va en las rutas <c>Read</c> que lo piden. (nuevo)
/// </summary>
public sealed record PersonaRefDto(Guid PersonPublicId, string FullName);

/// <summary>Un usuario como lo nombran las respuestas (api.md §1.7): su <c>PublicId</c> y su nombre. (nuevo)</summary>
public sealed record UsuarioRefDto(Guid UserPublicId, string Name);

/// <summary>Las búsquedas en bloque de personas y usuarios que comparten las consultas del gobierno de Cartera. (nuevo)</summary>
public static class ReferenciasDeGobierno
{
    /// <summary>Las personas de <paramref name="ids"/> (también eliminadas: lo histórico se sigue nombrando), por su Id interno.</summary>
    public static async Task<IReadOnlyDictionary<int, PersonaRefDto>> PersonasAsync(IApplicationDbContext db, IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<int, PersonaRefDto>();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id)).ToListAsync(ct);
        return personas.ToDictionary(p => p.Id, p => new PersonaRefDto(p.PublicId, Nombre(p)));
    }

    /// <summary>Los usuarios de <paramref name="ids"/> por su Id interno (<c>SEC_Users.Id</c>).</summary>
    public static async Task<IReadOnlyDictionary<int, UsuarioRefDto>> UsuariosAsync(IApplicationDbContext db, IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<int, UsuarioRefDto>();
        return await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => lista.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new UsuarioRefDto(u.PublicId, u.Email ?? u.Username), ct);
    }

    /// <summary>Los usuarios de los <paramref name="ids"/> que no son nulos (una gestión o una asignación de sistema no tiene usuario).</summary>
    public static Task<IReadOnlyDictionary<int, UsuarioRefDto>> UsuariosAsync(IApplicationDbContext db, IEnumerable<int?> ids, CancellationToken ct) =>
        UsuariosAsync(db, ids.Where(i => i is not null).Select(i => i!.Value), ct);

    /// <summary>El usuario de <paramref name="id"/>, o nulo si no hay (lo hizo el sistema) o no se encontró.</summary>
    public static UsuarioRefDto? Usuario(IReadOnlyDictionary<int, UsuarioRefDto> usuarios, int? id) =>
        id is { } i && usuarios.TryGetValue(i, out var u) ? u : null;

    /// <summary>El nombre completo de una persona natural o la razón social de una jurídica.</summary>
    public static string Nombre(Person p) =>
        string.IsNullOrWhiteSpace(p.BusinessName) || !string.IsNullOrWhiteSpace(p.FirstName) ? NombreDePersona.Completo(p) : p.BusinessName!;
}
