using FluentValidation;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.BankFiles;

/// <summary>
/// Formatos de archivo bancario (feature 010, N4; D-42): dato con vigencia ligado al banco y
/// compartido por los módulos. Aquí se crean, se versionan y se consultan; generar un archivo es
/// asunto de cada módulo (nómina en <c>Payroll/Dispersion</c>). Un formato que ya escribió
/// archivos no cambia de estructura: se le cierra la vigencia y se crea la versión nueva, para
/// que lo generado siga siendo reproducible (Principio XI).
/// </summary>
public static class BankFileFormatErrors
{
    public static readonly Error NotFound = new("Core.BankFileFormat.NotFound", "No existe el formato indicado.");
    public static readonly Error BankNotFound = new("Core.BankFileFormat.BankNotFound", "No existe el banco indicado.");
    public static Error CodeDuplicate(string code, string nombre) =>
        new ErrorConDatos("Core.BankFileFormat.CodeDuplicate", $"Ya existe un formato con el código {code}: {nombre}.", new { code });
    public static Error Invalid(IReadOnlyList<BankFileFormatError> errores) =>
        new ErrorConDatos("Core.BankFileFormat.Invalid",
            $"El formato tiene {errores.Count} problema(s): {string.Join(" ", errores.Take(3).Select(e => e.Message))}",
            new { errors = errores.Select(e => new { record = e.Record, order = e.Order, field = e.Field, message = e.Message }).ToList() });
    public static Error Overlaps(string code, DateOnly desde, DateOnly? hasta) =>
        new ErrorConDatos("Core.BankFileFormat.Overlaps",
            $"El banco ya tiene el formato {code} vigente para ese ámbito entre {desde:dd/MM/yyyy} y {(hasta is { } h ? h.ToString("dd/MM/yyyy") : "sin fin")}; cierre esa vigencia o elija otras fechas.",
            new { code, validFrom = desde, validTo = hasta });
    public static Error InUse(int archivos) =>
        new ErrorConDatos("Core.BankFileFormat.InUse",
            $"Con este formato ya se generaron {archivos} archivo(s): su estructura no cambia. Sólo se editan el nombre, la vigencia final, las notas y el estado; para otra estructura cree una versión nueva.",
            new { files = archivos });
    public static readonly Error Seeded = new("Core.BankFileFormat.Seeded", "Un formato sembrado no se borra; desactívelo o cierre su vigencia.");
}

// -------------------------------------------------------------------- crear --

/// <summary>Un formato nuevo desde su definición JSON (contracts/archivos.md §2.1); se valida antes de guardar.</summary>
public sealed record CreateBankFileFormatCommand(BankFileFormatDefinition Definition) : IRequest<Result<Guid>>;

public sealed class CreateBankFileFormatCommandValidator : AbstractValidator<CreateBankFileFormatCommand>
{
    public CreateBankFileFormatCommandValidator() => RuleFor(x => x.Definition).NotNull().WithMessage("La definición del formato es obligatoria.");
}

public sealed class CreateBankFileFormatCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<CreateBankFileFormatCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBankFileFormatCommand request, CancellationToken ct)
    {
        var def = request.Definition;
        var errores = BankFileFormatValidator.Validar(def);
        if (errores.Count > 0) return Result.Failure<Guid>(BankFileFormatErrors.Invalid(errores));

        int? bankId = null;
        if (def.BankPublicId is { } bankPublicId)
        {
            var banco = await db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == bankPublicId, ct);
            if (banco is null) return Result.Failure<Guid>(BankFileFormatErrors.BankNotFound);
            bankId = banco.Id;
        }

        var code = def.Code.Trim().ToUpperInvariant();
        var duplicado = await db.BankFileFormats.AsNoTracking().FirstOrDefaultAsync(f => f.Code == code, ct);
        if (duplicado is not null) return Result.Failure<Guid>(BankFileFormatErrors.CodeDuplicate(code, duplicado.Name));

        var scope = Enum.Parse<BankFileScope>(def.Scope, true);
        var solapado = await SolapadoAsync(db, bankId, scope, def.ValidFrom, def.ValidTo, excluirId: null, ct);
        if (solapado is not null) return Result.Failure<Guid>(BankFileFormatErrors.Overlaps(solapado.Code, solapado.ValidFrom, solapado.ValidTo));

        var formato = new BankFileFormat { CreatedAt = clock.UtcNow, CreatedBy = user.UserName };
        def.AplicarA(formato, bankId);
        foreach (var f in formato.Fields) { f.CreatedAt = formato.CreatedAt; f.CreatedBy = user.UserName; }
        db.BankFileFormats.Add(formato);
        await db.SaveChangesAsync(ct);
        return Result.Success(formato.PublicId);
    }

    /// <summary>Otro formato activo del mismo banco y ámbito cuya vigencia se cruza con la pedida (un genérico no compite con los del banco).</summary>
    internal static async Task<BankFileFormat?> SolapadoAsync(IApplicationDbContext db, int? bankId, BankFileScope scope, DateOnly desde, DateOnly? hasta, int? excluirId, CancellationToken ct)
    {
        var candidatos = await db.BankFileFormats.AsNoTracking()
            .Where(f => f.BankId == bankId && f.Scope == scope && f.IsActive && (excluirId == null || f.Id != excluirId))
            .ToListAsync(ct);
        return candidatos.FirstOrDefault(f => f.ValidFrom <= (hasta ?? DateOnly.MaxValue) && (f.ValidTo ?? DateOnly.MaxValue) >= desde);
    }
}

