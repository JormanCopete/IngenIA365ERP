using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 005: la semántica de una tabla por rangos pasa a ser DATO del parámetro,
    /// no conocimiento del motor: <c>RangeUnitParameterCode</c> dice en qué unidad están
    /// los tramos (UVT, SMMLV o pesos) y <c>RangeIsMarginal</c> si la tarifa es marginal
    /// sobre el exceso (retención) o plana sobre toda la base (fondo de solidaridad).
    ///
    /// <para>
    /// Rellena las dos tablas de la semilla 2026 que ya existan sin unidad
    /// (<c>RETEFTE_TABLA_UVT</c> → UVT marginal; <c>FSP_TABLA</c> → SMMLV plana): la
    /// semilla nunca actualiza filas existentes, y sin este dato el motor las leería en
    /// pesos y liquidaría mal en silencio. Idempotente: sólo toca filas con la unidad
    /// en nulo. Reversible: el Down retira las columnas.
    /// </para>
    /// </summary>
    public partial class NominaTablasPorRangos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RangeIsMarginal",
                schema: "dbo",
                table: "PAY_LegalParameters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RangeUnitParameterCode",
                schema: "dbo",
                table: "PAY_LegalParameters",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "dbo"."PAY_LegalParameters"
                   SET "RangeUnitParameterCode" = 'UVT', "RangeIsMarginal" = TRUE
                 WHERE "Code" = 'RETEFTE_TABLA_UVT' AND "RangeUnitParameterCode" IS NULL;

                UPDATE "dbo"."PAY_LegalParameters"
                   SET "RangeUnitParameterCode" = 'SMMLV', "RangeIsMarginal" = FALSE
                 WHERE "Code" = 'FSP_TABLA' AND "RangeUnitParameterCode" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RangeIsMarginal",
                schema: "dbo",
                table: "PAY_LegalParameters");

            migrationBuilder.DropColumn(
                name: "RangeUnitParameterCode",
                schema: "dbo",
                table: "PAY_LegalParameters");
        }
    }
}
