using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>El titular de un documento con la copia de su identificación tal como se emitió (I-1). (nuevo)</summary>
public sealed record IssuedDocumentHolderDto(Guid PersonPublicId, string FullName, string DocumentType, string DocumentNumber);

/// <summary>Un crédito que cubre el documento y el saldo que mostró. (nuevo)</summary>
public sealed record IssuedDocumentLoanDto(Guid LoanPublicId, string Number, decimal? Balance);

/// <summary><c>IssuedDocumentDto</c> (api.md §15): el documento con su estado calculado. (nuevo)</summary>
public sealed record IssuedDocumentDto(
    Guid PublicId,
    string Kind,
    string Number,
    IssuedDocumentHolderDto Holder,
    IReadOnlyList<IssuedDocumentLoanDto> Loans,
    DateOnly? CutoffDate,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    short? Year,
    DateTime IssuedAt,
    string IssuedBy,
    string Status,
    DateOnly? FinalityUntil,
    bool? CoversCoDebts,
    string VerificationCode,
    string DeliveryMode,
    bool IsTestEnvironment);

/// <summary>Un evento del documento (emisión, entrega, anulación) con quién, el motivo y la operación causa. (nuevo)</summary>
public sealed record IssuedDocumentEventDto(string Kind, DateTime At, string? By, string? Reason, string? CauseOperation, Guid? DebtorMessagePublicId);

/// <summary>El detalle de un documento: el documento, lo que muestra la verificación, sus eventos y la nota de ventas fuera de Cartera. (nuevo)</summary>
public sealed record IssuedDocumentDetailDto(IssuedDocumentDto Document, JsonElement EssentialData, IReadOnlyList<IssuedDocumentEventDto> Events,
    string? OutsideLendingNote);

/// <summary>El titular como lo muestra la verificación: el nombre y el documento enmascarado (nota 4 de PG: la forma prudente de api.md §15). (nuevo)</summary>
public sealed record VerifiedDocumentHolderDto(string FullName, string DocumentNumberMasked);

/// <summary>La anulación como la muestra la verificación: cuándo y por qué. (nuevo)</summary>
public sealed record VoidedDocumentDto(DateTime At, string Reason);

/// <summary>
/// La respuesta de la verificación (api.md §15, adaptadores-y-archivos.md §8.9). <see cref="PdfMatches"/> (K5, T0972) sólo viene si se pidió comparar
/// la huella de un PDF: <c>true</c> si es la del PDF guardado. (nuevo)
/// </summary>
public sealed record VerifiedDocumentDto(bool Authentic, string Status, string Kind, string Number, DateTime IssuedAt, VerifiedDocumentHolderDto Holder,
    JsonElement EssentialData, VoidedDocumentDto? Voided, bool? PdfMatches = null);

