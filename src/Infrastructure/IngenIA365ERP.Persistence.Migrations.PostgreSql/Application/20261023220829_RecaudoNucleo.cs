using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    LoanModality = table.Column<int>(type: "integer", nullable: true),
                    AccountModality = table.Column<int>(type: "integer", nullable: true),
                    OnlyWhenInstallmentsDue = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_ChannelModalityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CollectionPoints",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsMainCashBoxVault = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "RCD_DocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    UsesPointPrefix = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_MiscConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    RequiresThirdParty = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresCrossDocument = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_MiscConcepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PayerControls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CutoffTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ContractVersionObligations = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ContractVersionAccounts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
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
                    table.PrimaryKey("PK_RCD_Setup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_UserPaymasterScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PaymasterPersonId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectionPointId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BaseMode = table.Column<int>(type: "integer", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashWarnCap = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CashBlockCap = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DailyCashOutCap = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsMainCashBox = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                name: "RCD_ContingencyBooks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectionPointId = table.Column<int>(type: "integer", nullable: false),
                    FromNumber = table.Column<int>(type: "integer", nullable: false),
                    ToNumber = table.Column<int>(type: "integer", nullable: false),
                    RegisteredBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentMeansId = table.Column<int>(type: "integer", nullable: false),
                    CollectionPointId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PaymentMeansCollectionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PaymentMeansCollectionPoints_COR_PaymentMeans_PaymentMe~",
                        column: x => x.PaymentMeansId,
                        principalSchema: "dbo",
                        principalTable: "COR_PaymentMeans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PaymentMeansCollectionPoints_RCD_CollectionPoints_Colle~",
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectionPointId = table.Column<int>(type: "integer", nullable: false),
                    SupervisorUserId = table.Column<int>(type: "integer", nullable: false),
                    DelegateUserId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_RCD_SupervisorDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_SupervisorDelegations_RCD_CollectionPoints_CollectionPo~",
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CollectionPointId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_UserCollectionPointScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_UserCollectionPointScopes_RCD_CollectionPoints_Collecti~",
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashBoxId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CashBoxId = table.Column<int>(type: "integer", nullable: false),
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    MainCashBoxId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                name: "RCD_VaultCustodians",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VaultId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
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
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashBoxes_PublicId",
                schema: "dbo",
                table: "RCD_CashBoxes",
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
                filter: "\"IsDeleted\" = FALSE");

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
                name: "UK_RCD_DocumentTypes_Code",
                schema: "dbo",
                table: "RCD_DocumentTypes",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymentMeansCollectionPoints_PublicId",
                schema: "dbo",
                table: "RCD_PaymentMeansCollectionPoints",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Setup_PublicId",
                schema: "dbo",
                table: "RCD_Setup",
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
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

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
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Vaults_PublicId",
                schema: "dbo",
                table: "RCD_Vaults",
                column: "PublicId",
                unique: true);

            // Feature 015 (T0050; data-model §1.1): la fila única de RCD_Setup, con Id = 1, sin activar, la hora de corte por defecto (18:00,
            // Recaudo.HoraDeCorte) y las versiones 1 de los contratos. Idempotente: no hace nada si la fila ya existe. Es el nivel 1 del cerrojo.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""RCD_Setup"" (""Id"", ""PublicId"", ""Status"", ""CutoffTime"", ""ContractVersionObligations"", ""ContractVersionAccounts"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
OVERRIDING SYSTEM VALUE
SELECT 1, gen_random_uuid(), 1, TIME '18:00', 1, 1, FALSE, now(), 'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_Setup"");
SELECT setval(pg_get_serial_sequence('dbo.""RCD_Setup""', 'Id'), GREATEST((SELECT MAX(""Id"") FROM dbo.""RCD_Setup""), 1));
");

            // Feature 015, US1 (T0075; FR-026, FR-027, FR-198; D38): un tipo de documento por clase de R0, los conceptos del sistema (no se
            // inactivan) y las reglas de modalidad de los canales no interactivos (créditos CycleInstallment; cuentas Installments si hay
            // exigibles, si no Free). Idempotente: no repite lo que ya existe ni ocupa una clase ya tomada.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""RCD_DocumentTypes"" (""PublicId"", ""Code"", ""Name"", ""Kind"", ""UsesPointPrefix"", ""Status"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT gen_random_uuid(), v.code, v.name, v.kind, FALSE, 1, FALSE, now(), 'SYSTEM'
FROM (VALUES ('RC', 'Recibo de caja', 1), ('RB', 'Recibo de banco', 2), ('RP', 'Recibo de pagaduría', 4), ('RT', 'Recibo de contingencia', 6), ('AN', 'Anulación de recibo', 8), ('MC', 'Movimiento de caja', 9), ('AS', 'Ajuste de sesión de caja', 10)) AS v(code, name, kind)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_DocumentTypes"" t WHERE t.""Code"" = v.code AND t.""IsDeleted"" = FALSE)
  AND NOT EXISTS (SELECT 1 FROM dbo.""RCD_DocumentTypes"" t WHERE t.""Kind"" = v.kind AND t.""Status"" = 1 AND t.""IsDeleted"" = FALSE);
INSERT INTO dbo.""RCD_MiscConcepts"" (""PublicId"", ""Code"", ""Name"", ""Direction"", ""RequiresThirdParty"", ""RequiresCrossDocument"", ""RequiresInvoice"", ""RequiresApproval"", ""IsSystem"", ""Status"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT gen_random_uuid(), v.code, v.name, 1, FALSE, FALSE, FALSE, FALSE, TRUE, 1, FALSE, now(), 'SYSTEM'
FROM (VALUES ('GIRODEV', 'Giro devuelto'), ('SOBRANTE', 'Sobrantes de caja'), ('REINTEGRO', 'Reintegros'), ('RENDIM', 'Rendimientos')) AS v(code, name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_MiscConcepts"" c WHERE c.""Code"" = v.code AND c.""IsDeleted"" = FALSE);
INSERT INTO dbo.""RCD_ChannelModalityRules"" (""PublicId"", ""Channel"", ""Target"", ""LoanModality"", ""AccountModality"", ""OnlyWhenInstallmentsDue"", ""Priority"", ""ValidFrom"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT gen_random_uuid(), v.channel, v.target, v.loan, v.account, v.due, v.priority, DATE '2026-01-01', FALSE, now(), 'SYSTEM'
FROM (VALUES (2, 1, 3, NULL, FALSE, 1), (2, 2, NULL, 2, TRUE, 1), (2, 2, NULL, 1, FALSE, 2), (3, 1, 3, NULL, FALSE, 1), (3, 2, NULL, 2, TRUE, 1), (3, 2, NULL, 1, FALSE, 2), (6, 1, 3, NULL, FALSE, 1), (6, 2, NULL, 2, TRUE, 1), (6, 2, NULL, 1, FALSE, 2)) AS v(channel, target, loan, account, due, priority)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_ChannelModalityRules"" r WHERE r.""Channel"" = v.channel AND r.""Target"" = v.target AND r.""IsDeleted"" = FALSE);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_CashBoxCashiers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ChannelModalityRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ContingencyBooks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_DocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_MiscConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PayerControls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PaymentMeansCollectionPoints",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_Setup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_SupervisorDelegations",
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
                name: "RCD_Vaults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashBoxes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CollectionPoints",
                schema: "dbo");
        }
    }
}
