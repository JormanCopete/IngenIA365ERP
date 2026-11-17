using System.Globalization;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Contabilidad;

// 2026-11-17 (pedido del dueño): las cifras de Contabilidad en el tablero de inicio. Todo saldo sale del libro por el punto único
// de lectura, MovimientosContables (asientos IsPosted, sin el cierre del ejercicio consultado, con el alcance de sucursal de quien
// consulta): no hay saldos guardados y aquí no se suma ACC_JournalEntries por otro camino.

/// <summary>
/// Ingresos y gastos por mes desde el libro. Ingresos = clase 4 (crédito − débito); gastos = clases 5, 6 y 7 (débito − crédito):
/// gastos, costos de venta y costos de producción. El resultado es la resta, igual que el estado de resultados.
/// </summary>
internal static class ResultadosDelLibro
{
    public sealed record DelMes(int Anio, int Mes, decimal Ingresos, decimal Gastos)
    {
        public decimal Resultado => Ingresos - Gastos;
    }

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    public static string NombreDelMes(int mes) => Colombia.DateTimeFormat.GetAbbreviatedMonthName(mes).TrimEnd('.');

    /// <summary>
    /// Los meses de <paramref name="desde"/> a <paramref name="hasta"/> (ambos el día 1 de su mes), en orden y sin huecos: un mes
    /// sin movimientos vale cero.
    /// </summary>
    public static async Task<IReadOnlyList<DelMes>> PorMesAsync(
        IApplicationDbContext db, IUserBranchScope alcance, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var fin = hasta.AddMonths(1).AddDays(-1);
        // El contexto se arma directo y no por MovimientosContables.PrepararAsync: ése resuelve filtros que llegan por la URL
        // (PublicId → Id, nombres para el encabezado) y valida el rango contra el reloj; aquí no hay filtros y el rango es fijo y
        // corto. Base y DelRango son los mismos que usan los informes: sólo IsPosted, sin el cierre del ejercicio consultado
        // (IncludeClosing = false, porque el CI del 31/12 deja los resultados en cero) y sin la apertura (saldo inicial, no
        // movimiento del mes), con el alcance de sucursal del usuario.
        var filtros = new FiltrosDeInforme { From = desde, To = fin };
        var contexto = new MovimientosContables.Contexto(filtros, desde, fin, await alcance.ObtenerAsync(ct),
            null, null, null, null, null, null, null, null);

        var filas = await MovimientosContables.DelRango(MovimientosContables.Base(db, contexto), contexto)
            .Where(e => e.Account!.Code.StartsWith("4") || e.Account!.Code.StartsWith("5")
                        || e.Account!.Code.StartsWith("6") || e.Account!.Code.StartsWith("7"))
            .GroupBy(e => new { e.Date.Year, e.Date.Month, Clase = e.Account!.Code.Substring(0, 1) })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Clase, Neto = g.Sum(e => e.Credit - e.Debit) })
            .ToListAsync(ct);

        var meses = new List<DelMes>();
        for (var m = desde; m <= hasta; m = m.AddMonths(1))
        {
            var delMes = filas.Where(f => f.Year == m.Year && f.Month == m.Month).ToList();
            var ingresos = delMes.Where(f => f.Clase == "4").Sum(f => f.Neto);
            var gastos = -delMes.Where(f => f.Clase != "4").Sum(f => f.Neto);
            meses.Add(new DelMes(m.Year, m.Month, ingresos, gastos));
        }
        return meses;
    }
}

/// <summary>Resultado del mes (ingresos − gastos − costos, clases 4 a 7) contra el mes anterior.</summary>
public sealed class ResultadoDelMes(IApplicationDbContext db, IUserBranchScope alcance) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "contabilidad.resultado-del-mes", "Resultado del mes", "Contabilidad", "Accounting.Reports.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/contabilidad/estados-financieros?estado=eri",
        "Ingresos menos gastos y costos (clases 4 a 7) contabilizados este mes en las sucursales que usted ve, comparado con el mes anterior.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var esteMes = new DateOnly(contexto.Hoy.Year, contexto.Hoy.Month, 1);
        var meses = await ResultadosDelLibro.PorMesAsync(db, alcance, esteMes.AddMonths(-1), esteMes, ct);
        var actual = meses[^1];
        return Result.Success(new ResultadoDeIndicador(
            actual.Resultado, meses[0].Resultado, "mes anterior",
            Nota: $"Ingresos {ResultadosDelLibro.Pesos(actual.Ingresos)} · gastos y costos {ResultadosDelLibro.Pesos(actual.Gastos)}"));
    }
}

