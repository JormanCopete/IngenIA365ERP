using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

// Franquicias, adquirentes, datáfonos de cobro y denominaciones (feature 012, I3, T591; contracts/api.md §22.2; FR-096, FR-099,
// FR-101). Cada una con GET /, GET /{id}, POST /, PUT /{id} y DELETE /{id}; el código no cambia después del alta y borrar sólo se
// puede sin uso (Core.CardNetwork.InUse, Core.CardAcquirer.InUse, Core.CardTerminal.InUse). Las reglas de alta las reusa la
// plantilla 11 (ReglasDeTarjetas). (nuevo)

/// <summary>Las reglas del alta de franquicias, adquirentes y datáfonos, comunes al alta una a una y a la plantilla 11. (nuevo)</summary>
public static class ReglasDeTarjetas
{
    public const int LargoDeNombre = 60;
    public const int LargoDeCodigoDeDatafono = 20;
    public const int LargoDeSerial = 40;

    public static async Task<Error?> RedDuplicadaAsync(IApplicationDbContext db, string codigo, CancellationToken ct)
    {
        var local = db.CardNetworks.Local.FirstOrDefault(n => n.Code == codigo && !n.IsDeleted);
        if (local is not null) return CodigoDeCatalogo.Duplicado("una franquicia", codigo, local.Name, local.PublicId);
        var otra = await db.CardNetworks.Where(n => n.Code == codigo && !n.IsDeleted).Select(n => new { n.PublicId, n.Name }).FirstOrDefaultAsync(ct);
        return otra is null ? null : CodigoDeCatalogo.Duplicado("una franquicia", codigo, otra.Name, otra.PublicId);
    }

    public static async Task<Error?> AdquirenteDuplicadoAsync(IApplicationDbContext db, string codigo, CancellationToken ct)
    {
        var local = db.CardAcquirers.Local.FirstOrDefault(a => a.Code == codigo && !a.IsDeleted);
        if (local is not null) return CodigoDeCatalogo.Duplicado("un adquirente", codigo, local.Name, local.PublicId);
        var otro = await db.CardAcquirers.Where(a => a.Code == codigo && !a.IsDeleted).Select(a => new { a.PublicId, a.Name }).FirstOrDefaultAsync(ct);
        return otro is null ? null : CodigoDeCatalogo.Duplicado("un adquirente", codigo, otro.Name, otro.PublicId);
    }

    /// <summary>El código del datáfono (el TER del voucher) es único por adquirente.</summary>
    public static async Task<Error?> DatafonoDuplicadoAsync(IApplicationDbContext db, CardAcquirer adquirente, string codigo, CancellationToken ct)
    {
        var local = db.CardTerminals.Local.FirstOrDefault(t => t.Code == codigo && !t.IsDeleted
            && (ReferenceEquals(t.CardAcquirer, adquirente) || adquirente.Id != 0 && t.CardAcquirerId == adquirente.Id));
        if (local is not null) return CodigoDeCatalogo.Duplicado($"un datáfono del adquirente {adquirente.Code}", codigo, local.Code, local.PublicId);
        if (adquirente.Id == 0) return null;
        var otro = await db.CardTerminals.Where(t => t.CardAcquirerId == adquirente.Id && t.Code == codigo && !t.IsDeleted)
            .Select(t => new { t.PublicId, t.Code }).FirstOrDefaultAsync(ct);
        return otro is null ? null : CodigoDeCatalogo.Duplicado($"un datáfono del adquirente {adquirente.Code}", codigo, otro.Code, otro.PublicId);
    }

    public static async Task<int> PagosDelDatafonoAsync(IApplicationDbContext db, int datafonoId, CancellationToken ct) =>
        datafonoId == 0 ? 0 : await db.DocumentPayments.CountAsync(p => p.CardTerminalId == datafonoId && !p.IsDeleted, ct);
}

// =================================================================================================== franquicias --

