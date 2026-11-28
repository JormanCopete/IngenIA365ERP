using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// El <b>balance de prueba detallado</b> y los <b>saldos por cuenta</b> (2026-11-27, pedido del dueño para la implantación): el balance de prueba con cada
/// cuenta de movimiento abierta, debajo y en plano, por todo lo que lleva su información —tercero, documento cruce (facturas, cuentas por cobrar) y
/// centro de costo—, con saldo inicial, débitos, créditos y saldo final en cada renglón. Cada renglón de detalle es una combinación que tiene movimiento
/// o saldo; lo que la cuenta no maneja va vacío.
/// <list type="bullet">
/// <item><c>trial-balance-detailed</c>: el árbol del plan (clase → … → auxiliar) y el detalle debajo de cada auxiliar;</item>
/// <item><c>account-balances</c>: sólo las cuentas de movimiento de la cuenta o el rango pedido (la cuenta es obligatoria), cada una con su detalle.</item>
/// </list>
/// Lee el libro por <see cref="MovimientosContables"/> (sólo contabilizado, alcance de sucursal, todos los filtros, la apertura como saldo inicial) y
/// el detalle con dos consultas agrupadas. Un clic en una fila abre el libro auxiliar en ese tercero o documento (<c>_nodo</c>). La exportación se
/// audita como las demás vistas contables. (nuevo)
/// </summary>
public sealed record DetailedTrialBalanceQuery(FiltrosDeInforme Filtros, bool SoloCuentasDeMovimiento) : IRequest<Result<TablaExportable>>;

public sealed class DetailedTrialBalanceQueryValidator : AbstractValidator<DetailedTrialBalanceQuery>
{
    public DetailedTrialBalanceQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeLibrosValidator());
}

