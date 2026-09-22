using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// El único punto por donde las consultas e informes leen el libro (feature 009 E2, FR-046):
/// asientos <c>IsPosted</c> de comprobantes contabilizados o reversados —la reversa deja el original
/// y un espejo, los dos cuentan y se netean—, nunca borradores; con el alcance de sucursal de quien
/// consulta (FR-035) y todos los filtros de <see cref="FiltrosDeInforme"/> aplicados a la vez.
///
/// <para>
/// Dos reglas de fechas que parecen detalle y no lo son: el comprobante de <b>cierre</b> del
/// ejercicio consultado sólo entra si se pide (<c>IncludeClosing</c>) —el de los ejercicios
/// anteriores entra siempre, porque es lo que deja los resultados en cero y el excedente en la
/// 35 al arrancar el año—, y las líneas del comprobante de <b>apertura</b> son
/// siempre saldo inicial, caiga o no su fecha dentro del rango (US13: son el saldo con el que la
/// cooperativa entró, no un movimiento del período).
/// </para>
/// </summary>
public static class MovimientosContables
{
    public sealed record TerceroResuelto(int Id, Guid PublicId, string Nombre, string TaxId);

    /// <summary>
    /// El rubro NIIF pedido y los códigos que abarca: el suyo y los de todos sus descendientes por
    /// <c>ParentCode</c>. Las cuentas apuntan a un rubro por código, así que filtrar por «Activo»
    /// (ESF-A) es filtrar por todos los códigos que cuelgan de él.
    /// </summary>
    public sealed record RubroResuelto(string Code, string Name, IReadOnlyList<string> Codigos);

    /// <summary>Los filtros ya resueltos a identificadores internos y nombres para el encabezado.</summary>
    public sealed record Contexto(
        FiltrosDeInforme Filtros,
        DateOnly Desde,
        DateOnly Hasta,
        AlcanceDeSucursales Alcance,
        ChartOfAccount? Cuenta,
        TerceroResuelto? Tercero,
        int? CentroDeCostoId, string? CentroDeCosto,
        int? SucursalId, string? Sucursal,
        int? TipoDeCruceId, string? NumeroDeCruce,
        RubroResuelto? Rubro = null)
    {
        public string PeriodoTexto => $"{Desde:dd/MM/yyyy} – {Hasta:dd/MM/yyyy}";
    }

    // Los códigos que ya existen en AccountingErrors se reutilizan; los de tercero, centro, sucursal y rango son propios de las consultas.
    public static readonly Error CuentaNoEncontrada = new("Accounting.Account.NotFound", "La cuenta indicada no existe en el plan.");
    public static readonly Error TerceroNoEncontrado = new("Accounting.Person.NotFound", "El tercero indicado no existe.");
    public static readonly Error CentroNoEncontrado = new("Accounting.CostCenter.NotFound", "El centro de costo indicado no existe.");
    public static readonly Error SucursalNoEncontrada = new("Accounting.Branch.NotFound", "La sucursal indicada no existe.");
    public static readonly Error CruceNoEncontrado = Posting.AccountingErrors.CrossDocumentTypeNotFound;
    public static readonly Error RangoInvalido = new("Accounting.Report.InvalidRange", "La fecha final no puede ser anterior a la inicial.");
    public static readonly Error TerceroRequerido = new("Accounting.Report.PersonRequired", "Esta consulta necesita un tercero.");
    public static readonly Error CuentaRequerida = new("Accounting.Report.AccountRequired", "Esta consulta necesita una cuenta.");
    public static readonly Error RubroNoEncontrado = new("Accounting.Report.NiifItemNotFound", "El rubro NIIF indicado no existe para el grupo de la empresa.");

