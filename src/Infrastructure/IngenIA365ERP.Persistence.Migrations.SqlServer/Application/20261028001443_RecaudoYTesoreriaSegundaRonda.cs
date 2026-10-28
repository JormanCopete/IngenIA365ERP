using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class RecaudoYTesoreriaSegundaRonda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_COR_GoLiveChecklistItems_ChecklistId_Code",
                schema: "dbo",
                table: "COR_GoLiveChecklistItems");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "dbo",
                table: "TES_Reconciliations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                schema: "dbo",
                table: "TES_Reconciliations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RetiredOn",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RetiredOn",
                schema: "dbo",
                table: "TES_BankAccountSigners",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnBankAccountPublicId",
                schema: "dbo",
                table: "RCD_ReceiptMeans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RCD_ScreeningReleases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashOperationId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleasedByUserId = table.Column<int>(type: "int", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RCD_ScreeningReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ScreeningReleases_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ScreeningReleases_RCD_CashOperations_CashOperationId",
                        column: x => x.CashOperationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ScreeningReleases_SEC_Users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_TransferAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    CashBoxId = table.Column<int>(type: "int", nullable: true),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_RCD_TransferAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_TransferAccounts_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_TransferAccounts_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_TransferAccounts_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_COR_GoLiveChecklistItems_ChecklistId_Module_Code",
                schema: "dbo",
                table: "COR_GoLiveChecklistItems",
                columns: new[] { "ChecklistId", "Module", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ScreeningReleases_PersonId",
                schema: "dbo",
                table: "RCD_ScreeningReleases",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ScreeningReleases_ReleasedByUserId",
                schema: "dbo",
                table: "RCD_ScreeningReleases",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ScreeningReleases_CashOperationId_PersonId",
                schema: "dbo",
                table: "RCD_ScreeningReleases",
                columns: new[] { "CashOperationId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ScreeningReleases_PublicId",
                schema: "dbo",
                table: "RCD_ScreeningReleases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_TransferAccounts_CashBoxId",
                schema: "dbo",
                table: "RCD_TransferAccounts",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_TransferAccounts_CollectionPointId_PaymentMeansId",
                schema: "dbo",
                table: "RCD_TransferAccounts",
                columns: new[] { "CollectionPointId", "PaymentMeansId" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_TransferAccounts_PaymentMeansId",
                schema: "dbo",
                table: "RCD_TransferAccounts",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_TransferAccounts_PublicId",
                schema: "dbo",
                table: "RCD_TransferAccounts",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_ScreeningReleases",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_TransferAccounts",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_COR_GoLiveChecklistItems_ChecklistId_Module_Code",
                schema: "dbo",
                table: "COR_GoLiveChecklistItems");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "dbo",
                table: "TES_Reconciliations");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                schema: "dbo",
                table: "TES_Reconciliations");

            migrationBuilder.DropColumn(
                name: "RetiredOn",
                schema: "dbo",
                table: "TES_BankAccountTokens");

            migrationBuilder.DropColumn(
                name: "RetiredOn",
                schema: "dbo",
                table: "TES_BankAccountSigners");

            migrationBuilder.DropColumn(
                name: "OwnBankAccountPublicId",
                schema: "dbo",
                table: "RCD_ReceiptMeans");

            migrationBuilder.CreateIndex(
                name: "UK_COR_GoLiveChecklistItems_ChecklistId_Code",
                schema: "dbo",
                table: "COR_GoLiveChecklistItems",
                columns: new[] { "ChecklistId", "Code" },
                unique: true);
        }
    }
}
