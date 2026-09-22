using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>Sumas crudas de una cuenta: débitos y créditos iniciales (antes del rango) y del período.</summary>
public sealed record SumasDeCuenta(decimal DebitoInicial, decimal CreditoInicial, decimal Debitos, decimal Creditos)
{
    public static readonly SumasDeCuenta Cero = new(0m, 0m, 0m, 0m);

    public SumasDeCuenta Mas(SumasDeCuenta o) => new(DebitoInicial + o.DebitoInicial, CreditoInicial + o.CreditoInicial, Debitos + o.Debitos, Creditos + o.Creditos);

    /// <summary>Saldo inicial con signo según la naturaleza (FR-046; nota de <see cref="EncabezadoDeInforme.NotaDeSignos"/>).</summary>
    public decimal SaldoInicial(AccountNature naturaleza) => naturaleza == AccountNature.Debit ? DebitoInicial - CreditoInicial : CreditoInicial - DebitoInicial;

    public decimal SaldoFinal(AccountNature naturaleza) => SaldoInicial(naturaleza) + (naturaleza == AccountNature.Debit ? Debitos - Creditos : Creditos - Debitos);

    public bool EsCero => DebitoInicial == 0m && CreditoInicial == 0m && Debitos == 0m && Creditos == 0m;
}

/// <summary>
/// El plan de cuentas de la empresa en memoria con la agregación hacia arriba (FR-042, FR-046,
/// FR-063): las sumas se calculan sobre las cuentas de movimiento y suben por la cadena de padres
/// hasta la clase. No hay saldos guardados; esto es el «resumen en memoria» que la spec pide
/// (data-model.md, R4). Se carga una vez por consulta.
/// </summary>
public sealed class JerarquiaDelPlan
{
    public sealed record Cuenta(int Id, Guid PublicId, string Code, string Name, byte Level, AccountNature Nature, int? ParentId, bool IsMovement, string NiifItemCode, bool IsActive);

    private readonly Dictionary<int, Cuenta> _porId;
    private readonly Dictionary<string, Cuenta> _porCodigo;
    private readonly Dictionary<int, List<Cuenta>> _hijos;

    private JerarquiaDelPlan(List<Cuenta> cuentas)
    {
        Cuentas = cuentas;
        _porId = cuentas.ToDictionary(c => c.Id);
        _porCodigo = cuentas.ToDictionary(c => c.Code, StringComparer.Ordinal);
        _hijos = cuentas.Where(c => c.ParentId is not null).GroupBy(c => c.ParentId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Code, StringComparer.Ordinal).ToList());
    }

    public IReadOnlyList<Cuenta> Cuentas { get; }

    /// <summary>Todo el plan vivo (activas e inactivas: una inactiva puede tener saldo), sin las eliminadas.</summary>
    public static async Task<JerarquiaDelPlan> CargarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted)
            .OrderBy(a => a.Code)
            .Select(a => new Cuenta(a.Id, a.PublicId, a.Code, a.Name, a.Level, a.Nature, a.ParentId, a.IsMovement, a.NiifItemCode, a.IsActive))
            .ToListAsync(ct);
        return new JerarquiaDelPlan(cuentas);
    }

    public Cuenta? PorId(int id) => _porId.GetValueOrDefault(id);
    public Cuenta? PorCodigo(string code) => _porCodigo.GetValueOrDefault(code);
    public IReadOnlyList<Cuenta> Hijos(int id) => _hijos.GetValueOrDefault(id) ?? [];
    public IReadOnlyList<Cuenta> Raices => Cuentas.Where(c => c.ParentId is null).OrderBy(c => c.Code, StringComparer.Ordinal).ToList();
    public bool TieneHijos(int id) => _hijos.ContainsKey(id);

    /// <summary>La cuenta y sus ancestros hasta la clase, de abajo hacia arriba.</summary>
    public IEnumerable<Cuenta> Ancestros(Cuenta c)
    {
        var actual = c;
        while (actual.ParentId is { } padre && _porId.TryGetValue(padre, out var p)) { yield return p; actual = p; }
    }

    /// <summary>La cuenta del nivel pedido en la cadena de una cuenta (ella misma si ya es de ese nivel o de uno superior).</summary>
    public Cuenta AlNivel(Cuenta c, int nivel)
    {
        var actual = c;
        while (actual.Level > nivel && actual.ParentId is { } padre && _porId.TryGetValue(padre, out var p)) actual = p;
        return actual;
    }

    /// <summary>
    /// Sube las sumas de cada cuenta de movimiento por su cadena de padres. Devuelve las sumas por
    /// Id de cuenta, incluidas las de agrupación. Las sumas de una cuenta que no esté en el plan
    /// (no debería pasar: el asiento la referencia) se conservan bajo su propio Id.
    /// </summary>
    public Dictionary<int, SumasDeCuenta> Agregar(IReadOnlyDictionary<int, SumasDeCuenta> porCuentaDeMovimiento)
    {
        var total = new Dictionary<int, SumasDeCuenta>();
        foreach (var (id, sumas) in porCuentaDeMovimiento)
        {
            total[id] = total.GetValueOrDefault(id, SumasDeCuenta.Cero).Mas(sumas);
            if (!_porId.TryGetValue(id, out var cuenta)) continue;
            foreach (var padre in Ancestros(cuenta))
                total[padre.Id] = total.GetValueOrDefault(padre.Id, SumasDeCuenta.Cero).Mas(sumas);
        }
        return total;
    }

    /// <summary>Naturaleza de una clase del PUC cuando la cuenta no está en el plan: 1, 5, 6, 7 y 8 débito; 2, 3, 4 y 9 crédito.</summary>
    public static AccountNature NaturalezaDeClase(string code) =>
        code.Length > 0 && code[0] is '2' or '3' or '4' or '9' ? AccountNature.Credit : AccountNature.Debit;
}
