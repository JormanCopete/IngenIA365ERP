using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

// Las tres consultas que giran alrededor del tercero y del documento cruce (feature 009 E2, US5
// escenarios 7 y 8; FR-044 y FR-048; contracts/api.md §8): el estado de cuenta de un tercero, los
// documentos cruce con saldo pendiente y el saldo diario promedio de una cuenta. Ninguna guarda
// nada ni lee un saldo guardado (FR-046): todo es una suma sobre lo contabilizado que pasa por
// MovimientosContables, con el alcance de sucursal de quien consulta.

/// <summary>
/// Lo que comparten los tres validadores: nivel 1..6 y formato conocido, si vienen. Es local al
/// archivo (<c>file</c>) a propósito: cada archivo de consultas trae el suyo y ninguno choca con otro.
/// </summary>
file sealed class FiltrosValidos : AbstractValidator<FiltrosDeInforme>
{
    public const int NivelMinimo = 1;
    public const int NivelMaximo = 6;
    private static readonly string[] Formatos = ["json", "xlsx", "pdf", "docx"];

    public FiltrosValidos()
    {
        RuleFor(f => f.Level).InclusiveBetween(NivelMinimo, NivelMaximo).When(f => f.Level is not null)
            .WithMessage($"El nivel de detalle va de {NivelMinimo} a {NivelMaximo}.");
        RuleFor(f => f.Format).Must(f => Formatos.Contains(f!.Trim().ToLowerInvariant())).When(f => !string.IsNullOrWhiteSpace(f.Format))
            .WithMessage("El formato debe ser json, xlsx, pdf o docx.");
    }
}

/// <summary>Cómo se nombra una cuenta en secciones y filas: código y nombre.</summary>
file static class TextosDeInforme
{
    public static string Cuenta(string code, string name) => $"{code} {name}";

    /// <summary>«CG-12»; el número nunca falta en una línea contabilizada, pero el libro no se cae si faltara.</summary>
    public static string Comprobante(string tipo, long? numero) => numero is { } n ? $"{tipo}-{n}" : tipo;

    /// <summary>«FV 1001», «FV» o vacío cuando la línea no lleva documento cruce.</summary>
    public static string Cruce(string? tipo, string? numero) =>
        tipo is null ? string.Empty : numero is null ? tipo : $"{tipo} {numero}";

    /// <summary>Saldo con signo según la naturaleza: positivo cuando va con ella (nota de <see cref="EncabezadoDeInforme.NotaDeSignos"/>).</summary>
    public static decimal Saldo(AccountNature naturaleza, decimal debitos, decimal creditos) =>
        naturaleza == AccountNature.Debit ? debitos - creditos : creditos - debitos;
}

// ------------------------------------------------------------- estado de cuenta del tercero --

/// <summary>
/// Estado de cuenta de un tercero: sus movimientos del rango, por cuenta, con saldo corrido que
/// arranca en lo que traía antes del rango en esa cuenta. El tercero es obligatorio
/// (<see cref="MovimientosContables.TerceroRequerido"/>); uno dado de baja conserva su estado de
/// cuenta porque sus movimientos siguen en el libro. La reversa aparece como dos movimientos —el
/// original y su espejo— porque así está en el libro (decisión 7 del diseño E2). Una cuenta que
/// llega al rango saldada y no se mueve en él no sale: no hay nada que mostrar de ella.
/// </summary>
public sealed record ThirdPartyStatementQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class ThirdPartyStatementQueryValidator : AbstractValidator<ThirdPartyStatementQuery>
{
    public ThirdPartyStatementQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosValidos());
}

