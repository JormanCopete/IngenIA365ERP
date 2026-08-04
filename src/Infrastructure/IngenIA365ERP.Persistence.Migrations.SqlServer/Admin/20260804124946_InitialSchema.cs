using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ADM_CentralUserLoginAttempts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockoutAppliedSeconds = table.Column<int>(type: "int", nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MfaSecret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DefaultTenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsGlobalMasterAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviteAsTenantAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedByCentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ADM_Invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_PasswordResetTokens",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    RequesterIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_PasswordResetTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Roles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_TenantMemberships",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsTenantAdmin = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ADM_TenantMemberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_TenantMfaPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeactivatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ADM_TenantMfaPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Tenants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subdomain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlanType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Basic"),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LegalAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TaxRegime = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    StorageLimitMb = table.Column<long>(type: "bigint", nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConnectionString = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Identifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LicenseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_ADM_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_CentralUserClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_CentralUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ADM_CentralUserClaims_ADM_CentralUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "ADM_CentralUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ADM_CentralUserLogins",
                schema: "dbo",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_CentralUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_ADM_CentralUserLogins_ADM_CentralUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "ADM_CentralUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ADM_CentralUserTokens",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_CentralUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_ADM_CentralUserTokens_ADM_CentralUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "ADM_CentralUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ADM_CentralUserRoles",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_CentralUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_ADM_CentralUserRoles_ADM_CentralUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "ADM_CentralUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ADM_CentralUserRoles_ADM_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "ADM_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ADM_RoleClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ADM_RoleClaims_ADM_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "ADM_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Branches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsHeadquarters = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false, defaultValue: "COP"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextBillingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    SettingKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "String"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModulePrefix = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
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
                filter: "[IsHeadquarters] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_CentralUserClaims_UserId",
                schema: "dbo",
                table: "ADM_CentralUserClaims",
                column: "UserId");

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
                filter: "[IpAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_CentralUserLogins_UserId",
                schema: "dbo",
                table: "ADM_CentralUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_CentralUserRoles_RoleId",
                schema: "dbo",
                table: "ADM_CentralUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "dbo",
                table: "ADM_CentralUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "dbo",
                table: "ADM_CentralUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_Email_Tenant_Status",
                schema: "dbo",
                table: "ADM_Invitations",
                columns: new[] { "NormalizedEmail", "TenantId", "Status" },
                filter: "[IsDeleted] = 0");

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
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Invitations_Tenant_Status",
                schema: "dbo",
                table: "ADM_Invitations",
                columns: new[] { "TenantId", "Status" },
                filter: "[IsDeleted] = 0");

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
                filter: "[ConsumedAt] IS NULL AND [IsDeleted] = 0");

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
                name: "IX_ADM_RoleClaims_RoleId",
                schema: "dbo",
                table: "ADM_RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "dbo",
                table: "ADM_Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

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
                filter: "[IsDeleted] = 0");

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
                filter: "[IsTenantAdmin] = 1 AND [Status] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_TenantMemberships_Tenant_Status",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                columns: new[] { "TenantId", "Status" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_TenantMemberships_CentralUser_Tenant",
                schema: "dbo",
                table: "ADM_TenantMemberships",
                columns: new[] { "CentralUserId", "TenantId" },
                unique: true,
                filter: "[IsDeleted] = 0");

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
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Tenants_Nit",
                schema: "dbo",
                table: "ADM_Tenants",
                column: "Nit",
                unique: true,
                filter: "[Nit] IS NOT NULL AND [IsDeleted] = 0");

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
                unique: true,
                filter: "[Subdomain] IS NOT NULL");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_Branches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUserClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUserLoginAttempts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUserLogins",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUserRoles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_CentralUserTokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_Invitations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_PasswordResetTokens",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_RoleClaims",
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
                name: "ADM_CentralUsers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_Roles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_Tenants",
                schema: "dbo");
        }
    }
}
