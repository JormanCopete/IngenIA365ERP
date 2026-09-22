using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents;

// Digitación manual (feature 009, US3; FR-025..FR-031; contracts/api.md §6). El borrador se guarda
// con errores; contabilizar pasa por AccountingPoster, el único camino al libro; la corrección de
// lo contabilizado es la reversión (Principio XI): aquí nadie edita ni borra una línea
// contabilizada, y las sobrantes de un borrador se marcan eliminadas, nunca se quitan.

// ---------------------------------------------------------------------------- guardar borrador --

/// <summary>Crea (<c>PublicId</c> null) o actualiza un borrador. Sólo tipos de uso manual. Devuelve las infracciones actuales sin bloquear.</summary>
public sealed record SaveDraftDocumentCommand(
    Guid? PublicId,
    string VoucherTypeCode,
    DateOnly Date,
    string Description,
    IReadOnlyList<LineaDeBorradorInput> Lines) : IRequest<Result<BorradorGuardadoDto>>;

public sealed class LineaDeBorradorInputValidator : AbstractValidator<LineaDeBorradorInput>
{
    public LineaDeBorradorInputValidator()
    {
        RuleFor(x => x.AccountCode).NotEmpty().WithMessage("Cada línea lleva cuenta.").MaximumLength(20);
        RuleFor(x => x.Debit).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Credit).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Detail).MaximumLength(AccountingPoster.LargoDeDescripcion);
        RuleFor(x => x.CrossDocumentType).MaximumLength(10);
        RuleFor(x => x.CrossDocumentNumber).MaximumLength(30);
        RuleFor(x => x.TaxBase).GreaterThanOrEqualTo(0m).When(x => x.TaxBase is not null);
    }
}

public sealed class SaveDraftDocumentCommandValidator : AbstractValidator<SaveDraftDocumentCommand>
{
    public SaveDraftDocumentCommandValidator()
    {
        RuleFor(x => x.VoucherTypeCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Description).MaximumLength(AccountingPoster.LargoDeDescripcion);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Digite al menos una línea.");
        RuleForEach(x => x.Lines).SetValidator(new LineaDeBorradorInputValidator());
    }
}

