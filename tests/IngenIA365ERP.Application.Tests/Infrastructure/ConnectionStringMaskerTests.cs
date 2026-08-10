using FluentAssertions;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

public class ConnectionStringMaskerTests
{
    [Fact]
    public void Mask_SqlServer_OcultaCredencialesYConservaHostYBase()
    {
        var masked = ConnectionStringMasker.Mask(
            "Server=db.interno;Database=IngenIA365ERP;User Id=erp;Password=SuperSecreta!;TrustServerCertificate=true");

        masked.Should().Contain("db.interno").And.Contain("IngenIA365ERP");
        masked.Should().NotContain("SuperSecreta!").And.NotContain("erp;");
        masked.Should().Contain("***");
    }

    [Fact]
    public void Mask_PostgreSql_OcultaUsernameYPassword()
    {
        var masked = ConnectionStringMasker.Mask(
            "Host=localhost;Port=5432;Database=ingenia365erp;Username=ingenia;Password=IngenIA365_Dev2026!");

        masked.Should().Contain("localhost").And.Contain("ingenia365erp");
        masked.Should().NotContain("IngenIA365_Dev2026!");
        masked.Should().Contain("***");
    }

    [Fact]
    public void Mask_TrustedConnection_SinCredenciales_QuedaIntacta()
    {
        var masked = ConnectionStringMasker.Mask(
            "Server=localhost;Database=IngenIA365ERP;Trusted_Connection=true");

        // DbConnectionStringBuilder normaliza las claves a minusculas.
        masked.ToLowerInvariant().Should().Contain("localhost").And.Contain("trusted_connection");
    }

    [Fact]
    public void Mask_CadenaVaciaONoParseable_NoFiltraNada()
    {
        ConnectionStringMasker.Mask(null).Should().Be("(vacía)");
        ConnectionStringMasker.Mask("   ").Should().Be("(vacía)");
        ConnectionStringMasker.Mask("==;;==").Should().NotContain("==");
    }
}
