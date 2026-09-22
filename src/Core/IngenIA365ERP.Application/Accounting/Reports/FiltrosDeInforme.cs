using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Reports;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// Los filtros combinables de toda consulta e informe contable (feature 009 E2, FR-043): rango de
/// fechas, cuenta o rama del plan, tercero, documento cruce, centro de costo, sucursal, tipo de
/// comprobante, módulo origen, usuario y nivel de detalle. Llegan por query string a
/// <c>/api/reports/accounting/{vista}</c> con estos mismos nombres (contracts/api.md §8) y se
/// aplican todos a la vez sobre la misma vista y sobre su exportación.
/// </summary>
public sealed record FiltrosDeInforme
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    /// <summary>Rango de códigos, inclusive en los dos extremos (<c>AccountTo</c> compara por prefijo: «1105» cubre 110505).</summary>
    public string? AccountFrom { get; init; }
    public string? AccountTo { get; init; }
    /// <summary>Rama del plan: la cuenta y todo lo que cuelga de ella (prefijo del código).</summary>
    public Guid? AccountPublicId { get; init; }
    /// <summary>
    /// Código de rubro NIIF (<c>FinancialStatementItem.Code</c>: «ESF-A-EFE», «ERI-ING»…): las
    /// cuentas del rubro y de todos sus descendientes por <c>ParentCode</c>. Es el puente entre
    /// los estados financieros, que se leen por rubro, y el libro auxiliar, que se recorre por
    /// cuenta: un clic en una fila del ESF o del ERI abre el libro con este filtro.
    /// </summary>
    public string? NiifItem { get; init; }
    public Guid? Person { get; init; }
    /// <summary>«TIPO|NÚMERO», o sólo «TIPO» para todos los documentos de ese tipo.</summary>
    public string? CrossDocument { get; init; }
    public Guid? CostCenter { get; init; }
    public Guid? Branch { get; init; }
    /// <summary>Código del tipo de comprobante (CG, NM…).</summary>
    public string? VoucherType { get; init; }
    /// <summary>Módulo origen del comprobante (<c>AccountingDocument.OriginModule</c>: CNT, NOM…).</summary>
    public string? Origin { get; init; }
    /// <summary>Quien registró o contabilizó (contiene, sin distinguir mayúsculas).</summary>
    public string? User { get; init; }
    /// <summary>Nivel de detalle del plan (1..6); nulo = el nivel de movimiento de la empresa.</summary>
    public int? Level { get; init; }
    public bool WithThirdParties { get; init; }
    /// <summary>Los comprobantes de cierre (<c>Kind = Closing</c>) sólo entran si se piden.</summary>
    public bool IncludeClosing { get; init; }
    public string? Format { get; init; }

    public DateOnly Hasta(DateOnly hoy) => To ?? hoy;
    public DateOnly Desde(DateOnly hoy) => From ?? new DateOnly(Hasta(hoy).Year, 1, 1);
    /// <summary>
    /// El formato con la regla única de <see cref="FormatosDeInforme"/> («json» si no vino; si no,
    /// sin espacios y en minúsculas). Es lo que se audita: la API decide con la misma regla si
    /// entrega archivo y si exige el permiso de exportar, así que los tres no pueden divergir.
    /// </summary>
    public string FormatoNormalizado => FormatosDeInforme.Normalizar(Format);
    public bool EsExportacion => FormatosDeInforme.EsExportacion(Format);

    /// <summary>Tipo y número del documento cruce, si vinieron.</summary>
    public (string? Tipo, string? Numero) Cruce()
    {
        if (string.IsNullOrWhiteSpace(CrossDocument)) return (null, null);
        var partes = CrossDocument.Split('|', 2, StringSplitOptions.TrimEntries);
        var tipo = string.IsNullOrWhiteSpace(partes[0]) ? null : partes[0].ToUpperInvariant();
        var numero = partes.Length > 1 && !string.IsNullOrWhiteSpace(partes[1]) ? partes[1] : null;
        return (tipo, numero);
    }
}

/// <summary>
/// El encabezado obligatorio de todo informe (FR-045): empresa, NIT, filtros aplicados, período,
/// quién lo generó y cuándo. Va en <see cref="TablaExportable.Notas"/>, que la pantalla y los tres
/// exportadores ya pintan. La convención de signos también se dice aquí porque no es obvia.
/// </summary>
public static class EncabezadoDeInforme
{
    public const string NotaDeSignos = "Saldos con signo según la naturaleza de la cuenta: positivo cuando el saldo va con su naturaleza (débito en activos, costos y gastos; crédito en pasivos, patrimonio e ingresos).";

    public static async Task<IReadOnlyList<string>> NotasAsync(
        IApplicationDbContext db, ICurrentUserService user, IDateTimeService clock,
        FiltrosDeInforme filtros, string periodo, MovimientosContables.Contexto contexto, CancellationToken ct)
    {
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, Nit = c.TaxIdCheckDigit == null ? c.TaxId : c.TaxId + "-" + c.TaxIdCheckDigit })
            .FirstOrDefaultAsync(ct);
        var notas = new List<string>
        {
            empresa is null ? "Empresa: (sin datos de empresa)" : $"Empresa: {empresa.Name} · NIT {empresa.Nit}",
            $"Período: {periodo}",
            $"Filtros: {Filtros(filtros, contexto)}",
            $"Generado por {user.UserName ?? "(sin usuario)"} el {clock.UtcNow:dd/MM/yyyy HH:mm} UTC",
            NotaDeSignos,
        };
        return notas;
    }

    /// <summary>Los filtros aplicados en una frase legible; «ninguno» si sólo vino el rango.</summary>
    public static string Filtros(FiltrosDeInforme f, MovimientosContables.Contexto c)
    {
        var partes = new List<string>();
        if (c.Cuenta is { } cuenta) partes.Add($"rama {cuenta.Code} {cuenta.Name}");
        if (c.Rubro is { } rubro) partes.Add($"rubro {rubro.Code} {rubro.Name}");
        if (!string.IsNullOrWhiteSpace(f.AccountFrom) || !string.IsNullOrWhiteSpace(f.AccountTo)) partes.Add($"cuentas {f.AccountFrom ?? "…"} a {f.AccountTo ?? "…"}");
        if (c.Tercero is { } t) partes.Add($"tercero {t.Nombre} ({t.TaxId})");
        var (tipo, numero) = f.Cruce();
        if (tipo is not null) partes.Add(numero is null ? $"documento {tipo}" : $"documento {tipo} {numero}");
        if (c.CentroDeCosto is { } cc) partes.Add($"centro de costo {cc}");
        if (c.Sucursal is { } s) partes.Add($"sucursal {s}");
        if (!string.IsNullOrWhiteSpace(f.VoucherType)) partes.Add($"tipo de comprobante {f.VoucherType.ToUpperInvariant()}");
        if (!string.IsNullOrWhiteSpace(f.Origin)) partes.Add($"origen {f.Origin.ToUpperInvariant()}");
        if (!string.IsNullOrWhiteSpace(f.User)) partes.Add($"usuario «{f.User}»");
        if (f.Level is { } n) partes.Add($"nivel {n}");
        if (f.WithThirdParties) partes.Add("con terceros");
        if (f.IncludeClosing) partes.Add("incluye el cierre");
        if (c.Alcance.Restringido) partes.Add("sólo las sucursales asignadas a quien consulta");
        return partes.Count == 0 ? "ninguno" : string.Join("; ", partes);
    }
}
