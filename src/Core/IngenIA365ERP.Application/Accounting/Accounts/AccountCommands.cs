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

namespace IngenIA365ERP.Application.Accounting.Accounts;

// ---------------------------------------------------------------------------- crear --

/// <summary>
/// Crea una auxiliar (nivel 5 o 6) con sus reglas (feature 009, US2, FR-008..FR-011): cuelga de
/// una subcuenta o de una auxiliar de nivel 5, empieza por el código del padre, tiene la longitud
/// configurada para su nivel, hereda naturaleza y rubro, y sólo la del nivel de movimiento recibe
/// reglas. Un código repetido responde <c>Catalogo.CodigoDuplicado</c> con el nombre del existente.
/// </summary>
public sealed record CreateAccountCommand(
    string Code,
    string Name,
    Guid? ParentPublicId,
    IReadOnlyList<string> EnabledModules,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    CuentaBancariaInput? Bank,
    CuentaDeImpuestoInput? Tax) : IRequest<Result<Guid>>;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(12).Matches("^[0-9]+$").WithMessage("El código de una cuenta es numérico.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ParentPublicId).NotEmpty().WithMessage("Una auxiliar cuelga de una cuenta padre.");
        RuleForEach(x => x.EnabledModules).Must(ModuloContable.EsValido).WithMessage("Módulo desconocido.");
        When(x => x.Tax is not null, () =>
        {
            RuleFor(x => x.Tax!.Kind).Must(k => TaxKindTexto.Parse(k) is not null and not TaxKind.None).WithMessage("Clase de impuesto desconocida.");
            RuleFor(x => x.Tax!.Rates).Must(r => r.Select(t => t.ValidFrom).Distinct().Count() == r.Count).WithMessage("Dos tarifas no pueden tener la misma vigencia.");
            RuleForEach(x => x.Tax!.Rates).Must(t => t.Rate >= 0m && t.Rate <= 1m).WithMessage("La tarifa es una fracción entre 0 y 1 (0.04 es el 4 %).");
        });
        When(x => x.Bank is not null, () => RuleFor(x => x.Bank!.AccountNumber).MaximumLength(30));
    }
}

