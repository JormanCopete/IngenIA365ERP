using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class RecaudoCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RCD_CashDisbursements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: false),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    CollectionPointId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CashierUserId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "int", nullable: false),
                    ReceivedByPersonId = table.Column<int>(type: "int", nullable: true),
                    ProductPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ProductNature = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PayerCreditBalanceId = table.Column<int>(type: "int", nullable: true),
                    MiscConceptId = table.Column<int>(type: "int", nullable: true),
                    ModuleAuthorizationPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleAuthorizationNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GmfAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    GmfByOrigin = table.Column<bool>(type: "bit", nullable: false),
                    SignersJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SignatureAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OperationKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VerificationCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashDisbursements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_COR_People_ReceivedByPersonId",
                        column: x => x.ReceivedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "RCD_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_MiscConcepts_MiscConceptId",
                        column: x => x.MiscConceptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_MiscConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_PayerCreditBalances_PayerCreditBalanceId",
                        column: x => x.PayerCreditBalanceId,
                        principalSchema: "dbo",
                        principalTable: "RCD_PayerCreditBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_SEC_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_IdentificationRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Criterion = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ApplyOnLoad = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_RCD_IdentificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PendingApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    TargetModule = table.Column<int>(type: "int", nullable: false),
                    LoadObligationPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PendingApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_RCD_ReceiptApplications_ReceiptApplicationId",
                        column: x => x.ReceiptApplicationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ReceiptApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_ReportedDeposits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepositDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedImmediately = table.Column<bool>(type: "bit", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    StatementLinePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_RCD_ReportedDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_SEC_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashDisbursementVoidings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashDisbursementId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CashSessionId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleReversalPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleReversalNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashDisbursementVoidings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_RCD_CashDisbursements_CashDisbursementId",
                        column: x => x.CashDisbursementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashDisbursements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PendingApplicationResolutions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PendingApplicationId = table.Column<int>(type: "int", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Modality = table.Column<int>(type: "int", nullable: false),
                    PrepaymentChoice = table.Column<int>(type: "int", nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ModuleOperationPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModuleOperationNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PendingApplicationResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplicationResolutions_RCD_PendingApplications_PendingApplicationId",
                        column: x => x.PendingApplicationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_PendingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplicationResolutions_SEC_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_BeneficiaryPersonId_OperationDate",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "BeneficiaryPersonId", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_BranchId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CashBoxId_OperationDate",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "CashBoxId", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CashierUserId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CashSessionId_Status",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "CashSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CollectionPointId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "CollectionPointId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_MiscConceptId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "MiscConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_PayerCreditBalanceId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "PayerCreditBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_ReceivedByPersonId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "ReceivedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursements_DocumentTypeId_Number",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "DocumentTypeId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursements_OperationKey",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "OperationKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursements_PublicId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursementVoidings_ApprovedByUserId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursementVoidings_CashSessionId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursementVoidings_RequestedByUserId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursementVoidings_CashDisbursementId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "CashDisbursementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursementVoidings_PublicId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_IdentificationRules_IsActive_Priority",
                schema: "dbo",
                table: "RCD_IdentificationRules",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "UK_RCD_IdentificationRules_Code",
                schema: "dbo",
                table: "RCD_IdentificationRules",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_IdentificationRules_PublicId",
                schema: "dbo",
                table: "RCD_IdentificationRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplicationResolutions_ResolvedByUserId",
                schema: "dbo",
                table: "RCD_PendingApplicationResolutions",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplicationResolutions_PendingApplicationId",
                schema: "dbo",
                table: "RCD_PendingApplicationResolutions",
                column: "PendingApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplicationResolutions_PublicId",
                schema: "dbo",
                table: "RCD_PendingApplicationResolutions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplications_BranchId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplications_PersonId_Status",
                schema: "dbo",
                table: "RCD_PendingApplications",
                columns: new[] { "PersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplications_ReceiptId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplications_PublicId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplications_ReceiptApplicationId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "ReceiptApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_Account_Status_Date",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                columns: new[] { "OwnBankAccountPublicId", "Status", "DepositDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_BranchId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_PersonId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_ReceiptId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_StatementLinePublicId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "StatementLinePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_VerifiedByUserId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReportedDeposits_Number",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "Number",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReportedDeposits_PublicId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "PublicId",
                unique: true);

            // Feature 015, R1 (fase 26, T0373): el tipo de documento del egreso de caja (clase 7). Idempotente: no repite el código ni ocupa una clase ya tomada.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[RCD_DocumentTypes] ([PublicId], [Code], [Name], [Kind], [UsesPointPrefix], [Status], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT NEWID(), v.code, v.name, v.kind, 0, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES (N'EG', N'Egreso de caja', 7)) AS v(code, name, kind)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Code] = v.code AND t.[IsDeleted] = 0)
  AND NOT EXISTS (SELECT 1 FROM [dbo].[RCD_DocumentTypes] t WHERE t.[Kind] = v.kind AND t.[Status] = 1 AND t.[IsDeleted] = 0);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_CashDisbursementVoidings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_IdentificationRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PendingApplicationResolutions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ReportedDeposits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashDisbursements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PendingApplications",
                schema: "dbo");
        }
    }
}
