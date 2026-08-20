using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TextoEnlace = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Enlace = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Imagen = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImagenTipoMime = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ImagenTextoAlternativo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VigenteHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ADM_PromoContenidos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_UserSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettingKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