public sealed class ThirdPartyStatementQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ThirdPartyStatementQuery, Result<TablaExportable>>
{
    public const string Vista = "third-party-statement";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("Comprobante"),
        new("Cuenta"),
        new("Documento cruce"),
        new("Detalle"),
        new("Débito", TipoDeColumna.Moneda),
        new("Crédito", TipoDeColumna.Moneda),
        new("Saldo", TipoDeColumna.Moneda),
        new("Comprobante (id)", Clave: "_comprobante"),
        new("Cuenta (id)", Clave: "_cuenta"),
        new("Nodo", Clave: "_nodo"),
    ];

    public async Task<Result<TablaExportable>> Handle(ThirdPartyStatementQuery request, CancellationToken ct)
    {
        if (request.Filtros.Person is null) return Result.Failure<TablaExportable>(MovimientosContables.TerceroRequerido);

        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;
        var tercero = c.Tercero!;
        var q = MovimientosContables.Base(db, c);

        var iniciales = await MovimientosContables.Iniciales(q, c)
            .GroupBy(e => e.AccountId)
            .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) })
            .ToListAsync(ct);
        var lineas = await MovimientosContables.DelRango(q, c)
            .Select(e => new Linea(e.AccountId, e.Date, e.Document!.VoucherType!.Code, e.Document!.Number, e.LineNumber, e.Document!.PublicId,
                e.CrossDocumentType != null ? e.CrossDocumentType.Code : null, e.CrossDocumentNumber, e.Description, e.Debit, e.Credit))
            .ToListAsync(ct);

        var idsDeCuenta = iniciales.Select(i => i.Key).Concat(lineas.Select(l => l.AccountId)).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsDeCuenta.Contains(a.Id))
            .Select(a => new { a.Id, a.PublicId, a.Code, a.Name, a.Nature })
            .ToDictionaryAsync(a => a.Id, ct);

        var filas = new List<FilaExportable>();
        decimal totalDebitos = 0m, totalCreditos = 0m;
        foreach (var id in idsDeCuenta.OrderBy(id => cuentas.TryGetValue(id, out var a) ? a.Code : string.Empty, StringComparer.Ordinal))
        {
            // Una cuenta que ya no esté en el plan no debería pasar (el asiento la referencia); si pasa, la fila no se pierde.
            var cuenta = cuentas.GetValueOrDefault(id);
            var codigo = cuenta?.Code ?? id.ToString();
            var naturaleza = cuenta?.Nature ?? JerarquiaDelPlan.NaturalezaDeClase(codigo);
            var seccion = cuenta is null ? codigo : TextosDeInforme.Cuenta(cuenta.Code, cuenta.Name);
            var cuentaPublicId = cuenta?.PublicId.ToString();
            var nodo = $"person:{codigo}|{tercero.PublicId}";

            var inicial = iniciales.FirstOrDefault(i => i.Key == id);
            var saldo = TextosDeInforme.Saldo(naturaleza, inicial?.D ?? 0m, inicial?.C ?? 0m);
            var delRango = lineas.Where(l => l.AccountId == id).OrderBy(l => l.Date).ThenBy(l => l.Tipo, StringComparer.Ordinal).ThenBy(l => l.Numero).ThenBy(l => l.LineNumber).ToList();

            // Una cuenta que el tercero tocó alguna vez pero que llega al rango saldada y sin
            // movimiento (el crédito ya cancelado, los aportes ya devueltos) no dice nada: con un
            // asociado antiguo el estado de cuenta se llenaba de secciones con una sola fila
            // «Saldo inicial 0». Se conserva la sección si trae saldo o si hay movimiento en el rango.
            if (saldo == 0m && delRango.Count == 0) continue;

            filas.Add(new FilaExportable([c.Desde.AddDays(-1), string.Empty, codigo, string.Empty, "Saldo inicial", null, null, saldo, null, cuentaPublicId, nodo], seccion, Resaltada: true));

            foreach (var l in delRango)
            {
                saldo += TextosDeInforme.Saldo(naturaleza, l.Debito, l.Credito);
                totalDebitos += l.Debito;
                totalCreditos += l.Credito;
                filas.Add(new FilaExportable(
                    [l.Date, TextosDeInforme.Comprobante(l.Tipo, l.Numero), codigo, TextosDeInforme.Cruce(l.TipoDeCruce, l.NumeroDeCruce), l.Detalle ?? string.Empty,
                     l.Debito, l.Credito, saldo, l.DocumentoPublicId.ToString(), cuentaPublicId, nodo],
                    seccion));
            }
        }

        var totales = new FilaExportable(["Totales", string.Empty, string.Empty, string.Empty, string.Empty, totalDebitos, totalCreditos, null, null, null, null], Resaltada: true);
        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        notas.Add("La primera fila de cada cuenta es el saldo del tercero al día anterior al rango (incluida la apertura); el saldo corrido va según la naturaleza de la cuenta. Una cuenta saldada y sin movimiento en el rango no se lista.");
        var tabla = new TablaExportable($"Estado de cuenta · {tercero.Nombre} ({tercero.TaxId})", c.PeriodoTexto, Columnas, filas, totales, notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }

    private sealed record Linea(int AccountId, DateOnly Date, string Tipo, long? Numero, int LineNumber, Guid DocumentoPublicId,
        string? TipoDeCruce, string? NumeroDeCruce, string? Detalle, decimal Debito, decimal Credito);
}

// ------------------------------------------------------------------- documentos pendientes --

