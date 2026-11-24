using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class FiltrosDelProcesoDeCausacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiscountCycle",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentScope",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentForm",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountCycle",
                schema: "dbo",
                table: "LND_AccrualProcesses");

            migrationBuilder.DropColumn(
                name: "InstallmentScope",
                schema: "dbo",
                table: "LND_AccrualProcesses");

            migrationBuilder.DropColumn(
                name: "PaymentForm",
                schema: "dbo",
                table: "LND_AccrualProcesses");
        }
    }
}
