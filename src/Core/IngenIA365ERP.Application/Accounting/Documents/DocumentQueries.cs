using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents;

// Consultas de comprobantes (feature 009, US3; contracts/api.md §6).

// ------------------------------------------------------------------------------------ validar --

/// <summary>Las mismas reglas que al contabilizar, sin guardar: la pantalla lo llama al salir de cada campo.</summary>
public sealed record ValidateDraftQuery(string VoucherTypeCode, DateOnly Date, string Description, IReadOnlyList<LineaDeBorradorInput> Lines) : IRequest<Result<ValidacionDto>>;

public sealed class ValidateDraftQueryValidator : AbstractValidator<ValidateDraftQuery>
{
    public ValidateDraftQueryValidator()
    {
        RuleFor(x => x.VoucherTypeCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Description).MaximumLength(AccountingPoster.LargoDeDescripcion);
        RuleForEach(x => x.Lines).SetValidator(new LineaDeBorradorInputValidator());
    }
}

public sealed class ValidateDraftQueryHandler(IApplicationDbContext db, AccountingPoster poster) : IRequestHandler<ValidateDraftQuery, Result<ValidacionDto>>
{
    public async Task<Result<ValidacionDto>> Handle(ValidateDraftQuery request, CancellationToken ct)
    {
        var refs = await LineasDeBorrador.ResolverAsync(db, request.Lines, ct);
        var validacion = await poster.ValidarAsync(new PostingRequest(request.VoucherTypeCode.Trim().ToUpperInvariant(), request.Date, request.Description,
            AccountingOrigin.Manual(Guid.Empty), LineasDeBorrador.AlContrato(request.Lines, refs)), ct);
        return Result.Success(new ValidacionDto(validacion.Errores.Concat(validacion.Avisos).Select(ErrorDeLineaDto.De).ToList(),
            validacion.TotalDebit, validacion.TotalCredit, validacion.Diferencia));
    }
}

// ------------------------------------------------------------------------------------- detalle --

public sealed record GetDocumentQuery(Guid PublicId) : IRequest<Result<ComprobanteDto>>;

public sealed class GetDocumentQueryValidator : AbstractValidator<GetDocumentQuery>
{
    public GetDocumentQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetDocumentQueryHandler(IApplicationDbContext db, AccountingPoster poster) : IRequestHandler<GetDocumentQuery, Result<ComprobanteDto>>
{
    public async Task<Result<ComprobanteDto>> Handle(GetDocumentQuery request, CancellationToken ct)
    {
        var documento = await db.AccountingDocuments.AsNoTracking()
            .Include(d => d.VoucherType)
            .Include(d => d.ReversesDocument).ThenInclude(r => r!.VoucherType)
            .Include(d => d.ReversedByDocument).ThenInclude(r => r!.VoucherType)
            .Include(d => d.Lines).ThenInclude(l => l.Account)
            .Include(d => d.Lines).ThenInclude(l => l.Branch)
            .Include(d => d.Lines).ThenInclude(l => l.CostCenter)
            .Include(d => d.Lines).ThenInclude(l => l.Person)
            .Include(d => d.Lines).ThenInclude(l => l.CrossDocumentType)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (documento is null) return Result.Failure<ComprobanteDto>(AccountingErrors.DocumentNotFound);

        var adjuntos = await db.Attachments.AsNoTracking()
            .Where(a => !a.IsDeleted && a.OwnerEntityType == nameof(AccountingDocument) && a.OwnerEntityPublicId == documento.PublicId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AdjuntoDto(a.PublicId, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAt, a.CreatedBy))
            .ToListAsync(ct);

        // Un borrador se abre con sus infracciones vigentes: lo que cambió en cuentas o períodos desde que se guardó también cuenta.
        var errores = new List<ErrorDeLineaDto>();
        if (documento.Status == DocumentStatus.Draft)
        {
            var validacion = await poster.ValidarAsync(LineasDeBorrador.DesdeDocumento(documento), ct);
            errores.AddRange(validacion.Errores.Concat(validacion.Avisos).Select(ErrorDeLineaDto.De));
        }

        return Result.Success(Mapear(documento, adjuntos, errores));
    }

    internal static ComprobanteDto Mapear(AccountingDocument d, IReadOnlyList<AdjuntoDto> adjuntos, IReadOnlyList<ErrorDeLineaDto> errores)
    {
        var lineas = d.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).Select(l => new LineaDeComprobanteDto(
            l.LineNumber,
            l.Account?.PublicId ?? Guid.Empty, l.Account?.Code ?? string.Empty, l.Account?.Name ?? string.Empty,
            l.Branch?.PublicId ?? Guid.Empty, l.Branch?.Name ?? string.Empty,
            l.CostCenter?.PublicId, l.CostCenter?.Name,
            l.Person?.PublicId, l.Person is null ? null : PersonFactory.NombreVisible(l.Person.FirstName, l.Person.OtherNames, l.Person.LastName, l.Person.SecondLastName, l.Person.BusinessName), l.Person?.TaxId,
            l.CrossDocumentType?.Code, l.CrossDocumentNumber,
            l.Debit, l.Credit, l.Description, l.TaxBase)).ToList();

        return new ComprobanteDto(
            d.PublicId, d.VoucherType?.Code ?? string.Empty, d.VoucherType?.Name ?? string.Empty, d.Number, d.Date, d.Description,
            d.Status.ToString(), d.Kind.ToString(), d.TotalDebit, d.TotalCredit, d.RegisteredBy, d.CreatedAt, d.PostedBy, d.PostedAt,
            new OrigenDeComprobanteDto(d.OriginModule, ModuloContable.Nombre(d.OriginModule), d.SourceType, d.SourcePublicId, d.EsDeModulo ? EnlacesDeOrigen.Ruta(d.SourceType, d.SourcePublicId) : null),
            new ReversionDto(d.ReversesDocument?.PublicId, d.ReversesDocument?.Referencia(), d.ReversedByDocument?.PublicId, d.ReversedByDocument?.Referencia(), d.ReversalReason),
            lineas, adjuntos, errores);
    }
}