public sealed class CreateAccountCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<CreateAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<Guid>(AccountingErrors.NotInitialized);

        var padre = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.PublicId == request.ParentPublicId && !a.IsDeleted, ct);
        if (padre is null) return Result.Failure<Guid>(AccountingErrors.AccountParentNotFound);

        var codigo = request.Code.Trim();
        var nivel = (byte)(padre.Level + 1);
        if (nivel > setup.MovementLevel) return Result.Failure<Guid>(AccountingErrors.AccountLevelNotAllowed(nivel, setup.MovementLevel));
        if (nivel < 5)
        {
            // Cuenta propia (grupo, cuenta o subcuenta) sólo donde el catálogo no trae hijos: el CUIF
            // solidario deja así 127 cuentas y 6 grupos (reservas, fondos sociales, provisiones,
            // excedentes, contras de orden…). Donde sí los define, las auxiliares cuelgan de ellos.
            var conHijosDelCatalogo = await db.ChartOfAccounts.AsNoTracking()
                .AnyAsync(a => a.ParentId == padre.Id && !a.IsDeleted && a.Origin == AccountOrigin.Catalog, ct);
            if (conHijosDelCatalogo)
                return Result.Failure<Guid>(AccountingErrors.AccountCodeInvalid($"Bajo {padre.Code} el catálogo ya define sus cuentas: elija una de ellas. Las cuentas propias van sólo donde el catálogo no trae ninguna."));
        }
        if (LongitudDeAuxiliar.Reparo(codigo, nivel) is { } reparoDeLargo) return Result.Failure<Guid>(AccountingErrors.AccountCodeInvalid(reparoDeLargo));
        if (!codigo.StartsWith(padre.Code, StringComparison.Ordinal)) return Result.Failure<Guid>(AccountingErrors.AccountCodeInvalid($"El código debe empezar por el de su cuenta padre ({padre.Code})."));

        var existente = await db.ChartOfAccounts.AsNoTracking().Where(a => a.Code == codigo && !a.IsDeleted).Select(a => a.Name).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("una cuenta", codigo, existente));

        var esDeMovimiento = nivel == setup.MovementLevel;
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var cuenta = new ChartOfAccount
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Level = nivel,
            Nature = padre.Nature,
            ParentId = padre.Id,
            NiifItemCode = padre.NiifItemCode,
            Origin = AccountOrigin.Company,
            IsMovement = esDeMovimiento,
            IsActive = true,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        if (esDeMovimiento)
        {
            var reglas = await AplicarReglasAsync(db, cuenta, request.EnabledModules, request.RequiresThirdParty, request.RequiresCrossDocument,
                request.RequiresCostCenter, request.RequiresBranch, request.Bank, request.Tax, ahora, quien, ct);
            if (reglas.IsFailure) return Result.Failure<Guid>(reglas.Error);
        }
        db.ChartOfAccounts.Add(cuenta);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Account.Created", nameof(ChartOfAccount), cuenta.PublicId, null, Instantanea(cuenta), ct);
        return Result.Success(cuenta.PublicId);
    }

    /// <summary>Aplica reglas, banco e impuesto a una cuenta de movimiento; lo comparten crear y editar.</summary>
    internal static async Task<Result> AplicarReglasAsync(IApplicationDbContext db, ChartOfAccount cuenta, IReadOnlyList<string> modulos,
        bool tercero, bool cruce, bool centro, bool sucursal, CuentaBancariaInput? banco, CuentaDeImpuestoInput? impuesto, DateTime ahora, string quien, CancellationToken ct)
    {
        cuenta.EnabledModules = ModuloContable.Desde(modulos);
        cuenta.RequiresThirdParty = tercero;
        cuenta.RequiresCrossDocument = cruce;
        cuenta.RequiresCostCenter = centro;
        cuenta.RequiresBranch = sucursal;

        if (banco is null)
        {
            cuenta.BankId = null;
            cuenta.BankAccountNumber = null;
        }
        else
        {
            var b = await db.Banks.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == banco.BankPublicId && !x.IsDeleted, ct);
            if (b is null) return Result.Failure(AccountingErrors.AccountBankNotFound);
            cuenta.BankId = b.Id;
            cuenta.BankAccountNumber = string.IsNullOrWhiteSpace(banco.AccountNumber) ? null : banco.AccountNumber.Trim();
        }

        if (impuesto is null)
        {
            cuenta.TaxKind = TaxKind.None;
            cuenta.TaxConceptCode = null;
            cuenta.RequiresTaxBase = false;
            foreach (var t in cuenta.TaxRates.Where(t => !t.IsDeleted)) { t.IsDeleted = true; t.DeletedAt = ahora; t.DeletedBy = quien; }
        }
        else
        {
            cuenta.TaxKind = TaxKindTexto.Parse(impuesto.Kind) ?? TaxKind.None;
            cuenta.TaxConceptCode = string.IsNullOrWhiteSpace(impuesto.ConceptCode) ? null : impuesto.ConceptCode.Trim();
            cuenta.RequiresTaxBase = impuesto.RequiresTaxBase;
            // Las tarifas se reemplazan por vigencia: las que ya no vienen se retiran, las nuevas se agregan, las iguales se dejan.
            var vigentes = cuenta.TaxRates.Where(t => !t.IsDeleted).ToList();
            foreach (var t in vigentes.Where(t => !impuesto.Rates.Any(r => r.ValidFrom == t.ValidFrom && r.Rate == t.Rate)))
            {
                t.IsDeleted = true; t.DeletedAt = ahora; t.DeletedBy = quien;
            }
            foreach (var r in impuesto.Rates.Where(r => !vigentes.Any(t => t.ValidFrom == r.ValidFrom && t.Rate == r.Rate)))
                cuenta.TaxRates.Add(new AccountTaxRate { ValidFrom = r.ValidFrom, Rate = r.Rate, CreatedAt = ahora, CreatedBy = quien });
        }
        return Result.Success();
    }

    internal static object Instantanea(ChartOfAccount c) => new
    {
        c.Code, c.Name, c.Level, nature = c.Nature.ToString(), c.IsMovement, c.IsActive, modules = ModuloContable.Lista(c.EnabledModules),
        c.RequiresThirdParty, c.RequiresCrossDocument, c.RequiresCostCenter, c.RequiresBranch, c.BankId, c.BankAccountNumber,
        taxKind = c.TaxKind.ToString(), c.TaxConceptCode, c.RequiresTaxBase,
        rates = c.TaxRates.Where(t => !t.IsDeleted).OrderBy(t => t.ValidFrom).Select(t => new { t.ValidFrom, t.Rate }).ToList(),
    };
}