// ------------------------------------------------------------------ editar --

/// <summary>
/// Edita un formato. Si ya generó archivos sólo cambian nombre, vigencia final, notas y estado
/// (<c>Core.BankFileFormat.InUse</c> para cualquier otra cosa); si no, se reemplaza entero.
/// </summary>
public sealed record UpdateBankFileFormatCommand(Guid FormatPublicId, BankFileFormatDefinition Definition) : IRequest<Result>;

public sealed class UpdateBankFileFormatCommandValidator : AbstractValidator<UpdateBankFileFormatCommand>
{
    public UpdateBankFileFormatCommandValidator()
    {
        RuleFor(x => x.FormatPublicId).NotEmpty();
        RuleFor(x => x.Definition).NotNull();
    }
}

public sealed class UpdateBankFileFormatCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<UpdateBankFileFormatCommand, Result>
{
    public async Task<Result> Handle(UpdateBankFileFormatCommand request, CancellationToken ct)
    {
        var formato = await db.BankFileFormats.Include(f => f.Fields).Include(f => f.Bank).FirstOrDefaultAsync(f => f.PublicId == request.FormatPublicId, ct);
        if (formato is null) return Result.Failure(BankFileFormatErrors.NotFound);

        var def = request.Definition;
        var errores = BankFileFormatValidator.Validar(def);
        if (errores.Count > 0) return Result.Failure(BankFileFormatErrors.Invalid(errores));

        var archivos = await db.BankDisbursementFiles.AsNoTracking().CountAsync(a => a.FormatId == formato.Id, ct);
        if (archivos > 0)
        {
            // Sólo lo que no cambia el archivo: nombre, fin de vigencia, notas y estado.
            var actual = BankFileFormatDefinition.Desde(formato);
            var propuesto = def.ToJson();
            actual.Name = def.Name; actual.ValidTo = def.ValidTo; actual.Notes = def.Notes; actual.IsActive = def.IsActive;
            if (!string.Equals(actual.ToJson(), propuesto, StringComparison.Ordinal))
                return Result.Failure(BankFileFormatErrors.InUse(archivos));
            formato.Name = def.Name.Trim();
            formato.ValidTo = def.ValidTo;
            formato.Notes = string.IsNullOrWhiteSpace(def.Notes) ? null : def.Notes.Trim();
            formato.IsActive = def.IsActive;
        }
        else
        {
            int? bankId = null;
            if (def.BankPublicId is { } bankPublicId)
            {
                var banco = await db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == bankPublicId, ct);
                if (banco is null) return Result.Failure(BankFileFormatErrors.BankNotFound);
                bankId = banco.Id;
            }
            var code = def.Code.Trim().ToUpperInvariant();
            var duplicado = await db.BankFileFormats.AsNoTracking().FirstOrDefaultAsync(f => f.Code == code && f.Id != formato.Id, ct);
            if (duplicado is not null) return Result.Failure(BankFileFormatErrors.CodeDuplicate(code, duplicado.Name));
            var scope = Enum.Parse<BankFileScope>(def.Scope, true);
            if (def.IsActive)
            {
                var solapado = await CreateBankFileFormatCommandHandler.SolapadoAsync(db, bankId, scope, def.ValidFrom, def.ValidTo, formato.Id, ct);
                if (solapado is not null) return Result.Failure(BankFileFormatErrors.Overlaps(solapado.Code, solapado.ValidFrom, solapado.ValidTo));
            }
            foreach (var viejo in formato.Fields.ToList()) db.BankFileFormatFields.Remove(viejo);
            def.AplicarA(formato, bankId);
            foreach (var f in formato.Fields) { f.CreatedAt = clock.UtcNow; f.CreatedBy = user.UserName; }
        }

        formato.UpdatedAt = clock.UtcNow;
        formato.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ------------------------------------------------------------------ retirar --

/// <summary>Retiro suave de un formato propio sin archivos; uno sembrado o ya usado se desactiva en vez de borrarse.</summary>
public sealed record DeleteBankFileFormatCommand(Guid FormatPublicId) : IRequest<Result>;

public sealed class DeleteBankFileFormatCommandValidator : AbstractValidator<DeleteBankFileFormatCommand>
{
    public DeleteBankFileFormatCommandValidator() => RuleFor(x => x.FormatPublicId).NotEmpty();
}

public sealed class DeleteBankFileFormatCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<DeleteBankFileFormatCommand, Result>
{
    public async Task<Result> Handle(DeleteBankFileFormatCommand request, CancellationToken ct)
    {
        var formato = await db.BankFileFormats.FirstOrDefaultAsync(f => f.PublicId == request.FormatPublicId, ct);
        if (formato is null) return Result.Failure(BankFileFormatErrors.NotFound);
        if (formato.IsSeeded) return Result.Failure(BankFileFormatErrors.Seeded);
        var archivos = await db.BankDisbursementFiles.AsNoTracking().CountAsync(a => a.FormatId == formato.Id, ct);
        if (archivos > 0) return Result.Failure(BankFileFormatErrors.InUse(archivos));

        formato.IsDeleted = true;
        formato.IsActive = false;
        formato.UpdatedAt = clock.UtcNow;
        formato.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
