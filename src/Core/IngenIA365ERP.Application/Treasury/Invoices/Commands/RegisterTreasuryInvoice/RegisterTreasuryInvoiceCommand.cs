using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Invoices.Commands.RegisterTreasuryInvoice;

public record RegisterTreasuryInvoiceCommand : IRequest<Result<Guid>>
{
    public string InvoiceType { get; init; } = "CxP"; // CxP or CxC
    public Guid PersonPublicId { get; init; }
    public decimal Amount { get; init; }
    public DateOnly DueDate { get; init; }
    public Guid ConceptPublicId { get; init; }
    public string? Description { get; init; }
}

public class RegisterTreasuryInvoiceCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RegisterTreasuryInvoiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegisterTreasuryInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Person
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Persona no encontrada."));

        // 2. Resolve Concept
        var concept = await context.TreasuryConcepts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.ConceptPublicId && !c.IsDeleted, cancellationToken);

        if (concept is null)
            return Result.Failure<Guid>(new Error("TreasuryConcept.NotFound",
                "Concepto de tesoreria no encontrado."));

        // 3. Get consecutive number
        var lastConsec = await context.TreasuryInvoices
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.ConsecutiveNumber)
            .Select(i => i.ConsecutiveNumber)
            .FirstOrDefaultAsync(cancellationToken);

        // 4. Create TreasuryInvoice
        var today = DateOnly.FromDateTime(dateTime.UtcNow);
        var invoice = new TreasuryInvoice
        {
            ConceptCode = concept.ConceptCode,
            ConsecutiveNumber = lastConsec + 1,
            EntryDate = today,
            InvoiceNumber = (lastConsec + 1).ToString(),
            InvoiceDate = today,
            PersonId = person.Id,
            DueDate = request.DueDate,
            Description = request.Description,
            Amount = request.Amount,
            Status = "P", // Pendiente
            DocumentType = request.InvoiceType == "CxC" ? "CXC" : "CXP",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.TreasuryInvoices.Add(invoice);

        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(invoice.PublicId);
    }
}

public class RegisterTreasuryInvoiceCommandValidator : AbstractValidator<RegisterTreasuryInvoiceCommand>
{
    public RegisterTreasuryInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceType)
            .NotEmpty().WithMessage("El tipo de factura es requerido.")
            .Must(t => t is "CxP" or "CxC")
            .WithMessage("El tipo debe ser CxP (cuenta por pagar) o CxC (cuenta por cobrar).");

        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("La persona es requerida.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.ConceptPublicId)
            .NotEmpty().WithMessage("El concepto es requerido.");
    }
}
