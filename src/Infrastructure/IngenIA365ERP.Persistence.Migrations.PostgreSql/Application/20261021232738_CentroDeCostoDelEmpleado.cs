using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CentroDeCostoDelEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCenterRefId",
                schema: "dbo",
                table: "PAY_Employees",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Employees_CostCenterRefId",
                schema: "dbo",
                table: "PAY_Employees",
                column: "CostCenterRefId");

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_Employees_COR_CostCenters_CostCenterRefId",
                schema: "dbo",
                table: "PAY_Employees",
                column: "CostCenterRefId",
                principalSchema: "dbo",
                principalTable: "COR_CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Puebla la FK desde el código heredado (PAY_Employees.CostCenterId = COR_CostCenters.LegacyCode, sin
            // distinguir mayúsculas), sólo fichas y centros no borrados; las vacías o sin centro quedan nulas. Si dos
            // centros comparten código sin distinguir mayúsculas gana el de menor Id. Aditiva: no toca el string viejo.
            migrationBuilder.Sql(@"
UPDATE ""dbo"".""PAY_Employees"" AS e
SET ""CostCenterRefId"" = (
    SELECT MIN(c.""Id"") FROM ""dbo"".""COR_CostCenters"" AS c
    WHERE c.""IsDeleted"" = FALSE AND c.""LegacyCode"" IS NOT NULL
      AND UPPER(TRIM(c.""LegacyCode"")) = UPPER(TRIM(e.""CostCenterId"")))
WHERE e.""IsDeleted"" = FALSE AND e.""CostCenterRefId"" IS NULL
  AND e.""CostCenterId"" IS NOT NULL AND TRIM(e.""CostCenterId"") <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_Employees_COR_CostCenters_CostCenterRefId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropIndex(
                name: "IX_PAY_Employees_CostCenterRefId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "CostCenterRefId",
                schema: "dbo",
                table: "PAY_Employees");
        }
    }
}
