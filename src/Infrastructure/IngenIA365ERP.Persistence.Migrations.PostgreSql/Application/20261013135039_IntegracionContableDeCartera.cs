using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class IntegracionContableDeCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACC_LendingPostingRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModalityCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SubsegmentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RiskCategory = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    ConceptCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsEmployee = table.Column<bool>(type: "boolean", nullable: true),
                    InGracePeriod = table.Column<bool>(type: "boolean", nullable: true),
                    OnBehalfOfAssociate = table.Column<bool>(type: "boolean", nullable: true),
                    Restructured = table.Column<bool>(type: "boolean", nullable: true),
                    CollectorClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    DimensionKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SpecificityWeight = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_LendingPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_LendingPostingRules_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_LendingPostingRules_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_LendingPostingRules_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_LendingPostings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    MessageKind = table.Column<int>(type: "integer", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourcePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentClass = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceDocumentTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SourceDocumentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LoanPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_LendingPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_LendingPostings_ACC_Documents_AccountingDocumentId",
                        column: x => x.AccountingDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_LendingPostings_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_LendingVoucherMappings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CollectorClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VoucherTypeId = table.Column<int>(type: "integer", nullable: false),
                    CrossDocumentTypeId = table.Column<int>(type: "integer", nullable: true),
                    MappingKey = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_LendingVoucherMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_LendingVoucherMappings_ACC_CrossDocumentTypes_CrossDocu~",
                        column: x => x.CrossDocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_CrossDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_LendingVoucherMappings_ACC_VoucherTypes_VoucherTypeId",
                        column: x => x.VoucherTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_VoucherTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_RegulatoryIndicators",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersedesPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_RegulatoryIndicators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_RegulatoryIndicators_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostingRules_Account",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostingRules_Branch",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostingRules_CostCenter",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostingRules_Operation_Role",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                columns: new[] { "Operation", "Role", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_LendingPostingRules_DimensionKey_ValidFrom",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                columns: new[] { "DimensionKey", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_LendingPostingRules_PublicId",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostings_AccountingDocument",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostings_ActorUser",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostings_Batch",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "BatchPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostings_OperationDate",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "OperationDate");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostings_Related",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "RelatedMessagePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingPostings_Source",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "SourcePublicId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_LendingPostings_MessagePublicId",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_LendingPostings_PublicId",
                schema: "dbo",
                table: "ACC_LendingPostings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingVoucherMappings_CrossDocumentType",
                schema: "dbo",
                table: "ACC_LendingVoucherMappings",
                column: "CrossDocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_LendingVoucherMappings_VoucherType",
                schema: "dbo",
                table: "ACC_LendingVoucherMappings",
                column: "VoucherTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_LendingVoucherMappings_MappingKey",
                schema: "dbo",
                table: "ACC_LendingVoucherMappings",
                column: "MappingKey",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_LendingVoucherMappings_PublicId",
                schema: "dbo",
                table: "ACC_LendingVoucherMappings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_RegulatoryIndicators_Kind_Cutoff",
                schema: "dbo",
                table: "ACC_RegulatoryIndicators",
                columns: new[] { "Kind", "CutoffDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_RegulatoryIndicators_RegisteredByUser",
                schema: "dbo",
                table: "ACC_RegulatoryIndicators",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_RegulatoryIndicators_PublicId",
                schema: "dbo",
                table: "ACC_RegulatoryIndicators",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_RegulatoryIndicators_Supersedes",
                schema: "dbo",
                table: "ACC_RegulatoryIndicators",
                column: "SupersedesPublicId",
                unique: true,
                filter: "\"SupersedesPublicId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACC_LendingPostingRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_LendingPostings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_LendingVoucherMappings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_RegulatoryIndicators",
                schema: "dbo");
        }
    }
}
