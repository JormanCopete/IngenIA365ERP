using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 006 (2026-09-12). Sólo columnas nuevas con default; nada destructivo:
    /// <list type="bullet">
    /// <item><c>PAY_PayPeriods.SubPeriodNumber / ImputationYear / ImputationMonth</c>: número del
    /// período dentro del mes y mes al que se imputa. Los períodos existentes se rellenan por
    /// SQL desde <c>StartDate</c> con la misma regla de <c>PeriodCalendar.Proponer</c> según la
    /// periodicidad de su plan (quincena por día ≤15; mes = mes del inicio).</item>
    /// <item><c>PAY_RecurringNovelties.ApplyOn</c>: 0 = cada período (comportamiento anterior).</item>
    /// <item><c>PAY_PayrollRuns.DiscardedAt/By/Reason</c>: huella de «Descartar borrador».</item>
    /// </list>
    /// </summary>
    public partial class PeriodicidadesReglasYDescarte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplyOn",
                schema: "dbo",
                table: "PAY_RecurringNovelties",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscardReason",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscardedAt",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscardedBy",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ImputationMonth",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<short>(
                name: "ImputationYear",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<byte>(
                name: "SubPeriodNumber",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)1);

            // Datos: rellenar sub-período y mes de los períodos que ya existen.
            migrationBuilder.Sql("""
                UPDATE dbo."PAY_PayPeriods" p
                SET "ImputationYear" = EXTRACT(YEAR FROM p."StartDate")::smallint,
                    "ImputationMonth" = EXTRACT(MONTH FROM p."StartDate")::smallint,
                    "SubPeriodNumber" = CASE pl."Periodicity"
                        WHEN 15 THEN CASE WHEN EXTRACT(DAY FROM p."StartDate") <= 15 THEN 1 ELSE 2 END
                        ELSE 1 END
                FROM dbo."PAY_PayrollPlans" pl
                WHERE pl."Id" = p."PayrollPlanId" AND p."ImputationYear" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyOn",
                schema: "dbo",
                table: "PAY_RecurringNovelties");

            migrationBuilder.DropColumn(
                name: "DiscardReason",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "DiscardedAt",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "DiscardedBy",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ImputationMonth",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropColumn(
                name: "ImputationYear",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropColumn(
                name: "SubPeriodNumber",
                schema: "dbo",
                table: "PAY_PayPeriods");
        }
    }
}
