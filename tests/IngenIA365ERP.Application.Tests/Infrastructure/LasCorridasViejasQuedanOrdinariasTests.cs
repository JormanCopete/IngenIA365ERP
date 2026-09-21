using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 010 (R2, D-13): <c>PAY_PayrollRuns</c> es la tabla más viva de producción y la
/// migración <c>NominaPrestacionesYDian</c> le pone tipo y le suelta el período. Estas pruebas
/// convierten en regla lo que la migración promete, mirando el modelo que EF construye para
/// <b>cada</b> proveedor (sin abrir conexión, como <see cref="DosContextosUnaTablaTests"/>):
///
/// <list type="bullet">
/// <item>toda corrida que se inserte sin decir <c>Kind</c> queda <c>Ordinary</c>, porque el
/// default está <b>en la base</b> y no sólo en el CLR —una migración de datos con
/// <c>INSERT</c> a mano, o cualquier código que omita la columna, no puede dejar un NULL ni un
/// tipo raro—;</item>
/// <item><c>PayPeriodId</c> admite NULL, pero sólo tiene sentido en las especiales: la ordinaria
/// sigue exigiéndolo (<c>EsCoherente</c>) y su índice único filtrado lo cubre;</item>
/// <item>el único viejo <c>UK_PAY_PayrollRuns_Period_Version</c> ya no existe y en su lugar hay
/// cinco filtrados, uno por tipo, con el filtro traducido a la sintaxis de cada motor.</item>
/// </list>
/// </summary>
public class LasCorridasViejasQuedanOrdinariasTests
{
    private const string CadenaPg = "Host=localhost;Database=x;Username=y;Password=z";
    private const string CadenaSql = "Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True";

    private static IModel Modelo(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres ? builder.UseNpgsql(CadenaPg) : builder.UseSqlServer(CadenaSql);
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model;
    }

    private static IEntityType Corrida(bool postgres) => Modelo(postgres).FindEntityType(typeof(PayrollRun))!;

    public static TheoryData<bool> Proveedores => [true, false];

    [Theory]
    [MemberData(nameof(Proveedores))]
    public void UnaCorridaInsertadaSinKind_QuedaOrdinaria_PorqueElDefaultEstaEnLaBase(bool postgres)
    {
        var kind = Corrida(postgres).FindProperty(nameof(PayrollRun.Kind))!;

        kind.IsNullable.Should().BeFalse();
        kind.GetDefaultValue().Should().Be(PayrollRunKind.Ordinary,
            "las corridas de producción y cualquier INSERT que omita la columna quedan Ordinary sin migración de datos");
        ((int)PayrollRunKind.Ordinary).Should().Be(0, "el default de la base es literalmente 0");
    }

    [Theory]
    [MemberData(nameof(Proveedores))]
    public void ElPeriodoAdmiteNull_SoloParaLasEspeciales(bool postgres)
    {
        Corrida(postgres).FindProperty(nameof(PayrollRun.PayPeriodId))!.IsNullable.Should().BeTrue(
            "prima, cesantías, vacaciones y definitiva no tienen período");

        // La regla de negocio no está en la base sino en la entidad: la ordinaria sigue exigiéndolo.
        new PayrollRun { Kind = PayrollRunKind.Ordinary, PayPeriodId = 1 }.EsCoherente.Should().BeTrue();
        new PayrollRun { Kind = PayrollRunKind.Ordinary, PayPeriodId = null }.EsCoherente.Should().BeFalse();
        new PayrollRun { Kind = PayrollRunKind.ServiceBonus, PayPeriodId = null, CutoffDate = new DateOnly(2026, 12, 31) }.EsCoherente.Should().BeTrue();
        new PayrollRun { Kind = PayrollRunKind.ServiceBonus, PayPeriodId = 1, CutoffDate = new DateOnly(2026, 12, 31) }.EsCoherente.Should().BeFalse(
            "una especial con período mezclaría las dos semánticas");
        new PayrollRun { Kind = PayrollRunKind.Settlement, PayPeriodId = null, CutoffDate = null }.EsCoherente.Should().BeFalse(
            "una especial sin corte no se puede indexar ni fechar");
    }