public sealed class SaveDraftDocumentCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IUserBranchScope scope, AccountingPoster poster)
    : IRequestHandler<SaveDraftDocumentCommand, Result<BorradorGuardadoDto>>
{
    public async Task<Result<BorradorGuardadoDto>> Handle(SaveDraftDocumentCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<BorradorGuardadoDto>(AccountingErrors.NotInitialized);

        var codigoTipo = request.VoucherTypeCode.Trim().ToUpperInvariant();
        var tipo = await db.VoucherTypes.AsNoTracking().FirstOrDefaultAsync(v => v.Code == codigoTipo && !v.IsDeleted, ct);
        if (tipo is null) return Result.Failure<BorradorGuardadoDto>(AccountingErrors.VoucherTypeNotFound(codigoTipo));
        // La apertura (US13, FR-085) también se digita: es un borrador del tipo reservado AP, de clase
        // Opening y con la fecha que le toca (la víspera del primer período), no la que se digite.
        var esApertura = tipo.Usage == VoucherUsage.Opening;
        if (tipo.Usage != VoucherUsage.Manual && !esApertura) return Result.Failure<BorradorGuardadoDto>(AccountingErrors.DocumentManualOnly);
        if (!tipo.IsActive) return Result.Failure<BorradorGuardadoDto>(AccountingErrors.VoucherTypeInactive(codigoTipo));
        var fecha = request.Date;
        if (esApertura)
        {
            // Una apertura contabilizada bloquea otra (FR-087); la que se está editando, no.
            if (await Opening.Aperturas.VigenteAsync(db, ct) is { } vigente) return Result.Failure<BorradorGuardadoDto>(Opening.Aperturas.YaExiste(vigente));
            // La fecha la elige quien implanta (E2): si no viene ninguna se propone la víspera del primer período.
            if (fecha == default)
            {
                var esperada = await Opening.Aperturas.FechaEsperadaAsync(db, ct);
                if (esperada.IsFailure) return Result.Failure<BorradorGuardadoDto>(esperada.Error);
                fecha = esperada.Value;
            }
            if (await AccountingPoster.FechaDeAperturaInvalidaAsync(db, fecha, ct) is { } reparo) return Result.Failure<BorradorGuardadoDto>(reparo);
        }

        AccountingDocument? documento = null;
        if (request.PublicId is { } id)
        {
            documento = await db.AccountingDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.PublicId == id && !d.IsDeleted, ct);
            if (documento is null) return Result.Failure<BorradorGuardadoDto>(AccountingErrors.DocumentNotFound);
            if (documento.Status != DocumentStatus.Draft) return Result.Failure<BorradorGuardadoDto>(AccountingErrors.DocumentNotDraft);
        }

        var refs = await LineasDeBorrador.ResolverAsync(db, request.Lines, ct);
        var irrecuperables = LineasDeBorrador.Irrecuperables(request.Lines, refs);
        if (irrecuperables.Count > 0)
            return Result.Failure<BorradorGuardadoDto>(irrecuperables.Count == 1 ? irrecuperables[0].ComoError() : AccountingErrors.DocumentInvalid(irrecuperables));

        // Las infracciones se informan, no bloquean (FR-026): el borrador existe para corregirlas.
        var validacion = await poster.ValidarAsync(new PostingRequest(codigoTipo, fecha, request.Description, AccountingOrigin.Manual(request.PublicId ?? Guid.Empty),
            LineasDeBorrador.AlContrato(request.Lines, refs), esApertura ? DocumentKind.Opening : DocumentKind.Regular), ct);

        var ahora = clock.UtcNow;
        var quien = string.IsNullOrWhiteSpace(user.UserName) ? "system" : user.UserName;
        var alcance = await scope.ObtenerAsync(ct);
        var sucursalPropuesta = alcance.SucursalPorDefecto ?? setup.MainBranchId;
        var periodo = esApertura ? null : await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted && p.StartDate <= fecha && p.EndDate >= fecha).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);

        if (documento is null)
        {
            documento = poster.NuevoBorrador();
        }
        else
        {
            documento.UpdatedAt = ahora;
            documento.UpdatedBy = quien;
        }

        documento.VoucherTypeId = tipo.Id;
        documento.Kind = esApertura ? DocumentKind.Opening : DocumentKind.Regular;
        documento.Date = fecha;
        documento.Description = request.Description.Trim();
        documento.PeriodId = periodo;
        documento.TotalDebit = request.Lines.Sum(l => l.Debit);
        documento.TotalCredit = request.Lines.Sum(l => l.Credit);
        // Quien registra es quien lo escribió por última vez: contra esa persona se aplican los cuatro ojos.
        documento.RegisteredByUserId = user.UserId ?? 0;
        documento.RegisteredBy = quien;

        // Líneas por número: se actualizan las que siguen, se agregan las nuevas y las sobrantes se
        // marcan eliminadas (nunca Remove: lo vigila PrincipioXI_ContableImmutable).
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var entrada = request.Lines[i];
            var cuenta = refs.Cuentas[entrada.AccountCode.Trim()];
            var linea = i < vivas.Count ? vivas[i] : null;
            if (linea is null)
            {
                linea = poster.NuevaLineaDeBorrador();
                documento.Lines.Add(linea);
            }
            else
            {
                linea.UpdatedAt = ahora;
                linea.UpdatedBy = quien;
            }
            linea.LineNumber = i + 1;
            linea.AccountId = cuenta.Id;
            // Sin sucursal digitada queda la propuesta; «exige sucursal» se revisa al validar (regla 7).
            linea.BranchId = refs.Sucursal(entrada.BranchPublicId) ?? sucursalPropuesta;
            linea.CostCenterId = refs.Centro(entrada.CostCenterPublicId);
            linea.PersonId = refs.Tercero(entrada.PersonPublicId);
            linea.CrossDocumentTypeId = null;
            linea.CrossDocumentNumber = string.IsNullOrWhiteSpace(entrada.CrossDocumentNumber) ? null : entrada.CrossDocumentNumber.Trim();
            linea.Debit = entrada.Debit;
            linea.Credit = entrada.Credit;
            linea.Description = string.IsNullOrWhiteSpace(entrada.Detail) ? null : entrada.Detail.Trim();
            linea.TaxBase = entrada.TaxBase;
            linea.Date = fecha;
            linea.IsPosted = false;
        }
        for (var i = request.Lines.Count; i < vivas.Count; i++)
        {
            vivas[i].IsDeleted = true;
            vivas[i].DeletedAt = ahora;
            vivas[i].UpdatedAt = ahora;
            vivas[i].UpdatedBy = quien;
        }

        // El tipo de documento cruce se guarda por Id; los que no existen ya vienen señalados en la validación.
        var codigosDeCruce = request.Lines.Where(l => !string.IsNullOrWhiteSpace(l.CrossDocumentType)).Select(l => l.CrossDocumentType!.Trim().ToUpperInvariant()).Distinct().ToList();
        if (codigosDeCruce.Count > 0)
        {
            var tiposDeCruce = await db.CrossDocumentTypes.AsNoTracking().Where(t => !t.IsDeleted && codigosDeCruce.Contains(t.Code)).ToDictionaryAsync(t => t.Code, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);
            var porNumero = documento.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.LineNumber);
            for (var i = 0; i < request.Lines.Count; i++)
            {
                var codigo = request.Lines[i].CrossDocumentType?.Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(codigo) && tiposDeCruce.TryGetValue(codigo, out var tipoId)) porNumero[i + 1].CrossDocumentTypeId = tipoId;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(new BorradorGuardadoDto(documento.PublicId, validacion.Errores.Concat(validacion.Avisos).Select(ErrorDeLineaDto.De).ToList()));
    }
}

