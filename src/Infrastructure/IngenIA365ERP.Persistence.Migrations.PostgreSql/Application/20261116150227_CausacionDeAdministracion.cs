using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CausacionDeAdministracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Administration",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Administration",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "dbo",
                table: "LND_CreditLineInsurances",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Administration",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "Administration",
                schema: "dbo",
                table: "LND_LoanDailyPositions");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "dbo",
                table: "LND_CreditLineInsurances");
        }
    }
}
