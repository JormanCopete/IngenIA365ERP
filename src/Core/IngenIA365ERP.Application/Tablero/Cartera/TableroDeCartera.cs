using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Cartera;

// 2026-11-17 (pedido del dueño): las cifras de Cartera y captaciones en el tablero de inicio. Mismas definiciones que el tablero de Cartera
// (/cartera/tablero, Lending.Dashboard.View) y que las vistas del centro de informes, con el alcance de oficina y línea de quien mira; cada
// tarjeta calcula sólo lo suyo (el tablero de Cartera completo calcula además deterioro, recaudo, cosechas y promesas, que aquí sobran).

/// <summary>Saldo de capital de la cartera viva al día, contra el cierre del mes anterior (la ficha «saldo» del tablero de Cartera).</summary>
public sealed class SaldoDeCartera(IApplicationDbContext db, IAlcanceDeCartera alcance) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "cartera.saldo", "Saldo de cartera", "Cartera", "Lending.Dashboard.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/cartera/tablero",
        "Saldo de capital de los créditos vivos (sin castigados) de sus oficinas y líneas, comparado con el cierre del mes anterior.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var hoy = await PosicionesDelTablero.AlCorteAsync(db, alcance, contexto.Hoy, ct);
        var antes = await PosicionesDelTablero.AlCorteAsync(db, alcance, PosicionesDelTablero.FinDelMesAnterior(contexto.Hoy), ct);
        return Result.Success(new ResultadoDeIndicador(hoy.Sum(p => p.Capital), antes.Sum(p => p.Capital), "mes anterior",
            Nota: $"{hoy.Count} créditos"));
    }
}

/// <summary>
/// Cartera vencida sobre bruta (%) al día, contra el cierre del mes anterior (la ficha «calidad» del tablero de Cartera): capital con más de
/// <c>Informes.DiasCarteraVencida</c> días de mora sobre el capital total. La nota dice cuánto es en pesos.
/// </summary>
public sealed class CarteraVencida(IApplicationDbContext db, IAlcanceDeCartera alcance, ReglasDeParametrosDeCartera parametros) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "cartera.vencida", "Cartera vencida", "Cartera", "Lending.Dashboard.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Cifra, FormatoDeIndicador.Porcentaje, "/cartera/tablero",
        "Porcentaje de la cartera en mora (más de los días del parámetro Informes.DiasCarteraVencida) sobre el saldo total, y cuánto es en pesos.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var finDelMesAnterior = PosicionesDelTablero.FinDelMesAnterior(contexto.Hoy);
        var dias = await QualityReportQueryHandler.DiasDeVencidaAsync(parametros, contexto.Hoy, ct);
        var diasAnterior = await QualityReportQueryHandler.DiasDeVencidaAsync(parametros, finDelMesAnterior, ct);
        var hoy = await PosicionesDelTablero.AlCorteAsync(db, alcance, contexto.Hoy, ct);
        var antes = await PosicionesDelTablero.AlCorteAsync(db, alcance, finDelMesAnterior, ct);
        var vencida = hoy.Where(p => p.Dias > dias).Sum(p => p.Capital);
        return Result.Success(new ResultadoDeIndicador(
            QualityReportQueryHandler.Porcentaje(vencida, hoy.Sum(p => p.Capital)),
            QualityReportQueryHandler.Porcentaje(antes.Where(p => p.Dias > diasAnterior).Sum(p => p.Capital), antes.Sum(p => p.Capital)),
            "mes anterior",
            Nota: $"{PosicionesDelTablero.Pesos(vencida)} con más de {dias} días de mora"));
    }
}

/// <summary>
/// La cartera viva al día por rangos de días de mora (los de <c>Informes.RangosDeEdad</c>, los mismos de la vista «Cartera por edades»):
/// el saldo de capital de cada rango, sin datos de ningún deudor.
/// </summary>
public sealed class MoraPorRangos(IApplicationDbContext db, IAlcanceDeCartera alcance, ReglasDeParametrosDeCartera parametros) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "cartera.mora-por-rangos", "Cartera por días de mora", "Cartera", "Lending.Reports.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Serie, FormatoDeIndicador.Moneda, "/cartera/informes?vista=aging",
        "Saldo de capital de la cartera viva en cada rango de días de mora (al día, 1 a 30, 31 a 60…), según el parámetro Informes.RangosDeEdad.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.InformesRangosDeEdad, contexto.Hoy, ct: ct);
        var limites = (leido.IsSuccess ? AgingReportQueryHandler.Limites(leido.Value.Texto) : null)
                      ?? AgingReportQueryHandler.Limites(ParametrosDeCartera.Definiciones.First(d => d.Clave == ParametrosDeCartera.InformesRangosDeEdad).DefectoSeguro)!;
        var posiciones = await PosicionesDelTablero.AlCorteAsync(db, alcance, contexto.Hoy, ct);
        var porRango = posiciones.GroupBy(p => AgingReportQueryHandler.RangoDe(p.Dias, limites).Orden).ToDictionary(g => g.Key, g => g.Sum(p => p.Capital));
        var serie = AgingReportQueryHandler.Etiquetas(limites).Select((etiqueta, orden) => new PuntoDeSerie(etiqueta, porRango.GetValueOrDefault(orden))).ToList();
        return Result.Success(new ResultadoDeIndicador(posiciones.Sum(p => p.Capital), Serie: serie));
    }
}

