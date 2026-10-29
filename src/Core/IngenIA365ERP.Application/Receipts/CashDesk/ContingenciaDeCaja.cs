using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>El estado de un talonario de contingencia: lo digitado, lo que falta por digitar (por debajo del último digitado) y lo que queda sin usar. (nuevo)</summary>
public sealed record ContingencyBookStatusDto(Guid PublicId, Guid CollectionPointPublicId, int FromNumber, int ToNumber, IReadOnlyList<int> Typed, IReadOnlyList<int> Pending,
    int Unused, ContingencyBookState Status);

/// <summary>
/// <c>POST /api/receipts/contingency-receipts</c> (<c>Receipts.Receipt.RegisterContingency</c>, con clave; FR-186; R-07; interfaces.md §1.1): el recibo de contingencia del
/// talonario se digita después, sin operación de caja: nace del comando con el canal <c>Contingency</c>, su número del talonario además del consecutivo, la imagen
/// obligatoria (<c>ReceiptsContingencyImage</c>) y la fecha valor del talonario, que tiene que caber en la ventana del contrato (sin correrla: si el contrato no la admite,
/// se rechaza). Se aplica en forma <c>NonInteractive</c> con la modalidad que eligió el cajero en el talonario. El efectivo no entra a una sesión (el supervisor lo cuadra
/// con el talonario). (nuevo)
/// </summary>
public sealed record RegisterContingencyReceiptCommand(
    Guid ContingencyBookPublicId,
    int ContingencyNumber,
    Guid PayerPersonPublicId,
    DateOnly ValueDate,
    IReadOnlyList<ParteDeAbono> Parts,
    Guid ImageAttachmentPublicId,
    PaymentMeansClass MeansClass = PaymentMeansClass.Cash,
    Guid? DeliveredByPersonPublicId = null,
    IReadOnlyList<ModalidadDePago?>? LoanModalities = null) : IRequest<Result<ReceiptDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterContingencyReceiptCommandValidator : AbstractValidator<RegisterContingencyReceiptCommand>
{
    public RegisterContingencyReceiptCommandValidator()
    {
        RuleFor(x => x.ContingencyBookPublicId).NotEmpty();
        RuleFor(x => x.ContingencyNumber).GreaterThan(0);
        RuleFor(x => x.PayerPersonPublicId).NotEmpty();
        RuleFor(x => x.Parts).NotEmpty().WithMessage("Indique a qué productos va el recibo del talonario.");
        RuleForEach(x => x.Parts).ChildRules(p => p.RuleFor(x => x.Amount).GreaterThan(0m));
        RuleFor(x => x.ImageAttachmentPublicId).NotEmpty().WithMessage("Adjunte la imagen del recibo del talonario.");
        RuleFor(x => x.MeansClass).Must(m => m is PaymentMeansClass.Cash or PaymentMeansClass.Transfer or PaymentMeansClass.BankDeposit)
            .WithMessage("En contingencia el medio es efectivo, transferencia o consignación.");
    }
}

public sealed class RegisterContingencyReceiptCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    AplicacionNoInteractiva aplicacion,
    IDateTimeService reloj) : IRequestHandler<RegisterContingencyReceiptCommand, Result<ReceiptDto>>
{
    public async Task<Result<ReceiptDto>> Handle(RegisterContingencyReceiptCommand request, CancellationToken ct)
    {
        var talonario = await db.ContingencyBooks.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.ContingencyBookPublicId, ct);
        if (talonario is null || talonario.Status != ContingencyBookState.Active || !(await alcance.ObtenerAsync(ct)).IncluyePunto(talonario.CollectionPointId))
            return Result.Failure<ReceiptDto>(ErroresDeRecaudo.TalonarioNoUsable());
        if (!talonario.Incluye(request.ContingencyNumber)) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.NumeroFueraDelTalonario(request.ContingencyNumber));
        if (await db.IssuedReceipts.AsNoTracking().AnyAsync(r => r.ContingencyBookId == talonario.Id && r.ContingencyNumber == request.ContingencyNumber, ct))
            return Result.Failure<ReceiptDto>(ErroresDeRecaudo.NumeroDeTalonarioUsado(request.ContingencyNumber));
        if (!await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.ImageAttachmentPublicId, ct))
            return Result.Failure<ReceiptDto>(ErroresDeRecaudo.ImagenDeContingenciaRequerida());
        if (request.ValueDate > reloj.HoyLocal) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.MovimientoInvalido("La fecha valor no puede ser futura."));
        var oficina = await db.CollectionPoints.AsNoTracking().Where(p => p.Id == talonario.CollectionPointId).Select(p => p.BranchId).FirstAsync(ct);
        int? quien = request.DeliveredByPersonPublicId is { } q
            ? await db.People.AsNoTracking().Where(p => p.PublicId == q).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;

        var clave = string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:contingencia:{talonario.PublicId:N}:{request.ContingencyNumber}");
        var partes = request.Parts.Select((p, i) => new ParteNoInteractiva(p.Target, p.ProductPublicId, p.BeneficiaryPersonPublicId, p.Amount, p.MiscConceptPublicId,
            p.CrossDocument, request.LoanModalities is { } m && i < m.Count ? m[i] : null, InstallmentPublicIds: p.InstallmentPublicIds)).ToList();
        var r = await aplicacion.AplicarAsync(new LineaNoInteractiva(clave, ReceiptChannel.Contingency, request.PayerPersonPublicId, request.ValueDate, partes.Sum(p => p.Amount),
            oficina, partes, request.MeansClass switch
            {
                PaymentMeansClass.Transfer => ClaseDeMedioDeRecaudo.Transfer,
                PaymentMeansClass.BankDeposit => ClaseDeMedioDeRecaudo.BankDeposit,
                _ => ClaseDeMedioDeRecaudo.Cash,
            }, request.MeansClass, AllowLateValueDate: false, ContingencyBookId: talonario.Id, ContingencyNumber: request.ContingencyNumber,
            ContingencyImageAttachmentPublicId: request.ImageAttachmentPublicId, CollectionPointId: talonario.CollectionPointId, DeliveredByPersonId: quien), ct);
        if (r.Outcome == ProcessingLineOutcome.Rejected || r.Receipt is null)
            return Result.Failure<ReceiptDto>(new Error(r.Code ?? "Receipts.Contingency.Rejected", r.Message ?? "El contrato rechazó el recibo de contingencia."));
        await db.SaveChangesAsync(ct);
        return Result.Success(await RecibosDeCaja.DtoAsync(db, r.Receipt, ct));
    }
}

/// <summary>
/// <c>GET /api/receipts/contingency-books/{id}/status</c> (<c>Receipts.Receipt.RegisterContingency</c>): el informe del talonario: los números digitados, los que faltan
/// por digitar y cuántos quedan sin usar. (nuevo)
/// </summary>
public sealed record GetContingencyBookStatusQuery(Guid ContingencyBookPublicId) : IRequest<Result<ContingencyBookStatusDto>>;

public sealed class GetContingencyBookStatusQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetContingencyBookStatusQuery, Result<ContingencyBookStatusDto>>
{
    public async Task<Result<ContingencyBookStatusDto>> Handle(GetContingencyBookStatusQuery request, CancellationToken ct)
    {
        var t = await db.ContingencyBooks.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == request.ContingencyBookPublicId, ct);
        if (t is null || !(await alcance.ObtenerAsync(ct)).IncluyePunto(t.CollectionPointId)) return Result.Failure<ContingencyBookStatusDto>(ErroresDeRecaudo.TalonarioNoUsable());
        var usados = (await db.IssuedReceipts.AsNoTracking().Where(r => r.ContingencyBookId == t.Id && r.ContingencyNumber != null)
            .Select(r => r.ContingencyNumber!.Value).ToListAsync(ct)).Order().ToList();
        var maximo = usados.Count == 0 ? t.FromNumber - 1 : usados.Max();
        var pendientes = usados.Count == 0 ? [] : Enumerable.Range(t.FromNumber, maximo - t.FromNumber + 1).Except(usados).ToList();
        var punto = await db.CollectionPoints.AsNoTracking().Where(p => p.Id == t.CollectionPointId).Select(p => p.PublicId).FirstAsync(ct);
        return Result.Success(new ContingencyBookStatusDto(t.PublicId, punto, t.FromNumber, t.ToNumber, usados, pendientes, t.ToNumber - maximo, t.Status));
    }
}
