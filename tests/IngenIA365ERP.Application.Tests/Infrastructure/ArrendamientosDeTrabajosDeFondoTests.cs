using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 012, T047–T048 (decisiones-transversales T10, T47; data-model §0.1): el arrendamiento de un
/// trabajo de fondo vive en la base de cada cooperativa, en <c>COR_BackgroundLeases</c>. Aquí se fija el
/// modelo de la tabla en los dos motores —la toma por <c>UPDATE … WHERE</c> depende de que <c>Name</c> sea
/// único— y los nombres fijos. Que dos réplicas no lo tomen a la vez se prueba contra los motores reales
/// en <c>ArrendamientosYTransaccionTests</c> (e2e, T022): InMemory no ejecuta <c>ExecuteUpdateAsync</c>.
/// </summary>
public class ArrendamientosDeTrabajosDeFondoTests
{
    private const string CadenaPg = "Host=localhost;Database=x;Username=y;Password=z";
    private const string CadenaSql = "Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True";

    private static IEntityType Arrendamiento(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres ? builder.UseNpgsql(CadenaPg) : builder.UseSqlServer(CadenaSql);
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model.FindEntityType(typeof(BackgroundLease))!;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_tabla_tiene_nombre_unico_dueno_y_vencimiento(bool postgres)
    {
        var tipo = Arrendamiento(postgres);

        tipo.Should().NotBeNull("BackgroundLease está en el modelo de la cooperativa");
        tipo.GetTableName().Should().Be("COR_BackgroundLeases");

        var nombre = tipo.FindProperty(nameof(BackgroundLease.Name))!;
        nombre.GetMaxLength().Should().Be(100);
        nombre.IsNullable.Should().BeFalse();

        var dueno = tipo.FindProperty(nameof(BackgroundLease.Owner))!;
        dueno.GetMaxLength().Should().Be(150);
        dueno.IsNullable.Should().BeTrue("nulo = libre");

        tipo.FindProperty(nameof(BackgroundLease.LeaseUntil))!.IsNullable.Should().BeFalse();

        var unico = tipo.GetIndexes().Single(i => i.GetDatabaseName() == "UK_COR_BackgroundLeases_Name");
        unico.IsUnique.Should().BeTrue();
        unico.Properties.Select(p => p.Name).Should().Equal(nameof(BackgroundLease.Name));

        tipo.GetQueryFilter().Should().NotBeNull("filtro de borrado lógico, como toda entidad");
        tipo.GetProperties().Should().Contain(p => p.IsConcurrencyToken,
            "RowVersion (xmin en PostgreSQL): segunda defensa si dos réplicas toman a la vez");
    }

    [Fact]
    public void La_renovacion_no_es_un_cambio_que_auditar()
    {
        typeof(BackgroundLease).GetCustomAttributes(typeof(SinDiffDeAuditoriaAttribute), inherit: false)
            .Should().ContainSingle("renovar cada dos minutos no es un cambio de negocio");
    }

    [Fact]
    public void Los_cinco_nombres_son_los_fijos_de_la_plataforma()
    {
        NombresDeArrendamiento.Todos.Should().Equal(
            "integration.dispatch", "audit.forward", "einvoicing.process", "scheduled.tasks", "email.dispatch");
        NombresDeArrendamiento.Todos.Should().OnlyContain(n => n.Length <= 100);
    }

    [Fact]
    public void El_dueno_del_proceso_dice_maquina_proceso_y_arranque_y_cabe_en_la_columna()
    {
        var dueno = ArrendamientosEnBase.DuenoDelProceso;

        dueno.Should().Contain(Environment.MachineName.Length > 60 ? Environment.MachineName[..60] : Environment.MachineName);
        dueno.Should().Contain(Environment.ProcessId.ToString());
        dueno.Length.Should().BeLessThanOrEqualTo(150);
        ArrendamientosEnBase.DuenoDelProceso.Should().Be(dueno, "un Guid por arranque, no por llamada");
    }

    [Fact]
    public void El_ttl_por_defecto_es_de_dos_minutos()
    {
        ArrendamientosEnBase.DuracionPorDefecto.Should().Be(TimeSpan.FromSeconds(120));
    }
}
