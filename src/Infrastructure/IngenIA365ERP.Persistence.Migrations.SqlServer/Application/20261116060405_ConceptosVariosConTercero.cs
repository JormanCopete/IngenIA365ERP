using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class ConceptosVariosConTercero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultThirdPartyPersonId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedAmount",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThirdPartyMode",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_MiscConcepts_DefaultThirdPartyPersonId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                column: "DefaultThirdPartyPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_RCD_MiscConcepts_COR_People_DefaultThirdPartyPersonId",
                schema: "dbo",
                table: "RCD_MiscConcepts",
                column: "DefaultThirdPartyPersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RCD_MiscConcepts_COR_People_DefaultThirdPartyPersonId",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropIndex(
                name: "IX_RCD_MiscConcepts_DefaultThirdPartyPersonId",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropColumn(
                name: "DefaultThirdPartyPersonId",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropColumn(
                name: "SuggestedAmount",
                schema: "dbo",
                table: "RCD_MiscConcepts");

            migrationBuilder.DropColumn(
                name: "ThirdPartyMode",
                schema: "dbo",
                table: "RCD_MiscConcepts");
        }
    }
}
