using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Files;

/// <summary>Una línea del archivo de descuentos ya resuelta: la novedad, el crédito, el deudor y los valores por origen para el escritor. (nuevo)</summary>
public sealed record LineaDeDescuento(LineaDeNovedad Novedad, Loan Credito, Person Deudor, BankFileLineValues Valores);

/// <summary>Lo que necesitan la vista previa y la generación del archivo de un convenio y período. (nuevo)</summary>
public sealed record ArchivoDeDescuentosPreparado(
    PayrollAgreement Convenio,
    PayrollAgreementVersion Version,
    int NumeroDeVersion,
    PeriodoDeLibranza Periodo,
    BankFileFormat Formato,
    BankFileContext Contexto,
    PayrollDeductionFile? Base,
    ArchivoDeNovedades Novedades,
    IReadOnlyList<LineaDeDescuento> Lineas,
    DateOnly EnviarA,
    Person Pagaduria);

/// <summary>
/// Arma las líneas del archivo de descuentos de un convenio y período (feature 013, K2, T0835; T78, FR-111, FR-113; adaptadores-y-archivos.md §6.3;
/// molde <see cref="LoanDisbursementLines"/>): lee en bloque con <see cref="CargadorDeReconstruccion"/> lo exigible del período y lo vencido de cada
/// crédito vivo con vínculo vigente al convenio al fin del período, llama a <see cref="NovedadesDeLibranza"/> contra el último archivo
/// <c>Sent</c> del convenio y arma los valores por origen para <see cref="FlatFileWriter"/>:
/// <list type="bullet">
/// <item><c>PayeeDocumentType</c>, <c>PayeeDocument</c>, <c>PayeeFullName</c>, nombres y apellidos: el deudor; <c>PayerEmployeeId</c>: su
/// identificación ante la pagaduría (copia del vínculo);</item>
/// <item><c>LoanNumber</c> y <c>Concept</c>: el número del crédito; <c>InstallmentNumber</c>, <c>DueDate</c>: la cuota del período;</item>
/// <item><c>Amount</c>: lo que se pide descontar (exigible más lo vencido si el convenio lo lleva); <c>PastDueAmount</c>: lo vencido;</item>
/// <item><c>NoveltyKind</c>, <c>PeriodYear</c>, <c>PeriodMonth</c>, <c>LibranzaDate</c>.</item>
/// </list>
/// Lo vencido viaja si la versión lo admite (<c>IncludesPastDue</c>) o si su regla del no aplicado es «incluir en el archivo siguiente». No escribe.
/// (nuevo)
/// </summary>
public sealed class PayrollDeductionLines(IApplicationDbContext db, CargadorDeReconstruccion cargador, ConveniosDeLibranza convenios, IDateTimeService reloj)
{
    /// <summary>Los conceptos que se piden descontar (los de la cuota; no el anticipo ni el saldo a favor).</summary>
    private static readonly LoanConcept[] Cobrables =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    ];

    /// <summary>El lote de la vista previa, que todavía no tiene número de archivo.</summary>
    public const string LoteDeVistaPrevia = "VISTA-PREVIA";

    /// <summary>
    /// Prepara el archivo del <paramref name="periodo"/>. El formato es el de ida sellado en la versión vigente al inicio del período.
    /// </summary>
    public async Task<ArchivoDeDescuentosPreparado> PrepararAsync(PayrollAgreement convenio, PayrollAgreementVersion version, PeriodoDeLibranza periodo,
        string lote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(convenio);
        ArgumentNullException.ThrowIfNull(version);
        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields).FirstAsync(f => f.Id == version.OutboundFormatId, ct);
        var versiones = await convenios.VersionesAsync(convenio.Id, ct);
        var numeroDeVersion = versiones.Select((v, i) => (v.Id, i)).First(x => x.Id == version.Id).i + 1;
        var pagaduria = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == convenio.PaymasterPersonId, ct);
        var reglas = ReglasDelConvenio.Leer(version.NoveltyRulesJson);
        var enviarA = periodo.Dia(reglas.SendDay ?? version.CutoffDay);

        // Los vínculos vigentes al fin del período y sus créditos vivos.
        var vinculos = (await db.LoanPayrollLinks.AsNoTracking()
                .Where(v => v.PayrollAgreementId == convenio.Id && !v.IsDeleted && v.ValidFrom <= periodo.Hasta && (v.ValidTo == null || v.ValidTo >= periodo.Hasta))
                .ToListAsync(ct))
            .GroupBy(v => v.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.ValidFrom).First());
        var ids = vinculos.Keys.ToList();
        var vivos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id) && !l.IsDeleted && l.Status == LoanStatus.Active).ToListAsync(ct);
        var cargados = await cargador.CargarAsync(vivos.Select(l => l.Id).ToList(), null, periodo.Hasta, ct);

        // El archivo base: el último enviado del convenio antes de este período.
        var baseFile = await UltimoEnviadoAsync(convenio.Id, periodo, ct);
        var lineasBase = baseFile is null
            ? []
            : await db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == baseFile.Id).ToListAsync(ct);
        var enElBase = lineasBase.Select(l => l.LoanId).ToHashSet();

        var cuotas = await CuotasDelPlanAsync(vivos.Select(l => l.Id).ToList(), ct);
        var exigibles = new List<ExigibleDeLibranza>();
        foreach (var credito in vivos)
        {
            if (!cargados.TryGetValue(credito.Id, out var cargado) || !cargado.Listo) continue;
            var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, periodo.Hasta);
            if (estado.Plan is null) continue;
            var delPeriodo = estado.Plan.Cuotas.Where(c => c.Vencimiento >= periodo.Desde && c.Vencimiento <= periodo.Hasta).OrderBy(c => c.Numero).ToList();
            var anteriores = estado.Plan.Cuotas.Where(c => c.Vencimiento < periodo.Desde).Select(c => c.Numero).ToHashSet();
            var numerosDelPeriodo = delPeriodo.Select(c => c.Numero).ToHashSet();
            var exigible = estado.Saldos.Where(s => s.Cuota is { } n && numerosDelPeriodo.Contains(n) && Cobrables.Contains(s.Concepto) && s.Valor > 0m).Sum(s => s.Valor);
            var vencido = estado.Saldos.Where(s => s.Cuota is { } n && anteriores.Contains(n) && Cobrables.Contains(s.Concepto) && s.Valor > 0m).Sum(s => s.Valor);
            if (exigible + vencido <= 0m && !enElBase.Contains(credito.Id)) continue;
            var vinculo = vinculos[credito.Id];
            var primera = delPeriodo.FirstOrDefault();
            exigibles.Add(new ExigibleDeLibranza(credito.Id, vinculo.Id, credito.DebtorPersonId, vinculo.LibranzaDate ?? credito.OpenedOn, exigible, vencido)
            {
                EmployeeIdAtPaymaster = vinculo.EmployeeIdAtPaymaster,
                InstallmentNumber = primera?.Numero,
                DueDate = primera?.Vencimiento,
                RepaymentInstallmentId = primera is null ? null : cuotas.GetValueOrDefault((credito.Id, primera.Numero)),
            });
        }

        var novedades = NovedadesDeLibranza.Calcular(exigibles,
            lineasBase.Select(l => new LineaDelArchivoBase(l.LoanId, l.LoanPayrollLinkId, l.PersonId, l.LibranzaDate, l.TotalAmount, l.NoveltyKind)
            {
                EmployeeIdAtPaymaster = l.EmployeeIdAtPaymaster,
            }),
            version.IncludesPastDue || LlevaLoVencido(version));

        var deCredito = novedades.Lineas.Select(l => l.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => deCredito.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var deudores = creditos.Values.Select(l => l.DebtorPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => deudores.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var contexto = new BankFileContext
        {
            CompanyNit = empresa?.TaxId ?? string.Empty,
            CompanyNitDv = empresa?.TaxIdCheckDigit,
            CompanyName = empresa?.Name ?? string.Empty,
            SourceAgreementCode = convenio.Code,
            PaymentDate = enviarA,
            GeneratedAt = reloj.UtcNow,
            Sequence = numeroDeVersion,
            BatchReference = lote,
            Year = periodo.Anio,
        };

        var lineas = novedades.Lineas.Select(n =>
        {
            var credito = creditos[n.LoanId];
            var deudor = personas[credito.DebtorPersonId];
            var valores = new BankFileLineValues();
            valores[BankFieldSource.PayeeDocumentType] = LoanDisbursementLines.TipoDeDocumento(deudor.IdType);
            valores[BankFieldSource.PayeeDocument] = deudor.TaxId;
            valores[BankFieldSource.PayeeFullName] = ReferenciasDeGobierno.Nombre(deudor);
            valores[BankFieldSource.PayeeFirstNames] = Unir(deudor.FirstName, deudor.OtherNames);
            valores[BankFieldSource.PayeeLastNames] = Unir(deudor.LastName, deudor.SecondLastName);
            valores[BankFieldSource.PayeeEmail] = deudor.Email;
            valores[BankFieldSource.PayerEmployeeId] = n.EmployeeIdAtPaymaster;
            valores[BankFieldSource.LoanNumber] = credito.Number;
            valores[BankFieldSource.Concept] = credito.Number;
            valores[BankFieldSource.InstallmentNumber] = n.InstallmentNumber;
            valores[BankFieldSource.DueDate] = n.DueDate;
            valores[BankFieldSource.Amount] = n.Total;
            valores[BankFieldSource.PastDueAmount] = n.Vencido;
            valores[BankFieldSource.NoveltyKind] = n.Novedad.ToString();
            valores[BankFieldSource.PeriodYear] = periodo.Anio;
            valores[BankFieldSource.PeriodMonth] = periodo.Mes;
            valores[BankFieldSource.LibranzaDate] = n.LibranzaDate;
            return new LineaDeDescuento(n, credito, deudor, valores);
        }).ToList();

        return new ArchivoDeDescuentosPreparado(convenio, version, numeroDeVersion, periodo, formato, contexto, baseFile, novedades, lineas, enviarA, pagaduria);
    }

    /// <summary>El contexto con el lote definitivo (el número del archivo).</summary>
    public static BankFileContext ConLote(BankFileContext c, string lote) => new()
    {
        CompanyNit = c.CompanyNit, CompanyNitDv = c.CompanyNitDv, CompanyName = c.CompanyName, SourceAgreementCode = c.SourceAgreementCode,
        PaymentDate = c.PaymentDate, GeneratedAt = c.GeneratedAt, Sequence = c.Sequence, BatchReference = lote, Year = c.Year,
    };

    /// <summary>El último archivo enviado del convenio anterior al período (por período y versión).</summary>
    public async Task<PayrollDeductionFile?> UltimoEnviadoAsync(int convenioId, PeriodoDeLibranza periodo, CancellationToken ct) =>
        (await db.PayrollDeductionFiles.AsNoTracking()
            .Where(f => f.PayrollAgreementId == convenioId && f.Status == PayrollFileStatus.Sent && !f.IsDeleted)
            .ToListAsync(ct))
        .Where(f => (f.PeriodYear, f.PeriodMonth, f.SubPeriodNumber).CompareTo(((short)periodo.Anio, (byte)periodo.Mes, (byte)periodo.SubPeriodo)) < 0)
        .OrderByDescending(f => f.PeriodYear).ThenByDescending(f => f.PeriodMonth).ThenByDescending(f => f.SubPeriodNumber).ThenByDescending(f => f.Version)
        .FirstOrDefault();

    /// <summary>Las filas de las cuotas del plan vigente (la última versión) de cada crédito, por número.</summary>
    private async Task<IReadOnlyDictionary<(int LoanId, int Numero), long>> CuotasDelPlanAsync(IReadOnlyCollection<int> creditos, CancellationToken ct)
    {
        if (creditos.Count == 0) return new Dictionary<(int, int), long>();
        var ids = creditos.ToList();
        var versiones = (await db.RepaymentScheduleVersions.AsNoTracking().Where(v => ids.Contains(v.LoanId) && !v.IsDeleted)
                .Select(v => new { v.Id, v.LoanId, v.VersionNumber }).ToListAsync(ct))
            .GroupBy(v => v.LoanId).ToDictionary(g => g.MaxBy(v => v.VersionNumber)!.Id, g => g.Key);
        var vigentes = versiones.Keys.ToList();
        var filas = await db.RepaymentInstallments.AsNoTracking().Where(i => vigentes.Contains(i.ScheduleVersionId))
            .Select(i => new { i.Id, i.ScheduleVersionId, i.InstallmentNumber }).ToListAsync(ct);
        return filas.ToDictionary(i => (versiones[i.ScheduleVersionId], i.InstallmentNumber), i => i.Id);
    }

    /// <summary>¿El archivo lleva lo vencido? Si la versión lo admite o si su regla del no aplicado es incluirlo en el archivo siguiente (FR-115).</summary>
    public static bool LlevaLoVencido(PayrollAgreementVersion version) =>
        version.IncludesPastDue || version.NonDeductionAction == NonDeductionFollowUpAction.IncludedInNextFile;

    private static string? Unir(params string?[] partes)
    {
        var texto = string.Join(" ", partes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        return texto.Length == 0 ? null : texto;
    }
}