/// <summary>Alta de una franquicia (§22.2, <c>POST /api/core/card-networks</c>). (nuevo)</summary>
public sealed record CreateCardNetworkCommand(string Code, string Name, CardKind CardKind, bool IsActive = true)
    : IRequest<Result<CardNetworkDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCardNetworkCommandValidator : AbstractValidator<CreateCardNetworkCommand>
{
    public CreateCardNetworkCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeTarjetas.LargoDeNombre);
        RuleFor(x => x.CardKind).IsInEnum();
    }
}

public sealed class CreateCardNetworkCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCardNetworkCommand, Result<CardNetworkDto>>
{
    public async Task<Result<CardNetworkDto>> Handle(CreateCardNetworkCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await ReglasDeTarjetas.RedDuplicadaAsync(db, codigo, ct) is { } dup) return Result.Failure<CardNetworkDto>(dup);
        var red = new CardNetwork { Code = codigo, Name = request.Name.Trim(), CardKind = request.CardKind, IsActive = request.IsActive };
        db.CardNetworks.Add(red);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Red(red));
    }
}

/// <summary>Edición de una franquicia (§22.2, <c>PUT /{id}</c>): nombre, tipo y activa; el código no cambia. (nuevo)</summary>
public sealed record UpdateCardNetworkCommand(Guid CardNetworkPublicId, string Name, CardKind CardKind, bool IsActive)
    : IRequest<Result<CardNetworkDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCardNetworkCommandValidator : AbstractValidator<UpdateCardNetworkCommand>
{
    public UpdateCardNetworkCommandValidator()
    {
        RuleFor(x => x.CardNetworkPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeTarjetas.LargoDeNombre);
        RuleFor(x => x.CardKind).IsInEnum();
    }
}

public sealed class UpdateCardNetworkCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCardNetworkCommand, Result<CardNetworkDto>>
{
    public async Task<Result<CardNetworkDto>> Handle(UpdateCardNetworkCommand request, CancellationToken ct)
    {
        var red = await db.CardNetworks.FirstOrDefaultAsync(n => n.PublicId == request.CardNetworkPublicId && !n.IsDeleted, ct);
        if (red is null) return Result.Failure<CardNetworkDto>(PaymentMeansErrors.CardNetworkNotFound());
        red.Name = request.Name.Trim();
        red.CardKind = request.CardKind;
        red.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Red(red));
    }
}

/// <summary>Borrar una franquicia sin medios que la usen (§22.2); con uso, <c>Core.CardNetwork.InUse</c>. (nuevo)</summary>
public sealed record DeleteCardNetworkCommand(Guid CardNetworkPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteCardNetworkCommandValidator : AbstractValidator<DeleteCardNetworkCommand>
{
    public DeleteCardNetworkCommandValidator() => RuleFor(x => x.CardNetworkPublicId).NotEmpty();
}

public sealed class DeleteCardNetworkCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteCardNetworkCommand, Result>
{
    public async Task<Result> Handle(DeleteCardNetworkCommand request, CancellationToken ct)
    {
        var red = await db.CardNetworks.FirstOrDefaultAsync(n => n.PublicId == request.CardNetworkPublicId && !n.IsDeleted, ct);
        if (red is null) return Result.Failure(PaymentMeansErrors.CardNetworkNotFound());
        var medios = await db.PaymentMeans.CountAsync(m => m.CardNetworkId == red.Id && !m.IsDeleted, ct);
        if (medios > 0) return Result.Failure(PaymentMeansErrors.CardNetworkInUse(medios));
        red.IsDeleted = true;
        red.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// =================================================================================================== adquirentes --

/// <summary>Alta de un adquirente (§22.2, <c>POST /api/core/card-acquirers</c>), con el tercero de la cuenta por cobrar. (nuevo)</summary>
public sealed record CreateCardAcquirerCommand(string Code, string Name, Guid? PersonPublicId = null, bool IsActive = true)
    : IRequest<Result<CardAcquirerDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCardAcquirerCommandValidator : AbstractValidator<CreateCardAcquirerCommand>
{
    public CreateCardAcquirerCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeTarjetas.LargoDeNombre);
    }
}

public sealed class CreateCardAcquirerCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCardAcquirerCommand, Result<CardAcquirerDto>>
{
    public async Task<Result<CardAcquirerDto>> Handle(CreateCardAcquirerCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await ReglasDeTarjetas.AdquirenteDuplicadoAsync(db, codigo, ct) is { } dup) return Result.Failure<CardAcquirerDto>(dup);
        var persona = await PersonaVinculada.ResolverAsync(db, request.PersonPublicId, ct);
        if (persona.IsFailure) return Result.Failure<CardAcquirerDto>(persona.Error);
        var adquirente = new CardAcquirer { Code = codigo, Name = request.Name.Trim(), PersonId = persona.Value, IsActive = request.IsActive };
        db.CardAcquirers.Add(adquirente);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Adquirente(adquirente, request.PersonPublicId));
    }
}

