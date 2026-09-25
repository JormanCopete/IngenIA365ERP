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

/// <summary>
/// Los cuatro libros clásicos (feature 009 E2, FR-044): balance de prueba, libro diario, libro
/// mayor y balances y relación de comprobantes. Todos leen el libro por
/// <see cref="MovimientosContables"/> —sólo asientos contabilizados, con el alcance de sucursal y
/// los filtros de <see cref="FiltrosDeInforme"/>— y ninguno guarda nada: cada saldo es una suma en el
/// momento de la consulta (FR-046). Lo que comparten (sumas por cuenta, filas por nivel, nombres
/// de estado) vive en esta clase para que los cuatro digan lo mismo con los mismos números.
///
/// <para>
/// Tres reglas de fechas que las pruebas fijan: la <b>reversa</b> deja el original y su espejo,
/// los dos cuentan y se netean (decisión 7); el <b>cierre</b> sólo entra con <c>IncludeClosing</c>
/// (decisión 8); la <b>apertura</b> es siempre saldo inicial aunque su fecha caiga en el rango
/// (decisión 9), por eso el diario y la relación de comprobantes, que recorren el período, no la
/// listan: no es un movimiento del período sino el punto de partida.
/// </para>
/// </summary>
public static class LibrosQueries
{
    public const string ClaveNodo = "_nodo";
    public const string ClaveCuenta = "_cuenta";
    public const string ClaveComprobante = "_comprobante";

    /// <summary>Nivel por defecto del libro mayor y balances: el de cuenta (clase, grupo, cuenta, subcuenta).</summary>
    public const int NivelDeLibroMayor = 4;

    /// <summary>Nivel más profundo que admite el plan (FR-013: seis niveles).</summary>
    public const int NivelMaximo = 6;

    private static readonly string[] FormatosAdmitidos = ["json", "xlsx", "pdf", "docx"];

    /// <summary>Los formatos que la API entrega (<c>EntregaDeInformes</c>): json en pantalla y tres archivos.</summary>
    public static bool FormatoAdmitido(string? formato) =>
        string.IsNullOrWhiteSpace(formato) || FormatosAdmitidos.Contains(formato.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string NombreEstado(DocumentStatus estado) => estado switch
    {
        DocumentStatus.Draft => "Borrador",
        DocumentStatus.Posted => "Contabilizado",
        DocumentStatus.Reversed => "Reversado",
        _ => estado.ToString(),
    };

    public static string NombreClase(DocumentKind kind) => kind switch
    {
        DocumentKind.Regular => string.Empty,
        DocumentKind.Opening => "Apertura",
        DocumentKind.Closing => "Cierre",
        DocumentKind.Reversal => "Reversión",
        _ => kind.ToString(),
    };

    public static string Nodo(string codigoDeCuenta) => $"account:{codigoDeCuenta}";

    /// <summary>«FV 123», «FV» o vacío: el documento cruce como se lee en una fila.</summary>
    public static string Cruce(string? tipo, string? numero) =>
        tipo is null ? string.Empty : numero is null ? tipo : $"{tipo} {numero}";

    /// <summary>
    /// Débitos y créditos del período e iniciales por cuenta de movimiento, sobre lo que pasa los
    /// filtros. Dos consultas agrupadas, nunca las líneas una a una: un año de nómina son miles de
    /// asientos y el balance se pide cada rato.
    /// </summary>
    public static async Task<Dictionary<int, SumasDeCuenta>> SumasPorCuentaAsync(IApplicationDbContext db, MovimientosContables.Contexto c, CancellationToken ct)
    {
        var q = MovimientosContables.Base(db, c);
        var periodo = await MovimientosContables.DelRango(q, c).GroupBy(e => e.AccountId)
            .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);
        var iniciales = await MovimientosContables.Iniciales(q, c).GroupBy(e => e.AccountId)
            .Select(g => new { g.Key, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);

        var sumas = new Dictionary<int, SumasDeCuenta>();
        foreach (var p in periodo) sumas[p.Key] = new SumasDeCuenta(0m, 0m, p.D, p.C);
        foreach (var i in iniciales) sumas[i.Key] = sumas.GetValueOrDefault(i.Key, SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));
        return sumas;
    }

