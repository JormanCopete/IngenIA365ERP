using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                type: "int",
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
UPDATE e
SET e.[CostCenterRefId] = (
    SELECT MIN(c.[Id]) FROM [dbo].[COR_CostCenters] AS c
    WHERE c.[IsDeleted] = 0 AND c.[LegacyCode] IS NOT NULL
      AND UPPER(LTRIM(RTRIM(c.[LegacyCode]))) = UPPER(LTRIM(RTRIM(e.[CostCenterId]))))
FROM [dbo].[PAY_Employees] AS e
WHERE e.[IsDeleted] = 0 AND e.[CostCenterRefId] IS NULL
  AND e.[CostCenterId] IS NOT NULL AND LTRIM(RTRIM(e.[CostCenterId])) <> '';");
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
