using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Motivo de retiro del empleado como texto libre de hasta 120 caracteres. La columna traía
    /// el <c>varchar(4)</c> del código de causa de SOLIDO, pero aquí nada lo interpreta como código:
    /// la pantalla lo pide libre y el detalle lo muestra tal cual, así que cualquier motivo real
    /// («Renuncia») reventaba con <c>22001</c> y la API respondía 500 (visto al escribir la e2e de
    /// la feature 008, P14). Ensanchar no pierde datos. <c>Down</c> vuelve a 4 y <b>sí</b> perdería
    /// lo escrito con más de 4 caracteres: antes de correrlo hay que respaldar o recortar a mano.
    /// </summary>
    public partial class MotivoDeRetiroComoTexto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TerminationCause",
                schema: "dbo",
                table: "PAY_Employees",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4)",
                oldMaxLength: 4,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TerminationCause",
                schema: "dbo",
                table: "PAY_Employees",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);
        }
    }
}
