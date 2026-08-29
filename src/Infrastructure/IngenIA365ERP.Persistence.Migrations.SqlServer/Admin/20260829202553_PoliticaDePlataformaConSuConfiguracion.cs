using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
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
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AllowedMethodsMask",
                schema: "dbo",
                table: "ADM_PlatformMfaPolicy",
                type: "int",
                nullable: false,
                defaultValue: 3,
                oldClrType: typeof(int),
                oldType: "int");

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
                filter: "[IsDeleted] = 0");
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
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedBy",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AllowedMethodsMask",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 3);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlatformMfaPolicies",
                schema: "dbo",
                table: "PlatformMfaPolicies",
                column: "Id");
        }
    }
}
