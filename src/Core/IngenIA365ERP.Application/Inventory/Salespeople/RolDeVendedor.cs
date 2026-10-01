using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople;

/// <summary>
/// La <b>única</b> operación que crea, restaura y retira el rol vendedor (feature 012, T424–T426; FR-031, FR-092;
/// data-model §11; contracts/api.md §31): escribe a la vez la fila de <c>INV_Salespeople</c> y la marca
/// <c>Person.IsSalesperson</c> (feature 008: la bandera la escribe sólo quien crea o retira la fila hija). La usan
/// <c>CreateSalespersonCommand</c>, <c>DeleteSalespersonCommand</c> y la plantilla 9 (<c>ImportSalespeopleCommand</c>), así
/// la importación sigue exactamente las mismas reglas que la pantalla. No guarda: guarda quien la llama, en un solo
/// <c>SaveChanges</c>. (nuevo)
///
/// <para>
/// Una persona tiene a lo sumo una fila de vendedor en toda su historia: retirarla es una baja lógica y volver a darle el rol
/// la <b>restaura</b> con el mismo <c>PublicId</c>, así los documentos que la citan siguen apuntando a ella. Por eso el índice
/// único de <c>PersonId</c> no estorba mientras llega el índice filtrado a vivas de <c>InventarioComercialNucleo</c> (T427).
/// </para>
/// </summary>
public sealed class RolDeVendedor(IApplicationDbContext db, IDateTimeService reloj, IActorActual actorActual)
{
    /// <summary>La persona viva por su <c>PublicId</c>, rastreada; nula si no existe o fue eliminada.</summary>
    public Task<Person?> PersonaVivaAsync(Guid personPublicId, CancellationToken ct) =>
        db.People.FirstOrDefaultAsync(p => p.PublicId == personPublicId && !p.IsDeleted, ct);

    /// <summary>La fila de vendedor de la persona, viva o retirada (la más reciente si el legado dejó más de una).</summary>
    public Task<Salesperson?> FilaDeAsync(int personId, CancellationToken ct) =>
        db.Salespeople.IgnoreQueryFilters()
            .Where(s => s.PersonId == personId)
            .OrderBy(s => s.IsDeleted).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Da el rol a <paramref name="persona"/>: restaura <paramref name="fila"/> si está retirada o crea una nueva; enciende
    /// <c>IsSalesperson</c>. Con el rol vivo, <c>Inventory.Salesperson.AlreadyActive</c>. <paramref name="tipo"/> y
    /// <paramref name="comision"/> nulos conservan lo de la fila restaurada (o los valores por defecto en una nueva).
    /// </summary>
    public Result<AsignacionDeVendedor> Asignar(Person persona, Salesperson? fila, int? tipo, bool? comision)
    {
        ArgumentNullException.ThrowIfNull(persona);
        if (fila is { IsDeleted: false }) return Result.Failure<AsignacionDeVendedor>(ErroresDeVendedores.YaActivo(persona));

        var restaurada = fila is not null;
        if (fila is null)
        {
            fila = new Salesperson { PersonId = persona.Id, Person = persona };
            db.Salespeople.Add(fila);
        }
        else
        {
            fila.IsDeleted = false;
            fila.DeletedAt = null;
            fila.DeletedBy = null;
        }
        if (tipo is not null || !restaurada) fila.SalespersonType = tipo;
        if (comision is { } aplica) fila.AppliesCommission = aplica;

        persona.IsSalesperson = true;
        return Result.Success(new AsignacionDeVendedor(fila, restaurada));
    }

    /// <summary>Retira el rol: baja lógica de la fila y <c>IsSalesperson = false</c> en la persona (si sigue viva).</summary>
    public async Task RetirarAsync(Salesperson fila, Person? persona, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fila);
        var actor = await actorActual.ObtenerAsync(ct);
        fila.IsDeleted = true;
        fila.DeletedAt = reloj.UtcNow;
        fila.DeletedBy = Recortar(actor.Name);
        if (persona is { IsDeleted: false }) persona.IsSalesperson = false;
    }

    private static string Recortar(string nombre) => nombre.Length > 100 ? nombre[..100] : nombre;
}

/// <summary>El resultado de dar el rol: la fila y si se restauró una retirada. (nuevo)</summary>
public sealed record AsignacionDeVendedor(Salesperson Fila, bool Restaurada);

/// <summary>Los errores de vendedores (contracts/api.md §31; decisiones §2.17). (nuevo)</summary>
public static class ErroresDeVendedores
{
    public const string CodigoYaActivo = "Inventory.Salesperson.AlreadyActive";
    public const string CodigoPersonaInexistente = "Core.Person.NotFound";

    public static Error YaActivo(Person persona) => new ErrorConDatos(CodigoYaActivo,
        $"{Nombre(persona)} ya es vendedor.",
        new { personPublicId = persona.PublicId });

    public static Error PersonaInexistente() => new(CodigoPersonaInexistente, "La persona no existe.");

    /// <summary>La persona fue eliminada del maestro: se restaura en Personas, no aquí.</summary>
    public static Error PersonaEliminada(string documento) => new(CodigoPersonaInexistente,
        $"La persona con documento {documento} fue eliminada. Restaurala en Maestros → Personas y volvé a importar.");

    public static string Nombre(Person p) => $"{p.FirstName} {p.LastName}".Trim();
}
