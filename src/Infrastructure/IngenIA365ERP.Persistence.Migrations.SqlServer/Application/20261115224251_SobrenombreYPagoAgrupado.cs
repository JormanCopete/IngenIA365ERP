using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class SobrenombreYPagoAgrupado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsolidatedIntoOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintAlias",
                schema: "dbo",
                table: "SEC_Users",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_ConsolidatedIntoOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ConsolidatedIntoOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_ScheduledBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ScheduledBankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TES_PaymentOrders_TES_BankAccounts_ScheduledBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ScheduledBankAccountId",
                principalSchema: "dbo",
                principalTable: "TES_BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_PaymentOrders_TES_PaymentOrders_ConsolidatedIntoOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ConsolidatedIntoOrderId",
                principalSchema: "dbo",
                principalTable: "TES_PaymentOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TES_PaymentOrders_TES_BankAccounts_ScheduledBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TES_PaymentOrders_TES_PaymentOrders_ConsolidatedIntoOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_ConsolidatedIntoOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_ScheduledBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ConsolidatedIntoOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ScheduledBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "PrintAlias",
                schema: "dbo",
                table: "SEC_Users");
        }
    }
}