    // El rango de fechas se acota aquí, una sola vez, y no en cada validador (hay cuatro y ninguno
    // lo hacía). Sin tope, `from=0001-01-01&to=9999-12-31` era válido: el saldo diario promedio
    // iteraba 3,65 millones de días en memoria y los `AddDays(-1)`/`AddYears(-1)` con que los
    // informes calculan «el día anterior» y «el año comparativo» se salían del dominio de DateOnly
    // y respondían 500. Un rango de siglos tampoco significa nada contable: el ejercicio es el año.
    public const string RangoDemasiadoLargoCodigo = "Accounting.Report.RangeTooLong";
    /// <summary>Años que puede abarcar un rango si la consulta no pide menos (libros, terceros, estados).</summary>
    public const int RangoMaximoEnAnios = 5;
    /// <summary>Nada contable de una cooperativa es anterior a esto, y deja holgura de sobra para restar un año o un día.</summary>
    public static readonly DateOnly FechaMinima = DateOnly.FromDateTime(DateTime.UnixEpoch);
    /// <summary>Hasta dónde puede llegar <c>To</c>: un año después de hoy (presupuestos y proyecciones digitan a futuro, pero no más).</summary>
    public static DateOnly FechaMaxima(DateOnly hoy) => hoy.AddYears(1);

    public static readonly Error FechaFueraDeDominio = new("Accounting.Report.DateOutOfRange",
        $"Las fechas deben estar entre el {FechaMinima:dd/MM/yyyy} y un año después de hoy.");

    public static Error RangoDemasiadoLargo(int anios) => new(RangoDemasiadoLargoCodigo,
        anios == 1 ? "El rango no puede superar un año." : $"El rango no puede superar {anios} años.");

    /// <summary>
    /// Resuelve los filtros una sola vez (PublicId → Id, nombres para el encabezado) y el alcance de sucursal.
    /// <paramref name="rangoMaximoEnAnios"/> es lo más que puede abarcar <c>From..To</c>; el saldo diario
    /// promedio pide 1 porque es por definición de un período y produce una fila por día.
    /// </summary>
    public static async Task<Result<Contexto>> PrepararAsync(
        IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, FiltrosDeInforme f, CancellationToken ct,
        int rangoMaximoEnAnios = RangoMaximoEnAnios)
    {
        var hoy = clock.TodayUtc;
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        if (hasta < desde) return Result.Failure<Contexto>(RangoInvalido);
        if (desde < FechaMinima || hasta > FechaMaxima(hoy)) return Result.Failure<Contexto>(FechaFueraDeDominio);
        // Un ejercicio completo (01/01..31/12) cabe justo en un año: el tope se compara con el mismo día del año siguiente.
        if (hasta >= desde.AddYears(rangoMaximoEnAnios)) return Result.Failure<Contexto>(RangoDemasiadoLargo(rangoMaximoEnAnios));

        ChartOfAccount? cuenta = null;
        if (f.AccountPublicId is { } cuentaId)
        {
            cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == cuentaId && !a.IsDeleted, ct);
            if (cuenta is null) return Result.Failure<Contexto>(CuentaNoEncontrada);
        }

