using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending;

/// <summary>
/// El adaptador real de <see cref="IActivosLiquidosParaCartera"/> (feature 014, respuesta 99 B del dueño, E250): la suma de débitos menos créditos de los
/// movimientos contabilizados hasta la fecha, por prefijo de código del plan de cuentas. Sólo lee, sin seguimiento; no pide permisos (lo usa Cartera dentro de una
/// medición ya autorizada). Un prefijo vacío o con caracteres que no son del código se rechaza. (nuevo)
/// </summary>
public sealed class ActivosLiquidosParaCartera(IApplicationDbContext db) : IActivosLiquidosParaCartera
{
    public async Task<Result<IReadOnlyList<SaldoPorPrefijoDto>>> SaldosPorPrefijosAsync(IReadOnlyList<string> prefijos, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prefijos);
        var limpios = prefijos.Select(p => p?.Trim() ?? string.Empty).ToList();
        if (limpios.Any(p => p.Length == 0 || !p.All(char.IsLetterOrDigit)))
            return Result.Failure<IReadOnlyList<SaldoPorPrefijoDto>>(new Error("Accounting.LiquidAssets.PrefixInvalid",
                "Un prefijo de cuenta de los activos líquidos está vacío o trae caracteres que no son del código."));
        var resultado = new List<SaldoPorPrefijoDto>(limpios.Count);
        foreach (var prefijo in limpios)
        {
            var saldo = await db.JournalEntries.AsNoTracking()
                .Where(e => e.IsPosted && e.Date <= fecha && e.Account!.Code.StartsWith(prefijo))
                .SumAsync(e => (decimal?)(e.Debit - e.Credit), ct) ?? 0m;
            resultado.Add(new SaldoPorPrefijoDto(prefijo, saldo));
        }
        return Result.Success<IReadOnlyList<SaldoPorPrefijoDto>>(resultado);
    }
}
