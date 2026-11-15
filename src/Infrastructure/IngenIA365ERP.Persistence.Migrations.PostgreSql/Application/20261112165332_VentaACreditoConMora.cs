using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class VentaACreditoConMora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArrearsAcknowledgedByUserId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArrearsAcknowledgedDays",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArrearsApprovalRequestPublicId",
                schema: "dbo",
                table: "INV_DocumentPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPayments_ArrearsApproval",
                schema: "dbo",
                table: "INV_DocumentPayments",
                column: "ArrearsApprovalRequestPublicId",
                filter: "\"ArrearsApprovalRequestPublicId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_INV_DocumentPayments_ArrearsApproval",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "ArrearsAcknowledgedByUserId",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "ArrearsAcknowledgedDays",
                schema: "dbo",
                table: "INV_DocumentPayments");

            migrationBuilder.DropColumn(
                name: "ArrearsApprovalRequestPublicId",
                schema: "dbo",
                table: "INV_DocumentPayments");
        }
    }
}
