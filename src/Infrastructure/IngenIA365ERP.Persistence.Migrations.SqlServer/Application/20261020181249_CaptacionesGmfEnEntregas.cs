using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CaptacionesGmfEnEntregas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GmfOnDeceasedAndUnclaimed",
                schema: "dbo",
                table: "LND_DepositLineVersions",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GmfOnDeceasedAndUnclaimed",
                schema: "dbo",
                table: "LND_DepositLineVersions");
        }
    }
}
