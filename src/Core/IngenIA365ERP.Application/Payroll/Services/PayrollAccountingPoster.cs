using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Arma el comprobante <c>NM</c> de una corrida aprobada (D-07, FR-019, FR-023) y lo entrega al
/// contrato de contabilización (feature 009): agrupa por concepto, centro de costo del empleado y
/// tercero, toma las cuentas de <c>PAY_ConceptDefinitionAccounts</c> (la fila del centro de
/// costo, o la fila por defecto) y manda una línea al débito y otra al crédito por grupo. El
/// tercero es el empleado en devengos y deducciones, y la persona vinculada a la EPS, ARL, fondo o
/// caja del empleado en aportes y provisiones (FR-088, <see cref="TercerosDeNomina"/>): si la
/// entidad no tiene persona vinculada y la cuenta exige tercero, falla nombrándola. El contrato
/// aplica las reglas de cada cuenta, numera y agrega SIN guardar: el comando de aprobación guarda
/// corrida y comprobante en una sola transacción, o nada. Un valor negativo (ajuste de redondeo en
/// contra) invierte débito y crédito. Si a un concepto le faltan cuentas, devuelve el error y no
/// toca el contexto.
/// </summary>
public sealed class PayrollAccountingPoster(IApplicationDbContext db, AccountingPoster poster)
{
    public const string VoucherCode = "NM";
    public const string ModuleCode = ModuloContable.Nomina;

    /// <summary>El <c>SourceType</c> de la nómina ordinaria; las liquidaciones especiales llevan el suyo por tipo (<see cref="PayrollRun.SourceTypeNameDe"/>, R2).</summary>
    public const string SourceType = "PayrollRun";

    private sealed record Grupo(string Code, int? CostCenterId, int? PersonId, EntidadInstitucional Entidad, string? EntidadNombre);

    /// <summary>El origen contable de una corrida: módulo Nómina, <c>SourceType</c> según su <c>Kind</c> y su PublicId.</summary>
    public static AccountingOrigin OrigenDe(PayrollRun run) => new(ModuleCode, run.SourceTypeName, run.PublicId);

    public Task<Result<AccountingDocument>> PostAsync(
        PayrollRun run,
        IReadOnlyList<(PayrollRunEmployee RunEmployee, Employee Employee, IReadOnlyList<PayrollRunLine> Lines)> employees,
        DateOnly documentDate,
        string detail,
        CancellationToken ct) =>
        PostAsync(OrigenDe(run), employees, documentDate, detail, terceroPorLinea: null, ct);

