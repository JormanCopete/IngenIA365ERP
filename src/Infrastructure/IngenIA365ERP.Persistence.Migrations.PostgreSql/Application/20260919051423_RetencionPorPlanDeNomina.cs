using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Pedido del dueño (2026-09-19): la tabla de retención de <c>/nomina/parametros-retencion</c> deja de
    /// colgar de la «empresa nómina» del legado y pasa a ser propia de un plan de nómina, y la liquidación
    /// la usa (reemplaza a <c>RETEFTE_TABLA_UVT</c> para el plan que trae tramos). Agrega
    /// <c>PayrollPlanId</c>, lleva los tramos existentes al plan por defecto de la cooperativa y cambia el
    /// índice único a (plan, desde, hasta). <c>PayrollCompanyId</c> se conserva (modelo heredado, siempre 1).
    /// No retira columnas.
    /// </summary>
    public partial class RetencionPorPlanDeNomina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PAY_WithholdingParameters_PayrollCompanyId_UvtRangeStart_Uv~",
                schema: "dbo",
                table: "PAY_WithholdingParameters");

            migrationBuilder.AddColumn<int>(
                name: "PayrollPlanId",
                schema: "dbo",
                table: "PAY_WithholdingParameters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Los tramos que ya existían (empresa nómina 1) pasan al plan por defecto de la cooperativa.
            migrationBuilder.Sql(@"UPDATE dbo.""PAY_WithholdingParameters"" SET ""PayrollPlanId"" = (SELECT ""Id"" FROM dbo.""PAY_PayrollPlans"" WHERE ""IsDefault"" AND NOT ""IsDeleted"" ORDER BY ""Id"" LIMIT 1) WHERE ""PayrollPlanId"" = 0;");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_WithholdingParameters_Plan_Range",
                schema: "dbo",
                table: "PAY_WithholdingParameters",
                columns: new[] { "PayrollPlanId", "UvtRangeStart", "UvtRangeEnd" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_WithholdingParameters_PAY_PayrollPlans_PayrollPlanId",
                schema: "dbo",
                table: "PAY_WithholdingParameters",
                column: "PayrollPlanId",
                principalSchema: "dbo",
                principalTable: "PAY_PayrollPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_WithholdingParameters_PAY_PayrollPlans_PayrollPlanId",
                schema: "dbo",
                table: "PAY_WithholdingParameters");

            migrationBuilder.DropIndex(
                name: "UK_PAY_WithholdingParameters_Plan_Range",
                schema: "dbo",
                table: "PAY_WithholdingParameters");

            migrationBuilder.DropColumn(
                name: "PayrollPlanId",
                schema: "dbo",
                table: "PAY_WithholdingParameters");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WithholdingParameters_PayrollCompanyId_UvtRangeStart_Uv~",
                schema: "dbo",
                table: "PAY_WithholdingParameters",
                columns: new[] { "PayrollCompanyId", "UvtRangeStart", "UvtRangeEnd" },
                unique: true);
        }
    }
}
