using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 005: la corrida guarda, por empleado, cómo se formaron las bases
    /// (<c>BasesJson</c>) y lo que el motor se negó a calcular o dejó sin línea
    /// (<c>NotesJson</c>), para el detalle con explicación (FR-013). Los comprobantes de
    /// aprobación y reversión pasan a clave larga con navegación, para que la aprobación
    /// fije la referencia al comprobante NM en el mismo SaveChanges que lo crea (FR-023).
    /// Sin datos que rellenar: las tablas nacieron con esta feature y no tienen filas fuera
    /// de desarrollo.
    /// </summary>
    public partial class NominaDetalleDeCorrida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BasesJson",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotesJson",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "ReversalAccountingDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "AccountingDocumentId",
                principalSchema: "dbo",
                principalTable: "ACC_Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "ReversalAccountingDocumentId",
                principalSchema: "dbo",
                principalTable: "ACC_Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRuns_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRuns_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "BasesJson",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees");

            migrationBuilder.DropColumn(
                name: "NotesJson",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees");

            migrationBuilder.AlterColumn<int>(
                name: "ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
