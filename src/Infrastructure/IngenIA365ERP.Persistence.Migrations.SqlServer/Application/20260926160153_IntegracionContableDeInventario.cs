using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class IntegracionContableDeInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                schema: "dbo",
                table: "ACC_AccountTaxRates",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,4)",
                oldPrecision: 9,
                oldScale: 4);

            migrationBuilder.CreateTable(
                name: "ACC_InventoryPostingRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AccountingGroupCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    WarehouseCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PointOfSaleCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PaymentMeansCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TaxRateCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    CostCenterId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    DimensionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SpecificityWeight = table.Column<short>(type: "smallint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_InventoryPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryPostingRules_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryPostingRules_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryPostingRules_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_InventoryPostings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    MessageKind = table.Column<int>(type: "int", nullable: false),
                    SourceModule = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SourcePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentClass = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SourceDocumentTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SourceDocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RelatedDocumentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    NoVoucherReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BatchPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginUserCentralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ActorKind = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_InventoryPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryPostings_ACC_Documents_AccountingDocumentId",
                        column: x => x.AccountingDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryPostings_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_InventoryVoucherMappings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Operation = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InventoryDocumentTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    VoucherTypeId = table.Column<int>(type: "int", nullable: false),
                    CrossDocumentTypeId = table.Column<int>(type: "int", nullable: true),
                    MappingKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_InventoryVoucherMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryVoucherMappings_ACC_CrossDocumentTypes_CrossDocumentTypeId",
                        column: x => x.CrossDocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_CrossDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_InventoryVoucherMappings_ACC_VoucherTypes_VoucherTypeId",
                        column: x => x.VoucherTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_VoucherTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationBatchCounters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_COR_IntegrationBatchCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Trigger = table.Column<int>(type: "int", nullable: false),
                    ScheduleKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CashSessionPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: true),
                    PeriodMonth = table.Column<byte>(type: "tinyint", nullable: true),
                    CutoffMessageId = table.Column<long>(type: "bigint", nullable: true),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Granularity = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByKind = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedByCentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RequestedByEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    RequestedByIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    DocumentCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    VoucherCount = table.Column<int>(type: "int", nullable: false),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResultSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_COR_IntegrationBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationBatches_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationDeliveryAttempts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryId = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActorKind = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BatchId = table.Column<int>(type: "int", nullable: true),
                    Instance = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_COR_IntegrationDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationDeliveryAttempts_COR_IntegrationBatches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "dbo",
                        principalTable: "COR_IntegrationBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationDeliveryAttempts_COR_IntegrationMessageDeliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalSchema: "dbo",
                        principalTable: "COR_IntegrationMessageDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationDeliveryAttempts_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostingRules_Account",
                schema: "dbo",
                table: "ACC_InventoryPostingRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostingRules_Branch",
                schema: "dbo",
                table: "ACC_InventoryPostingRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostingRules_CostCenter",
                schema: "dbo",
                table: "ACC_InventoryPostingRules",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostingRules_Operation_Role_ValidFrom",
                schema: "dbo",
                table: "ACC_InventoryPostingRules",
                columns: new[] { "Operation", "Role", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_InventoryPostingRules_DimensionKey_ValidFrom",
                schema: "dbo",
                table: "ACC_InventoryPostingRules",
                columns: new[] { "DimensionKey", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_InventoryPostingRules_PublicId",
                schema: "dbo",
                table: "ACC_InventoryPostingRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostings_AccountingDocument",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostings_ActorUser",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostings_Batch",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "BatchPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostings_OperationDate",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "OperationDate");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostings_Related",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "RelatedDocumentPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryPostings_Source",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "SourcePublicId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_InventoryPostings_MessagePublicId",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_InventoryPostings_PublicId",
                schema: "dbo",
                table: "ACC_InventoryPostings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryVoucherMappings_CrossDocumentType",
                schema: "dbo",
                table: "ACC_InventoryVoucherMappings",
                column: "CrossDocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_InventoryVoucherMappings_VoucherType",
                schema: "dbo",
                table: "ACC_InventoryVoucherMappings",
                column: "VoucherTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_InventoryVoucherMappings_MappingKey",
                schema: "dbo",
                table: "ACC_InventoryVoucherMappings",
                column: "MappingKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_InventoryVoucherMappings_PublicId",
                schema: "dbo",
                table: "ACC_InventoryVoucherMappings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationBatchCounters_PublicId",
                schema: "dbo",
                table: "COR_IntegrationBatchCounters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationBatches_Destination_Status",
                schema: "dbo",
                table: "COR_IntegrationBatches",
                columns: new[] { "Destination", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationBatches_RequestedByUser",
                schema: "dbo",
                table: "COR_IntegrationBatches",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationBatches_CashSession",
                schema: "dbo",
                table: "COR_IntegrationBatches",
                column: "CashSessionPublicId",
                unique: true,
                filter: "[Trigger] = 2 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationBatches_Number",
                schema: "dbo",
                table: "COR_IntegrationBatches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationBatches_PublicId",
                schema: "dbo",
                table: "COR_IntegrationBatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationBatches_Schedule",
                schema: "dbo",
                table: "COR_IntegrationBatches",
                columns: new[] { "ScheduleKey", "ScheduledFor" },
                unique: true,
                filter: "[Trigger] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationDeliveryAttempts_ActorUser",
                schema: "dbo",
                table: "COR_IntegrationDeliveryAttempts",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationDeliveryAttempts_Batch",
                schema: "dbo",
                table: "COR_IntegrationDeliveryAttempts",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationDeliveryAttempts_Message",
                schema: "dbo",
                table: "COR_IntegrationDeliveryAttempts",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationDeliveryAttempts_Delivery_Attempt",
                schema: "dbo",
                table: "COR_IntegrationDeliveryAttempts",
                columns: new[] { "DeliveryId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationDeliveryAttempts_PublicId",
                schema: "dbo",
                table: "COR_IntegrationDeliveryAttempts",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_COR_IntegrationMessageDeliveries_COR_IntegrationBatches_BatchId",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                column: "BatchId",
                principalSchema: "dbo",
                principalTable: "COR_IntegrationBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COR_IntegrationMessageDeliveries_COR_IntegrationBatches_BatchId",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries");

            migrationBuilder.DropTable(
                name: "ACC_InventoryPostingRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_InventoryPostings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_InventoryVoucherMappings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationBatchCounters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationDeliveryAttempts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationBatches",
                schema: "dbo");

            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                schema: "dbo",
                table: "ACC_AccountTaxRates",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6);
        }
    }
}
