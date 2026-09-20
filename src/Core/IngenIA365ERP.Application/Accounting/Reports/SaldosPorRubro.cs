using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// Los rubros NIIF del grupo de la empresa (<c>AccountingSetup.NiifGroup</c>) con sus saldos, para
/// los cuatro estados financieros (feature 009 E2, FR-047). Ningún rubro está en código: se leen
/// de <c>ACC_FinancialStatementItems</c> y la jerarquía (<c>ParentCode</c>) se arma en memoria.
///
/// <para>
/// El valor de un rubro es la suma de los saldos de las cuentas con movimiento cuyo
/// <c>NiifItemCode</c> es su código, medidos por la naturaleza <b>que el rubro espera</b> —no por
/// la de la cuenta— y multiplicados por el <c>Sign</c> del rubro (−1 en las contra: deterioros,
/// depreciaciones, devoluciones), más el valor de sus hijos, que ya llegan con su propio signo
/// (<see cref="Saldos.NaturalezaEsperada"/>). Una cuenta cuyo rubro no existe para el grupo no se
/// pierde: va a <see cref="Saldos.SinRubro"/> y el estado la muestra en una fila «Sin rubro NIIF».
/// </para>
///
/// <para>
/// El cierre se trata distinto a las demás consultas, y a propósito: un saldo <b>a una fecha</b>
/// (ESF, ECP, EFE) siempre incluye los cierres de los ejercicios <i>anteriores</i> al de esa fecha
/// —sin ellos los ingresos y gastos del año pasado seguirían abiertos y el ESF del segundo año no
/// cuadraría— y excluye sólo el del ejercicio de la fecha, salvo <c>IncludeClosing</c>; los
/// <b>movimientos de un rango</b> (ERI) excluyen todo cierre salvo la bandera, como el resto de
/// las vistas. Cuando el cierre del año entra, el ERI del año queda en cero y las cuentas del
/// resultado traen el valor: no se cuenta dos veces.
/// </para>
/// </summary>
public sealed class SaldosPorRubro
{
    public sealed record Rubro(string Code, string Name, FinancialStatementKind Statement, string Section, int Order, short Sign, string? ParentCode);

    private readonly IApplicationDbContext _db;
    private readonly MovimientosContables.Contexto _contexto;
    private readonly IQueryable<JournalEntry> _libro;
    private readonly Dictionary<string, Rubro> _porCodigo;
    private readonly Dictionary<string, List<Rubro>> _hijos;

