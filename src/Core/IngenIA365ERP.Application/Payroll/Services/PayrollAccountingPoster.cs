using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
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
/// <para>
/// Cuenta por entidad: si la EPS, el fondo, la ARL o la caja de la línea tiene cuenta propia
/// (<c>PayrollAccountId</c> de su catálogo), esa cuenta reemplaza la cuenta <b>crédito</b> del concepto —la
/// del pasivo con la entidad— y el crédito lleva a la entidad como tercero. Qué líneas son de una entidad lo
/// dice <see cref="TercerosDeNomina.EntidadDeLaCuenta"/>: aportes y provisiones con entidad, y las deducciones
/// de ley del empleado (salud, pensión, solidaridad), en las que el débito sigue siendo del empleado. El débito
/// (gasto o salarios por pagar) siempre es el del concepto. Sin cuenta propia, todo queda como antes.
/// </para>
/// </summary>
public sealed class PayrollAccountingPoster(IApplicationDbContext db, AccountingPoster poster)
{
    public const string VoucherCode = "NM";
    public const string ModuleCode = ModuloContable.Nomina;

    /// <summary>El <c>SourceType</c> de la nómina ordinaria; las liquidaciones especiales llevan el suyo por tipo (<see cref="PayrollRun.SourceTypeNameDe"/>, R2).</summary>
    public const string SourceType = "PayrollRun";

