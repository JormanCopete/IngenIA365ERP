using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 014 — Cartera financiera, captaciones: la migración par de los aportes (K7; research C08; tasks.md T012, T056). Hoy
    /// lleva la naturaleza de la línea común (<c>LND_CreditLines.Nature</c>, defecto colocación para todas las existentes) con
    /// <c>IX_LND_CreditLines_Nature_Status</c> y <c>UK_LND_CreditLines_ActiveContribution</c> (una sola línea de aportes activa).
    /// <b>Regenerable hasta el congelamiento de la fase final</b> (T056): las tareas fundacionales y de US1/US2/US3/US10 le suman sus
    /// tablas regenerándola, nunca editándola a mano. Aditiva.
    /// </summary>
    public partial class CaptacionesAportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Nature",
                schema: "dbo",
                table: "LND_CreditLines",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLines_Nature_Status",
                schema: "dbo",
                table: "LND_CreditLines",
                columns: new[] { "Nature", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLines_ActiveContribution",
                schema: "dbo",
                table: "LND_CreditLines",
                column: "Nature",
                unique: true,
                filter: "\"Nature\" = 3 AND \"Status\" = 2 AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LND_CreditLines_Nature_Status",
                schema: "dbo",
                table: "LND_CreditLines");

            migrationBuilder.DropIndex(
                name: "UK_LND_CreditLines_ActiveContribution",
                schema: "dbo",
                table: "LND_CreditLines");

            migrationBuilder.DropColumn(
                name: "Nature",
                schema: "dbo",
                table: "LND_CreditLines");
        }
    }
}
