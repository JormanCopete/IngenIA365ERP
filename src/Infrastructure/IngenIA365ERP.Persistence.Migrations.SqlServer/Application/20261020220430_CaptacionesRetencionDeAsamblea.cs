using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CaptacionesRetencionDeAsamblea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WithheldAmount",
                schema: "dbo",
                table: "LND_AssemblyDistributionLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "WithholdingConcept",
                schema: "dbo",
                table: "LND_AssemblyDistributionLines",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingRate",
                schema: "dbo",
                table: "LND_AssemblyDistributionLines",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WithheldAmount",
                schema: "dbo",
                table: "LND_AssemblyDistributionLines");

            migrationBuilder.DropColumn(
                name: "WithholdingConcept",
                schema: "dbo",
                table: "LND_AssemblyDistributionLines");

            migrationBuilder.DropColumn(
                name: "WithholdingRate",
                schema: "dbo",
                table: "LND_AssemblyDistributionLines");
        }
    }
}