    private SaldosPorRubro(IApplicationDbContext db, MovimientosContables.Contexto contexto, byte grupo, List<Rubro> rubros, JerarquiaDelPlan plan)
    {
        _db = db;
        _contexto = contexto;
        Grupo = grupo;
        Rubros = rubros;
        Plan = plan;
        _porCodigo = rubros.ToDictionary(r => r.Code, StringComparer.Ordinal);
        _hijos = rubros.Where(r => r.ParentCode is not null).GroupBy(r => r.ParentCode!)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Order).ToList(), StringComparer.Ordinal);
        // Base con el cierre incluido: el filtro fino de cierre (por ejercicio de la fecha) lo pone cada medición.
        _libro = MovimientosContables.Base(db, contexto with { Filtros = contexto.Filtros with { IncludeClosing = true } });
    }

    public byte Grupo { get; }
    public IReadOnlyList<Rubro> Rubros { get; }
    public JerarquiaDelPlan Plan { get; }
    public bool IncluirCierre => _contexto.Filtros.IncludeClosing;

    public static async Task<Result<SaldosPorRubro>> CargarAsync(IApplicationDbContext db, MovimientosContables.Contexto contexto, CancellationToken ct)
    {
        var grupo = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => (byte?)s.NiifGroup).FirstOrDefaultAsync(ct);
        if (grupo is null) return Result.Failure<SaldosPorRubro>(AccountingErrors.NotInitialized);
        var rubros = await db.FinancialStatementItems.AsNoTracking()
            .Where(r => !r.IsDeleted && r.NiifGroup == grupo.Value)
            .OrderBy(r => r.Order)
            .Select(r => new Rubro(r.Code, r.Name, r.Statement, r.Section, r.Order, r.Sign, r.ParentCode))
            .ToListAsync(ct);
        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        return Result.Success(new SaldosPorRubro(db, contexto, grupo.Value, rubros, plan));
    }

    public Rubro? PorCodigo(string code) => _porCodigo.GetValueOrDefault(code);
    public bool Existe(string code) => _porCodigo.ContainsKey(code);
    public IReadOnlyList<Rubro> Hijos(string code) => _hijos.GetValueOrDefault(code) ?? [];
    public bool TieneHijos(string code) => _hijos.ContainsKey(code);

    /// <summary>Los rubros de un estado en el orden de presentación (<c>Order</c>).</summary>
    public IReadOnlyList<Rubro> DelEstado(FinancialStatementKind estado) => Rubros.Where(r => r.Statement == estado).OrderBy(r => r.Order).ToList();

    /// <summary>Las secciones de un estado en el orden en que aparecen sus rubros.</summary>
    public IReadOnlyList<string> SeccionesDe(FinancialStatementKind estado) =>
        DelEstado(estado).Select(r => r.Section).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>El 1 de enero del ejercicio de una fecha (el ejercicio contable es el año calendario: <c>FiscalYear.StartDate</c>).</summary>
    public static DateOnly InicioDelEjercicio(DateOnly fecha) => new(fecha.Year, 1, 1);

    /// <summary>Saldos acumulados hasta una fecha inclusive, apertura incluida (estados a una fecha).</summary>
    public Task<Saldos> ALaFechaAsync(DateOnly fecha, CancellationToken ct)
    {
        var inicioDelEjercicio = InicioDelEjercicio(fecha);
        var incluirCierre = IncluirCierre;
        var q = _libro.Where(e => e.Date <= fecha
                                  && (incluirCierre || e.Document!.Kind != DocumentKind.Closing || e.Date < inicioDelEjercicio));
        return MedirAsync(q, ct);
    }

    /// <summary>
    /// Saldos acumulados hasta una fecha inclusive, contando los cierres fechados <b>antes de
    /// <paramref name="desde"/></b> (el inicio del rango consultado) y ninguno posterior, salvo la
    /// bandera. Es la medición de los dos extremos de una variación (EFE): los dos tienen que ver
    /// los mismos cierres o la variación se los atribuye al período. Con <see cref="ALaFechaAsync"/>
    /// el saldo al 31/12/2026 no veía el cierre de 2026 (es «su» ejercicio) y el del 31/12/2027 sí,
    /// así que el EFE de 2027 restaba el resultado de 2026 a «Resultado del ejercicio» y lo mandaba
    /// entero a financiación como si se hubiera distribuido; la fila «Diferencia» daba cero porque
    /// los dos errores se compensaban.
    /// </summary>
    public Task<Saldos> ALaFechaConCierresAnterioresAAsync(DateOnly fecha, DateOnly desde, CancellationToken ct)
    {
        var incluirCierre = IncluirCierre;
        var q = _libro.Where(e => e.Date <= fecha
                                  && (incluirCierre || e.Document!.Kind != DocumentKind.Closing || e.Date < desde));
        return MedirAsync(q, ct);
    }

    /// <summary>Movimientos de un rango, sin la apertura (que es saldo inicial) y sin cierre salvo la bandera.</summary>
    public Task<Saldos> DelRangoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var incluirCierre = IncluirCierre;
        var q = _libro.Where(e => e.Date >= desde && e.Date <= hasta
                                  && e.Document!.Kind != DocumentKind.Opening
                                  && (incluirCierre || e.Document!.Kind != DocumentKind.Closing));
        return MedirAsync(q, ct);
    }

    /// <summary>
    /// El resultado del ejercicio acumulado a una fecha (1 de enero → fecha): ingresos menos costos
    /// y gastos, es decir la suma crédito-positiva de todo lo contabilizado en cuentas cuyo rubro
    /// pertenece al ERI. Es lo que el ESF inyecta en «Resultado del ejercicio» mientras no hay
    /// cierre; con el cierre incluido vale cero y las cuentas del resultado traen el valor.
    /// </summary>
    public async Task<decimal> ResultadoALaFechaAsync(DateOnly fecha, CancellationToken ct)
    {
        var rango = await DelRangoAsync(InicioDelEjercicio(fecha), fecha, ct);
        return rango.ResultadoContable;
    }

    private async Task<Saldos> MedirAsync(IQueryable<JournalEntry> q, CancellationToken ct)
    {
        var sumas = await q.GroupBy(e => e.AccountId)
            .Select(g => new { g.Key, Debitos = g.Sum(e => e.Debit), Creditos = g.Sum(e => e.Credit) })
            .ToListAsync(ct);
        var porCuenta = sumas.ToDictionary(s => s.Key, s => new SumasDeCuenta(0m, 0m, s.Debitos, s.Creditos));
        return new Saldos(this, porCuenta);
    }

    /// <summary>A qué estado pertenece por su clase una cuenta cuyo rubro no existe: 4, 5, 6 y 7 al ERI; el resto al ESF.</summary>
    public static FinancialStatementKind EstadoPorClase(string code) =>
        code.Length > 0 && code[0] is '4' or '5' or '6' or '7' ? FinancialStatementKind.IncomeStatement : FinancialStatementKind.FinancialPosition;

    /// <summary>Una medición: las sumas por cuenta y, derivados de ellas, los valores por rubro.</summary>
    public sealed class Saldos
    {
        private readonly SaldosPorRubro _rubros;
        private readonly Dictionary<string, decimal> _propio = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SumasDeCuenta> _sumasPropias = new(StringComparer.Ordinal);
        private readonly Dictionary<string, decimal> _total = new(StringComparer.Ordinal);

        internal Saldos(SaldosPorRubro rubros, Dictionary<int, SumasDeCuenta> porCuenta)
        {
            _rubros = rubros;
            PorCuenta = porCuenta;
            var sinRubro = new List<(JerarquiaDelPlan.Cuenta Cuenta, decimal Saldo)>();
            decimal resultado = 0m;
            foreach (var (id, sumas) in porCuenta)
            {
                var cuenta = rubros.Plan.PorId(id);
                if (cuenta is null) continue; // el asiento la referencia; no debería pasar
                var rubro = rubros.PorCodigo(cuenta.NiifItemCode);
                if (rubro is null)
                {
                    sinRubro.Add((cuenta, sumas.SaldoFinal(cuenta.Nature)));
                    continue;
                }
                var saldo = sumas.SaldoFinal(NaturalezaEsperada(cuenta, rubro));
                _propio[rubro.Code] = _propio.GetValueOrDefault(rubro.Code) + saldo * rubro.Sign;
                _sumasPropias[rubro.Code] = _sumasPropias.GetValueOrDefault(rubro.Code, SumasDeCuenta.Cero).Mas(sumas);
                if (rubro.Statement == FinancialStatementKind.IncomeStatement) resultado += sumas.Creditos - sumas.Debitos;
            }
            SinRubro = sinRubro.OrderBy(x => x.Cuenta.Code, StringComparer.Ordinal).ToList();
            ResultadoContable = resultado;
        }

        /// <summary>
        /// El lado por el que se mide una cuenta dentro de su rubro: la naturaleza de su <b>clase</b>
        /// (1 débito, 2 y 3 crédito…), invertida cuando el rubro es una contra (<c>Sign</c> −1).
        /// No es la naturaleza de la cuenta, a propósito: el CUIF trae cuentas de naturaleza contraria
        /// a su clase dentro de rubros normales —3510 «Pérdida del ejercicio» es débito y está en el
        /// patrimonio, 6220 es crédito y está en costos, los deterioros de inversiones (1203xx) son
        /// crédito y están en el activo— y medirlas por su propia naturaleza las hacía sumar donde
        /// deben restar: con una pérdida de 100 en 3510 el patrimonio subía 100 y el ESF descuadraba
        /// 200. Con el lado del rubro, los cuatro casos salen bien: 1408 (crédito, deterioro de cartera,
        /// rubro −1) → esperada crédito, saldo +X, × −1 = −X; 3510 (débito, rubro +1) → esperada crédito,
        /// saldo C − D = −100, resta del patrimonio; 4175 devoluciones (débito, rubro −1) → esperada
        /// débito, saldo +X, × −1 = −X; 6220 (crédito, rubro +1) → esperada débito, saldo D − C negativo,
        /// resta de los costos. Y como cada clase queda medida por un solo lado, la suma de los rubros
        /// de una raíz es Σ (débitos − créditos) o Σ (créditos − débitos) de toda la clase, que es lo
        /// que hace cuadrar Activo = Pasivo + Patrimonio.
        /// </summary>
        public static AccountNature NaturalezaEsperada(JerarquiaDelPlan.Cuenta cuenta, Rubro rubro)
        {
            var esperada = JerarquiaDelPlan.NaturalezaDeClase(cuenta.Code);
            if (rubro.Sign >= 0) return esperada;
            return esperada == AccountNature.Debit ? AccountNature.Credit : AccountNature.Debit;
        }

        /// <summary>Débitos y créditos por cuenta (en <see cref="SumasDeCuenta.Debitos"/> y <see cref="SumasDeCuenta.Creditos"/>).</summary>
        public IReadOnlyDictionary<int, SumasDeCuenta> PorCuenta { get; }

        /// <summary>Cuentas con movimiento cuyo <c>NiifItemCode</c> no existe para el grupo, con su saldo por naturaleza.</summary>
        public IReadOnlyList<(JerarquiaDelPlan.Cuenta Cuenta, decimal Saldo)> SinRubro { get; }

        /// <summary>Ingresos menos costos y gastos de la medición: Σ (créditos − débitos) de las cuentas con rubro del ERI.</summary>
        public decimal ResultadoContable { get; }

        /// <summary>Valor de las cuentas directas del rubro (saldo por el lado que el rubro espera × signo), sin sus hijos.</summary>
        public decimal Propio(string code) => _propio.GetValueOrDefault(code);

        /// <summary>Valor del rubro con sus hijos (cada hijo ya trae su signo).</summary>
        public decimal Total(string code)
        {
            if (_total.TryGetValue(code, out var v)) return v;
            v = Propio(code) + _rubros.Hijos(code).Sum(h => Total(h.Code));
            _total[code] = v;
            return v;
        }

        /// <summary>Débitos y créditos crudos de las cuentas del rubro y de sus descendientes.</summary>
        public SumasDeCuenta Sumas(string code)
        {
            var s = _sumasPropias.GetValueOrDefault(code, SumasDeCuenta.Cero);
            foreach (var h in _rubros.Hijos(code)) s = s.Mas(Sumas(h.Code));
            return s;
        }

        /// <summary>Las cuentas sin rubro que caen en un estado (por su clase) y la suma de sus saldos.</summary>
        public (IReadOnlyList<(JerarquiaDelPlan.Cuenta Cuenta, decimal Saldo)> Cuentas, decimal Suma) SinRubroDe(FinancialStatementKind estado)
        {
            var cuentas = SinRubro.Where(x => EstadoPorClase(x.Cuenta.Code) == estado).ToList();
            return (cuentas, cuentas.Sum(x => x.Saldo));
        }
    }
}
