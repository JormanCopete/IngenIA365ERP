using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class AjusteDelCorte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CutoverAdjustedAt",
                schema: "dbo",
                table: "LND_LoanControls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detail",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CutoverAdjustedAt",
                schema: "dbo",
                table: "LND_LoanControls");

            migrationBuilder.DropColumn(
                name: "Detail",
                schema: "dbo",
                table: "LND_AccrualProcessLines");
        }
    }
}
