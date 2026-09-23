using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class AdjuntosDirectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                schema: "dbo",
                table: "COR_Attachments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                schema: "dbo",
                table: "COR_Attachments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Format",
                schema: "dbo",
                table: "COR_Attachments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "dbo",
                table: "COR_Attachments",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "dbo",
                table: "COR_Attachments",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadExpiresAt",
                schema: "dbo",
                table: "COR_Attachments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                schema: "dbo",
                table: "COR_Attachments");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                schema: "dbo",
                table: "COR_Attachments");

            migrationBuilder.DropColumn(
                name: "Format",
                schema: "dbo",
                table: "COR_Attachments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "dbo",
                table: "COR_Attachments");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "dbo",
                table: "COR_Attachments");

            migrationBuilder.DropColumn(
                name: "UploadExpiresAt",
                schema: "dbo",
                table: "COR_Attachments");
        }
    }
}
