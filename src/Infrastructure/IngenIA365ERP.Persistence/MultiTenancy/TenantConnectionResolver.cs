using IngenIA365ERP.Persistence.Providers;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// El único sitio del sistema que traduce «esta cooperativa» a «esta cadena de
/// conexión».
///
/// <para>
/// Que sea uno solo es la garantía. Con una base por cooperativa, componer una
/// cadena es la operación que decide contra qué datos habla la aplicación: si
/// hubiera tres sitios que la compusieran, habría tres sitios donde equivocarse
/// y el error no daría excepción, daría los datos de otra cooperativa.
/// </para>
///
/// <para>
/// La precedencia es deliberada: si la cooperativa declara cadena propia, manda
/// esa — es lo que permite trasladarla a otro servidor cambiando un dato y no el
/// código, como exige el Principio IV. Si no, se compone de la plantilla más el
/// nombre de su base. Y si no hay ni una cosa ni la otra, <b>lanza</b>: nunca
/// devuelve la plantilla sin sustituir, porque eso sería apuntar a la base
/// equivocada en silencio.
/// </para>
/// </summary>
public sealed class TenantConnectionResolver(IOptions<DatabaseOptions> opciones)
{
    /// <summary>
    /// La cadena de la instancia por defecto, sin apuntar a ninguna cooperativa.
    /// La usan el arranque, las migraciones y los trabajos sin petición detrás.
    /// </summary>
    public string Plantilla => opciones.Value.GetActiveConnectionString();

    /// <summary>
    /// Cadena de la cooperativa. <paramref name="cadenaPropia"/> gana si viene;
    /// si no, se compone con <paramref name="nombreDeBase"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Cuando no hay ni cadena propia ni nombre de base. Es intencionado: la
    /// alternativa sería devolver la plantilla, y entonces una cooperativa mal
    /// resuelta leería y escribiría en la base de plantilla sin que nada lo diga.
    /// </exception>
    public string Resolver(string? nombreDeBase, string? cadenaPropia, string? paraElMensaje = null)
    {
        if (!string.IsNullOrWhiteSpace(cadenaPropia)) return cadenaPropia;

        if (string.IsNullOrWhiteSpace(nombreDeBase))
        {
            throw new InvalidOperationException(
                $"La cooperativa {paraElMensaje ?? "(sin identificar)"} no declara base de datos " +
                "ni cadena propia, así que no se puede saber contra qué datos operar. Revisá " +
                "ADM_Tenants.DatabaseName. No se cae a la plantilla a propósito: eso mezclaría " +
                "los datos de todas.");
        }

        return ConnectionStringTargeting.ConCatalogo(Plantilla, nombreDeBase);
    }
}
