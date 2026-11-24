using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class VariasCuentasDeAportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_LND_MemberAccounts_LiveContribution",
                schema: "dbo",
                table: "LND_MemberAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "ProductNumber",
                schema: "dbo",
                table: "RCD_PayrollDeductionFileLines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ProductNumber",
                schema: "dbo",
                table: "RCD_PaymasterResponseLines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                schema: "dbo",
                table: "PAY_PayrollAccountDeductions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                schema: "dbo",
                table: "LND_MemberAccounts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProductNumber",
                schema: "dbo",
                table: "RCD_PayrollDeductionFileLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "ProductNumber",
                schema: "dbo",
                table: "RCD_PaymasterResponseLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                schema: "dbo",
                table: "PAY_PayrollAccountDeductions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                schema: "dbo",
                table: "LND_MemberAccounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccounts_LiveContribution",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "HolderPersonId",
                unique: true,
                filter: "\"Nature\" = 3 AND \"Status\" <> 6 AND \"IsDeleted\" = FALSE");
        }
    }
}