// --------------------------------------------------------------------------- editar --

/// <summary>
/// Edita una auxiliar (FR-012): con movimientos sólo cambian el nombre y el estado; las reglas
/// quedan bloqueadas y la respuesta dice desde cuándo. Las cuentas del catálogo no se editan.
/// </summary>
public sealed record UpdateAccountCommand(
    Guid PublicId,
    string Name,
    IReadOnlyList<string> EnabledModules,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    CuentaBancariaInput? Bank,
    CuentaDeImpuestoInput? Tax) : IRequest<Result>;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.EnabledModules).Must(ModuloContable.EsValido).WithMessage("Módulo desconocido.");
        When(x => x.Tax is not null, () =>
        {
            RuleFor(x => x.Tax!.Kind).Must(k => TaxKindTexto.Parse(k) is not null and not TaxKind.None).WithMessage("Clase de impuesto desconocida.");
            RuleFor(x => x.Tax!.Rates).Must(r => r.Select(t => t.ValidFrom).Distinct().Count() == r.Count).WithMessage("Dos tarifas no pueden tener la misma vigencia.");
        });
    }
}

public sealed class UpdateAccountCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<UpdateAccountCommand, Result>
{
    public async Task<Result> Handle(UpdateAccountCommand request, CancellationToken ct)
    {
        var cuenta = await db.ChartOfAccounts.Include(a => a.TaxRates).FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);
        if (cuenta is null) return Result.Failure(AccountingErrors.AccountNotFound(request.PublicId.ToString()));
        if (cuenta.Origin == AccountOrigin.Catalog) return Result.Failure(AccountingErrors.AccountFromCatalog);

        var antes = CreateAccountCommandHandler.Instantanea(cuenta);
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";

        if (cuenta.IsMovement)
        {
            var cambianReglas =
                cuenta.EnabledModules != ModuloContable.Desde(request.EnabledModules) ||
                cuenta.RequiresThirdParty != request.RequiresThirdParty || cuenta.RequiresCrossDocument != request.RequiresCrossDocument ||
                cuenta.RequiresCostCenter != request.RequiresCostCenter || cuenta.RequiresBranch != request.RequiresBranch ||
                (request.Bank is null) != (cuenta.BankId is null) ||
                (request.Tax is null) != (cuenta.TaxKind == TaxKind.None) ||
                (request.Tax is not null && (TaxKindTexto.Parse(request.Tax.Kind) != cuenta.TaxKind || request.Tax.RequiresTaxBase != cuenta.RequiresTaxBase));
            if (cambianReglas && cuenta.FirstMovementAt is { } desde)
                return Result.Failure(AccountingErrors.AccountLocked(desde));

            if (cuenta.FirstMovementAt is null)
            {
                var reglas = await CreateAccountCommandHandler.AplicarReglasAsync(db, cuenta, request.EnabledModules, request.RequiresThirdParty,
                    request.RequiresCrossDocument, request.RequiresCostCenter, request.RequiresBranch, request.Bank, request.Tax, ahora, quien, ct);
                if (reglas.IsFailure) return reglas;
            }
            else if (request.Tax is not null && cuenta.TaxKind != TaxKind.None)
            {
                // Con movimientos, lo único que sigue cambiando de la parte tributaria es agregar vigencias nuevas (FR-065).
                foreach (var r in request.Tax.Rates.Where(r => !cuenta.TaxRates.Any(t => !t.IsDeleted && t.ValidFrom == r.ValidFrom)))
                    cuenta.TaxRates.Add(new AccountTaxRate { ValidFrom = r.ValidFrom, Rate = r.Rate, CreatedAt = ahora, CreatedBy = quien });
            }
        }

