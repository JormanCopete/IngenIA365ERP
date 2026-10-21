using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

// Feature 013, US3 (bloque 7B; T0454; FR-047, T78, C10; contracts/adaptadores-y-archivos.md §6.2; api.md §9.3; data-model §7.4, §7.6, §7.7): lo que
// comparten la vista previa y la generación del archivo plano de desembolsos, sus errores y sus DTO. (nuevo)

/// <summary>Los errores del archivo de desembolsos (data-model §7.4, §7.6 y §10.1). (nuevo)</summary>
public static class ErroresDeArchivoDeDesembolsos
{
    public const string CodigoInexistente = "Lending.DisbursementFile.NotFound";
    public const string CodigoOrdenEnArchivo = "Lending.DisbursementOrder.AlreadyInFile";
    public const string CodigoBancoDistinto = "Lending.DisbursementFile.BankMismatch";
    public const string CodigoConResultados = "Lending.DisbursementFile.HasResults";
    public const string CodigoVacio = "Lending.DisbursementFile.Empty";
    public const string CodigoCuentaInexistente = "Lending.DisbursementFile.SourceAccountNotFound";
    public const string CodigoFormatoNoVigente = "Lending.DisbursementFile.FormatNotCurrent";
    public const string CodigoYaAnulado = "Lending.DisbursementFile.AlreadyVoided";
    public const string CodigoNoCabe = "Lending.DisbursementFile.LineTooLong";

    public static Error Inexistente() => new(CodigoInexistente, "El archivo de desembolsos no existe.");

    public static Error OrdenEnArchivo(IReadOnlyList<string> ordenes) => new ErrorConDatos(CodigoOrdenEnArchivo,
        $"La orden {string.Join(", ", ordenes)} ya va en otro archivo de desembolsos no anulado. Anule ese archivo o quítela de la selección.",
        new { orders = ordenes });

    public static Error BancoDistinto(string cuenta, string bancoDeLaCuenta, string bancoDelFormato) => new ErrorConDatos(CodigoBancoDistinto,
        $"La cuenta {cuenta} es de {bancoDeLaCuenta} y el formato es de {bancoDelFormato}: elija una cuenta de ese banco o un formato genérico.",
        new { sourceAccount = cuenta, accountBank = bancoDeLaCuenta, formatBank = bancoDelFormato });

    public static Error ConResultados(IReadOnlyList<string> ordenes) => new ErrorConDatos(CodigoConResultados,
        $"Tesorería ya informó el resultado de la orden {string.Join(", ", ordenes)}: el archivo no se anula.", new { orders = ordenes });

    public static Error Vacio(IReadOnlyList<PendienteDeArchivoDto> pendientes) => new ErrorConDatos(CodigoVacio,
        "No hay órdenes que puedan ir al archivo: revise las pendientes y sus motivos.",
        new { pending = pendientes.Select(p => new { order = p.OrderNumber, reason = p.Reason, detail = p.Detail }).ToList() });

    public static Error CuentaInexistente() => new(CodigoCuentaInexistente,
        "La cuenta de origen no es una cuenta bancaria del plan de cuentas. Elíjala de la lista de cuentas de origen.");

    public static Error FormatoNoVigente(string codigo, DateOnly fecha) => new ErrorConDatos(CodigoFormatoNoVigente,
        $"El formato {codigo} no está activo y vigente para la fecha de pago {fecha:yyyy-MM-dd}.", new { code = codigo, paymentDate = fecha });

    public static Error YaAnulado(string numero) => new ErrorConDatos(CodigoYaAnulado, $"El archivo {numero} ya está anulado.", new { number = numero });

    public static Error NoCabe(int linea, string campo, string orden) => new ErrorConDatos(CodigoNoCabe,
        $"La orden {orden} no cabe en el formato: el campo «{campo}» es más largo de lo que admite.", new { line = linea, field = campo, order = orden });
}

/// <summary>Un archivo de desembolsos (api.md §9.3). (nuevo)</summary>
public sealed record DisbursementFileDto(
    Guid PublicId,
    string Number,
    Guid FormatPublicId,
    string FormatCode,
    string? BankName,
    string SourceAccountMasked,
    DateOnly PaymentDate,
    int Orders,
    decimal Total,
    string Sha256,
    string Status,
    bool HasResults,
    DateTime GeneratedAt,
    string FileName,
    string? VoidReason);

/// <summary>Lo que responde generar: el archivo y las órdenes que quedaron en pendientes con su motivo. (nuevo)</summary>
public sealed record DisbursementFileGeneratedDto(DisbursementFileDto File, IReadOnlyList<PendienteDeArchivoDto> Pending);

/// <summary>La vista previa (api.md §9.3, <c>POST /preview</c>): las líneas como quedarían, las pendientes y el total. (nuevo)</summary>
public sealed record DisbursementFilePreviewDto(string FileName, IReadOnlyList<string> Lines, IReadOnlyList<PendienteDeArchivoDto> Pending, decimal Total);

