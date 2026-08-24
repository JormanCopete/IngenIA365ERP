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
    /// <param name="nombreDeBase">
    /// Nombre de la base, tal como está en <c>ADM_Tenants.DatabaseName</c>.
    /// </param>
    /// <param name="cadenaPropia">
    /// Cadena propia si la cooperativa vive fuera de la instancia por defecto.
    /// Null es lo normal.
    /// </param>
    ITenantDbScope Abrir(string nombreDeBase, string? cadenaPropia = null);
}

/// <summary>
/// Contexto operativo apuntado a un esquema, con su ciclo de vida propio. Hay
/// que liberarlo: no lo gestiona el contenedor.
/// </summary>
public interface ITenantDbScope : IAsyncDisposable
{
    IApplicationDbContext Db { get; }
}
