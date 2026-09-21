using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Services;

/// <summary>
/// Cómo se crea una persona, en un solo sitio (feature 008). Lo usan
/// <c>CreatePersonCommand</c> y los compuestos que registran persona y rol en una sola
/// transacción; por eso <b>agrega al contexto sin guardar</b>: el que llama decide cuándo
/// va el único <c>SaveChangesAsync</c>.
///
/// <para>
/// El documento se busca <b>incluyendo eliminadas</b>. El índice único
/// <c>UK_COR_People_TaxId</c> no distingue una fila con <c>IsDeleted</c>: hasta el
/// 2026-09-13 la comprobación las ignoraba, la inserción violaba el índice y el usuario
/// veía un error genérico. Ahora una eliminada responde <c>Person.TaxIdDeleted</c> con la
/// fecha, y quien tiene permiso de eliminar puede restaurarla (<c>RestorePersonCommand</c>).
/// </para>
/// </summary>
public sealed class PersonFactory(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
{
    /// <summary>
    /// Nombre del índice único del documento. Es lo que traen los mensajes de PostgreSQL
    /// («duplicate key value violates unique constraint "UK_COR_People_TaxId"») y de SQL
    /// Server («…with unique index 'UK_COR_People_TaxId'») cuando dos usuarios crean la misma
    /// persona a la vez y ambos pasaron la comprobación previa.
    /// </summary>
    public const string IndiceUnicoDelDocumento = "UK_COR_People_TaxId";

    public async Task<Result<Person>> PrepareAsync(PersonInput input, CancellationToken ct)
    {
        var colision = await ColisionDeDocumentoAsync(input.TaxId, excluirId: null, ct);
        if (colision is not null)
            return Result.Failure<Person>(colision);

        int? cityId = null;
        if (input.CityPublicId.HasValue)
        {
            cityId = await context.Cities.AsNoTracking()
                .Where(c => c.PublicId == input.CityPublicId.Value && !c.IsDeleted)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
            if (cityId is null)
                return Result.Failure<Person>(new Error("Person.CityNotFound", "Ciudad no encontrada."));
        }

        var person = new Person
        {
            IdType = input.IdType,
            TaxId = input.TaxId,
            TaxIdCheckDigit = input.TaxIdCheckDigit,
            IdIssuedAt = input.IdIssuedAt,
            IdIssueDate = input.IdIssueDate,
            FirstName = input.FirstName,
            LastName = input.LastName,
            SecondLastName = Vacio(input.SecondLastName),
            OtherNames = Vacio(input.OtherNames),
            BusinessName = input.BusinessName,
            PersonType = input.PersonType,
            Address = input.Address,
            Phone1 = input.Phone1,
            Phone2 = input.Phone2,
            Mobile = input.Mobile,
            Email = input.Email,
            CityId = cityId,
            Gender = input.Gender,
            MaritalStatus = input.MaritalStatus,
            DateOfBirth = input.DateOfBirth,
            EducationLevel = input.EducationLevel,
            IsThirdParty = input.IsThirdParty,
            IsAdvisor = input.IsAdvisor,
            IsCustomer = input.IsCustomer,
            IsSupplier = input.IsSupplier,
            ReceivesInvoice = input.ReceivesInvoice,
            Status = string.IsNullOrEmpty(input.Status) ? "A" : input.Status,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.People.Add(person);
        return Result.Success(person);
    }

    /// <summary>
    /// Busca otra persona con ese documento, <b>eliminadas incluidas</b>, y arma el error que
    /// corresponde: <c>Person.TaxIdDuplicate</c> si está viva, <c>Person.TaxIdDeleted</c> si
    /// está eliminada. Nulo si el documento está libre. <paramref name="excluirId"/> deja fuera
    /// a la propia persona cuando se está editando.
    /// </summary>
    public async Task<Error?> ColisionDeDocumentoAsync(string taxId, int? excluirId, CancellationToken ct)
    {
        var existente = await context.People
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TaxId == taxId && (excluirId == null || p.Id != excluirId))
            .OrderBy(p => p.IsDeleted) // si hubiera una viva y una eliminada, manda la viva
            .Select(p => new { p.PublicId, p.FirstName, p.LastName, p.BusinessName, p.IsDeleted, p.DeletedAt })
            .FirstOrDefaultAsync(ct);

        if (existente is null) return null;

        var nombre = NombreVisible(existente.FirstName, existente.LastName, existente.BusinessName);
        return existente.IsDeleted
            ? DocumentoEliminado(nombre, existente.DeletedAt)
            : DocumentoDuplicado(nombre);
    }

    /// <summary>
    /// Cierra la carrera que la comprobación previa no puede cerrar: si el <c>SaveChanges</c>
    /// chocó contra <see cref="IndiceUnicoDelDocumento"/>, devuelve el mismo
    /// <c>Person.TaxIdDuplicate</c> (o <c>TaxIdDeleted</c>) que habría dado la comprobación,
    /// con el nombre de la persona que ganó. Nulo si la excepción es otra cosa: el que llama
    /// la relanza.
    /// </summary>
    public async Task<Error?> TraducirColisionAsync(DbUpdateException ex, string taxId, CancellationToken ct)
    {
        if (!EsColisionDeDocumento(ex)) return null;
        return await ColisionDeDocumentoAsync(taxId, excluirId: null, ct)
               ?? DocumentoDuplicado("otra persona");
    }

    public static bool EsColisionDeDocumento(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual.Message.Contains(IndiceUnicoDelDocumento, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static Error DocumentoDuplicado(string nombre) =>
        new("Person.TaxIdDuplicate", $"Ya existe {nombre} con ese documento.");

    public static Error DocumentoEliminado(string nombre, DateTime? eliminadaEl) =>
        new("Person.TaxIdDeleted",
            eliminadaEl is { } fecha
                ? $"Ese documento pertenece a una persona eliminada el {fecha:yyyy-MM-dd}: {nombre}."
                : $"Ese documento pertenece a una persona eliminada: {nombre}.");

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string NombreVisible(string firstName, string lastName, string? businessName) =>
        !string.IsNullOrWhiteSpace(businessName) ? businessName : $"{firstName} {lastName}".Trim();
}