    /// <summary>Lo mismo que <see cref="SumasPorCuentaAsync"/> pero abierto por tercero (nulo = sin tercero), para «con terceros».</summary>
    public static async Task<Dictionary<(int CuentaId, int? PersonaId), SumasDeCuenta>> SumasPorCuentaYTerceroAsync(IApplicationDbContext db, MovimientosContables.Contexto c, CancellationToken ct)
    {
        var q = MovimientosContables.Base(db, c);
        var periodo = await MovimientosContables.DelRango(q, c).GroupBy(e => new { e.AccountId, e.PersonId })
            .Select(g => new { g.Key.AccountId, g.Key.PersonId, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);
        var iniciales = await MovimientosContables.Iniciales(q, c).GroupBy(e => new { e.AccountId, e.PersonId })
            .Select(g => new { g.Key.AccountId, g.Key.PersonId, D = g.Sum(e => e.Debit), C = g.Sum(e => e.Credit) }).ToListAsync(ct);

        var sumas = new Dictionary<(int, int?), SumasDeCuenta>();
        foreach (var p in periodo) sumas[(p.AccountId, p.PersonId)] = new SumasDeCuenta(0m, 0m, p.D, p.C);
        foreach (var i in iniciales) sumas[(i.AccountId, i.PersonId)] = sumas.GetValueOrDefault((i.AccountId, i.PersonId), SumasDeCuenta.Cero).Mas(new SumasDeCuenta(i.D, i.C, 0m, 0m));
        return sumas;
    }

    /// <summary>
    /// El nivel de movimiento de la empresa (<c>AccountingSetup.MovementLevel</c>); si la
    /// contabilidad no está iniciada no hay asientos, y se cae al nivel más profundo del plan para
    /// que la consulta devuelva una tabla vacía en vez de un error.
    /// </summary>
    public static async Task<int> NivelDeMovimientoAsync(IApplicationDbContext db, JerarquiaDelPlan plan, CancellationToken ct)
    {
        var nivel = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => (int?)s.MovementLevel).FirstOrDefaultAsync(ct);
        if (nivel is { } n && n > 0) return n;
        return plan.Cuentas.Count == 0 ? NivelMaximo : plan.Cuentas.Max(a => (int)a.Level);
    }

    /// <summary>Naturaleza con la que se lee el saldo: la de la cuenta si está en el plan, la de su clase si no.</summary>
    public static AccountNature Naturaleza(JerarquiaDelPlan.Cuenta? cuenta, string codigo) =>
        cuenta?.Nature ?? JerarquiaDelPlan.NaturalezaDeClase(codigo);

    /// <summary>La clase (nivel 1) a la que pertenece una cuenta, como texto de sección.</summary>
    public static string SeccionDeClase(JerarquiaDelPlan plan, JerarquiaDelPlan.Cuenta cuenta)
    {
        var clase = plan.AlNivel(cuenta, 1);
        return $"{clase.Code} {clase.Name}";
    }

    /// <summary>
    /// Las filas de un balance por niveles: cada cuenta del plan hasta <paramref name="nivel"/>
    /// con movimiento o saldo, en orden de código, con la clase como sección. Las cuentas que ya
    /// no están en el plan (eliminadas con saldo: no debería pasar, pero el libro las tiene) van
    /// al final con su código, para que ningún peso se pierda de la vista.
    /// </summary>
    public static async Task<List<FilaExportable>> FilasPorNivelAsync(
        IApplicationDbContext db, JerarquiaDelPlan plan, Dictionary<int, SumasDeCuenta> porMovimiento, int nivel, CancellationToken ct)
    {
        var agregadas = plan.Agregar(porMovimiento);
        var filas = new List<FilaExportable>();
        foreach (var cuenta in plan.Cuentas)
        {
            if (cuenta.Level > nivel) continue;
            if (!agregadas.TryGetValue(cuenta.Id, out var s) || s.EsCero) continue;
            filas.Add(FilaDeCuenta(plan, cuenta, s));
        }

        var faltantes = agregadas.Keys.Where(id => plan.PorId(id) is null).ToList();
        if (faltantes.Count > 0)
        {
            var eliminadas = await db.ChartOfAccounts.AsNoTracking().Where(a => faltantes.Contains(a.Id))
                .Select(a => new { a.Id, a.PublicId, a.Code, a.Name, a.Nature }).ToListAsync(ct);
            foreach (var e in eliminadas.OrderBy(x => x.Code, StringComparer.Ordinal))
            {
                var s = agregadas[e.Id];
                filas.Add(new FilaExportable(
                    [e.Code, $"{e.Name} (eliminada)", s.SaldoInicial(e.Nature), s.Debitos, s.Creditos, s.SaldoFinal(e.Nature), Nodo(e.Code), e.PublicId],
                    "Cuentas fuera del plan"));
            }
        }
        return filas;
    }

