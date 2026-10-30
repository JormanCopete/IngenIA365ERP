using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionId",
                schema: "dbo",
                table: "SEC_Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "dbo",
                table: "COR_People",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            // (1) Relleno idempotente: la misma regla de UsuarioDeDocumento.DerivarParaPersona. SQL
            //     Server no tiene expresiones regulares: el número se pasa a mayúsculas y se le quita,
            //     uno a uno, todo carácter fuera de [0-9A-Z] (intercalación binaria para que el rango
            //     sea el ASCII y no incluya minúsculas ni letras con tilde).
            migrationBuilder.Sql("""
                UPDATE [dbo].[COR_People] SET [Username] = UPPER(ISNULL([TaxId], N''));
                WHILE EXISTS (SELECT 1 FROM [dbo].[COR_People]
                              WHERE PATINDEX(N'%[^0-9A-Z]%', [Username] COLLATE Latin1_General_BIN) > 0)
                    UPDATE [dbo].[COR_People]
                       SET [Username] = STUFF([Username], PATINDEX(N'%[^0-9A-Z]%', [Username] COLLATE Latin1_General_BIN), 1, N'')
                     WHERE PATINDEX(N'%[^0-9A-Z]%', [Username] COLLATE Latin1_General_BIN) > 0;
                UPDATE [dbo].[COR_People] SET [Username] =
                    CASE UPPER(LTRIM(RTRIM(ISNULL([IdType], N''))))
                        WHEN N'C'   THEN N''
                        WHEN N'CC'  THEN N''
                        WHEN N'CE'  THEN N'CE'
                        WHEN N'E'   THEN N'CE'
                        WHEN N'PT'  THEN N'PPT'
                        WHEN N'PE'  THEN N'PEP'
                        WHEN N'TI'  THEN N'TI'
                        WHEN N'T'   THEN N'TI'
                        WHEN N'PA'  THEN N'PA'
                        WHEN N'P'   THEN N'PA'
                        WHEN N'RC'  THEN N'RC'
                        WHEN N'R'   THEN N'RC'
                        WHEN N'NI'  THEN N'NIT'
                        WHEN N'N'   THEN N'NIT'
                        WHEN N'NIT' THEN N'NIT'
                        WHEN N''    THEN N'SIN-'
                        ELSE UPPER(LTRIM(RTRIM([IdType]))) + N'-'
                    END + [Username];
                """);

            // (2) Guarda: dos personas que hoy son distintas sólo por puntos, espacios o guiones
            //     quedarían con el mismo usuario. No se corrige nada solo: se decide con la cooperativa.
            migrationBuilder.Sql("""
                DECLARE @repetidos int = (SELECT COUNT(*) FROM (
                    SELECT [Username] FROM [dbo].[COR_People] GROUP BY [Username] HAVING COUNT(*) > 1) d);
                IF @repetidos > 0
                BEGIN
                    DECLARE @mensaje nvarchar(600) = CONCAT(N'UsuarioDeLaPersona: ', @repetidos,
                        N' documento(s) quedan repetidos al normalizarlos (puntos, espacios o guiones de mas, o documento vacio). Revise el diagnostico B6 de specs/017-login-documento-invitacion/diagnostico-de-datos.md y corrijalos con la cooperativa antes de continuar.');
                    THROW 50017, @mensaje, 1;
                END
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
                IF EXISTS (SELECT [TaxId] FROM [dbo].[COR_People] GROUP BY [TaxId] HAVING COUNT(*) > 1)
                    THROW 50018, N'UsuarioDeLaPersona (reversa): ya conviven personas con el mismo numero y distinto tipo de documento; la unicidad por numero no se puede restaurar. Restaure el respaldo previo al despliegue.', 1;
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
