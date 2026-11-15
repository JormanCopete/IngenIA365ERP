using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>line-balances</c> de <c>/api/reports/lending</c>: el resumen de saldos a una fecha, una fila por cuenta contable, línea y concepto, con
/// cuántas obligaciones hay y por cuánto, a favor y a cargo del asociado. Es agregada: no trae datos de deudores ni titulares. Sirve para cuadrar cada
/// implantación contra el informe del sistema anterior, así que funciona antes de activar.
/// <list type="bullet">
/// <item><b>Una fuente por naturaleza</b> (créditos, aportes, captaciones): activada, el libro hasta <c>asOf</c> por los mismos lectores de la
/// conciliación contable (<see cref="SaldosContablesDeCartera"/>, <see cref="SaldosContablesDeCaptaciones"/>); sin activar, la carga aplicada a su propio
/// corte (<see cref="CuadreDeMigracion.PartidasAsync"/>, <see cref="CuadreDeCaptaciones.Partidas"/>), que es lo que nacerá al activar. Por eso cada
/// naturaleza puede ir a una fecha distinta, y la nota lo dice.</item>
/// <item><b>La cuenta la responde Contabilidad</b> con la matriz vigente al corte de cada fuente: la misma combinación del cuadre. Sin regla la columna
/// queda vacía, la fila resaltada y la nota dice cuántas son. Sin contabilidad iniciada o sin respuesta, el informe sale igual, sin cuentas.</item>
/// <item><b>El total</b> deja fuera las cuentas de orden (suspenso y castigos), que van en su sección; cuenta cada obligación una vez aunque tenga varios
/// conceptos. El deterioro no es saldo de la obligación y no está.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record LineBalancesReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class LineBalancesReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    CargaVigenteDeCartera cargaDeCreditos,
    CuadreDeMigracion cuadre,
    SaldosContablesDeCartera libroDeCreditos,
    SaldosContablesDeCaptaciones libroDeCuentas,
    IContabilidadParaCartera contabilidad,
    IDateTimeService reloj) : IRequestHandler<LineBalancesReportQuery, Result<TablaExportable>>
{
    public const string Titulo = "Saldos por línea";
    public const string SeccionDeCreditos = "Créditos";
    public const string SeccionDeAportes = "Aportes";
    public const string SeccionDeCaptaciones = "Captaciones";
    public const string SeccionDeOrden = "Cuentas de orden";

    public const string NotaSinFuente = "No hay nada que mostrar: ninguna naturaleza está activada ni tiene una carga aplicada.";

    public const string NotaDeLectura =
        "El total y los conceptos cuentan cada obligación una vez. Las cuentas de orden (suspenso y castigos) van en su sección y fuera del total. El deterioro no es saldo de la obligación y no está.";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Lo que la vista declara al publicarse.</summary>
    public static readonly VistaDeInformeDeCartera Vista = new("line-balances", Titulo,
        "Saldos a una fecha por cuenta contable, línea y concepto, con el número de obligaciones. Antes de activar, sale de la carga aplicada.",
        "saldos-por-linea", ["asOf", "branch", "line"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Cuenta contable"),
        new("Nombre de la cuenta"),
        new("Línea"),
        new("Nombre de la línea"),
        new("Concepto"),
        new("No. a favor", TipoDeColumna.Entero),
        new("Saldo a favor", TipoDeColumna.Moneda),
        new("No. a cargo", TipoDeColumna.Entero),
        new("Saldo a cargo", TipoDeColumna.Moneda),
    ];

    private sealed record Partida(ClaveDeSaldoDeCartera Clave, string Obligacion, decimal Monto);

    private sealed record Fuente(LineNature Naturaleza, DateOnly Corte, IReadOnlyList<Partida> Partidas, string Nota, bool DeLaCarga);

    private sealed record CuentasDeLaClave(string Codigos, string Nombres);

    public async Task<Result<TablaExportable>> Handle(LineBalancesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var fuentes = await FuentesAsync(corte, alcance, ct);
        if (fuentes.Count == 0)
            return Result.Success(new TablaExportable(Titulo, $"Al {corte:yyyy-MM-dd}", Columnas, [], null, [NotaSinFuente]));

        var codigoDeLinea = f.Line is { } linea
            ? await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.PublicId == linea).Select(l => l.Code).FirstOrDefaultAsync(ct)
            : null;
        var lineas = await LineasAsync(fuentes, ct);
        var oficinas = await OficinasAsync(fuentes, ct);
        var notas = fuentes.Select(x => x.Nota).ToList();

        bool Entra(Fuente fuente, ClaveDeSaldoDeCartera c)
        {
            if (c.Rol.StartsWith("Deterioro", StringComparison.Ordinal)) return false;
            if (f.Branch is { } oficina && c.Sucursal != oficina) return false;
            if (f.Line is not null && !string.Equals(c.Linea, codigoDeLinea, StringComparison.OrdinalIgnoreCase)) return false;
            // Lo del libro ya viene con el alcance; la carga es de toda la cooperativa y aquí se recorta. Con alcance total no se mira nada: una línea o una
            // oficina que ya no se encuentre no puede sacar el saldo de un informe de cuadre. Con alcance parcial falla cerrado.
            if (!fuente.DeLaCarga) return true;
            var oficinaEntra = alcance.TodasLasOficinas || (oficinas.TryGetValue(c.Sucursal, out var o) && alcance.IncluyeOficina(o));
            var lineaEntra = alcance.TodasLasLineas || (c.Linea is { } codigo && lineas.TryGetValue(codigo, out var l) && alcance.IncluyeLinea(l.Id));
            return oficinaEntra && lineaEntra;
        }

        var saldos = new List<(Fuente Fuente, ClaveDeSaldoDeCartera Clave, string Obligacion, decimal Saldo)>();
        foreach (var fuente in fuentes)
        {
            saldos.AddRange(fuente.Partidas.Where(p => Entra(fuente, p.Clave))
                .GroupBy(p => (p.Clave, p.Obligacion))
                .Select(g => (fuente, g.Key.Clave, g.Key.Obligacion, Saldo: g.Sum(p => p.Monto)))
                .Where(x => x.Saldo != 0m));
        }

        var cuentas = new Dictionary<ClaveDeSaldoDeCartera, CuentasDeLaClave>();
        var sinContabilidad = new List<string>();
        foreach (var grupo in saldos.GroupBy(s => s.Fuente.Corte))
        {
            var r = await CuentasAsync(grupo.Key, grupo.Select(s => s.Clave).Distinct().ToList(), cuentas, ct);
            if (r is not null) sinContabilidad.Add(r);
        }

        var filas = saldos
            .GroupBy(s => (Seccion: Seccion(s.Clave), Cuenta: cuentas.GetValueOrDefault(s.Clave), Linea: lineas.GetValueOrDefault(s.Clave.Linea ?? string.Empty)?.Code ?? s.Clave.Linea,
                Concepto: Concepto(s.Clave)))
            .OrderBy(g => OrdenDe(g.Key.Seccion)).ThenBy(g => g.Key.Cuenta?.Codigos ?? "~", StringComparer.Ordinal)
            .ThenBy(g => g.Key.Linea, StringComparer.Ordinal).ThenBy(g => g.Key.Concepto, StringComparer.Ordinal)
            .Select(g =>
            {
                var aFavor = g.Where(x => AFavor(x.Clave)).ToList();
                var aCargo = g.Where(x => !AFavor(x.Clave)).ToList();
                return new FilaExportable(
                [
                    g.Key.Cuenta?.Codigos ?? string.Empty, g.Key.Cuenta?.Nombres ?? string.Empty, g.Key.Linea,
                    lineas.GetValueOrDefault(g.Key.Linea ?? string.Empty)?.Name ?? string.Empty, g.Key.Concepto,
                    aFavor.Select(x => x.Obligacion).Distinct(StringComparer.Ordinal).Count(), aFavor.Sum(x => x.Saldo),
                    aCargo.Select(x => x.Obligacion).Distinct(StringComparer.Ordinal).Count(), aCargo.Sum(x => x.Saldo),
                ], g.Key.Seccion, Resaltada: g.Key.Cuenta is null);
            })
            .ToList();

        var deBalance = saldos.Where(s => Seccion(s.Clave) != SeccionDeOrden).ToList();
        int Obligaciones(bool favor) => deBalance.Where(s => AFavor(s.Clave) == favor)
            .Select(s => (s.Fuente.Naturaleza, s.Obligacion)).Distinct().Count();
        var totales = new FilaExportable(["Total", null, null, null, null, Obligaciones(true), deBalance.Where(s => AFavor(s.Clave)).Sum(s => s.Saldo),
            Obligaciones(false), deBalance.Where(s => !AFavor(s.Clave)).Sum(s => s.Saldo)]);

        notas.Add(NotaDeLectura);
        if (saldos.Count > 0) notas.Add("Por concepto: " + PorConcepto(saldos.Select(s => (s.Clave, s.Obligacion, s.Saldo)).ToList()) + ".");
        notas.AddRange(sinContabilidad);
        var sinCuenta = filas.Count(x => x.Resaltada);
        if (sinContabilidad.Count == 0 && sinCuenta > 0)
            notas.Add($"{sinCuenta} {(sinCuenta == 1 ? "fila queda" : "filas quedan")} sin cuenta contable: ninguna regla de la matriz las resuelve al corte.");

        var fechas = fuentes.Select(x => x.Corte).Distinct().Order().Select(d => d.ToString("yyyy-MM-dd"));
        return Result.Success(new TablaExportable(Titulo, $"Al {string.Join(" y ", fechas)}", Columnas, filas, totales, notas));
    }

    // ---------------------------------------------------------------------------------------------------------- fuentes --

    private async Task<IReadOnlyList<Fuente>> FuentesAsync(DateOnly corte, AlcanceDeCartera alcance, CancellationToken ct)
    {
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var fuentes = new List<Fuente>(3);

        if (fechas.Activada(LineNature.Placement))
        {
            var movimientos = await libroDeCreditos.MovimientosAsync(corte, alcance, ct);
            fuentes.Add(DelLibro(LineNature.Placement, corte, movimientos));
        }
        else if (await cargaDeCreditos.AplicadaAsync(ct) is { } carga)
        {
            var partidas = await cuadre.PartidasAsync(carga, ct);
            fuentes.Add(new Fuente(LineNature.Placement, carga.CutoffDate, partidas.Select(p => new Partida(p.Clave, p.Obligacion, p.Monto)).ToList(),
                NotaDeLaCarga(SeccionDeCreditos, carga.Number, carga.CutoffDate), DeLaCarga: true));
        }

        IReadOnlyList<MovimientoDeCombinacionDeCartera>? deCuentas = null;
        foreach (var naturaleza in new[] { LineNature.Contribution, LineNature.Deposit })
        {
            if (fechas.Activada(naturaleza))
            {
                deCuentas ??= await libroDeCuentas.MovimientosAsync(corte, alcance, ct);
                fuentes.Add(DelLibro(naturaleza, corte, deCuentas.Where(m => m.Clave.Naturaleza == naturaleza.ToString()).ToList()));
                continue;
            }

            var carga = await db.MigrationLoads.AsNoTracking()
                .Where(l => l.Nature == naturaleza && l.Status == MigrationLoadStatus.Applied && !l.IsDeleted)
                .OrderByDescending(l => l.AppliedAt).FirstOrDefaultAsync(ct);
            if (carga is null) continue;
            var filas = await FilasDeLaCarga.LeerAsync(db, carga, ct);
            fuentes.Add(new Fuente(naturaleza, carga.CutoffDate, CuadreDeCaptaciones.Partidas(filas).Select(p => new Partida(p.Clave, p.Obligacion, p.Monto)).ToList(),
                NotaDeLaCarga(naturaleza == LineNature.Contribution ? SeccionDeAportes : SeccionDeCaptaciones, carga.Number, carga.CutoffDate), DeLaCarga: true));
        }
        return fuentes;
    }

    private static Fuente DelLibro(LineNature naturaleza, DateOnly corte, IReadOnlyList<MovimientoDeCombinacionDeCartera> movimientos) =>
        new(naturaleza, corte, movimientos.Select(m => new Partida(m.Clave, m.LoanId.ToString(CultureInfo.InvariantCulture), m.Monto)).ToList(),
            $"Del libro: {NombreDe(naturaleza)} al {corte:yyyy-MM-dd}, por fecha contable.", DeLaCarga: false);

    private static string NotaDeLaCarga(string naturaleza, int numero, DateOnly corte) =>
        $"De la carga {numero}: {naturaleza.ToLowerInvariant()} todavía sin activar, al corte de la carga ({corte:yyyy-MM-dd}), sea cual sea la fecha pedida.";

    private static string NombreDe(LineNature naturaleza) => naturaleza switch
    {
        LineNature.Contribution => SeccionDeAportes.ToLowerInvariant(),
        LineNature.Deposit => SeccionDeCaptaciones.ToLowerInvariant(),
        _ => SeccionDeCreditos.ToLowerInvariant(),
    };

    // ---------------------------------------------------------------------------------------------------- referencias --

    private sealed record LineaDelInforme(int Id, string Code, string Name);

    private async Task<IReadOnlyDictionary<string, LineaDelInforme>> LineasAsync(IReadOnlyList<Fuente> fuentes, CancellationToken ct)
    {
        var codigos = fuentes.SelectMany(x => x.Partidas).Select(p => p.Clave.Linea).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var filas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => codigos.Contains(l.Code))
            .Select(l => new LineaDelInforme(l.Id, l.Code, l.Name)).ToListAsync(ct);
        return filas.GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<Guid, int>> OficinasAsync(IReadOnlyList<Fuente> fuentes, CancellationToken ct)
    {
        var ids = fuentes.SelectMany(x => x.Partidas).Select(p => p.Clave.Sucursal).Distinct().ToList();
        return await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => ids.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
    }

    /// <summary>Llena las cuentas de cada clave. Devuelve la nota si Contabilidad no pudo responder; el informe sale igual, sin cuentas.</summary>
    private async Task<string?> CuentasAsync(DateOnly corte, IReadOnlyList<ClaveDeSaldoDeCartera> claves, Dictionary<ClaveDeSaldoDeCartera, CuentasDeLaClave> cuentas,
        CancellationToken ct)
    {
        var r = await contabilidad.SaldosDeCuentasMapeadasAsync(corte, claves.Select(c => c.ComoCombinacion()).ToList(), ct);
        if (r.IsFailure)
            return r.Error.Code == "Accounting.NotInitialized"
                ? "La contabilidad no está iniciada: la cuenta contable queda vacía."
                : $"Contabilidad no respondió ({r.Error.Code}): la cuenta contable queda vacía.";

        foreach (var conjunto in r.Value.Sets)
        {
            var delConjunto = conjunto.Accounts.OrderBy(a => a.AccountCode, StringComparer.Ordinal).ToList();
            var valor = new CuentasDeLaClave(string.Join(", ", delConjunto.Select(a => a.AccountCode)), string.Join(", ", delConjunto.Select(a => a.AccountName)));
            foreach (var i in conjunto.Combinations) cuentas[claves[i]] = valor;
        }
        return null;
    }

    // ------------------------------------------------------------------------------------------------------- presentación --

    private static bool AFavor(ClaveDeSaldoDeCartera c) => SaldosContablesDeCartera.Signo(c.Rol) < 0;

    private static string Seccion(ClaveDeSaldoDeCartera c)
    {
        if (c.Rol is "SuspensoEnOrden" or "CastigadaEnOrden") return SeccionDeOrden;
        return c.Naturaleza switch
        {
            nameof(LineNature.Contribution) => SeccionDeAportes,
            nameof(LineNature.Deposit) => SeccionDeCaptaciones,
            _ => SeccionDeCreditos,
        };
    }

    private static int OrdenDe(string seccion) => seccion switch
    {
        SeccionDeCreditos => 1,
        SeccionDeAportes => 2,
        SeccionDeCaptaciones => 3,
        _ => 4,
    };

    private static string Concepto(ClaveDeSaldoDeCartera c) => c.Rol switch
    {
        "Capital" => TextosDeInformesDeCartera.Concepto(LoanConcept.Principal),
        "Anticipo" => TextosDeInformesDeCartera.Concepto(LoanConcept.Advance),
        "SaldoAFavor" => TextosDeInformesDeCartera.Concepto(LoanConcept.CreditBalance),
        "CastigadaEnOrden" => $"{ConceptoDelLibro(c)} (castigada)",
        SaldosContablesDeCaptaciones.Capital => "Capital",
        SaldosContablesDeCaptaciones.CapitalInactiva => "Capital (cuenta inactiva)",
        SaldosContablesDeCaptaciones.InteresPorPagar => "Interés por pagar",
        SaldosContablesDeCaptaciones.CargoPorCobrar => "Cargo por cobrar",
        SaldosContablesDeCaptaciones.Aportes => c.ClaseDeAporte switch
        {
            nameof(ContributionClass.Minimum) => "Aportes mínimos",
            nameof(ContributionClass.Ordinary) => "Aportes ordinarios",
            nameof(ContributionClass.Extraordinary) => "Aportes extraordinarios",
            nameof(ContributionClass.Revalued) => "Aportes revalorizados",
            _ => "Aportes",
        },
        _ => ConceptoDelLibro(c),
    };

    private static string ConceptoDelLibro(ClaveDeSaldoDeCartera c) =>
        Enum.TryParse<LoanConcept>(c.Concepto, out var concepto) ? TextosDeInformesDeCartera.Concepto(concepto) : c.Concepto ?? c.Rol;

    private static string PorConcepto(IReadOnlyList<(ClaveDeSaldoDeCartera Clave, string Obligacion, decimal Saldo)> saldos) =>
        string.Join(" · ", saldos.GroupBy(s => (Seccion: Seccion(s.Clave), Concepto: Concepto(s.Clave)))
            .OrderBy(g => OrdenDe(g.Key.Seccion)).ThenBy(g => g.Key.Concepto, StringComparer.Ordinal)
            .Select(g => string.Create(Cultura, $"{g.Key.Concepto}: {g.Select(s => s.Obligacion).Distinct(StringComparer.Ordinal).Count():N0} por {g.Sum(s => s.Saldo):N2}")));
}
