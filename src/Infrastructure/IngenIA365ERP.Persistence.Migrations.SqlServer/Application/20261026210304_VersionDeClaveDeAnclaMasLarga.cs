using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Aditiva (incidente del 2026-10-26): <c>COR_AuditAnchors.KeyVersion</c> pasa de 20 a 64 caracteres
    /// (<c>AuditAnchor.LongitudMaximaDeVersionDeClave</c>). La versión de la clave de anclas de producción tenía 21 y ninguna
    /// ancla se insertaba (22001), así que la cadena de auditoría no se selló desde el 2026-10-01. Sólo ensancha: no toca filas.
    /// </summary>
    public partial class VersionDeClaveDeAnclaMasLarga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "KeyVersion",
                schema: "dbo",
                table: "COR_AuditAnchors",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "KeyVersion",
                schema: "dbo",
                table: "COR_AuditAnchors",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);
        }
    }
}
