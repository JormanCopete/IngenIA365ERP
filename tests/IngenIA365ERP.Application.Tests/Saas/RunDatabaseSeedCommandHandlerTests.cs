using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Database;
using IngenIA365ERP.Application.Saas.DatabaseAdmin;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Saas;

public class RunDatabaseSeedCommandValidatorTests
{
    private readonly RunDatabaseSeedCommandValidator _validator = new();

    [Theory]
    [InlineData("Parametric", "All")]
    [InlineData("test", "tenant")]
    [InlineData("PARAMETRIC", "Admin")]
    public void CategoriaYAlcanceValidos_Pasan(string category, string scope)
    {
        _validator.Validate(new RunDatabaseSeedCommand(category, scope, null, false))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void CategoriaInvalida_Falla_ConCodigo()
    {
        var result = _validator.Validate(new RunDatabaseSeedCommand("Bogus", "All", null, false));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "Database.Seed.InvalidCategory");
    }

    [Fact]
    public void AlcanceInvalido_Falla_ConCodigo()
    {
        var result = _validator.Validate(new RunDatabaseSeedCommand("Parametric", "Everything", null, false));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "Database.Seed.InvalidScope");
    }

    [Fact]
    public void TenantPublicIdConAlcanceAdmin_Falla()
    {
        var result = _validator.Validate(new RunDatabaseSeedCommand("Parametric", "Admin", Guid.NewGuid(), false));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "Database.Seed.InvalidScope");
    }
}

public class RunDatabaseSeedCommandHandlerTests
{
    private readonly IDataSeedRunner _runner = Substitute.For<IDataSeedRunner>();

    [Fact]
    public async Task Exito_DevuelveSeedersEjecutados()
    {
        _runner.RunAsync("Parametric", null, null, false, Arg.Any<CancellationToken>())
            .Returns([new SeedRunEntry("RolesSeeder", "Tenant", 2, 8)]);

        var result = await new RunDatabaseSeedCommandHandler(_runner)
            .Handle(new RunDatabaseSeedCommand("Parametric", "All", null, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SeedersRun.Should().ContainSingle(e => e.Name == "RolesSeeder" && e.Inserted == 8);
    }

    [Fact]
    public async Task AlcanceAll_SeTraduceANull_ParaElRunner()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await new RunDatabaseSeedCommandHandler(_runner)
            .Handle(new RunDatabaseSeedCommand("Test", "All", null, true), CancellationToken.None);

        await _runner.Received(1).RunAsync("Test", null, null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedRunException_SeMapeaAResultFailure_ConSuCodigo()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SeedRunException("Database.Seed.SchemaOutdated", "Hay migraciones pendientes."));

        var result = await new RunDatabaseSeedCommandHandler(_runner)
            .Handle(new RunDatabaseSeedCommand("Parametric", "All", null, false), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Database.Seed.SchemaOutdated");
    }

    [Fact]
    public async Task ConfirmacionFaltanteEnProduccion_SeMapeaConCodigo()
    {
        _runner.RunAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<Guid?>(), false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new SeedRunException("Database.Seed.ConfirmationRequired", "Requiere confirmTestSeed."));

        var result = await new RunDatabaseSeedCommandHandler(_runner)
            .Handle(new RunDatabaseSeedCommand("Test", "All", null, false), CancellationToken.None);

        result.Error!.Code.Should().Be("Database.Seed.ConfirmationRequired");
    }
}