/// <summary>
/// Documentos cruce con saldo pendiente a la fecha final: todo lo acumulado hasta <c>To</c>
/// (no sólo el rango: un documento abierto en enero sigue pendiente en marzo), agrupado por
/// cuenta, tercero, tipo y número, dejando sólo lo que no quedó en cero. Un documento pagado por
/// completo no sale. El tercero es opcional: sin él, la cartera o las cuentas por pagar de todos.
/// </summary>
public sealed record PendingDocumentsQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class PendingDocumentsQueryValidator : AbstractValidator<PendingDocumentsQuery>
{
    public PendingDocumentsQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosValidos());
}

public sealed class PendingDocumentsQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<PendingDocumentsQuery, Result<TablaExportable>>
{
    public const string Vista = "pending-documents";
    public const string SinTercero = "Sin tercero";
    private const string Ninguno = "none";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Cuenta"),
        new("Tercero"),
        new("Documento"),
        new("Número"),
        new("Fecha primer mov.", TipoDeColumna.Fecha),
        new("Débitos", TipoDeColumna.Moneda),
        new("Créditos", TipoDeColumna.Moneda),
        new("Saldo", TipoDeColumna.Moneda),
        new("Nodo", Clave: "_nodo"),
        new("Cuenta (id)", Clave: "_cuenta"),
    ];

    public async Task<Result<TablaExportable>> Handle(PendingDocumentsQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        // Sólo las líneas que llevan documento cruce forman un documento; las demás no tienen nada que quedar pendiente.
        var q = MovimientosContables.HastaInclusive(MovimientosContables.Base(db, c), c.Hasta).Where(e => e.CrossDocumentTypeId != null);
        var grupos = await q
            .GroupBy(e => new { e.AccountId, e.PersonId, TipoId = e.CrossDocumentTypeId!.Value, e.CrossDocumentNumber })
            .Select(g => new { g.Key.AccountId, g.Key.PersonId, g.Key.TipoId, Numero = g.Key.CrossDocumentNumber, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit), Primera = g.Min(e => e.Date) })
            .ToListAsync(ct);

        var idsDeCuenta = grupos.Select(g => g.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsDeCuenta.Contains(a.Id))
            .Select(a => new { a.Id, a.PublicId, a.Code, a.Name, a.Nature })
            .ToDictionaryAsync(a => a.Id, ct);
        var idsDeTercero = grupos.Where(g => g.PersonId != null).Select(g => g.PersonId!.Value).Distinct().ToList();
        // Un tercero dado de baja sigue debiendo o acreedor: se nombra igual.
        var terceros = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDeTercero.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, Nombre = PersonFactory.NombreVisible(p.FirstName, p.LastName, p.BusinessName) })
            .ToDictionaryAsync(p => p.Id, ct);
        var idsDeTipo = grupos.Select(g => g.TipoId).Distinct().ToList();
        var tipos = await db.CrossDocumentTypes.AsNoTracking().Where(t => idsDeTipo.Contains(t.Id)).Select(t => new { t.Id, t.Code }).ToDictionaryAsync(t => t.Id, t => t.Code, ct);

        var pendientes = grupos
            .Select(g =>
            {
                var cuenta = cuentas.GetValueOrDefault(g.AccountId);
                var codigo = cuenta?.Code ?? g.AccountId.ToString();
                var naturaleza = cuenta?.Nature ?? JerarquiaDelPlan.NaturalezaDeClase(codigo);
                var tercero = g.PersonId is { } p ? terceros.GetValueOrDefault(p) : null;
                return new
                {
                    Codigo = codigo,
                    Seccion = cuenta is null ? codigo : TextosDeInforme.Cuenta(cuenta.Code, cuenta.Name),
                    CuentaPublicId = cuenta?.PublicId.ToString(),
                    Tercero = tercero?.Nombre ?? SinTercero,
                    TerceroClave = tercero?.PublicId.ToString() ?? Ninguno,
                    Tipo = tipos.GetValueOrDefault(g.TipoId) ?? g.TipoId.ToString(),
                    g.Numero,
                    g.Primera,
                    g.D,
                    g.C,
                    Saldo = TextosDeInforme.Saldo(naturaleza, g.D, g.C),
                };
            })
            .Where(x => x.Saldo != 0m)
            .OrderBy(x => x.Codigo, StringComparer.Ordinal).ThenBy(x => x.Tercero, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Tipo, StringComparer.Ordinal).ThenBy(x => x.Numero, StringComparer.Ordinal)
            .ToList();

        var filas = pendientes.Select(x => new FilaExportable(
            [x.Codigo, x.Tercero, x.Tipo, x.Numero ?? string.Empty, x.Primera, x.D, x.C, x.Saldo,
             $"document:{x.Codigo}|{x.TerceroClave}|{x.Tipo}|{x.Numero ?? Ninguno}", x.CuentaPublicId],
            x.Seccion)).ToList();
        var totales = new FilaExportable(["Totales", string.Empty, string.Empty, string.Empty, null, pendientes.Sum(x => x.D), pendientes.Sum(x => x.C), pendientes.Sum(x => x.Saldo), null, null], Resaltada: true);
        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        notas.Add($"Saldos acumulados hasta el {c.Hasta:dd/MM/yyyy} (todo lo contabilizado hasta esa fecha, no sólo el rango); un documento saldado no aparece.");
        var titulo = c.Tercero is { } t ? $"Documentos cruce pendientes · {t.Nombre} ({t.TaxId})" : "Documentos cruce con saldo pendiente";
        var tabla = new TablaExportable(titulo, $"Al {c.Hasta:dd/MM/yyyy}", Columnas, filas, totales, notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }
}

