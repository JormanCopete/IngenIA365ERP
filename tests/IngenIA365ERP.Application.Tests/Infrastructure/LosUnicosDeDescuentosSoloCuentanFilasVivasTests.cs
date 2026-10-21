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
/// <c>(TerminationId, ObligationPublicId)</c> o <c>(TerminationId, RecurringNoveltyId)</c>. Los dos únicos de
/// <c>PAY_SettlementDeductions</c> filtraban sólo «no nulo» y ese INSERT violaba el índice (500 sin sobre).
/// Aquí se fija que, en el modelo de <b>cada</b> proveedor, los dos únicos cuentan sólo filas vivas, como el
/// resto de los únicos del módulo (data-model §2.6), y que el filtro va traducido a la sintaxis del motor.
/// <para>
/// Feature 013 (T0113; data-model parte 3 §7.1, §7.2): el único vivo de los créditos pasa a la obligación de Cartera,
/// <c>UK_PAY_SettlementDeductions_Termination_Obligation (TerminationId, ObligationPublicId)</c>; el heredado
/// <c>…_Termination_Loan</c> se fue con su columna en <c>RetiroDeLaCarteraHeredada</c> (T0190). Y <c>PAY_PayrollLoanDeductions</c> tiene su único
/// por corrida, empleado y obligación entre las filas vivas.
/// </para>
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
        { true, "UK_PAY_SettlementDeductions_Termination_Obligation" },
        { false, "UK_PAY_SettlementDeductions_Termination_Obligation" },
        { true, "UK_PAY_SettlementDeductions_Termination_Libranza" },
        { false, "UK_PAY_SettlementDeductions_Termination_Libranza" },
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void El_unico_heredado_del_credito_ya_no_existe(bool postgres)
    {
        // Feature 013 (T0190): RetiroDeLaCarteraHeredada suelta la columna LoanPortfolioId, su FK, su IX y este único.
        var descuentos = Descuentos(postgres);
        descuentos.GetIndexes().Select(i => i.GetDatabaseName()).Should().NotContain("UK_PAY_SettlementDeductions_Termination_Loan");
        descuentos.FindProperty("LoanPortfolioId").Should().BeNull();
    }

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void El_unico_de_la_obligacion_va_por_terminacion_y_obligacion(bool postgres)
    {
        var indice = Descuentos(postgres).GetIndexes().Single(i => i.GetDatabaseName() == "UK_PAY_SettlementDeductions_Termination_Obligation");

        indice.Properties.Select(p => p.Name).Should().Equal(nameof(SettlementDeduction.TerminationId), nameof(SettlementDeduction.ObligationPublicId));
        indice.GetFilter().Should().Contain(postgres ? "\"ObligationPublicId\" IS NOT NULL" : "[ObligationPublicId] IS NOT NULL");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Los_descuentos_de_Cartera_de_la_ordinaria_son_unicos_por_corrida_empleado_y_obligacion_entre_vivas(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres ? builder.UseNpgsql(CadenaPg) : builder.UseSqlServer(CadenaSql);
        using var db = new ApplicationDbContext(builder.Options);
        var tipo = db.Model.FindEntityType(typeof(Domain.Entities.Payroll.Transactions.PayrollLoanDeduction))!;
        tipo.GetTableName().Should().Be("PAY_PayrollLoanDeductions");

        var indice = tipo.GetIndexes().Single(i => i.GetDatabaseName() == "UK_PAY_PayrollLoanDeductions_Run_Employee_Obligation");

        indice.IsUnique.Should().BeTrue();
        indice.Properties.Select(p => p.Name).Should().Equal("PayrollRunId", "PayrollRunEmployeeId", "ObligationPublicId");
        indice.GetFilter().Should().Contain(postgres ? "\"IsDeleted\" = FALSE" : "[IsDeleted] = 0");
        tipo.GetForeignKeys().Should().NotContain(fk => fk.PrincipalEntityType.GetTableName()!.StartsWith("LND_"), "ninguna tabla PAY_ apunta a Cartera (FR-008)");
    }
}
