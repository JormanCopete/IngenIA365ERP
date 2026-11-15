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
/// <item><c>voucher:&lt;documentPublicId&gt;</c> → todas las líneas del comprobante;</item>
/// <item><c>entries:&lt;código&gt;</c> → las líneas de una cuenta de movimiento con saldo inicial y corrido (2026-11-15; con
/// <see cref="FiltrosDeInforme.OnlyMovement"/> la raíz es la lista plana de cuentas de movimiento y cada una abre aquí).</item>
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
        "El nodo del libro auxiliar no tiene la forma esperada: account:<código>, entries:<código>, person:<código>|<tercero>, document:<código>|<tercero>|<tipo>|<número> o voucher:<comprobante>.");

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

        var soloMovimiento = request.Filtros.OnlyMovement;
        Result<Nivel> nivel = nodo.Value.Tipo switch
        {
            TipoDeNodo.Raiz when soloMovimiento => await CuentasDeMovimientoAsync(q, c, plan, ct),
            TipoDeNodo.Raiz => await CuentasAsync(q, c, plan, cuenta: null, ct),
            TipoDeNodo.Cuenta => await DesdeCuentaAsync(q, c, plan, nodo.Value, soloMovimiento, ct),
            TipoDeNodo.Lineas => await LineasDeCuentaAsync(q, c, plan, nodo.Value, request.Filtros.Columns, ct),
            TipoDeNodo.Tercero => await DocumentosAsync(q, c, plan, nodo.Value, ct),
            TipoDeNodo.Documento => await ComprobantesAsync(q, c, plan, nodo.Value, ct),
            TipoDeNodo.Comprobante => await LineasAsync(c, nodo.Value, ct),
            _ => Result.Failure<Nivel>(NodoInvalido),
        };
        if (nivel.IsFailure) return Result.Failure<TablaExportable>(nivel.Error);

        var n = nivel.Value;
        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        // La misma nota que deja el balance de prueba en la misma situación: filas con saldo que no se suman.
        if (n.Saldos == SaldosDelTotal.NoSumables && n.Filas.Any(f => f.Valores[2] is decimal))
            notas.Add("Totales: suma de débitos y créditos del período; los saldos no se suman porque mezclan naturalezas.");
        var tabla = new TablaExportable(
            string.Join(" · ", new[] { "Libro auxiliar" }.Concat(n.Camino)),
            $"{n.Descripcion} · {c.PeriodoTexto}",
            n.Columnas ?? Columnas, n.Filas, n.Total ?? Totales(n), notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Informe, new { filtros = request.Filtros, node = request.Node }, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }

    // ------------------------------------------------------------------ niveles --

    /// <summary>
    /// Cómo se llenan las celdas de saldo de la fila «Total» de un nivel. Débitos y créditos se suman
    /// siempre; los saldos sólo cuando sumarlos significa algo:
    /// <list type="bullet">
    /// <item><see cref="Sumables"/>: las filas comparten naturaleza (hijas de una misma cuenta, terceros o
    /// documentos de una cuenta), así que Σ saldo inicial y Σ saldo final son el saldo del padre;</item>
    /// <item><see cref="NoSumables"/>: las filas mezclan naturalezas (las clases en la raíz) o no llevan
    /// saldo (las líneas de un comprobante): las celdas van vacías, como en el balance de prueba;</item>
    /// <item><see cref="Corridos"/>: cada fila lleva el saldo corrido antes y después de su comprobante,
    /// así que sumarlos multiplica el saldo por el número de filas; el total lleva el saldo inicial de
    /// la combinación y el saldo final de la última fila (<see cref="Nivel.SaldoInicial"/>, <see cref="Nivel.SaldoFinal"/>).</item>
    /// </list>
    /// </summary>
    private enum SaldosDelTotal { Sumables, NoSumables, Corridos }

    /// <summary>
    /// Lo que devuelve un nodo: el camino legible para el título, qué se está viendo, sus filas y cómo se
    /// totalizan sus saldos. <paramref name="SaldoInicial"/> y <paramref name="SaldoFinal"/> sólo cuentan
    /// con <see cref="SaldosDelTotal.Corridos"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="Columnas"/> y <paramref name="Total"/> sólo los trae el nivel de las líneas de una cuenta de movimiento
    /// (<see cref="LineasDeCuentaAsync"/>), cuyas columnas elige quien consulta; los demás usan las fijas del libro.
    /// </remarks>
    private sealed record Nivel(
        IReadOnlyList<string> Camino, string Descripcion, IReadOnlyList<FilaExportable> Filas,
        SaldosDelTotal Saldos = SaldosDelTotal.Sumables, decimal SaldoInicial = 0m, decimal SaldoFinal = 0m,
        IReadOnlyList<ColumnaExportable>? Columnas = null, FilaExportable? Total = null);

    private async Task<Result<Nivel>> DesdeCuentaAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, Nodo nodo, bool soloMovimiento, CancellationToken ct)
    {
        var cuenta = plan.PorCodigo(nodo.Codigo!);
        if (cuenta is null) return Result.Failure<Nivel>(MovimientosContables.CuentaNoEncontrada);
        // Con hijas en el plan se sigue por el plan; sin hijas (la de movimiento, o una de agrupación
        // a la que la empresa no le creó hijas) se abre por terceros: no hay otro sitio donde bajar.
        // Con «sólo cuentas de movimiento» la de movimiento se abre en sus líneas, no por terceros.
        if (plan.TieneHijos(cuenta.Id)) return await CuentasAsync(q, c, plan, cuenta, ct);
        return soloMovimiento
            ? await LineasDeCuentaAsync(q, c, plan, nodo with { Tipo = TipoDeNodo.Lineas }, c.Filtros.Columns, ct)
            : await TercerosAsync(q, c, plan, cuenta, ct);
    }

    /// <summary>
    /// 2026-11-15 (pedido del dueño): la lista plana de las cuentas de movimiento con saldo o movimiento, sin las de agrupación,
    /// en orden de código; cada una se abre en sus líneas (<c>entries:&lt;código&gt;</c>).
    /// </summary>
    private static async Task<Result<Nivel>> CuentasDeMovimientoAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, CancellationToken ct)
    {
        var porCuenta = new Dictionary<int, SumasDeCuenta>();
        foreach (var p in await MovimientosContables.DelRango(q, c).GroupBy(e => e.AccountId)
                     .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
            porCuenta[p.Key] = porCuenta.GetValueOrDefault(p.Key, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(0m, 0m, p.D, p.C));
        foreach (var i in await MovimientosContables.Iniciales(q, c).GroupBy(e => e.AccountId)
                     .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct))
            porCuenta[i.Key] = porCuenta.GetValueOrDefault(i.Key, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));

        var filas = porCuenta
            .Where(x => !x.Value.EsCero)
            .Select(x => (Cuenta: plan.PorId(x.Key), Sumas: x.Value))
            .Where(x => x.Cuenta is not null)
            .OrderBy(x => x.Cuenta!.Code, StringComparer.Ordinal)
            .Select(x => Fila(x.Cuenta!.Code, x.Cuenta.Name, x.Sumas, x.Cuenta.Nature, $"entries:{x.Cuenta.Code}", "entries", x.Cuenta.PublicId, null))
            .ToList();

        // Puede mezclar naturalezas (una de activo y otra de pasivo en el mismo rango): los saldos no se suman.
        return Result.Success(new Nivel([], "Cuentas de movimiento", filas, SaldosDelTotal.NoSumables));
    }

    /// <summary>
    /// 2026-11-15 (pedido del dueño): las líneas de una cuenta de movimiento en el rango, en orden de fecha y comprobante, con
    /// la fila «Saldo inicial» primero y el saldo corrido en cada línea; las columnas opcionales (fecha, tercero, centro de
    /// costo, documento cruce…) son las que eligió quien consulta (<see cref="Domain.Accounting.ColumnasDelLibroAuxiliar"/>).
    /// Cada línea lleva oculto su comprobante para que la pantalla lo abra.
    /// </summary>
    private async Task<Result<Nivel>> LineasDeCuentaAsync(IQueryable<JournalEntry> q, MovimientosContables.Contexto c, JerarquiaDelPlan plan, Nodo nodo, string? columnas, CancellationToken ct)
    {
        var cuenta = plan.PorCodigo(nodo.Codigo!);
        if (cuenta is null) return Result.Failure<Nivel>(MovimientosContables.CuentaNoEncontrada);
        var enCuenta = q.Where(e => e.AccountId == cuenta.Id);

        var inicial = await MovimientosContables.Iniciales(enCuenta, c)
            .GroupBy(_ => 1).Select(g => new { D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).FirstOrDefaultAsync(ct);
        var lineas = await MovimientosContables.DelRango(enCuenta, c)
            .Select(e => new
            {
                e.Id, e.Date, e.LineNumber, e.Debit, e.Credit, e.Description, e.CrossDocumentNumber,
                Cruce = e.CrossDocumentType != null ? e.CrossDocumentType.Code : null,
                PersonaNombre = e.Person != null ? e.Person.FirstName : null,
                PersonaOtrosNombres = e.Person != null ? e.Person.OtherNames : null,
                PersonaApellido = e.Person != null ? e.Person.LastName : null,
                PersonaSegundoApellido = e.Person != null ? e.Person.SecondLastName : null,
                PersonaRazon = e.Person != null ? e.Person.BusinessName : null,
                PersonaDocumento = e.Person != null ? e.Person.TaxId : null,
                Centro = e.CostCenter != null ? (e.CostCenter.LegacyCode != null ? e.CostCenter.LegacyCode + " " + e.CostCenter.Name : e.CostCenter.Name) : null,
                Sucursal = e.Branch != null ? e.Branch.Name : null,
                Documento = new { e.Document!.PublicId, e.Document.Number, Tipo = e.Document.VoucherType!.Code },
            })
            .ToListAsync(ct);

        var elegidas = Domain.Accounting.ColumnasDelLibroAuxiliar.Normalizar(
            columnas?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var nombres = Domain.Accounting.ColumnasDelLibroAuxiliar.Todas.ToDictionary(x => x.Codigo, x => x.Nombre, StringComparer.Ordinal);
        var cols = new List<ColumnaExportable> { new("Comprobante") };
        cols.AddRange(elegidas.Select(k => new ColumnaExportable(nombres[k], k == Domain.Accounting.ColumnasDelLibroAuxiliar.Fecha ? TipoDeColumna.Fecha : TipoDeColumna.Texto)));
        cols.AddRange([
            new("Débito", TipoDeColumna.Moneda), new("Crédito", TipoDeColumna.Moneda), new("Saldo", TipoDeColumna.Moneda),
            new("Nodo", Clave: "_nodo"), new("Tipo", Clave: "_tipo"), new("Cuenta", Clave: "_cuenta"), new("Comprobante", Clave: "_comprobante"),
        ]);

        var saldoInicial = new SumasDeCuenta(inicial?.D ?? 0m, inicial?.C ?? 0m, 0m, 0m).SaldoInicial(cuenta.Nature);
        object?[] Vacias() => elegidas.Select(_ => (object?)null).ToArray();
        var filas = new List<FilaExportable>
        {
            new([.. new object?[] { "Saldo inicial" }, .. Vacias(), null, null, saldoInicial, null, null, cuenta.PublicId, null], Resaltada: true),
        };

        var saldo = saldoInicial;
        foreach (var l in lineas.OrderBy(l => l.Date).ThenBy(l => l.Documento.Tipo, StringComparer.Ordinal).ThenBy(l => l.Documento.Number).ThenBy(l => l.LineNumber).ThenBy(l => l.Id))
        {
            saldo += cuenta.Nature == AccountNature.Debit ? l.Debit - l.Credit : l.Credit - l.Debit;
            var tercero = l.PersonaNombre is null && l.PersonaApellido is null && l.PersonaRazon is null
                ? string.Empty
                : PersonFactory.NombreVisible(l.PersonaNombre, l.PersonaOtrosNombres, l.PersonaApellido, l.PersonaSegundoApellido, l.PersonaRazon);
            object? Valor(string k) => k switch
            {
                Domain.Accounting.ColumnasDelLibroAuxiliar.Fecha => l.Date,
                Domain.Accounting.ColumnasDelLibroAuxiliar.Tercero => tercero,
                Domain.Accounting.ColumnasDelLibroAuxiliar.Documento => l.PersonaDocumento ?? string.Empty,
                Domain.Accounting.ColumnasDelLibroAuxiliar.CentroDeCosto => l.Centro ?? string.Empty,
                Domain.Accounting.ColumnasDelLibroAuxiliar.DocumentoCruce => LibrosQueries.Cruce(l.Cruce, l.CrossDocumentNumber),
                Domain.Accounting.ColumnasDelLibroAuxiliar.Descripcion => l.Description ?? string.Empty,
                Domain.Accounting.ColumnasDelLibroAuxiliar.Sucursal => l.Sucursal ?? string.Empty,
                _ => null,
            };
            filas.Add(new FilaExportable([
                .. new object?[] { $"{l.Documento.Tipo}-{l.Documento.Number}" }, .. elegidas.Select(Valor),
                l.Debit, l.Credit, saldo, null, "voucher", cuenta.PublicId, l.Documento.PublicId]));
        }

        var total = new FilaExportable([
            .. new object?[] { "Saldo final" }, .. Vacias(), lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit), saldo, null, null, null, null], Resaltada: true);
        return Result.Success(new Nivel([Etiqueta(cuenta)], $"Movimientos ({lineas.Count} línea(s))", filas,
            SaldosDelTotal.Corridos, saldoInicial, saldo, cols, total));
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

        // En la raíz cada clase trae su saldo «positivo según naturaleza»: activo + pasivo + patrimonio
        // no es un saldo de nada. Las hijas de una cuenta heredan su naturaleza y sí se suman.
        return Result.Success(new Nivel(
            cuenta is null ? [] : [Etiqueta(cuenta)],
            cuenta is null ? "Clases del plan" : $"Cuentas de nivel {cuenta.Level + 1}",
            filas,
            cuenta is null ? SaldosDelTotal.NoSumables : SaldosDelTotal.Sumables));
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
                .Select(p => new MovimientosContables.TerceroResuelto(p.Id, p.PublicId, PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), p.TaxId))
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

        var saldoInicial = new SumasDeCuenta(inicial?.D ?? 0m, inicial?.C ?? 0m, 0m, 0m).SaldoInicial(cuenta.Nature);
        var saldo = saldoInicial;
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
        // Tras el bucle, «saldo» es el saldo corrido después del último comprobante: el final de la combinación.
        return Result.Success(new Nivel([Etiqueta(cuenta), Etiqueta(tercero.Value), documento], "Comprobantes", filas,
            SaldosDelTotal.Corridos, saldoInicial, saldo));
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
                Tercero = e.Person == null ? null : PersonFactory.NombreVisible(e.Person.FirstName, e.Person.OtherNames, e.Person.LastName, e.Person.SecondLastName, e.Person.BusinessName),
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

        return Result.Success(new Nivel([$"{doc.Tipo}-{doc.Number}", $"{doc.Date:dd/MM/yyyy} {doc.Description}"], "Líneas del comprobante", filas, SaldosDelTotal.NoSumables));
    }

    // ------------------------------------------------------------------ apoyo --

    private static FilaExportable Fila(string codigo, string nombre, SumasDeCuenta s, AccountNature naturaleza, string nodo, string tipo, Guid? cuenta, Guid? comprobante) =>
        new([codigo, nombre, s.SaldoInicial(naturaleza), s.Debitos, s.Creditos, s.SaldoFinal(naturaleza), nodo, tipo, cuenta, comprobante]);

    /// <summary>
    /// La fila «Total» del nivel: Σ débitos y Σ créditos siempre; los saldos según lo que el nivel
    /// declaró en <see cref="Nivel.Saldos"/>. Hasta el 2026-09-20 se sumaban en todos los niveles: en la
    /// raíz mezclaba activo + pasivo + patrimonio y en los comprobantes sumaba saldos corridos (tres
    /// comprobantes de 50 sobre un inicial de 100 daban «saldo inicial 450, final 600» en vez de 100 y 250).
    /// </summary>
    private static FilaExportable? Totales(Nivel n)
    {
        if (n.Filas.Count == 0) return null;
        decimal Suma(int i) => n.Filas.Sum(f => f.Valores[i] as decimal? ?? 0m);
        var (inicial, final) = n.Saldos switch
        {
            SaldosDelTotal.Sumables => ((decimal?)Suma(2), (decimal?)Suma(5)),
            SaldosDelTotal.Corridos => (n.SaldoInicial, n.SaldoFinal),
            _ => (null, null),
        };
        return new FilaExportable(["Total", string.Empty, inicial, Suma(3), Suma(4), final, null, null, null, null], Resaltada: true);
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
            .Select(p => new MovimientosContables.TerceroResuelto(p.Id, p.PublicId, PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), p.TaxId))
            .FirstOrDefaultAsync(ct);
        return t is null ? Result.Failure<MovimientosContables.TerceroResuelto?>(MovimientosContables.TerceroNoEncontrado) : Result.Success<MovimientosContables.TerceroResuelto?>(t);
    }

    private enum TipoDeNodo { Raiz, Cuenta, Tercero, Documento, Comprobante, Lineas }

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
                case "entries":
                    return clave.Contains('|') ? Result.Failure<Nodo>(NodoInvalido) : Result.Success(new Nodo(TipoDeNodo.Lineas, clave, null, null, null, null));
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
