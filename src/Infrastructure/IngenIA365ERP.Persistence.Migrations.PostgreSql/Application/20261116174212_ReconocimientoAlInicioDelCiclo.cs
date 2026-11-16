using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class ReconocimientoAlInicioDelCiclo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstallmentNumber",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RecognitionDate",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstallmentNumber",
                schema: "dbo",
                table: "LND_AccrualProcessLines");

            migrationBuilder.DropColumn(
                name: "RecognitionDate",
                schema: "dbo",
                table: "LND_AccrualProcessLines");
        }
    }
}
