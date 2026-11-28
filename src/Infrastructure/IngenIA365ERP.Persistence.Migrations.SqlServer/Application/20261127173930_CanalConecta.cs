using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CanalConecta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelEndpoint",
                schema: "dbo",
                table: "COR_ElectronicEmissionSettings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChannelTenantId",
                schema: "dbo",
                table: "COR_ElectronicEmissionSettings",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelEndpoint",
                schema: "dbo",
                table: "COR_ElectronicEmissionSettings");

            migrationBuilder.DropColumn(
                name: "ChannelTenantId",
                schema: "dbo",
                table: "COR_ElectronicEmissionSettings");
        }
    }
}
