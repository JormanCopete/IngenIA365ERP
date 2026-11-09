using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class AgilidadDeTecladoYMediosUnificados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Last4",
                schema: "dbo",
                table: "RCD_ReceiptMeans",
                type: "nchar(4)",
                fixedLength: true,
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckAccountNumber",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckBankId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckBankName",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckDrawerName",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CheckIssueDate",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckNumber",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CheckSamePlace",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnBankAccountLabel",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnBankAccountPublicId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                schema: "dbo",
                table: "COR_People",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "INV_TransferAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PointOfSaleId = table.Column<int>(type: "int", nullable: false),
                    CashRegisterId = table.Column<int>(type: "int", nullable: true),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountLabel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_TransferAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_TransferAccounts_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferAccounts_INV_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferAccounts_INV_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalSchema: "dbo",
                        principalTable: "INV_PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_CheckBankId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                column: "CheckBankId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_People_SearchText",
                schema: "dbo",
                table: "COR_People",
                column: "SearchText")
                .Annotation("SqlServer:Include", new[] { "TaxId", "FirstName", "LastName", "BusinessName", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferAccounts_CashRegisterId",
                schema: "dbo",
                table: "INV_TransferAccounts",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferAccounts_PaymentMeansId",
                schema: "dbo",
                table: "INV_TransferAccounts",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_TransferAccounts_Point_Register_Means_Account",
                schema: "dbo",
                table: "INV_TransferAccounts",
                columns: new[] { "PointOfSaleId", "CashRegisterId", "PaymentMeansId", "OwnBankAccountPublicId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_TransferAccounts_PublicId",
                schema: "dbo",
                table: "INV_TransferAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_DocumentPayments_COR_Banks_CheckBankId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                column: "CheckBankId",
                principalSchema: "dbo",
                principalTable: "COR_Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_DocumentPayments_COR_Banks_CheckBankId",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropTable(
                name: "INV_TransferAccounts",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_INV_DocumentPayments_CheckBankId",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropIndex(
                name: "IX_COR_People_SearchText",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "Last4",
                schema: "dbo",
                table: "RCD_ReceiptMeans");

            migrationBuilder.DropColumn(
                name: "CheckAccountNumber",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CheckBankId",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CheckBankName",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CheckDrawerName",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CheckIssueDate",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CheckNumber",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "CheckSamePlace",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "OwnBankAccountLabel",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "OwnBankAccountPublicId",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "SearchText",
                schema: "dbo",
                table: "COR_People");
        }
    }
}
