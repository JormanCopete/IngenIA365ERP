using FluentValidation;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.BankFiles;

public sealed record BankFileFormatSummaryDto(
    Guid FormatPublicId, string Code, string Name, Guid? BankPublicId, string? BankName, string Scope,
    DateOnly ValidFrom, DateOnly? ValidTo, string Kind, bool IsActive, bool IsSeeded, bool VigenteHoy,
    int FieldCount, int FilesGenerated, string? Notes);

public sealed record BankFileFormatDetailDto(BankFileFormatSummaryDto Summary, BankFileFormatDefinition Definition);

/// <summary>Un origen que el editor de formatos ofrece, con en qué registros se admite.</summary>
public sealed record BankFieldSourceDto(string Code, string Description, string DataType, bool Header, bool Detail, bool Trailer, string? Scope);

// ----------------------------------------------------------------- listado --

/// <summary>Los formatos de un ámbito (y de un banco si viene); los genéricos (sin banco) salen siempre.</summary>
public sealed record ListBankFileFormatsQuery(string? Scope = null, Guid? BankPublicId = null, bool OnlyActive = false)
    : IRequest<Result<IReadOnlyList<BankFileFormatSummaryDto>>>;

public sealed class ListBankFileFormatsQueryValidator : AbstractValidator<ListBankFileFormatsQuery>
{
    public ListBankFileFormatsQueryValidator() =>
        RuleFor(x => x.Scope).Must(s => s is null || Enum.TryParse<BankFileScope>(s, true, out _)).WithMessage("Ámbito desconocido.");
}

public sealed class ListBankFileFormatsQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<ListBankFileFormatsQuery, Result<IReadOnlyList<BankFileFormatSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<BankFileFormatSummaryDto>>> Handle(ListBankFileFormatsQuery request, CancellationToken ct)
    {
        var q = db.BankFileFormats.AsNoTracking().Include(f => f.Bank).Include(f => f.Fields).AsQueryable();
        if (request.Scope is { } s) { var scope = Enum.Parse<BankFileScope>(s, true); q = q.Where(f => f.Scope == scope); }
        if (request.BankPublicId is { } bankPublicId) q = q.Where(f => f.BankId == null || f.Bank!.PublicId == bankPublicId);
        if (request.OnlyActive) q = q.Where(f => f.IsActive);
        var formatos = await q.OrderBy(f => f.Bank != null ? f.Bank.Name : string.Empty).ThenBy(f => f.Code).ThenByDescending(f => f.ValidFrom).ToListAsync(ct);
        var ids = formatos.Select(f => f.Id).ToList();
        var archivos = await db.BankDisbursementFiles.AsNoTracking().Where(a => ids.Contains(a.FormatId))
            .GroupBy(a => a.FormatId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var hoy = clock.TodayUtc;
        return Result.Success<IReadOnlyList<BankFileFormatSummaryDto>>(formatos.Select(f => Map(f, archivos.GetValueOrDefault(f.Id), hoy)).ToList());
    }

    public static BankFileFormatSummaryDto Map(BankFileFormat f, int archivos, DateOnly hoy) => new(
        f.PublicId, f.Code, f.Name, f.Bank?.PublicId, f.Bank?.Name, f.Scope.ToString(), f.ValidFrom, f.ValidTo, f.Kind.ToString(),
        f.IsActive, f.IsSeeded, f.VigenteEn(hoy), f.Fields.Count(x => !x.IsDeleted), archivos, f.Notes);
}

// ----------------------------------------------------------------- detalle --

public sealed record GetBankFileFormatQuery(Guid FormatPublicId) : IRequest<Result<BankFileFormatDetailDto>>;

public sealed class GetBankFileFormatQueryValidator : AbstractValidator<GetBankFileFormatQuery>
{
    public GetBankFileFormatQueryValidator() => RuleFor(x => x.FormatPublicId).NotEmpty();
}

public sealed class GetBankFileFormatQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<GetBankFileFormatQuery, Result<BankFileFormatDetailDto>>
{
    public async Task<Result<BankFileFormatDetailDto>> Handle(GetBankFileFormatQuery request, CancellationToken ct)
    {
        var f = await db.BankFileFormats.AsNoTracking().Include(x => x.Bank).Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.PublicId == request.FormatPublicId, ct);
        if (f is null) return Result.Failure<BankFileFormatDetailDto>(BankFileFormatErrors.NotFound);
        var archivos = await db.BankDisbursementFiles.AsNoTracking().CountAsync(a => a.FormatId == f.Id, ct);
        return Result.Success(new BankFileFormatDetailDto(ListBankFileFormatsQueryHandler.Map(f, archivos, clock.TodayUtc), BankFileFormatDefinition.Desde(f)));
    }
}

// ----------------------------------------------------------------- orígenes --

/// <summary>El catálogo de orígenes para el editor: qué es cada uno y dónde se admite.</summary>
public sealed record ListBankFieldSourcesQuery : IRequest<Result<IReadOnlyList<BankFieldSourceDto>>>;