        cuenta.Name = request.Name.Trim();
        cuenta.UpdatedAt = ahora;
        cuenta.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Account.Updated", nameof(ChartOfAccount), cuenta.PublicId, antes, CreateAccountCommandHandler.Instantanea(cuenta), ct);
        return Result.Success();
    }
}

// ------------------------------------------------------------------ activar / inactivar --

/// <summary>FR-007: las cuentas del catálogo sólo se activan o inactivan; una inactiva no recibe movimientos nuevos.</summary>
public sealed record SetAccountActiveCommand(Guid PublicId, bool Active) : IRequest<Result>;

public sealed class SetAccountActiveCommandValidator : AbstractValidator<SetAccountActiveCommand>
{
    public SetAccountActiveCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class SetAccountActiveCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<SetAccountActiveCommand, Result>
{
    public async Task<Result> Handle(SetAccountActiveCommand request, CancellationToken ct)
    {
        var cuenta = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);
        if (cuenta is null) return Result.Failure(AccountingErrors.AccountNotFound(request.PublicId.ToString()));
        if (cuenta.IsActive == request.Active) return Result.Success();

        cuenta.IsActive = request.Active;
        cuenta.UpdatedAt = clock.UtcNow;
        cuenta.UpdatedBy = user.UserName ?? "system";
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(request.Active ? "Accounting.Account.Activated" : "Accounting.Account.Deactivated", nameof(ChartOfAccount), cuenta.PublicId,
            new { cuenta.Code, isActive = !request.Active }, new { cuenta.Code, isActive = request.Active }, ct);
        return Result.Success();
    }
}

// ------------------------------------------------------------------------- eliminar --

/// <summary>
/// Elimina (soft) una auxiliar sin movimientos, sin hijas y sin parametrizaciones que la
/// referencien (FR-011): la respuesta lista dónde está parametrizada para que la persona sepa
/// qué cambiar primero.
/// </summary>
public sealed record DeleteAccountCommand(Guid PublicId) : IRequest<Result>;

public sealed class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class DeleteAccountCommandHandler(IApplicationDbContext db, IAccountReferenceFinder referencias, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<DeleteAccountCommand, Result>
{
    public async Task<Result> Handle(DeleteAccountCommand request, CancellationToken ct)
    {
        var cuenta = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);
        if (cuenta is null) return Result.Failure(AccountingErrors.AccountNotFound(request.PublicId.ToString()));
        if (cuenta.Origin == AccountOrigin.Catalog) return Result.Failure(AccountingErrors.AccountFromCatalog);
        if (await db.ChartOfAccounts.AnyAsync(a => a.ParentId == cuenta.Id && !a.IsDeleted, ct)) return Result.Failure(AccountingErrors.AccountHasChildren);
        if (cuenta.FirstMovementAt is not null || await db.JournalEntries.AnyAsync(j => j.AccountId == cuenta.Id && !j.IsDeleted, ct))
            return Result.Failure(AccountingErrors.AccountHasMovements);

        var refs = await referencias.BuscarAsync(cuenta.Id, cuenta.Code, ct);
        if (refs.Count > 0) return Result.Failure(AccountingErrors.AccountReferenced(refs));

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        cuenta.IsDeleted = true;
        cuenta.DeletedAt = ahora;
        cuenta.DeletedBy = quien;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Account.Deleted", nameof(ChartOfAccount), cuenta.PublicId, CreateAccountCommandHandler.Instantanea(cuenta), null, ct);
        return Result.Success();
    }
}