/// <summary>Edición de un adquirente (§22.2, <c>PUT /{id}</c>): nombre, tercero y activo; el código no cambia. (nuevo)</summary>
public sealed record UpdateCardAcquirerCommand(Guid CardAcquirerPublicId, string Name, Guid? PersonPublicId, bool IsActive)
    : IRequest<Result<CardAcquirerDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCardAcquirerCommandValidator : AbstractValidator<UpdateCardAcquirerCommand>
{
    public UpdateCardAcquirerCommandValidator()
    {
        RuleFor(x => x.CardAcquirerPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeTarjetas.LargoDeNombre);
    }
}

public sealed class UpdateCardAcquirerCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCardAcquirerCommand, Result<CardAcquirerDto>>
{
    public async Task<Result<CardAcquirerDto>> Handle(UpdateCardAcquirerCommand request, CancellationToken ct)
    {
        var adquirente = await db.CardAcquirers.FirstOrDefaultAsync(a => a.PublicId == request.CardAcquirerPublicId && !a.IsDeleted, ct);
        if (adquirente is null) return Result.Failure<CardAcquirerDto>(PaymentMeansErrors.CardAcquirerNotFound());
        var persona = await PersonaVinculada.ResolverAsync(db, request.PersonPublicId, ct);
        if (persona.IsFailure) return Result.Failure<CardAcquirerDto>(persona.Error);
        adquirente.Name = request.Name.Trim();
        adquirente.PersonId = persona.Value;
        adquirente.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Adquirente(adquirente, request.PersonPublicId));
    }
}

