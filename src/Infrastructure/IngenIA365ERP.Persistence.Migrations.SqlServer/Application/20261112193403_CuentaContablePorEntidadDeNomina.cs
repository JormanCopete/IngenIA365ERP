using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CuentaContablePorEntidadDeNomina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_PensionProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WorkRiskProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                column: "PayrollAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SeveranceProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                column: "PayrollAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PensionProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_PensionProviders",
                column: "PayrollAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_HealthInsuranceProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                column: "PayrollAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_FamilyCompensationFunds_PayrollAccountId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "PayrollAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_FamilyCompensationFunds_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "PayrollAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_HealthInsuranceProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                column: "PayrollAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PensionProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_PensionProviders",
                column: "PayrollAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_SeveranceProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                column: "PayrollAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_WorkRiskProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                column: "PayrollAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_FamilyCompensationFunds_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_HealthInsuranceProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PensionProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_SeveranceProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_WorkRiskProviders_ACC_ChartOfAccounts_PayrollAccountId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_WorkRiskProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_SeveranceProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PensionProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_HealthInsuranceProviders_PayrollAccountId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_FamilyCompensationFunds_PayrollAccountId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");

            migrationBuilder.DropColumn(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropColumn(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropColumn(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropColumn(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropColumn(
                name: "PayrollAccountId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");
        }
    }
}
