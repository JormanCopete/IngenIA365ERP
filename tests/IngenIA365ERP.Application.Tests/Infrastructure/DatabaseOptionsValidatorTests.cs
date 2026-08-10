using FluentAssertions;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

public class DatabaseOptionsValidatorTests
{
    private static DatabaseOptions ValidPostgres() => new()
    {
        Provider = "PostgreSQL",
        ConnectionStrings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["PostgreSQL"] = "Host=localhost;Database=ingenia365erp;Username=u;Password=p"
        }
    };

    private readonly DatabaseOptionsValidator _validator = new();

    [Fact]
    public void ProviderValido_PostgreSQL_Pasa()
    {
        _validator.Validate(null, ValidPostgres()).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("postgresql")]
    [InlineData("POSTGRESQL")]
    [InlineData("postgres")]
    public void Provider_EsCaseInsensitive_YAceptaAlias(string provider)
    {
        var options = ValidPostgres();
        options.Provider = provider;
        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void ProviderInvalido_Falla_ConCodigoYValoresValidos()
    {
        var options = ValidPostgres();
        options.Provider = "Oracle";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Database.InvalidProvider")
            .And.Contain("Oracle").And.Contain("PostgreSQL, SqlServer");
    }

    [Fact]
    public void ConnectionStringFaltanteDelProviderActivo_Falla_NombrandoLaClave()
    {
        var options = ValidPostgres();
        options.ConnectionStrings["PostgreSQL"] = "";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Database.ConnectionStringMissing")
            .And.Contain("Database:ConnectionStrings:PostgreSQL");
    }

    [Fact]
    public void ConnectionStringDelProviderNoActivo_Ausente_NoEsError()
    {
        // FR-004: solo la cadena del provider activo es obligatoria.
        var options = ValidPostgres();
        options.ConnectionStrings.Remove("SqlServer");

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(60, 0)]
    public void StartupOptionsInvalidas_Fallan(int window, int interval)
    {
        var options = ValidPostgres();
        options.Startup.RetryWindowSeconds = window;
        options.Startup.RetryIntervalSeconds = interval;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Database.InvalidStartupOptions");
    }

    [Fact]
    public void DerivacionAdmin_AgregaSufijoPorProveedor()
    {
        DatabaseOptions.DeriveAdminConnectionString(
                "Host=localhost;Database=ingenia365erp;Username=u;Password=p",
                DatabaseProvider.PostgreSql)
            .Should().Contain("ingenia365erp_admin");

        DatabaseOptions.DeriveAdminConnectionString(
                "Server=localhost;Database=IngenIA365ERP;Trusted_Connection=true",
                DatabaseProvider.SqlServer)
            .Should().Contain("IngenIA365ERP_Admin");
    }
}