/// <summary>Borrar un adquirente sin medios ni datáfonos (§22.2); con uso, <c>Core.CardAcquirer.InUse</c>. (nuevo)</summary>
public sealed record DeleteCardAcquirerCommand(Guid CardAcquirerPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteCardAcquirerCommandValidator : AbstractValidator<DeleteCardAcquirerCommand>
{
    public DeleteCardAcquirerCommandValidator() => RuleFor(x => x.CardAcquirerPublicId).NotEmpty();
}

public sealed class DeleteCardAcquirerCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteCardAcquirerCommand, Result>
{
    public async Task<Result> Handle(DeleteCardAcquirerCommand request, CancellationToken ct)
    {
        var adquirente = await db.CardAcquirers.FirstOrDefaultAsync(a => a.PublicId == request.CardAcquirerPublicId && !a.IsDeleted, ct);
        if (adquirente is null) return Result.Failure(PaymentMeansErrors.CardAcquirerNotFound());
        var medios = await db.PaymentMeans.CountAsync(m => m.CardAcquirerId == adquirente.Id && !m.IsDeleted, ct);
        var datafonos = await db.CardTerminals.CountAsync(t => t.CardAcquirerId == adquirente.Id && !t.IsDeleted, ct);
        if (medios > 0 || datafonos > 0) return Result.Failure(PaymentMeansErrors.CardAcquirerInUse(medios, datafonos));
        adquirente.IsDeleted = true;
        adquirente.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// =================================================================================================== datáfonos --

/// <summary>
/// Alta de un datáfono de cobro (§22.2, <c>POST /api/core/card-terminals</c>): ligado a su adquirente, código (TER) único en él.
/// No es <c>DEB_PosTerminals</c>. El de cada caja se propone desde <c>INV_CashRegisters.DefaultCardTerminalId</c>. (nuevo)
/// </summary>
public sealed record CreateCardTerminalCommand(string Code, Guid CardAcquirerPublicId, string? Serial = null, string? Description = null, bool IsActive = true)
    : IRequest<Result<CardTerminalDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCardTerminalCommandValidator : AbstractValidator<CreateCardTerminalCommand>
{
    public CreateCardTerminalCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(ReglasDeTarjetas.LargoDeCodigoDeDatafono).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.CardAcquirerPublicId).NotEmpty();
        RuleFor(x => x.Serial).MaximumLength(ReglasDeTarjetas.LargoDeSerial);
        RuleFor(x => x.Description).MaximumLength(80);
    }
}

public sealed class CreateCardTerminalCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCardTerminalCommand, Result<CardTerminalDto>>
{
    public async Task<Result<CardTerminalDto>> Handle(CreateCardTerminalCommand request, CancellationToken ct)
    {
        var adquirente = await db.CardAcquirers.FirstOrDefaultAsync(a => a.PublicId == request.CardAcquirerPublicId && !a.IsDeleted, ct);
        if (adquirente is null) return Result.Failure<CardTerminalDto>(PaymentMeansErrors.CardAcquirerNotFound());
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await ReglasDeTarjetas.DatafonoDuplicadoAsync(db, adquirente, codigo, ct) is { } dup) return Result.Failure<CardTerminalDto>(dup);
        var datafono = new CardTerminal
        {
            CardAcquirerId = adquirente.Id, CardAcquirer = adquirente, Code = codigo,
            Serial = string.IsNullOrWhiteSpace(request.Serial) ? null : request.Serial.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
        };
        db.CardTerminals.Add(datafono);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Datafono(datafono, adquirente));
    }
}

/// <summary>
/// Edición de un datáfono (§22.2, <c>PUT /{id}</c>): serial, descripción, activo y adquirente; con pagos no cambia de adquirente
/// (<c>Core.CardTerminal.InUse</c>: sus vouchers ya se liquidaron con ése). El código no cambia. (nuevo)
/// </summary>
public sealed record UpdateCardTerminalCommand(Guid CardTerminalPublicId, Guid CardAcquirerPublicId, string? Serial, string? Description, bool IsActive)
    : IRequest<Result<CardTerminalDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCardTerminalCommandValidator : AbstractValidator<UpdateCardTerminalCommand>
{
    public UpdateCardTerminalCommandValidator()
    {
        RuleFor(x => x.CardTerminalPublicId).NotEmpty();
        RuleFor(x => x.CardAcquirerPublicId).NotEmpty();
        RuleFor(x => x.Serial).MaximumLength(ReglasDeTarjetas.LargoDeSerial);
        RuleFor(x => x.Description).MaximumLength(80);
    }
}

public sealed class UpdateCardTerminalCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCardTerminalCommand, Result<CardTerminalDto>>
{
    public async Task<Result<CardTerminalDto>> Handle(UpdateCardTerminalCommand request, CancellationToken ct)
    {
        var datafono = await db.CardTerminals.FirstOrDefaultAsync(t => t.PublicId == request.CardTerminalPublicId && !t.IsDeleted, ct);
        if (datafono is null) return Result.Failure<CardTerminalDto>(PaymentMeansErrors.CardTerminalNotFound());
        var adquirente = await db.CardAcquirers.FirstOrDefaultAsync(a => a.PublicId == request.CardAcquirerPublicId && !a.IsDeleted, ct);
        if (adquirente is null) return Result.Failure<CardTerminalDto>(PaymentMeansErrors.CardAcquirerNotFound());
        if (adquirente.Id != datafono.CardAcquirerId)
        {
            var pagos = await ReglasDeTarjetas.PagosDelDatafonoAsync(db, datafono.Id, ct);
            if (pagos > 0) return Result.Failure<CardTerminalDto>(PaymentMeansErrors.CardTerminalInUse(pagos, 0));
            if (await ReglasDeTarjetas.DatafonoDuplicadoAsync(db, adquirente, datafono.Code, ct) is { } dup) return Result.Failure<CardTerminalDto>(dup);
        }
        datafono.CardAcquirerId = adquirente.Id;
        datafono.CardAcquirer = adquirente;
        datafono.Serial = string.IsNullOrWhiteSpace(request.Serial) ? null : request.Serial.Trim();
        datafono.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        datafono.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Datafono(datafono, adquirente));
    }
}

