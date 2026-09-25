using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Dispersion;

/// <summary>Un empleado de la corrida listo para el archivo, o excluido con motivo.</summary>
public sealed record CandidatoDeDispersion(
    int RunEmployeeId, int EmployeeId, Guid EmployeePublicId, string Nombre, string Documento, string TipoDocumento,
    int? BankId, string? BankName, string? BankCode, int AccountType, string? AccountNumber, decimal NetPay, string? Email,
    BankFileLineValues Valores, DisbursementExcludedDto? Excluido);

/// <summary>Todo lo que hace falta para escribir el archivo de una corrida: contexto, candidatos y etiqueta.</summary>
public sealed record InsumosDeDispersion(PayrollRun Run, string Etiqueta, string Concepto, BankFileContext Contexto, IReadOnlyList<CandidatoDeDispersion> Candidatos);

/// <summary>
/// Arma las líneas del archivo desde la relación de pago de una corrida aprobada (feature 010, US8):
/// el neto del empleado, su banco destino (<c>Employee.DisbursementBankId</c> → <c>COR_Banks.TransferCode</c>,
/// que es el código ACH que ya usaba SOLIDO: D-10) y su cuenta. Quien ya tiene marca de pago vigente
/// o está en otro archivo no anulado de la corrida queda fuera (<c>AlreadyPaid</c>, <c>AlreadySent</c>); quien
/// no tiene cuenta, banco o código de banco, o un campo requerido vacío, queda en pendientes con motivo
/// (FR-032). El módulo decide qué va en cada origen; el motor sólo escribe.
/// </summary>
public sealed class PayrollDisbursementLines(IApplicationDbContext db)
{
    public async Task<Result<InsumosDeDispersion>> CargarAsync(PayrollRun run, BankFileFormat formato, DateOnly paymentDate, DateTime generatedAt, int sequence,
        string? reference, IReadOnlyList<Guid>? soloEmpleados, int? sourceAccountId, string? sourceAccountNumber, string? sourceAccountType, string? sourceBankCode, CancellationToken ct)
    {
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (empresa is null) return Result.Failure<InsumosDeDispersion>(DisbursementErrors.CompanyMissing);

        var etiqueta = await EtiquetaAsync(run, ct);
        var concepto = Concepto(run, etiqueta);
        var contexto = new BankFileContext
        {
            CompanyNit = empresa.TaxId, CompanyNitDv = empresa.TaxIdCheckDigit, CompanyName = empresa.Name,
            SourceAccountNumber = sourceAccountNumber, SourceAccountType = sourceAccountType, SourceBankCode = sourceBankCode,
            SourceAgreementCode = formato.AgreementCode,
            PaymentDate = paymentDate, GeneratedAt = generatedAt, Sequence = sequence,
            BatchReference = string.IsNullOrWhiteSpace(reference) ? $"{etiqueta}".ToUpperInvariant() : reference.Trim(),
            Year = run.Year ?? paymentDate.Year,
        };

        var filas = await (
            from re in db.PayrollRunEmployees.AsNoTracking()
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            orderby p.LastName, p.FirstName
            select new
            {
                re.Id, re.EmployeeId, e.PublicId, p.FirstName, p.LastName, p.SecondLastName, p.OtherNames, p.IdType, p.TaxId, p.Email, re.NetPay,
                e.DisbursementBankId, e.PayrollBankId, e.PayrollBankAccountType, e.PayrollBankAccountNumber,
            }).ToListAsync(ct);

        if (soloEmpleados is { Count: > 0 })
            filas = filas.Where(f => soloEmpleados.Contains(f.PublicId)).ToList();

        var ids = filas.Select(f => f.Id).ToList();
        var pagados = await db.PayrollPayments.AsNoTracking().Where(p => ids.Contains(p.PayrollRunEmployeeId) && !p.IsReverted)
            .Select(p => p.PayrollRunEmployeeId).ToListAsync(ct);
        var enArchivo = await (
            from l in db.BankDisbursementFileLines.AsNoTracking()
            join a in db.BankDisbursementFiles.AsNoTracking() on l.FileId equals a.Id
            where ids.Contains(l.PayrollRunEmployeeId) && a.Status != BankDisbursementFileStatus.Voided
            select new { l.PayrollRunEmployeeId, a.PublicId, a.FileName }).ToListAsync(ct);
        var enArchivoPorEmpleado = enArchivo.GroupBy(x => x.PayrollRunEmployeeId).ToDictionary(g => g.Key, g => g.First());

        var idsBanco = filas.Select(f => f.DisbursementBankId).Where(b => b.HasValue).Select(b => b!.Value).Distinct().ToList();
        var bancos = await db.Banks.AsNoTracking().Where(b => idsBanco.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        var codigosLegado = filas.Where(f => !f.DisbursementBankId.HasValue && !string.IsNullOrWhiteSpace(f.PayrollBankId)).Select(f => f.PayrollBankId!).Distinct().ToList();
        var bancosLegado = codigosLegado.Count == 0 ? new Dictionary<string, Bank>() :
            await db.Banks.AsNoTracking().Where(b => b.LegacyCode != null && codigosLegado.Contains(b.LegacyCode)).ToDictionaryAsync(b => b.LegacyCode!, ct);

        var candidatos = new List<CandidatoDeDispersion>(filas.Count);
        foreach (var f in filas)
        {
            var nombre = NombreDePersona.Completo(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName);
            var banco = f.DisbursementBankId is { } bid ? bancos.GetValueOrDefault(bid)
                      : f.PayrollBankId is { } legado ? bancosLegado.GetValueOrDefault(legado) : null;
            var valores = new BankFileLineValues();
            valores[BankFieldSource.PayeeDocumentType] = TipoDeDocumento(f.IdType);
            valores[BankFieldSource.PayeeDocument] = f.TaxId;
            valores[BankFieldSource.PayeeFullName] = nombre;
            valores[BankFieldSource.PayeeFirstNames] = string.Join(" ", new[] { f.FirstName, f.OtherNames }.Where(s => !string.IsNullOrWhiteSpace(s)));
            valores[BankFieldSource.PayeeLastNames] = string.Join(" ", new[] { f.LastName, f.SecondLastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            valores[BankFieldSource.PayeeBankCode] = banco?.TransferCode;
            valores[BankFieldSource.PayeeAccountType] = f.PayrollBankAccountType == 0 ? null : f.PayrollBankAccountType.ToString();
            valores[BankFieldSource.PayeeAccountNumber] = string.IsNullOrWhiteSpace(f.PayrollBankAccountNumber) ? null : f.PayrollBankAccountNumber.Trim();
            valores[BankFieldSource.Amount] = f.NetPay;
            valores[BankFieldSource.Concept] = concepto;
            valores[BankFieldSource.PayeeEmail] = f.Email;

            DisbursementExcludedDto? excluido = null;
            if (pagados.Contains(f.Id))
                excluido = new(f.PublicId, nombre, "AlreadyPaid", "Ya tiene marca de pago vigente.", f.NetPay);
            else if (enArchivoPorEmpleado.TryGetValue(f.Id, out var otro))
                excluido = new(f.PublicId, nombre, "AlreadySent", $"Ya está en el archivo {otro.FileName} (no anulado).", f.NetPay);
            else if (f.NetPay <= 0m)
                excluido = new(f.PublicId, nombre, "ZeroNet", "El neto a pagar es cero o negativo.", f.NetPay);
            else if (string.IsNullOrWhiteSpace(f.PayrollBankAccountNumber))
                excluido = new(f.PublicId, nombre, "NoBankAccount", "La ficha no tiene número de cuenta bancaria.", f.NetPay);
            else if (banco is null)
                excluido = new(f.PublicId, nombre, "NoBankAccount", "La ficha no tiene banco de dispersión.", f.NetPay);
            else if (string.IsNullOrWhiteSpace(banco.TransferCode))
                excluido = new(f.PublicId, nombre, "BankCodeMissing", $"El banco {banco.Name} no tiene código de transferencia (ACH) en Maestros › Bancos.", f.NetPay);
            else
            {
                var faltan = FlatFileWriter.RequeridosVacios(formato, valores, contexto);
                if (faltan.Count > 0)
                    excluido = new(f.PublicId, nombre, "RequiredFieldEmpty", $"Sin dato para el campo requerido «{faltan[0]}» del formato.", f.NetPay);
            }

            candidatos.Add(new CandidatoDeDispersion(f.Id, f.EmployeeId, f.PublicId, nombre, f.TaxId, TipoDeDocumento(f.IdType),
                banco?.Id, banco?.Name, banco?.TransferCode, f.PayrollBankAccountType, f.PayrollBankAccountNumber, f.NetPay, f.Email, valores, excluido));
        }

        return Result.Success(new InsumosDeDispersion(run, etiqueta, concepto, contexto, candidatos));
    }

    /// <summary>El código estándar del tipo de documento (la persona guarda <c>C</c> para la cédula; el formato lo mapea al del banco).</summary>
    public static string TipoDeDocumento(string? idType) => idType?.Trim().ToUpperInvariant() switch
    {
        null or "" or "C" => "CC",
        var otro => otro,
    };

    public async Task<string> EtiquetaAsync(PayrollRun run, CancellationToken ct)
    {
        if (run.Kind != PayrollRunKind.Ordinary) return SettlementLabels.Etiqueta(run);
        if (run.PayPeriodId is not { } periodId) return "Nómina";
        var periodo = await db.PayPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId, ct);
        return periodo is null ? "Nómina" : $"Nómina {periodo.StartDate:yyyy-MM} {(periodo.Periodicity is { } per ? Domain.Payroll.Calculation.PeriodCalendar.Etiqueta((PayrollPeriodicity)per, periodo.SubPeriodNumber) : periodo.Description)}".Trim();
    }

    /// <summary>El concepto que va en cada línea, corto y en mayúsculas sin tildes (los bancos lo muestran en el extracto).</summary>
    public static string Concepto(PayrollRun run, string etiqueta) => FlatFileWriter.SinTildes(run.Kind switch
    {
        PayrollRunKind.ServiceBonus => $"PRIMA {run.Year}-{(run.Semester == 1 ? "I" : "II")}",
        PayrollRunKind.Severance => $"INTERESES CESANTIAS {run.Year}",
        PayrollRunKind.Vacation => "VACACIONES",
        PayrollRunKind.Settlement => "LIQUIDACION DEFINITIVA",
        _ => etiqueta.ToUpperInvariant(),
    }).ToUpperInvariant();
}
