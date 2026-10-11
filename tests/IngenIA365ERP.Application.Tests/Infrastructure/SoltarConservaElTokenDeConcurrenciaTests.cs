using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 013, US4 (bloque 8C; defecto encontrado por <c>CausacionYLiquidacionTests</c> en PostgreSQL): la tanda del ciclo diario suelta lo que
/// escribió y sigue con la corrida. Lo hacía con <c>DescartarCambios()</c> y <c>Attach(corrida)</c>; en PostgreSQL el token de concurrencia es la
/// sombra <c>xmin</c>, que se pierde al soltar la entidad, y el guardado siguiente salía con <c>WHERE xmin = 0</c>: «modificado por Proceso de
/// integración mientras editabas» después de la primera tanda. <see cref="ApplicationDbContext.DescartarCambiosSalvo"/> conserva el token. Se
/// mira el modelo de cada proveedor sin abrir conexión (como <see cref="LasCorridasViejasQuedanOrdinariasTests"/>). (nuevo)
/// </summary>
public class SoltarConservaElTokenDeConcurrenciaTests
{
    private const string CadenaPg = "Host=localhost;Database=x;Username=y;Password=z";
    private const string CadenaSql = "Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True";

    private static ApplicationDbContext Contexto(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres ? builder.UseNpgsql(CadenaPg) : builder.UseSqlServer(CadenaSql);
        return new ApplicationDbContext(builder.Options);
    }

    public static TheoryData<bool> Proveedores => [true, false];

    [Theory]
    [MemberData(nameof(Proveedores))]
    public void Soltar_todo_salvo_la_corrida_conserva_su_token_y_la_deja_sin_cambios(bool postgres)
    {
        using var db = Contexto(postgres);
        var corrida = new LendingProcessRun { Id = 7, Kind = LendingProcessKind.DailyCycle, ExecutionOrigin = "Tarea:cartera.diario:2026-10-11" };
        var error = new LendingProcessRunError { Id = 3, ProcessRunId = 7, Step = "Causacion", ErrorCode = "X", ErrorMessage = "y" };
        var otra = new LendingProcessRun { Id = 8, Kind = LendingProcessKind.DailyCycle, ExecutionOrigin = "otra" };
        db.Attach(corrida);
        db.Attach(error);
        db.Attach(otra);
        var token = TokenDe(db, corrida, postgres, valor: 4242u, bytes: [0, 0, 0, 0, 0, 0, 0x10, 0x92]);
        corrida.CursorLoanId = 99;

        db.DescartarCambiosSalvo(corrida, error);

        var seguidas = db.ChangeTracker.Entries().Select(e => e.Entity).ToList();
        seguidas.Should().HaveCount(2, "lo demás se soltó");
        seguidas.Should().Contain(x => ReferenceEquals(x, corrida)).And.Contain(x => ReferenceEquals(x, error));
        var entrada = db.Entry(corrida);
        entrada.State.Should().Be(EntityState.Unchanged);
        var propiedad = entrada.Properties.Single(p => p.Metadata.IsConcurrencyToken);
        propiedad.CurrentValue.Should().BeEquivalentTo(token, "el token sigue siendo el de la base");
        propiedad.OriginalValue.Should().BeEquivalentTo(token, "el UPDATE siguiente lo usa en el WHERE");
        propiedad.IsModified.Should().BeFalse();

        corrida.Status = LendingProcessRunStatus.Running;
        db.ChangeTracker.DetectChanges();
        entrada.State.Should().Be(EntityState.Modified, "lo que se cambie después se guarda");
    }

    [Fact]
    public void En_PostgreSQL_soltar_y_volver_a_adjuntar_perdia_el_token()
    {
        // El defecto: la sombra xmin vuelve a cero, y «WHERE xmin = 0» no encuentra la fila.
        using var db = Contexto(postgres: true);
        var corrida = new LendingProcessRun { Id = 7, Kind = LendingProcessKind.DailyCycle, ExecutionOrigin = "x" };
        db.Attach(corrida);
        TokenDe(db, corrida, postgres: true, valor: 4242u, bytes: []);

        db.DescartarCambios();
        db.Attach(corrida);

        db.Entry(corrida).Property("xmin").OriginalValue.Should().Be(0u);
    }

    /// <summary>Pone el token de la corrida como si viniera de la base y lo devuelve.</summary>
    private static object TokenDe(ApplicationDbContext db, LendingProcessRun corrida, bool postgres, uint valor, byte[] bytes)
    {
        var propiedad = db.Entry(corrida).Properties.Single(p => p.Metadata.IsConcurrencyToken);
        object token = postgres ? valor : bytes;
        propiedad.Metadata.Name.Should().Be(postgres ? "xmin" : nameof(LendingProcessRun.RowVersion));
        propiedad.CurrentValue = token;
        propiedad.OriginalValue = token;
        propiedad.IsModified = false;
        return token;
    }
}