    /// <summary>
    /// Un renglón del comprobante antes de cuentas. <c>PersonId</c>/<c>Entidad</c> es el tercero del grupo (el de las dos
    /// líneas si no hay cuenta propia); <c>CuentaDeEntidad</c> es la cuenta propia de la entidad, que va al crédito con
    /// <c>PersonaDelCredito</c> como tercero.
    /// </summary>
    private sealed record Grupo(string Code, int? CostCenterId, int? PersonId, EntidadInstitucional Entidad, string? EntidadNombre,
        int? CuentaDeEntidad = null, int? PersonaDelCredito = null, EntidadInstitucional EntidadDelCredito = EntidadInstitucional.Ninguna, string? NombreDelCredito = null)
    {
        public int? PersonaCredito => CuentaDeEntidad is null ? PersonId : PersonaDelCredito;
        public EntidadInstitucional EntidadCredito => CuentaDeEntidad is null ? Entidad : EntidadDelCredito;
        public string? NombreCredito => CuentaDeEntidad is null ? EntidadNombre : NombreDelCredito;
    }

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
        // --- centro de costo de cada empleado: la FK PAY_Employees.CostCenterRefId (nula = sin centro) ---
        // Hasta el 2026-10-22 se casaba el texto Employee.CostCenterId con COR_CostCenters.LegacyCode, nadie lo
        // escribía y un código que no casaba dejaba el centro en nulo sin avisar.
        var idsDeCentros = employees.Select(e => e.Employee.CostCenterRefId).Where(c => c is not null).Select(c => c!.Value).Distinct().ToList();
        var centrosEliminados = idsDeCentros.Count == 0 ? new HashSet<int>()
            : (await db.CostCenters.AsNoTracking().Where(c => idsDeCentros.Contains(c.Id) && c.IsDeleted).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var conCentroEliminado = employees.Where(e => e.Employee.CostCenterRefId is { } c && centrosEliminados.Contains(c)).Select(e => e.Employee).ToList();
        if (conCentroEliminado.Count > 0)
            return Result.Failure<AccountingDocument>(await ErrorDeEmpleadosAsync("Payroll.Employee.CostCenterDeleted",
                "tiene asignado un centro de costo que fue eliminado", "tienen asignado un centro de costo que fue eliminado",
                conCentroEliminado, null, ct));

        // --- entidades institucionales de los empleados: nombre y persona vinculada (FR-088) ---
        var entidades = await CargarEntidadesAsync(employees.Select(e => e.Employee).ToList(), ct);

        // --- cuentas por concepto (antes de agrupar: deciden el tercero de las líneas sin contraparte) ---
        var codigos = employees.SelectMany(e => e.Lines).Where(l => l.AffectsAccounting && l.Amount != 0m)
            .Select(l => l.ConceptCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cuentas = await db.PayrollConceptDefinitionAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && codigos.Contains(a.ConceptCode))
            .ToListAsync(ct);
        PayrollConceptDefinitionAccount? FilaDe(string code, int? cc) =>
            cuentas.FirstOrDefault(a => a.ConceptCode.Equals(code, StringComparison.OrdinalIgnoreCase) && a.CostCenterId == cc)
            ?? cuentas.FirstOrDefault(a => a.ConceptCode.Equals(code, StringComparison.OrdinalIgnoreCase) && a.CostCenterId == null);
        var idsDeCuentaDeConceptos = cuentas.SelectMany(a => new[] { a.DebitAccountId, a.CreditAccountId }).Distinct().ToList();
        var cuentasQueExigenTercero = (await db.ChartOfAccounts.AsNoTracking()
            .Where(c => idsDeCuentaDeConceptos.Contains(c.Id) && c.RequiresThirdParty)
            .Select(c => c.Id).ToListAsync(ct)).ToHashSet();

        // --- agrupar por (concepto, centro de costo, tercero) ---
        var grupos = new Dictionary<Grupo, decimal>();
        var empleadosDelGrupo = new Dictionary<Grupo, List<Employee>>();
        foreach (var (_, employee, lines) in employees)
        {
            int? cc = employee.CostCenterRefId;
            foreach (var line in lines.Where(l => l.AffectsAccounting && l.Amount != 0m))
            {
                var entidad = terceroPorLinea?.Invoke(line) ?? TercerosDeNomina.EntidadDe(line.ConceptCode, line.Nature);
                var (personId, nombre, _) = entidad == EntidadInstitucional.Ninguna
                    ? (line.Nature is ConceptNature.Earning or ConceptNature.Deduction ? employee.PersonId : (int?)null, null, null)
                    : entidades.Buscar(entidad, IdDeEntidad(employee, entidad));
                // Sin contraparte (ajustes y provisiones de prestaciones, SENA, ICBF) la línea va sin tercero, salvo que la
                // cuenta del concepto lo exija: entonces el tercero es el empleado. Hasta el 2026-11-13 la definitiva fallaba
                // con «La cuenta … exige tercero» en los cuatro *_AJUSTE_PROV si la empresa pedía tercero en gasto y provisión.
                if (entidad == EntidadInstitucional.Ninguna && personId is null
                    && FilaDe(line.ConceptCode, cc) is { } filaDelConcepto
                    && (cuentasQueExigenTercero.Contains(filaDelConcepto.DebitAccountId) || cuentasQueExigenTercero.Contains(filaDelConcepto.CreditAccountId)))
                    personId = employee.PersonId;
                var key = new Grupo(line.ConceptCode, cc, personId, entidad, nombre);
                // Cuenta por entidad: la cuenta propia de la entidad reemplaza el crédito del concepto.
                var entidadDeLaCuenta = TercerosDeNomina.EntidadDeLaCuenta(line.ConceptCode, line.Nature, entidad);
                if (entidadDeLaCuenta != EntidadInstitucional.Ninguna
                    && entidades.Buscar(entidadDeLaCuenta, IdDeEntidad(employee, entidadDeLaCuenta)) is { AccountId: { } cuentaPropia } deLaCuenta)
                    key = key with { CuentaDeEntidad = cuentaPropia, PersonaDelCredito = deLaCuenta.PersonId, EntidadDelCredito = entidadDeLaCuenta, NombreDelCredito = deLaCuenta.Nombre };
                grupos[key] = grupos.GetValueOrDefault(key) + line.Amount;
                if (!empleadosDelGrupo.TryGetValue(key, out var delGrupo)) empleadosDelGrupo[key] = delGrupo = [];
                if (!delGrupo.Contains(employee)) delGrupo.Add(employee);
            }
        }

        var sinCuentas = new List<string>();
        var asientos = new List<(Grupo Grupo, int DebitAccountId, int CreditAccountId, decimal Amount)>();
        foreach (var (grupo, amount) in grupos.OrderBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.CostCenterId).ThenBy(g => g.Key.PersonId))
        {
            var fila = FilaDe(grupo.Code, grupo.CostCenterId);
            if (fila is null) { if (!sinCuentas.Contains(grupo.Code)) sinCuentas.Add(grupo.Code); continue; }
            asientos.Add((grupo, fila.DebitAccountId, grupo.CuentaDeEntidad ?? fila.CreditAccountId, amount));
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
        foreach (var a in asientos)
        {
            // Cada lado con su tercero: sin cuenta propia los dos llevan el del grupo (y se mira si alguna de las dos cuentas lo
            // exige, como siempre); con cuenta propia, el crédito lleva a la entidad de esa cuenta y el débito, el del grupo.
            var lados = a.Grupo.CuentaDeEntidad is null
                ? new[] { (Cuenta: exigenTercero.GetValueOrDefault(a.DebitAccountId) ?? exigenTercero.GetValueOrDefault(a.CreditAccountId), Entidad: a.Grupo.Entidad, Persona: a.Grupo.PersonId, Nombre: a.Grupo.EntidadNombre) }
                : [(exigenTercero.GetValueOrDefault(a.DebitAccountId), a.Grupo.Entidad, a.Grupo.PersonId, a.Grupo.EntidadNombre),
                   (exigenTercero.GetValueOrDefault(a.CreditAccountId), a.Grupo.EntidadCredito, a.Grupo.PersonaCredito, a.Grupo.NombreCredito)];
            foreach (var (cuenta, entidad, persona, nombre) in lados)
            {
                if (cuenta is null || entidad == EntidadInstitucional.Ninguna || persona is not null) continue;
                var nombreEntidad = nombre ?? $"la {TercerosDeNomina.Nombre(entidad)} del empleado";
                return Result.Failure<AccountingDocument>(new ErrorConDatos("Accounting.Line.ThirdPartyRequired",
                    $"La cuenta {cuenta} del concepto {a.Grupo.Code} exige tercero y {nombreEntidad} no tiene persona vinculada. " +
                    $"Vincúlela en {TercerosDeNomina.Pantalla(entidad)} antes de aprobar.",
                    new { accountCode = cuenta, concept = a.Grupo.Code, entity = nombreEntidad, entityKind = entidad.ToString() }));
            }
        }

        // --- centro de costo: si la cuenta del concepto lo EXIGE y el empleado no lo tiene, se dice a quién asignárselo
        //     (el contrato sólo diría «la cuenta X exige centro de costo», sin nombrar a nadie) ---
        var manejaCentro = await db.ChartOfAccounts.AsNoTracking()
            .Where(c => idsDeCuenta.Contains(c.Id) && c.RequiresCostCenter)
            .Select(c => new { c.Id, c.Code })
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var sinCentro = new Dictionary<Employee, HashSet<string>>();
        foreach (var a in asientos.Where(a => a.Grupo.CostCenterId is null))
        {
            var exige = manejaCentro.GetValueOrDefault(a.DebitAccountId) ?? manejaCentro.GetValueOrDefault(a.CreditAccountId);
            if (exige is null) continue;
            foreach (var empleado in empleadosDelGrupo[a.Grupo])
            {
                if (!sinCentro.TryGetValue(empleado, out var detalleDelEmpleado)) sinCentro[empleado] = detalleDelEmpleado = [];
                detalleDelEmpleado.Add($"{a.Grupo.Code} (cuenta {exige})");
            }
        }
        if (sinCentro.Count > 0)
            return Result.Failure<AccountingDocument>(await ErrorDeEmpleadosAsync("Payroll.Employee.CostCenterRequired",
                "no tiene centro de costo y la cuenta contable de uno de sus conceptos lo exige",
                "no tienen centro de costo y la cuenta contable de alguno de sus conceptos lo exige",
                sinCentro.Keys.ToList(), sinCentro, ct));

        // El contrato descarta el centro en la línea cuya cuenta no lo maneja (AccountingPoster), así que el centro del
        // empleado viaja en todas las líneas y sólo "pega" donde la cuenta lo maneja.
        // Un valor negativo invierte los lados, cada cuenta con su tercero. Se suman las líneas idénticas (misma cuenta, lado,
        // centro, tercero y detalle): sin cuentas por entidad no hay ninguna repetida, y con ellas el crédito de una deducción
        // de ley queda en una línea por entidad y no en una por empleado.
        var lineas = new List<PostingLine>(asientos.Count * 2);
        foreach (var a in asientos)
        {
            var valor = Math.Abs(a.Amount);
            var detalleDebito = a.Grupo.EntidadNombre is null ? $"Nómina {a.Grupo.Code}" : $"Nómina {a.Grupo.Code} · {a.Grupo.EntidadNombre}";
            var detalleCredito = a.Grupo.NombreCredito is null ? $"Nómina {a.Grupo.Code}" : $"Nómina {a.Grupo.Code} · {a.Grupo.NombreCredito}";
            var ladoDebito = (Cuenta: a.DebitAccountId, Persona: a.Grupo.PersonId, Detalle: detalleDebito);
            var ladoCredito = (Cuenta: a.CreditAccountId, Persona: a.Grupo.PersonaCredito, Detalle: detalleCredito);
            var (alDebito, alCredito) = a.Amount >= 0m ? (ladoDebito, ladoCredito) : (ladoCredito, ladoDebito);
            Sumar(lineas, alDebito.Cuenta, valor, 0m, alDebito.Detalle, a.Grupo.CostCenterId, alDebito.Persona);
            Sumar(lineas, alCredito.Cuenta, 0m, valor, alCredito.Detalle, a.Grupo.CostCenterId, alCredito.Persona);
        }

        return await poster.PrepareAsync(new PostingRequest(VoucherCode, documentDate, detail, origen, lineas), ct);
    }

