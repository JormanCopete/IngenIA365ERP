using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Los motivos por los que una orden no entra al archivo (contracts/adaptadores-y-archivos.md §6.2; como la dispersión de la 010). (nuevo)</summary>
public static class MotivosDePendienteDeArchivo
{
    /// <summary>El banco destino no tiene código de transferencia (ACH) en Maestros › Bancos.</summary>
    public const string SinCodigoDeBanco = "BankCodeMissing";

    /// <summary>La orden no trae banco o número de cuenta destino.</summary>
    public const string SinCuenta = "NoBankAccount";

    /// <summary>La orden ya va en otro archivo no anulado.</summary>
    public const string EnArchivo = "AlreadyInFile";

    /// <summary>La orden no está emitida para pagarse por archivo (otro estado, cheque o giro por Tesorería).</summary>
    public const string NoElegible = "NotEligible";

    /// <summary>Un campo obligatorio del formato quedó vacío (el detalle dice cuál).</summary>
    public const string CampoVacio = "RequiredFieldEmpty";
}

/// <summary>Una orden que no entra al archivo, con su motivo (api.md §9.3, <c>pending[]</c>). (nuevo)</summary>
public sealed record PendienteDeArchivoDto(Guid OrderPublicId, string OrderNumber, string Reason, string Detail, decimal NetAmount);

/// <summary>Una orden lista para el archivo, o pendiente con su motivo; sus valores por origen ya resueltos. (nuevo)</summary>
public sealed record CandidatoDeArchivo(DisbursementOrder Orden, string NumeroDelCredito, BankFileLineValues Valores, PendienteDeArchivoDto? Pendiente);

