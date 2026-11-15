using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.Common;

/// <summary>
/// Con qué persona se puede vincular un usuario. La segregación de Cartera compara personas: si dos usuarios comparten una, quien radica y
/// quien decide parecen el mismo aunque sean dos cuentas, y al revés, una persona con dos cuentas podría aprobarse a sí misma si se le vincula
/// otra. Una persona es de un solo usuario vivo; la comprobación es de la aplicación, no hay índice único. (nuevo)
/// </summary>
internal static class PersonaDelUsuario
{
    /// <summary>El <c>Id</c> interno de la persona: <c>Core.Person.NotFound</c> si no existe o está eliminada, y <see cref="UserErrorCodes.PersonAlreadyLinked"/> si es de otro usuario.</summary>
    public static async Task<Result<int>> ResolverAsync(IApplicationDbContext db, int usuarioId, Guid personaPublicId, CancellationToken ct)
    {
        var personaId = await db.People.AsNoTracking()
            .Where(p => p.PublicId == personaPublicId && !p.IsDeleted)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (personaId is null) return Result.Failure<int>("Core.Person.NotFound", "La persona no existe en esta cooperativa.");

        var otro = await OtroUsuarioConLaPersonaAsync(db, usuarioId, personaId.Value, ct);
        return otro is null
            ? Result.Success(personaId.Value)
            : Result.Failure<int>(UserErrorCodes.PersonAlreadyLinked, $"Esa persona ya está vinculada al usuario {otro}.");
    }

    /// <summary>El correo del otro usuario vivo vinculado a la persona, o nulo si no hay.</summary>
    public static Task<string?> OtroUsuarioConLaPersonaAsync(IApplicationDbContext db, int usuarioId, int personaId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.PersonId == personaId && u.Id != usuarioId && !u.IsDeleted)
            .Select(u => u.Email ?? u.Username)
            .FirstOrDefaultAsync(ct);
}
