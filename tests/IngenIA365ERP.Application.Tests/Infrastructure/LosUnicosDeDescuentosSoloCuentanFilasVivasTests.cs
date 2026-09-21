using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Feature 010, revisión N1 (2026-09-21). El recálculo de la definitiva retira en blando la deuda que Cartera
/// ya no trae (<c>IsDeleted = true</c>) y, si la misma obligación vuelve, crea otra fila con la misma llave
/// <c>(TerminationId, LoanPortfolioId)</c> o <c>(TerminationId, RecurringNoveltyId)</c>. Los dos únicos de
/// <c>PAY_SettlementDeductions</c> filtraban sólo «no nulo» y ese INSERT violaba el índice (500 sin sobre).
/// Aquí se fija que, en el modelo de <b>cada</b> proveedor, los dos únicos cuentan sólo filas vivas, como el
/// resto de los únicos del módulo (data-model §2.6), y que el filtro va traducido a la sintaxis del motor.
/// </summary>
public class LosUnicosDeDescuentosSoloCuentanFilasVivasTests
{
    private const string CadenaPg = "Host=localhost;Database=x;Username=y;Password=z";
    private const string CadenaSql = "Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True";

    private static IEntityType Descuentos(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres ? builder.UseNpgsql(CadenaPg) : builder.UseSqlServer(CadenaSql);
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model.FindEntityType(typeof(SettlementDeduction))!;
    }

    public static TheoryData<bool, string> Casos => new()
    {
        { true, "UK_PAY_SettlementDeductions_Termination_Loan" },
        { true, "UK_PAY_SettlementDeductions_Termination_Libranza" },
        { false, "UK_PAY_SettlementDeductions_Termination_Loan" },
        { false, "UK_PAY_SettlementDeductions_Termination_Libranza" },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_unico_excluye_las_filas_retiradas_en_blando(bool postgres, string nombre)
    {
        var indice = Descuentos(postgres).GetIndexes().Single(i => i.GetDatabaseName() == nombre);

        indice.IsUnique.Should().BeTrue();
        var filtro = indice.GetFilter();
        filtro.Should().NotBeNullOrEmpty();
        filtro.Should().Contain("IS NOT NULL", "la llave sólo aplica a las filas de ese tipo de deuda");
        if (postgres)
            filtro.Should().Contain("\"IsDeleted\" = FALSE", "una deuda retirada en blando que vuelve se inserta de nuevo con la misma llave");
        else
            filtro.Should().Contain("[IsDeleted] = 0", "una deuda retirada en blando que vuelve se inserta de nuevo con la misma llave");
    }
}
