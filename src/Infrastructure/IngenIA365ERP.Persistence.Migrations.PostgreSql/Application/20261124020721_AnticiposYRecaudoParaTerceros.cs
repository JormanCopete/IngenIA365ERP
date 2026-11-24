using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class AnticiposYRecaudoParaTerceros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MiscConceptId",
                schema: "dbo",
                table: "RCD_PayerCreditBalances",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdvanceMeansId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CollectedForThirdParty",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdvance",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PayerCreditBalances_MiscConceptId",
                schema: "dbo",
                table: "RCD_PayerCreditBalances",
                column: "MiscConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_MiscConcepts_AdvanceMeansId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                column: "AdvanceMeansId");

            migrationBuilder.AddForeignKey(
                name: "FK_RCD_MiscConcepts_COR_PaymentMeans_AdvanceMeansId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                column: "AdvanceMeansId",
                principalSchema: "dbo",
                principalTable: "COR_PaymentMeans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RCD_PayerCreditBalances_RCD_MiscConcepts_MiscConceptId",
                schema: "dbo",
                table: "RCD_PayerCreditBalances",
                column: "MiscConceptId",
                principalSchema: "dbo",
                principalTable: "RCD_MiscConcepts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RCD_MiscConcepts_COR_PaymentMeans_AdvanceMeansId",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropForeignKey(
                name: "FK_RCD_PayerCreditBalances_RCD_MiscConcepts_MiscConceptId",
                schema: "dbo",
                table: "RCD_PayerCreditBalances");

            migrationBuilder.DropIndex(
                name: "IX_RCD_PayerCreditBalances_MiscConceptId",
                schema: "dbo",
                table: "RCD_PayerCreditBalances");

            migrationBuilder.DropIndex(
                name: "IX_RCD_MiscConcepts_AdvanceMeansId",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropColumn(
                name: "MiscConceptId",
                schema: "dbo",
                table: "RCD_PayerCreditBalances");

            migrationBuilder.DropColumn(
                name: "AdvanceMeansId",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropColumn(
                name: "CollectedForThirdParty",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropColumn(
                name: "IsAdvance",
                schema: "dbo",
                table: "RCD_MiscConcepts");
        }
    }
}
