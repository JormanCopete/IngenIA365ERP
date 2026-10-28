using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 017 (D-13, research R3 y R14): la persona de la cooperativa gana el usuario derivado de
    /// tipo y número, que pasa a ser su llave única en lugar del número solo; así conviven CC 123456
    /// («123456») y CE 123456 («CE123456»). Aditiva: <c>TaxId</c> no se reescribe. Se detiene si dos
    /// personas chocan al normalizar (diagnóstico B6). El relleno repite en SQL la regla de
    /// <c>UsuarioDeDocumento.DerivarParaPersona</c>; lo comprueba la e2e de migración.
    /// </summary>
    public partial class UsuarioDeLaPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CentralUsername",
                schema: "dbo",
                table: "SEC_Users",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionId",
                schema: "dbo",
                table: "SEC_Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "dbo",
                table: "COR_People",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            // (1) Relleno idempotente: la misma regla de UsuarioDeDocumento.DerivarParaPersona.
            migrationBuilder.Sql("""
                UPDATE dbo."COR_People" SET "Username" =
                    CASE upper(btrim("IdType"))
                        WHEN 'C'   THEN ''
                        WHEN 'CC'  THEN ''
                        WHEN 'CE'  THEN 'CE'
                        WHEN 'E'   THEN 'CE'
                        WHEN 'PT'  THEN 'PPT'
                        WHEN 'PE'  THEN 'PEP'
                        WHEN 'TI'  THEN 'TI'
                        WHEN 'T'   THEN 'TI'
                        WHEN 'PA'  THEN 'PA'
                        WHEN 'P'   THEN 'PA'
                        WHEN 'RC'  THEN 'RC'
                        WHEN 'R'   THEN 'RC'
                        WHEN 'NI'  THEN 'NIT'
                        WHEN 'N'   THEN 'NIT'
                        WHEN 'NIT' THEN 'NIT'
                        ELSE coalesce(nullif(regexp_replace(upper(coalesce("IdType", '')), '[^0-9A-Z]', '', 'g'), ''), 'SIN') || '-'
                    END
                    || regexp_replace(upper(coalesce("TaxId", '')), '[^0-9A-Z]', '', 'g');
                """);

            // (2) Guarda: dos personas que hoy son distintas sólo por puntos, espacios o guiones
            //     quedarían con el mismo usuario. No se corrige nada solo: se decide con la cooperativa.
            migrationBuilder.Sql("""
                DO $$
                DECLARE n integer;
                BEGIN
                    SELECT count(*) INTO n FROM (
                        SELECT "Username" FROM dbo."COR_People" GROUP BY "Username" HAVING count(*) > 1) d;
                    IF n > 0 THEN
                        RAISE EXCEPTION 'UsuarioDeLaPersona: % documento(s) quedan repetidos al normalizarlos (puntos, espacios o guiones de mas, o documento vacio). Revise el diagnostico B6 de specs/017-login-documento-invitacion/diagnostico-de-datos.md y corrijalos con la cooperativa antes de continuar.', n;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "UK_COR_People_Username",
                schema: "dbo",
                table: "COR_People",
                column: "Username",
                unique: true);

            // (3) Sólo con el índice nuevo puesto se retira el viejo: nunca hay un momento sin unicidad.
            migrationBuilder.DropIndex(
                name: "UK_COR_People_TaxId",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.CreateIndex(
                name: "IX_COR_People_TaxId",
                schema: "dbo",
                table: "COR_People",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_SEC_Users_PositionId",
                schema: "dbo",
                table: "SEC_Users",
                column: "PositionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SEC_Users_COR_Positions_PositionId",
                schema: "dbo",
                table: "SEC_Users",
                column: "PositionId",
                principalSchema: "dbo",
                principalTable: "COR_Positions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // La reversa vuelve a exigir el número solo único. Si ya conviven dos tipos con el mismo
            // número no puede: se detiene con un mensaje claro y la reversa real es el respaldo previo
            // (docs/operaciones/identidad-con-documento.md).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT "TaxId" FROM dbo."COR_People" GROUP BY "TaxId" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'UsuarioDeLaPersona (reversa): ya conviven personas con el mismo numero y distinto tipo de documento; la unicidad por numero no se puede restaurar. Restaure el respaldo previo al despliegue.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SEC_Users_COR_Positions_PositionId",
                schema: "dbo",
                table: "SEC_Users");

            migrationBuilder.DropIndex(
                name: "IX_SEC_Users_PositionId",
                schema: "dbo",
                table: "SEC_Users");

            migrationBuilder.DropIndex(
                name: "IX_COR_People_TaxId",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.CreateIndex(
                name: "UK_COR_People_TaxId",
                schema: "dbo",
                table: "COR_People",
                column: "TaxId",
                unique: true);

            migrationBuilder.DropIndex(
                name: "UK_COR_People_Username",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "CentralUsername",
                schema: "dbo",
                table: "SEC_Users");

            migrationBuilder.DropColumn(
                name: "PositionId",
                schema: "dbo",
                table: "SEC_Users");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "dbo",
                table: "COR_People");
        }
    }
}
