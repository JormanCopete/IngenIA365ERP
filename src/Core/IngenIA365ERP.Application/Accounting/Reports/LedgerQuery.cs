using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// El libro auxiliar interactivo (feature 009 E2, FR-042): una vista que se profundiza nodo a
/// nodo, desde las clases del plan hasta la línea del comprobante, con saldo inicial, débitos,
/// créditos y saldo final en cada escalón y con todos los filtros de <see cref="FiltrosDeInforme"/>
/// aplicados a la vez. <paramref name="Node"/> es «tipo:clave» (diseño E2 §3):
/// <list type="bullet">
/// <item>vacío → las clases con saldo o movimiento;</item>
/// <item><c>account:&lt;código&gt;</c> → sus cuentas hijas, o, si es de movimiento, los terceros con
/// movimiento en ella (incluida la fila «Sin tercero»);</item>
/// <item><c>person:&lt;código&gt;|&lt;personPublicId o none&gt;</c> → los documentos cruce (incluida «Sin documento»);</item>
/// <item><c>document:&lt;código&gt;|&lt;tercero&gt;|&lt;tipo o none&gt;|&lt;número o none&gt;</c> → los comprobantes
/// que tocaron esa combinación en el rango, con saldo corrido;</item>
/// <item><c>voucher:&lt;documentPublicId&gt;</c> → todas las líneas del comprobante.</item>
/// </list>
/// Cada fila lleva en columnas ocultas la clave del hijo (<c>_nodo</c>), su tipo (<c>_tipo</c>), la
/// cuenta (<c>_cuenta</c>) y el comprobante (<c>_comprobante</c>) para que la pantalla siga bajando o
/// abra el comprobante sin volver a preguntar.
/// </summary>
public sealed record LedgerQuery(FiltrosDeInforme Filtros, string? Node) : IRequest<Result<TablaExportable>>;

public sealed class LedgerQueryValidator : AbstractValidator<LedgerQuery>
{
    private static readonly string[] Formatos = ["json", "xlsx", "pdf", "docx"];

    public LedgerQueryValidator()
    {
        RuleFor(x => x.Filtros).NotNull();
        RuleFor(x => x.Filtros!.Level).InclusiveBetween(1, 6).When(x => x.Filtros?.Level is not null);
        RuleFor(x => x.Filtros!.Format)
            .Must(f => Formatos.Contains(f!.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.Filtros?.Format))
            .WithMessage("El formato debe ser json, xlsx, pdf o docx.");
        // La forma del nodo es regla de negocio (422, Accounting.Report.InvalidNode): aquí sólo se acota el largo.
        RuleFor(x => x.Node).MaximumLength(LedgerQueryHandler.LargoMaximoDeNodo).When(x => x.Node is not null);
    }
}

