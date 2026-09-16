using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Accounting.Setup;

/// <summary>
/// Cómo una entrada de catálogo se vuelve cuenta de la empresa (feature 009, FR-003, FR-006):
/// niveles 1 a 4, <c>Origin = Catalog</c>, nunca de movimiento, sin reglas, activa. El padre se
/// resuelve por navegación —entre las recién creadas o las que ya tenía la empresa— para que
/// EF ordene las inserciones sin dos pasadas. Lo usan iniciar, cambiar de catálogo y adoptar
/// cuentas nuevas; así los tres copian exactamente lo mismo.
/// </summary>
public static class CopiaDelCatalogo
{
    public static IReadOnlyList<ChartOfAccount> Copiar(
        IEnumerable<AccountCatalogEntry> entradas,
        IReadOnlyDictionary<string, ChartOfAccount> existentes,
        string quien,
        DateTime ahora)
    {
        var nuevas = new Dictionary<string, ChartOfAccount>(StringComparer.Ordinal);
        var lista = new List<ChartOfAccount>();
        foreach (var e in entradas.OrderBy(e => e.Code.Length).ThenBy(e => e.Code, StringComparer.Ordinal))
        {
            ChartOfAccount? padre = null;
            if (e.ParentCode is { } codigoPadre && !nuevas.TryGetValue(codigoPadre, out padre))
                existentes.TryGetValue(codigoPadre, out padre);

            var cuenta = new ChartOfAccount
            {
                Code = e.Code,
                Name = e.Name,
                Level = e.Level,
                Nature = e.Nature,
                Parent = padre,
                ParentId = padre?.Id > 0 ? padre.Id : null,
                NiifItemCode = e.NiifItemCode,
                Origin = AccountOrigin.Catalog,
                IsMovement = false,
                IsActive = true,
                EnabledModules = AccountingModules.None,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            nuevas[e.Code] = cuenta;
            lista.Add(cuenta);
        }
        return lista;
    }

    /// <summary>Los doce períodos mensuales de un ejercicio, abiertos.</summary>
    public static FiscalYear Ejercicio(int year, string quien, DateTime ahora)
    {
        var ejercicio = new FiscalYear { Year = year, Status = PeriodStatus.Open, CreatedAt = ahora, CreatedBy = quien };
        for (var mes = 1; mes <= 12; mes++)
        {
            var inicio = new DateOnly(year, mes, 1);
            ejercicio.Periods.Add(new AccountingPeriod
            {
                Month = (byte)mes,
                StartDate = inicio,
                EndDate = inicio.AddMonths(1).AddDays(-1),
                Status = PeriodStatus.Open,
                CreatedAt = ahora,
                CreatedBy = quien,
            });
        }
        return ejercicio;
    }

    /// <summary>Longitudes admitidas de las auxiliares: más largas que la subcuenta (6) y dentro del código (12), crecientes por nivel.</summary>
    public static string? ReparoDeLongitudes(byte movementLevel, byte level5Length, byte level6Length)
    {
        if (movementLevel is not (5 or 6)) return "El nivel de movimiento es 5 o 6.";
        if (level5Length is < 7 or > 12) return "La longitud del nivel 5 va de 7 a 12 dígitos (la subcuenta tiene 6).";
        if (movementLevel == 6 && (level6Length <= level5Length || level6Length > 12))
            return $"La longitud del nivel 6 tiene que ser mayor que la del nivel 5 ({level5Length}) y de hasta 12 dígitos.";
        return null;
    }
}
