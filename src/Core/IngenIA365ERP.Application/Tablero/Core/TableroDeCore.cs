using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Core;

// 2026-11-17: el primer indicador del tablero, de Core. Es también el molde para los de cada módulo: una clase por
// cifra, sólo lectura, el permiso con que se consulta la pantalla de la que sale y «sólo lo mío» por la columna que dice quién.

/// <summary>Personas registradas en el mes, contra el mes anterior. «Sólo lo mío»: las que registró el usuario.</summary>
public sealed class PersonasRegistradasEnElMes(IApplicationDbContext db) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "personas.registradas-del-mes", "Personas registradas en el mes", "Maestros", "Core.People.View",
        AdmiteSoloLoMio: true, TipoDeIndicador.Cifra, FormatoDeIndicador.Numero, "/maestros/personas",
        "Cuántas personas (asociados, clientes, proveedores, empleados…) se registraron este mes, comparado con el mes anterior.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var inicio = new DateTime(contexto.Hoy.Year, contexto.Hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var anterior = inicio.AddMonths(-1);
        var personas = db.People.AsNoTracking().Where(p => !p.IsDeleted && p.CreatedAt >= anterior);
        if (contexto.SoloLoMio) personas = personas.Where(p => p.CreatedBy == contexto.UsuarioDeAuditoria);

        var porMes = await personas.GroupBy(p => p.CreatedAt >= inicio).Select(g => new { EsteMes = g.Key, Cantidad = g.Count() }).ToListAsync(ct);
        return Result.Success(new ResultadoDeIndicador(
            porMes.Where(x => x.EsteMes).Sum(x => x.Cantidad),
            porMes.Where(x => !x.EsteMes).Sum(x => x.Cantidad),
            "mes anterior"));
    }
}
