using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CaptacionesRegiroDeOrdenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_LND_AccountPayoutOrders_Operation",
                schema: "dbo",
                table: "LND_AccountPayoutOrders");

            migrationBuilder.AddColumn<int>(
                name: "ReissuesOrderId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_ReissuesOrderId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "ReissuesOrderId",
                filter: "[ReissuesOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountPayoutOrders_Operation",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "OperationId",
                unique: true,
                filter: "[ReissuesOrderId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountPayoutOrders_ReissuesOrder",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "ReissuesOrderId",
                principalSchema: "dbo",
                principalTable: "LND_AccountPayoutOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountPayoutOrders_ReissuesOrder",
                schema: "dbo",
                table: "LND_AccountPayoutOrders");

            migrationBuilder.DropIndex(
                name: "IX_LND_AccountPayoutOrders_ReissuesOrderId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders");

            migrationBuilder.DropIndex(
                name: "UK_LND_AccountPayoutOrders_Operation",
                schema: "dbo",
                table: "LND_AccountPayoutOrders");

            migrationBuilder.DropColumn(
                name: "ReissuesOrderId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountPayoutOrders_Operation",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "OperationId",
                unique: true);
        }
    }
}