    private static void Sumar(List<PostingLine> lineas, int cuenta, decimal debito, decimal credito, string detalle, int? centro, int? persona)
    {
        var existente = lineas.FindIndex(l => l.AccountId == cuenta && (l.Debit > 0m) == (debito > 0m) && l.CostCenterId == centro
                                              && l.PersonId == persona && l.Detail == detalle);
        if (existente < 0)
        {
            lineas.Add(new PostingLine { AccountId = cuenta, Debit = debito, Credit = credito, Detail = detalle, CostCenterId = centro, PersonId = persona });
            return;
        }
        var l = lineas[existente];
        lineas[existente] = l with { Debit = l.Debit + debito, Credit = l.Credit + credito };
    }

    /// <summary>Comprobante reverso del original (FR-032) por el contrato: mismas líneas con débito y crédito invertidos, referencia en ambos sentidos.</summary>
    public Task<Result<AccountingDocument>> ReverseAsync(AccountingDocument original, DateOnly documentDate, string reason, CancellationToken ct) =>
        ReverseAsync(original, documentDate, reason, new AccountingOrigin(ModuleCode, original.SourceType ?? SourceType, original.SourcePublicId ?? Guid.Empty), ct);

    /// <summary>Reverso con el origen explícito (una liquidación especial conserva su <c>SourceType</c> en el espejo).</summary>
    public Task<Result<AccountingDocument>> ReverseAsync(AccountingDocument original, DateOnly documentDate, string reason, AccountingOrigin origen, CancellationToken ct) =>
        poster.PrepareReversalAsync(original, documentDate, reason, origen, ct);