    public static FilaExportable FilaDeCuenta(JerarquiaDelPlan plan, JerarquiaDelPlan.Cuenta cuenta, SumasDeCuenta s) =>
        new([cuenta.Code, cuenta.Name, s.SaldoInicial(cuenta.Nature), s.Debitos, s.Creditos, s.SaldoFinal(cuenta.Nature), Nodo(cuenta.Code), cuenta.PublicId],
            SeccionDeClase(plan, cuenta),
            Resaltada: cuenta.Level == 1);

    /// <summary>
    /// La fila de totales de un balance: Σ débitos y Σ créditos del período sobre las cuentas de
    /// movimiento (por eso cuadran: cada comprobante entró cuadrado). Los saldos no se suman:
    /// mezclar naturalezas no significa nada, así que esas celdas van vacías.
    /// </summary>
    public static FilaExportable Totales(Dictionary<int, SumasDeCuenta> porMovimiento, int columnas)
    {
        var valores = new object?[columnas];
        valores[0] = "Totales";
        valores[1] = string.Empty;
        valores[3] = porMovimiento.Values.Sum(s => s.Debitos);
        valores[4] = porMovimiento.Values.Sum(s => s.Creditos);
        return new FilaExportable(valores, Resaltada: true);
    }

    /// <summary>Nombre visible y documento de los terceros pedidos, incluidos los dados de baja (conservan sus movimientos).</summary>
    public static async Task<Dictionary<int, (Guid PublicId, string Nombre, string TaxId)>> TercerosAsync(IApplicationDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName, p.TaxId }).ToListAsync(ct);
        return personas.ToDictionary(p => p.Id, p => (p.PublicId, PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName), p.TaxId));
    }
}

/// <summary>
/// Reglas comunes a los filtros de los libros: el nivel entre 1 y 6 y el formato uno de los cuatro
/// que la API entrega. Cada consulta lo engancha con <c>SetValidator</c>; lo demás (rangos,
/// PublicIds que no existen) lo resuelve <see cref="MovimientosContables.PrepararAsync"/> con su
/// propio error, porque necesita la base.
/// </summary>
public sealed class FiltrosDeLibrosValidator : AbstractValidator<FiltrosDeInforme>
{
    public FiltrosDeLibrosValidator()
    {
        RuleFor(x => x.Level).InclusiveBetween(1, LibrosQueries.NivelMaximo).When(x => x.Level is not null)
            .WithMessage($"El nivel de detalle va de 1 a {LibrosQueries.NivelMaximo}.");
        RuleFor(x => x.Format).Must(LibrosQueries.FormatoAdmitido).WithMessage("El formato debe ser json, xlsx, pdf o docx.");
    }
}

// ------------------------------------------------------------------------------- balance de prueba --

/// <summary>
/// Balance de prueba a un nivel del plan (por defecto el de movimiento de la empresa): saldo
/// inicial, débitos y créditos del período y saldo final por cuenta, cada nivel agregando el de
/// abajo. Con <c>WithThirdParties</c> abre cada cuenta de movimiento por tercero. Σ débitos =
/// Σ créditos siempre, y una prueba lo exige.
/// </summary>
public sealed record TrialBalanceQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class TrialBalanceQueryValidator : AbstractValidator<TrialBalanceQuery>
{
    public TrialBalanceQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeLibrosValidator());
}

