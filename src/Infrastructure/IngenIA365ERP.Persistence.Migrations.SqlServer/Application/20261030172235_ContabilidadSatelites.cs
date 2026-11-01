using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class ContabilidadSatelites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OpeningAccumulated",
                schema: "dbo",
                table: "ACC_FixedAssets",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OpeningElapsedMonths",
                schema: "dbo",
                table: "ACC_FixedAssets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LayoutJson",
                schema: "dbo",
                table: "ACC_ExogenousFormats",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificatesExcludedModules",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "CAR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningAccumulated",
                schema: "dbo",
                table: "ACC_FixedAssets");

            migrationBuilder.DropColumn(
                name: "OpeningElapsedMonths",
                schema: "dbo",
                table: "ACC_FixedAssets");

            migrationBuilder.DropColumn(
                name: "LayoutJson",
                schema: "dbo",
                table: "ACC_ExogenousFormats");

            migrationBuilder.DropColumn(
                name: "CertificatesExcludedModules",
                schema: "dbo",
                table: "ACC_AccountingSetups");
        }
    }
}
