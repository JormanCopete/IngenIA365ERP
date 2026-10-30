using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Environment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScenarioAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    BankId = table.Column<int>(type: "integer", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HolderTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CertificateAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacesId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChecklistId = table.Column<int>(type: "integer", nullable: false),
                    Module = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvidenceAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OverrideApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    OverrideApprovedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OverrideApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverrideReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                filter: "\"DocumentType\" IN ('RCD', 'TES')");

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
INSERT INTO dbo.""COR_PersonBankAccounts"" (""PublicId"", ""PersonId"", ""BankId"", ""AccountType"", ""AccountNumber"", ""HolderName"", ""HolderTaxId"",
    ""Origin"", ""Status"", ""RequestedByName"", ""PayableFrom"", ""CreatedAt"", ""CreatedBy"", ""IsDeleted"")
SELECT gen_random_uuid(), p.""Id"", b.""Id"",
    CASE UPPER(TRIM(COALESCE(p.""SupplierBankAccountType"", '')))
        WHEN '' THEN 'Ahorros' WHEN '1' THEN 'Ahorros' WHEN 'AH' THEN 'Ahorros' WHEN 'A' THEN 'Ahorros'
        WHEN '2' THEN 'Corriente' WHEN 'CC' THEN 'Corriente' WHEN 'CO' THEN 'Corriente' WHEN 'C' THEN 'Corriente'
        ELSE TRIM(p.""SupplierBankAccountType"") END,
    LEFT(UPPER(REPLACE(REPLACE(TRIM(p.""SupplierBankAccountNumber""), ' ', ''), '-', '')), 30),
    LEFT(COALESCE(NULLIF(TRIM(p.""BusinessName""), ''), TRIM(CONCAT(p.""FirstName"", ' ', p.""LastName""))), 200),
    LEFT(p.""TaxId"", 20),
    2, 2, 'Migración', CURRENT_DATE, now(), 'SYSTEM', FALSE
FROM dbo.""COR_People"" p
CROSS JOIN LATERAL (
    SELECT bk.""Id"" FROM dbo.""COR_Banks"" bk
    WHERE bk.""IsDeleted"" = FALSE
      AND (bk.""TransferCode"" = TRIM(p.""SupplierBankCode"") OR bk.""LegacyCode"" = TRIM(p.""SupplierBankCode""))
    ORDER BY bk.""Id"" LIMIT 1) b
WHERE p.""IsDeleted"" = FALSE
  AND NULLIF(TRIM(p.""SupplierBankCode""), '') IS NOT NULL
  AND NULLIF(TRIM(p.""SupplierBankAccountNumber""), '') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.""COR_PersonBankAccounts"" c WHERE c.""PersonId"" = p.""Id"" AND c.""Origin"" = 2);
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