public sealed class DetailedTrialBalanceQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<DetailedTrialBalanceQuery, Result<TablaExportable>>
{
    public const string VistaDetallada = "trial-balance-detailed";
    public const string VistaPorCuenta = "account-balances";

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Código"), new("Nombre de la cuenta"), new("Documento del tercero"), new("Tercero"), new("Documento cruce"), new("Centro de costo"),
        new("Saldo inicial", TipoDeColumna.Moneda), new("Débitos", TipoDeColumna.Moneda), new("Créditos", TipoDeColumna.Moneda),
        new("Saldo final", TipoDeColumna.Moneda),
        new("Nodo", Clave: LibrosQueries.ClaveNodo), new("Cuenta", Clave: LibrosQueries.ClaveCuenta),
    ];

    private sealed record Clave(int CuentaId, int? PersonaId, int? CruceId, string? NumeroCruce, int? CentroId);

    public async Task<Result<TablaExportable>> Handle(DetailedTrialBalanceQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        if (request.SoloCuentasDeMovimiento && f.AccountPublicId is null && string.IsNullOrWhiteSpace(f.AccountFrom))
            return Result.Failure<TablaExportable>(MovimientosContables.CuentaRequerida);

        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, f, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var porMovimiento = await LibrosQueries.SumasPorCuentaAsync(db, c, ct);
        var detalle = await DetalleAsync(c, ct);
        var nombres = await NombresAsync(detalle.Keys, ct);

        var filas = new List<FilaExportable>();
        if (request.SoloCuentasDeMovimiento)
        {
            foreach (var cuenta in plan.Cuentas.Where(x => !plan.TieneHijos(x.Id)).OrderBy(x => x.Code, StringComparer.Ordinal))
            {
                if (!porMovimiento.TryGetValue(cuenta.Id, out var s) || s.EsCero) continue;
                filas.Add(FilaDeCuenta(cuenta.Code, cuenta.Name, cuenta.Nature, s, cuenta.PublicId, $"{cuenta.Code} {cuenta.Name}", resaltada: true));
                filas.AddRange(Detalle(cuenta, detalle, nombres, $"{cuenta.Code} {cuenta.Name}"));
            }
        }
        else
        {
            var agregadas = plan.Agregar(porMovimiento);
            foreach (var cuenta in plan.Cuentas)
            {
                if (!agregadas.TryGetValue(cuenta.Id, out var s) || s.EsCero) continue;
                var seccion = LibrosQueries.SeccionDeClase(plan, cuenta);
                filas.Add(FilaDeCuenta(cuenta.Code, cuenta.Name, cuenta.Nature, s, cuenta.PublicId, seccion, resaltada: cuenta.Level == 1));
                if (!plan.TieneHijos(cuenta.Id)) filas.AddRange(Detalle(cuenta, detalle, nombres, seccion));
            }
        }

        // Lo que el libro tiene en cuentas que ya no están en el plan (eliminadas con saldo): al final, para que ningún peso se pierda.
        var fuera = porMovimiento.Where(x => plan.PorId(x.Key) is null && !x.Value.EsCero).Select(x => x.Key).ToList();
        if (fuera.Count > 0)
        {
            var eliminadas = await db.ChartOfAccounts.AsNoTracking().Where(a => fuera.Contains(a.Id)).Select(a => new { a.Id, a.PublicId, a.Code, a.Name, a.Nature }).ToListAsync(ct);
            foreach (var e in eliminadas.OrderBy(x => x.Code, StringComparer.Ordinal))
                filas.Add(FilaDeCuenta(e.Code, $"{e.Name} (eliminada)", e.Nature, porMovimiento[e.Id], e.PublicId, "Cuentas fuera del plan", resaltada: true));
        }

        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, f, c.PeriodoTexto, c, ct)).ToList();
        notas.Add("Debajo de cada cuenta de movimiento, un renglón por cada combinación de tercero, documento cruce y centro de costo con movimiento o saldo; "
                  + "lo que la cuenta no maneja va vacío. Los renglones de detalle suman lo de su cuenta.");
        notas.Add("Totales: suma de débitos y créditos del período; los saldos no se suman porque mezclan naturalezas.");

        var totales = new object?[Columnas.Count];
        totales[0] = "Totales";
        totales[7] = porMovimiento.Values.Sum(s => s.Debitos);
        totales[8] = porMovimiento.Values.Sum(s => s.Creditos);

        var vista = request.SoloCuentasDeMovimiento ? VistaPorCuenta : VistaDetallada;
        var titulo = request.SoloCuentasDeMovimiento ? "Saldos por cuenta" : "Balance de prueba detallado";
        var tabla = new TablaExportable(titulo, c.PeriodoTexto, Columnas, filas, new FilaExportable(totales, Resaltada: true), notas);
        if (f.EsExportacion) await audit.EmitirExportacionAsync(vista, f, f.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }

    private static FilaExportable FilaDeCuenta(string codigo, string nombre, AccountNature naturaleza, SumasDeCuenta s, Guid cuenta, string seccion, bool resaltada) =>
        new([codigo, nombre, null, null, null, null, s.SaldoInicial(naturaleza), s.Debitos, s.Creditos, s.SaldoFinal(naturaleza), LibrosQueries.Nodo(codigo), cuenta],
            seccion, resaltada);

    private sealed record Nombres(
        IReadOnlyDictionary<int, (Guid PublicId, string Nombre, string TaxId)> Terceros,
        IReadOnlyDictionary<int, string> Cruces,
        IReadOnlyDictionary<int, string> Centros);

    private static IEnumerable<FilaExportable> Detalle(JerarquiaDelPlan.Cuenta cuenta, Dictionary<Clave, SumasDeCuenta> detalle, Nombres nombres, string seccion)
    {
        var deLaCuenta = detalle.Where(x => x.Key.CuentaId == cuenta.Id && !x.Value.EsCero).ToList();
        // Una sola combinación sin ninguna dimensión repetiría la fila de la cuenta: no se abre.
        if (deLaCuenta.Count == 0 || (deLaCuenta.Count == 1 && deLaCuenta[0].Key is { PersonaId: null, CruceId: null, NumeroCruce: null, CentroId: null }))
            yield break;

        foreach (var (k, s) in deLaCuenta
                     .Select(x => (x.Key, x.Value, Tercero: x.Key.PersonaId is { } p && nombres.Terceros.TryGetValue(p, out var t) ? t.Nombre : null))
                     .OrderBy(x => x.Key.PersonaId is null ? 1 : 0).ThenBy(x => x.Tercero, StringComparer.CurrentCultureIgnoreCase)
                     .ThenBy(x => x.Key.NumeroCruce, StringComparer.Ordinal).ThenBy(x => x.Key.CentroId)
                     .Select(x => (x.Key, x.Value)))
        {
            var tercero = k.PersonaId is { } id && nombres.Terceros.TryGetValue(id, out var t) ? t : default;
            var (documento, nombre) = k.PersonaId is null ? (string.Empty, "Sin tercero") : tercero.Nombre is null ? (string.Empty, "(tercero no encontrado)") : (tercero.TaxId, tercero.Nombre);
            var tipoCruce = k.CruceId is { } cr ? nombres.Cruces.GetValueOrDefault(cr) : null;
            var cruce = LibrosQueries.Cruce(tipoCruce, k.NumeroCruce);
            var centro = k.CentroId is { } ce ? nombres.Centros.GetValueOrDefault(ce) ?? string.Empty : string.Empty;
            var personaNodo = k.PersonaId is null ? "none" : tercero.PublicId.ToString();
            var nodo = tipoCruce is null
                ? $"person:{cuenta.Code}|{personaNodo}"
                : $"document:{cuenta.Code}|{personaNodo}|{tipoCruce}|{k.NumeroCruce ?? "none"}";
            yield return new FilaExportable(
                [cuenta.Code, cuenta.Name, documento, nombre, cruce, centro, s.SaldoInicial(cuenta.Nature), s.Debitos, s.Creditos, s.SaldoFinal(cuenta.Nature), nodo, null],
                seccion);
        }
    }

    private async Task<Dictionary<Clave, SumasDeCuenta>> DetalleAsync(MovimientosContables.Contexto c, CancellationToken ct)
    {
        var q = MovimientosContables.Base(db, c);
        var periodo = await MovimientosContables.DelRango(q, c)
            .GroupBy(e => new { e.AccountId, e.PersonId, e.CrossDocumentTypeId, e.CrossDocumentNumber, e.CostCenterId })
            .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);
        var iniciales = await MovimientosContables.Iniciales(q, c)
            .GroupBy(e => new { e.AccountId, e.PersonId, e.CrossDocumentTypeId, e.CrossDocumentNumber, e.CostCenterId })
            .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);

        var sumas = new Dictionary<Clave, SumasDeCuenta>();
        foreach (var p in periodo)
        {
            var k = new Clave(p.Key.AccountId, p.Key.PersonId, p.Key.CrossDocumentTypeId, p.Key.CrossDocumentNumber, p.Key.CostCenterId);
            sumas[k] = sumas.GetValueOrDefault(k, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(0m, 0m, p.D, p.C));
        }
        foreach (var i in iniciales)
        {
            var k = new Clave(i.Key.AccountId, i.Key.PersonId, i.Key.CrossDocumentTypeId, i.Key.CrossDocumentNumber, i.Key.CostCenterId);
            sumas[k] = sumas.GetValueOrDefault(k, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));
        }
        return sumas;
    }

    private async Task<Nombres> NombresAsync(IEnumerable<Clave> claves, CancellationToken ct)
    {
        var lista = claves.ToList();
        var terceros = await LibrosQueries.TercerosAsync(db, lista.Select(k => k.PersonaId).OfType<int>().Distinct().ToList(), ct);
        var cruceIds = lista.Select(k => k.CruceId).OfType<int>().Distinct().ToList();
        var cruces = await db.CrossDocumentTypes.AsNoTracking().IgnoreQueryFilters().Where(x => cruceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, ct);
        var centroIds = lista.Select(k => k.CentroId).OfType<int>().Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking().IgnoreQueryFilters().Where(x => centroIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.LegacyCode) ? x.Name : $"{x.LegacyCode} {x.Name}", ct);
        return new Nombres(terceros, cruces, centros);
    }
}
