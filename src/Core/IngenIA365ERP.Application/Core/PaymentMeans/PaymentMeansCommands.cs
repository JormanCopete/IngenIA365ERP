using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MedioDePago = IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

/// <summary>Las reglas de forma del cuerpo de un medio (400 <c>Validation.Invalid</c>), comunes al alta y a la edición. (nuevo)</summary>
public sealed class PaymentMeansInputValidator : AbstractValidator<PaymentMeansInput>
{
    public PaymentMeansInputValidator(bool conCodigo)
    {
        if (conCodigo)
            RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
                .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeMedioDePago.LargoDeNombre);
        RuleFor(x => x.Class).IsInEnum();
        RuleFor(x => x.QuickKey).Matches("^[A-Za-z0-9]$").When(x => !string.IsNullOrWhiteSpace(x.QuickKey))
            .WithMessage("La tecla rápida es una letra o un dígito.");
        RuleFor(x => x.DestinationAccountNumber).MaximumLength(ReglasDeMedioDePago.LargoDeCuenta);
        RuleFor(x => x.DestinationAccountType).InclusiveBetween((byte)1, (byte)2).When(x => x.DestinationAccountType is not null)
            .WithMessage("El tipo de cuenta es 1 (ahorros) o 2 (corriente).");
        RuleFor(x => x.ReferenceKind).IsInEnum().When(x => x.ReferenceKind is not null);
        RuleFor(x => x.CountMethod).IsInEnum().When(x => x.CountMethod is not null);
        RuleFor(x => x.DianPaymentMeansCode).NotEmpty().MaximumLength(3);
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(ReglasDeMedioDePago.LargoDeNotas);
        RuleFor(x => x.CreditDefaults!.SuggestedLineCode).MaximumLength(ReglasDeMedioDePago.LargoDeLinea).When(x => x.CreditDefaults is not null);
    }
}

/// <summary>
/// Alta de un medio de pago (feature 012, I3, T590; contracts/api.md §22.1, <c>POST /api/core/payment-means</c> → 201
/// <c>{ paymentMeansPublicId }</c>; FR-096). La regla es <see cref="ReglasDeMedioDePago.AplicarAsync"/>. (nuevo)
/// </summary>
public sealed record CreatePaymentMeansCommand(PaymentMeansInput Means) : IRequest<Result<Guid>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePaymentMeansCommandValidator : AbstractValidator<CreatePaymentMeansCommand>
{
    public CreatePaymentMeansCommandValidator() =>
        RuleFor(x => x.Means).NotNull().SetValidator(new PaymentMeansInputValidator(conCodigo: true));
}

public sealed class CreatePaymentMeansCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<CreatePaymentMeansCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePaymentMeansCommand request, CancellationToken ct)
    {
        var datos = await ReferenciasDelMedio.ResolverAsync(db, request.Means, ct);
        if (datos.IsFailure) return Result.Failure<Guid>(datos.Error);
        var medio = await ReglasDeMedioDePago.AplicarAsync(db, null, datos.Value, reloj.HoyLocal, ct);
        if (medio.IsFailure) return Result.Failure<Guid>(medio.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(medio.Value.PublicId);
    }
}

/// <summary>
/// Edición de un medio (T590; §22.1, <c>PUT /{id}</c>) con motivo obligatorio (<c>IConMotivo</c>: la tolerancia y la comisión
/// esperada son parámetros del medio, FR-012, FR-096; el historial es el diff de auditoría con su motivo). El código no cambia;
/// con pagos, tampoco la clase, la red, el adquirente ni el arqueo (<c>Core.PaymentMeans.InUse</c>). (nuevo)
/// </summary>
public sealed record UpdatePaymentMeansCommand(Guid PaymentMeansPublicId, PaymentMeansInput Means, string Reason)
    : IRequest<Result<PaymentMeansDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdatePaymentMeansCommandValidator : ValidadorConMotivo<UpdatePaymentMeansCommand>
{
    public UpdatePaymentMeansCommandValidator()
    {
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
        RuleFor(x => x.Means).NotNull().SetValidator(new PaymentMeansInputValidator(conCodigo: false));
    }
}

