using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 010, revisión N1 (2026-09-21). Los dos únicos de <c>PAY_SettlementDeductions</c> pasan a
    /// contar sólo filas vivas (<c>IsDeleted = 0</c>): el recálculo de la definitiva retira en blando la
    /// deuda que Cartera ya no trae y, si la misma obligación vuelve (un pago reversado en Cartera),
    /// crea otra fila con la misma llave; sin el filtro ese INSERT violaba el índice y respondía 500.
    /// Reversible: <c>Down</c> deja los índices como estaban.
    /// </summary>
    public partial class SettlementDeductionsUnicosEntreVivas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Libranza",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Libranza",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "RecurringNoveltyId" },
                unique: true,
                filter: "[RecurringNoveltyId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "LoanPortfolioId" },
                unique: true,
                filter: "[LoanPortfolioId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Libranza",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Libranza",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "RecurringNoveltyId" },
                unique: true,
                filter: "[RecurringNoveltyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "LoanPortfolioId" },
                unique: true,
                filter: "[LoanPortfolioId] IS NOT NULL");
        }
    }
}
