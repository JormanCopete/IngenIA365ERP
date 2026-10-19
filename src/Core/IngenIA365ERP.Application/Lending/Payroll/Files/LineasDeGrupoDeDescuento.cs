using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Files;

/// <summary>Una equivalencia de la versión con su grupo de archivo (feature 014, T091). (nuevo)</summary>
public sealed record EquivalenciaConGrupo(EquivalenciaDePagaduria Equivalencia, int? GrupoId);

/// <summary>El código de la pagaduría que cubre una línea, su prioridad dentro del código y el grupo donde va. (nuevo)</summary>
public sealed record CodigoDeLinea(string Codigo, int Prioridad, int? GrupoId, ConceptAllocationRule Regla);

/// <summary>Una cuota de cuenta en el archivo de un grupo, ya resuelta: la cuenta, la cuota, el titular, el código, la novedad y los valores del escritor. (nuevo)</summary>
public sealed record LineaDeCuentaEnDescuento(
    int LineNumber,
    MemberAccount Cuenta,
    AccountInstallment Cuota,
    Person Titular,
    string? EmployeeIdAtPaymaster,
    CodigoDeLinea Codigo,
    decimal Corriente,
    decimal Vencido,
    PayrollNoveltyKind Novedad,
    decimal? Anterior,
    BankFileLineValues Valores)
{
    public decimal Total => Corriente + Vencido;
}

/// <summary>
/// Lo propio del archivo de un <b>grupo</b> del convenio (feature 014, T091; decisión del dueño del 2026-10-18, D36; contrato-de-cuentas §6.4): leer las
/// equivalencias de la versión, resolver el código de la pagaduría de cada línea de Cartera (créditos: naturaleza <c>Placement</c> y su línea; cuentas: su
/// naturaleza y su línea; el concepto es la cuota completa) y armar las líneas de las <b>cuotas de cuentas</b>. Una cuenta entra al archivo de la pagaduría
/// cuando la empresa empleadora de la ficha del asociado titular (<c>COR_Associates.EmployerCompanyId</c>) tiene el NIT de la pagaduría del convenio: es el
/// vínculo que decidió el dueño, porque una cuenta no tiene vínculo de libranza. Una línea que ningún código cubre no va en ningún archivo y se cuenta para
/// avisarlo. No escribe. (nuevo)
/// </summary>
public static class LineasDeGrupoDeDescuento
{
    /// <summary>Las equivalencias vivas de la versión, con su grupo.</summary>
    public static async Task<IReadOnlyList<EquivalenciaConGrupo>> EquivalenciasAsync(IApplicationDbContext db, int versionId, CancellationToken ct) =>
        (await db.PayrollAgreementConceptEquivalences.AsNoTracking().Where(e => e.PayrollAgreementVersionId == versionId && !e.IsDeleted)
            .OrderBy(e => e.Priority).ThenBy(e => e.PaymasterConceptCode).ToListAsync(ct))
        .Select(e => new EquivalenciaConGrupo(new EquivalenciaDePagaduria(e.PaymasterConceptCode, e.AllocationRule, e.Priority,
            EquivalenciasDePagaduria.Leer(e.ItemsJson)), e.FileGroupId))
        .ToList();

    /// <summary>El código que cubre la cuota completa de una línea de Cartera (o nulo: sin equivalencia), con la menor prioridad de sus ítems que la cubren.</summary>
    public static CodigoDeLinea? Resolver(IReadOnlyList<EquivalenciaConGrupo> equivalencias, LineNature naturaleza, string? linea)
    {
        var codigo = linea ?? string.Empty;
        foreach (var e in equivalencias)
        {
            var cubren = e.Equivalencia.Items.Where(i => i.Cubre(naturaleza, codigo, ItemDeEquivalencia.CuotaCompleta)).ToList();
            if (cubren.Count > 0) return new CodigoDeLinea(e.Equivalencia.Codigo, cubren.Min(i => i.Priority), e.GrupoId, e.Equivalencia.Regla);
        }
        return null;
    }

