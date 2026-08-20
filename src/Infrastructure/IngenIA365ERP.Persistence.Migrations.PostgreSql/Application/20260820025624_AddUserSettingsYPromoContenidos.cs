using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class AddUserSettingsYPromoContenidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_PromoContenidos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Texto = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    TextoEnlace = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Enlace = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Imagen = table.Column<byte[]>(type: "bytea", nullable: true),
                    ImagenTipoMime = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ImagenTextoAlternativo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Publicado = table.Column<bool>(type: "boolean", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VigenteHasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ADM_PromoContenidos", x => x.Id);
                });

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
                name: "ADM_PromoContenidos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ADM_UserSettings",
                schema: "dbo");
        }
    }
}
