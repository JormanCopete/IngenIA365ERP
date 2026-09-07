using FluentAssertions;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Traducción del script de migración al esquema de cada cooperativa.
///
/// <para>
/// La sentencia <c>CREATE SCHEMA dbo;</c> no llevaba punto ni comillas, así que
/// no encajaba en ninguno de los reemplazos —que buscan el esquema como
/// calificador— y sobrevivía sin traducir. Aprovisionar una cooperativa
/// intentaba entonces crear <c>dbo</c>, que ya existe, fallaba con 42P06 y el
/// esquema del tenant NO se creaba nunca. Se detectó al registrar la primera
/// cooperativa real: quedó la fila, se envió la invitación, y no había esquema
/// detrás.
/// </para>
/// </summary>
public class TranslateSchemaTests
{
    private const string Esquema = "coop_ejemplo";

    [Theory]
    [InlineData("CREATE SCHEMA dbo;")]
    [InlineData("CREATE SCHEMA IF NOT EXISTS dbo;")]
    [InlineData("CREATE SCHEMA \"dbo\";")]
    [InlineData("create schema dbo;")]
    public void PostgreSql_LaCreacionDelEsquemaSeTraduce(string sentencia)
    {
        var r = TenantSchemaService.TranslateSchema(sentencia, Esquema, DatabaseProvider.PostgreSql);

        r.Should().Contain(Esquema);
        r.Should().NotMatchRegex(@"CREATE\s+SCHEMA\s+(IF\s+NOT\s+EXISTS\s+)?""?dbo",
            "crear dbo falla con 42P06 porque ya existe");
        r.Should().Contain("IF NOT EXISTS", "reaprovisionar debe poder repetirse");
    }

    [Theory]
    [InlineData("CREATE SCHEMA [dbo];")]
    [InlineData("CREATE SCHEMA dbo;")]
    public void SqlServer_LaCreacionDelEsquemaSeTraduce(string sentencia)
    {
        var r = TenantSchemaService.TranslateSchema(sentencia, Esquema, DatabaseProvider.SqlServer);

        r.Should().Contain($"[{Esquema}]");
        r.Should().NotContain("[dbo]");
    }

    [Fact]
    public void PostgreSql_ElEsquemaComoCalificadorSigueTraduciendose()
    {
        // Lo que ya funcionaba no debe romperse al arreglar lo otro.
        var script = "CREATE TABLE \"dbo\".\"COR_People\" (\"Id\" integer NOT NULL);";

        var r = TenantSchemaService.TranslateSchema(script, Esquema, DatabaseProvider.PostgreSql);

        r.Should().Be($"CREATE TABLE \"{Esquema}\".\"COR_People\" (\"Id\" integer NOT NULL);");
    }

    [Fact]
    public void PostgreSql_ElHistorialDeMigracionesTambienSeTraduce()
    {
        // Si el historial quedara en dbo, cada tenant creeria tener aplicadas
        // las migraciones de la plantilla y no crearia ni una tabla propia.
        var script = "INSERT INTO \"dbo\".\"__EFMigrationsHistory\" VALUES ('x', '10.0');";

        var r = TenantSchemaService.TranslateSchema(script, Esquema, DatabaseProvider.PostgreSql);

        r.Should().Contain($"\"{Esquema}\".\"__EFMigrationsHistory\"");
        r.Should().NotContain("\"dbo\"");
    }

    [Fact]
    public void UnNombreDeTablaQueContengaDbo_NoSeDestroza()
    {
        // "dbo" dentro de un identificador mas largo no es el esquema.
        var script = "CREATE TABLE \"dbo\".\"COR_dbolsa\" (\"Id\" integer);";

        var r = TenantSchemaService.TranslateSchema(script, Esquema, DatabaseProvider.PostgreSql);

        r.Should().Contain("COR_dbolsa");
    }
}