    // --------------------------------------------------------------------------------------------

    /// <summary>
    /// Error que nombra a los empleados a corregir. <c>data.employees</c> trae cada uno con su ficha, documento y, si
    /// aplica, los conceptos cuya cuenta exige el centro.
    /// </summary>
    private async Task<Error> ErrorDeEmpleadosAsync(string codigo, string causaSingular, string causaPlural,
        IReadOnlyList<Employee> empleados, Dictionary<Employee, HashSet<string>>? conceptosPorEmpleado, CancellationToken ct)
    {
        var personas = empleados.Select(e => e.PersonId).Distinct().ToList();
        var datos = await db.People.AsNoTracking().Where(p => personas.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.TaxId })
            .ToDictionaryAsync(p => p.Id, ct);
        var detalle = empleados
            .Select(e =>
            {
                var p = datos.GetValueOrDefault(e.PersonId);
                var nombre = p is null ? $"empleado #{e.Id}" : NombreDePersona.Completo(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName);
                return new
                {
                    employeePublicId = e.PublicId,
                    name = nombre,
                    document = p?.TaxId ?? "",
                    concepts = conceptosPorEmpleado is not null && conceptosPorEmpleado.TryGetValue(e, out var cs)
                        ? cs.OrderBy(c => c, StringComparer.Ordinal).ToArray() : Array.Empty<string>(),
                };
            })
            .OrderBy(x => x.name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        const int MostrarEnElMensaje = 5;
        var nombres = string.Join(", ", detalle.Take(MostrarEnElMensaje).Select(x => string.IsNullOrEmpty(x.document) ? x.name : $"{x.name} (doc. {x.document})"));
        var resto = detalle.Count > MostrarEnElMensaje ? $" y {detalle.Count - MostrarEnElMensaje} más" : "";
        const string Remedio = "Asigne un centro de costo en la ficha de cada empleado (Nómina › Empleados) antes de aprobar.";
        var mensaje = detalle.Count == 1
            ? $"El empleado {nombres} {causaSingular}. {Remedio}"
            : $"{detalle.Count} empleados ({nombres}{resto}) {causaPlural}. {Remedio}";
        return new ErrorConDatos(codigo, mensaje, new { employees = detalle });
    }

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
        private readonly Dictionary<(EntidadInstitucional, int), (int? PersonId, string Nombre, int? AccountId)> _filas = [];