/// <summary>
/// Borrar un datáfono sin pagos, sin arqueos y que ninguna caja proponga (§22.2); con uso, <c>Core.CardTerminal.InUse</c>. (nuevo)
/// </summary>
public sealed record DeleteCardTerminalCommand(Guid CardTerminalPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteCardTerminalCommandValidator : AbstractValidator<DeleteCardTerminalCommand>
{
    public DeleteCardTerminalCommandValidator() => RuleFor(x => x.CardTerminalPublicId).NotEmpty();
}

public sealed class DeleteCardTerminalCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteCardTerminalCommand, Result>
{
    public async Task<Result> Handle(DeleteCardTerminalCommand request, CancellationToken ct)
    {
        var datafono = await db.CardTerminals.FirstOrDefaultAsync(t => t.PublicId == request.CardTerminalPublicId && !t.IsDeleted, ct);
        if (datafono is null) return Result.Failure(PaymentMeansErrors.CardTerminalNotFound());
        var pagos = await ReglasDeTarjetas.PagosDelDatafonoAsync(db, datafono.Id, ct)
            + await db.CashCountTerminalBatches.CountAsync(b => b.CardTerminalId == datafono.Id && !b.IsDeleted, ct);
        var cajas = await db.CashRegisters.CountAsync(c => c.DefaultCardTerminalId == datafono.Id && !c.IsDeleted, ct);
        if (pagos > 0 || cajas > 0) return Result.Failure(PaymentMeansErrors.CardTerminalInUse(pagos, cajas));
        datafono.IsDeleted = true;
        datafono.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// =================================================================================================== denominaciones --

/// <summary>
/// Alta de un billete o moneda (§22.2, <c>POST /api/core/cash-denominations</c>; FR-099). Con <see cref="RetiresPublicId"/>, la
/// denominación que sale de circulación cierra su vigencia la víspera de <see cref="ValidFrom"/>: se agregan billetes nuevos y se
/// cierra la vigencia de los retirados, sin borrar nada (los arqueos ya hechos guardan su valor). Único
/// <c>(moneda, clase, valor)</c> entre vivos. (nuevo)
/// </summary>
public sealed record CreateCashDenominationCommand(CashDenominationKind Kind, decimal Value, DateOnly ValidFrom, DateOnly? ValidTo = null, short DisplayOrder = 0, Guid? RetiresPublicId = null)
    : IRequest<Result<CashDenominationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCashDenominationCommandValidator : AbstractValidator<CreateCashDenominationCommand>
{
    public CreateCashDenominationCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null).WithMessage("La vigencia termina antes de empezar.");
    }
}

public sealed class CreateCashDenominationCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCashDenominationCommand, Result<CashDenominationDto>>
{
    public async Task<Result<CashDenominationDto>> Handle(CreateCashDenominationCommand request, CancellationToken ct)
    {
        var existente = await db.CashDenominations.FirstOrDefaultAsync(d => d.Currency == CashDenomination.MonedaPorDefecto
            && d.Kind == request.Kind && d.Value == request.Value && !d.IsDeleted, ct);
        if (existente is not null)
            return Result.Failure<CashDenominationDto>(CodigoDeCatalogo.Duplicado("una denominación", $"{request.Value:0.##}", $"{existente.Kind} {existente.Value:0.##}", existente.PublicId));

        if (request.RetiresPublicId is { } retiradaId)
        {
            var retirada = await db.CashDenominations.FirstOrDefaultAsync(d => d.PublicId == retiradaId && !d.IsDeleted, ct);
            if (retirada is null) return Result.Failure<CashDenominationDto>(PaymentMeansErrors.CashDenominationNotFound());
            var vispera = request.ValidFrom.AddDays(-1);
            if (vispera < retirada.ValidFrom)
                return Result.Failure<CashDenominationDto>("Validation.Invalid", "La denominación retirada no puede cerrar su vigencia antes de empezarla.");
            retirada.ValidTo = vispera;
        }

        var nueva = new CashDenomination
        {
            Currency = CashDenomination.MonedaPorDefecto, Kind = request.Kind, Value = request.Value, DisplayOrder = request.DisplayOrder,
            IsActive = true, ValidFrom = request.ValidFrom, ValidTo = request.ValidTo,
        };
        db.CashDenominations.Add(nueva);
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Denominacion(nueva));
    }
}

