using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class Transporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDriver",
                schema: "dbo",
                table: "COR_People",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "COR_Connectors",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_COR_Connectors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BillingPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Proration = table.Column<short>(type: "smallint", nullable: false),
                    SalesDocumentTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WarehouseCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SalesPointCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DueDay = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_TRN_BillingPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TRN_BillingRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Period = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    Mode = table.Column<short>(type: "smallint", nullable: false),
                    PaymentMode = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StartedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoicesCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SummaryJson = table.Column<string>(type: "text", nullable: true),
                    OperationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: true),
                    CursorVehicleId = table.Column<int>(type: "integer", nullable: true),
                    AlreadyBilledCount = table.Column<int>(type: "integer", nullable: false),
                    OmittedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_TRN_BillingRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TRN_CatalogItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TRN_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TRN_DocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    WarningDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TRN_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Drivers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Since = table.Column<DateOnly>(type: "date", nullable: false),
                    RetiredOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_TRN_Drivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_Drivers_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ConnectorCredentials",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    ProtectedPayload = table.Column<string>(type: "text", nullable: false),
                    SetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SetBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastVerificationOk = table.Column<bool>(type: "boolean", nullable: true),
                    LastVerificationMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_COR_ConnectorCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ConnectorCredentials_COR_Connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "COR_Connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ConnectorRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    OriginRef = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastStatusCode = table.Column<int>(type: "integer", nullable: true),
                    LastResponseExcerpt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Warning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_COR_ConnectorRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ConnectorRequests_COR_Connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "COR_Connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ConnectorVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    DefinitionSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_COR_ConnectorVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ConnectorVersions_COR_Connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "COR_Connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Services",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AssociateCreditMeansCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CustomerCreditMeansCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ConnectorId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TRN_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_Services_COR_Connectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "COR_Connectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Vehicles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Plate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InternalNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RadioCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OwnerPersonId = table.Column<int>(type: "integer", nullable: false),
                    OwnerSince = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    StatusSince = table.Column<DateOnly>(type: "date", nullable: false),
                    RetiredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RetirementReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BrandId = table.Column<int>(type: "integer", nullable: true),
                    Line = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ModelYear = table.Column<short>(type: "smallint", nullable: true),
                    VehicleClassId = table.Column<int>(type: "integer", nullable: true),
                    BodyTypeId = table.Column<int>(type: "integer", nullable: true),
                    Color = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EngineDisplacementCc = table.Column<int>(type: "integer", nullable: true),
                    FuelId = table.Column<int>(type: "integer", nullable: true),
                    PassengerCapacity = table.Column<short>(type: "smallint", nullable: true),
                    EngineNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ChassisNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Vin = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TransitLicenseNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TransitAuthorityId = table.Column<int>(type: "integer", nullable: true),
                    RegistrationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ServiceClassId = table.Column<int>(type: "integer", nullable: true),
                    ModalityId = table.Column<int>(type: "integer", nullable: true),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    AffiliationContractNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AffiliationContractFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    AffiliationContractTo = table.Column<DateOnly>(type: "date", nullable: true),
                    OperationCardNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RollingGroupId = table.Column<int>(type: "integer", nullable: true),
                    MinDailyRolling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinMonthlyRolling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_TRN_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_COR_People_OwnerPersonId",
                        column: x => x.OwnerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_BodyTypeId",
                        column: x => x.BodyTypeId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_FuelId",
                        column: x => x.FuelId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_ModalityId",
                        column: x => x.ModalityId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_RollingGroupId",
                        column: x => x.RollingGroupId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_ServiceClassId",
                        column: x => x.ServiceClassId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_TransitAuthorityId",
                        column: x => x.TransitAuthorityId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Vehicles_TRN_CatalogItems_VehicleClassId",
                        column: x => x.VehicleClassId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ConnectorCalls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<short>(type: "smallint", nullable: false),
                    ResponseExcerpt = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RequestBodySanitized = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Url = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_COR_ConnectorCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ConnectorCalls_COR_ConnectorRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dbo",
                        principalTable: "COR_ConnectorRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_DriverAssignments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DriverId = table.Column<int>(type: "integer", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_TRN_DriverAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_DriverAssignments_TRN_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_DriverAssignments_TRN_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ServiceCharges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    BillingRunId = table.Column<int>(type: "integer", nullable: false),
                    OwnerPersonId = table.Column<int>(type: "integer", nullable: false),
                    InvoicePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InvoiceLinePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Route = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternalId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotifiedRequestPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewFlag = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_TRN_ServiceCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceCharges_COR_People_OwnerPersonId",
                        column: x => x.OwnerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceCharges_TRN_BillingRuns_BillingRunId",
                        column: x => x.BillingRunId,
                        principalSchema: "dbo",
                        principalTable: "TRN_BillingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceCharges_TRN_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_ServiceCharges_TRN_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_Subscriptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TRN_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_Subscriptions_TRN_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_Subscriptions_TRN_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_VehicleDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_TRN_VehicleDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_VehicleDocuments_TRN_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "TRN_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_VehicleDocuments_TRN_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_VehicleEquipment",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentTypeId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_TRN_VehicleEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_VehicleEquipment_TRN_CatalogItems_EquipmentTypeId",
                        column: x => x.EquipmentTypeId,
                        principalSchema: "dbo",
                        principalTable: "TRN_CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TRN_VehicleEquipment_TRN_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_VehicleEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ToValue = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FromAffiliation = table.Column<short>(type: "smallint", nullable: true),
                    ToAffiliation = table.Column<short>(type: "smallint", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    ChangedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_TRN_VehicleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_VehicleEvents_TRN_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_ChargeAllocations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChargeId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<short>(type: "smallint", nullable: false),
                    SourceMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReversesAllocationId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_TRN_ChargeAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_ChargeAllocations_TRN_ServiceCharges_ChargeId",
                        column: x => x.ChargeId,
                        principalSchema: "dbo",
                        principalTable: "TRN_ServiceCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TRN_SubscriptionPrices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_TRN_SubscriptionPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TRN_SubscriptionPrices_TRN_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "dbo",
                        principalTable: "TRN_Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COR_ConnectorCalls_RequestId",
                schema: "dbo",
                table: "COR_ConnectorCalls",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorCalls_PublicId",
                schema: "dbo",
                table: "COR_ConnectorCalls",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorCredentials_Connector",
                schema: "dbo",
                table: "COR_ConnectorCredentials",
                column: "ConnectorId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorCredentials_PublicId",
                schema: "dbo",
                table: "COR_ConnectorCredentials",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_ConnectorRequests_Connector_Status",
                schema: "dbo",
                table: "COR_ConnectorRequests",
                columns: new[] { "ConnectorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_ConnectorRequests_OriginRef",
                schema: "dbo",
                table: "COR_ConnectorRequests",
                column: "OriginRef");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorRequests_Message",
                schema: "dbo",
                table: "COR_ConnectorRequests",
                column: "MessagePublicId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorRequests_PublicId",
                schema: "dbo",
                table: "COR_ConnectorRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_Connectors_Platform",
                schema: "dbo",
                table: "COR_Connectors",
                column: "PlatformPublicId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_Connectors_PublicId",
                schema: "dbo",
                table: "COR_Connectors",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorVersions_Connector_Number",
                schema: "dbo",
                table: "COR_ConnectorVersions",
                columns: new[] { "ConnectorId", "Number" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ConnectorVersions_PublicId",
                schema: "dbo",
                table: "COR_ConnectorVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_BillingPolicies_ValidFrom",
                schema: "dbo",
                table: "TRN_BillingPolicies",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_BillingPolicies_PublicId",
                schema: "dbo",
                table: "TRN_BillingPolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_BillingRuns_Period_Status",
                schema: "dbo",
                table: "TRN_BillingRuns",
                columns: new[] { "Period", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_TRN_BillingRuns_OperationKey",
                schema: "dbo",
                table: "TRN_BillingRuns",
                column: "OperationKey",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_BillingRuns_PublicId",
                schema: "dbo",
                table: "TRN_BillingRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_CatalogItems_Kind_Code",
                schema: "dbo",
                table: "TRN_CatalogItems",
                columns: new[] { "Kind", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_CatalogItems_PublicId",
                schema: "dbo",
                table: "TRN_CatalogItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ChargeAllocations_ReceiptPublicId",
                schema: "dbo",
                table: "TRN_ChargeAllocations",
                column: "ReceiptPublicId");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ChargeAllocations_Charge_Message",
                schema: "dbo",
                table: "TRN_ChargeAllocations",
                columns: new[] { "ChargeId", "SourceMessagePublicId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"SourceMessagePublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ChargeAllocations_PublicId",
                schema: "dbo",
                table: "TRN_ChargeAllocations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_DocumentTypes_Code",
                schema: "dbo",
                table: "TRN_DocumentTypes",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_DocumentTypes_PublicId",
                schema: "dbo",
                table: "TRN_DocumentTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_DriverAssignments_DriverId",
                schema: "dbo",
                table: "TRN_DriverAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_DriverAssignments_Vehicle_Driver",
                schema: "dbo",
                table: "TRN_DriverAssignments",
                columns: new[] { "VehicleId", "DriverId" });

            migrationBuilder.CreateIndex(
                name: "UK_TRN_DriverAssignments_PublicId",
                schema: "dbo",
                table: "TRN_DriverAssignments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Drivers_PersonId",
                schema: "dbo",
                table: "TRN_Drivers",
                column: "PersonId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Drivers_PublicId",
                schema: "dbo",
                table: "TRN_Drivers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_BillingRunId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "BillingRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_InvoiceNumber",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_InvoicePublicId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "InvoicePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_LoanPublicId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "LoanPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_OwnerPersonId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_PaymentPublicId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "PaymentPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_ServiceCharges_ServiceId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ServiceCharges_ExternalId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ServiceCharges_PublicId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ServiceCharges_Vehicle_Service_Period",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                columns: new[] { "VehicleId", "ServiceId", "Period" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Services_ConnectorId",
                schema: "dbo",
                table: "TRN_Services",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Services_Product",
                schema: "dbo",
                table: "TRN_Services",
                column: "ProductPublicId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Services_PublicId",
                schema: "dbo",
                table: "TRN_Services",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_SubscriptionPrices_SubscriptionId",
                schema: "dbo",
                table: "TRN_SubscriptionPrices",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_SubscriptionPrices_PublicId",
                schema: "dbo",
                table: "TRN_SubscriptionPrices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Subscriptions_ServiceId",
                schema: "dbo",
                table: "TRN_Subscriptions",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Subscriptions_Vehicle_Service",
                schema: "dbo",
                table: "TRN_Subscriptions",
                columns: new[] { "VehicleId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Subscriptions_PublicId",
                schema: "dbo",
                table: "TRN_Subscriptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_VehicleDocuments_DocumentTypeId",
                schema: "dbo",
                table: "TRN_VehicleDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_VehicleDocuments_ExpiresOn",
                schema: "dbo",
                table: "TRN_VehicleDocuments",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_VehicleDocuments_PublicId",
                schema: "dbo",
                table: "TRN_VehicleDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_VehicleDocuments_Vehicle_Type_Vigente",
                schema: "dbo",
                table: "TRN_VehicleDocuments",
                columns: new[] { "VehicleId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"SupersededAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_VehicleEquipment_EquipmentTypeId",
                schema: "dbo",
                table: "TRN_VehicleEquipment",
                column: "EquipmentTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_VehicleEquipment_PublicId",
                schema: "dbo",
                table: "TRN_VehicleEquipment",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_VehicleEquipment_Vehicle_Type",
                schema: "dbo",
                table: "TRN_VehicleEquipment",
                columns: new[] { "VehicleId", "EquipmentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_VehicleEvents_Vehicle_Kind_Date",
                schema: "dbo",
                table: "TRN_VehicleEvents",
                columns: new[] { "VehicleId", "Kind", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UK_TRN_VehicleEvents_PublicId",
                schema: "dbo",
                table: "TRN_VehicleEvents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_BodyTypeId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "BodyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_BranchId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_BrandId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_FuelId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "FuelId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_ModalityId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "ModalityId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_OwnerPersonId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_RollingGroupId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "RollingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_ServiceClassId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "ServiceClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_Status",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_TransitAuthorityId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "TransitAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_TRN_Vehicles_VehicleClassId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "VehicleClassId");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Vehicles_InternalNumber",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "InternalNumber",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Vehicles_Plate",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "Plate",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" <> 3");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Vehicles_PublicId",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_Vehicles_RadioCode",
                schema: "dbo",
                table: "TRN_Vehicles",
                column: "RadioCode",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" <> 3 AND \"RadioCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COR_ConnectorCalls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ConnectorCredentials",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ConnectorVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_BillingPolicies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_ChargeAllocations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_DriverAssignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_SubscriptionPrices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_VehicleDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_VehicleEquipment",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_VehicleEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ConnectorRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_ServiceCharges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_Drivers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_Subscriptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_DocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_BillingRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_Services",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_Vehicles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_Connectors",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRN_CatalogItems",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "IsDriver",
                schema: "dbo",
                table: "COR_People");
        }
    }
}
