using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class PuenteConIdentidadCentral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CentralUserId",
                schema: "dbo",
                table: "SEC_Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CentralUserPublicEmail",
                schema: "dbo",
                table: "SEC_Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SEC_Users_CentralUserId",
                schema: "dbo",
                table: "SEC_Users",
                column: "CentralUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SEC_Users_CentralUserId",
                schema: "dbo",
                table: "SEC_Users");

            migrationBuilder.DropColumn(
                name: "CentralUserId",
                schema: "dbo",
                table: "SEC_Users");

            migrationBuilder.DropColumn(
                name: "CentralUserPublicEmail",
                schema: "dbo",
                table: "SEC_Users");
        }
    }
}