/// <summary>Edición de una denominación (§22.2, <c>PUT /{id}</c>): orden, activa y fin de vigencia; el valor no cambia. (nuevo)</summary>
public sealed record UpdateCashDenominationCommand(Guid CashDenominationPublicId, short DisplayOrder, bool IsActive, DateOnly? ValidTo)
    : IRequest<Result<CashDenominationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateCashDenominationCommandValidator : AbstractValidator<UpdateCashDenominationCommand>
{
    public UpdateCashDenominationCommandValidator() => RuleFor(x => x.CashDenominationPublicId).NotEmpty();
}

public sealed class UpdateCashDenominationCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCashDenominationCommand, Result<CashDenominationDto>>
{
    public async Task<Result<CashDenominationDto>> Handle(UpdateCashDenominationCommand request, CancellationToken ct)
    {
        var d = await db.CashDenominations.FirstOrDefaultAsync(x => x.PublicId == request.CashDenominationPublicId && !x.IsDeleted, ct);
        if (d is null) return Result.Failure<CashDenominationDto>(PaymentMeansErrors.CashDenominationNotFound());
        if (request.ValidTo is { } hasta && hasta < d.ValidFrom)
            return Result.Failure<CashDenominationDto>("Validation.Invalid", "La vigencia termina antes de empezar.");
        d.DisplayOrder = request.DisplayOrder;
        d.IsActive = request.IsActive;
        d.ValidTo = request.ValidTo;
        await db.SaveChangesAsync(ct);
        return Result.Success(VistaDeTarjetas.Denominacion(d));
    }
}

