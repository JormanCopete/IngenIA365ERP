using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.VoucherTypes;

// Tipos de comprobante y tipos de documento cruce (feature 009, FR-018, FR-019, FR-013;
// contracts/api.md §4). Los sembrados no cambian de uso ni se eliminan; un código pertenece a un
// solo módulo.

public sealed record TipoComprobanteDto(Guid PublicId, string Code, string Name, string Usage, string? ModuleCode, long NextNumber, bool IsActive, bool IsSeeded);

public sealed record TipoCruceDto(Guid PublicId, string Code, string Name, bool IsActive, bool IsSeeded);

// ---------------------------------------------------------------------- tipos de comprobante --

public sealed record ListVoucherTypesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<TipoComprobanteDto>>>;

public sealed class ListVoucherTypesQueryValidator : AbstractValidator<ListVoucherTypesQuery>;

public sealed class ListVoucherTypesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListVoucherTypesQuery, Result<IReadOnlyList<TipoComprobanteDto>>>
{
    public async Task<Result<IReadOnlyList<TipoComprobanteDto>>> Handle(ListVoucherTypesQuery request, CancellationToken ct)
    {
        var q = db.VoucherTypes.AsNoTracking().Where(v => !v.IsDeleted);
        if (!request.IncludeInactive) q = q.Where(v => v.IsActive);
        var lista = await q.OrderBy(v => v.Usage).ThenBy(v => v.Code)
            .Select(v => new TipoComprobanteDto(v.PublicId, v.Code, v.Name, v.Usage.ToString(), v.ModuleCode, v.NextNumber, v.IsActive, v.IsSeeded))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<TipoComprobanteDto>>(lista);
    }
}

public sealed record CreateVoucherTypeCommand(string Code, string Name, string Usage, string? ModuleCode, bool IsActive = true) : IRequest<Result<Guid>>;

public sealed class CreateVoucherTypeCommandValidator : AbstractValidator<CreateVoucherTypeCommand>
{
    public CreateVoucherTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Usage).Must(u => Enum.TryParse<VoucherUsage>(u, true, out _)).WithMessage("Uso desconocido (Manual, Module, Closing, Opening, Assets).");
        RuleFor(x => x.ModuleCode).Must(m => m is null || ModuloContable.EsValido(m.ToUpperInvariant())).WithMessage("Módulo desconocido.");
        RuleFor(x => x).Must(x => !Enum.TryParse<VoucherUsage>(x.Usage, true, out var u) || u != VoucherUsage.Module || !string.IsNullOrWhiteSpace(x.ModuleCode))
            .WithMessage("Un tipo de módulo dice de qué módulo es.");
    }
}

public sealed class CreateVoucherTypeCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<CreateVoucherTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateVoucherTypeCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.VoucherTypes.AsNoTracking().Where(v => v.Code == codigo && !v.IsDeleted).Select(v => v.Name).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un tipo de comprobante", codigo, existente));

        var uso = Enum.Parse<VoucherUsage>(request.Usage, true);
        var tipo = new VoucherType
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Usage = uso,
            ModuleCode = uso is VoucherUsage.Module or VoucherUsage.Assets ? request.ModuleCode?.Trim().ToUpperInvariant() : null,
            NextNumber = 1,
            IsActive = request.IsActive,
            IsSeeded = false,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName ?? "system",
        };
        db.VoucherTypes.Add(tipo);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.VoucherType.Created", nameof(VoucherType), tipo.PublicId, null, new { tipo.Code, tipo.Name, usage = tipo.Usage.ToString(), tipo.ModuleCode, tipo.IsActive }, ct);
        return Result.Success(tipo.PublicId);
    }
}

public sealed record UpdateVoucherTypeCommand(Guid PublicId, string Name, string? Usage, string? ModuleCode, bool IsActive) : IRequest<Result>;

public sealed class UpdateVoucherTypeCommandValidator : AbstractValidator<UpdateVoucherTypeCommand>
{
    public UpdateVoucherTypeCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Usage).Must(u => u is null || Enum.TryParse<VoucherUsage>(u, true, out _)).WithMessage("Uso desconocido.");
    }
}

public sealed class UpdateVoucherTypeCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<UpdateVoucherTypeCommand, Result>
{
    public async Task<Result> Handle(UpdateVoucherTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.VoucherTypes.FirstOrDefaultAsync(v => v.PublicId == request.PublicId && !v.IsDeleted, ct);
        if (tipo is null) return Result.Failure(AccountingErrors.VoucherTypeNotFound(request.PublicId.ToString()));

        var antes = new { tipo.Name, usage = tipo.Usage.ToString(), tipo.ModuleCode, tipo.IsActive };
        if (request.Usage is { } usoTexto)
        {
            var uso = Enum.Parse<VoucherUsage>(usoTexto, true);
            var modulo = uso is VoucherUsage.Module or VoucherUsage.Assets ? request.ModuleCode?.Trim().ToUpperInvariant() : null;
            if (tipo.IsSeeded && (uso != tipo.Usage || modulo != tipo.ModuleCode)) return Result.Failure(AccountingErrors.VoucherTypeSeeded(tipo.Code));
            tipo.Usage = uso;
            tipo.ModuleCode = modulo;
        }
        tipo.Name = request.Name.Trim();
        tipo.IsActive = request.IsActive;
        tipo.UpdatedAt = clock.UtcNow;
        tipo.UpdatedBy = user.UserName ?? "system";
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.VoucherType.Updated", nameof(VoucherType), tipo.PublicId, antes, new { tipo.Name, usage = tipo.Usage.ToString(), tipo.ModuleCode, tipo.IsActive }, ct);
        return Result.Success();
    }
}