    /// <summary>
    /// Las cuotas de cuentas del grupo: por cuenta viva (activa o inactiva) de aportes, de ahorro permanente o contractual del asociado empleado por la
    /// pagaduría, lo pendiente de la cuota del período y —si el convenio lo lleva— lo pendiente de las anteriores; la novedad frente a la línea de la misma
    /// cuenta en el archivo base del grupo. Numeradas desde <paramref name="primerNumero"/>, por titular, vencimiento y cuenta.
    /// </summary>
    public static async Task<(IReadOnlyList<LineaDeCuentaEnDescuento> Lineas, int SinEquivalencia)> CuentasAsync(IApplicationDbContext db, Person pagaduria,
        PayrollAgreement convenio, IReadOnlyList<EquivalenciaConGrupo> equivalencias, PayrollAgreementFileGroup grupo, PeriodoDeLibranza periodo,
        bool llevaVencido, PayrollDeductionFile? baseFile, int primerNumero, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pagaduria);
        ArgumentNullException.ThrowIfNull(convenio);
        ArgumentNullException.ThrowIfNull(grupo);
        if (string.IsNullOrWhiteSpace(pagaduria.TaxId)) return ([], 0);
        var nit = pagaduria.TaxId.Trim();
        var empresas = await db.EmployerCompanies.AsNoTracking().Where(e => !e.IsDeleted && e.TaxId != null && e.TaxId.Trim() == nit).Select(e => e.Id)
            .ToListAsync(ct);
        if (empresas.Count == 0) return ([], 0);
        var personas = await db.Associates.AsNoTracking().Where(a => !a.IsDeleted && a.EmployerCompanyId != null && empresas.Contains(a.EmployerCompanyId.Value))
            .Select(a => a.PersonId).Distinct().ToListAsync(ct);
        if (personas.Count == 0) return ([], 0);

        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => personas.Contains(a.HolderPersonId) && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive)
                        && (a.Nature == LineNature.Contribution || a.DepositClass == DepositClass.Permanent || a.DepositClass == DepositClass.Contractual))
            .ToListAsync(ct);
        if (cuentas.Count == 0) return ([], 0);
        var lineasIds = cuentas.Select(c => c.CreditLineId).Distinct().ToList();
        var codigosDeLinea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineasIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);

        var sinEquivalencia = 0;
        var delGrupo = new List<(MemberAccount Cuenta, CodigoDeLinea Codigo)>();
        foreach (var c in cuentas)
        {
            var codigo = Resolver(equivalencias, c.Nature, codigosDeLinea.GetValueOrDefault(c.CreditLineId));
            if (codigo is null) sinEquivalencia++;
            else if (codigo.GrupoId == grupo.Id) delGrupo.Add((c, codigo));
        }
        if (delGrupo.Count == 0) return ([], sinEquivalencia);

        var cuotas = (await LectorDeCuotas.DeCuentasAsync(db, delGrupo.Select(x => x.Cuenta.Id).ToList(), ct))
            .Where(q => q.Pendiente > 0m && q.Cuota.DueDate <= periodo.Hasta).GroupBy(q => q.Cuota.MemberAccountId).ToDictionary(g => g.Key, g => g.ToList());
        var titulares = delGrupo.Select(x => x.Cuenta.HolderPersonId).Distinct().ToList();
        var porPersona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => titulares.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var empleados = await EmpleadosAsync(db, convenio.Id, titulares, periodo, ct);
        var enElBase = baseFile is null
            ? new Dictionary<int, decimal>()
            : (await db.PayrollDeductionFileAccountLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == baseFile.Id).ToListAsync(ct))
                .GroupBy(l => l.MemberAccountId).ToDictionary(g => g.Key, g => g.Sum(l => l.TotalAmount));

        var armadas = new List<(MemberAccount Cuenta, CodigoDeLinea Codigo, AccountInstallment Cuota, decimal Corriente, decimal Vencido)>();
        foreach (var (cuenta, codigo) in delGrupo)
        {
            if (!cuotas.TryGetValue(cuenta.Id, out var pendientes)) continue;
            var delPeriodo = pendientes.Where(q => q.Cuota.DueDate >= periodo.Desde).OrderBy(q => q.Cuota.DueDate).ToList();
            var anteriores = pendientes.Where(q => q.Cuota.DueDate < periodo.Desde).OrderBy(q => q.Cuota.DueDate).ToList();
            var corriente = delPeriodo.Sum(q => q.Pendiente);
            var vencido = llevaVencido ? anteriores.Sum(q => q.Pendiente) : 0m;
            if (corriente + vencido <= 0m) continue;
            var cuota = (delPeriodo.FirstOrDefault() ?? anteriores.Last()).Cuota;
            armadas.Add((cuenta, codigo, cuota, corriente, vencido));
        }

        var lineas = armadas
            .OrderBy(x => x.Cuenta.HolderPersonId).ThenBy(x => x.Codigo.Prioridad).ThenBy(x => x.Cuota.DueDate).ThenBy(x => x.Cuenta.Id)
            .Select((x, i) =>
            {
                var titular = porPersona[x.Cuenta.HolderPersonId];
                var empleado = empleados.GetValueOrDefault(titular.Id) ?? titular.TaxId;
                decimal? anterior = enElBase.TryGetValue(x.Cuenta.Id, out var previo) ? previo : null;
                var total = x.Corriente + x.Vencido;
                var novedad = anterior is null ? PayrollNoveltyKind.New : anterior.Value == total ? PayrollNoveltyKind.Unchanged : PayrollNoveltyKind.Change;
                var valores = new BankFileLineValues();
                valores[BankFieldSource.PayeeDocumentType] = LoanDisbursementLines.TipoDeDocumento(titular.IdType);
                valores[BankFieldSource.PayeeDocument] = titular.TaxId;
                valores[BankFieldSource.PayeeFullName] = ReferenciasDeGobierno.Nombre(titular);
                valores[BankFieldSource.PayeeEmail] = titular.Email;
                valores[BankFieldSource.PayerEmployeeId] = empleado;
                valores[BankFieldSource.AccountNumber] = x.Cuenta.Number;
                valores[BankFieldSource.Concept] = x.Codigo.Codigo;
                valores[BankFieldSource.PaymasterConceptCode] = x.Codigo.Codigo;
                valores[BankFieldSource.AccountInstallmentPeriod] = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{x.Cuota.PeriodYear:D4}{x.Cuota.PeriodMonth:D2}");
                valores[BankFieldSource.DueDate] = x.Cuota.DueDate;
                valores[BankFieldSource.Amount] = total;
                valores[BankFieldSource.PastDueAmount] = x.Vencido;
                valores[BankFieldSource.NoveltyKind] = novedad.ToString();
                valores[BankFieldSource.PeriodYear] = periodo.Anio;
                valores[BankFieldSource.PeriodMonth] = periodo.Mes;
                return new LineaDeCuentaEnDescuento(primerNumero + i, x.Cuenta, x.Cuota, titular, empleado, x.Codigo, x.Corriente, x.Vencido, novedad,
                    anterior, valores);
            })
            .ToList();
        return (lineas, sinEquivalencia);
    }

    /// <summary>La identificación del titular ante la pagaduría: la de su vínculo de libranza vigente con el convenio, si tiene un crédito vinculado.</summary>
    private static async Task<IReadOnlyDictionary<int, string?>> EmpleadosAsync(IApplicationDbContext db, int convenioId, IReadOnlyCollection<int> personas,
        PeriodoDeLibranza periodo, CancellationToken ct)
    {
        var ids = personas.ToList();
        var filas = await (from v in db.LoanPayrollLinks.AsNoTracking()
                           join l in db.Loans.AsNoTracking() on v.LoanId equals l.Id
                           where v.PayrollAgreementId == convenioId && !v.IsDeleted && v.EmployeeIdAtPaymaster != null && ids.Contains(l.DebtorPersonId)
                                 && v.ValidFrom <= periodo.Hasta && (v.ValidTo == null || v.ValidTo >= periodo.Hasta)
                           select new { l.DebtorPersonId, v.EmployeeIdAtPaymaster, v.ValidFrom }).ToListAsync(ct);
        return filas.GroupBy(f => f.DebtorPersonId).ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.ValidFrom).First().EmployeeIdAtPaymaster);
    }
}