/// <summary>Resultado de cada uno de los últimos seis meses (barras), con los ingresos y gastos del semestre en la nota.</summary>
public sealed class ResultadoDeLosUltimosSeisMeses(IApplicationDbContext db, IUserBranchScope alcance) : IIndicadorDelTablero
{
    public const int Meses = 6;

    public DefinicionDeIndicador Definicion { get; } = new(
        "contabilidad.resultado-seis-meses", "Ingresos y gastos, últimos 6 meses", "Contabilidad", "Accounting.Reports.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Serie, FormatoDeIndicador.Moneda, "/contabilidad/estados-financieros?estado=eri",
        "El resultado (ingresos menos gastos y costos) de cada uno de los últimos seis meses, incluido el actual; la cifra es el acumulado.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var esteMes = new DateOnly(contexto.Hoy.Year, contexto.Hoy.Month, 1);
        var meses = await ResultadosDelLibro.PorMesAsync(db, alcance, esteMes.AddMonths(-(Meses - 1)), esteMes, ct);
        var serie = meses.Select(m => new PuntoDeSerie(ResultadosDelLibro.NombreDelMes(m.Mes), m.Resultado)).ToList();
        return Result.Success(new ResultadoDeIndicador(
            meses.Sum(m => m.Resultado),
            Serie: serie,
            Nota: $"Ingresos {ResultadosDelLibro.Pesos(meses.Sum(m => m.Ingresos))} · gastos y costos {ResultadosDelLibro.Pesos(meses.Sum(m => m.Gastos))}"));
    }
}

/// <summary>
/// Comprobantes en borrador. «Sólo lo mío»: los que registró el usuario (<c>RegisteredByUserId</c>). Con alcance de sucursal se
/// cuentan los que tienen alguna línea en las sucursales permitidas, como la lista de Comprobantes.
/// </summary>
public sealed class ComprobantesEnBorrador(IApplicationDbContext db, IUserBranchScope alcance) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "contabilidad.borradores", "Comprobantes en borrador", "Contabilidad", "Accounting.Vouchers.View",
        AdmiteSoloLoMio: true, TipoDeIndicador.Cifra, FormatoDeIndicador.Numero, "/contabilidad/comprobantes",
        "Comprobantes guardados en borrador, todavía sin contabilizar: no cuentan en ningún saldo ni informe.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var borradores = await BorradoresContables.VisiblesAsync(db, alcance, ct);
        if (contexto.SoloLoMio) borradores = borradores.Where(d => d.RegisteredByUserId == contexto.UsuarioId);
        var cuenta = await borradores.GroupBy(d => d.TotalDebit > 0 && d.TotalDebit == d.TotalCredit)
            .Select(g => new { Cuadrado = g.Key, Cantidad = g.Count() }).ToListAsync(ct);
        var sinCuadrar = cuenta.Where(c => !c.Cuadrado).Sum(c => c.Cantidad);
        return Result.Success(new ResultadoDeIndicador(
            cuenta.Sum(c => c.Cantidad),
            Nota: sinCuadrar == 0 ? null : sinCuadrar == 1 ? "1 sin cuadrar" : $"{sinCuadrar} sin cuadrar"));
    }
}

/// <summary>Los borradores contables que el usuario puede ver (la regla de alcance de <c>ListDocumentsQueryHandler</c>).</summary>
internal static class BorradoresContables
{
    public static async Task<IQueryable<Domain.Entities.Accounting.Transactions.AccountingDocument>> VisiblesAsync(
        IApplicationDbContext db, IUserBranchScope alcance, CancellationToken ct)
    {
        var q = db.AccountingDocuments.AsNoTracking().Where(d => !d.IsDeleted && d.Status == DocumentStatus.Draft);
        var permitido = await alcance.ObtenerAsync(ct);
        if (permitido.Restringido)
        {
            var sucursales = permitido.Sucursales.ToList();
            q = q.Where(d => d.Lines.Any(l => !l.IsDeleted && sucursales.Contains(l.BranchId)));
        }
        return q;
    }
}