/// <summary>
/// Lo que la vista previa y la generación comparten (feature 013, US3, T0454; §6.2): el formato del ámbito <c>LoanDisbursement</c> vigente a la fecha
/// de pago (<c>Core.BankFileFormat.ScopeMismatch</c> si es de otro); la cuenta de origen, que llega por <see cref="IContabilidadParaCartera"/> (Cartera
/// no lee tablas <c>ACC_</c>) y es del banco del formato o el formato es genérico (<c>Lending.DisbursementFile.BankMismatch</c>); las órdenes pedidas
/// o, sin pedir ninguna, todas las elegibles de créditos del alcance de oficina y línea (<see cref="AlcanceDeCartera"/>); y sus líneas por
/// <see cref="LoanDisbursementLines"/>. No escribe. (nuevo)
/// </summary>
public sealed class PreparacionDeArchivoDeDesembolsos(
    IApplicationDbContext db,
    LoanDisbursementLines lineas,
    IContabilidadParaCartera contabilidad,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj)
{
    /// <summary>El lote de la vista previa, que todavía no tiene número de archivo.</summary>
    public const string LoteDeVistaPrevia = "VISTA-PREVIA";

    /// <summary>La fecha local de la cooperativa: la fecha de pago por defecto.</summary>
    public DateOnly Hoy => reloj.HoyLocal;

    public sealed record Preparado(BankFileFormat Formato, CuentaBancariaDeOrigenDto? Cuenta, int? BancoId, BankFileContext Contexto,
        IReadOnlyList<CandidatoDeArchivo> Candidatos)
    {
        public IReadOnlyList<CandidatoDeArchivo> Incluidos => Candidatos.Where(c => c.Pendiente is null).ToList();

        public IReadOnlyList<PendienteDeArchivoDto> Pendientes => Candidatos.Where(c => c.Pendiente is not null).Select(c => c.Pendiente!).ToList();
    }

    public async Task<Result<Preparado>> PrepararAsync(Guid formatPublicId, Guid? sourceAccountPublicId, IReadOnlyList<Guid>? ordenesPedidas,
        DateOnly fechaDePago, string lote, int secuencia, CancellationToken ct)
    {
        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Bank).Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.PublicId == formatPublicId && !f.IsDeleted, ct);
        if (formato is null) return Falla(BankFileFormatErrors.NotFound);
        if (formato.Scope != BankFileScope.LoanDisbursement)
            return Falla(BankFileFormatErrors.ScopeMismatch(formato.Code, formato.Scope, BankFileScope.LoanDisbursement));
        if (!formato.VigenteEn(fechaDePago)) return Falla(ErroresDeArchivoDeDesembolsos.FormatoNoVigente(formato.Code, fechaDePago));

        CuentaBancariaDeOrigenDto? cuenta = null;
        int? bancoId = formato.BankId;
        if (sourceAccountPublicId is { } pedida)
        {
            var cuentas = await contabilidad.CuentasBancariasAsync(null, ct);
            if (cuentas.IsFailure) return Falla(cuentas.Error);
            cuenta = cuentas.Value.FirstOrDefault(c => c.AccountPublicId == pedida);
            if (cuenta is null) return Falla(ErroresDeArchivoDeDesembolsos.CuentaInexistente());
            if (formato.Bank is { } delFormato && cuenta.BankPublicId != delFormato.PublicId)
                return Falla(ErroresDeArchivoDeDesembolsos.BancoDistinto(cuenta.AccountNumber ?? cuenta.AccountCode, cuenta.BankName, delFormato.Name));
            bancoId ??= await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.PublicId == cuenta.BankPublicId).Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);
        }

        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var contexto = new BankFileContext
        {
            CompanyNit = empresa?.TaxId ?? string.Empty,
            CompanyNitDv = empresa?.TaxIdCheckDigit,
            CompanyName = empresa?.Name ?? string.Empty,
            SourceAccountNumber = cuenta?.AccountNumber,
            SourceAccountType = cuenta is null ? null : "1",
            SourceBankCode = cuenta?.TransferCode ?? formato.Bank?.TransferCode,
            SourceAgreementCode = formato.AgreementCode,
            PaymentDate = fechaDePago,
            GeneratedAt = reloj.UtcNow,
            Sequence = secuencia,
            BatchReference = lote,
        };

        var ordenes = await OrdenesAsync(ordenesPedidas, ct);
        var candidatos = await lineas.CargarAsync(formato, contexto, ordenes, ct);
        return Result.Success(new Preparado(formato, cuenta, bancoId, contexto, candidatos));
    }

    /// <summary>El contexto con el lote definitivo (el número del archivo, que se asigna al final).</summary>
    public static BankFileContext ConLote(BankFileContext c, string lote) => new()
    {
        CompanyNit = c.CompanyNit, CompanyNitDv = c.CompanyNitDv, CompanyName = c.CompanyName, SourceAccountNumber = c.SourceAccountNumber,
        SourceAccountType = c.SourceAccountType, SourceBankCode = c.SourceBankCode, SourceAgreementCode = c.SourceAgreementCode, PaymentDate = c.PaymentDate,
        GeneratedAt = c.GeneratedAt, Sequence = c.Sequence, BatchReference = lote, Year = c.Year, FundNit = c.FundNit, FundPilaCode = c.FundPilaCode,
    };

    /// <summary>Las órdenes pedidas, o todas las elegibles; siempre de créditos del alcance (las demás no se ven).</summary>
    private async Task<IReadOnlyList<DisbursementOrder>> OrdenesAsync(IReadOnlyList<Guid>? pedidas, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.DisbursementOrders.AsNoTracking().Where(o => !o.IsDeleted && creditos.Contains(o.LoanId));
        consulta = pedidas is { Count: > 0 }
            ? consulta.Where(o => pedidas.Contains(o.PublicId))
            : consulta.Where(o => o.Status == DisbursementOrderStatus.Issued && o.PaidByFile && o.CurrentDisbursementFileId == null
                && o.VoidRequestedAt == null && o.Form == DisbursementForm.Transfer);
        return await consulta.OrderBy(o => o.Number).ToListAsync(ct);
    }

    private static Result<Preparado> Falla(Error e) => Result.Failure<Preparado>(e);
}