// --------------------------------------------------------------------------------------- lista --

/// <summary>Lista paginada con filtros; con alcance de sucursal sólo se ven comprobantes con alguna línea en las sucursales permitidas (FR-035).</summary>
public sealed record ListDocumentsQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    string? VoucherType = null,
    string? Status = null,
    string? Origin = null,
    long? Number = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedList<ComprobanteResumenDto>>>;

public sealed class ListDocumentsQueryValidator : AbstractValidator<ListDocumentsQuery>
{
    public ListDocumentsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 500);
        RuleFor(x => x.Status).Must(s => s is null || Enum.TryParse<DocumentStatus>(s, true, out _)).WithMessage("Estado desconocido (Draft, Posted, Reversed).");
        RuleFor(x => x.Origin).Must(o => string.IsNullOrWhiteSpace(o) || ModuloContable.EsValido(o.ToUpperInvariant())).WithMessage("Módulo desconocido.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("«Desde» no puede ser posterior a «hasta».");
    }
}

public sealed class ListDocumentsQueryHandler(IApplicationDbContext db, IUserBranchScope scope) : IRequestHandler<ListDocumentsQuery, Result<PagedList<ComprobanteResumenDto>>>
{
    public async Task<Result<PagedList<ComprobanteResumenDto>>> Handle(ListDocumentsQuery request, CancellationToken ct)
    {
        var q = db.AccountingDocuments.AsNoTracking().Where(d => !d.IsDeleted);
        if (request.From is { } desde) q = q.Where(d => d.Date >= desde);
        if (request.To is { } hasta) q = q.Where(d => d.Date <= hasta);
        if (!string.IsNullOrWhiteSpace(request.VoucherType)) { var tipo = request.VoucherType.Trim().ToUpperInvariant(); q = q.Where(d => d.VoucherType!.Code == tipo); }
        if (!string.IsNullOrWhiteSpace(request.Status)) { var estado = Enum.Parse<DocumentStatus>(request.Status, true); q = q.Where(d => d.Status == estado); }
        if (!string.IsNullOrWhiteSpace(request.Origin)) { var origen = request.Origin.Trim().ToUpperInvariant(); q = q.Where(d => d.OriginModule == origen); }
        if (request.Number is { } numero) q = q.Where(d => d.Number == numero);

        var alcance = await scope.ObtenerAsync(ct);
        if (alcance.Restringido)
        {
            var sucursales = alcance.Sucursales.ToList();
            q = q.Where(d => d.Lines.Any(l => !l.IsDeleted && sucursales.Contains(l.BranchId)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.Date).ThenByDescending(d => d.Number).ThenByDescending(d => d.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new ComprobanteResumenDto(d.PublicId, d.VoucherType!.Code, d.Number, d.Date, d.Description, d.Status.ToString(), d.Kind.ToString(),
                d.OriginModule, d.TotalDebit, d.TotalCredit, d.RegisteredBy, d.PostedBy, d.PostedAt, d.Lines.Count(l => !l.IsDeleted)))
            .ToListAsync(ct);
        return Result.Success(new PagedList<ComprobanteResumenDto>(items, total, request.Page, request.PageSize));
    }
}

// ------------------------------------------------------------------------------- por origen --

/// <summary>El comprobante vigente (contabilizado y no reversión) de un documento de módulo; si fue reversado, ese mismo con su reversión.</summary>
public sealed record GetDocumentBySourceQuery(string Module, Guid SourcePublicId) : IRequest<Result<ComprobanteDto>>;

public sealed class GetDocumentBySourceQueryValidator : AbstractValidator<GetDocumentBySourceQuery>
{
    public GetDocumentBySourceQueryValidator()
    {
        RuleFor(x => x.Module).NotEmpty().Must(m => ModuloContable.EsValido(m.ToUpperInvariant())).WithMessage("Módulo desconocido.");
        RuleFor(x => x.SourcePublicId).NotEmpty();
    }
}

public sealed class GetDocumentBySourceQueryHandler(IApplicationDbContext db, ISender sender) : IRequestHandler<GetDocumentBySourceQuery, Result<ComprobanteDto>>
{
    public async Task<Result<ComprobanteDto>> Handle(GetDocumentBySourceQuery request, CancellationToken ct)
    {
        var modulo = request.Module.Trim().ToUpperInvariant();
        var publicId = await db.AccountingDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && d.OriginModule == modulo && d.SourcePublicId == request.SourcePublicId && d.Kind != DocumentKind.Reversal)
            .OrderByDescending(d => d.Id)
            .Select(d => (Guid?)d.PublicId)
            .FirstOrDefaultAsync(ct);
        if (publicId is null) return Result.Failure<ComprobanteDto>(AccountingErrors.DocumentNotFound);
        return await sender.Send(new GetDocumentQuery(publicId.Value), ct);
    }
}

// ------------------------------------------------------------------------------ mis borradores --

public sealed record GetMyDraftsQuery : IRequest<Result<IReadOnlyList<ComprobanteResumenDto>>>;

public sealed class GetMyDraftsQueryValidator : AbstractValidator<GetMyDraftsQuery>;

public sealed class GetMyDraftsQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetMyDraftsQuery, Result<IReadOnlyList<ComprobanteResumenDto>>>
{
    public async Task<Result<IReadOnlyList<ComprobanteResumenDto>>> Handle(GetMyDraftsQuery request, CancellationToken ct)
    {
        var usuario = user.UserId ?? 0;
        var lista = await db.AccountingDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && d.Status == DocumentStatus.Draft && d.RegisteredByUserId == usuario)
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .Select(d => new ComprobanteResumenDto(d.PublicId, d.VoucherType!.Code, d.Number, d.Date, d.Description, d.Status.ToString(), d.Kind.ToString(),
                d.OriginModule, d.TotalDebit, d.TotalCredit, d.RegisteredBy, d.PostedBy, d.PostedAt, d.Lines.Count(l => !l.IsDeleted)))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ComprobanteResumenDto>>(lista);
    }
}

