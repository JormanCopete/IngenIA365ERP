using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CuotaDejadaALaPagaduria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LoanClosedAtResult",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LeftInstallmentAmount",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeftInstallmentNumber",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeftInstallmentSettledAt",
                schema: "dbo",
                table: "LND_DisbursementPayoffs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoanClosedAtResult",
                schema: "dbo",
                table: "LND_PayrollDeductionResults");

            migrationBuilder.DropColumn(
                name: "LeftInstallmentAmount",
                schema: "dbo",
                table: "LND_DisbursementPayoffs");

            migrationBuilder.DropColumn(
                name: "LeftInstallmentNumber",
                schema: "dbo",
                table: "LND_DisbursementPayoffs");

            migrationBuilder.DropColumn(
                name: "LeftInstallmentSettledAt",
                schema: "dbo",
                table: "LND_DisbursementPayoffs");
        }
    }
}
