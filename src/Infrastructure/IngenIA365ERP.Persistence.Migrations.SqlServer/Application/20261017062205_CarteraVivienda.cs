using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CarteraVivienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UvrValue",
                schema: "dbo",
                table: "LND_RepaymentScheduleVersions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingPrincipalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningPrincipalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UvrAmount",
                schema: "dbo",
                table: "LND_LoanTransactions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UvrValue",
                schema: "dbo",
                table: "LND_LoanTransactions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DenominationUnit",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "PayoffAmountUvr",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalUvr",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UvrValue",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DenominationUnit",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsSocialHousing",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UvrValue",
                schema: "dbo",
                table: "LND_RepaymentScheduleVersions");

            migrationBuilder.DropColumn(
                name: "ClosingPrincipalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "InsuranceUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "InterestUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "OpeningPrincipalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "PrincipalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "TotalUvr",
                schema: "dbo",
                table: "LND_RepaymentInstallments");

            migrationBuilder.DropColumn(
                name: "UvrAmount",
                schema: "dbo",
                table: "LND_LoanTransactions");

            migrationBuilder.DropColumn(
                name: "UvrValue",
                schema: "dbo",
                table: "LND_LoanTransactions");

            migrationBuilder.DropColumn(
                name: "DenominationUnit",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropColumn(
                name: "PayoffAmountUvr",
                schema: "dbo",
                table: "LND_LoanDailyPositions");

            migrationBuilder.DropColumn(
                name: "PrincipalUvr",
                schema: "dbo",
                table: "LND_LoanDailyPositions");

            migrationBuilder.DropColumn(
                name: "UvrValue",
                schema: "dbo",
                table: "LND_LoanDailyPositions");

            migrationBuilder.DropColumn(
                name: "DenominationUnit",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "IsSocialHousing",
                schema: "dbo",
                table: "LND_CreditApplications");
        }
    }
}
