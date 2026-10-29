using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class IntegracionContableDeRecaudoYTesoreria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACC_OwnBankAccountLinks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChartOfAccountId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ACC_OwnBankAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_OwnBankAccountLinks_ACC_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ReceiptsPostingRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CollectionPointCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CashBoxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MeansCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MeansClass = table.Column<int>(type: "integer", nullable: true),
                    MiscConceptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TargetModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TargetNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CollectorClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OwnBankAccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrderOrigin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OrderKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Cause = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DimensionKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SpecificityWeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_ReceiptsPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ReceiptsPostingRules_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ReceiptsPostingRules_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ReceiptsPostings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    MessageKind = table.Column<int>(type: "integer", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourcePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentClass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceDocumentTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SourceDocumentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RelatedMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    NoVoucherReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BatchPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginActorKind = table.Column<int>(type: "integer", nullable: false),
                    OriginUserCentralId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_ReceiptsPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ReceiptsPostings_ACC_Documents_AccountingDocumentId",
                        column: x => x.AccountingDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ReceiptsPostings_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ReceiptsVoucherMappings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    VoucherTypeId = table.Column<int>(type: "integer", nullable: false),
                    CrossDocumentTypeId = table.Column<int>(type: "integer", nullable: true),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_ReceiptsVoucherMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ReceiptsVoucherMappings_ACC_CrossDocumentTypes_CrossDoc~",
                        column: x => x.CrossDocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_CrossDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ReceiptsVoucherMappings_ACC_VoucherTypes_VoucherTypeId",
                        column: x => x.VoucherTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_VoucherTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_TreasuryPostingRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CollectionPointCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CashBoxCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MeansCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MeansClass = table.Column<int>(type: "integer", nullable: true),
                    MiscConceptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TargetModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    TargetNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CollectorClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OwnBankAccountCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrderOrigin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OrderKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Cause = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DimensionKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SpecificityWeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_TreasuryPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_TreasuryPostingRules_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_TreasuryPostingRules_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_TreasuryPostings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    MessageKind = table.Column<int>(type: "integer", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourcePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentClass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceDocumentTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SourceDocumentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RelatedMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    NoVoucherReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BatchPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginActorKind = table.Column<int>(type: "integer", nullable: false),
                    OriginUserCentralId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_TreasuryPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_TreasuryPostings_ACC_Documents_AccountingDocumentId",
                        column: x => x.AccountingDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_TreasuryPostings_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_TreasuryVoucherMappings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    VoucherTypeId = table.Column<int>(type: "integer", nullable: false),
                    CrossDocumentTypeId = table.Column<int>(type: "integer", nullable: true),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_TreasuryVoucherMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_TreasuryVoucherMappings_ACC_CrossDocumentTypes_CrossDoc~",
                        column: x => x.CrossDocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_CrossDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_TreasuryVoucherMappings_ACC_VoucherTypes_VoucherTypeId",
                        column: x => x.VoucherTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_VoucherTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_OwnBankAccountLinks_ChartOfAccountId_Vigente",
                schema: "dbo",
                table: "ACC_OwnBankAccountLinks",
                column: "ChartOfAccountId",
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_OwnBankAccountLinks_OwnBankAccountPublicId_Vigente",
                schema: "dbo",
                table: "ACC_OwnBankAccountLinks",
                column: "OwnBankAccountPublicId",
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_OwnBankAccountLinks_PublicId",
                schema: "dbo",
                table: "ACC_OwnBankAccountLinks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostingRules_Account",
                schema: "dbo",
                table: "ACC_ReceiptsPostingRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostingRules_CostCenter",
                schema: "dbo",
                table: "ACC_ReceiptsPostingRules",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostingRules_FactType_Role",
                schema: "dbo",
                table: "ACC_ReceiptsPostingRules",
                columns: new[] { "FactType", "Role", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ReceiptsPostingRules_DimensionKey_ValidFrom",
                schema: "dbo",
                table: "ACC_ReceiptsPostingRules",
                columns: new[] { "DimensionKey", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ReceiptsPostingRules_PublicId",
                schema: "dbo",
                table: "ACC_ReceiptsPostingRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostings_AccountingDocument",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostings_ActorUser",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostings_Batch",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "BatchPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostings_Related",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "RelatedMessagePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsPostings_Source",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "SourcePublicId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ReceiptsPostings_MessagePublicId",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ReceiptsPostings_PublicId",
                schema: "dbo",
                table: "ACC_ReceiptsPostings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsVoucherMappings_CrossDocumentType",
                schema: "dbo",
                table: "ACC_ReceiptsVoucherMappings",
                column: "CrossDocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ReceiptsVoucherMappings_VoucherType",
                schema: "dbo",
                table: "ACC_ReceiptsVoucherMappings",
                column: "VoucherTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ReceiptsVoucherMappings_FactType",
                schema: "dbo",
                table: "ACC_ReceiptsVoucherMappings",
                column: "FactType",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ReceiptsVoucherMappings_PublicId",
                schema: "dbo",
                table: "ACC_ReceiptsVoucherMappings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostingRules_Account",
                schema: "dbo",
                table: "ACC_TreasuryPostingRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostingRules_CostCenter",
                schema: "dbo",
                table: "ACC_TreasuryPostingRules",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostingRules_FactType_Role",
                schema: "dbo",
                table: "ACC_TreasuryPostingRules",
                columns: new[] { "FactType", "Role", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TreasuryPostingRules_DimensionKey_ValidFrom",
                schema: "dbo",
                table: "ACC_TreasuryPostingRules",
                columns: new[] { "DimensionKey", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TreasuryPostingRules_PublicId",
                schema: "dbo",
                table: "ACC_TreasuryPostingRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostings_AccountingDocument",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostings_ActorUser",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostings_Batch",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "BatchPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostings_Related",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "RelatedMessagePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryPostings_Source",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "SourcePublicId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TreasuryPostings_MessagePublicId",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TreasuryPostings_PublicId",
                schema: "dbo",
                table: "ACC_TreasuryPostings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryVoucherMappings_CrossDocumentType",
                schema: "dbo",
                table: "ACC_TreasuryVoucherMappings",
                column: "CrossDocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TreasuryVoucherMappings_VoucherType",
                schema: "dbo",
                table: "ACC_TreasuryVoucherMappings",
                column: "VoucherTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TreasuryVoucherMappings_FactType",
                schema: "dbo",
                table: "ACC_TreasuryVoucherMappings",
                column: "FactType",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TreasuryVoucherMappings_PublicId",
                schema: "dbo",
                table: "ACC_TreasuryVoucherMappings",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACC_OwnBankAccountLinks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ReceiptsPostingRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ReceiptsPostings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ReceiptsVoucherMappings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_TreasuryPostingRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_TreasuryPostings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_TreasuryVoucherMappings",
                schema: "dbo");
        }
    }
}