// --------------------------------------------------------------------------- descartar borrador --

public sealed record DiscardDraftCommand(Guid PublicId) : IRequest<Result>;

public sealed class DiscardDraftCommandValidator : AbstractValidator<DiscardDraftCommand>
{
    public DiscardDraftCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class DiscardDraftCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user) : IRequestHandler<DiscardDraftCommand, Result>
{
    public async Task<Result> Handle(DiscardDraftCommand request, CancellationToken ct)
    {
        var documento = await db.AccountingDocuments.Include(d => d.Lines).FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (documento is null) return Result.Failure(AccountingErrors.DocumentNotFound);
        if (documento.Status != DocumentStatus.Draft) return Result.Failure(AccountingErrors.DocumentNotDraft);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        documento.IsDeleted = true;
        documento.DeletedAt = ahora;
        documento.UpdatedAt = ahora;
        documento.UpdatedBy = quien;
        foreach (var l in documento.Lines.Where(l => !l.IsDeleted))
        {
            l.IsDeleted = true;
            l.DeletedAt = ahora;
            l.UpdatedAt = ahora;
            l.UpdatedBy = quien;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ------------------------------------------------------------------------------- contabilizar --

/// <summary>
/// Contabiliza el borrador en su sitio: número, período, líneas resueltas. Con cuatro ojos, quien
/// contabiliza no puede ser quien lo registró (FR-029). Reintenta ante una carrera de numeración.
/// </summary>
public sealed record PostDocumentCommand(Guid PublicId) : IRequest<Result<ContabilizadoDto>>, IReintentableAnteConcurrencia;

public sealed class PostDocumentCommandValidator : AbstractValidator<PostDocumentCommand>
{
    public PostDocumentCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class PostDocumentCommandHandler(IApplicationDbContext db, ICurrentUserService user, AccountingPoster poster) : IRequestHandler<PostDocumentCommand, Result<ContabilizadoDto>>
{
    public async Task<Result<ContabilizadoDto>> Handle(PostDocumentCommand request, CancellationToken ct)
    {
        var documento = await db.AccountingDocuments
            .Include(d => d.VoucherType)
            .Include(d => d.Lines).ThenInclude(l => l.CrossDocumentType)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (documento is null) return Result.Failure<ContabilizadoDto>(AccountingErrors.DocumentNotFound);
        if (documento.Status != DocumentStatus.Draft) return Result.Failure<ContabilizadoDto>(AccountingErrors.DocumentNotDraft);
        if (!documento.EsDeModulo && documento.VoucherType?.Usage is not (VoucherUsage.Manual or VoucherUsage.Opening))
            return Result.Failure<ContabilizadoDto>(AccountingErrors.DocumentManualOnly);

        // Con seguimiento: la apertura contabilizada se referencia desde la configuración (FR-087).
        var setup = await db.AccountingSetups.FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<ContabilizadoDto>(AccountingErrors.NotInitialized);
        if (setup.FourEyes && documento.RegisteredByUserId == (user.UserId ?? 0)) return Result.Failure<ContabilizadoDto>(AccountingErrors.DocumentFourEyes);
        if (documento.Kind == DocumentKind.Opening && await Opening.Aperturas.VigenteAsync(db, ct) is { } vigente)
            return Result.Failure<ContabilizadoDto>(Opening.Aperturas.YaExiste(vigente));

        var contabilizado = await poster.ContabilizarBorradorAsync(documento, LineasDeBorrador.DesdeDocumento(documento), ct);
        if (contabilizado.IsFailure) return Result.Failure<ContabilizadoDto>(contabilizado.Error);
        if (documento.Kind == DocumentKind.Opening) setup.OpeningDocument = documento;

        await db.SaveChangesAsync(ct);
        return Result.Success(new ContabilizadoDto(documento.Number!.Value));
    }
}

// ------------------------------------------------------------------------------------ reversar --

/// <summary>Reversa un comprobante manual contabilizado; los de módulo se reversan desde su módulo (<c>data.origin</c> dice cuál).</summary>
public sealed record ReverseDocumentCommand(Guid PublicId, string Reason, DateOnly? Date = null) : IRequest<Result<ReversadoDto>>, IReintentableAnteConcurrencia;

public sealed class ReverseDocumentCommandValidator : AbstractValidator<ReverseDocumentCommand>
{
    public ReverseDocumentCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la reversión.").MaximumLength(AccountingPoster.LargoDeDescripcion);
    }
}

public sealed class ReverseDocumentCommandHandler(IApplicationDbContext db, IDateTimeService clock, AccountingPoster poster) : IRequestHandler<ReverseDocumentCommand, Result<ReversadoDto>>
{
    public async Task<Result<ReversadoDto>> Handle(ReverseDocumentCommand request, CancellationToken ct)
    {
        var original = await db.AccountingDocuments.Include(d => d.VoucherType).Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (original is null) return Result.Failure<ReversadoDto>(AccountingErrors.DocumentNotFound);
        if (original.EsDeModulo)
        {
            var error = AccountingErrors.DocumentModuleOwned(original.OriginModule);
            return Result.Failure<ReversadoDto>(new ErrorConDatos(error.Code, error.Message,
                new { origin = new { module = original.OriginModule, moduleName = ModuloContable.Nombre(original.OriginModule), sourceType = original.SourceType, sourcePublicId = original.SourcePublicId } }));
        }

        if (original.Kind == DocumentKind.Closing) return Result.Failure<ReversadoDto>(AccountingErrors.DocumentIsClosing);

        var reverso = await poster.PrepareReversalAsync(original, request.Date ?? clock.TodayUtc, request.Reason, AccountingOrigin.Manual(original.PublicId), ct);
        if (reverso.IsFailure) return Result.Failure<ReversadoDto>(reverso.Error);

        // Reversada la apertura, la empresa vuelve a poder cargar otra (FR-087); las dos quedan referenciadas entre sí.
        if (original.Kind == DocumentKind.Opening)
        {
            var setup = await db.AccountingSetups.FirstOrDefaultAsync(s => !s.IsDeleted && s.OpeningDocumentId == original.Id, ct);
            if (setup is not null) setup.OpeningDocumentId = null;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(new ReversadoDto(reverso.Value.PublicId, reverso.Value.Number!.Value));
    }
}
