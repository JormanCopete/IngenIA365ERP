using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CajaUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PosCashRegisterPublicId",
                schema: "dbo",
                table: "RCD_CashBoxes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedSessionPublicId",
                schema: "dbo",
                table: "INV_CashSessions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PosCashRegisterPublicId",
                schema: "dbo",
                table: "RCD_CashBoxes");

            migrationBuilder.DropColumn(
                name: "LinkedSessionPublicId",
                schema: "dbo",
                table: "INV_CashSessions");
        }
    }
}
