using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CaptacionesRespuestasDelDueno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationBankAccountNumber",
                schema: "dbo",
                table: "LND_MigrationTermDepositRows",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "DestinationBankAccountType",
                schema: "dbo",
                table: "LND_MigrationTermDepositRows",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationBankTransferCode",
                schema: "dbo",
                table: "LND_MigrationTermDepositRows",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccruedInterest",
                schema: "dbo",
                table: "LND_LiquidityFundHoldings",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "CapitalizesInterest",
                schema: "dbo",
                table: "LND_LiquidityFundHoldings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TermTitleKind",
                schema: "dbo",
                table: "LND_LiquidityFundHoldings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsObligatedToKeepBooks",
                schema: "dbo",
                table: "COR_People",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationBankAccountNumber",
                schema: "dbo",
                table: "LND_MigrationTermDepositRows");

            migrationBuilder.DropColumn(
                name: "DestinationBankAccountType",
                schema: "dbo",
                table: "LND_MigrationTermDepositRows");

            migrationBuilder.DropColumn(
                name: "DestinationBankTransferCode",
                schema: "dbo",
                table: "LND_MigrationTermDepositRows");

            migrationBuilder.DropColumn(
                name: "AccruedInterest",
                schema: "dbo",
                table: "LND_LiquidityFundHoldings");

            migrationBuilder.DropColumn(
                name: "CapitalizesInterest",
                schema: "dbo",
                table: "LND_LiquidityFundHoldings");

            migrationBuilder.DropColumn(
                name: "TermTitleKind",
                schema: "dbo",
                table: "LND_LiquidityFundHoldings");

            migrationBuilder.DropColumn(
                name: "IsObligatedToKeepBooks",
                schema: "dbo",
                table: "COR_People");
        }
    }
}
