using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 008 (FR-012): las banderas <c>IsEmployee</c>, <c>IsAssociate</c> e <c>IsSalesperson</c> de
    /// <c>COR_People</c> son el reflejo de una fila viva en <c>PAY_Employees</c> (Status ≠ -1),
    /// <c>COR_Associates</c> e <c>INV_Salespeople</c>, y desde esta feature las escribe únicamente el
    /// handler que crea o retira esa fila (Principio V). Hasta el 2026-09-13 <c>UpdatePersonCommand</c>
    /// sobrescribía las ocho banderas con lo que trajera la pantalla, así que las bases ya sembradas
    /// pueden tener banderas desalineadas de sus tablas hijas. Esta migración las recalcula: sólo
    /// DML, idempotente (sólo toca filas desalineadas), sin <c>DELETE</c>, sella
    /// <c>UpdatedBy = 'system:migration:008'</c>. Diagnóstico previo y posterior:
    /// <c>specs/008-alta-persona-un-paso/diagnostico-banderas.sql</c> (en PDN el 2026-09-13: 0 desalineadas).
    /// <c>Down</c> es no-op a propósito: el estado anterior no se guarda y no vale la pena restaurarlo
    /// —era el bug—; volver a correr <c>Up</c> es idempotente.
    /// </summary>
    public partial class ReconciliarBanderasDerivadasDePersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Empleado: encender donde hay ficha viva, apagar donde no la hay.
                UPDATE p SET IsEmployee = 1, UpdatedBy = 'system:migration:008', UpdatedAt = SYSUTCDATETIME()
                FROM dbo.COR_People p
                WHERE p.IsEmployee = 0 AND EXISTS (SELECT 1 FROM dbo.PAY_Employees e WHERE e.PersonId = p.Id AND e.IsDeleted = 0 AND e.[Status] <> -1);
                UPDATE p SET IsEmployee = 0, UpdatedBy = 'system:migration:008', UpdatedAt = SYSUTCDATETIME()
                FROM dbo.COR_People p
                WHERE p.IsEmployee = 1 AND NOT EXISTS (SELECT 1 FROM dbo.PAY_Employees e WHERE e.PersonId = p.Id AND e.IsDeleted = 0 AND e.[Status] <> -1);

                -- Asociado: la afiliación existe (retirada o no) hasta que se elimina.
                UPDATE p SET IsAssociate = 1, UpdatedBy = 'system:migration:008', UpdatedAt = SYSUTCDATETIME()
                FROM dbo.COR_People p
                WHERE p.IsAssociate = 0 AND EXISTS (SELECT 1 FROM dbo.COR_Associates a WHERE a.PersonId = p.Id AND a.IsDeleted = 0);
                UPDATE p SET IsAssociate = 0, UpdatedBy = 'system:migration:008', UpdatedAt = SYSUTCDATETIME()
                FROM dbo.COR_People p
                WHERE p.IsAssociate = 1 AND NOT EXISTS (SELECT 1 FROM dbo.COR_Associates a WHERE a.PersonId = p.Id AND a.IsDeleted = 0);

                -- Vendedor.
                UPDATE p SET IsSalesperson = 1, UpdatedBy = 'system:migration:008', UpdatedAt = SYSUTCDATETIME()
                FROM dbo.COR_People p
                WHERE p.IsSalesperson = 0 AND EXISTS (SELECT 1 FROM dbo.INV_Salespeople s WHERE s.PersonId = p.Id AND s.IsDeleted = 0);
                UPDATE p SET IsSalesperson = 0, UpdatedBy = 'system:migration:008', UpdatedAt = SYSUTCDATETIME()
                FROM dbo.COR_People p
                WHERE p.IsSalesperson = 1 AND NOT EXISTS (SELECT 1 FROM dbo.INV_Salespeople s WHERE s.PersonId = p.Id AND s.IsDeleted = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op a propósito: el estado anterior de las banderas no se guarda y era el defecto;
            // volver a ejecutar Up es idempotente. Ver el resumen de la clase.
        }
    }
}
