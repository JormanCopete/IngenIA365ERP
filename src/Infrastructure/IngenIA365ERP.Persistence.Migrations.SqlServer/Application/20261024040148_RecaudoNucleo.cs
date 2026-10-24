using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class RecaudoNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RCD_ChannelModalityRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    LoanModality = table.Column<int>(type: "int", nullable: true),
                    AccountModality = table.Column<int>(type: "int", nullable: true),
                    OnlyWhenInstallmentsDue = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_RCD_ChannelModalityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CollectionPoints",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentPrefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsMainCashBoxVault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_RCD_CollectionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionPoints_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_DailySquares",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CashInBoxes = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashInVault = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashInTransit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LedgerBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EmittedPendingBatch = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExplainedByUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VaultBalanceMatches = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_RCD_DailySquares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_DailySquares_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_DailySquares_SEC_Users_ExplainedByUserId",
                        column: x => x.ExplainedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_DocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    UsesPointPrefix = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_MiscConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    RequiresThirdParty = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCrossDocument = table.Column<bool>(type: "bit", nullable: false),
                    RequiresInvoice = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_MiscConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PayerControls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PayerControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PayerControls_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_Setup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CutoffTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ContractVersionObligations = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ContractVersionAccounts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
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
                    table.PrimaryKey("PK_RCD_Setup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_SourceOfFundsDeclarations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    ReceiptOperationPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeclaredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThresholdVersionPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiredBy = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_SourceOfFundsDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_SourceOfFundsDeclarations_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_UserPaymasterScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PaymasterPersonId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_UserPaymasterScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_UserPaymasterScopes_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_UserPaymasterScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashBoxes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BaseMode = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashWarnCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CashBlockCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DailyCashOutCap = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsMainCashBox = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashBoxes_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CollectionPointDays",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DayCloseReportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CollectionPointDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionPointDays_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionPointDays_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_ContingencyBooks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    FromNumber = table.Column<int>(type: "int", nullable: false),
                    ToNumber = table.Column<int>(type: "int", nullable: false),
                    RegisteredBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_ContingencyBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ContingencyBooks_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PaymentMeansCollectionPoints",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_RCD_PaymentMeansCollectionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PaymentMeansCollectionPoints_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PaymentMeansCollectionPoints_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_SupervisorDelegations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    SupervisorUserId = table.Column<int>(type: "int", nullable: false),
                    DelegateUserId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_RCD_SupervisorDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_SupervisorDelegations_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_SupervisorDelegations_SEC_Users_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_SupervisorDelegations_SEC_Users_SupervisorUserId",
                        column: x => x.SupervisorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_UserCollectionPointScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_UserCollectionPointScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_UserCollectionPointScopes_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_UserCollectionPointScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashBoxCashiers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashBoxCashiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashBoxCashiers_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashBoxCashiers_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_UserCashBoxScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_UserCashBoxScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_UserCashBoxScopes_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_UserCashBoxScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_Vaults",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    MainCashBoxId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_Vaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_Vaults_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Vaults_RCD_CashBoxes_MainCashBoxId",
                        column: x => x.MainCashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    CashierUserId = table.Column<int>(type: "int", nullable: false),
                    CollectionPointDayId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpeningBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SealedSettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedByMeansJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsForgotten = table.Column<bool>(type: "bit", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    WitnessUserId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessions_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessions_RCD_CollectionPointDays_CollectionPointDayId",
                        column: x => x.CollectionPointDayId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPointDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessions_SEC_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessions_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessions_SEC_Users_WitnessUserId",
                        column: x => x.WitnessUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_DayCloses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionPointDayId = table.Column<int>(type: "int", nullable: false),
                    SessionCount = table.Column<int>(type: "int", nullable: false),
                    ReceiptCount = table.Column<int>(type: "int", nullable: false),
                    VoidingCount = table.Column<int>(type: "int", nullable: false),
                    Expected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Counted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: false),
                    SupervisorDelegationId = table.Column<int>(type: "int", nullable: true),
                    VerificationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_RCD_DayCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloses_RCD_CollectionPointDays_CollectionPointDayId",
                        column: x => x.CollectionPointDayId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPointDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloses_RCD_SupervisorDelegations_SupervisorDelegationId",
                        column: x => x.SupervisorDelegationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_SupervisorDelegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloses_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_VaultCustodians",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaultId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_RCD_VaultCustodians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_VaultCustodians_RCD_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_VaultCustodians_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_VaultDenominationBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaultId = table.Column<int>(type: "int", nullable: false),
                    CashDenominationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RCD_VaultDenominationBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_VaultDenominationBalances_COR_CashDenominations_CashDenominationId",
                        column: x => x.CashDenominationId,
                        principalSchema: "dbo",
                        principalTable: "COR_CashDenominations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_VaultDenominationBalances_RCD_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashCounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashSessionId = table.Column<int>(type: "int", nullable: true),
                    VaultId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CountedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CountedByUserId = table.Column<int>(type: "int", nullable: false),
                    WitnessUserId = table.Column<int>(type: "int", nullable: true),
                    IsBlind = table.Column<bool>(type: "bit", nullable: false),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerificationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashCounts_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCounts_RCD_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCounts_SEC_Users_CountedByUserId",
                        column: x => x.CountedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCounts_SEC_Users_WitnessUserId",
                        column: x => x.WitnessUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashSessionAdjustments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdjustedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashSessionAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessionAdjustments_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessionAdjustments_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessionAdjustments_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashSessionAdjustments_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_Receipts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CollectionPointId = table.Column<int>(type: "int", nullable: true),
                    CashBoxId = table.Column<int>(type: "int", nullable: true),
                    CashSessionId = table.Column<int>(type: "int", nullable: true),
                    CashierUserId = table.Column<int>(type: "int", nullable: true),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayerPersonId = table.Column<int>(type: "int", nullable: false),
                    DeliveredByPersonId = table.Column<int>(type: "int", nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProposedAccountingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalMeans = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeGiven = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceOfFundsDeclarationId = table.Column<int>(type: "int", nullable: true),
                    IsContingency = table.Column<bool>(type: "bit", nullable: false),
                    ContingencyBookId = table.Column<int>(type: "int", nullable: true),
                    ContingencyNumber = table.Column<int>(type: "int", nullable: true),
                    ContingencyImageAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BackdatedApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BackdatedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayerIsThirdParty = table.Column<bool>(type: "bit", nullable: false),
                    InputsHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankStatementLinePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymasterRemittanceId = table.Column<int>(type: "int", nullable: true),
                    VerificationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_RCD_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_COR_People_DeliveredByPersonId",
                        column: x => x.DeliveredByPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_COR_People_PayerPersonId",
                        column: x => x.PayerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_RCD_ContingencyBooks_ContingencyBookId",
                        column: x => x.ContingencyBookId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ContingencyBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_RCD_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "RCD_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_RCD_SourceOfFundsDeclarations_SourceOfFundsDeclarationId",
                        column: x => x.SourceOfFundsDeclarationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_SourceOfFundsDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_Receipts_SEC_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_DayCloseLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayCloseId = table.Column<int>(type: "int", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    Expected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Counted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RCD_DayCloseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloseLines_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloseLines_RCD_DayCloses_DayCloseId",
                        column: x => x.DayCloseId,
                        principalSchema: "dbo",
                        principalTable: "RCD_DayCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashCountLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashCountId = table.Column<int>(type: "int", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Expected = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Counted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tolerance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithinTolerance = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Treatment = table.Column<int>(type: "int", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountLines_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountLines_RCD_CashCounts_CashCountId",
                        column: x => x.CashCountId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountLines_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashMovements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: false),
                    CounterpartSessionId = table.Column<int>(type: "int", nullable: true),
                    VaultId = table.Column<int>(type: "int", nullable: true),
                    FromMeansId = table.Column<int>(type: "int", nullable: true),
                    ToMeansId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SecondCustodianUserId = table.Column<int>(type: "int", nullable: true),
                    MovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_COR_PaymentMeans_FromMeansId",
                        column: x => x.FromMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_COR_PaymentMeans_ToMeansId",
                        column: x => x.ToMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_RCD_CashSessions_CounterpartSessionId",
                        column: x => x.CounterpartSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_RCD_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "RCD_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_RCD_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovements_SEC_Users_SecondCustodianUserId",
                        column: x => x.SecondCustodianUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_ReceiptApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    BeneficiaryPersonId = table.Column<int>(type: "int", nullable: false),
                    LoanModality = table.Column<int>(type: "int", nullable: true),
                    AccountModality = table.Column<int>(type: "int", nullable: true),
                    PrepaymentChoice = table.Column<int>(type: "int", nullable: true),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: true),
                    InstallmentPublicIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdvanceNoticeAccepted = table.Column<bool>(type: "bit", nullable: false),
                    MiscConceptId = table.Column<int>(type: "int", nullable: true),
                    CrossDocumentReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    InvoicePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ModuleVoucherPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleVoucherNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FinalityUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_ReceiptApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptApplications_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptApplications_RCD_MiscConcepts_MiscConceptId",
                        column: x => x.MiscConceptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_MiscConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptApplications_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_ReceiptMeans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "int", nullable: false),
                    MeansClass = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CardTerminalId = table.Column<int>(type: "int", nullable: true),
                    TerminalBatch = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ApprovalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceivedCheckId = table.Column<int>(type: "int", nullable: true),
                    VoucherNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_ReceiptMeans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptMeans_COR_CardTerminals_CardTerminalId",
                        column: x => x.CardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptMeans_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptMeans_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashCountDenominations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashCountLineId = table.Column<int>(type: "int", nullable: false),
                    CashDenominationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashCountDenominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountDenominations_COR_CashDenominations_CashDenominationId",
                        column: x => x.CashDenominationId,
                        principalSchema: "dbo",
                        principalTable: "COR_CashDenominations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountDenominations_RCD_CashCountLines_CashCountLineId",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashCountTerminalBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashCountLineId = table.Column<int>(type: "int", nullable: false),
                    CardTerminalId = table.Column<int>(type: "int", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashCountTerminalBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountTerminalBatches_COR_CardTerminals_CardTerminalId",
                        column: x => x.CardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountTerminalBatches_RCD_CashCountLines_CashCountLineId",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashierReceivables",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashierPersonId = table.Column<int>(type: "int", nullable: false),
                    CashCountLineId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollDeductionAuthorizationAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashierReceivables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashierReceivables_COR_People_CashierPersonId",
                        column: x => x.CashierPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashierReceivables_RCD_CashCountLines_CashCountLineId",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashSurpluses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashCountLineId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToIncomeFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MovedToIncomeAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashSurpluses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashSurpluses_RCD_CashCountLines_CashCountLineId",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashMovementDenominations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashMovementId = table.Column<int>(type: "int", nullable: false),
                    CashDenominationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashMovementDenominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovementDenominations_COR_CashDenominations_CashDenominationId",
                        column: x => x.CashDenominationId,
                        principalSchema: "dbo",
                        principalTable: "COR_CashDenominations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashMovementDenominations_RCD_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashOperations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashSessionId = table.Column<int>(type: "int", nullable: false),
                    PayerPersonId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "int", nullable: true),
                    DeliveredByPersonId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeansJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImputationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImputationFixed = table.Column<bool>(type: "bit", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BackdateReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BackdateSupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScreeningOutcome = table.Column<int>(type: "int", nullable: false),
                    CustodyMovementId = table.Column<int>(type: "int", nullable: true),
                    ReleasedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    SourceOfFundsDeclarationId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_COR_People_DeliveredByPersonId",
                        column: x => x.DeliveredByPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_COR_People_PayerPersonId",
                        column: x => x.PayerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_RCD_CashMovements_CustodyMovementId",
                        column: x => x.CustodyMovementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_RCD_SourceOfFundsDeclarations_SourceOfFundsDeclarationId",
                        column: x => x.SourceOfFundsDeclarationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_SourceOfFundsDeclarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashOperations_SEC_Users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_VaultMovements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VaultId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CashMovementId = table.Column<int>(type: "int", nullable: true),
                    RemittancePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustodianUserId = table.Column<int>(type: "int", nullable: false),
                    SecondCustodianUserId = table.Column<int>(type: "int", nullable: true),
                    BagSeal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RCD_VaultMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_VaultMovements_RCD_CashMovements_CashMovementId",
                        column: x => x.CashMovementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_VaultMovements_RCD_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_VaultMovements_SEC_Users_CustodianUserId",
                        column: x => x.CustodianUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_VaultMovements_SEC_Users_SecondCustodianUserId",
                        column: x => x.SecondCustodianUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashCountReferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashCountLineId = table.Column<int>(type: "int", nullable: false),
                    ReceiptMeansId = table.Column<long>(type: "bigint", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashCountReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountReferences_RCD_CashCountLines_CashCountLineId",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashCountReferences_RCD_ReceiptMeans_ReceiptMeansId",
                        column: x => x.ReceiptMeansId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ReceiptMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_ReceiptApplicationPortions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptMeansId = table.Column<long>(type: "bigint", nullable: false),
                    MeansClass = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ModuleOperationPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleOperationNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReversalKey = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
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
                    table.PrimaryKey("PK_RCD_ReceiptApplicationPortions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptApplicationPortions_RCD_ReceiptApplications_ReceiptApplicationId",
                        column: x => x.ReceiptApplicationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ReceiptApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReceiptApplicationPortions_RCD_ReceiptMeans_ReceiptMeansId",
                        column: x => x.ReceiptMeansId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ReceiptMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_VaultMovementDenominations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaultMovementId = table.Column<int>(type: "int", nullable: false),
                    CashDenominationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_RCD_VaultMovementDenominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_VaultMovementDenominations_COR_CashDenominations_CashDenominationId",
                        column: x => x.CashDenominationId,
                        principalSchema: "dbo",
                        principalTable: "COR_CashDenominations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_VaultMovementDenominations_RCD_VaultMovements_VaultMovementId",
                        column: x => x.VaultMovementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_VaultMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashBoxCashiers_UserId",
                schema: "dbo",
                table: "RCD_CashBoxCashiers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashBoxCashiers_CashBoxId_UserId_ValidFrom",
                schema: "dbo",
                table: "RCD_CashBoxCashiers",
                columns: new[] { "CashBoxId", "UserId", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashBoxCashiers_PublicId",
                schema: "dbo",
                table: "RCD_CashBoxCashiers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashBoxes_CollectionPointId_Code",
                schema: "dbo",
                table: "RCD_CashBoxes",
                columns: new[] { "CollectionPointId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashBoxes_PublicId",
                schema: "dbo",
                table: "RCD_CashBoxes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountDenominations_CashCountLineId",
                schema: "dbo",
                table: "RCD_CashCountDenominations",
                column: "CashCountLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountDenominations_CashDenominationId",
                schema: "dbo",
                table: "RCD_CashCountDenominations",
                column: "CashDenominationId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashCountDenominations_PublicId",
                schema: "dbo",
                table: "RCD_CashCountDenominations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountLines_ApprovedByUserId",
                schema: "dbo",
                table: "RCD_CashCountLines",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountLines_PaymentMeansId",
                schema: "dbo",
                table: "RCD_CashCountLines",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashCountLines_CashCountId_PaymentMeansId",
                schema: "dbo",
                table: "RCD_CashCountLines",
                columns: new[] { "CashCountId", "PaymentMeansId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashCountLines_PublicId",
                schema: "dbo",
                table: "RCD_CashCountLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountReferences_CashCountLineId",
                schema: "dbo",
                table: "RCD_CashCountReferences",
                column: "CashCountLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountReferences_ReceiptMeansId",
                schema: "dbo",
                table: "RCD_CashCountReferences",
                column: "ReceiptMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashCountReferences_PublicId",
                schema: "dbo",
                table: "RCD_CashCountReferences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCounts_CashSessionId",
                schema: "dbo",
                table: "RCD_CashCounts",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCounts_CountedByUserId",
                schema: "dbo",
                table: "RCD_CashCounts",
                column: "CountedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCounts_VaultId",
                schema: "dbo",
                table: "RCD_CashCounts",
                column: "VaultId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCounts_WitnessUserId",
                schema: "dbo",
                table: "RCD_CashCounts",
                column: "WitnessUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashCounts_PublicId",
                schema: "dbo",
                table: "RCD_CashCounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountTerminalBatches_CardTerminalId",
                schema: "dbo",
                table: "RCD_CashCountTerminalBatches",
                column: "CardTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashCountTerminalBatches_CashCountLineId",
                schema: "dbo",
                table: "RCD_CashCountTerminalBatches",
                column: "CashCountLineId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashCountTerminalBatches_PublicId",
                schema: "dbo",
                table: "RCD_CashCountTerminalBatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashierReceivables_CashierPersonId",
                schema: "dbo",
                table: "RCD_CashierReceivables",
                column: "CashierPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashierReceivables_CashCountLineId",
                schema: "dbo",
                table: "RCD_CashierReceivables",
                column: "CashCountLineId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashierReceivables_PublicId",
                schema: "dbo",
                table: "RCD_CashierReceivables",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovementDenominations_CashDenominationId",
                schema: "dbo",
                table: "RCD_CashMovementDenominations",
                column: "CashDenominationId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovementDenominations_CashMovementId",
                schema: "dbo",
                table: "RCD_CashMovementDenominations",
                column: "CashMovementId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashMovementDenominations_PublicId",
                schema: "dbo",
                table: "RCD_CashMovementDenominations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_CashSessionId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_CounterpartSessionId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "CounterpartSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_FromMeansId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "FromMeansId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_ReceiptId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_SecondCustodianUserId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "SecondCustodianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_ToMeansId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "ToMeansId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashMovements_VaultId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "VaultId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashMovements_DocumentTypeId_Number",
                schema: "dbo",
                table: "RCD_CashMovements",
                columns: new[] { "DocumentTypeId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashMovements_PublicId",
                schema: "dbo",
                table: "RCD_CashMovements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_BeneficiaryPersonId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_CashSessionId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_CustodyMovementId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "CustodyMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_DeliveredByPersonId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "DeliveredByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_PayerPersonId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "PayerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_ReceiptId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_ReleasedByUserId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashOperations_SourceOfFundsDeclarationId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "SourceOfFundsDeclarationId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashOperations_PublicId",
                schema: "dbo",
                table: "RCD_CashOperations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessionAdjustments_ApprovedByUserId",
                schema: "dbo",
                table: "RCD_CashSessionAdjustments",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessionAdjustments_CashSessionId",
                schema: "dbo",
                table: "RCD_CashSessionAdjustments",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessionAdjustments_PaymentMeansId",
                schema: "dbo",
                table: "RCD_CashSessionAdjustments",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessionAdjustments_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_CashSessionAdjustments",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashSessionAdjustments_Number",
                schema: "dbo",
                table: "RCD_CashSessionAdjustments",
                column: "Number",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashSessionAdjustments_PublicId",
                schema: "dbo",
                table: "RCD_CashSessionAdjustments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessions_CashierUserId_Live",
                schema: "dbo",
                table: "RCD_CashSessions",
                column: "CashierUserId",
                filter: "[Status] IN (1, 2, 5) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessions_ClosedByUserId",
                schema: "dbo",
                table: "RCD_CashSessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessions_CollectionPointDayId",
                schema: "dbo",
                table: "RCD_CashSessions",
                column: "CollectionPointDayId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashSessions_WitnessUserId",
                schema: "dbo",
                table: "RCD_CashSessions",
                column: "WitnessUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashSessions_CashBoxId_Live",
                schema: "dbo",
                table: "RCD_CashSessions",
                column: "CashBoxId",
                unique: true,
                filter: "[Status] IN (1, 2, 5) AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashSessions_PublicId",
                schema: "dbo",
                table: "RCD_CashSessions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashSurpluses_CashCountLineId",
                schema: "dbo",
                table: "RCD_CashSurpluses",
                column: "CashCountLineId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashSurpluses_PublicId",
                schema: "dbo",
                table: "RCD_CashSurpluses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ChannelModalityRules_Channel_Target_Priority",
                schema: "dbo",
                table: "RCD_ChannelModalityRules",
                columns: new[] { "Channel", "Target", "Priority" });

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ChannelModalityRules_PublicId",
                schema: "dbo",
                table: "RCD_ChannelModalityRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CollectionPointDays_ClosedByUserId",
                schema: "dbo",
                table: "RCD_CollectionPointDays",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionPointDays_Point_Date",
                schema: "dbo",
                table: "RCD_CollectionPointDays",
                columns: new[] { "CollectionPointId", "BusinessDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionPointDays_PublicId",
                schema: "dbo",
                table: "RCD_CollectionPointDays",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CollectionPoints_BranchId",
                schema: "dbo",
                table: "RCD_CollectionPoints",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionPoints_Code",
                schema: "dbo",
                table: "RCD_CollectionPoints",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionPoints_PublicId",
                schema: "dbo",
                table: "RCD_CollectionPoints",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ContingencyBooks_CollectionPointId_FromNumber",
                schema: "dbo",
                table: "RCD_ContingencyBooks",
                columns: new[] { "CollectionPointId", "FromNumber" });

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ContingencyBooks_PublicId",
                schema: "dbo",
                table: "RCD_ContingencyBooks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_DailySquares_ExplainedByUserId",
                schema: "dbo",
                table: "RCD_DailySquares",
                column: "ExplainedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DailySquares_BranchId_BusinessDate",
                schema: "dbo",
                table: "RCD_DailySquares",
                columns: new[] { "BranchId", "BusinessDate" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DailySquares_PublicId",
                schema: "dbo",
                table: "RCD_DailySquares",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_DayCloseLines_DayCloseId",
                schema: "dbo",
                table: "RCD_DayCloseLines",
                column: "DayCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_DayCloseLines_PaymentMeansId",
                schema: "dbo",
                table: "RCD_DayCloseLines",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloseLines_PublicId",
                schema: "dbo",
                table: "RCD_DayCloseLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_DayCloses_ClosedByUserId",
                schema: "dbo",
                table: "RCD_DayCloses",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_DayCloses_SupervisorDelegationId",
                schema: "dbo",
                table: "RCD_DayCloses",
                column: "SupervisorDelegationId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloses_CollectionPointDayId",
                schema: "dbo",
                table: "RCD_DayCloses",
                column: "CollectionPointDayId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloses_PublicId",
                schema: "dbo",
                table: "RCD_DayCloses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DocumentTypes_Code",
                schema: "dbo",
                table: "RCD_DocumentTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DocumentTypes_PublicId",
                schema: "dbo",
                table: "RCD_DocumentTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_MiscConcepts_Code",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_MiscConcepts_PublicId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PayerControls_PersonId",
                schema: "dbo",
                table: "RCD_PayerControls",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PayerControls_PublicId",
                schema: "dbo",
                table: "RCD_PayerControls",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PaymentMeansCollectionPoints_PaymentMeansId",
                schema: "dbo",
                table: "RCD_PaymentMeansCollectionPoints",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymentMeansCollectionPoints_Point_Means_From",
                schema: "dbo",
                table: "RCD_PaymentMeansCollectionPoints",
                columns: new[] { "CollectionPointId", "PaymentMeansId", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymentMeansCollectionPoints_PublicId",
                schema: "dbo",
                table: "RCD_PaymentMeansCollectionPoints",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptApplicationPortions_ReceiptApplicationId",
                schema: "dbo",
                table: "RCD_ReceiptApplicationPortions",
                column: "ReceiptApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptApplicationPortions_ReceiptMeansId",
                schema: "dbo",
                table: "RCD_ReceiptApplicationPortions",
                column: "ReceiptMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReceiptApplicationPortions_IdempotencyKey",
                schema: "dbo",
                table: "RCD_ReceiptApplicationPortions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReceiptApplicationPortions_PublicId",
                schema: "dbo",
                table: "RCD_ReceiptApplicationPortions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptApplications_BeneficiaryPersonId",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptApplications_MiscConceptId",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                column: "MiscConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptApplications_ProductPublicId",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                column: "ProductPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptApplications_ReceiptId",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReceiptApplications_PublicId",
                schema: "dbo",
                table: "RCD_ReceiptApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptMeans_CardTerminalId",
                schema: "dbo",
                table: "RCD_ReceiptMeans",
                column: "CardTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceiptMeans_ReceiptId",
                schema: "dbo",
                table: "RCD_ReceiptMeans",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReceiptMeans_PaymentMeansId_VoucherNumber",
                schema: "dbo",
                table: "RCD_ReceiptMeans",
                columns: new[] { "PaymentMeansId", "VoucherNumber" },
                unique: true,
                filter: "[VoucherNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReceiptMeans_PublicId",
                schema: "dbo",
                table: "RCD_ReceiptMeans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_BranchId_ValueDate",
                schema: "dbo",
                table: "RCD_Receipts",
                columns: new[] { "BranchId", "ValueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_CashBoxId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_CashierUserId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_CashSessionId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_CollectionPointId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "CollectionPointId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_ContingencyBookId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "ContingencyBookId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_DeliveredByPersonId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "DeliveredByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_PayerPersonId_RegisteredAt",
                schema: "dbo",
                table: "RCD_Receipts",
                columns: new[] { "PayerPersonId", "RegisteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Receipts_SourceOfFundsDeclarationId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "SourceOfFundsDeclarationId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Receipts_DocumentTypeId_Number",
                schema: "dbo",
                table: "RCD_Receipts",
                columns: new[] { "DocumentTypeId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Receipts_OperationKey",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "OperationKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Receipts_PublicId",
                schema: "dbo",
                table: "RCD_Receipts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Setup_PublicId",
                schema: "dbo",
                table: "RCD_Setup",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_SourceOfFundsDeclarations_PersonId",
                schema: "dbo",
                table: "RCD_SourceOfFundsDeclarations",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_SourceOfFundsDeclarations_ReceiptOperationPublicId",
                schema: "dbo",
                table: "RCD_SourceOfFundsDeclarations",
                column: "ReceiptOperationPublicId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_SourceOfFundsDeclarations_PublicId",
                schema: "dbo",
                table: "RCD_SourceOfFundsDeclarations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_SupervisorDelegations_CollectionPointId_DelegateUserId",
                schema: "dbo",
                table: "RCD_SupervisorDelegations",
                columns: new[] { "CollectionPointId", "DelegateUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_SupervisorDelegations_DelegateUserId",
                schema: "dbo",
                table: "RCD_SupervisorDelegations",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_SupervisorDelegations_SupervisorUserId",
                schema: "dbo",
                table: "RCD_SupervisorDelegations",
                column: "SupervisorUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_SupervisorDelegations_PublicId",
                schema: "dbo",
                table: "RCD_SupervisorDelegations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_UserCashBoxScopes_CashBoxId",
                schema: "dbo",
                table: "RCD_UserCashBoxScopes",
                column: "CashBoxId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_UserCashBoxScopes_PublicId",
                schema: "dbo",
                table: "RCD_UserCashBoxScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_UserCashBoxScopes_UserId_CashBoxId",
                schema: "dbo",
                table: "RCD_UserCashBoxScopes",
                columns: new[] { "UserId", "CashBoxId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_UserCollectionPointScopes_CollectionPointId",
                schema: "dbo",
                table: "RCD_UserCollectionPointScopes",
                column: "CollectionPointId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_UserCollectionPointScopes_PublicId",
                schema: "dbo",
                table: "RCD_UserCollectionPointScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_UserCollectionPointScopes_UserId_CollectionPointId",
                schema: "dbo",
                table: "RCD_UserCollectionPointScopes",
                columns: new[] { "UserId", "CollectionPointId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_UserPaymasterScopes_PaymasterPersonId",
                schema: "dbo",
                table: "RCD_UserPaymasterScopes",
                column: "PaymasterPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_UserPaymasterScopes_PublicId",
                schema: "dbo",
                table: "RCD_UserPaymasterScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_UserPaymasterScopes_UserId_PaymasterPersonId",
                schema: "dbo",
                table: "RCD_UserPaymasterScopes",
                columns: new[] { "UserId", "PaymasterPersonId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultCustodians_UserId",
                schema: "dbo",
                table: "RCD_VaultCustodians",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultCustodians_PublicId",
                schema: "dbo",
                table: "RCD_VaultCustodians",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultCustodians_VaultId_UserId_ValidFrom",
                schema: "dbo",
                table: "RCD_VaultCustodians",
                columns: new[] { "VaultId", "UserId", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultDenominationBalances_CashDenominationId",
                schema: "dbo",
                table: "RCD_VaultDenominationBalances",
                column: "CashDenominationId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultDenominationBalances_PublicId",
                schema: "dbo",
                table: "RCD_VaultDenominationBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultDenominationBalances_VaultId_DenominationId",
                schema: "dbo",
                table: "RCD_VaultDenominationBalances",
                columns: new[] { "VaultId", "CashDenominationId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultMovementDenominations_CashDenominationId",
                schema: "dbo",
                table: "RCD_VaultMovementDenominations",
                column: "CashDenominationId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultMovementDenominations_VaultMovementId",
                schema: "dbo",
                table: "RCD_VaultMovementDenominations",
                column: "VaultMovementId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultMovementDenominations_PublicId",
                schema: "dbo",
                table: "RCD_VaultMovementDenominations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultMovements_CashMovementId",
                schema: "dbo",
                table: "RCD_VaultMovements",
                column: "CashMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultMovements_CustodianUserId",
                schema: "dbo",
                table: "RCD_VaultMovements",
                column: "CustodianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultMovements_SecondCustodianUserId",
                schema: "dbo",
                table: "RCD_VaultMovements",
                column: "SecondCustodianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_VaultMovements_VaultId",
                schema: "dbo",
                table: "RCD_VaultMovements",
                column: "VaultId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultMovements_Number",
                schema: "dbo",
                table: "RCD_VaultMovements",
                column: "Number",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_VaultMovements_PublicId",
                schema: "dbo",
                table: "RCD_VaultMovements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Vaults_MainCashBoxId",
                schema: "dbo",
                table: "RCD_Vaults",
                column: "MainCashBoxId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Vaults_BranchId",
                schema: "dbo",
                table: "RCD_Vaults",
                column: "BranchId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Vaults_PublicId",
                schema: "dbo",
                table: "RCD_Vaults",
                column: "PublicId",
                unique: true);

            // Feature 015 (T0050; data-model §1.1): la fila única de RCD_Setup, con Id = 1, sin activar, la hora de corte por defecto (18:00,
            // Recaudo.HoraDeCorte) y las versiones 1 de los contratos. Idempotente: no hace nada si la fila ya existe. Es el nivel 1 del cerrojo.
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [dbo].[RCD_Setup] ON;
INSERT INTO [dbo].[RCD_Setup] ([Id], [PublicId], [Status], [CutoffTime], [ContractVersionObligations], [ContractVersionAccounts], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT 1, NEWID(), 1, CAST('18:00' AS time), 1, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_Setup]);
SET IDENTITY_INSERT [dbo].[RCD_Setup] OFF;
");

            // Feature 015, US1 (T0075; FR-026, FR-027, FR-198; D38): un tipo de documento por clase de R0, los conceptos del sistema (no se
            // inactivan) y las reglas de modalidad de los canales no interactivos (créditos CycleInstallment; cuentas Installments si hay
            // exigibles, si no Free). Idempotente: no repite lo que ya existe ni ocupa una clase ya tomada.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[RCD_DocumentTypes] ([PublicId], [Code], [Name], [Kind], [UsesPointPrefix], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT NEWID(), v.code, v.name, v.kind, 0, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES (N'RC', N'Recibo de caja', 1), (N'RB', N'Recibo de banco', 2), (N'RP', N'Recibo de pagaduría', 4), (N'RT', N'Recibo de contingencia', 6), (N'AN', N'Anulación de recibo', 8), (N'MC', N'Movimiento de caja', 9), (N'AS', N'Ajuste de sesión de caja', 10)) AS v(code, name, kind)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Code] = v.code AND t.[IsDeleted] = 0)
  AND NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Kind] = v.kind AND t.[Status] = 1 AND t.[IsDeleted] = 0);
INSERT INTO [dbo].[RCD_MiscConcepts] ([PublicId], [Code], [Name], [Direction], [RequiresThirdParty], [RequiresCrossDocument], [RequiresInvoice], [RequiresApproval], [IsSystem], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT NEWID(), v.code, v.name, 1, 0, 0, 0, 0, 1, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES (N'GIRODEV', N'Giro devuelto'), (N'SOBRANTE', N'Sobrantes de caja'), (N'REINTEGRO', N'Reintegros'), (N'RENDIM', N'Rendimientos')) AS v(code, name)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_MiscConcepts] c WHERE c.[Code] = v.code AND c.[IsDeleted] = 0);
INSERT INTO [dbo].[RCD_ChannelModalityRules] ([PublicId], [Channel], [Target], [LoanModality], [AccountModality], [OnlyWhenInstallmentsDue], [Priority], [ValidFrom], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT NEWID(), v.channel, v.target, v.loan, v.account, v.due, v.priority, CAST('2026-01-01' AS date), 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES (2, 1, 3, NULL, 0, 1), (2, 2, NULL, 2, 1, 1), (2, 2, NULL, 1, 0, 2), (3, 1, 3, NULL, 0, 1), (3, 2, NULL, 2, 1, 1), (3, 2, NULL, 1, 0, 2), (6, 1, 3, NULL, 0, 1), (6, 2, NULL, 2, 1, 1), (6, 2, NULL, 1, 0, 2)) AS v(channel, target, loan, account, due, priority)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_ChannelModalityRules] r WHERE r.[Channel] = v.channel AND r.[Target] = v.target AND r.[IsDeleted] = 0);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_CashBoxCashiers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashCountDenominations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashCountReferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashCountTerminalBatches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashierReceivables",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashMovementDenominations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashOperations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashSessionAdjustments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashSurpluses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ChannelModalityRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_DailySquares",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_DayCloseLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PayerControls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PaymentMeansCollectionPoints",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ReceiptApplicationPortions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_Setup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_UserCashBoxScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_UserCollectionPointScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_UserPaymasterScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_VaultCustodians",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_VaultDenominationBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_VaultMovementDenominations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashCountLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_DayCloses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ReceiptApplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ReceiptMeans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_VaultMovements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashCounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_SupervisorDelegations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_MiscConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashMovements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_Receipts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_Vaults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashSessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ContingencyBooks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_DocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_SourceOfFundsDeclarations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashBoxes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CollectionPointDays",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CollectionPoints",
                schema: "dbo");
        }
    }
}