/// <summary>Borrar una denominación que ningún arqueo contó (§22.2); con uso, <c>Core.CashDenomination.InUse</c>. (nuevo)</summary>
public sealed record DeleteCashDenominationCommand(Guid CashDenominationPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteCashDenominationCommandValidator : AbstractValidator<DeleteCashDenominationCommand>
{
    public DeleteCashDenominationCommandValidator() => RuleFor(x => x.CashDenominationPublicId).NotEmpty();
}

public sealed class DeleteCashDenominationCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteCashDenominationCommand, Result>
{
    public async Task<Result> Handle(DeleteCashDenominationCommand request, CancellationToken ct)
    {
        var d = await db.CashDenominations.FirstOrDefaultAsync(x => x.PublicId == request.CashDenominationPublicId && !x.IsDeleted, ct);
        if (d is null) return Result.Failure(PaymentMeansErrors.CashDenominationNotFound());
        var conteos = await db.CashCountDenominations.CountAsync(c => c.CashDenominationId == d.Id && !c.IsDeleted, ct);
        if (conteos > 0) return Result.Failure(PaymentMeansErrors.CashDenominationInUse(conteos));
        d.IsDeleted = true;
        d.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// =================================================================================================== consultas --

/// <summary>Las franquicias (§22.2, <c>GET /api/core/card-networks?active=</c>). (nuevo)</summary>
public sealed record ListCardNetworksQuery(bool? Active = null) : IRequest<Result<IReadOnlyList<CardNetworkDto>>>;

public sealed class ListCardNetworksQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCardNetworksQuery, Result<IReadOnlyList<CardNetworkDto>>>
{
    public async Task<Result<IReadOnlyList<CardNetworkDto>>> Handle(ListCardNetworksQuery request, CancellationToken ct)
    {
        var q = db.CardNetworks.AsNoTracking().Where(n => !n.IsDeleted);
        if (request.Active is { } a) q = q.Where(n => n.IsActive == a);
        return Result.Success<IReadOnlyList<CardNetworkDto>>((await q.OrderBy(n => n.Code).ToListAsync(ct)).Select(VistaDeTarjetas.Red).ToList());
    }
}

/// <summary>Una franquicia (§22.2, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetCardNetworkQuery(Guid CardNetworkPublicId) : IRequest<Result<CardNetworkDto>>;

public sealed class GetCardNetworkQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCardNetworkQuery, Result<CardNetworkDto>>
{
    public async Task<Result<CardNetworkDto>> Handle(GetCardNetworkQuery request, CancellationToken ct)
    {
        var red = await db.CardNetworks.AsNoTracking().FirstOrDefaultAsync(n => n.PublicId == request.CardNetworkPublicId && !n.IsDeleted, ct);
        return red is null ? Result.Failure<CardNetworkDto>(PaymentMeansErrors.CardNetworkNotFound()) : Result.Success(VistaDeTarjetas.Red(red));
    }
}

/// <summary>Los adquirentes (§22.2, <c>GET /api/core/card-acquirers?active=</c>). (nuevo)</summary>
public sealed record ListCardAcquirersQuery(bool? Active = null) : IRequest<Result<IReadOnlyList<CardAcquirerDto>>>;

public sealed class ListCardAcquirersQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCardAcquirersQuery, Result<IReadOnlyList<CardAcquirerDto>>>
{
    public async Task<Result<IReadOnlyList<CardAcquirerDto>>> Handle(ListCardAcquirersQuery request, CancellationToken ct)
    {
        var q = db.CardAcquirers.AsNoTracking().Where(a => !a.IsDeleted);
        if (request.Active is { } a) q = q.Where(x => x.IsActive == a);
        var lista = await q.OrderBy(x => x.Code).ToListAsync(ct);
        var personas = await VistaDeTarjetas.PersonasAsync(db, lista.Select(x => x.PersonId), ct);
        return Result.Success<IReadOnlyList<CardAcquirerDto>>(lista
            .Select(x => VistaDeTarjetas.Adquirente(x, x.PersonId is { } p && personas.TryGetValue(p, out var g) ? g : null)).ToList());
    }
}

/// <summary>Un adquirente (§22.2, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetCardAcquirerQuery(Guid CardAcquirerPublicId) : IRequest<Result<CardAcquirerDto>>;

public sealed class GetCardAcquirerQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCardAcquirerQuery, Result<CardAcquirerDto>>
{
    public async Task<Result<CardAcquirerDto>> Handle(GetCardAcquirerQuery request, CancellationToken ct)
    {
        var x = await db.CardAcquirers.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.CardAcquirerPublicId && !a.IsDeleted, ct);
        if (x is null) return Result.Failure<CardAcquirerDto>(PaymentMeansErrors.CardAcquirerNotFound());
        var personas = await VistaDeTarjetas.PersonasAsync(db, [x.PersonId], ct);
        return Result.Success(VistaDeTarjetas.Adquirente(x, x.PersonId is { } p && personas.TryGetValue(p, out var g) ? g : null));
    }
}

/// <summary>Los datáfonos de cobro (§22.2, <c>GET /api/core/card-terminals?acquirer=&amp;active=</c>). (nuevo)</summary>
public sealed record ListCardTerminalsQuery(Guid? CardAcquirerPublicId = null, bool? Active = null) : IRequest<Result<IReadOnlyList<CardTerminalDto>>>;

public sealed class ListCardTerminalsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCardTerminalsQuery, Result<IReadOnlyList<CardTerminalDto>>>
{
    public async Task<Result<IReadOnlyList<CardTerminalDto>>> Handle(ListCardTerminalsQuery request, CancellationToken ct)
    {
        var q = db.CardTerminals.AsNoTracking().Include(t => t.CardAcquirer).Where(t => !t.IsDeleted);
        if (request.CardAcquirerPublicId is { } adq) q = q.Where(t => t.CardAcquirer!.PublicId == adq);
        if (request.Active is { } a) q = q.Where(t => t.IsActive == a);
        var lista = await q.OrderBy(t => t.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<CardTerminalDto>>(lista.Select(t => VistaDeTarjetas.Datafono(t, t.CardAcquirer!)).ToList());
    }
}

/// <summary>Un datáfono (§22.2, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetCardTerminalQuery(Guid CardTerminalPublicId) : IRequest<Result<CardTerminalDto>>;

public sealed class GetCardTerminalQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCardTerminalQuery, Result<CardTerminalDto>>
{
    public async Task<Result<CardTerminalDto>> Handle(GetCardTerminalQuery request, CancellationToken ct)
    {
        var t = await db.CardTerminals.AsNoTracking().Include(x => x.CardAcquirer).FirstOrDefaultAsync(x => x.PublicId == request.CardTerminalPublicId && !x.IsDeleted, ct);
        return t is null ? Result.Failure<CardTerminalDto>(PaymentMeansErrors.CardTerminalNotFound()) : Result.Success(VistaDeTarjetas.Datafono(t, t.CardAcquirer!));
    }
}

/// <summary>Las denominaciones (§22.2, <c>GET /api/core/cash-denominations?asOf=</c>): con fecha, sólo las vigentes. (nuevo)</summary>
public sealed record ListCashDenominationsQuery(DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<CashDenominationDto>>>;

public sealed class ListCashDenominationsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCashDenominationsQuery, Result<IReadOnlyList<CashDenominationDto>>>
{
    public async Task<Result<IReadOnlyList<CashDenominationDto>>> Handle(ListCashDenominationsQuery request, CancellationToken ct)
    {
        var q = db.CashDenominations.AsNoTracking().Where(d => !d.IsDeleted);
        if (request.AsOf is { } f) q = q.Where(d => d.IsActive && d.ValidFrom <= f && (d.ValidTo == null || d.ValidTo >= f));
        var lista = await q.OrderBy(d => d.Kind).ThenByDescending(d => d.Value).ToListAsync(ct);
        return Result.Success<IReadOnlyList<CashDenominationDto>>(lista.Select(VistaDeTarjetas.Denominacion).ToList());
    }
}

/// <summary>Una denominación (§22.2, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetCashDenominationQuery(Guid CashDenominationPublicId) : IRequest<Result<CashDenominationDto>>;

public sealed class GetCashDenominationQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCashDenominationQuery, Result<CashDenominationDto>>
{
    public async Task<Result<CashDenominationDto>> Handle(GetCashDenominationQuery request, CancellationToken ct)
    {
        var d = await db.CashDenominations.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.CashDenominationPublicId && !x.IsDeleted, ct);
        return d is null ? Result.Failure<CashDenominationDto>(PaymentMeansErrors.CashDenominationNotFound()) : Result.Success(VistaDeTarjetas.Denominacion(d));
    }
}

/// <summary>Los DTO de franquicias, adquirentes, datáfonos y denominaciones. (nuevo)</summary>
public static class VistaDeTarjetas
{
    public static CardNetworkDto Red(CardNetwork n) => new(n.PublicId, n.Code, n.Name, n.CardKind, n.IsActive);

    public static CardAcquirerDto Adquirente(CardAcquirer a, Guid? persona) => new(a.PublicId, a.Code, a.Name, persona, a.IsActive);

    public static CardTerminalDto Datafono(CardTerminal t, CardAcquirer a) => new(t.PublicId, t.Code, a.PublicId, a.Code, t.Serial, t.Description, t.IsActive);

    public static CashDenominationDto Denominacion(CashDenomination d) => new(d.PublicId, d.Currency, d.Kind, d.Value, d.DisplayOrder, d.IsActive, d.ValidFrom, d.ValidTo);

    public static async Task<IReadOnlyDictionary<int, Guid>> PersonasAsync(IApplicationDbContext db, IEnumerable<int?> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<int, Guid>();
        return await db.People.AsNoTracking().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
    }
}
