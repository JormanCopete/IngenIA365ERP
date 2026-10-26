using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class TesoreriaAmpliada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TES_BankNotes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    StatementLinePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    NoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ThirdPartyPersonId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_BankNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_BankNotes_COR_People_ThirdPartyPersonId",
                        column: x => x.ThirdPartyPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankNotes_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankNotes_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankNotes_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_LegacyPendingLoads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    LegacyReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    CheckNumber = table.Column<long>(type: "bigint", nullable: true),
                    BeneficiaryPersonId = table.Column<int>(type: "int", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MigratedToPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LoadedByUserId = table.Column<int>(type: "int", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_LegacyPendingLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_LegacyPendingLoads_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_LegacyPendingLoads_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_LegacyPendingLoads_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PayrollDispersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FilePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    SourceBankCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SourceAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_TES_PayrollDispersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PayrollDispersions_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_ReconciliationRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Criterion = table.Column<int>(type: "int", nullable: false),
                    ToleranceDays = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TES_ReconciliationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_ReconciliationRules_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankNotes_Account_Status",
                schema: "dbo",
                table: "TES_BankNotes",
                columns: new[] { "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankNotes_DecidedByUserId",
                schema: "dbo",
                table: "TES_BankNotes",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankNotes_RegisteredByUserId",
                schema: "dbo",
                table: "TES_BankNotes",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankNotes_StatementLine",
                schema: "dbo",
                table: "TES_BankNotes",
                column: "StatementLinePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankNotes_ThirdPartyPersonId",
                schema: "dbo",
                table: "TES_BankNotes",
                column: "ThirdPartyPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankNotes_Number",
                schema: "dbo",
                table: "TES_BankNotes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankNotes_PublicId",
                schema: "dbo",
                table: "TES_BankNotes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_LegacyPendingLoads_BankAccountId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_LegacyPendingLoads_BeneficiaryPersonId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_LegacyPendingLoads_LoadedByUserId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_LegacyPendingLoads_Kind_Reference",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                columns: new[] { "Kind", "LegacyReference" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_TES_LegacyPendingLoads_PublicId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PayrollDispersions_Account_PayDate",
                schema: "dbo",
                table: "TES_PayrollDispersions",
                columns: new[] { "BankAccountId", "PayDate" });

            migrationBuilder.CreateIndex(
                name: "UK_TES_PayrollDispersions_File",
                schema: "dbo",
                table: "TES_PayrollDispersions",
                column: "FilePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PayrollDispersions_PublicId",
                schema: "dbo",
                table: "TES_PayrollDispersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconciliationRules_Account_Priority",
                schema: "dbo",
                table: "TES_ReconciliationRules",
                columns: new[] { "BankAccountId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "UK_TES_ReconciliationRules_Code",
                schema: "dbo",
                table: "TES_ReconciliationRules",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_TES_ReconciliationRules_PublicId",
                schema: "dbo",
                table: "TES_ReconciliationRules",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TES_BankNotes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_LegacyPendingLoads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PayrollDispersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_ReconciliationRules",
                schema: "dbo");
        }
    }
}
