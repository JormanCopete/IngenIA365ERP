using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Admin
{
    /// <inheritdoc />
    public partial class PasesDeIngreso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowLoginPass",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoginPassMaxHours",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.CreateTable(
                name: "ADM_LoginPasses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByMaster = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    SecurityStampAtGrant = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_LoginPasses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_LoginPasses_CentralUserId_TenantId",
                schema: "dbo",
                table: "ADM_LoginPasses",
                columns: new[] { "CentralUserId", "TenantId" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_LoginPasses_PublicId",
                schema: "dbo",
                table: "ADM_LoginPasses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADM_LoginPasses_TenantId_GrantedAt",
                schema: "dbo",
                table: "ADM_LoginPasses",
                columns: new[] { "TenantId", "GrantedAt" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UX_ADM_LoginPasses_TokenHash",
                schema: "dbo",
                table: "ADM_LoginPasses",
                column: "TokenHash",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_LoginPasses",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "AllowLoginPass",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies");

            migrationBuilder.DropColumn(
                name: "LoginPassMaxHours",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies");
        }
    }
}