public sealed class TrialBalanceQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<TrialBalanceQuery, Result<TablaExportable>>
{
    public const string Vista = "trial-balance";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Código"), new("Nombre"),
        new("Saldo inicial", TipoDeColumna.Moneda), new("Débitos", TipoDeColumna.Moneda), new("Créditos", TipoDeColumna.Moneda), new("Saldo final", TipoDeColumna.Moneda),
        new("Nodo", Clave: LibrosQueries.ClaveNodo), new("Cuenta", Clave: LibrosQueries.ClaveCuenta),
    ];

    public async Task<Result<TablaExportable>> Handle(TrialBalanceQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var nivel = request.Filtros.Level ?? await LibrosQueries.NivelDeMovimientoAsync(db, plan, ct);
        var porMovimiento = await LibrosQueries.SumasPorCuentaAsync(db, c, ct);

        var filas = await LibrosQueries.FilasPorNivelAsync(db, plan, porMovimiento, nivel, ct);
        if (request.Filtros.WithThirdParties) filas = await AbrirPorTerceroAsync(c, plan, filas, ct);

        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        notas.Add("Totales: suma de débitos y créditos del período; los saldos no se suman porque mezclan naturalezas.");

        var tabla = new TablaExportable("Balance de prueba", $"Nivel {nivel} · {c.PeriodoTexto}", Columnas, filas,
            LibrosQueries.Totales(porMovimiento, Columnas.Count), notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }

    /// <summary>
    /// Debajo de cada cuenta de movimiento (la que no tiene hijos en el plan) una fila por tercero
    /// con movimiento o saldo, y «Sin tercero» para lo que no lleva; la sección es la cuenta y el
    /// nodo el del libro auxiliar (<c>person:código|tercero</c>) para seguir profundizando.
    /// </summary>
    private async Task<List<FilaExportable>> AbrirPorTerceroAsync(MovimientosContables.Contexto c, JerarquiaDelPlan plan, List<FilaExportable> filas, CancellationToken ct)
    {
        var porTercero = await LibrosQueries.SumasPorCuentaYTerceroAsync(db, c, ct);
        var terceros = await LibrosQueries.TercerosAsync(db, porTercero.Keys.Where(k => k.PersonaId is not null).Select(k => k.PersonaId!.Value).Distinct().ToList(), ct);

        var resultado = new List<FilaExportable>(filas.Count * 2);
        foreach (var fila in filas)
        {
            resultado.Add(fila);
            var cuenta = fila.Valores[0] is string codigo ? plan.PorCodigo(codigo) : null;
            if (cuenta is null || plan.TieneHijos(cuenta.Id)) continue;

            var hijas = porTercero.Where(x => x.Key.CuentaId == cuenta.Id && !x.Value.EsCero)
                .Select(x => (x.Key.PersonaId, Sumas: x.Value, Tercero: x.Key.PersonaId is { } id && terceros.TryGetValue(id, out var t) ? t : default))
                .OrderBy(x => x.PersonaId is null ? 1 : 0).ThenBy(x => x.Tercero.Nombre, StringComparer.CurrentCultureIgnoreCase);
            foreach (var h in hijas)
            {
                var (codigoTercero, nombre) = h.PersonaId is null
                    ? (string.Empty, "Sin tercero")
                    : h.Tercero.Nombre is null ? (string.Empty, "(tercero no encontrado)") : (h.Tercero.TaxId, h.Tercero.Nombre);
                var nodo = $"person:{cuenta.Code}|{(h.PersonaId is null ? "none" : h.Tercero.PublicId.ToString())}";
                resultado.Add(new FilaExportable(
                    [codigoTercero, nombre, h.Sumas.SaldoInicial(cuenta.Nature), h.Sumas.Debitos, h.Sumas.Creditos, h.Sumas.SaldoFinal(cuenta.Nature), nodo, null],
                    $"{cuenta.Code} {cuenta.Name}"));
            }
        }
        return resultado;
    }
}

// ------------------------------------------------------------------------ libro mayor y balances --

/// <summary>
/// Libro mayor y balances: saldo anterior, débitos, créditos y nuevo saldo por cuenta hasta el
/// nivel pedido (por defecto 4, el de cuenta). Es el balance de prueba visto como libro oficial;
/// los mismos números, otros nombres de columna.
/// </summary>
public sealed record GeneralLedgerQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class GeneralLedgerQueryValidator : AbstractValidator<GeneralLedgerQuery>
{
    public GeneralLedgerQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeLibrosValidator());
}

