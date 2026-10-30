using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CarteraYCaptacionesConPartidaCitada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankItemPublicId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankItemPublicId",
                schema: "dbo",
                table: "LND_LoanOperations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_BankItemPublicId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "BankItemPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_BankItemPublicId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "BankItemPublicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LND_MemberAccountOperations_BankItemPublicId",
                schema: "dbo",
                table: "LND_MemberAccountOperations");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanOperations_BankItemPublicId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropColumn(
                name: "BankItemPublicId",
                schema: "dbo",
                table: "LND_MemberAccountOperations");

            migrationBuilder.DropColumn(
                name: "BankItemPublicId",
                schema: "dbo",
                table: "LND_LoanOperations");
        }
    }
}
