using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class DescuentoPorPagoAnticipado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceDiscount",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceDiscountFraction",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvanceDiscount",
                schema: "dbo",
                table: "RCD_ReceiptApplications");

            migrationBuilder.DropColumn(
                name: "AdvanceDiscountFraction",
                schema: "dbo",
                table: "RCD_ReceiptApplications");
        }
    }
}
