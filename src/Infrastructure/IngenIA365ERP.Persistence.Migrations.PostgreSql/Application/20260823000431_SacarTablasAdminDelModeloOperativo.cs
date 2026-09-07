using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class SacarTablasAdminDelModeloOperativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SEC_PasswordPolicies_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_PasswordPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_SEC_Roles_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_SEC_UserBranchAssignments_ADM_Branches_BranchId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SEC_UserBranchAssignments_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SEC_UserTenantAssignments_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_UserTenantAssignments");

            migrationBuilder.DropTable(
                name: "ADM_Branches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUserLoginAttempts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUsers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_Invitations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_PasswordResetTokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_PromoContenidos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_Subscriptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_TenantMemberships",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_TenantMfaPolicies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_TenantSettings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_UserSettings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_Tenants",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_SEC_UserTenantAssignments_TenantId",
                schema: "dbo",
                table: "SEC_UserTenantAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SEC_UserBranchAssignments_BranchId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments");

            migrationBuilder.DropIndex(
                name: "IX_SEC_UserBranchAssignments_TenantId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_CentralUserLoginAttempts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    LockoutAppliedSeconds = table.Column<int>(type: "integer", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_CentralUserLoginAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_CentralUsers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DefaultTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsGlobalMasterAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MfaSecret = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "text", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_CentralUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Invitations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InviteAsTenantAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_PasswordResetTokens",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_PasswordResetTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_PromoContenidos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Enlace = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Imagen = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImagenTextoAlternativo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImagenTipoMime = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Publicado = table.Column<bool>(type: "boolean", nullable: false),
                    Texto = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    TextoEnlace = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VigenteHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_PromoContenidos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_TenantMemberships",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsTenantAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_TenantMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_TenantMfaPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeactivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_TenantMfaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Tenants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    ContactPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DatabaseName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LegalAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Basic"),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StorageLimitMb = table.Column<long>(type: "bigint", nullable: false, defaultValue: 5120L),
                    Subdomain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaxRegime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_UserSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TenantPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Branches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsHeadquarters = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ADM_Branches_ADM_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "ADM_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Subscriptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "COP"),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NextBillingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ADM_Subscriptions_ADM_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "ADM_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ADM_TenantSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModulePrefix = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    ValueType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "String"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_TenantSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ADM_TenantSettings_ADM_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "ADM_Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SEC_UserTenantAssignments_TenantId",
                schema: "dbo",
                table: "SEC_UserTenantAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SEC_UserBranchAssignments_BranchId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SEC_UserBranchAssignments_TenantId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Branches_PublicId",
                schema: "dbo",
                table: "ADM_Branches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Branches_TenantId_Code",
                schema: "dbo",
                table: "ADM_Branches",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Branches_TenantId_IsHeadquarters",
                schema: "dbo",
                table: "ADM_Branches",
                columns: new[] { "TenantId", "IsHeadquarters" },
                unique: true,
                filter: "\"IsHeadquarters\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_LoginAttempts_Email_Timestamp",
                schema: "dbo",
                table: "ADM_CentralUserLoginAttempts",
                columns: new[] { "NormalizedEmail", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_LoginAttempts_Ip_Timestamp",
                schema: "dbo",
                table: "ADM_CentralUserLoginAttempts",
                columns: new[] { "IpAddress", "Timestamp" },
                descending: new[] { false, true },
                filter: "\"IpAddress\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_Email_Tenant_Status",
                schema: "dbo",
                table: "ADM_Invitations",
                columns: new[] { "NormalizedEmail", "TenantId", "Status" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_PublicId",
                schema: "dbo",
                table: "ADM_Invitations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_Status_ExpiresAt",
                schema: "dbo",
                table: "ADM_Invitations",
                columns: new[] { "Status", "ExpiresAt" },
                filter: "\"Status\" = 0 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_Tenant_Status",
                schema: "dbo",
                table: "ADM_Invitations",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_Invitations_TokenHash",
                schema: "dbo",
                table: "ADM_Invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_PasswordResetTokens_CentralUserId_ExpiresAt",
                schema: "dbo",
                table: "ADM_PasswordResetTokens",
                columns: new[] { "CentralUserId", "ExpiresAt" },
                filter: "\"ConsumedAt\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_PasswordResetTokens_PublicId",
                schema: "dbo",
                table: "ADM_PasswordResetTokens",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_PasswordResetTokens_TokenHash",
                schema: "dbo",
                table: "ADM_PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_PromoContenidos_Publicado_Orden",
                schema: "dbo",
                table: "ADM_PromoContenidos",
                columns: new[] { "Publicado", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_PromoContenidos_PublicId",
                schema: "dbo",
                table: "ADM_PromoContenidos",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Subscriptions_PublicId",
                schema: "dbo",
                table: "ADM_Subscriptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Subscriptions_TenantId",
                schema: "dbo",
                table: "ADM_Subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantMemberships_CentralUser_Status",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                columns: new[] { "CentralUserId", "Status" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantMemberships_PublicId",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantMemberships_Tenant_ActiveAdmins",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                column: "TenantId",
                filter: "\"IsTenantAdmin\" = TRUE AND \"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantMemberships_Tenant_Status",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                columns: new[] { "TenantId", "Status" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_TenantMemberships_CentralUser_Tenant",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                columns: new[] { "CentralUserId", "TenantId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantMfaPolicies_PublicId",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ADM_TenantMfaPolicies_TenantId",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                column: "TenantId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Tenants_Nit",
                schema: "dbo",
                table: "ADM_Tenants",
                column: "Nit",
                unique: true,
                filter: "\"Nit\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Tenants_PublicId",
                schema: "dbo",
                table: "ADM_Tenants",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Tenants_SchemaName",
                schema: "dbo",
                table: "ADM_Tenants",
                column: "SchemaName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Tenants_Subdomain",
                schema: "dbo",
                table: "ADM_Tenants",
                column: "Subdomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantSettings_PublicId",
                schema: "dbo",
                table: "ADM_TenantSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantSettings_TenantId_SettingKey",
                schema: "dbo",
                table: "ADM_TenantSettings",
                columns: new[] { "TenantId", "SettingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_UserSettings_PublicId",
                schema: "dbo",
                table: "ADM_UserSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_UserSettings_Usuario",
                schema: "dbo",
                table: "ADM_UserSettings",
                column: "CentralUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_UserSettings_Usuario_Tenant_Clave",
                schema: "dbo",
                table: "ADM_UserSettings",
                columns: new[] { "CentralUserId", "TenantPublicId", "SettingKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SEC_PasswordPolicies_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_PasswordPolicies",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "ADM_Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SEC_Roles_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_Roles",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "ADM_Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SEC_UserBranchAssignments_ADM_Branches_BranchId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments",
                column: "BranchId",
                principalSchema: "dbo",
                principalTable: "ADM_Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SEC_UserBranchAssignments_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_UserBranchAssignments",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "ADM_Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SEC_UserTenantAssignments_ADM_Tenants_TenantId",
                schema: "dbo",
                table: "SEC_UserTenantAssignments",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "ADM_Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