        TerceroResuelto? tercero = null;
        if (f.Person is { } personaId)
        {
            // Un tercero dado de baja conserva sus movimientos: su estado de cuenta sigue existiendo.
            tercero = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == personaId)
                .Select(p => new TerceroResuelto(p.Id, p.PublicId, PersonFactory.NombreVisible(p.FirstName, p.LastName, p.BusinessName), p.TaxId))
                .FirstOrDefaultAsync(ct);
            if (tercero is null) return Result.Failure<Contexto>(TerceroNoEncontrado);
        }

        int? centroId = null; string? centro = null;
        if (f.CostCenter is { } centroPublicId)
        {
            var c = await db.CostCenters.AsNoTracking().Where(x => x.PublicId == centroPublicId).Select(x => new { x.Id, x.Name, x.LegacyCode }).FirstOrDefaultAsync(ct);
            if (c is null) return Result.Failure<Contexto>(CentroNoEncontrado);
            centroId = c.Id; centro = string.IsNullOrWhiteSpace(c.LegacyCode) ? c.Name : $"{c.LegacyCode} {c.Name}";
        }

        int? sucursalId = null; string? sucursal = null;
        if (f.Branch is { } sucursalPublicId)
        {
            var s = await db.Branches.AsNoTracking().Where(x => x.PublicId == sucursalPublicId).Select(x => new { x.Id, x.Name }).FirstOrDefaultAsync(ct);
            if (s is null) return Result.Failure<Contexto>(SucursalNoEncontrada);
            sucursalId = s.Id; sucursal = s.Name;
        }

        int? cruceId = null;
        var (tipoCruce, numeroCruce) = f.Cruce();
        if (tipoCruce is not null)
        {
            var t = await db.CrossDocumentTypes.AsNoTracking().Where(x => x.Code == tipoCruce).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (t is null) return Result.Failure<Contexto>(CruceNoEncontrado);
            cruceId = t;
        }

        RubroResuelto? rubro = null;
        if (!string.IsNullOrWhiteSpace(f.NiifItem))
        {
            var resuelto = await ResolverRubroAsync(db, f.NiifItem, ct);
            if (resuelto.IsFailure) return Result.Failure<Contexto>(resuelto.Error);
            rubro = resuelto.Value;
        }

        var scope = await alcance.ObtenerAsync(ct);
        return Result.Success(new Contexto(f, desde, hasta, scope, cuenta, tercero, centroId, centro, sucursalId, sucursal, cruceId, numeroCruce, rubro));
    }

    /// <summary>
    /// El rubro por código dentro del grupo NIIF de la empresa, con todos sus descendientes. La
    /// jerarquía se arma en memoria sobre los rubros del grupo (son decenas, no miles), igual que
    /// hace <see cref="SaldosPorRubro"/>; un rubro de otro grupo o un código inventado no existen.
    /// </summary>
    private static async Task<Result<RubroResuelto>> ResolverRubroAsync(IApplicationDbContext db, string codigo, CancellationToken ct)
    {
        var grupo = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => (byte?)s.NiifGroup).FirstOrDefaultAsync(ct);
        if (grupo is null) return Result.Failure<RubroResuelto>(Posting.AccountingErrors.NotInitialized);
        var rubros = await db.FinancialStatementItems.AsNoTracking()
            .Where(r => !r.IsDeleted && r.NiifGroup == grupo.Value)
            .Select(r => new { r.Code, r.Name, r.ParentCode })
            .ToListAsync(ct);
        var buscado = codigo.Trim();
        var raiz = rubros.FirstOrDefault(r => r.Code.Equals(buscado, StringComparison.OrdinalIgnoreCase));
        if (raiz is null) return Result.Failure<RubroResuelto>(RubroNoEncontrado);

        var hijosDe = rubros.Where(r => r.ParentCode is not null).ToLookup(r => r.ParentCode!, r => r.Code, StringComparer.Ordinal);
        var codigos = new List<string> { raiz.Code };
        var pendientes = new Queue<string>([raiz.Code]);
        while (pendientes.TryDequeue(out var actual))
            foreach (var hijo in hijosDe[actual])
            {
                if (codigos.Contains(hijo, StringComparer.Ordinal)) continue; // un ciclo en la semilla no debe colgar la consulta
                codigos.Add(hijo);
                pendientes.Enqueue(hijo);
            }
        return Result.Success(new RubroResuelto(raiz.Code, raiz.Name, codigos));
    }

    /// <summary>
    /// Asientos contabilizados que pasan todos los filtros salvo los de fecha. Sobre esto se
    /// piden <see cref="DelRango"/> (débitos y créditos del período) e <see cref="Iniciales"/>
    /// (lo que forma el saldo inicial).
    /// </summary>
    public static IQueryable<JournalEntry> Base(IApplicationDbContext db, Contexto c)
    {
        var q = db.JournalEntries.AsNoTracking().Where(e => e.IsPosted);
        if (!c.Filtros.IncludeClosing)
        {
            // Se aparta sólo el cierre del ejercicio consultado (o de uno posterior): el de los años
            // anteriores ya es historia y forma parte del saldo inicial. Cuando se apartaba todo
            // cierre, en el año 2 los ingresos y gastos arrancaban con el saldo del año 1 y la 35 sin
            // el excedente; el balance «cuadraba» (Σ débitos = Σ créditos) con los saldos mal (US6,
            // escenario 4). Es la misma regla que ya aplica SaldosPorRubro.ALaFechaAsync al ESF.
            var inicioDelEjercicio = SaldosPorRubro.InicioDelEjercicio(c.Desde);
            q = q.Where(e => e.Document!.Kind != DocumentKind.Closing || e.Date < inicioDelEjercicio);
        }
        if (c.Alcance.Restringido)
        {
            var permitidas = c.Alcance.Sucursales.ToList();
            q = q.Where(e => permitidas.Contains(e.BranchId));
        }
        if (c.Cuenta is { } cuenta) q = q.Where(e => e.Account!.Code.StartsWith(cuenta.Code));
        if (c.Rubro is { } rubro)
        {
            var codigos = rubro.Codigos.ToList();
            q = q.Where(e => codigos.Contains(e.Account!.NiifItemCode));
        }
        if (!string.IsNullOrWhiteSpace(c.Filtros.AccountFrom))
        {
            var desde = c.Filtros.AccountFrom.Trim();
            q = q.Where(e => string.Compare(e.Account!.Code, desde) >= 0);
        }
        if (!string.IsNullOrWhiteSpace(c.Filtros.AccountTo))
        {
            // «hasta 1105» incluye 110505: entra lo que empieza así o queda antes.
            var hasta = c.Filtros.AccountTo.Trim();
            q = q.Where(e => e.Account!.Code.StartsWith(hasta) || string.Compare(e.Account!.Code, hasta) <= 0);
        }
        if (c.Tercero is { } t) q = q.Where(e => e.PersonId == t.Id);
        if (c.TipoDeCruceId is { } cruce)
        {
            q = q.Where(e => e.CrossDocumentTypeId == cruce);
            if (c.NumeroDeCruce is { } numero) q = q.Where(e => e.CrossDocumentNumber == numero);
        }
        if (c.CentroDeCostoId is { } centro) q = q.Where(e => e.CostCenterId == centro);
        if (c.SucursalId is { } sucursal) q = q.Where(e => e.BranchId == sucursal);
        if (!string.IsNullOrWhiteSpace(c.Filtros.VoucherType))
        {
            var tipo = c.Filtros.VoucherType.Trim().ToUpperInvariant();
            q = q.Where(e => e.Document!.VoucherType!.Code == tipo);
        }
        if (!string.IsNullOrWhiteSpace(c.Filtros.Origin))
        {
            var origen = c.Filtros.Origin.Trim().ToUpperInvariant();
            q = q.Where(e => e.Document!.OriginModule == origen);
        }
        if (!string.IsNullOrWhiteSpace(c.Filtros.User))
        {
            var usuario = c.Filtros.User.Trim().ToLower();
            q = q.Where(e => e.Document!.RegisteredBy.ToLower().Contains(usuario)
                             || (e.Document!.PostedBy != null && e.Document!.PostedBy.ToLower().Contains(usuario)));
        }
        return q;
    }

    /// <summary>Débitos y créditos del período: dentro del rango y que no sean la apertura.</summary>
    public static IQueryable<JournalEntry> DelRango(IQueryable<JournalEntry> q, Contexto c) =>
        q.Where(e => e.Date >= c.Desde && e.Date <= c.Hasta && e.Document!.Kind != DocumentKind.Opening);

    /// <summary>Lo que forma el saldo inicial: todo lo anterior al rango más la apertura, tenga la fecha que tenga (hasta el final del rango).</summary>
    public static IQueryable<JournalEntry> Iniciales(IQueryable<JournalEntry> q, Contexto c) =>
        q.Where(e => e.Date < c.Desde || (e.Document!.Kind == DocumentKind.Opening && e.Date <= c.Hasta));

    /// <summary>Todo lo acumulado hasta una fecha inclusive (para saldos a una fecha: estados financieros).</summary>
    public static IQueryable<JournalEntry> HastaInclusive(IQueryable<JournalEntry> q, DateOnly fecha) =>
        q.Where(e => e.Date <= fecha);
}
