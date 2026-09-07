using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Infrastructure;

/// <summary>
/// Un solo host de identidad central para las clases que sólo comprueban la puerta
/// (sin token → 401 o 404 indistinguible, cabecera de cooperativa divergente → rechazo).
/// Venían de <c>ApiTestFixture</c>, la fixture de la Fase 0: SQL Server fijo y una
/// cooperativa «demo» sobre el esquema <c>dbo</c>, que contradecía el modelo de una
/// base por cooperativa y ninguna de ellas usaba. Con la colección, las seis clases
/// comparten los tres contenedores en vez de levantarlos seis veces.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class IdentidadCentralCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Identidad central compartida";
}
