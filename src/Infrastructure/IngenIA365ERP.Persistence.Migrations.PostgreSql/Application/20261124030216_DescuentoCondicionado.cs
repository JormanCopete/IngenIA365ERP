using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class DescuentoCondicionado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubsidizedPurchase",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubsidyAmount",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountOptions",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AssetValue",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountOptionCode",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierPersonId",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_SupplierPersonId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "SupplierPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplications_COR_People_SupplierPersonId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "SupplierPersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditApplications_COR_People_SupplierPersonId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropIndex(
                name: "IX_LND_CreditApplications_SupplierPersonId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropColumn(
                name: "SubsidizedPurchase",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropColumn(
                name: "SubsidyAmount",
                schema: "dbo",
                table: "LND_DisbursementOrders");

            migrationBuilder.DropColumn(
                name: "DiscountOptions",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "AssetValue",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropColumn(
                name: "DiscountOptionCode",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropColumn(
                name: "SupplierPersonId",
                schema: "dbo",
                table: "LND_CreditApplications");
        }
    }
}