public sealed class UpdatePaymentMeansCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<UpdatePaymentMeansCommand, Result<PaymentMeansDto>>
{
    public async Task<Result<PaymentMeansDto>> Handle(UpdatePaymentMeansCommand request, CancellationToken ct)
    {
        var medio = await db.PaymentMeans.FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<PaymentMeansDto>(PaymentMeansErrors.NotFound());
        var datos = await ReferenciasDelMedio.ResolverAsync(db, request.Means, ct);
        if (datos.IsFailure) return Result.Failure<PaymentMeansDto>(datos.Error);
        var r = await ReglasDeMedioDePago.AplicarAsync(db, medio, datos.Value, reloj.HoyLocal, ct);
        if (r.IsFailure) return Result.Failure<PaymentMeansDto>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeMediosDePago.UnoAsync(db, medio, detalle: false, ct));
    }
}

/// <summary>
/// Retiro suave de un medio <b>sin</b> pagos (T590; §22.1, <c>DELETE /{id}</c>); con pagos, <c>Core.PaymentMeans.InUse</c> y
/// se inactiva. Se lleva sus conjuntos de disponibilidad. (nuevo)
/// </summary>
public sealed record DeletePaymentMeansCommand(Guid PaymentMeansPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeletePaymentMeansCommandValidator : AbstractValidator<DeletePaymentMeansCommand>
{
    public DeletePaymentMeansCommandValidator() => RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
}

public sealed class DeletePaymentMeansCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<DeletePaymentMeansCommand, Result>
{
    public async Task<Result> Handle(DeletePaymentMeansCommand request, CancellationToken ct)
    {
        var medio = await db.PaymentMeans.FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure(PaymentMeansErrors.NotFound());
        var pagos = await ReglasDeMedioDePago.PagosAsync(db, medio.Id, ct);
        if (pagos > 0) return Result.Failure(PaymentMeansErrors.InUse(pagos));

        var ahora = reloj.UtcNow;
        medio.IsDeleted = true;
        medio.DeletedAt = ahora;
        foreach (var x in await db.PaymentMeansPointsOfSale.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).ToListAsync(ct)) { x.IsDeleted = true; x.DeletedAt = ahora; }
        foreach (var x in await db.PaymentMeansChannels.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).ToListAsync(ct)) { x.IsDeleted = true; x.DeletedAt = ahora; }
        foreach (var x in await db.PaymentMeansDocumentTypes.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).ToListAsync(ct)) { x.IsDeleted = true; x.DeletedAt = ahora; }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Resuelve los <c>PublicId</c> que cita el cuerpo de un medio (franquicia, adquirente, banco). (nuevo)</summary>
public static class ReferenciasDelMedio
{
    public static async Task<Result<DatosDeMedio>> ResolverAsync(IApplicationDbContext db, PaymentMeansInput input, CancellationToken ct)
    {
        CardNetwork? red = null;
        if (input.CardNetworkPublicId is { } redId)
        {
            red = await db.CardNetworks.FirstOrDefaultAsync(n => n.PublicId == redId && !n.IsDeleted, ct);
            if (red is null) return Result.Failure<DatosDeMedio>(PaymentMeansErrors.CardNetworkNotFound());
        }
        CardAcquirer? adquirente = null;
        if (input.CardAcquirerPublicId is { } adqId)
        {
            adquirente = await db.CardAcquirers.FirstOrDefaultAsync(a => a.PublicId == adqId && !a.IsDeleted, ct);
            if (adquirente is null) return Result.Failure<DatosDeMedio>(PaymentMeansErrors.CardAcquirerNotFound());
        }
        int? banco = null;
        if (input.BankPublicId is { } bancoId)
        {
            banco = await db.Banks.Where(b => b.PublicId == bancoId && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (banco is null) return Result.Failure<DatosDeMedio>(PaymentMeansErrors.BankNotFound());
        }
        return Result.Success(new DatosDeMedio(input, red, adquirente, banco));
    }
}
