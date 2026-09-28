using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class VentasYPuntoDeVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CashRegisterId",
                schema: "dbo",
                table: "INV_Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CashSessionId",
                schema: "dbo",
                table: "INV_Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                schema: "dbo",
                table: "INV_Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PointOfSaleId",
                schema: "dbo",
                table: "INV_Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                schema: "dbo",
                table: "INV_Documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedLabel",
                schema: "dbo",
                table: "INV_Documents",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                schema: "dbo",
                table: "INV_DocumentLines",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceListId",
                schema: "dbo",
                table: "INV_DocumentLines",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "COR_CardAcquirers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_COR_CardAcquirers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_CardAcquirers_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_CardNetworks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CardKind = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_COR_CardNetworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_CashDenominations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_COR_CashDenominations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_DiscountCaps",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    MaxLineRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MaxDocumentRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_INV_DiscountCaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DiscountCaps_SEC_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentLineDiscounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentLineId = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<byte>(type: "smallint", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    FromDocumentDiscount = table.Column<bool>(type: "boolean", nullable: false),
                    IsPriceOverride = table.Column<bool>(type: "boolean", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CapRateApplied = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalRequestId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovalMethod = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_INV_DocumentLineDiscounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineDiscounts_COR_ApprovalRequests_ApprovalRequ~",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "dbo",
                        principalTable: "COR_ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineDiscounts_INV_DocumentLines_DocumentLineId",
                        column: x => x.DocumentLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineDiscounts_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineDiscounts_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PointsOfSale",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    SalesChannelId = table.Column<int>(type: "integer", nullable: false),
                    PosEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DefaultWarehouseId = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_INV_PointsOfSale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PointsOfSale_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PointsOfSale_INV_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "dbo",
                        principalTable: "INV_SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PointsOfSale_INV_Warehouses_DefaultWarehouseId",
                        column: x => x.DefaultWarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PriceLists",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IncludesTaxes = table.Column<bool>(type: "boolean", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    Segment = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    SalesChannelId = table.Column<int>(type: "integer", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    ScopeKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DimensionCount = table.Column<byte>(type: "smallint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_INV_PriceLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PriceLists_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceLists_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceLists_INV_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "dbo",
                        principalTable: "INV_SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_CardTerminals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CardAcquirerId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Serial = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_COR_CardTerminals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_CardTerminals_COR_CardAcquirers_CardAcquirerId",
                        column: x => x.CardAcquirerId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardAcquirers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_PaymentMeans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DisplayOrder = table.Column<short>(type: "smallint", nullable: false),
                    QuickKey = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Class = table.Column<int>(type: "integer", nullable: false),
                    CardNetworkId = table.Column<int>(type: "integer", nullable: true),
                    CardAcquirerId = table.Column<int>(type: "integer", nullable: true),
                    BankId = table.Column<int>(type: "integer", nullable: true),
                    DestinationAccountNumber = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    DestinationAccountType = table.Column<byte>(type: "smallint", nullable: true),
                    RequiresReference = table.Column<bool>(type: "boolean", nullable: false),
                    ReferenceKind = table.Column<int>(type: "integer", nullable: true),
                    ReferenceMinLength = table.Column<byte>(type: "smallint", nullable: true),
                    ReferenceMaxLength = table.Column<byte>(type: "smallint", nullable: true),
                    AllowsChange = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsPartial = table.Column<bool>(type: "boolean", nullable: false),
                    UniqueReference = table.Column<bool>(type: "boolean", nullable: false),
                    CountMethod = table.Column<int>(type: "integer", nullable: false),
                    RequiresTerminalBatchAtClose = table.Column<bool>(type: "boolean", nullable: false),
                    ToleranceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExpectedCommissionRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ExpectedCommissionFixed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DianPaymentMeansCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OfferedAtAllPointsOfSale = table.Column<bool>(type: "boolean", nullable: false),
                    OfferedInAllChannels = table.Column<bool>(type: "boolean", nullable: false),
                    OfferedForAllDocumentTypes = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTermDays = table.Column<short>(type: "smallint", nullable: true),
                    MaxTermDays = table.Column<short>(type: "smallint", nullable: true),
                    DefaultInstallments = table.Column<short>(type: "smallint", nullable: true),
                    MaxInstallments = table.Column<short>(type: "smallint", nullable: true),
                    InstallmentPeriodDays = table.Column<short>(type: "smallint", nullable: true),
                    SuggestedCreditLineCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_COR_PaymentMeans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_PaymentMeans_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_PaymentMeans_COR_CardAcquirers_CardAcquirerId",
                        column: x => x.CardAcquirerId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardAcquirers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_PaymentMeans_COR_CardNetworks_CardNetworkId",
                        column: x => x.CardNetworkId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DayCloses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    OperatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SessionCount = table.Column<short>(type: "smallint", nullable: false),
                    TotalExpected = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCounted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReopenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_INV_DayCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DayCloses_INV_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalSchema: "dbo",
                        principalTable: "INV_PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DayCloses_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DayCloses_SEC_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_UserPointOfSaleScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_INV_UserPointOfSaleScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_UserPointOfSaleScopes_INV_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalSchema: "dbo",
                        principalTable: "INV_PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_UserPointOfSaleScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PriceListItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PriceListId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_INV_PriceListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItems_INV_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "dbo",
                        principalTable: "INV_PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItems_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItems_INV_UnitsOfMeasure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashRegisters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    WarehouseId = table.Column<int>(type: "integer", nullable: false),
                    DefaultCardTerminalId = table.Column<int>(type: "integer", nullable: true),
                    DianCashRegisterPlate = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DianCashRegisterTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReceiptWidthMm = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)80),
                    PrintCopies = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)1),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_INV_CashRegisters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisters_COR_CardTerminals_DefaultCardTerminalId",
                        column: x => x.DefaultCardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisters_INV_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalSchema: "dbo",
                        principalTable: "INV_PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisters_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PaymentMeansChannels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    SalesChannelId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_INV_PaymentMeansChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PaymentMeansChannels_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PaymentMeansChannels_INV_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "dbo",
                        principalTable: "INV_SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PaymentMeansDocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_INV_PaymentMeansDocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PaymentMeansDocumentTypes_COR_PaymentMeans_PaymentMeans~",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PaymentMeansDocumentTypes_INV_DocumentTypes_DocumentTyp~",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PaymentMeansPointsOfSale",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_INV_PaymentMeansPointsOfSale", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PaymentMeansPointsOfSale_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PaymentMeansPointsOfSale_INV_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalSchema: "dbo",
                        principalTable: "INV_PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DayCloseLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayCloseId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    CardAcquirerId = table.Column<int>(type: "integer", nullable: true),
                    CardTerminalId = table.Column<int>(type: "integer", nullable: true),
                    DetailKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCount = table.Column<int>(type: "integer", nullable: false),
                    BatchNumbersJson = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_INV_DayCloseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DayCloseLines_COR_CardAcquirers_CardAcquirerId",
                        column: x => x.CardAcquirerId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardAcquirers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DayCloseLines_COR_CardTerminals_CardTerminalId",
                        column: x => x.CardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DayCloseLines_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DayCloseLines_INV_DayCloses_DayCloseId",
                        column: x => x.DayCloseId,
                        principalSchema: "dbo",
                        principalTable: "INV_DayCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashRegisterDocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashRegisterId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_INV_CashRegisterDocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisterDocumentTypes_INV_CashRegisters_CashRegiste~",
                        column: x => x.CashRegisterId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisterDocumentTypes_INV_DocumentTypes_DocumentTyp~",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashRegisterId = table.Column<int>(type: "integer", nullable: false),
                    PointOfSaleId = table.Column<int>(type: "integer", nullable: false),
                    CashierUserId = table.Column<int>(type: "integer", nullable: false),
                    CashierPersonId = table.Column<int>(type: "integer", nullable: true),
                    CashierName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OperatingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BaseMode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    OpeningBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseIncomeDocumentId = table.Column<int>(type: "integer", nullable: true),
                    ShortageTreatment = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    IsBlindCount = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusiveCashier = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_INV_CashSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashSessions_COR_People_CashierPersonId",
                        column: x => x.CashierPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashSessions_INV_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashSessions_INV_Documents_BaseIncomeDocumentId",
                        column: x => x.BaseIncomeDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashSessions_INV_PointsOfSale_PointOfSaleId",
                        column: x => x.PointOfSaleId,
                        principalSchema: "dbo",
                        principalTable: "INV_PointsOfSale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashSessions_SEC_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashSessions_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashCounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashSessionId = table.Column<int>(type: "integer", nullable: false),
                    CountedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CountedByUserId = table.Column<int>(type: "integer", nullable: false),
                    IsBlind = table.Column<bool>(type: "boolean", nullable: false),
                    TotalExpected = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCounted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DifferenceDocumentId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_INV_CashCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashCounts_INV_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashCounts_INV_Documents_DifferenceDocumentId",
                        column: x => x.DifferenceDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashCounts_SEC_Users_CountedByUserId",
                        column: x => x.CountedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentPayments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<short>(type: "smallint", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountTendered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ChangeGiven = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NormalizedReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CardTerminalId = table.Column<int>(type: "integer", nullable: true),
                    TerminalBatchNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: true),
                    CashSessionId = table.Column<int>(type: "integer", nullable: true),
                    MeansCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MeansName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MeansClass = table.Column<int>(type: "integer", nullable: false),
                    CardNetworkCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CardAcquirerCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CardAcquirerPersonId = table.Column<int>(type: "integer", nullable: true),
                    BankId = table.Column<int>(type: "integer", nullable: true),
                    DianPaymentMeansCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExpectedCommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundsPaymentId = table.Column<int>(type: "integer", nullable: true),
                    CreditTermDays = table.Column<short>(type: "smallint", nullable: true),
                    InstallmentCount = table.Column<short>(type: "smallint", nullable: true),
                    InstallmentPeriodDays = table.Column<short>(type: "smallint", nullable: true),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FinalDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SuggestedCreditLineCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PendingValidation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreditOrigin = table.Column<int>(type: "integer", nullable: true),
                    AccountsReceivableRecordedBy = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    ApprovalRequestId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_INV_DocumentPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPayments_COR_ApprovalRequests_ApprovalRequestId",
                        column: x => x.ApprovalRequestId,
                        principalSchema: "dbo",
                        principalTable: "COR_ApprovalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPayments_COR_CardTerminals_CardTerminalId",
                        column: x => x.CardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPayments_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPayments_INV_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPayments_INV_DocumentPayments_RefundsPaymentId",
                        column: x => x.RefundsPaymentId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPayments_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashCountLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashCountId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    CountMethod = table.Column<int>(type: "integer", nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DifferenceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCount = table.Column<int>(type: "integer", nullable: false),
                    ToleranceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithinTolerance = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Treatment = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_INV_CashCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashCountLines_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashCountLines_INV_CashCounts_CashCountId",
                        column: x => x.CashCountId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashMovementDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    CashSessionId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SourcePaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    TargetPaymentMeansId = table.Column<int>(type: "integer", nullable: true),
                    Destination = table.Column<int>(type: "integer", nullable: true),
                    DestinationCashRegisterId = table.Column<int>(type: "integer", nullable: true),
                    DestinationCashSessionId = table.Column<int>(type: "integer", nullable: true),
                    DepositBankId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DenominationsJson = table.Column<string>(type: "text", nullable: true),
                    ReclassifiedPaymentId = table.Column<int>(type: "integer", nullable: true),
                    TargetReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TargetAuthorizationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TargetCardTerminalId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_INV_CashMovementDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_COR_Banks_DepositBankId",
                        column: x => x.DepositBankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_COR_CardTerminals_TargetCardTermina~",
                        column: x => x.TargetCardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_COR_PaymentMeans_SourcePaymentMeans~",
                        column: x => x.SourcePaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_COR_PaymentMeans_TargetPaymentMeans~",
                        column: x => x.TargetPaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_INV_CashRegisters_DestinationCashRe~",
                        column: x => x.DestinationCashRegisterId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_INV_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_INV_CashSessions_DestinationCashSes~",
                        column: x => x.DestinationCashSessionId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_INV_DocumentPayments_ReclassifiedPa~",
                        column: x => x.ReclassifiedPaymentId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashMovementDetails_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_VoucherRedemptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    NormalizedNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DocumentPaymentId = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedByDocumentId = table.Column<int>(type: "integer", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_INV_VoucherRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_VoucherRedemptions_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_VoucherRedemptions_INV_DocumentPayments_DocumentPayment~",
                        column: x => x.DocumentPaymentId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_VoucherRedemptions_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_VoucherRedemptions_INV_Documents_ReleasedByDocumentId",
                        column: x => x.ReleasedByDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashCountDenominations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashCountLineId = table.Column<int>(type: "integer", nullable: false),
                    CashDenominationId = table.Column<int>(type: "integer", nullable: false),
                    DenominationValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_INV_CashCountDenominations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashCountDenominations_COR_CashDenominations_CashDenomi~",
                        column: x => x.CashDenominationId,
                        principalSchema: "dbo",
                        principalTable: "COR_CashDenominations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashCountDenominations_INV_CashCountLines_CashCountLine~",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashCountReferenceChecks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashCountLineId = table.Column<int>(type: "integer", nullable: false),
                    DocumentPaymentId = table.Column<int>(type: "integer", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_INV_CashCountReferenceChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashCountReferenceChecks_INV_CashCountLines_CashCountLi~",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashCountReferenceChecks_INV_DocumentPayments_DocumentP~",
                        column: x => x.DocumentPaymentId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashCountTerminalBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashCountLineId = table.Column<int>(type: "integer", nullable: false),
                    CardTerminalId = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BatchTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VoucherCount = table.Column<int>(type: "integer", nullable: false),
                    ExpectedTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_INV_CashCountTerminalBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashCountTerminalBatches_COR_CardTerminals_CardTerminal~",
                        column: x => x.CardTerminalId,
                        principalSchema: "dbo",
                        principalTable: "COR_CardTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashCountTerminalBatches_INV_CashCountLines_CashCountLi~",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CashDocumentLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<short>(type: "smallint", nullable: false),
                    CashCountLineId = table.Column<int>(type: "integer", nullable: false),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    Sign = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Treatment = table.Column<int>(type: "integer", nullable: false),
                    WithinTolerance = table.Column<bool>(type: "boolean", nullable: false),
                    CashierUserId = table.Column<int>(type: "integer", nullable: false),
                    CashierPersonId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_INV_CashDocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashDocumentLines_COR_PaymentMeans_PaymentMeansId",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashDocumentLines_COR_People_CashierPersonId",
                        column: x => x.CashierPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashDocumentLines_INV_CashCountLines_CashCountLineId",
                        column: x => x.CashCountLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashCountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashDocumentLines_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashDocumentLines_SEC_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_CashRegisterId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_CashSessionId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CashSessionId",
                filter: "\"CashSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_PointOfSaleId",
                schema: "dbo",
                table: "INV_Documents",
                column: "PointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_PriceListId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "PriceListId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_CardAcquirers_PersonId",
                schema: "dbo",
                table: "COR_CardAcquirers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_CardAcquirers_Code",
                schema: "dbo",
                table: "COR_CardAcquirers",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_CardAcquirers_PublicId",
                schema: "dbo",
                table: "COR_CardAcquirers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_CardNetworks_Code",
                schema: "dbo",
                table: "COR_CardNetworks",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_CardNetworks_PublicId",
                schema: "dbo",
                table: "COR_CardNetworks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_CardTerminals_Acquirer_Code",
                schema: "dbo",
                table: "COR_CardTerminals",
                columns: new[] { "CardAcquirerId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_CardTerminals_PublicId",
                schema: "dbo",
                table: "COR_CardTerminals",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_CashDenominations_Currency_Kind_Value",
                schema: "dbo",
                table: "COR_CashDenominations",
                columns: new[] { "Currency", "Kind", "Value" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_CashDenominations_PublicId",
                schema: "dbo",
                table: "COR_CashDenominations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_PaymentMeans_BankId",
                schema: "dbo",
                table: "COR_PaymentMeans",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_PaymentMeans_CardAcquirerId",
                schema: "dbo",
                table: "COR_PaymentMeans",
                column: "CardAcquirerId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_PaymentMeans_CardNetworkId",
                schema: "dbo",
                table: "COR_PaymentMeans",
                column: "CardNetworkId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_PaymentMeans_Code",
                schema: "dbo",
                table: "COR_PaymentMeans",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_PaymentMeans_PublicId",
                schema: "dbo",
                table: "COR_PaymentMeans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashCountDenominations_CashDenominationId",
                schema: "dbo",
                table: "INV_CashCountDenominations",
                column: "CashDenominationId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountDenominations_Line_Denomination",
                schema: "dbo",
                table: "INV_CashCountDenominations",
                columns: new[] { "CashCountLineId", "CashDenominationId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountDenominations_PublicId",
                schema: "dbo",
                table: "INV_CashCountDenominations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashCountLines_PaymentMeansId",
                schema: "dbo",
                table: "INV_CashCountLines",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountLines_Count_Means",
                schema: "dbo",
                table: "INV_CashCountLines",
                columns: new[] { "CashCountId", "PaymentMeansId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountLines_PublicId",
                schema: "dbo",
                table: "INV_CashCountLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashCountReferenceChecks_DocumentPaymentId",
                schema: "dbo",
                table: "INV_CashCountReferenceChecks",
                column: "DocumentPaymentId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountReferenceChecks_Line_Payment",
                schema: "dbo",
                table: "INV_CashCountReferenceChecks",
                columns: new[] { "CashCountLineId", "DocumentPaymentId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountReferenceChecks_PublicId",
                schema: "dbo",
                table: "INV_CashCountReferenceChecks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashCounts_CountedByUserId",
                schema: "dbo",
                table: "INV_CashCounts",
                column: "CountedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashCounts_DifferenceDocumentId",
                schema: "dbo",
                table: "INV_CashCounts",
                column: "DifferenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCounts_CashSessionId",
                schema: "dbo",
                table: "INV_CashCounts",
                column: "CashSessionId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCounts_PublicId",
                schema: "dbo",
                table: "INV_CashCounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashCountTerminalBatches_CardTerminalId",
                schema: "dbo",
                table: "INV_CashCountTerminalBatches",
                column: "CardTerminalId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountTerminalBatches_Line_Terminal_Batch",
                schema: "dbo",
                table: "INV_CashCountTerminalBatches",
                columns: new[] { "CashCountLineId", "CardTerminalId", "BatchNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashCountTerminalBatches_PublicId",
                schema: "dbo",
                table: "INV_CashCountTerminalBatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashDocumentLines_CashCountLineId",
                schema: "dbo",
                table: "INV_CashDocumentLines",
                column: "CashCountLineId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashDocumentLines_CashierPersonId",
                schema: "dbo",
                table: "INV_CashDocumentLines",
                column: "CashierPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashDocumentLines_CashierUserId",
                schema: "dbo",
                table: "INV_CashDocumentLines",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashDocumentLines_PaymentMeansId",
                schema: "dbo",
                table: "INV_CashDocumentLines",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashDocumentLines_Document_LineNumber",
                schema: "dbo",
                table: "INV_CashDocumentLines",
                columns: new[] { "DocumentId", "LineNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashDocumentLines_PublicId",
                schema: "dbo",
                table: "INV_CashDocumentLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_CashSessionId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_DepositBankId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "DepositBankId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_DestinationCashRegisterId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "DestinationCashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_DestinationCashSessionId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "DestinationCashSessionId",
                filter: "\"DestinationCashSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_ReclassifiedPaymentId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "ReclassifiedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_SourcePaymentMeansId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "SourcePaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_TargetCardTerminalId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "TargetCardTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashMovementDetails_TargetPaymentMeansId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "TargetPaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashMovementDetails_DocumentId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "DocumentId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashMovementDetails_PublicId",
                schema: "dbo",
                table: "INV_CashMovementDetails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashRegisterDocumentTypes_DocumentTypeId",
                schema: "dbo",
                table: "INV_CashRegisterDocumentTypes",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashRegisterDocumentTypes_PublicId",
                schema: "dbo",
                table: "INV_CashRegisterDocumentTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashRegisterDocumentTypes_Register_Role",
                schema: "dbo",
                table: "INV_CashRegisterDocumentTypes",
                columns: new[] { "CashRegisterId", "Role" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashRegisters_DefaultCardTerminalId",
                schema: "dbo",
                table: "INV_CashRegisters",
                column: "DefaultCardTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashRegisters_WarehouseId",
                schema: "dbo",
                table: "INV_CashRegisters",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashRegisters_Point_Code",
                schema: "dbo",
                table: "INV_CashRegisters",
                columns: new[] { "PointOfSaleId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashRegisters_PublicId",
                schema: "dbo",
                table: "INV_CashRegisters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashSessions_BaseIncomeDocumentId",
                schema: "dbo",
                table: "INV_CashSessions",
                column: "BaseIncomeDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashSessions_CashierPersonId",
                schema: "dbo",
                table: "INV_CashSessions",
                column: "CashierPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashSessions_ClosedByUserId",
                schema: "dbo",
                table: "INV_CashSessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashSessions_Point_OperatingDate",
                schema: "dbo",
                table: "INV_CashSessions",
                columns: new[] { "PointOfSaleId", "OperatingDate" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashSessions_Cashier_Open",
                schema: "dbo",
                table: "INV_CashSessions",
                column: "CashierUserId",
                unique: true,
                filter: "\"Status\" = 1 AND \"ExclusiveCashier\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashSessions_PublicId",
                schema: "dbo",
                table: "INV_CashSessions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashSessions_Register_Open",
                schema: "dbo",
                table: "INV_CashSessions",
                column: "CashRegisterId",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DayCloseLines_CardAcquirerId",
                schema: "dbo",
                table: "INV_DayCloseLines",
                column: "CardAcquirerId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DayCloseLines_CardTerminalId",
                schema: "dbo",
                table: "INV_DayCloseLines",
                column: "CardTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DayCloseLines_PaymentMeansId",
                schema: "dbo",
                table: "INV_DayCloseLines",
                column: "PaymentMeansId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DayCloseLines_DayClose_DetailKey",
                schema: "dbo",
                table: "INV_DayCloseLines",
                columns: new[] { "DayCloseId", "DetailKey" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DayCloseLines_PublicId",
                schema: "dbo",
                table: "INV_DayCloseLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DayCloses_ClosedByUserId",
                schema: "dbo",
                table: "INV_DayCloses",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DayCloses_ReopenedByUserId",
                schema: "dbo",
                table: "INV_DayCloses",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DayCloses_Point_Date_Closed",
                schema: "dbo",
                table: "INV_DayCloses",
                columns: new[] { "PointOfSaleId", "OperatingDate" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DayCloses_Point_Date_Version",
                schema: "dbo",
                table: "INV_DayCloses",
                columns: new[] { "PointOfSaleId", "OperatingDate", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DayCloses_PublicId",
                schema: "dbo",
                table: "INV_DayCloses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DiscountCaps_PublicId",
                schema: "dbo",
                table: "INV_DiscountCaps",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DiscountCaps_Role_ValidFrom",
                schema: "dbo",
                table: "INV_DiscountCaps",
                columns: new[] { "RoleId", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLineDiscounts_ApprovalRequestId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLineDiscounts_ApprovedByUserId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLineDiscounts_DocumentId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLineDiscounts_Line_Sequence",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                columns: new[] { "DocumentLineId", "Sequence" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLineDiscounts_PublicId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_ApprovalRequestId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_Means_Reference",
                schema: "dbo",
                table: "INV_DocumentPayments",
                columns: new[] { "PaymentMeansId", "NormalizedReference" },
                filter: "\"NormalizedReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_RefundsPaymentId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                column: "RefundsPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_Session_Means",
                schema: "dbo",
                table: "INV_DocumentPayments",
                columns: new[] { "CashSessionId", "PaymentMeansId" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_Terminal_Batch",
                schema: "dbo",
                table: "INV_DocumentPayments",
                columns: new[] { "CardTerminalId", "TerminalBatchNumber" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentPayments_Document_LineNumber",
                schema: "dbo",
                table: "INV_DocumentPayments",
                columns: new[] { "DocumentId", "LineNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentPayments_PublicId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PaymentMeansChannels_SalesChannelId",
                schema: "dbo",
                table: "INV_PaymentMeansChannels",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PaymentMeansChannels_Means_Channel",
                schema: "dbo",
                table: "INV_PaymentMeansChannels",
                columns: new[] { "PaymentMeansId", "SalesChannelId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PaymentMeansChannels_PublicId",
                schema: "dbo",
                table: "INV_PaymentMeansChannels",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PaymentMeansDocumentTypes_DocumentTypeId",
                schema: "dbo",
                table: "INV_PaymentMeansDocumentTypes",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PaymentMeansDocumentTypes_Means_Type",
                schema: "dbo",
                table: "INV_PaymentMeansDocumentTypes",
                columns: new[] { "PaymentMeansId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PaymentMeansDocumentTypes_PublicId",
                schema: "dbo",
                table: "INV_PaymentMeansDocumentTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PaymentMeansPointsOfSale_PointOfSaleId",
                schema: "dbo",
                table: "INV_PaymentMeansPointsOfSale",
                column: "PointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PaymentMeansPointsOfSale_Means_Point",
                schema: "dbo",
                table: "INV_PaymentMeansPointsOfSale",
                columns: new[] { "PaymentMeansId", "PointOfSaleId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PaymentMeansPointsOfSale_PublicId",
                schema: "dbo",
                table: "INV_PaymentMeansPointsOfSale",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PointsOfSale_BranchId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PointsOfSale_DefaultWarehouseId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "DefaultWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PointsOfSale_SalesChannelId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PointsOfSale_Code",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PointsOfSale_PublicId",
                schema: "dbo",
                table: "INV_PointsOfSale",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListItems_ProductId",
                schema: "dbo",
                table: "INV_PriceListItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListItems_UnitId",
                schema: "dbo",
                table: "INV_PriceListItems",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PriceListItems_List_Product_Unit",
                schema: "dbo",
                table: "INV_PriceListItems",
                columns: new[] { "PriceListId", "ProductId", "UnitId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PriceListItems_PublicId",
                schema: "dbo",
                table: "INV_PriceListItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceLists_BranchId",
                schema: "dbo",
                table: "INV_PriceLists",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceLists_PersonId",
                schema: "dbo",
                table: "INV_PriceLists",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceLists_SalesChannelId",
                schema: "dbo",
                table: "INV_PriceLists",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PriceLists_Code",
                schema: "dbo",
                table: "INV_PriceLists",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PriceLists_PublicId",
                schema: "dbo",
                table: "INV_PriceLists",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_PriceLists_Scope_ValidFrom",
                schema: "dbo",
                table: "INV_PriceLists",
                columns: new[] { "ScopeKey", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_INV_UserPointOfSaleScopes_PointOfSaleId",
                schema: "dbo",
                table: "INV_UserPointOfSaleScopes",
                column: "PointOfSaleId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_UserPointOfSaleScopes_Default",
                schema: "dbo",
                table: "INV_UserPointOfSaleScopes",
                column: "UserId",
                unique: true,
                filter: "\"IsDefault\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_UserPointOfSaleScopes_PublicId",
                schema: "dbo",
                table: "INV_UserPointOfSaleScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_UserPointOfSaleScopes_User_Point",
                schema: "dbo",
                table: "INV_UserPointOfSaleScopes",
                columns: new[] { "UserId", "PointOfSaleId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_INV_VoucherRedemptions_DocumentId",
                schema: "dbo",
                table: "INV_VoucherRedemptions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_VoucherRedemptions_DocumentPaymentId",
                schema: "dbo",
                table: "INV_VoucherRedemptions",
                column: "DocumentPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_VoucherRedemptions_ReleasedByDocumentId",
                schema: "dbo",
                table: "INV_VoucherRedemptions",
                column: "ReleasedByDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_VoucherRedemptions_Means_Number_Active",
                schema: "dbo",
                table: "INV_VoucherRedemptions",
                columns: new[] { "PaymentMeansId", "NormalizedNumber" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_VoucherRedemptions_PublicId",
                schema: "dbo",
                table: "INV_VoucherRedemptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_DocumentLines_INV_PriceLists_PriceListId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "PriceListId",
                principalSchema: "dbo",
                principalTable: "INV_PriceLists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_Documents_INV_CashRegisters_CashRegisterId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CashRegisterId",
                principalSchema: "dbo",
                principalTable: "INV_CashRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_Documents_INV_CashSessions_CashSessionId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CashSessionId",
                principalSchema: "dbo",
                principalTable: "INV_CashSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_Documents_INV_PointsOfSale_PointOfSaleId",
                schema: "dbo",
                table: "INV_Documents",
                column: "PointOfSaleId",
                principalSchema: "dbo",
                principalTable: "INV_PointsOfSale",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_DocumentLines_INV_PriceLists_PriceListId",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_INV_CashRegisters_CashRegisterId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_INV_CashSessions_CashSessionId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_INV_PointsOfSale_PointOfSaleId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropTable(
                name: "INV_CashCountDenominations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashCountReferenceChecks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashCountTerminalBatches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashDocumentLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashMovementDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashRegisterDocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DayCloseLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DiscountCaps",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentLineDiscounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PaymentMeansChannels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PaymentMeansDocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PaymentMeansPointsOfSale",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PriceListItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_UserPointOfSaleScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_VoucherRedemptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_CashDenominations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashCountLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DayCloses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PriceLists",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentPayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashCounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_PaymentMeans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashSessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_CardNetworks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CashRegisters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_CardTerminals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PointsOfSale",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_CardAcquirers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_INV_Documents_CashRegisterId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropIndex(
                name: "IX_INV_Documents_CashSessionId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropIndex(
                name: "IX_INV_Documents_PointOfSaleId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropIndex(
                name: "IX_INV_DocumentLines_PriceListId",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropColumn(
                name: "CashRegisterId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "CashSessionId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "PointOfSaleId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "SuspendedLabel",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "ListPrice",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropColumn(
                name: "PriceListId",
                schema: "dbo",
                table: "INV_DocumentLines");
        }
    }
}