        public void Agregar(EntidadInstitucional entidad, int id, int? personId, string nombre, int? accountId) => _filas[(entidad, id)] = (personId, nombre, accountId);

        /// <summary>Persona vinculada y nombre de la entidad; si la fila no existe, nombra la clase y el id para que el error sea accionable.</summary>
        public (int? PersonId, string Nombre, int? AccountId) Buscar(EntidadInstitucional entidad, int id) =>
            _filas.TryGetValue((entidad, id), out var f) ? f : (null, $"la {TercerosDeNomina.Nombre(entidad)} #{id} (no existe en el catálogo)", null);
    }

    private async Task<Entidades> CargarEntidadesAsync(IReadOnlyList<Employee> empleados, CancellationToken ct)
    {
        var e = new Entidades();
        var eps = empleados.Select(x => x.HealthInsuranceId).Distinct().ToList();
        foreach (var f in await db.HealthInsuranceProviders.AsNoTracking().Where(x => !x.IsDeleted && eps.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name, x.PayrollAccountId }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.Eps, f.Id, f.PersonId, $"la EPS {f.Name}", f.PayrollAccountId);
        var arl = empleados.Select(x => x.WorkRiskId).Distinct().ToList();
        foreach (var f in await db.WorkRiskProviders.AsNoTracking().Where(x => !x.IsDeleted && arl.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name, x.PayrollAccountId }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.Arl, f.Id, f.PersonId, $"la ARL {f.Name}", f.PayrollAccountId);
        var pensiones = empleados.Select(x => x.PensionFundId).Distinct().ToList();
        foreach (var f in await db.PensionProviders.AsNoTracking().Where(x => !x.IsDeleted && pensiones.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name, x.PayrollAccountId }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.FondoDePensiones, f.Id, f.PersonId, $"el fondo de pensiones {f.Name}", f.PayrollAccountId);
        var cesantias = empleados.Select(x => x.SeveranceFundId).Distinct().ToList();
        foreach (var f in await db.SeveranceProviders.AsNoTracking().Where(x => !x.IsDeleted && cesantias.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name, x.PayrollAccountId }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.FondoDeCesantias, f.Id, f.PersonId, $"el fondo de cesantías {f.Name}", f.PayrollAccountId);
        var cajas = empleados.Select(x => x.FamilySubsidyId).Distinct().ToList();
        foreach (var f in await db.FamilyCompensationFunds.AsNoTracking().Where(x => !x.IsDeleted && cajas.Contains(x.Id)).Select(x => new { x.Id, x.PersonId, x.Name, x.PayrollAccountId }).ToListAsync(ct))
            e.Agregar(EntidadInstitucional.CajaDeCompensacion, f.Id, f.PersonId, $"la caja de compensación {f.Name}", f.PayrollAccountId);
        return e;
    }
}
