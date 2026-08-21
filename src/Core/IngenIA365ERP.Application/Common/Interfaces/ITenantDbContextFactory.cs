namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Abre la base operativa de una cooperativa concreta, que no tiene por qué ser
/// la de la petición en curso.
///
/// <para>
/// Existe porque hay handlers que, por naturaleza, escriben fuera de su propio
/// contexto: aceptar una invitación provisiona el usuario en la cooperativa que
/// dice la invitación —y esa ruta es anónima, sin cooperativa activa—, y
/// aprovisionar el esquema de una cooperativa lo hace el administrador maestro,
/// que no pertenece a ninguna. Con el contexto que entrega el contenedor
/// escribirían en el sitio equivocado, o directamente lanzarían.
/// </para>
///
/// <para>
/// Vive en Application y se implementa en Persistence a propósito: el Principio
/// II prohíbe que Application conozca EF Core.
/// </para>
/// </summary>
public interface ITenantDbContextFactory
{
    /// <param name="esquema">
    /// Nombre del esquema, tal como está en <c>ADM_Tenants.SchemaName</c>. No el
    /// identificador ni el PublicId.
    /// </param>
    ITenantDbScope Abrir(string esquema);
}

/// <summary>
/// Contexto operativo apuntado a un esquema, con su ciclo de vida propio. Hay
/// que liberarlo: no lo gestiona el contenedor.
/// </summary>
public interface ITenantDbScope : IAsyncDisposable
{
    IApplicationDbContext Db { get; }
}