/// <summary>
/// Arma las líneas del archivo plano de desembolsos (feature 013, US3, T0453; contracts/adaptadores-y-archivos.md §6.2; molde
/// <c>Payroll/Dispersion/PayrollDisbursementLines</c>) sobre el motor común <see cref="FlatFileWriter"/>: no conoce la nómina y llena sólo los
/// orígenes genéricos del ámbito <c>LoanDisbursement</c>, desde la <b>copia sellada</b> de cada orden (I-1), nunca desde el maestro de hoy:
/// <list type="bullet">
/// <item><c>PayeeDocumentType</c>, <c>PayeeDocument</c>, <c>PayeeFullName</c>: el beneficiario tal como se emitió la orden; nombres y apellidos por
/// separado y el correo, que la orden no copia, salen de la persona;</item>
/// <item><c>PayeeBankCode</c>: el código de transferencia (<c>COR_Banks.TransferCode</c>, ACH) del banco destino de la orden;</item>
/// <item><c>PayeeAccountType</c>, <c>PayeeAccountNumber</c>: la cuenta destino de la orden (1 ahorros, 2 corriente; el formato lo mapea);</item>
/// <item><c>Amount</c>: el neto a girar (después de las recogidas); <c>Concept</c> y <c>LoanNumber</c>: el número del crédito;</item>
/// <item><c>BatchReference</c> (en el contexto): el número del archivo.</item>
/// </list>
/// Lo que no cabe queda en pendientes con su motivo (<see cref="MotivosDePendienteDeArchivo"/>). El módulo decide qué va en cada origen; el motor
/// sólo escribe. (nuevo)
/// </summary>
public sealed class LoanDisbursementLines(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<CandidatoDeArchivo>> CargarAsync(BankFileFormat formato, BankFileContext contexto, IReadOnlyList<DisbursementOrder> ordenes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(formato);
        ArgumentNullException.ThrowIfNull(contexto);
        var creditos = ordenes.Select(o => o.LoanId).Distinct().ToList();
        var numeros = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Number, ct);
        var idsBanco = ordenes.Where(o => o.DestinationBankId is not null).Select(o => o.DestinationBankId!.Value).Distinct().ToList();
        var bancos = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => idsBanco.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        var idsPersona = ordenes.Select(o => o.BeneficiaryPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsPersona.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var candidatos = new List<CandidatoDeArchivo>(ordenes.Count);
        foreach (var o in ordenes)
        {
            var numero = numeros.GetValueOrDefault(o.LoanId) ?? string.Empty;
            var banco = o.DestinationBankId is { } bid ? bancos.GetValueOrDefault(bid) : null;
            var persona = personas.GetValueOrDefault(o.BeneficiaryPersonId);
            var valores = new BankFileLineValues();
            valores[BankFieldSource.PayeeDocumentType] = string.IsNullOrWhiteSpace(o.BeneficiaryTaxIdType) ? null : TipoDeDocumento(o.BeneficiaryTaxIdType);
            valores[BankFieldSource.PayeeDocument] = o.BeneficiaryTaxId;
            valores[BankFieldSource.PayeeFullName] = o.BeneficiaryName;
            valores[BankFieldSource.PayeeFirstNames] = persona is null ? null : Unir(persona.FirstName, persona.OtherNames);
            valores[BankFieldSource.PayeeLastNames] = persona is null ? null : Unir(persona.LastName, persona.SecondLastName);
            valores[BankFieldSource.PayeeEmail] = persona?.Email;
            valores[BankFieldSource.PayeeBankCode] = banco?.TransferCode;
            valores[BankFieldSource.PayeeAccountType] = o.DestinationAccountType?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            valores[BankFieldSource.PayeeAccountNumber] = string.IsNullOrWhiteSpace(o.DestinationAccountNumber) ? null : o.DestinationAccountNumber.Trim();
            valores[BankFieldSource.Amount] = o.NetAmount;
            valores[BankFieldSource.Concept] = numero;
            valores[BankFieldSource.LoanNumber] = numero;

            candidatos.Add(new CandidatoDeArchivo(o, numero, valores, Pendiente(o, banco, formato, valores, contexto)));
        }
        return candidatos;
    }

    private static PendienteDeArchivoDto? Pendiente(DisbursementOrder o, Bank? banco, BankFileFormat formato, BankFileLineValues valores, BankFileContext contexto)
    {
        PendienteDeArchivoDto Con(string motivo, string detalle) => new(o.PublicId, o.Number, motivo, detalle, o.NetAmount);

        if (o.Status != DisbursementOrderStatus.Issued || !o.PaidByFile || o.Form != DisbursementForm.Transfer || o.AnulacionPedida)
            return Con(MotivosDePendienteDeArchivo.NoElegible, o.AnulacionPedida
                ? "La orden tiene la anulación pedida a Tesorería."
                : $"La orden está en «{o.Status}» o no se emitió para pagarse por archivo.");
        if (o.CurrentDisbursementFileId is not null)
            return Con(MotivosDePendienteDeArchivo.EnArchivo, "La orden ya va en otro archivo de desembolsos no anulado.");
        if (string.IsNullOrWhiteSpace(o.DestinationAccountNumber) || banco is null)
            return Con(MotivosDePendienteDeArchivo.SinCuenta, "La orden no tiene banco o número de cuenta destino.");
        if (string.IsNullOrWhiteSpace(banco.TransferCode))
            return Con(MotivosDePendienteDeArchivo.SinCodigoDeBanco, $"El banco {banco.Name} no tiene código de transferencia (ACH) en Maestros › Bancos.");
        var faltan = FlatFileWriter.RequeridosVacios(formato, valores, contexto);
        return faltan.Count > 0 ? Con(MotivosDePendienteDeArchivo.CampoVacio, $"Sin dato para el campo requerido «{faltan[0]}» del formato.") : null;
    }

    /// <summary>El código estándar del tipo de documento (la persona guarda <c>C</c> para la cédula; el formato lo mapea al del banco).</summary>
    public static string TipoDeDocumento(string? idType) => idType?.Trim().ToUpperInvariant() switch
    {
        null or "" or "C" => "CC",
        var otro => otro,
    };

    private static string? Unir(params string?[] partes)
    {
        var texto = string.Join(" ", partes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        return texto.Length == 0 ? null : texto;
    }
}