/// <summary>Los archivos de desembolsos dentro del alcance y sus DTO. (nuevo)</summary>
public static class ArchivosDeDesembolsos
{
    /// <summary>
    /// El archivo por su <c>PublicId</c> si alguna de sus órdenes es de un crédito del alcance de oficina y línea; si no, el mismo 404 que si no
    /// existiera (y el evento de fuera de alcance).
    /// </summary>
    public static async Task<Result<DisbursementFile>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion,
        LendingAuditEmitter auditoria, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.DisbursementFiles : db.DisbursementFiles.AsNoTracking();
        var archivo = await consulta.FirstOrDefaultAsync(f => f.PublicId == publicId && !f.IsDeleted, ct);
        if (archivo is null) return Result.Failure<DisbursementFile>(ErroresDeArchivoDeDesembolsos.Inexistente());
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var visible = await db.DisbursementFileLines.AsNoTracking().AnyAsync(l => l.FileId == archivo.Id
            && db.DisbursementOrders.Any(o => o.Id == l.OrderId && creditos.Contains(o.LoanId)), ct);
        if (visible) return Result.Success(archivo);
        await auditoria.EmitirFueraDeAlcanceAsync(nameof(DisbursementFile), archivo.PublicId, ct);
        return Result.Failure<DisbursementFile>(ErroresDeArchivoDeDesembolsos.Inexistente());
    }

    public static async Task<IReadOnlyList<DisbursementFileDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<DisbursementFile> filas, CancellationToken ct)
    {
        var ids = filas.Select(f => f.Id).ToList();
        var formatos = filas.Select(f => f.FormatId).Distinct().ToList();
        var deFormato = await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(f => formatos.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.PublicId, ct);
        var bancos = filas.Select(f => f.BankId).Distinct().ToList();
        var deBanco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => bancos.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var lineas = await db.DisbursementFileLines.AsNoTracking().Where(l => ids.Contains(l.FileId)).Select(l => new { l.FileId, l.OrderId }).ToListAsync(ct);
        var ordenes = lineas.Select(l => l.OrderId).Distinct().ToList();
        var conResultado = (await db.DisbursementOrderResults.AsNoTracking().Where(r => ordenes.Contains(r.OrderId)).Select(r => r.OrderId).Distinct()
            .ToListAsync(ct)).ToHashSet();
        var resultadosPorArchivo = lineas.GroupBy(l => l.FileId).ToDictionary(g => g.Key, g => g.Any(l => conResultado.Contains(l.OrderId)));
        return filas.Select(f => new DisbursementFileDto(f.PublicId, f.Number, deFormato.GetValueOrDefault(f.FormatId), f.FormatCode, deBanco.GetValueOrDefault(f.BankId),
            Enmascarar(f.SourceAccountNumber), f.PaymentDate, f.LineCount, f.TotalAmount, f.FileSha256, f.Anulado ? "Voided" : "Generated",
            resultadosPorArchivo.GetValueOrDefault(f.Id), f.GeneratedAt, f.FileName, f.VoidReason)).ToList();
    }

    /// <summary>La cuenta con sólo sus cuatro últimos dígitos.</summary>
    public static string Enmascarar(string? cuenta) =>
        string.IsNullOrWhiteSpace(cuenta) ? string.Empty : cuenta.Length <= 4 ? cuenta : new string('*', cuenta.Length - 4) + cuenta[^4..];
}