public sealed class ListBankFieldSourcesQueryValidator : AbstractValidator<ListBankFieldSourcesQuery>;

public sealed class ListBankFieldSourcesQueryHandler : IRequestHandler<ListBankFieldSourcesQuery, Result<IReadOnlyList<BankFieldSourceDto>>>
{
    private static readonly (BankFieldSource Source, string Descripcion, bool H, bool D, bool T, string? Ambito)[] Catalogo =
    [
        (BankFieldSource.Constant, "Valor literal (con «value»)", true, true, true, null),
        (BankFieldSource.Blank, "Espacios o ceros según el relleno", true, true, true, null),
        (BankFieldSource.CompanyNit, "NIT de la empresa sin dígito", true, true, true, null),
        (BankFieldSource.CompanyNitDv, "Dígito de verificación del NIT", true, true, true, null),
        (BankFieldSource.CompanyName, "Razón social", true, true, true, null),
        (BankFieldSource.SourceAccountNumber, "Cuenta bancaria origen elegida al generar", true, true, true, null),
        (BankFieldSource.SourceAccountType, "Tipo de la cuenta origen (1 ahorros / 2 corriente; use un mapa)", true, true, true, null),
        (BankFieldSource.SourceBankCode, "Código de transferencia (ACH) del banco pagador", true, true, true, null),
        (BankFieldSource.SourceAgreementCode, "Convenio o código de empresa en el banco (del formato)", true, true, true, null),
        (BankFieldSource.PaymentDate, "Fecha de pago del lote", true, true, true, null),
        (BankFieldSource.GenerationDate, "Fecha en que se generó el archivo", true, true, true, null),
        (BankFieldSource.GenerationTime, "Hora en que se generó (HHmmss)", true, true, true, null),
        (BankFieldSource.Sequence, "Consecutivo diario de archivos de la cooperativa", true, false, true, null),
        (BankFieldSource.BatchReference, "Referencia del lote", true, true, true, null),
        (BankFieldSource.LineNumber, "Número de línea 1..N", false, true, false, null),
        (BankFieldSource.PayeeDocumentType, "Tipo de documento del beneficiario (CC, CE, PA…; use un mapa)", false, true, false, null),
        (BankFieldSource.PayeeDocument, "Número de documento del beneficiario", false, true, false, null),
        (BankFieldSource.PayeeFullName, "Nombre completo del beneficiario", false, true, false, null),
        (BankFieldSource.PayeeFirstNames, "Nombres del beneficiario", false, true, false, null),
        (BankFieldSource.PayeeLastNames, "Apellidos del beneficiario", false, true, false, null),
        (BankFieldSource.PayeeBankCode, "Código de transferencia (ACH) del banco destino", false, true, false, null),
        (BankFieldSource.PayeeAccountType, "Tipo de cuenta destino (1 ahorros / 2 corriente; use un mapa)", false, true, false, null),
        (BankFieldSource.PayeeAccountNumber, "Número de cuenta destino", false, true, false, null),
        (BankFieldSource.Amount, "Valor a pagar (neto del empleado, cesantías del fondo…)", false, true, false, null),
        (BankFieldSource.Concept, "Concepto del pago («NOMINA 2026-12 Q1», «PRIMA 2026-II»)", false, true, false, null),
        (BankFieldSource.PayeeEmail, "Correo del beneficiario", false, true, false, null),
        (BankFieldSource.LineCount, "Cantidad de líneas de detalle", true, false, true, null),
        (BankFieldSource.TotalAmount, "Suma de los valores del detalle", true, false, true, null),
        (BankFieldSource.FundNit, "NIT del fondo de cesantías", true, true, true, nameof(BankFileScope.SeveranceDeposit)),
        (BankFieldSource.FundPilaCode, "Código PILA del fondo", true, true, true, nameof(BankFileScope.SeveranceDeposit)),
        (BankFieldSource.SeveranceDays, "Días liquidados de cesantías", false, true, false, nameof(BankFileScope.SeveranceDeposit)),
        (BankFieldSource.SeveranceBaseSalary, "Salario base de liquidación", false, true, false, nameof(BankFileScope.SeveranceDeposit)),
        (BankFieldSource.PayeeHireDate, "Fecha de ingreso del empleado", false, true, false, nameof(BankFileScope.SeveranceDeposit)),
        (BankFieldSource.Year, "Año de la consignación (o del pago)", true, true, true, null),
    ];

    public Task<Result<IReadOnlyList<BankFieldSourceDto>>> Handle(ListBankFieldSourcesQuery request, CancellationToken ct) =>
        Task.FromResult(Result.Success<IReadOnlyList<BankFieldSourceDto>>(Catalogo.Select(c =>
            new BankFieldSourceDto(c.Source.ToString(), c.Descripcion, BankFileFormatDefinition.TipoPorDefecto(c.Source).ToString(), c.H, c.D, c.T, c.Ambito)).ToList()));
}
