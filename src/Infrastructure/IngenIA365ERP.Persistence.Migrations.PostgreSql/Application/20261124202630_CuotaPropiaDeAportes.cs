using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CuotaPropiaDeAportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ContributionFirstDueDate",
                schema: "dbo",
                table: "LND_MemberAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContributionInstallmentAmount",
                schema: "dbo",
                table: "LND_MemberAccounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContributionInstallmentPeriodicity",
                schema: "dbo",
                table: "LND_MemberAccounts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContributionPaymentChannel",
                schema: "dbo",
                table: "LND_MemberAccounts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributionFirstDueDate",
                schema: "dbo",
                table: "LND_MemberAccounts");

            migrationBuilder.DropColumn(
                name: "ContributionInstallmentAmount",
                schema: "dbo",
                table: "LND_MemberAccounts");

            migrationBuilder.DropColumn(
                name: "ContributionInstallmentPeriodicity",
                schema: "dbo",
                table: "LND_MemberAccounts");

            migrationBuilder.DropColumn(
                name: "ContributionPaymentChannel",
                schema: "dbo",
                table: "LND_MemberAccounts");
        }
    }
}
