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
                name: "INV_SupplierInvoicePayments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOrderPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TreasuryReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
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
                    table.PrimaryKey("PK_INV_SupplierInvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_SupplierInvoicePayments_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_AcquirerSettlements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CardAcquirerId = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    BankFileFormatId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StatementLinePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatementAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionVatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingTaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingVatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingIcaAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tolerance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FactMessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_TES_AcquirerSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_AcquirerSettlements_COR_BankFileFormats_BankFileFormatId",
                        column: x => x.BankFileFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_AcquirerSettlements_COR_CardAcquirers_CardAcquirerId",
                        column: x => x.CardAcquirerId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardAcquirers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_AcquirerSettlements_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_AcquirerSettlements_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_AcquirerSettlements_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "TES_CashFlowCategories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TES_CashFlowCategories", x => x.Id);
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
                name: "TES_LiquiditySnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TakenByUserId = table.Column<int>(type: "int", nullable: false),
                    BanksTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VaultsTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashSessionsTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommitmentsTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedLending = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedDeposits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Available = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Projected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiquidityFundTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ComponentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissingComponentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FundMessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_TES_LiquiditySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_LiquiditySnapshots_SEC_Users_TakenByUserId",
                        column: x => x.TakenByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
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

            migrationBuilder.CreateTable(
                name: "TES_AcquirerSettlementLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettlementId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    TerminalCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BatchDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionVatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: true),
                    CollectedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_TES_AcquirerSettlementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_AcquirerSettlementLines_TES_AcquirerSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalSchema: "dbo",
                        principalTable: "TES_AcquirerSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_CashFlowProjections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    PeriodKind = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TES_CashFlowProjections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_CashFlowProjections_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_CashFlowProjections_TES_CashFlowCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "dbo",
                        principalTable: "TES_CashFlowCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_SupplierInvoicePayments_Document",
                schema: "dbo",
                table: "INV_SupplierInvoicePayments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoicePayments_Message",
                schema: "dbo",
                table: "INV_SupplierInvoicePayments",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoicePayments_PublicId",
                schema: "dbo",
                table: "INV_SupplierInvoicePayments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_AcquirerSettlementLines_PublicId",
                schema: "dbo",
                table: "TES_AcquirerSettlementLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_AcquirerSettlementLines_Settlement_Line",
                schema: "dbo",
                table: "TES_AcquirerSettlementLines",
                columns: new[] { "SettlementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_AcquirerSettlements_Account_Date",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                columns: new[] { "BankAccountId", "SettlementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_AcquirerSettlements_BankFileFormatId",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                column: "BankFileFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_AcquirerSettlements_DecidedByUserId",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_AcquirerSettlements_RegisteredByUserId",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_AcquirerSettlements_Acquirer_Sha256",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                columns: new[] { "CardAcquirerId", "FileSha256" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_TES_AcquirerSettlements_Number",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_AcquirerSettlements_PublicId",
                schema: "dbo",
                table: "TES_AcquirerSettlements",
                column: "PublicId",
                unique: true);

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
                name: "UK_TES_CashFlowCategories_Code",
                schema: "dbo",
                table: "TES_CashFlowCategories",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_TES_CashFlowCategories_PublicId",
                schema: "dbo",
                table: "TES_CashFlowCategories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_CashFlowProjections_RegisteredByUserId",
                schema: "dbo",
                table: "TES_CashFlowProjections",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_CashFlowProjections_Category_Period",
                schema: "dbo",
                table: "TES_CashFlowProjections",
                columns: new[] { "CategoryId", "PeriodKind", "PeriodStart" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_TES_CashFlowProjections_PublicId",
                schema: "dbo",
                table: "TES_CashFlowProjections",
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
                name: "IX_TES_LiquiditySnapshots_Date",
                schema: "dbo",
                table: "TES_LiquiditySnapshots",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_TES_LiquiditySnapshots_TakenByUserId",
                schema: "dbo",
                table: "TES_LiquiditySnapshots",
                column: "TakenByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_LiquiditySnapshots_Number",
                schema: "dbo",
                table: "TES_LiquiditySnapshots",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_LiquiditySnapshots_PublicId",
                schema: "dbo",
                table: "TES_LiquiditySnapshots",
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
            // Feature 015, T2 (fases 41 a 43): las adopciones de los mensajes nuevos de T2 por sus destinos ya registrados (una Tesorería activada antes de T2 no los
            // recibiría hasta adoptarlos; INV y CAR los reciben desde aquí). Idempotente; sin el participante, no hace nada.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT p.[Id], v.[Contrato], CAST(v.[Version] AS smallint), '1900-01-01', NULL, N'Migración TesoreriaAmpliada', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES
    (N'INV', N'Inventory', N'PagoDeFacturaDeProveedor', 1),
    (N'CAR', N'Lending', N'SaldoDelFondoDeLiquidez', 1),
    (N'TES', N'Treasury', N'DispersionDeNominaGenerada', 1),
    (N'TES', N'Treasury', N'FacturaDeProveedorPorPagar', 1),
    (N'TES', N'Treasury', N'AnulacionDeFacturaDeProveedor', 1)
) AS v([Code], [Destino], [Contrato], [Version])
JOIN [dbo].[COR_IntegrationParticipants] p
  ON p.[Code] = v.[Code] AND p.[Role] = 2 AND p.[Destination] = v.[Destino] AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0 AND p.[IsTestDouble] = 0
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
    WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = v.[Contrato] AND a.[Version] = v.[Version]);
");

            // Feature 015, T2 (fase 43, T0460): las categorías sugeridas del flujo de caja (la cooperativa las cambia o desactiva). Idempotente.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[TES_CashFlowCategories] ([Code], [Name], [Direction], [Source], [SortOrder], [IsActive], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT v.[Code], v.[Nombre], v.[Sentido], v.[Fuente], v.[Orden], 1, NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES
    (N'CARTERA', N'Cuotas de Cartera por cobrar', 1, 2, 10),
    (N'CAPTAC', N'Cuotas de aportes y ahorros por cobrar', 1, 3, 20),
    (N'OTROSING', N'Otros ingresos', 1, 1, 30),
    (N'ORDENES', N'Órdenes de pago', 2, 4, 40),
    (N'NOMINA', N'Dispersiones de nómina', 2, 5, 50),
    (N'OTROSEGR', N'Otros egresos', 2, 1, 60)
) AS v([Code], [Nombre], [Sentido], [Fuente], [Orden])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[TES_CashFlowCategories] c WHERE c.[Code] = v.[Code] AND c.[IsDeleted] = 0);
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [dbo].[COR_IntegrationParticipantAdoptions] WHERE [Reason] = N'Migración TesoreriaAmpliada';");

            migrationBuilder.DropTable(
                name: "INV_SupplierInvoicePayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_AcquirerSettlementLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_BankNotes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_CashFlowProjections",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_LegacyPendingLoads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_LiquiditySnapshots",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PayrollDispersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_ReconciliationRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_AcquirerSettlements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_CashFlowCategories",
                schema: "dbo");
        }
    }
}
