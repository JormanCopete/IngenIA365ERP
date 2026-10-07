using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Lending;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0133 (T34; data-model §0.10): el SQL que emite <see cref="SqlDelCerrojoDeCartera"/> para cada motor, en el orden
/// <c>LND_Setup</c> → <c>LND_DebtorControls</c> → <c>LND_LoanControls</c> → numeración, por Id ascendente; el modo «esperar» de las
/// operaciones y el «saltar» del proceso; el INSERT de la fila de deudor que falta con su propia auditoría y el nombre del actor sólo
/// como parámetro; y <see cref="CerrojoDeCartera"/> que lanza sin transacción. Molde: <c>CerrojoDeInventarioSqlTests</c>.
/// </summary>
public class CerrojoDeCarteraSqlTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 15, 30, 0, DateTimeKind.Utc);
    private const string ActorConComillas = "Ana O'Neil; DROP TABLE x";

    [Fact]
    public void El_setup_es_compartido_en_las_operaciones_y_exclusivo_al_cerrar()
    {
        SqlDelCerrojoDeCartera.Setup(DatabaseProvider.PostgreSql, exclusivo: false).Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"LND_Setup\" ORDER BY \"Id\" FOR SHARE;");
        SqlDelCerrojoDeCartera.Setup(DatabaseProvider.PostgreSql, exclusivo: true).Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"LND_Setup\" ORDER BY \"Id\" FOR UPDATE;");
        SqlDelCerrojoDeCartera.Setup(DatabaseProvider.SqlServer, exclusivo: false).Sql
            .Should().Be("SELECT [Id] FROM [dbo].[LND_Setup] WITH (ROWLOCK, HOLDLOCK) ORDER BY [Id];");
        SqlDelCerrojoDeCartera.Setup(DatabaseProvider.SqlServer, exclusivo: true).Sql
            .Should().Be("SELECT [Id] FROM [dbo].[LND_Setup] WITH (XLOCK, ROWLOCK, HOLDLOCK) ORDER BY [Id];",
                "en SQL Server UPDLOCK convive con el compartido: el cierre no esperaría a las operaciones");
    }

    [Fact]
    public void PostgreSql_crea_el_deudor_que_falta_y_lo_bloquea_por_Id()
    {
        var s = SqlDelCerrojoDeCartera.Deudores(DatabaseProvider.PostgreSql, [9, 4, 9], ModoDeEspera.Esperar, ActorConComillas, Ahora);

        s.Select(x => x.Tabla).Should().Equal("LND_DebtorControls", "LND_DebtorControls");
        s[0].Sql.Should().Be("INSERT INTO \"dbo\".\"LND_DebtorControls\" (\"PublicId\", \"PersonId\", \"CreatedAt\", \"CreatedBy\", \"IsDeleted\") "
            + "SELECT gen_random_uuid(), v.\"PersonId\", {0}, {1}, FALSE FROM (VALUES (4), (9)) AS v(\"PersonId\") ON CONFLICT DO NOTHING;");
        s[0].Parametros.Should().Equal(Ahora, ActorConComillas);
        s[1].Sql.Should().Be("SELECT \"Id\" AS \"Value\" FROM \"dbo\".\"LND_DebtorControls\" WHERE \"PersonId\" IN (4, 9) ORDER BY \"Id\" FOR UPDATE;");
    }

    [Fact]
    public void SqlServer_crea_el_deudor_que_falta_con_WHERE_NOT_EXISTS()
    {
        var s = SqlDelCerrojoDeCartera.Deudores(DatabaseProvider.SqlServer, [7], ModoDeEspera.Esperar, ActorConComillas, Ahora);

        s[0].Sql.Should().Be("INSERT INTO [dbo].[LND_DebtorControls] ([PublicId], [PersonId], [CreatedAt], [CreatedBy], [IsDeleted]) "
            + "SELECT NEWID(), v.[PersonId], {0}, {1}, 0 FROM (VALUES (7)) AS v([PersonId]) "
            + "WHERE NOT EXISTS (SELECT 1 FROM [dbo].[LND_DebtorControls] t WITH (UPDLOCK, HOLDLOCK) WHERE t.[PersonId] = v.[PersonId]);");
        s[1].Sql.Should().Be("SELECT [Id] AS [Value] FROM [dbo].[LND_DebtorControls] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE [PersonId] IN (7) ORDER BY [Id];");
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void El_nombre_del_actor_nunca_entra_como_texto(DatabaseProvider motor)
    {
        var s = SqlDelCerrojoDeCartera.Deudores(motor, [1], ModoDeEspera.Esperar, ActorConComillas, Ahora);

        s.Should().OnlyContain(x => !x.Sql.Contains("Ana", StringComparison.Ordinal));
        s[0].Parametros.Should().Contain(ActorConComillas);
    }

    [Fact]
    public void Los_creditos_van_en_una_sentencia_por_Id_esperando_o_saltando()
    {
        SqlDelCerrojoDeCartera.Creditos(DatabaseProvider.PostgreSql, [30, 12, 30], ModoDeEspera.Esperar)!.Sql
            .Should().Be("SELECT \"LoanId\" AS \"Value\" FROM \"dbo\".\"LND_LoanControls\" WHERE \"LoanId\" IN (12, 30) ORDER BY \"Id\" FOR UPDATE;");
        SqlDelCerrojoDeCartera.Creditos(DatabaseProvider.PostgreSql, [5], ModoDeEspera.Saltar)!.Sql
            .Should().EndWith("ORDER BY \"Id\" FOR UPDATE SKIP LOCKED;");
        SqlDelCerrojoDeCartera.Creditos(DatabaseProvider.SqlServer, [12, 30], ModoDeEspera.Esperar)!.Sql
            .Should().Be("SELECT [LoanId] AS [Value] FROM [dbo].[LND_LoanControls] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE [LoanId] IN (12, 30) ORDER BY [Id];");
        SqlDelCerrojoDeCartera.Creditos(DatabaseProvider.SqlServer, [5], ModoDeEspera.Saltar)!.Sql
            .Should().Contain("WITH (UPDLOCK, ROWLOCK, READPAST)");
        SqlDelCerrojoDeCartera.Creditos(DatabaseProvider.SqlServer, [], ModoDeEspera.Esperar).Should().BeNull();
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void El_proceso_salta_tambien_al_deudor_y_sin_personas_no_hay_sentencias(DatabaseProvider motor)
    {
        SqlDelCerrojoDeCartera.Deudores(motor, [3], ModoDeEspera.Saltar, "Proceso", Ahora)[1].Sql
            .Should().Contain(motor == DatabaseProvider.PostgreSql ? "FOR UPDATE SKIP LOCKED" : "READPAST");
        SqlDelCerrojoDeCartera.Deudores(motor, [], ModoDeEspera.Esperar, "Ana", Ahora).Should().BeEmpty();
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void La_numeracion_va_aparte_y_exclusiva(DatabaseProvider motor)
    {
        var s = SqlDelCerrojoDeCartera.Numeracion(motor, 4);
        s.Tabla.Should().Be("LND_NumberSequences");
        s.Sql.Should().Contain(motor == DatabaseProvider.PostgreSql ? "FOR UPDATE;" : "UPDLOCK, ROWLOCK, HOLDLOCK").And.Contain("IN (4)");
    }

    [Fact]
    public void El_instante_de_creacion_va_en_UTC()
    {
        var accion = () => SqlDelCerrojoDeCartera.Deudores(DatabaseProvider.PostgreSql, [1], ModoDeEspera.Esperar, "Ana",
            DateTime.SpecifyKind(Ahora, DateTimeKind.Local));
        accion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Sin_transaccion_en_curso_el_cerrojo_lanza()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True").Options;
        await using var db = new ApplicationDbContext(opciones);
        var cerrojo = new CerrojoDeCartera(db, Substitute.For<IActorActual>(), Substitute.For<IDateTimeService>());

        await FluentActions.Awaiting(() => cerrojo.TomarSetupAsync(false)).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => cerrojo.BloquearDeudoresAsync([1], ModoDeEspera.Esperar)).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => cerrojo.BloquearCreditosAsync([1], ModoDeEspera.Saltar)).Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => cerrojo.BloquearNumeracionAsync(1)).Should().ThrowAsync<InvalidOperationException>();
    }
}
