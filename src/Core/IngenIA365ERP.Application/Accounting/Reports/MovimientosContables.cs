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
/// Dos reglas de fechas que parecen detalle y no lo son: el comprobante de <b>cierre</b> sólo
/// entra si se pide (<c>IncludeClosing</c>), y las líneas del comprobante de <b>apertura</b> son
/// siempre saldo inicial, caiga o no su fecha dentro del rango (US13: son el saldo con el que la
/// cooperativa entró, no un movimiento del período).
/// </para>
/// </summary>
public static class MovimientosContables
{
    public sealed record TerceroResuelto(int Id, Guid PublicId, string Nombre, string TaxId);

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
        int? TipoDeCruceId, string? NumeroDeCruce)
    {
        public string PeriodoTexto => $"{Desde:dd/MM/yyyy} – {Hasta:dd/MM/yyyy}";
    }

    public static readonly Error CuentaNoEncontrada = new("Accounting.Account.NotFound", "La cuenta indicada no existe en el plan.");
    public static readonly Error TerceroNoEncontrado = new("Accounting.Person.NotFound", "El tercero indicado no existe.");
    public static readonly Error CentroNoEncontrado = new("Accounting.CostCenter.NotFound", "El centro de costo indicado no existe.");
    public static readonly Error SucursalNoEncontrada = new("Accounting.Branch.NotFound", "La sucursal indicada no existe.");
    public static readonly Error CruceNoEncontrado = new("Accounting.CrossDocumentType.NotFound", "El tipo de documento cruce indicado no existe.");
    public static readonly Error RangoInvalido = new("Accounting.Report.InvalidRange", "La fecha final no puede ser anterior a la inicial.");

    /// <summary>Resuelve los filtros una sola vez (PublicId → Id, nombres para el encabezado) y el alcance de sucursal.</summary>
    public static async Task<Result<Contexto>> PrepararAsync(
        IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, FiltrosDeInforme f, CancellationToken ct)
    {
        var hoy = clock.TodayUtc;
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        if (hasta < desde) return Result.Failure<Contexto>(RangoInvalido);

        ChartOfAccount? cuenta = null;
        if (f.AccountPublicId is { } cuentaId)
        {
            cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == cuentaId && !a.IsDeleted, ct);
            if (cuenta is null) return Result.Failure<Contexto>(CuentaNoEncontrada);
        }

        TerceroResuelto? tercero = null;
        if (f.Person is { } personaId)
        {
            tercero = await db.People.AsNoTracking().Where(p => p.PublicId == personaId)
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

        var scope = await alcance.ObtenerAsync(ct);
        return Result.Success(new Contexto(f, desde, hasta, scope, cuenta, tercero, centroId, centro, sucursalId, sucursal, cruceId, numeroCruce));
    }

    /// <summary>
    /// Asientos contabilizados que pasan todos los filtros salvo los de fecha. Sobre esto se
    /// piden <see cref="DelRango"/> (débitos y créditos del período) e <see cref="Iniciales"/>
    /// (lo que forma el saldo inicial).
    /// </summary>
    public static IQueryable<JournalEntry> Base(IApplicationDbContext db, Contexto c)
    {
        var q = db.JournalEntries.AsNoTracking().Where(e => e.IsPosted);
        if (!c.Filtros.IncludeClosing) q = q.Where(e => e.Document!.Kind != DocumentKind.Closing);
        if (c.Alcance.Restringido)
        {
            var permitidas = c.Alcance.Sucursales.ToList();
            q = q.Where(e => permitidas.Contains(e.BranchId));
        }
        if (c.Cuenta is { } cuenta) q = q.Where(e => e.Account!.Code.StartsWith(cuenta.Code));
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