/// <summary>
/// Desembolsos del mes sin reversar, contra el mismo tramo del mes anterior (la ficha «colocación del mes» del tablero de Cartera). «Solo lo
/// mío»: los que registró el usuario (<c>LND_LoanOperations.ActorUserId</c>, <c>SEC_Users.Id</c>).
/// </summary>
public sealed class DesembolsosDelMes(IApplicationDbContext db, IAlcanceDeCartera alcance) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "cartera.desembolsos-del-mes", "Desembolsos del mes", "Cartera", "Lending.Dashboard.View",
        AdmiteSoloLoMio: true, TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/cartera/tablero",
        "Lo desembolsado (sin reversar) del primero del mes a hoy, comparado con el mismo tramo del mes anterior.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var deAlcance = await alcance.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(deAlcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var inicio = new DateOnly(contexto.Hoy.Year, contexto.Hoy.Month, 1);
        var inicioAnterior = inicio.AddMonths(-1);
        var mismoDiaAnterior = contexto.Hoy.AddMonths(-1);
        var desembolsos = db.LoanOperations.AsNoTracking()
            .Where(o => creditos.Contains(o.LoanId) && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null
                        && o.ValueDate >= inicioAnterior && o.ValueDate <= contexto.Hoy);
        if (contexto.SoloLoMio) desembolsos = desembolsos.Where(o => o.ActorUserId != null && o.ActorUserId == contexto.UsuarioId);

        var filas = await desembolsos.Select(o => new { o.ValueDate, o.Amount }).ToListAsync(ct);
        var delMes = filas.Where(f => f.ValueDate >= inicio).ToList();
        return Result.Success(new ResultadoDeIndicador(
            delMes.Sum(f => f.Amount),
            filas.Where(f => f.ValueDate < inicio && f.ValueDate <= mismoDiaAnterior).Sum(f => f.Amount),
            "mes anterior",
            Nota: delMes.Count == 1 ? "1 desembolso" : $"{delMes.Count} desembolsos"));
    }
}

/// <summary>Saldo de aportes sociales al día (todas las clases), contra el cierre del mes anterior: la suma del libro de cuentas de aportes.</summary>
public sealed class SaldoDeAportes(IApplicationDbContext db, IAlcanceDeCartera alcance) : IIndicadorDelTablero
{
    public DefinicionDeIndicador Definicion { get; } = new(
        "captaciones.saldo-de-aportes", "Saldo de aportes", "Captaciones", "Lending.Accounts.Read",
        AdmiteSoloLoMio: false, TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/cartera/aportes",
        "Aportes sociales de los asociados de sus oficinas y líneas (mínimos, ordinarios, extraordinarios y revalorizados), comparado con el cierre del mes anterior.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToList();
        return Result.Success(new ResultadoDeIndicador(
            await PosicionesDelTablero.SaldoDeCuentasAsync(db, alcance, LineNature.Contribution, conceptos, contexto.Hoy, ct),
            await PosicionesDelTablero.SaldoDeCuentasAsync(db, alcance, LineNature.Contribution, conceptos, PosicionesDelTablero.FinDelMesAnterior(contexto.Hoy), ct),
            "mes anterior"));
    }
}

/// <summary>
/// Saldo de las cuentas de captación (ahorro a la vista, contractual, permanente y CDAT) al día —capital más interés por pagar, el total de la
/// vista «Saldos de captaciones»—, contra el cierre del mes anterior.
/// </summary>
public sealed class SaldoDeCaptaciones(IApplicationDbContext db, IAlcanceDeCartera alcance) : IIndicadorDelTablero
{
    private static readonly MemberAccountConcept[] Conceptos = [MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable];

    public DefinicionDeIndicador Definicion { get; } = new(
        "captaciones.saldo-de-ahorros", "Saldo de ahorros y CDAT", "Captaciones", "Lending.Reports.View",
        AdmiteSoloLoMio: false, TipoDeIndicador.Cifra, FormatoDeIndicador.Moneda, "/cartera/informes?vista=deposit-balances",
        "Saldo de las cuentas de captación (ahorro a la vista, contractual, permanente y CDAT) con su interés por pagar, comparado con el cierre del mes anterior.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        Result.Success(new ResultadoDeIndicador(
            await PosicionesDelTablero.SaldoDeCuentasAsync(db, alcance, LineNature.Deposit, Conceptos, contexto.Hoy, ct),
            await PosicionesDelTablero.SaldoDeCuentasAsync(db, alcance, LineNature.Deposit, Conceptos, PosicionesDelTablero.FinDelMesAnterior(contexto.Hoy), ct),
            "mes anterior"));
}
