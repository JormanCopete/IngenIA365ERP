using System.Data.Common;
using FluentAssertions;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Apuntar una cadena de conexión a otra base.
///
/// <para>
/// Con una base por cooperativa esto deja de ser una utilidad y pasa a ser la
/// operación central del aislamiento: cada llamada decide contra qué datos va a
/// hablar la aplicación. Un fallo aquí no da error — da los datos de otra
/// cooperativa.
/// </para>
///
/// <para>
/// La trampa que justifica que exista un solo sitio: la clave del catálogo se
/// llama <c>Database</c> en PostgreSQL e <c>Initial Catalog</c> en SQL Server.
/// El proyecto soporta los dos.
/// </para>
/// </summary>
public class ComponerCadenasDeConexionTests
{
    private const string Postgres =
        "Host=localhost;Port=5432;Database=erp_original;Username=ingenia;Password=secreta-de-prueba;Pooling=true";
    private const string SqlServer =
        "Server=localhost;Initial Catalog=ErpOriginal;User Id=sa;Password=secreta-de-prueba;TrustServerCertificate=True";

    [Fact]
    public void PostgreSql_CambiaElCatalogo()
    {
        var r = ConnectionStringTargeting.ConCatalogo(Postgres, "erp_coop_alfa");

        ConnectionStringTargeting.CatalogoDe(r).Should().Be("erp_coop_alfa");
    }

    [Fact]
    public void SqlServer_CambiaElCatalogo()
    {
        // La clave se llama distinto. Es la razon de que esto exista una sola vez.
        var r = ConnectionStringTargeting.ConCatalogo(SqlServer, "ErpCoopAlfa");

        ConnectionStringTargeting.CatalogoDe(r).Should().Be("ErpCoopAlfa");
    }

    [Theory]
    [InlineData(Postgres, "Username", "ingenia")]
    [InlineData(Postgres, "Pooling", "true")]
    [InlineData(SqlServer, "User Id", "sa")]
    [InlineData(SqlServer, "TrustServerCertificate", "True")]
    public void ElRestoDeLaCadenaSeConserva(string original, string clave, string valor)
    {
        // Perder el pooling o la configuración de certificado al cambiar de base
        // sería un fallo silencioso: conecta igual, con otras condiciones.
        var r = ConnectionStringTargeting.ConCatalogo(original, "otra_base");

        var constructor = new DbConnectionStringBuilder { ConnectionString = r };
        constructor.ContainsKey(clave).Should().BeTrue();
        constructor[clave].ToString().Should().Be(valor);
    }

    [Fact]
    public void SinCatalogoDeclarado_Lanza()
    {
        // Adivinar el nombre de la base seria peor que fallar.
        var accion = () => ConnectionStringTargeting.ConCatalogo(
            "Host=localhost;Username=ingenia;Password=secreta-de-prueba", "erp_coop_alfa");

        accion.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ElErrorNoFiltraLaContrasena()
    {
        // La cadena lleva credenciales. Un mensaje que la incluya acaba en un log.
        var accion = () => ConnectionStringTargeting.ConCatalogo(
            "Host=localhost;Username=ingenia;Password=secreta-de-prueba", "erp_coop_alfa");

        accion.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().NotContain("secreta-de-prueba");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SinNombreDeBaseDestino_Lanza(string vacio)
    {
        var accion = () => ConnectionStringTargeting.ConCatalogo(Postgres, vacio);

        accion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ANivelDeServidor_PostgreSqlApuntaAPostgres()
    {
        var r = ConnectionStringTargeting.ANivelDeServidor(Postgres, DatabaseProvider.PostgreSql);

        ConnectionStringTargeting.CatalogoDe(r).Should().Be("postgres");
    }

    [Fact]
    public void ANivelDeServidor_SqlServerApuntaAMaster()
    {
        var r = ConnectionStringTargeting.ANivelDeServidor(SqlServer, DatabaseProvider.SqlServer);

        ConnectionStringTargeting.CatalogoDe(r).Should().Be("master");
    }

    [Fact]
    public void ANivelDeServidor_SinCatalogo_NoLanza()
    {
        // A diferencia de ConCatalogo: una cadena sin catalogo ya apunta al
        // servidor, que es justo lo que se pedia.
        var sinCatalogo = "Host=localhost;Username=ingenia;Password=secreta-de-prueba";

        var accion = () => ConnectionStringTargeting.ANivelDeServidor(
            sinCatalogo, DatabaseProvider.PostgreSql);

        accion.Should().NotThrow();
    }

    [Fact]
    public void CatalogoDe_DevuelveNullSiNoHay()
    {
        ConnectionStringTargeting.CatalogoDe("Host=localhost;Username=x").Should().BeNull();
    }

    [Fact]
    public void ApuntarDosVeces_NoAcumula()
    {
        // Idempotencia: reapuntar la cadena ya apuntada da la misma base, no una
        // concatenacion. Importa porque el aprovisionamiento puede reintentarse.
        var una = ConnectionStringTargeting.ConCatalogo(Postgres, "erp_coop_alfa");
        var dos = ConnectionStringTargeting.ConCatalogo(una, "erp_coop_alfa");

        ConnectionStringTargeting.CatalogoDe(dos).Should().Be("erp_coop_alfa");
    }

    [Fact]
    public void DerivarLaCadenaAdmin_SigueDandoElMismoResultado()
    {
        // El comportamiento de DatabaseOptions no cambia al reescribirlo sobre el
        // componente nuevo: sigue siendo el nombre original mas el sufijo.
        var r = DatabaseOptions.DeriveAdminConnectionString(Postgres, DatabaseProvider.PostgreSql);

        ConnectionStringTargeting.CatalogoDe(r).Should().Be("erp_original_admin");
    }

    [Fact]
    public void DerivarLaCadenaAdmin_EnSqlServerUsaOtroSufijo()
    {
        var r = DatabaseOptions.DeriveAdminConnectionString(SqlServer, DatabaseProvider.SqlServer);

        ConnectionStringTargeting.CatalogoDe(r).Should().Be("ErpOriginal_Admin");
    }
}
