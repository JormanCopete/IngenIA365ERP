using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Códigos de catálogo alfanuméricos (2026-09-12):
    ///
    /// 1. <c>Code</c> de EPS, ARL, pensiones, cesantías, cajas de compensación y causas de
    ///    retención pasa de entero a texto de 10. El scaffolder lo marca como posible pérdida
    ///    por el cambio de tipo; no la hay: cada número se convierte a su texto («1» → «1»)
    ///    y cabe en 10. <c>Down</c> vuelve a entero, que sólo funciona si todos los códigos
    ///    siguen siendo numéricos.
    /// 2. Índice único filtrado sobre <c>LegacyCode</c> en los dieciséis catálogos de Core
    ///    que ahora lo exponen como «Código»: único cuando existe, NULL permitido para lo
    ///    migrado del SOLIDO sin código. Si una base migrada trae códigos repetidos, esta
    ///    migración falla ahí y hay que limpiarlos antes.
    /// </summary>
    public partial class CodigosAlfanumericosEnCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "PAY_WithholdingCauses",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "PAY_PensionProviders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_COR_WithdrawalReasons_LegacyCode",
                schema: "dbo",
                table: "COR_WithdrawalReasons",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Sports_LegacyCode",
                schema: "dbo",
                table: "COR_Sports",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Sections_LegacyCode",
                schema: "dbo",
                table: "COR_Sections",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Relationships_LegacyCode",
                schema: "dbo",
                table: "COR_Relationships",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Professions_LegacyCode",
                schema: "dbo",
                table: "COR_Professions",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Positions_LegacyCode",
                schema: "dbo",
                table: "COR_Positions",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Entities_LegacyCode",
                schema: "dbo",
                table: "COR_Entities",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Diseases_LegacyCode",
                schema: "dbo",
                table: "COR_Diseases",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_CulturalActivities_LegacyCode",
                schema: "dbo",
                table: "COR_CulturalActivities",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_CostCenters_LegacyCode",
                schema: "dbo",
                table: "COR_CostCenters",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Companies_LegacyCode",
                schema: "dbo",
                table: "COR_Companies",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Committees_LegacyCode",
                schema: "dbo",
                table: "COR_Committees",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Cities_LegacyCode",
                schema: "dbo",
                table: "COR_Cities",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Branches_LegacyCode",
                schema: "dbo",
                table: "COR_Branches",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Banks_LegacyCode",
                schema: "dbo",
                table: "COR_Banks",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Agreements_LegacyCode",
                schema: "dbo",
                table: "COR_Agreements",
                column: "LegacyCode",
                unique: true,
                filter: "[LegacyCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_COR_WithdrawalReasons_LegacyCode",
                schema: "dbo",
                table: "COR_WithdrawalReasons");

            migrationBuilder.DropIndex(
                name: "IX_COR_Sports_LegacyCode",
                schema: "dbo",
                table: "COR_Sports");

            migrationBuilder.DropIndex(
                name: "IX_COR_Sections_LegacyCode",
                schema: "dbo",
                table: "COR_Sections");

            migrationBuilder.DropIndex(
                name: "IX_COR_Relationships_LegacyCode",
                schema: "dbo",
                table: "COR_Relationships");

            migrationBuilder.DropIndex(
                name: "IX_COR_Professions_LegacyCode",
                schema: "dbo",
                table: "COR_Professions");

            migrationBuilder.DropIndex(
                name: "IX_COR_Positions_LegacyCode",
                schema: "dbo",
                table: "COR_Positions");

            migrationBuilder.DropIndex(
                name: "IX_COR_Entities_LegacyCode",
                schema: "dbo",
                table: "COR_Entities");

            migrationBuilder.DropIndex(
                name: "IX_COR_Diseases_LegacyCode",
                schema: "dbo",
                table: "COR_Diseases");

            migrationBuilder.DropIndex(
                name: "IX_COR_CulturalActivities_LegacyCode",
                schema: "dbo",
                table: "COR_CulturalActivities");

            migrationBuilder.DropIndex(
                name: "IX_COR_CostCenters_LegacyCode",
                schema: "dbo",
                table: "COR_CostCenters");

            migrationBuilder.DropIndex(
                name: "IX_COR_Companies_LegacyCode",
                schema: "dbo",
                table: "COR_Companies");

            migrationBuilder.DropIndex(
                name: "IX_COR_Committees_LegacyCode",
                schema: "dbo",
                table: "COR_Committees");

            migrationBuilder.DropIndex(
                name: "IX_COR_Cities_LegacyCode",
                schema: "dbo",
                table: "COR_Cities");

            migrationBuilder.DropIndex(
                name: "IX_COR_Branches_LegacyCode",
                schema: "dbo",
                table: "COR_Branches");

            migrationBuilder.DropIndex(
                name: "IX_COR_Banks_LegacyCode",
                schema: "dbo",
                table: "COR_Banks");

            migrationBuilder.DropIndex(
                name: "IX_COR_Agreements_LegacyCode",
                schema: "dbo",
                table: "COR_Agreements");

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                schema: "dbo",
                table: "PAY_WithholdingCauses",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                schema: "dbo",
                table: "PAY_PensionProviders",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<int>(
                name: "Code",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10);
        }
    }
}