// ----------------------------------------------------------------- saldo diario promedio --

/// <summary>
/// Saldo diario promedio de una cuenta (o rama) en un rango (FR-048): el saldo al día anterior al
/// rango y luego cada día, tenga o no movimiento, con sus débitos, créditos y saldo; el promedio
/// es la media simple de los saldos diarios. Reemplaza la tabla y la pantalla de «categorías de
/// riesgo» del legado, que guardaban el saldo de cada día: aquí se calcula desde los movimientos.
/// La cuenta es obligatoria (<see cref="MovimientosContables.CuentaRequerida"/>).
/// </summary>
public sealed record DailyAverageBalanceQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class DailyAverageBalanceQueryValidator : AbstractValidator<DailyAverageBalanceQuery>
{
    public DailyAverageBalanceQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosValidos());
}

public sealed class DailyAverageBalanceQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<DailyAverageBalanceQuery, Result<TablaExportable>>
{
    public const string Vista = "daily-average";
    public const int RangoMaximoEnAnios = 1;

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha),
        new("Débitos", TipoDeColumna.Moneda),
        new("Créditos", TipoDeColumna.Moneda),
        new("Saldo", TipoDeColumna.Moneda),
    ];

    public async Task<Result<TablaExportable>> Handle(DailyAverageBalanceQuery request, CancellationToken ct)
    {
        if (request.Filtros.AccountPublicId is null) return Result.Failure<TablaExportable>(MovimientosContables.CuentaRequerida);

        // Un año como mucho: el informe produce una fila por día del rango y el promedio es de un período.
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct, rangoMaximoEnAnios: RangoMaximoEnAnios);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;
        var cuenta = c.Cuenta!;
        var q = MovimientosContables.Base(db, c);

        var iniciales = MovimientosContables.Iniciales(q, c);
        var debitoInicial = await iniciales.SumAsync(e => e.Debit, ct);
        var creditoInicial = await iniciales.SumAsync(e => e.Credit, ct);
        var porDia = await MovimientosContables.DelRango(q, c)
            .GroupBy(e => e.Date)
            .Select(g => new { Fecha = g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) })
            .ToDictionaryAsync(x => x.Fecha, ct);

        var saldo = TextosDeInforme.Saldo(cuenta.Nature, debitoInicial, creditoInicial);
        var filas = new List<FilaExportable> { new([c.Desde.AddDays(-1), null, null, saldo], "Saldo inicial", Resaltada: true) };
        decimal debitos = 0m, creditos = 0m, sumaDeSaldos = 0m;
        var dias = 0;
        for (var dia = c.Desde; dia <= c.Hasta; dia = dia.AddDays(1))
        {
            var mov = porDia.GetValueOrDefault(dia);
            var d = mov?.D ?? 0m;
            var cr = mov?.C ?? 0m;
            saldo += TextosDeInforme.Saldo(cuenta.Nature, d, cr);
            debitos += d;
            creditos += cr;
            sumaDeSaldos += saldo;
            dias++;
            filas.Add(new FilaExportable([dia, d, cr, saldo], "Saldo diario"));
        }

        var promedio = dias == 0 ? 0m : Math.Round(sumaDeSaldos / dias, 2, MidpointRounding.AwayFromZero);
        var totales = new FilaExportable(["Saldo promedio", debitos, creditos, promedio], Resaltada: true);
        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        notas.Add($"Saldo promedio = suma de los saldos de cada uno de los {dias} días del rango (con o sin movimiento) dividida entre {dias}; calculado desde los movimientos contabilizados, sin ninguna tabla de saldos (FR-048).");
        var tabla = new TablaExportable($"Saldo diario promedio · {TextosDeInforme.Cuenta(cuenta.Code, cuenta.Name)}", c.PeriodoTexto, Columnas, filas, totales, notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }
}
