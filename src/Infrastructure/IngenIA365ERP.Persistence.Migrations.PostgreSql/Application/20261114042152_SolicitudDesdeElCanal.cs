using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
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
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMemberPublicId",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntakeChannel",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DO $$ BEGIN IF EXISTS (SELECT 1 FROM dbo.""LND_CreditApplications"" WHERE ""CreatedByUserId"" IS NULL) THEN RAISE EXCEPTION 'Hay solicitudes de credito del canal digital sin usuario: la reversa de SolicitudDesdeElCanal las perderia.'; END IF; END $$;");

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
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
