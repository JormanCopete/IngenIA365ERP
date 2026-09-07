namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Deja lista la <b>base de datos</b> de una cooperativa: la crea si falta, la
/// lleva al nivel actual de migraciones y le siembra sus catálogos, permisos y
/// roles.
///
/// <para>
/// Sustituye al aprovisionador por esquema. La diferencia no es de nombre: crear
/// una base no admite las mismas herramientas que crear un esquema.
/// <c>CREATE DATABASE</c> no corre dentro de una transacción, exige conectarse a
/// la base de sistema del motor, y las migraciones se aplican con EF normal —
/// sin reescribir el script buscando la cadena literal <c>dbo</c>, que es de
/// donde salían los peores fallos del modelo anterior.
/// </para>
/// </summary>
public interface ITenantDatabaseProvisioner
{
    /// <param name="nombreDeBase">Nombre de la base, tal como está en <c>ADM_Tenants.DatabaseName</c>.</param>
    /// <param name="identificador">Identificador de la cooperativa, para los registros.</param>
    /// <param name="cadenaPropia">
    /// Cadena de conexión propia si la cooperativa vive fuera de la instancia por
    /// defecto. Null es lo normal: entonces se compone de la plantilla más el
    /// nombre de la base.
    /// </param>
    Task<ResultadoAprovisionamiento> AprovisionarAsync(
        string nombreDeBase, string identificador, string? cadenaPropia, CancellationToken ct);
}

/// <param name="BaseCreada">False si ya existía. Aprovisionar es idempotente.</param>
/// <param name="MigracionAplicada">Última migración de la base al terminar.</param>
/// <param name="FilasSembradas">Filas insertadas por los seeders de cooperativa.</param>
public sealed record ResultadoAprovisionamiento(
    bool BaseCreada,
    string? MigracionAplicada,
    int FilasSembradas);
