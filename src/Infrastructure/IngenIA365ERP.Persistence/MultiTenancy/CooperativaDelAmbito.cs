using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// La fábrica de <see cref="ErpTenantInfo"/>: decide, una vez por ámbito, contra qué base habla
/// <c>ApplicationDbContext</c>. Estaba escrita en línea en <c>DependencyInjection</c>; salió aquí
/// para poder probarla (feature 012, T044) cuando ganó su tercer camino.
///
/// <para>Los tres caminos, en este orden:</para>
/// <list type="number">
/// <item><b>Petición HTTP</b>: la cooperativa que resolvió <c>TenantResolutionMiddleware</c>. Sin
/// cooperativa resuelta <b>lanza</b>: no hay segunda barrera que lo recoja y caer a la plantilla
/// mezclaría los datos de todas.</item>
/// <item><b>Trabajo de fondo con <see cref="ContextoAmbiental"/></b> (lo fija
/// <see cref="IEjecutorEnCooperativa"/>): la base de esa cooperativa. Una entrada sin base ni
/// cadena propia lanza, por la misma razón.</item>
/// <item><b>Sin petición ni contexto</b> —arranque, migraciones, la CLI—: la plantilla, donde viven
/// el árbol de migraciones y el modelo. Es el único camino que va a la plantilla.</item>
/// </list>
/// </summary>
public static class CooperativaDelAmbito
{
    public static ErpTenantInfo Crear(IServiceProvider sp)
    {
        var resolutor = sp.GetRequiredService<TenantConnectionResolver>();
        var peticion = sp.GetService<IHttpContextAccessor>()?.HttpContext;

        if (peticion is null)
        {
            if (ContextoAmbiental.Cooperativa is { } cooperativa)
            {
                return new ErpTenantInfo
                {
                    // dbo, igual que en una peticion: con base por cooperativa el esquema de la
                    // entrada es un resto del modelo anterior y no decide nada.
                    SchemaName = "dbo",
                    // Lanza si la entrada no declara base ni cadena propia (TenantConnectionResolver).
                    ConnectionString = resolutor.Resolver(cooperativa.DatabaseName, cooperativa.ConnectionString, cooperativa.Name),
                    Name = cooperativa.Name,
                    Identifier = cooperativa.Identifier,
                    InternalId = cooperativa.Id,
                    PublicId = cooperativa.PublicId,
                    DatabaseName = cooperativa.DatabaseName,
                    // El mismo formato que TenantContextAccessor.TenantId en una petición.
                    Id = cooperativa.PublicId.ToString("N"),
                };
            }

            // Sin peticion HTTP ni contexto de trabajo —arranque, la CLI de migraciones— no hay
            // cooperativa de la que tirar. Va contra la instancia por defecto, donde viven el arbol
            // de migraciones y la plantilla. Se comprueba el HttpContext y no la cooperativa porque
            // por esa via son indistinguibles.
            return new ErpTenantInfo { SchemaName = "dbo", ConnectionString = resolutor.Plantilla };
        }

        var baseDeDatos = peticion.Items.TryGetValue("TenantDatabase", out var b) ? b as string : null;
        var propia = peticion.Items.TryGetValue("TenantConnectionOverride", out var c) ? c as string : null;

        // Dentro de una peticion, una cooperativa sin resolver NO puede caer a la base de
        // plantilla. No hay segunda barrera que lo recoja: ninguna entidad implementa
        // ITenantEntity y los filtros globales son todos de borrado logico. Si esto sale mal,
        // nada lo detiene y los datos se mezclan.
        if (string.IsNullOrWhiteSpace(baseDeDatos) && string.IsNullOrWhiteSpace(propia))
        {
            throw new InvalidOperationException(
                "Se pidio la base operativa dentro de una peticion sin cooperativa resuelta " +
                $"({peticion.Request.Method} {peticion.Request.Path}). Caer a la base de " +
                "plantilla mezclaria los datos de todas las cooperativas. Si esta ruta debe " +
                "funcionar sin cooperativa, no tiene que usar IApplicationDbContext.");
        }

        var actual = sp.GetRequiredService<ICurrentTenantService>();
        return new ErpTenantInfo
        {
            SchemaName = "dbo",
            ConnectionString = resolutor.Resolver(baseDeDatos, propia, actual.TenantName),
            Name = actual.TenantName,
            Id = actual.TenantId,
        };
    }
}
