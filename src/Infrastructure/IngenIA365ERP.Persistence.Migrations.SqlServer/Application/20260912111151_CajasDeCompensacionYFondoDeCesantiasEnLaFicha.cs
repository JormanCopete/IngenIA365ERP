using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Dos cosas de la ficha del empleado (2026-09-12):
    ///
    /// 1. <c>PAY_Employees.SeveranceFundId</c> pasa de <c>decimal(6,0)</c> a entero. La columna
    ///    venia del SOLIDO con ese tipo, nada la escribia ni la leia, y desde hoy referencia la
    ///    fila de <c>PAY_SeveranceProviders</c> como el resto de afiliaciones. El scaffolder lo
    ///    marca como posible perdida de datos por el estrechamiento de tipo; no la hay: es una
    ///    columna entera de seis digitos guardada como numeric, y en toda base viva vale 0.
    ///    <c>Down</c> devuelve el tipo original.
    /// 2. Nace el catalogo <c>PAY_FamilyCompensationFunds</c> (cajas de compensacion) al que
    ///    apunta <c>PAY_Employees.FamilySubsidyId</c>, que hasta hoy era un entero sin tabla.
    ///
    /// Principio XII: backup por cooperativa antes de aplicarla en produccion. DEV y QA la
    /// reciben por AutoMigrate; produccion por el Job PreSync (tres pasos).
    /// </summary>
    public partial class CajasDeCompensacionYFondoDeCesantiasEnLaFicha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SeveranceFundId",
                schema: "dbo",
                table: "PAY_Employees",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(6,0)",
                oldPrecision: 6);

            migrationBuilder.CreateTable(
                name: "PAY_FamilyCompensationFunds",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckDigit = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_FamilyCompensationFunds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_FamilyCompensationFunds_Code",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_FamilyCompensationFunds_PublicId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PAY_FamilyCompensationFunds",
                schema: "dbo");

            migrationBuilder.AlterColumn<decimal>(
                name: "SeveranceFundId",
                schema: "dbo",
                table: "PAY_Employees",
                type: "decimal(6,0)",
                precision: 6,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
