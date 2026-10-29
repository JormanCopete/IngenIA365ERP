using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 015, T0306 (FR-223; data-model §25 #2; hallazgo I4): el retiro de los dobles de prueba de la 013 (<c>TST</c> recaudador y <c>TES</c> tesorería de
    /// prueba). <b>Sólo datos e idempotente</b>: cierra con <c>ValidTo</c> = la fecha del despliegue (o su <c>ValidFrom</c>, si fuera posterior) las filas
    /// vigentes de <c>COR_IntegrationParticipants</c> que son un doble —<c>IsTestDouble</c> o clase <c>Test</c> (5), el mismo criterio de
    /// <c>RegistroDeIntegracion.EsDoble</c>— y sus adopciones vigentes, como <c>EscritorDelRegistroDeIntegracion.RetirarAsync</c>.
    /// <c>IntegrationParticipant</c> es <c>IHechoInmutable</c>: <c>ValidTo</c> es su única columna escribible, así que <c>IsEnabled</c> y <c>Reason</c> no se tocan
    /// y no se borra nada. En producción no hay dobles (nunca se registraron allí) y no hace nada. El motivo —«retirado al desplegar Recaudo y Tesorería
    /// reales»— queda en esta migración: la cadena de sellos de la auditoría (<c>COR_AuditOutbox</c>) necesita el flujo de la cooperativa y el sello del
    /// reenviador, que una migración no tiene (desviación anotada en tasks.md, T0306). <c>Down()</c> no reabre los dobles: el código que los consumía ya no existe.
    /// </summary>
    public partial class RetiroDeLosDoblesDePrueba : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retiro de los dobles de prueba (T0306): sólo cierra vigencias; idempotente (ValidTo IS NULL).
            migrationBuilder.Sql(@"
UPDATE dbo.""COR_IntegrationParticipantAdoptions"" a
SET ""ValidTo"" = GREATEST(a.""ValidFrom"", CURRENT_DATE)
FROM dbo.""COR_IntegrationParticipants"" p
WHERE a.""ParticipantId"" = p.""Id""
  AND (p.""IsTestDouble"" = TRUE OR p.""Class"" = 5)
  AND p.""ValidTo"" IS NULL
  AND a.""ValidTo"" IS NULL;

UPDATE dbo.""COR_IntegrationParticipants""
SET ""ValidTo"" = GREATEST(""ValidFrom"", CURRENT_DATE)
WHERE (""IsTestDouble"" = TRUE OR ""Class"" = 5)
  AND ""ValidTo"" IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atrás: los dobles no se reabren (su código se retiró con T0307) y una vigencia cerrada no se reescribe.
        }
    }
}