    /// <summary>
    /// Feature 010: la misma armada del comprobante para cualquier origen (prima, cesantías, vacaciones,
    /// definitiva), con <c>SourceType</c> propio y, si hace falta, otra regla de tercero por línea
    /// (<paramref name="terceroPorLinea"/>: nulo = la de <see cref="TercerosDeNomina"/>). Es la
    /// sobrecarga que usa <see cref="SettlementAccountingPoster"/>; el camino al libro sigue siendo
    /// uno solo, <see cref="AccountingPoster.PrepareAsync"/>.
    /// </summary>
    public async Task<Result<AccountingDocument>> PostAsync(
        AccountingOrigin origen,
        IReadOnlyList<(PayrollRunEmployee RunEmployee, Employee Employee, IReadOnlyList<PayrollRunLine> Lines)> employees,
        DateOnly documentDate,
        string detail,
        Func<PayrollRunLine, EntidadInstitucional>? terceroPorLinea,
        CancellationToken ct)
    {
        // --- centro de costo de cada empleado (código legado → Id); sin centro, la cuenta decide ---
        var codigosCc = employees.Select(e => e.Employee.CostCenterId).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking()
            .Where(c => !c.IsDeleted && c.LegacyCode != null && codigosCc.Contains(c.LegacyCode))
            .Select(c => new { c.Id, c.LegacyCode })
            .ToListAsync(ct);
        var ccPorCodigo = centros.ToDictionary(c => c.LegacyCode!, c => c.Id, StringComparer.OrdinalIgnoreCase);

        // --- entidades institucionales de los empleados: nombre y persona vinculada (FR-088) ---
        var entidades = await CargarEntidadesAsync(employees.Select(e => e.Employee).ToList(), ct);

        // --- agrupar por (concepto, centro de costo, tercero) ---
        var grupos = new Dictionary<Grupo, decimal>();
        foreach (var (_, employee, lines) in employees)
        {
            int? cc = !string.IsNullOrWhiteSpace(employee.CostCenterId) && ccPorCodigo.TryGetValue(employee.CostCenterId, out var id) ? id : null;
            foreach (var line in lines.Where(l => l.AffectsAccounting && l.Amount != 0m))
            {
                var entidad = terceroPorLinea?.Invoke(line) ?? TercerosDeNomina.EntidadDe(line.ConceptCode, line.Nature);
                var (personId, nombre) = entidad == EntidadInstitucional.Ninguna
                    ? (line.Nature is ConceptNature.Earning or ConceptNature.Deduction ? employee.PersonId : (int?)null, null)
                    : entidades.Buscar(entidad, IdDeEntidad(employee, entidad));
                var key = new Grupo(line.ConceptCode, cc, personId, entidad, nombre);
                grupos[key] = grupos.GetValueOrDefault(key) + line.Amount;
            }
        }

        // --- cuentas por concepto ---
        var codigos = grupos.Keys.Select(k => k.Code).Distinct().ToList();
        var cuentas = await db.PayrollConceptDefinitionAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && codigos.Contains(a.ConceptCode))
            .ToListAsync(ct);
        var sinCuentas = new List<string>();
        var asientos = new List<(Grupo Grupo, int DebitAccountId, int CreditAccountId, decimal Amount)>();
        foreach (var (grupo, amount) in grupos.OrderBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.CostCenterId).ThenBy(g => g.Key.PersonId))
        {
            var fila = cuentas.FirstOrDefault(a => a.ConceptCode.Equals(grupo.Code, StringComparison.OrdinalIgnoreCase) && a.CostCenterId == grupo.CostCenterId)
                    ?? cuentas.FirstOrDefault(a => a.ConceptCode.Equals(grupo.Code, StringComparison.OrdinalIgnoreCase) && a.CostCenterId == null);
            if (fila is null) { if (!sinCuentas.Contains(grupo.Code)) sinCuentas.Add(grupo.Code); continue; }
            asientos.Add((grupo, fila.DebitAccountId, fila.CreditAccountId, amount));
        }
        if (sinCuentas.Count > 0)
            return Result.Failure<AccountingDocument>(new Error("Payroll.ConceptWithoutAccounts",
                $"Conceptos liquidados sin cuentas contables configuradas: {string.Join(", ", sinCuentas)}. " +
                "Configúrelas en Nómina › Conceptos › Cuentas antes de aprobar."));

        if (asientos.Count == 0)
            return Result.Failure<AccountingDocument>(new Error("Payroll.NothingToPost",
                "La corrida no tiene líneas que afecten contabilidad."));

        // --- FR-088: una entidad sin persona vinculada sólo es problema si la cuenta exige tercero; se dice cuál ---
        var idsDeCuenta = asientos.SelectMany(a => new[] { a.DebitAccountId, a.CreditAccountId }).Distinct().ToList();
        var exigenTercero = await db.ChartOfAccounts.AsNoTracking()
            .Where(c => idsDeCuenta.Contains(c.Id) && c.RequiresThirdParty)
            .Select(c => new { c.Id, c.Code })
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        foreach (var a in asientos.Where(a => a.Grupo.Entidad != EntidadInstitucional.Ninguna && a.Grupo.PersonId is null))
        {
            var cuenta = exigenTercero.GetValueOrDefault(a.DebitAccountId) ?? exigenTercero.GetValueOrDefault(a.CreditAccountId);
            if (cuenta is null) continue;
            var nombreEntidad = a.Grupo.EntidadNombre ?? $"la {TercerosDeNomina.Nombre(a.Grupo.Entidad)} del empleado";
            return Result.Failure<AccountingDocument>(new ErrorConDatos("Accounting.Line.ThirdPartyRequired",
                $"La cuenta {cuenta} del concepto {a.Grupo.Code} exige tercero y {nombreEntidad} no tiene persona vinculada. " +
                $"Vincúlela en {TercerosDeNomina.Pantalla(a.Grupo.Entidad)} antes de aprobar.",
                new { accountCode = cuenta, concept = a.Grupo.Code, entity = nombreEntidad, entityKind = a.Grupo.Entidad.ToString() }));
        }

        var lineas = new List<PostingLine>(asientos.Count * 2);
        foreach (var a in asientos)
        {
            var valor = Math.Abs(a.Amount);
            var (debito, credito) = a.Amount >= 0m ? (a.DebitAccountId, a.CreditAccountId) : (a.CreditAccountId, a.DebitAccountId);
            var detalle = a.Grupo.EntidadNombre is null ? $"Nómina {a.Grupo.Code}" : $"Nómina {a.Grupo.Code} · {a.Grupo.EntidadNombre}";
            lineas.Add(new PostingLine { AccountId = debito, Debit = valor, Detail = detalle, CostCenterId = a.Grupo.CostCenterId, PersonId = a.Grupo.PersonId });
            lineas.Add(new PostingLine { AccountId = credito, Credit = valor, Detail = detalle, CostCenterId = a.Grupo.CostCenterId, PersonId = a.Grupo.PersonId });
        }

        return await poster.PrepareAsync(new PostingRequest(VoucherCode, documentDate, detail, origen, lineas), ct);
    }

    /// <summary>Comprobante reverso del original (FR-032) por el contrato: mismas líneas con débito y crédito invertidos, referencia en ambos sentidos.</summary>
    public Task<Result<AccountingDocument>> ReverseAsync(AccountingDocument original, DateOnly documentDate, string reason, CancellationToken ct) =>
        ReverseAsync(original, documentDate, reason, new AccountingOrigin(ModuleCode, original.SourceType ?? SourceType, original.SourcePublicId ?? Guid.Empty), ct);

    /// <summary>Reverso con el origen explícito (una liquidación especial conserva su <c>SourceType</c> en el espejo).</summary>
    public Task<Result<AccountingDocument>> ReverseAsync(AccountingDocument original, DateOnly documentDate, string reason, AccountingOrigin origen, CancellationToken ct) =>
        poster.PrepareReversalAsync(original, documentDate, reason, origen, ct);

    // --------------------------------------------------------------------------------------------

    private static int IdDeEntidad(Employee e, EntidadInstitucional entidad) => entidad switch
    {
        EntidadInstitucional.Eps => e.HealthInsuranceId,
        EntidadInstitucional.Arl => e.WorkRiskId,
        EntidadInstitucional.FondoDePensiones => e.PensionFundId,
        EntidadInstitucional.FondoDeCesantias => e.SeveranceFundId,
        EntidadInstitucional.CajaDeCompensacion => e.FamilySubsidyId,
        _ => 0,
    };

    private sealed class Entidades
    {
        private readonly Dictionary<(EntidadInstitucional, int), (int? PersonId, string Nombre)> _filas = [];

        public void Agregar(EntidadInstitucional entidad, int id, int? personId, string nombre) => _filas[(entidad, id)] = (personId, nombre);

        /// <summary>Persona vinculada y nombre de la entidad; si la fila no existe, nombra la clase y el id para que el error sea accionable.</summary>
        public (int? PersonId, string Nombre) Buscar(EntidadInstitucional entidad, int id) =>
            _filas.TryGetValue((entidad, id), out var f) ? f : (null, $"la {TercerosDeNomina.Nombre(entidad)} #{id} (no existe en el catálogo)");
    }

    private async Task<Entidades> CargarEntidadesAsync(IReadOnlyList<Employee> empleados, CancellationToken ct)
    {
        var e = new Entidades();
        var eps = empleados.Select(x => x.HealthInsuranceId).Distinct().ToList();
        foreach (var f in await db.HealthInsuranceProviders.AsNoTracking().Where(x => !x.IsDeleted && eps.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.Eps, f.Id, f.PersonId, $"la EPS {f.Name}");
        var arl = empleados.Select(x => x.WorkRiskId).Distinct().ToList();
        foreach (var f in await db.WorkRiskProviders.AsNoTracking().Where(x => !x.IsDeleted && arl.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.Arl, f.Id, f.PersonId, $"la ARL {f.Name}");
        var pensiones = empleados.Select(x => x.PensionFundId).Distinct().ToList();
        foreach (var f in await db.PensionProviders.AsNoTracking().Where(x => !x.IsDeleted && pensiones.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.FondoDePensiones, f.Id, f.PersonId, $"el fondo de pensiones {f.Name}");
        var cesantias = empleados.Select(x => x.SeveranceFundId).Distinct().ToList();
        foreach (var f in await db.SeveranceProviders.AsNoTracking().Where(x => !x.IsDeleted && cesantias.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.FondoDeCesantias, f.Id, f.PersonId, $"el fondo de cesantías {f.Name}");
        var cajas = empleados.Select(x => x.FamilySubsidyId).Distinct().ToList();
        foreach (var f in await db.FamilyCompensationFunds.AsNoTracking().Where(x => !x.IsDeleted && cajas.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.CajaDeCompensacion, f.Id, f.PersonId, $"la caja de compensación {f.Name}");
        return e;
    }
}