    [Theory]
    [MemberData(nameof(Proveedores))]
    public void ElUnicoPorPeriodo_SeReemplazaPorCincoFiltradosPorTipo(bool postgres)
    {
        var indices = Corrida(postgres).GetIndexes().ToList();
        var nombres = indices.Select(i => i.GetDatabaseName()).ToList();

        nombres.Should().NotContain("UK_PAY_PayrollRuns_Period_Version",
            "con PayPeriodId NULL en las especiales, ese índice colisionaría en SQL Server (un solo NULL por índice único)");

        var esperados = new Dictionary<string, (string[] Columnas, int Kind)>
        {
            ["UK_PAY_PayrollRuns_Ordinary_Period_Version"] = (["PayPeriodId", "Version"], 0),
            ["UK_PAY_PayrollRuns_ServiceBonus_Year_Semester_Version"] = (["Year", "Semester", "Version"], 1),
            ["UK_PAY_PayrollRuns_Severance_Year_Version"] = (["Year", "Version"], 2),
            // D-32: una corrida por movimiento; el corte de un disfrute futuro es «hoy» y dos movimientos del mismo día lo comparten.
            ["UK_PAY_PayrollRuns_Vacation_Movement_Version"] = (["VacationMovementId", "Version"], 3),
            ["UK_PAY_PayrollRuns_Settlement_Employee_Cutoff_Version"] = (["EmployeeId", "CutoffDate", "Version"], 4),
        };

        foreach (var (nombre, (columnas, kind)) in esperados)
        {
            var indice = indices.SingleOrDefault(i => i.GetDatabaseName() == nombre);
            indice.Should().NotBeNull($"falta el índice {nombre}");
            indice!.IsUnique.Should().BeTrue();
            indice.Properties.Select(p => p.GetColumnName()).Should().Equal(columnas);

            // ProviderModelConventions traduce el filtro T-SQL canónico a PostgreSQL (corchetes →
            // comillas). El número no se toca: Kind es un entero, no un booleano.
            var filtroEsperado = postgres ? $"\"Kind\" = {kind}" : $"[Kind] = {kind}";
            indice.GetFilter().Should().Be(filtroEsperado);
        }

        nombres.Should().Contain("IX_PAY_PayrollRuns_Kind_Status", "las listas por tipo lo necesitan");
        nombres.Should().Contain("IX_PAY_PayrollRuns_Period_Status", "el de la 005 se conserva");
    }

    [Theory]
    [MemberData(nameof(Proveedores))]
    public void LasReferenciasDeLaCorridaEspecial_SonRestrict(bool postgres)
    {
        // Nada se borra en cascada en nómina (data-model 010): borrar un empleado, una terminación
        // o un movimiento con corrida tiene que fallar, no arrastrar la corrida.
        var fks = Corrida(postgres).GetForeignKeys().ToList();

        foreach (var columna in new[] { nameof(PayrollRun.EmployeeId), nameof(PayrollRun.TerminationId), nameof(PayrollRun.VacationMovementId), nameof(PayrollRun.PayPeriodId) })
        {
            var fk = fks.SingleOrDefault(f => f.Properties.Count == 1 && f.Properties[0].Name == columna);
            fk.Should().NotBeNull($"falta la FK por {columna}");
            fk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        }
    }

    [Theory]
    [MemberData(nameof(Proveedores))]
    public void UnaSolaTerminacionVivaPorFicha_SonDosIndicesSobreLaMismaColumna(bool postgres)
    {
        // data-model §2.5: «(EmployeeId) con [Status] IN (0, 1)» escrito como dos índices porque el
        // traductor de filtros sólo entiende comparaciones simples. Con las mismas propiedades EF
        // devuelve el MISMO índice salvo que el nombre vaya en HasIndex; esto vigila que sigan siendo dos.
        var indices = Modelo(postgres).FindEntityType(typeof(Domain.Entities.Payroll.EmploymentTermination))!
            .GetIndexes().Where(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == "EmployeeId").ToList();

        indices.Should().HaveCount(2);
        indices.Select(i => i.GetDatabaseName()).Should().BeEquivalentTo(
            ["UK_PAY_EmploymentTerminations_Employee_Registered", "UK_PAY_EmploymentTerminations_Employee_Settled"]);
        indices.Select(i => i.GetFilter()).Should().BeEquivalentTo(postgres
            ? ["\"Status\" = 0 AND \"IsDeleted\" = FALSE", "\"Status\" = 1 AND \"IsDeleted\" = FALSE"]
            : ["[Status] = 0 AND [IsDeleted] = 0", "[Status] = 1 AND [IsDeleted] = 0"]);
    }

    [Fact]
    public void ElOrigenContable_TieneNombrePorTipo_YLaOrdinariaConservaElSuyo()
    {
        // Los comprobantes NM ya emitidos llevan SourceType «PayrollRun»; cambiarlo rompería la
        // trazabilidad hacia atrás. Los otros cuatro son nuevos y distintos entre sí.
        PayrollRun.SourceTypeNameDe(PayrollRunKind.Ordinary).Should().Be("PayrollRun");
        var especiales = new[] { PayrollRunKind.ServiceBonus, PayrollRunKind.Severance, PayrollRunKind.Vacation, PayrollRunKind.Settlement }
            .Select(PayrollRun.SourceTypeNameDe).ToList();
        especiales.Should().OnlyHaveUniqueItems().And.NotContain("PayrollRun");
        especiales.Should().Equal("ServiceBonusRun", "SeveranceRun", "VacationRun", "SettlementRun");
    }
}
