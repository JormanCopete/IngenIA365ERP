using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Admin
{
    /// <inheritdoc />
    public partial class AddUserSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_UserSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ADM_UserSettings", x => x.Id);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_UserSettings",
                schema: "dbo");
        }
    }
}