// ------------------------------------------------------------------------------------ imprimir --

public sealed record PrintDocumentQuery(Guid PublicId) : IRequest<Result<ComprobanteImpreso>>;

public sealed class PrintDocumentQueryValidator : AbstractValidator<PrintDocumentQuery>
{
    public PrintDocumentQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class PrintDocumentQueryHandler(IApplicationDbContext db, ISender sender, IVoucherPdfRenderer renderer) : IRequestHandler<PrintDocumentQuery, Result<ComprobanteImpreso>>
{
    public async Task<Result<ComprobanteImpreso>> Handle(PrintDocumentQuery request, CancellationToken ct)
    {
        var comprobante = await sender.Send(new GetDocumentQuery(request.PublicId), ct);
        if (comprobante.IsFailure) return Result.Failure<ComprobanteImpreso>(comprobante.Error);

        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new EmpresaParaImpresion(c.Name, c.TaxIdCheckDigit == null ? c.TaxId : c.TaxId + "-" + c.TaxIdCheckDigit))
            .FirstOrDefaultAsync(ct) ?? new EmpresaParaImpresion(string.Empty, string.Empty);

        var d = comprobante.Value;
        var nombre = d.Number is { } n ? $"comprobante-{d.VoucherTypeCode}-{n}.pdf" : $"borrador-{d.VoucherTypeCode}-{d.Date:yyyyMMdd}.pdf";
        return Result.Success(new ComprobanteImpreso(renderer.Render(d, empresa), nombre));
    }
}
