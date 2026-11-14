using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 021, US13 (T141): la solicitud de credito que arma el asociado por el canal digital. Aditiva: <c>IntakeChannel</c> (1 = oficina, las de
    /// antes), <c>CreatedByMemberPublicId</c> y <c>CreatedByUserId</c> nulable. La reversa se niega si ya hay solicitudes del canal (sin usuario).
    /// </summary>
    public partial class SolicitudDesdeElCanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMemberPublicId",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntakeChannel",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM dbo.LND_CreditApplications WHERE CreatedByUserId IS NULL) THROW 50000, 'Hay solicitudes de credito del canal digital sin usuario: la reversa de SolicitudDesdeElCanal las perderia.', 1;");

            migrationBuilder.DropColumn(
                name: "CreatedByMemberPublicId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropColumn(
                name: "IntakeChannel",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
