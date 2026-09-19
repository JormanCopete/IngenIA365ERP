using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 009 (aclaración del dueño del 2026-09-18). La longitud del código de una auxiliar deja de
    /// configurarse por empresa y pasa a ser una regla fija por nivel (nivel 5: 7 a 9 dígitos; nivel 6: 10 a
    /// 12), en <c>Application.Accounting.Accounts.LongitudDeAuxiliar</c>. Se retiran las dos columnas de
    /// configuración que ya nadie lee. No toca datos contables (sólo dos parámetros con valores 8 y 10 en las
    /// pocas empresas iniciadas en DEV/QA) y es <b>reversible</b>: <c>Down</c> vuelve a crear las columnas.
    /// </summary>
    /// <remarks>
    /// MIGRACION-DESTRUCTIVA-APROBADA (Principio XII): <c>DropColumn</c> de dos parámetros de configuración,
    /// sin datos transaccionales. Respaldo: el <c>pg_dump</c> previo al despliegue de cada ambiente (runbook);
    /// en DEV/QA los valores retirados eran 8 y 10 y no se necesitan de vuelta. Segundo revisor: Jorman Copete
    /// (dueño del producto), designado el 2026-09-19 al autorizar la promoción a producción (respaldos
    /// <c>*-20260919-pre-f008-f009.dump</c> en <c>/root/respaldos/</c> del nodo de PDN).
    /// </remarks>
    public partial class LongitudDeAuxiliarPorRango : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Level5Length",
                schema: "dbo",
                table: "ACC_AccountingSetups");

            migrationBuilder.DropColumn(
                name: "Level6Length",
                schema: "dbo",
                table: "ACC_AccountingSetups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Level5Length",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "Level6Length",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }
    }
}
