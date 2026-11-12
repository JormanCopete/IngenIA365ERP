using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class EmisionAsincrona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DirectPrint",
                schema: "dbo",
                table: "INV_CashRegisters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadAt",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeadAttemptLimit",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransientFailures",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeadAttemptLimit",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnqueuedAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpAttempts",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpDeadAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpDueAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpLastError",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransientFailures",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationMessageDeliveries_Dead",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries",
                column: "DeadAt",
                filter: "\"DeadAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_Dead",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "DeadAt",
                filter: "\"DeadAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_FollowUp",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "FollowUpDueAt",
                filter: "\"FollowUpDueAt\" IS NOT NULL AND \"FollowUpDeadAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_COR_IntegrationMessageDeliveries_Dead",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_COR_ElectronicDocuments_Dead",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropIndex(
                name: "IX_COR_ElectronicDocuments_FollowUp",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "DirectPrint",
                schema: "dbo",
                table: "INV_CashRegisters");

            migrationBuilder.DropColumn(
                name: "DeadAt",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries");

            migrationBuilder.DropColumn(
                name: "DeadAttemptLimit",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries");

            migrationBuilder.DropColumn(
                name: "TransientFailures",
                schema: "dbo",
                table: "COR_IntegrationMessageDeliveries");

            migrationBuilder.DropColumn(
                name: "DeadAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "DeadAttemptLimit",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "EnqueuedAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "FollowUpAttempts",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "FollowUpDeadAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "FollowUpDueAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "FollowUpLastError",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropColumn(
                name: "TransientFailures",
                schema: "dbo",
                table: "COR_ElectronicDocuments");
        }
    }
}
