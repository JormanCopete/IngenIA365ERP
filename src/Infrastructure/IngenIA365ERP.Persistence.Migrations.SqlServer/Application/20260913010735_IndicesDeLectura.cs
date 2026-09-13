using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Rendimiento percibido, ronda 1 (2026-09-13). Sólo índices de lectura; ninguna
    /// columna ni dato cambia, y <c>Down</c> los retira:
    /// <list type="bullet">
    /// <item><c>IX_LND_DepositEntries_Account_Date (AccountNumber, EntryDate)</c> y
    /// <c>IX_LND_DepositEntries_PersonCode</c>: extracto y saldo de cuentas de ahorro
    /// (<c>SavingsAccountQueries</c>) recorrían la tabla entera de movimientos.</item>
    /// <item><c>IX_ACC_Documents_Date_Number (DocumentDate, DocumentNumber)</c>: el listado de
    /// comprobantes filtra por rango de fecha y ordena por fecha desc, número.</item>
    /// <item><c>IX_ACC_AccountBalances_Period_Account (PeriodYear, PeriodMonth, AccountId)</c>:
    /// saldos y estados financieros piden un (año, mes) para miles de cuentas a la vez.</item>
    /// <item><c>IX_ACC_JournalEntries_Account_Date (AccountId, TransactionDate)</c>: libro mayor,
    /// conciliación y certificados filtran por cuenta y fecha. Sustituye al índice simple
    /// <c>IX_ACC_JournalEntries_AccountId</c> (la clave foránea), que queda cubierto por la
    /// primera columna del compuesto; por eso EF lo retira.</item>
    /// </list>
    /// <c>CREATE INDEX</c> toma un bloqueo de escritura breve por tabla: se aplica con el Job
    /// PreSync en la ventana de despliegue, tras el <c>pg_dump</c> de rigor.
    /// </summary>
    public partial class IndicesDeLectura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ACC_JournalEntries_AccountId",
                schema: "dbo",
                table: "ACC_JournalEntries");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DepositEntries_Account_Date",
                schema: "dbo",
                table: "LND_DepositEntries",
                columns: new[] { "AccountNumber", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DepositEntries_PersonCode",
                schema: "dbo",
                table: "LND_DepositEntries",
                column: "PersonCode");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_Account_Date",
                schema: "dbo",
                table: "ACC_JournalEntries",
                columns: new[] { "AccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_Date_Number",
                schema: "dbo",
                table: "ACC_Documents",
                columns: new[] { "DocumentDate", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountBalances_Period_Account",
                schema: "dbo",
                table: "ACC_AccountBalances",
                columns: new[] { "PeriodYear", "PeriodMonth", "AccountId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LND_DepositEntries_Account_Date",
                schema: "dbo",
                table: "LND_DepositEntries");

            migrationBuilder.DropIndex(
                name: "IX_LND_DepositEntries_PersonCode",
                schema: "dbo",
                table: "LND_DepositEntries");

            migrationBuilder.DropIndex(
                name: "IX_ACC_JournalEntries_Account_Date",
                schema: "dbo",
                table: "ACC_JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_ACC_Documents_Date_Number",
                schema: "dbo",
                table: "ACC_Documents");

            migrationBuilder.DropIndex(
                name: "IX_ACC_AccountBalances_Period_Account",
                schema: "dbo",
                table: "ACC_AccountBalances");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_AccountId",
                schema: "dbo",
                table: "ACC_JournalEntries",
                column: "AccountId");
        }
    }
}
