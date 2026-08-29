using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Admin
{
    /// <inheritdoc />
    public partial class PoliticaDePlataformaConSuConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PlatformMfaPolicies",
                schema: "dbo",
                table: "PlatformMfaPolicies");

            migrationBuilder.RenameTable(
                name: "PlatformMfaPolicies",
                schema: "dbo",
                newName: "ADM_PlatformMfaPolicy",
                newSchema: "dbo");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AllowedMethodsMask",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "integer",
                nullable: false,
                defaultValue: 3,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ADM_PlatformMfaPolicy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_PlatformMfaPolicy_PublicId",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ADM_PlatformMfaPolicy_Scope",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                column: "Scope",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ADM_PlatformMfaPolicy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy");

            migrationBuilder.DropIndex(
                name: "IX_ADM_PlatformMfaPolicy_PublicId",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy");

            migrationBuilder.DropIndex(
                name: "UX_ADM_PlatformMfaPolicy_Scope",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy");

            migrationBuilder.RenameTable(
                name: "ADM_PlatformMfaPolicy",
                schema: "dbo",
                newName: "PlatformMfaPolicies",
                newSchema: "dbo");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AllowedMethodsMask",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 3);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlatformMfaPolicies",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                column: "Id");
        }
    }
}