public sealed class GeneralLedgerQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<GeneralLedgerQuery, Result<TablaExportable>>
{
    public const string Vista = "general-ledger";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Código"), new("Nombre"),
        new("Saldo anterior", TipoDeColumna.Moneda), new("Débitos", TipoDeColumna.Moneda), new("Créditos", TipoDeColumna.Moneda), new("Nuevo saldo", TipoDeColumna.Moneda),
        new("Nodo", Clave: LibrosQueries.ClaveNodo), new("Cuenta", Clave: LibrosQueries.ClaveCuenta),
    ];

    public async Task<Result<TablaExportable>> Handle(GeneralLedgerQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var nivel = request.Filtros.Level ?? LibrosQueries.NivelDeLibroMayor;
        var porMovimiento = await LibrosQueries.SumasPorCuentaAsync(db, c, ct);
        var filas = await LibrosQueries.FilasPorNivelAsync(db, plan, porMovimiento, nivel, ct);

        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        notas.Add("Totales: suma de débitos y créditos del período; los saldos no se suman porque mezclan naturalezas.");

        var tabla = new TablaExportable("Libro mayor y balances", $"Nivel {nivel} · {c.PeriodoTexto}", Columnas, filas,
            LibrosQueries.Totales(porMovimiento, Columnas.Count), notas);

        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }
}

// ----------------------------------------------------------------------------------- libro diario --

/// <summary>
/// Libro diario: los comprobantes del rango, uno por sección (tipo, número, fecha y descripción),
/// con todas sus líneas que pasen los filtros. Ordenado por fecha, tipo y número, como se lee el
/// libro. La apertura no se lista (es saldo inicial, decisión 9) y el cierre sólo con
/// <c>IncludeClosing</c>.
/// </summary>
public sealed record JournalQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class JournalQueryValidator : AbstractValidator<JournalQuery>
{
    public JournalQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeLibrosValidator());
}

public sealed class JournalQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<JournalQuery, Result<TablaExportable>>
{
    public const string Vista = "journal";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha), new("Comprobante"), new("Cuenta"), new("Nombre cuenta"), new("Tercero"), new("Documento cruce"), new("Detalle"),
        new("Débito", TipoDeColumna.Moneda), new("Crédito", TipoDeColumna.Moneda),
        new("Comprobante (id)", Clave: LibrosQueries.ClaveComprobante),
    ];

    public async Task<Result<TablaExportable>> Handle(JournalQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        var lineas = await MovimientosContables.DelRango(MovimientosContables.Base(db, c), c)
            .Where(e => !e.IsDeleted)
            .Select(e => new
            {
                e.Date, e.LineNumber, e.Debit, e.Credit, e.Description, e.CrossDocumentNumber,
                Cruce = e.CrossDocumentType != null ? e.CrossDocumentType.Code : null,
                CuentaCodigo = e.Account!.Code, CuentaNombre = e.Account!.Name,
                PersonaNombre = e.Person != null ? e.Person.FirstName : null,
                PersonaOtrosNombres = e.Person != null ? e.Person.OtherNames : null,
                PersonaApellido = e.Person != null ? e.Person.LastName : null,
                PersonaSegundoApellido = e.Person != null ? e.Person.SecondLastName : null,
                PersonaRazon = e.Person != null ? e.Person.BusinessName : null,
                Documento = new { e.Document!.PublicId, e.Document.Number, e.Document.Description, Tipo = e.Document.VoucherType!.Code },
            })
            .ToListAsync(ct);

        var filas = lineas
            .OrderBy(l => l.Date).ThenBy(l => l.Documento.Tipo, StringComparer.Ordinal).ThenBy(l => l.Documento.Number).ThenBy(l => l.LineNumber)
            .Select(l =>
            {
                var referencia = $"{l.Documento.Tipo}-{l.Documento.Number}";
                var tercero = l.PersonaNombre is null && l.PersonaApellido is null && l.PersonaRazon is null
                    ? string.Empty
                    : PersonFactory.NombreVisible(l.PersonaNombre, l.PersonaOtrosNombres, l.PersonaApellido, l.PersonaSegundoApellido, l.PersonaRazon);
                return new FilaExportable(
                    [l.Date, referencia, l.CuentaCodigo, l.CuentaNombre, tercero, LibrosQueries.Cruce(l.Cruce, l.CrossDocumentNumber), l.Description ?? string.Empty, l.Debit, l.Credit, l.Documento.PublicId],
                    $"{referencia} · {l.Date:dd/MM/yyyy} · {l.Documento.Description}");
            })
            .ToList();

        var totales = new FilaExportable(["Totales", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, $"{filas.Count} línea(s)",
            lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit), null], Resaltada: true);
        var notas = await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct);

        var tabla = new TablaExportable("Libro diario", c.PeriodoTexto, Columnas, filas, totales, notas);
        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }
}

