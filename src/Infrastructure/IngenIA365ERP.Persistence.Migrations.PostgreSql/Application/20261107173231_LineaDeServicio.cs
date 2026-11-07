using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class LineaDeServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsService",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsService",
                schema: "dbo",
                table: "LND_CreditLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsService",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "IsService",
                schema: "dbo",
                table: "LND_CreditLines");
        }
    }
}
