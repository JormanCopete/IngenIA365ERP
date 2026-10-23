using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class PlataformaParaRecaudoYTesoreria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COR_GoLiveChecklists",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScenarioAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_COR_GoLiveChecklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_PersonBankAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HolderTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CertificateAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacesId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_COR_PersonBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_PersonBankAccounts_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_PersonBankAccounts_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_PersonBankAccounts_COR_PersonBankAccounts_ReplacesId",
                        column: x => x.ReplacesId,
                        principalSchema: "dbo",
                        principalTable: "COR_PersonBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_GoLiveChecklistItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChecklistId = table.Column<int>(type: "int", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OverrideApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    OverrideApprovedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OverrideApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_COR_GoLiveChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_GoLiveChecklistItems_COR_GoLiveChecklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalSchema: "dbo",
                        principalTable: "COR_GoLiveChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_COR_Sequences_Name_RcdTes",
                schema: "dbo",
                table: "COR_Sequences",
                column: "Name",
                unique: true,
                filter: "[DocumentType] IN ('RCD', 'TES')");

            migrationBuilder.CreateIndex(
                name: "UK_COR_GoLiveChecklistItems_ChecklistId_Code",
                schema: "dbo",
                table: "COR_GoLiveChecklistItems",
                columns: new[] { "ChecklistId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_GoLiveChecklistItems_PublicId",
                schema: "dbo",
                table: "COR_GoLiveChecklistItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_GoLiveChecklists_PublicId",
                schema: "dbo",
                table: "COR_GoLiveChecklists",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_PersonBankAccounts_BankId_AccountNumber",
                schema: "dbo",
                table: "COR_PersonBankAccounts",
                columns: new[] { "BankId", "AccountNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_PersonBankAccounts_PersonId_Status",
                schema: "dbo",
                table: "COR_PersonBankAccounts",
                columns: new[] { "PersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_PersonBankAccounts_ReplacesId",
                schema: "dbo",
                table: "COR_PersonBankAccounts",
                column: "ReplacesId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_PersonBankAccounts_PublicId",
                schema: "dbo",
                table: "COR_PersonBankAccounts",
                column: "PublicId",
                unique: true);

            // Feature 015 (T0049; R-21, Q4; data-model §16.1): copia idempotente de los campos sueltos de proveedor de la persona
            // (COR_People.SupplierBank*) como cuentas Imported, activas desde hoy y sin certificación (la verificación de la cuenta destino lo
            // marca). Sólo se copian las que tienen banco y número y cuyo código de banco casa con COR_Banks.TransferCode o LegacyCode (decisión
            // del dueño del 2026-10-24); las demás siguen en la persona. Una segunda corrida no duplica: sólo copia a quien no tiene una Imported.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[COR_PersonBankAccounts] ([PublicId], [PersonId], [BankId], [AccountType], [AccountNumber], [HolderName], [HolderTaxId],
    [Origin], [Status], [RequestedByName], [PayableFrom], [CreatedAt], [CreatedBy], [IsDeleted])
SELECT NEWID(), p.[Id], b.[Id],
    CASE UPPER(LTRIM(RTRIM(COALESCE(p.[SupplierBankAccountType], N''))))
        WHEN N'' THEN N'Ahorros' WHEN N'1' THEN N'Ahorros' WHEN N'AH' THEN N'Ahorros' WHEN N'A' THEN N'Ahorros'
        WHEN N'2' THEN N'Corriente' WHEN N'CC' THEN N'Corriente' WHEN N'CO' THEN N'Corriente' WHEN N'C' THEN N'Corriente'
        ELSE LTRIM(RTRIM(p.[SupplierBankAccountType])) END,
    LEFT(UPPER(REPLACE(REPLACE(LTRIM(RTRIM(p.[SupplierBankAccountNumber])), N' ', N''), N'-', N'')), 30),
    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(p.[BusinessName])), N''), LTRIM(RTRIM(CONCAT(p.[FirstName], N' ', p.[LastName])))), 200),
    LEFT(p.[TaxId], 20),
    2, 2, N'Migración', CAST(SYSUTCDATETIME() AS date), SYSUTCDATETIME(), N'SYSTEM', 0
FROM [dbo].[COR_People] p
CROSS APPLY (
    SELECT TOP (1) bk.[Id] FROM [dbo].[COR_Banks] bk
    WHERE bk.[IsDeleted] = 0
      AND (bk.[TransferCode] = LTRIM(RTRIM(p.[SupplierBankCode])) OR bk.[LegacyCode] = LTRIM(RTRIM(p.[SupplierBankCode])))
    ORDER BY bk.[Id]) b
WHERE p.[IsDeleted] = 0
  AND NULLIF(LTRIM(RTRIM(p.[SupplierBankCode])), N'') IS NOT NULL
  AND NULLIF(LTRIM(RTRIM(p.[SupplierBankAccountNumber])), N'') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [dbo].[COR_PersonBankAccounts] c WHERE c.[PersonId] = p.[Id] AND c.[Origin] = 2);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COR_GoLiveChecklistItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_PersonBankAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_GoLiveChecklists",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_COR_Sequences_Name_RcdTes",
                schema: "dbo",
                table: "COR_Sequences");
        }
    }
}