// ----------------------------------------------------------------------- relación de comprobantes --

/// <summary>
/// Relación de comprobantes: una fila por documento del rango que tenga al menos una línea que
/// pase los filtros; sólo contabilizados o reversados (los borradores no son movimiento), con
/// los totales del documento completo, quién lo registró y quién lo contabilizó.
/// </summary>
public sealed record VoucherListQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

public sealed class VoucherListQueryValidator : AbstractValidator<VoucherListQuery>
{
    public VoucherListQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeLibrosValidator());
}

public sealed class VoucherListQueryHandler(
    IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<VoucherListQuery, Result<TablaExportable>>
{
    public const string Vista = "voucher-list";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha), new("Tipo"), new("Número", TipoDeColumna.Entero), new("Descripción"), new("Origen"), new("Estado"),
        new("Débitos", TipoDeColumna.Moneda), new("Créditos", TipoDeColumna.Moneda), new("Registró"), new("Contabilizó"),
        new("Comprobante (id)", Clave: LibrosQueries.ClaveComprobante),
    ];

    public async Task<Result<TablaExportable>> Handle(VoucherListQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;

        // Los documentos se eligen por sus líneas (así el filtro por cuenta o tercero deja sólo los
        // comprobantes que las tocan), pero se muestran completos: los totales son del documento.
        var documentos = MovimientosContables.DelRango(MovimientosContables.Base(db, c), c).Select(e => e.DocumentId).Distinct();
        var lista = await db.AccountingDocuments.AsNoTracking()
            .Where(d => documentos.Contains(d.Id) && !d.IsDeleted && d.Status != DocumentStatus.Draft)
            .Select(d => new
            {
                d.PublicId, d.Date, d.Number, d.Description, d.OriginModule, d.Status, d.Kind, d.TotalDebit, d.TotalCredit, d.RegisteredBy, d.PostedBy,
                Tipo = d.VoucherType!.Code,
            })
            .ToListAsync(ct);

        var filas = lista
            .OrderBy(d => d.Date).ThenBy(d => d.Tipo, StringComparer.Ordinal).ThenBy(d => d.Number)
            .Select(d =>
            {
                var clase = LibrosQueries.NombreClase(d.Kind);
                var estado = clase.Length == 0 ? LibrosQueries.NombreEstado(d.Status) : $"{LibrosQueries.NombreEstado(d.Status)} · {clase}";
                return new FilaExportable(
                    [d.Date, d.Tipo, d.Number, d.Description, Posting.ModuloContable.Nombre(d.OriginModule), estado, d.TotalDebit, d.TotalCredit, d.RegisteredBy, d.PostedBy ?? string.Empty, d.PublicId]);
            })
            .ToList();

        var totales = new FilaExportable(["Totales", string.Empty, filas.Count, string.Empty, string.Empty, string.Empty,
            lista.Sum(d => d.TotalDebit), lista.Sum(d => d.TotalCredit), string.Empty, string.Empty, null], Resaltada: true);
        var notas = (await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct)).ToList();
        notas.Add("Los totales son del comprobante completo aunque los filtros dejen ver sólo algunas de sus líneas.");

        var tabla = new TablaExportable("Relación de comprobantes", c.PeriodoTexto, Columnas, filas, totales, notas);
        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, request.Filtros, request.Filtros.FormatoNormalizado, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }
}
