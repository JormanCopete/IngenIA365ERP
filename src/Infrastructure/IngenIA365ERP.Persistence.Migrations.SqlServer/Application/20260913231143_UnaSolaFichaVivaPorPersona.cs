using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 008 (FR-017): el reingreso de un empleado retirado es una ficha nueva; la retirada
    /// queda como historial de sus liquidaciones. <c>UK_PAY_Employees_PersonId</c> era único sin
    /// filtro —una sola ficha por persona, para siempre— y el reingreso, que la validación ya
    /// permitía (sólo mira fichas vivas), reventaba en la base con un 500. El índice pasa a ser
    /// único sólo entre fichas vivas (<c>Status &lt;&gt; -1</c> y no eliminadas). Reversible:
    /// <c>Down</c> vuelve al índice sin filtro, que sólo fallaría si ya hubiera dos fichas de una
    /// misma persona (y entonces hay que decidir cuál conservar, no correr Down a ciegas).
    /// </summary>
    public partial class UnaSolaFichaVivaPorPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_PAY_Employees_PersonId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_Employees_PersonId",
                schema: "dbo",
                table: "PAY_Employees",
                column: "PersonId",
                unique: true,
                filter: "[Status] <> -1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_PAY_Employees_PersonId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_Employees_PersonId",
                schema: "dbo",
                table: "PAY_Employees",
                column: "PersonId",
                unique: true);
        }
    }
}