public sealed record SetVoucherTypeActiveCommand(Guid PublicId, bool Active) : IRequest<Result>;

public sealed class SetVoucherTypeActiveCommandValidator : AbstractValidator<SetVoucherTypeActiveCommand>
{
    public SetVoucherTypeActiveCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class SetVoucherTypeActiveCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<SetVoucherTypeActiveCommand, Result>
{
    public async Task<Result> Handle(SetVoucherTypeActiveCommand request, CancellationToken ct)
    {
        var tipo = await db.VoucherTypes.FirstOrDefaultAsync(v => v.PublicId == request.PublicId && !v.IsDeleted, ct);
        if (tipo is null) return Result.Failure(AccountingErrors.VoucherTypeNotFound(request.PublicId.ToString()));
        if (tipo.IsActive == request.Active) return Result.Success();
        tipo.IsActive = request.Active;
        tipo.UpdatedAt = clock.UtcNow;
        tipo.UpdatedBy = user.UserName ?? "system";
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync(request.Active ? "Accounting.VoucherType.Activated" : "Accounting.VoucherType.Deactivated", nameof(VoucherType), tipo.PublicId,
            new { tipo.Code, isActive = !request.Active }, new { tipo.Code, isActive = request.Active }, ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- documentos cruce --

public sealed record ListCrossDocumentTypesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<TipoCruceDto>>>;

public sealed class ListCrossDocumentTypesQueryValidator : AbstractValidator<ListCrossDocumentTypesQuery>;

public sealed class ListCrossDocumentTypesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCrossDocumentTypesQuery, Result<IReadOnlyList<TipoCruceDto>>>
{
    public async Task<Result<IReadOnlyList<TipoCruceDto>>> Handle(ListCrossDocumentTypesQuery request, CancellationToken ct)
    {
        var q = db.CrossDocumentTypes.AsNoTracking().Where(t => !t.IsDeleted);
        if (!request.IncludeInactive) q = q.Where(t => t.IsActive);
        var lista = await q.OrderBy(t => t.Code).Select(t => new TipoCruceDto(t.PublicId, t.Code, t.Name, t.IsActive, t.IsSeeded)).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TipoCruceDto>>(lista);
    }
}

public sealed record CreateCrossDocumentTypeCommand(string Code, string Name, bool IsActive = true) : IRequest<Result<Guid>>;

public sealed class CreateCrossDocumentTypeCommandValidator : AbstractValidator<CreateCrossDocumentTypeCommand>
{
    public CreateCrossDocumentTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class CreateCrossDocumentTypeCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<CreateCrossDocumentTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCrossDocumentTypeCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.CrossDocumentTypes.AsNoTracking().Where(t => t.Code == codigo && !t.IsDeleted).Select(t => t.Name).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un tipo de documento cruce", codigo, existente));

        var tipo = new CrossDocumentType { Code = codigo, Name = request.Name.Trim(), IsActive = request.IsActive, IsSeeded = false, CreatedAt = clock.UtcNow, CreatedBy = user.UserName ?? "system" };
        db.CrossDocumentTypes.Add(tipo);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.CrossDocumentType.Created", nameof(CrossDocumentType), tipo.PublicId, null, new { tipo.Code, tipo.Name, tipo.IsActive }, ct);
        return Result.Success(tipo.PublicId);
    }
}

public sealed record UpdateCrossDocumentTypeCommand(Guid PublicId, string Name, bool IsActive) : IRequest<Result>;

public sealed class UpdateCrossDocumentTypeCommandValidator : AbstractValidator<UpdateCrossDocumentTypeCommand>
{
    public UpdateCrossDocumentTypeCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}

public sealed class UpdateCrossDocumentTypeCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<UpdateCrossDocumentTypeCommand, Result>
{
    public async Task<Result> Handle(UpdateCrossDocumentTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.CrossDocumentTypes.FirstOrDefaultAsync(t => t.PublicId == request.PublicId && !t.IsDeleted, ct);
        if (tipo is null) return Result.Failure(AccountingErrors.CrossDocumentTypeNotFound);
        var antes = new { tipo.Name, tipo.IsActive };
        tipo.Name = request.Name.Trim();
        tipo.IsActive = request.IsActive;
        tipo.UpdatedAt = clock.UtcNow;
        tipo.UpdatedBy = user.UserName ?? "system";
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.CrossDocumentType.Updated", nameof(CrossDocumentType), tipo.PublicId, antes, new { tipo.Name, tipo.IsActive }, ct);
        return Result.Success();
    }
}