/// <summary>
/// Lo que comparten las consultas y los comandos sobre un documento emitido (feature 013, US13, T0785): la lectura con el alcance —un documento se ve
/// si <b>ninguno</b> de los créditos que cubre está fuera del alcance de oficina y línea de quien pregunta; fuera, el mismo 404 que si no existiera y
/// <c>Lending.Access.OutOfScope</c>—, el estado siempre por <see cref="EstadoDeDocumentoEmitido"/> y los DTO. (nuevo)
/// </summary>
public static class DocumentosEmitidos
{
    /// <summary>Los documentos visibles para el alcance (antes de contar ni paginar).</summary>
    public static IQueryable<IssuedLendingDocument> Visibles(IApplicationDbContext db, AlcanceDeCartera alcance)
    {
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        return db.IssuedLendingDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && !db.IssuedDocumentLoans.Any(x => x.IssuedDocumentId == d.Id && !creditos.Contains(x.LoanId)));
    }

    /// <summary>Un documento por su <c>PublicId</c>, en el alcance; si no, <c>Lending.Document.NotFound</c>.</summary>
    public static async Task<Result<IssuedLendingDocument>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var documento = await db.IssuedLendingDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == publicId && !d.IsDeleted, ct);
        if (documento is null) return Result.Failure<IssuedLendingDocument>(DocumentErrors.Inexistente());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (await Visibles(db, alcance).AnyAsync(d => d.Id == documento.Id, ct)) return Result.Success(documento);
        await auditoria.EmitirFueraDeAlcanceAsync(nameof(IssuedLendingDocument), documento.PublicId, ct);
        return Result.Failure<IssuedLendingDocument>(DocumentErrors.Inexistente());
    }

    /// <summary>La anulación del documento, si la tiene (guardada o en esta unidad de trabajo).</summary>
    public static async Task<IssuedDocumentEvent?> AnulacionAsync(IApplicationDbContext db, int documentoId, CancellationToken ct) =>
        db.IssuedDocumentEvents.Local.FirstOrDefault(e => e.IssuedDocumentId == documentoId && e.Kind == IssuedDocumentEventKind.Voided)
        ?? await db.IssuedDocumentEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IssuedDocumentId == documentoId && e.Kind == IssuedDocumentEventKind.Voided, ct);

    /// <summary>El estado de <paramref name="documento"/> a <paramref name="hoy"/>.</summary>
    public static async Task<IssuedDocumentStatus> EstadoAsync(IApplicationDbContext db, IssuedLendingDocument documento, DateOnly hoy, CancellationToken ct) =>
        documento.EstadoA(await AnulacionAsync(db, documento.Id, ct) is not null, hoy);

    /// <summary>Los DTO de <paramref name="documentos"/> con su estado a <paramref name="hoy"/>, en el mismo orden.</summary>
    public static async Task<IReadOnlyList<IssuedDocumentDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<IssuedLendingDocument> documentos,
        DateOnly hoy, CancellationToken ct)
    {
        if (documentos.Count == 0) return [];
        var ids = documentos.Select(d => d.Id).ToList();
        var anulados = (await db.IssuedDocumentEvents.AsNoTracking()
            .Where(e => ids.Contains(e.IssuedDocumentId) && e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId).ToListAsync(ct))
            .ToHashSet();
        var lineas = await (from x in db.IssuedDocumentLoans.AsNoTracking()
                            join l in db.Loans.AsNoTracking() on x.LoanId equals l.Id
                            where ids.Contains(x.IssuedDocumentId)
                            orderby x.Id
                            select new { x.IssuedDocumentId, l.PublicId, l.Number, x.BalanceAtCutoff }).ToListAsync(ct);
        var personas = documentos.Select(d => d.PersonId).Distinct().ToList();
        var publicas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId }).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var usuarios = documentos.Select(d => d.IssuedByUserId).Distinct().ToList();
        var nombres = await NombresDeUsuariosAsync(db, usuarios, ct);

        return documentos.Select(d => new IssuedDocumentDto(
            d.PublicId, d.Kind.ToString(), d.Number,
            new IssuedDocumentHolderDto(publicas.GetValueOrDefault(d.PersonId), d.HolderName, d.HolderTaxIdType, d.HolderTaxId),
            lineas.Where(x => x.IssuedDocumentId == d.Id).Select(x => new IssuedDocumentLoanDto(x.PublicId, x.Number, x.BalanceAtCutoff)).ToList(),
            d.AsOfDate, d.PeriodFrom, d.PeriodTo, d.TaxYear, d.IssuedAt, nombres.GetValueOrDefault(d.IssuedByUserId) ?? string.Empty,
            d.EstadoA(anulados.Contains(d.Id), hoy).ToString(), d.FinalityUntil, d.CoversCoDebts, CodigoDeVerificacion.Formatear(d.VerificationCode),
            d.DeliveryMode.ToString(), d.IsTestEnvironment)).ToList();
    }

    /// <summary>Lo que muestra la verificación, como JSON.</summary>
    public static JsonElement DatosEsenciales(IssuedLendingDocument documento)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(documento.EssentialDataJson) ? "{}" : documento.EssentialDataJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }

    /// <summary>El documento de identidad enmascarado: sólo los cuatro últimos caracteres a la vista.</summary>
    public static string Enmascarar(string documento)
    {
        var limpio = (documento ?? string.Empty).Trim();
        const int visibles = 4;
        return limpio.Length <= visibles ? new string('*', limpio.Length) : new string('*', limpio.Length - visibles) + limpio[^visibles..];
    }

    public static async Task<Dictionary<int, string>> NombresDeUsuariosAsync(IApplicationDbContext db, IReadOnlyCollection<int> usuarios, CancellationToken ct) =>
        await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarios.Contains(u.Id))
            .Select(u => new { u.Id, u.Username }).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
}

/// <summary>
/// <c>GET /api/lending/documents?kind=&amp;personPublicId=&amp;loanPublicId=&amp;status=&amp;from=&amp;to=&amp;page=</c> (feature 013, US13, T0785; api.md §15,
/// <c>Lending.Documents.Read</c>): los documentos emitidos visibles para el alcance de oficina y línea (<see cref="AlcanceDeCartera"/>), por clase,
/// persona, crédito, estado (calculado) y fecha de emisión (hora de Colombia), los más recientes primero. (nuevo)
/// </summary>
public sealed record ListIssuedLendingDocumentsQuery(
    IssuedDocumentKind? Kind = null,
    Guid? PersonPublicId = null,
    Guid? LoanPublicId = null,
    IssuedDocumentStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<IssuedDocumentDto>>>;

public sealed class ListIssuedLendingDocumentsQueryValidator : AbstractValidator<ListIssuedLendingDocumentsQuery>
{
    public ListIssuedLendingDocumentsQueryValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("La fecha inicial no puede ser posterior a la final.");
    }
}

public sealed class ListIssuedLendingDocumentsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListIssuedLendingDocumentsQuery, Result<PagedResult<IssuedDocumentDto>>>
{
    public async Task<Result<PagedResult<IssuedDocumentDto>>> Handle(ListIssuedLendingDocumentsQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var hoy = reloj.HoyLocal;
        var desfase = reloj.AhoraLocal.Offset;
        var consulta = DocumentosEmitidos.Visibles(db, alcance);
        if (request.Kind is { } clase) consulta = consulta.Where(d => d.Kind == clase);
        if (request.PersonPublicId is { } persona)
        {
            var id = db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => p.Id);
            consulta = consulta.Where(d => id.Contains(d.PersonId));
        }
        if (request.LoanPublicId is { } credito)
        {
            var id = db.Loans.AsNoTracking().Where(l => l.PublicId == credito).Select(l => l.Id);
            consulta = consulta.Where(d => db.IssuedDocumentLoans.Any(x => x.IssuedDocumentId == d.Id && id.Contains(x.LoanId)));
        }
        if (request.From is { } desde)
        {
            var desdeUtc = DateTime.SpecifyKind(desde.ToDateTime(TimeOnly.MinValue) - desfase, DateTimeKind.Utc);
            consulta = consulta.Where(d => d.IssuedAt >= desdeUtc);
        }
        if (request.To is { } hasta)
        {
            var hastaUtc = DateTime.SpecifyKind(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) - desfase, DateTimeKind.Utc);
            consulta = consulta.Where(d => d.IssuedAt < hastaUtc);
        }
        var anulados = db.IssuedDocumentEvents.AsNoTracking().Where(e => e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId);
        consulta = request.Status switch
        {
            IssuedDocumentStatus.Voided => consulta.Where(d => anulados.Contains(d.Id)),
            IssuedDocumentStatus.SubjectToFinality => consulta.Where(d => !anulados.Contains(d.Id) && d.FinalityUntil != null && d.FinalityUntil > hoy),
            IssuedDocumentStatus.Valid => consulta.Where(d => !anulados.Contains(d.Id) && (d.FinalityUntil == null || d.FinalityUntil <= hoy)),
            _ => consulta,
        };

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(d => d.IssuedAt).ThenByDescending(d => d.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<IssuedDocumentDto>(await DocumentosEmitidos.DtosAsync(db, filas, hoy, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>
/// <c>GET /api/lending/documents/{id}</c> (feature 013, US13, T0785; api.md §15, <c>Lending.Documents.Read</c>): el documento con lo que muestra la
/// verificación y sus eventos —emisión, entrega y anulación, con quién, el motivo y la operación causa—. Fuera del alcance o inexistente,
/// <c>Lending.Document.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetIssuedLendingDocumentQuery(Guid DocumentPublicId) : IRequest<Result<IssuedDocumentDetailDto>>;

public sealed class GetIssuedLendingDocumentQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetIssuedLendingDocumentQuery, Result<IssuedDocumentDetailDto>>
{
    public async Task<Result<IssuedDocumentDetailDto>> Handle(GetIssuedLendingDocumentQuery request, CancellationToken ct)
    {
        var leido = await DocumentosEmitidos.LeerAsync(db, alcance, auditoria, request.DocumentPublicId, ct);
        if (leido.IsFailure) return Result.Failure<IssuedDocumentDetailDto>(leido.Error);
        var documento = leido.Value;
        var dto = (await DocumentosEmitidos.DtosAsync(db, [documento], reloj.HoyLocal, ct))[0];
        var eventos = await db.IssuedDocumentEvents.AsNoTracking().Where(e => e.IssuedDocumentId == documento.Id).OrderBy(e => e.Id)
            .ToListAsync(ct);
        var usuarios = eventos.Where(e => e.ActorUserId is not null).Select(e => e.ActorUserId!.Value).Distinct().ToList();
        var nombres = await DocumentosEmitidos.NombresDeUsuariosAsync(db, usuarios, ct);
        var operaciones = eventos.Where(e => e.CausingOperationId is not null).Select(e => e.CausingOperationId!.Value).Distinct().ToList();
        var numeros = await db.LoanOperations.AsNoTracking().Where(o => operaciones.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        var mensajes = eventos.Where(e => e.DebtorMessageId is not null).Select(e => e.DebtorMessageId!.Value).Distinct().ToList();
        var publicos = await db.DebtorMessages.AsNoTracking().Where(m => mensajes.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.PublicId, ct);
        return Result.Success(new IssuedDocumentDetailDto(dto, DocumentosEmitidos.DatosEsenciales(documento),
            eventos.Select(e => new IssuedDocumentEventDto(e.Kind.ToString(), e.OccurredAt,
                e.ActorUserId is { } u ? nombres.GetValueOrDefault(u) : null, e.Reason,
                e.CausingOperationId is { } o ? numeros.GetValueOrDefault(o) : null,
                e.DebtorMessageId is { } m && publicos.TryGetValue(m, out var publico) ? publico : null)).ToList(),
            documento.OutsideLendingNote));
    }
}

/// <summary>
/// <c>GET /api/lending/documents/verify/{code}</c> (feature 013, US13, T0785; api.md §15, adaptadores-y-archivos.md §8.9, <c>Lending.Documents.Verify</c>;
/// FR-132, D21): normaliza el código (<see cref="CodigoDeVerificacion.Normalizar"/>) y responde si es auténtico, su estado, clase, número, fecha, el
/// titular enmascarado, lo que muestra la verificación y, si está anulado, cuándo y por qué. Sólo la usan personas de la cooperativa —no hay ruta
/// anónima— y, como cada cooperativa tiene su base, un código de otra o inexistente responde <c>Lending.Document.NotFound</c> («el código no
/// corresponde a un documento de esta cooperativa»). La verificación no mira el alcance de oficina: quien verifica sólo recibe los datos esenciales
/// del documento, no el expediente. Se audita <c>Lending.Document.Verified</c>.
/// <para>K5 (T0972, R149): con <see cref="Sha256"/> —la huella de un PDF que el navegador calcula con <c>crypto.subtle</c>; el archivo nunca sube—
/// responde además <c>pdfMatches</c>, si coincide con el <c>ContentSha256</c> guardado; la comparación queda en la misma lectura auditada. Una huella
/// que no son 64 caracteres hexadecimales es <c>Validation.Invalid</c>.</para> (nuevo)
/// </summary>
public sealed record VerifyLendingDocumentQuery(string Code, string? Sha256 = null) : IRequest<Result<VerifiedDocumentDto>>;

public sealed class VerifyLendingDocumentQueryHandler(IApplicationDbContext db, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<VerifyLendingDocumentQuery, Result<VerifiedDocumentDto>>
{
    /// <summary>¿Es una huella SHA-256 en hexadecimal (64 caracteres, mayúsculas o minúsculas)?</summary>
    public static bool EsHuella(string? texto) => texto is { Length: 64 } && texto.All(Uri.IsHexDigit);

    public async Task<Result<VerifiedDocumentDto>> Handle(VerifyLendingDocumentQuery request, CancellationToken ct)
    {
        var huella = string.IsNullOrWhiteSpace(request.Sha256) ? null : request.Sha256.Trim();
        if (huella is not null && !EsHuella(huella))
            return Result.Failure<VerifiedDocumentDto>("Validation.Invalid", "La huella del PDF no es válida: deben ser 64 caracteres hexadecimales (SHA-256).");
        var codigo = CodigoDeVerificacion.Normalizar(request.Code);
        if (codigo is null) return Result.Failure<VerifiedDocumentDto>(DocumentErrors.CodigoDesconocido());
        var documento = await db.IssuedLendingDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.VerificationCode == codigo && !d.IsDeleted, ct);
        if (documento is null) return Result.Failure<VerifiedDocumentDto>(DocumentErrors.CodigoDesconocido());

        var anulacion = await DocumentosEmitidos.AnulacionAsync(db, documento.Id, ct);
        var estado = documento.EstadoA(anulacion is not null, reloj.HoyLocal);
        bool? coincide = huella is null ? null : string.Equals(huella, documento.ContentSha256, StringComparison.OrdinalIgnoreCase);
        await auditoria.EmitirDocumentoVerificadoAsync(documento.PublicId, anulacion is null, ct, coincide);
        return Result.Success(new VerifiedDocumentDto(true, estado.ToString(), documento.Kind.ToString(), documento.Number, documento.IssuedAt,
            new VerifiedDocumentHolderDto(documento.HolderName, DocumentosEmitidos.Enmascarar(documento.HolderTaxId)),
            DocumentosEmitidos.DatosEsenciales(documento),
            anulacion is null ? null : new VoidedDocumentDto(anulacion.OccurredAt, anulacion.Reason ?? string.Empty), coincide));
    }
}
