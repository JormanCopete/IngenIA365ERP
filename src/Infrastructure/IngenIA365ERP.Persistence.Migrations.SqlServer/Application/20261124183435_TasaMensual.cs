using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class TasaMensual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRate",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                type: "decimal(9,8)",
                precision: 9,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateBasis",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRate",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "decimal(9,8)",
                precision: 9,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RateBasis",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyRate",
                schema: "dbo",
                table: "LND_CreditLineRateBands",
                type: "decimal(9,8)",
                precision: 9,
                scale: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlyRate",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropColumn(
                name: "RateBasis",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropColumn(
                name: "MonthlyRate",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "RateBasis",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "MonthlyRate",
                schema: "dbo",
                table: "LND_CreditLineRateBands");
        }
    }
}
