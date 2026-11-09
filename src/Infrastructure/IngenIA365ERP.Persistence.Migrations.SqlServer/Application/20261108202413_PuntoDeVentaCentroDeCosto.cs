using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class PuntoDeVentaCentroDeCosto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCenterId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PointsOfSale_CostCenterId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "CostCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_INV_PointsOfSale_COR_CostCenters_CostCenterId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "CostCenterId",
                principalSchema: "dbo",
                principalTable: "COR_CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_PointsOfSale_COR_CostCenters_CostCenterId",
                schema: "dbo",
                table: "INV_PointsOfSale");

            migrationBuilder.DropIndex(
                name: "IX_INV_PointsOfSale_CostCenterId",
                schema: "dbo",
                table: "INV_PointsOfSale");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                schema: "dbo",
                table: "INV_PointsOfSale");
        }
    }
}
