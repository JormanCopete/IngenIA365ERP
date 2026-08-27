using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
{
    /// <inheritdoc />
    public partial class CredencialesWebAuthn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AaGuid",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttestationFormat",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CredentialId",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "varbinary(1023)",
                maxLength: 1023,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBackedUp",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBackupEligible",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PublicKeyCose",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "varbinary(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SignCount",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transports",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_ADM_MfaCredentials_CredentialId",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                column: "CredentialId",
                unique: true,
                filter: "[CredentialId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ADM_MfaCredentials_CredentialId",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "AaGuid",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "AttestationFormat",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "CredentialId",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "IsBackedUp",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "IsBackupEligible",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "PublicKeyCose",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "SignCount",
                schema: "dbo",
                table: "ADM_MfaCredentials");

            migrationBuilder.DropColumn(
                name: "Transports",
                schema: "dbo",
                table: "ADM_MfaCredentials");
        }
    }
}
