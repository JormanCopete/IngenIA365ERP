using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class TesoreriaCompleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstApprovedAt",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManualApprovalState",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManualOrderConceptId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualRejectionReason",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SecondApprovedAt",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SecondApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupportAttachmentPublicId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WithholdingAmount",
                schema: "dbo",
                table: "TES_PaymentOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MovedToPayableOn",
                schema: "dbo",
                table: "TES_IssuedChecks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemittancePublicId",
                schema: "dbo",
                table: "INV_DayCloses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TES_BankAccountSigners",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SignatureClass = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaxAmountAlone = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CanSignJointly = table.Column<bool>(type: "bit", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetirementMinutesAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TES_BankAccountSigners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountSigners_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountSigners_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountSigners_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountSigners_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_BankAccountTokens",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Serial = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetirementMinutesAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetirementReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TES_BankAccountTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountTokens_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountTokens_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountTokens_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccountTokens_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_ManualOrderConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiresWithholding = table.Column<bool>(type: "bit", nullable: false),
                    WithholdingNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_TES_ManualOrderConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TES_MonthEndCheckRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DaysToPayable = table.Column<int>(type: "int", nullable: false),
                    ReclassifiedCount = table.Column<int>(type: "int", nullable: false),
                    ReclassifiedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovedToPayableCount = table.Column<int>(type: "int", nullable: false),
                    MovedToPayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReversalPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunByUserId = table.Column<int>(type: "int", nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_TES_MonthEndCheckRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_MonthEndCheckRuns_SEC_Users_RunByUserId",
                        column: x => x.RunByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_MonthEndCheckRuns_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentVouchers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PaymentOrderId = table.Column<int>(type: "int", nullable: false),
                    DisbursementResultId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    Form = table.Column<int>(type: "int", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TreasuryReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CheckNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Concept = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    VerificationCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SentByUserId = table.Column<int>(type: "int", nullable: true),
                    SendCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TES_PaymentVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentVouchers_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentVouchers_SEC_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentVouchers_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentVouchers_TES_DisbursementResults_DisbursementResultId",
                        column: x => x.DisbursementResultId,
                        principalSchema: "dbo",
                        principalTable: "TES_DisbursementResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentVouchers_TES_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_Transfers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromBankAccountId = table.Column<int>(type: "int", nullable: false),
                    ToBankAccountId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LiquidityFundUseId = table.Column<int>(type: "int", nullable: true),
                    SentByUserId = table.Column<int>(type: "int", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ConfirmedByStatementLinePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmationSupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_SEC_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_SEC_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_TES_BankAccounts_FromBankAccountId",
                        column: x => x.FromBankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_TES_BankAccounts_ToBankAccountId",
                        column: x => x.ToBankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_TES_LiquidityFundUses_LiquidityFundUseId",
                        column: x => x.LiquidityFundUseId,
                        principalSchema: "dbo",
                        principalTable: "TES_LiquidityFundUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_FirstApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "FirstApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_ManualOrderConceptId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ManualOrderConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_Origin_ManualApprovalState",
                schema: "dbo",
                table: "TES_PaymentOrders",
                columns: new[] { "Origin", "ManualApprovalState" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_SecondApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "SecondApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_IssuedChecks_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks",
                column: "MovedToPayableRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountSigners_Account_Status",
                schema: "dbo",
                table: "TES_BankAccountSigners",
                columns: new[] { "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountSigners_PersonId",
                schema: "dbo",
                table: "TES_BankAccountSigners",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountSigners_RequestedByUserId",
                schema: "dbo",
                table: "TES_BankAccountSigners",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountSigners_UserId",
                schema: "dbo",
                table: "TES_BankAccountSigners",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankAccountSigners_PublicId",
                schema: "dbo",
                table: "TES_BankAccountSigners",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountTokens_Account_Status",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                columns: new[] { "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountTokens_PersonId",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountTokens_RequestedByUserId",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountTokens_Serial",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                column: "Serial");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccountTokens_UserId",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankAccountTokens_PublicId",
                schema: "dbo",
                table: "TES_BankAccountTokens",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_ManualOrderConcepts_Code",
                schema: "dbo",
                table: "TES_ManualOrderConcepts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_ManualOrderConcepts_PublicId",
                schema: "dbo",
                table: "TES_ManualOrderConcepts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_MonthEndCheckRuns_RunByUserId",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "RunByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_Account_Period",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                columns: new[] { "BankAccountId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_Number",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_PublicId",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_ReversalPublicId",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "ReversalPublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentVouchers_BankAccountId",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentVouchers_BeneficiaryPersonId",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentVouchers_DisbursementResultId",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "DisbursementResultId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentVouchers_PaidOn",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "PaidOn");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentVouchers_SentByUserId",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentVouchers_Number",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentVouchers_PaymentOrderId",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "PaymentOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentVouchers_PublicId",
                schema: "dbo",
                table: "TES_PaymentVouchers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_ConfirmedByUserId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_From_Status",
                schema: "dbo",
                table: "TES_Transfers",
                columns: new[] { "FromBankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_LiquidityFundUseId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "LiquidityFundUseId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_RequestedByUserId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_SentByUserId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_To_Status",
                schema: "dbo",
                table: "TES_Transfers",
                columns: new[] { "ToBankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_TES_Transfers_Number",
                schema: "dbo",
                table: "TES_Transfers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_Transfers_PublicId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_IssuedChecks_TES_MonthEndCheckRuns_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks",
                column: "MovedToPayableRunId",
                principalSchema: "dbo",
                principalTable: "TES_MonthEndCheckRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_PaymentOrders_SEC_Users_FirstApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "FirstApprovedByUserId",
                principalSchema: "dbo",
                principalTable: "SEC_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_PaymentOrders_SEC_Users_PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "PreparedByUserId",
                principalSchema: "dbo",
                principalTable: "SEC_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_PaymentOrders_SEC_Users_SecondApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "SecondApprovedByUserId",
                principalSchema: "dbo",
                principalTable: "SEC_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_PaymentOrders_TES_ManualOrderConcepts_ManualOrderConceptId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ManualOrderConceptId",
                principalSchema: "dbo",
                principalTable: "TES_ManualOrderConcepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TES_IssuedChecks_TES_MonthEndCheckRuns_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_TES_PaymentOrders_SEC_Users_FirstApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TES_PaymentOrders_SEC_Users_PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TES_PaymentOrders_SEC_Users_SecondApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_TES_PaymentOrders_TES_ManualOrderConcepts_ManualOrderConceptId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropTable(
                name: "TES_BankAccountSigners",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_BankAccountTokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_ManualOrderConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_MonthEndCheckRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentVouchers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_Transfers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_FirstApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_ManualOrderConceptId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_Origin_ManualApprovalState",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_PaymentOrders_SecondApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropIndex(
                name: "IX_TES_IssuedChecks_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropColumn(
                name: "FirstApprovedAt",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "FirstApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ManualApprovalState",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ManualOrderConceptId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ManualRejectionReason",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "SecondApprovedAt",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "SecondApprovedByUserId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "SupportAttachmentPublicId",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "WithholdingAmount",
                schema: "dbo",
                table: "TES_PaymentOrders");

            migrationBuilder.DropColumn(
                name: "MovedToPayableOn",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropColumn(
                name: "MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropColumn(
                name: "RemittancePublicId",
                schema: "dbo",
                table: "INV_DayCloses");
        }
    }
}