public sealed class LedgerQueryHandler(
    IApplicationDbContext db,
    IUserBranchScope alcance,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingAuditEmitter audit) : IRequestHandler<LedgerQuery, Result<TablaExportable>>
{
    public const string Informe = "ledger";
    public const int LargoMaximoDeNodo = 300;
    public const string Ninguno = "none";
    private const int SinTercero = 0;

    public static readonly Error NodoInvalido = new("Accounting.Report.InvalidNode",
        "El nodo del libro auxiliar no tiene la forma esperada: account:<código>, person:<código>|<tercero>, document:<código>|<tercero>|<tipo>|<número> o voucher:<comprobante>.");

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Código"), new("Nombre"),
        new("Saldo inicial", TipoDeColumna.Moneda), new("Débitos", TipoDeColumna.Moneda),
        new("Créditos", TipoDeColumna.Moneda), new("Saldo final", TipoDeColumna.Moneda),
        new("Nodo", Clave: "_nodo"), new("Tipo", Clave: "_tipo"),
        new("Cuenta", Clave: "_cuenta"), new("Comprobante", Clave: "_comprobante"),
    ];

    public async Task<Result<TablaExportable>> Handle(LedgerQuery request, CancellationToken ct)
    {
        var nodo = Nodo.Interpretar(request.Node);
        if (nodo.IsFailure) return Result.Failure<TablaExportable>(nodo.Error);

        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var q = MovimientosContables.Base(db, c);

        Result<Nivel> nivel = nodo.Value.Tipo switch
        {
            TipoDeNodo.Raiz => await CuentasAsync(q, c, plan, cuenta: null, ct),
            TipoDeNodo.Cuenta => await DesdeCuentaAsync(q, c, plan, nodo.Value, ct),
            TipoDeNodo.Tercero => await DocumentosAsync(q, c, plan, nodo.Value, ct),
            TipoDeNodo.Documento => await ComprobantesAsync(q, c, plan, nodo.Value, ct),
            TipoDeNodo.Comprobante => await LineasAsync(c, nodo.Value, ct),
            _ => Result.Failure<Nivel>(NodoInvalido),
        };
        if (nivel.IsFailure) return Result.Failure<TablaExportable>(nivel.Error);

        var n = nivel.Value;
        var notas = await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct);
        var tabla = new TablaExportable(
            string.Join(" · ", new[] { "Libro auxiliar" }.Concat(n.Camino)),
            $"{n.Descripcion} · {c.PeriodoTexto}",
            Columnas, n.Filas, Totales(n.Filas, n.ConSaldos), notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Informe, new { filtros = request.Filtros, node = request.Node }, request.Filtros.Format!, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }

    // ------------------------------------------------------------------ niveles --

    /// <summary>Lo que devuelve un nodo: el camino legible para el título, qué se está viendo y sus filas.</summary>
    private sealed record Nivel(IReadOnlyList<string> Camino, string Descripcion, IReadOnlyList<FilaExportable> Filas, bool ConSaldos = true);

    private async Task<Result<Nivel>> DesdeCuentaAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, Nodo nodo, CancellationToken ct)
    {
        var cuenta = plan.PorCodigo(nodo.Codigo!);
        if (cuenta is null) return Result.Failure<Nivel>(MovimientosContables.CuentaNoEncontrada);
        // Con hijas en el plan se sigue por el plan; sin hijas (la de movimiento, o una de agrupación
        // a la que la empresa no le creó hijas) se abre por terceros: no hay otro sitio donde bajar.
        return plan.TieneHijos(cuenta.Id)
            ? await CuentasAsync(q, c, plan, cuenta, ct)
            : await TercerosAsync(q, c, plan, cuenta, ct);
    }

    /// <summary>Las clases (raíz) o las hijas de una cuenta, con las sumas agregadas desde las cuentas de movimiento.</summary>
    private static async Task<Result<Nivel>> CuentasAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, JerarquiaDelPlan.Cuenta? cuenta, CancellationToken ct)
    {
        var porCuenta = new Dictionary<int, SumasDeCuenta>();
        foreach (var p in await MovimientosContables.DelRango(q, c).GroupBy(e => e.AccountId)
                     .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
            porCuenta[p.Key] = porCuenta.GetValueOrDefault(p.Key, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(0m, 0m, p.D, p.C));
        foreach (var i in await MovimientosContables.Iniciales(q, c).GroupBy(e => e.AccountId)
                     .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
            porCuenta[i.Key] = porCuenta.GetValueOrDefault(i.Key, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));
        var total = plan.Agregar(porCuenta);

        var hijas = cuenta is null ? plan.Raices : plan.Hijos(cuenta.Id);
        var filas = hijas
            .Select(h => (Cuenta: h, Sumas: total.GetValueOrDefault(h.Id, SumasDeCuenta.Cero)))
            .Where(x => !x.Sumas.EsCero)
            .Select(x => Fila(x.Cuenta.Code, x.Cuenta.Name, x.Sumas, x.Cuenta.Nature, $"account:{x.Cuenta.Code}", "account", x.Cuenta.PublicId, null))
            .ToList();

        return Result.Success(new Nivel(
            cuenta is null ? [] : [Etiqueta(cuenta)],
            cuenta is null ? "Clases del plan" : $"Cuentas de nivel {cuenta.Level + 1}",
            filas));
    }

    /// <summary>Los terceros con saldo o movimiento en una cuenta de movimiento; la fila «Sin tercero» agrupa las líneas sin él.</summary>
    private async Task<Result<Nivel>> TercerosAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, JerarquiaDelPlan.Cuenta cuenta, CancellationToken ct)
    {
        var enCuenta = q.Where(e => e.AccountId == cuenta.Id);
        // La clave 0 es «Sin tercero» (ningún Id real vale 0): un diccionario no admite la clave nula.
        var porTercero = new Dictionary<int, SumasDeCuenta>();
        foreach (var p in await MovimientosContables.DelRango(enCuenta, c).GroupBy(e => e.PersonId)
                     .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
            porTercero[p.Key ?? SinTercero] = porTercero.GetValueOrDefault(p.Key ?? SinTercero, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(0m, 0m, p.D, p.C));
        foreach (var i in await MovimientosContables.Iniciales(enCuenta, c).GroupBy(e => e.PersonId)
                     .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
            porTercero[i.Key ?? SinTercero] = porTercero.GetValueOrDefault(i.Key ?? SinTercero, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));

        var ids = porTercero.Keys.Where(k => k != SinTercero).ToList();
        var personas = ids.Count == 0
            ? []
            : await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
                .Select(p => new MovimientosContables.TerceroResuelto(p.Id, p.PublicId, PersonFactory.NombreVisible(p.FirstName, p.LastName, p.BusinessName), p.TaxId))
                .ToListAsync(ct);
        var porId = personas.ToDictionary(p => p.Id);

        var filas = porTercero
            .Where(x => !x.Value.EsCero)
            .Select(x =>
            {
                var t = x.Key == SinTercero ? null : porId.GetValueOrDefault(x.Key);
                var nombre = x.Key == SinTercero ? "Sin tercero" : t?.Nombre ?? $"Tercero #{x.Key}";
                var codigo = t?.TaxId ?? "—";
                var clave = t?.PublicId.ToString() ?? Ninguno;
                return (Orden: x.Key == SinTercero ? 1 : 0, Nombre: nombre,
                    Fila: Fila(codigo, nombre, x.Value, cuenta.Nature, $"person:{cuenta.Code}|{clave}", "person", cuenta.PublicId, null));
            })
            .OrderBy(x => x.Orden).ThenBy(x => x.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Fila)
            .ToList();

        return Result.Success(new Nivel([Etiqueta(cuenta)], "Terceros", filas));
    }

    /// <summary>Los documentos cruce de un tercero en una cuenta; «Sin documento» agrupa las líneas sin cruce.</summary>
    private async Task<Result<Nivel>> DocumentosAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, Nodo nodo, CancellationToken ct)
    {
        var cuenta = plan.PorCodigo(nodo.Codigo!);
        if (cuenta is null) return Result.Failure<Nivel>(MovimientosContables.CuentaNoEncontrada);
        var tercero = await TerceroAsync(nodo, ct);
        if (tercero.IsFailure) return Result.Failure<Nivel>(tercero.Error);

        var enCombinacion = PorTercero(q.Where(e => e.AccountId == cuenta.Id), tercero.Value);
        var porDocumento = new Dictionary<(int? Tipo, string? Numero), SumasDeCuenta>();
        foreach (var p in await MovimientosContables.DelRango(enCombinacion, c).GroupBy(e => new { e.CrossDocumentTypeId, e.CrossDocumentNumber })
                     .Select(g => new { g.Key.CrossDocumentTypeId, g.Key.CrossDocumentNumber, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
        {
            var k = (p.CrossDocumentTypeId, p.CrossDocumentNumber);
            porDocumento[k] = porDocumento.GetValueOrDefault(k, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(0m, 0m, p.D, p.C));
        }
        foreach (var i in await MovimientosContables.Iniciales(enCombinacion, c).GroupBy(e => new { e.CrossDocumentTypeId, e.CrossDocumentNumber })
                     .Select(g => new { g.Key.CrossDocumentTypeId, g.Key.CrossDocumentNumber, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
        {
            var k = (i.CrossDocumentTypeId, i.CrossDocumentNumber);
            porDocumento[k] = porDocumento.GetValueOrDefault(k, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));
        }

        var tiposId = porDocumento.Keys.Where(k => k.Tipo is not null).Select(k => k.Tipo!.Value).Distinct().ToList();
        var tipos = tiposId.Count == 0
            ? new Dictionary<int, (string Code, string Name)>()
            : await db.CrossDocumentTypes.AsNoTracking().Where(t => tiposId.Contains(t.Id))
                .Select(t => new { t.Id, t.Code, t.Name }).ToDictionaryAsync(t => t.Id, t => (t.Code, t.Name), ct);

        var terceroClave = tercero.Value?.PublicId.ToString() ?? Ninguno;
        var filas = porDocumento
            .Where(x => !x.Value.EsCero)
            .Select(x =>
            {
                var (tipoId, numero) = x.Key;
                var tipo = tipoId is { } id ? tipos.GetValueOrDefault(id) : default;
                var tipoCodigo = tipoId is null ? null : tipo.Code ?? $"#{tipoId}";
                var sinDocumento = tipoCodigo is null && string.IsNullOrWhiteSpace(numero);
                var codigo = sinDocumento ? "—" : string.Join(" ", new[] { tipoCodigo, numero }.Where(s => !string.IsNullOrWhiteSpace(s)));
                var nombre = sinDocumento ? "Sin documento" : tipo.Name ?? "Documento cruce";
                var clave = $"document:{cuenta.Code}|{terceroClave}|{tipoCodigo ?? Ninguno}|{(string.IsNullOrWhiteSpace(numero) ? Ninguno : numero)}";
                return (Orden: sinDocumento ? 1 : 0, Codigo: codigo, Fila: Fila(codigo, nombre, x.Value, cuenta.Nature, clave, "document", cuenta.PublicId, null));
            })
            .OrderBy(x => x.Orden).ThenBy(x => x.Codigo, StringComparer.Ordinal)
            .Select(x => x.Fila)
            .ToList();

        return Result.Success(new Nivel([Etiqueta(cuenta), Etiqueta(tercero.Value)], "Documentos cruce", filas));
    }

    /// <summary>Los comprobantes del rango que tocaron (cuenta, tercero, documento), con saldo corrido desde el inicial de la combinación.</summary>
    private async Task<Result<Nivel>> ComprobantesAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, Nodo nodo, CancellationToken ct)
    {
        var cuenta = plan.PorCodigo(nodo.Codigo!);
        if (cuenta is null) return Result.Failure<Nivel>(MovimientosContables.CuentaNoEncontrada);
        var tercero = await TerceroAsync(nodo, ct);
        if (tercero.IsFailure) return Result.Failure<Nivel>(tercero.Error);

        int? tipoId = null; string? tipoNombre = null;
        if (nodo.TipoDeCruce is { } tipoCodigo)
        {
            var t = await db.CrossDocumentTypes.AsNoTracking().Where(x => x.Code == tipoCodigo).Select(x => new { x.Id, x.Name }).FirstOrDefaultAsync(ct);
            if (t is null) return Result.Failure<Nivel>(MovimientosContables.CruceNoEncontrado);
            tipoId = t.Id; tipoNombre = t.Name;
        }

        var enCombinacion = PorTercero(q.Where(e => e.AccountId == cuenta.Id), tercero.Value)
            .Where(e => e.CrossDocumentTypeId == tipoId);
        var numero = nodo.NumeroDeCruce;
        enCombinacion = numero is null
            ? enCombinacion.Where(e => e.CrossDocumentNumber == null || e.CrossDocumentNumber == string.Empty)
            : enCombinacion.Where(e => e.CrossDocumentNumber == numero);

        var inicial = await MovimientosContables.Iniciales(enCombinacion, c)
            .GroupBy(_ => 1).Select(g => new { D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).FirstOrDefaultAsync(ct);
        var movimientos = await MovimientosContables.DelRango(enCombinacion, c).GroupBy(e => e.DocumentId)
            .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);
        var ids = movimientos.Select(m => m.Key).ToList();
        var documentos = ids.Count == 0
            ? []
            : await db.AccountingDocuments.AsNoTracking().Where(d => ids.Contains(d.Id))
                .Select(d => new { d.Id, d.PublicId, Tipo = d.VoucherType!.Code, d.Number, d.Date, d.Description }).ToListAsync(ct);
        var porId = documentos.ToDictionary(d => d.Id);

        var saldo = new SumasDeCuenta(inicial?.D ?? 0m, inicial?.C ?? 0m, 0m, 0m).SaldoInicial(cuenta.Nature);
        var filas = new List<FilaExportable>();
        foreach (var m in movimientos.Where(m => porId.ContainsKey(m.Key)).OrderBy(m => porId[m.Key].Date).ThenBy(m => porId[m.Key].Number).ThenBy(m => m.Key))
        {
            var d = porId[m.Key];
            var antes = saldo;
            saldo += cuenta.Nature == AccountNature.Debit ? m.D - m.C : m.C - m.D;
            filas.Add(new FilaExportable([
                $"{d.Tipo}-{d.Number}", $"{d.Date:dd/MM/yyyy} · {d.Description}", antes, m.D, m.C, saldo,
                $"voucher:{d.PublicId}", "voucher", cuenta.PublicId, d.PublicId]));
        }

        var documento = nodo.TipoDeCruce is null && numero is null
            ? "Sin documento"
            : string.Join(" ", new[] { nodo.TipoDeCruce, numero }.Where(s => !string.IsNullOrWhiteSpace(s))) + (tipoNombre is null ? string.Empty : $" ({tipoNombre})");
        return Result.Success(new Nivel([Etiqueta(cuenta), Etiqueta(tercero.Value), documento], "Comprobantes", filas));
    }

    /// <summary>Todas las líneas de un comprobante, con el alcance de sucursal de quien consulta; las columnas de saldo van vacías.</summary>
    private async Task<Result<Nivel>> LineasAsync(MovimientosContables.Contexto c, Nodo nodo, CancellationToken ct)
    {
        var doc = await db.AccountingDocuments.AsNoTracking().Where(d => d.PublicId == nodo.Comprobante!.Value)
            .Select(d => new { d.Id, d.PublicId, Tipo = d.VoucherType!.Code, d.Number, d.Date, d.Description }).FirstOrDefaultAsync(ct);
        if (doc is null) return Result.Failure<Nivel>(AccountingErrors.DocumentNotFound);

        var q = db.JournalEntries.AsNoTracking().Where(e => e.DocumentId == doc.Id && e.IsPosted);
        if (c.Alcance.Restringido)
        {
            var permitidas = c.Alcance.Sucursales.ToList();
            q = q.Where(e => permitidas.Contains(e.BranchId));
        }
        var lineas = await q.OrderBy(e => e.LineNumber)
            .Select(e => new
            {
                Codigo = e.Account!.Code, Nombre = e.Account!.Name, Cuenta = e.Account!.PublicId,
                Tercero = e.Person == null ? null : PersonFactory.NombreVisible(e.Person.FirstName, e.Person.LastName, e.Person.BusinessName),
                Tipo = e.CrossDocumentType == null ? null : e.CrossDocumentType.Code, e.CrossDocumentNumber,
                e.Description, e.Debit, e.Credit,
            })
            .ToListAsync(ct);

        var filas = lineas.Select(l =>
        {
            var documento = string.Join(" ", new[] { l.Tipo, l.CrossDocumentNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var nombre = string.Join(" · ", new[] { l.Nombre, l.Tercero, documento, l.Description }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new FilaExportable([l.Codigo, nombre, null, l.Debit, l.Credit, null, null, "line", l.Cuenta, doc.PublicId]);
        }).ToList();

        return Result.Success(new Nivel([$"{doc.Tipo}-{doc.Number}", $"{doc.Date:dd/MM/yyyy} {doc.Description}"], "Líneas del comprobante", filas, ConSaldos: false));
    }

    // ------------------------------------------------------------------ apoyo --

    private static FilaExportable Fila(string codigo, string nombre, SumasDeCuenta s, AccountNature naturaleza, string nodo, string tipo, Guid? cuenta, Guid? comprobante) =>
        new([codigo, nombre, s.SaldoInicial(naturaleza), s.Debitos, s.Creditos, s.SaldoFinal(naturaleza), nodo, tipo, cuenta, comprobante]);

    private static FilaExportable? Totales(IReadOnlyList<FilaExportable> filas, bool conSaldos)
    {
        if (filas.Count == 0) return null;
        decimal Suma(int i) => filas.Sum(f => f.Valores[i] as decimal? ?? 0m);
        return new FilaExportable(["Total", string.Empty, conSaldos ? Suma(2) : null, Suma(3), Suma(4), conSaldos ? Suma(5) : null, null, null, null, null], Resaltada: true);
    }

    private static string Etiqueta(JerarquiaDelPlan.Cuenta cuenta) => $"{cuenta.Code} {cuenta.Name}";

    private static string Etiqueta(MovimientosContables.TerceroResuelto? tercero) => tercero is null ? "Sin tercero" : $"{tercero.Nombre} ({tercero.TaxId})";

    private static IQueryable<JournalEntry> PorTercero(IQueryable<JournalEntry> q, MovimientosContables.TerceroResuelto? tercero) =>
        tercero is null ? q.Where(e => e.PersonId == null) : q.Where(e => e.PersonId == tercero.Id);

    /// <summary>El tercero del nodo: null es la fila «Sin tercero»; uno que no existe (ni dado de baja) es un error.</summary>
    private async Task<Result<MovimientosContables.TerceroResuelto?>> TerceroAsync(Nodo nodo, CancellationToken ct)
    {
        if (nodo.Tercero is not { } publicId) return Result.Success<MovimientosContables.TerceroResuelto?>(null);
        var t = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == publicId)
            .Select(p => new MovimientosContables.TerceroResuelto(p.Id, p.PublicId, PersonFactory.NombreVisible(p.FirstName, p.LastName, p.BusinessName), p.TaxId))
            .FirstOrDefaultAsync(ct);
        return t is null ? Result.Failure<MovimientosContables.TerceroResuelto?>(MovimientosContables.TerceroNoEncontrado) : Result.Success<MovimientosContables.TerceroResuelto?>(t);
    }

    private enum TipoDeNodo { Raiz, Cuenta, Tercero, Documento, Comprobante }

    /// <summary>
    /// El nodo ya leído. <c>Tercero</c> nulo con tipo Tercero/Documento significa la fila «Sin
    /// tercero»; <c>TipoDeCruce</c> y <c>NumeroDeCruce</c> nulos, «Sin documento».
    /// </summary>
    private sealed record Nodo(TipoDeNodo Tipo, string? Codigo, Guid? Tercero, string? TipoDeCruce, string? NumeroDeCruce, Guid? Comprobante)
    {
        public static Result<Nodo> Interpretar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return Result.Success(new Nodo(TipoDeNodo.Raiz, null, null, null, null, null));
            var separador = texto.IndexOf(':');
            if (separador <= 0 || separador == texto.Length - 1) return Result.Failure<Nodo>(NodoInvalido);
            var tipo = texto[..separador].Trim().ToLowerInvariant();
            var clave = texto[(separador + 1)..].Trim();

            switch (tipo)
            {
                case "account":
                    return clave.Contains('|') ? Result.Failure<Nodo>(NodoInvalido) : Result.Success(new Nodo(TipoDeNodo.Cuenta, clave, null, null, null, null));
                case "person":
                {
                    var partes = clave.Split('|');
                    if (partes.Length != 2 || string.IsNullOrWhiteSpace(partes[0]) || !LeerTercero(partes[1], out var tercero)) return Result.Failure<Nodo>(NodoInvalido);
                    return Result.Success(new Nodo(TipoDeNodo.Tercero, partes[0].Trim(), tercero, null, null, null));
                }
                case "document":
                {
                    // El número puede llevar «|» adentro: se parte en cuatro y el resto queda en el número.
                    var partes = clave.Split('|', 4);
                    if (partes.Length != 4 || string.IsNullOrWhiteSpace(partes[0]) || !LeerTercero(partes[1], out var tercero)) return Result.Failure<Nodo>(NodoInvalido);
                    var tipoCruce = EsNinguno(partes[2]) ? null : partes[2].Trim().ToUpperInvariant();
                    var numero = EsNinguno(partes[3]) ? null : partes[3].Trim();
                    return Result.Success(new Nodo(TipoDeNodo.Documento, partes[0].Trim(), tercero, tipoCruce, numero, null));
                }
                case "voucher":
                    return Guid.TryParse(clave, out var comprobante)
                        ? Result.Success(new Nodo(TipoDeNodo.Comprobante, null, null, null, null, comprobante))
                        : Result.Failure<Nodo>(NodoInvalido);
                default:
                    return Result.Failure<Nodo>(NodoInvalido);
            }
        }

        private static bool EsNinguno(string s) => string.IsNullOrWhiteSpace(s) || s.Trim().Equals(Ninguno, StringComparison.OrdinalIgnoreCase);

        private static bool LeerTercero(string s, out Guid? tercero)
        {
            tercero = null;
            if (EsNinguno(s)) return true;
            if (!Guid.TryParse(s.Trim(), out var id)) return false;
            tercero = id;
            return true;
        }
    }
}
