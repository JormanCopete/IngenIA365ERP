using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Contributions.Commands.ProcessContribution;

public record ProcessContributionCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public decimal Amount { get; init; }
    public string ContributionType { get; init; } = "A"; // A=aporte, R=retiro
    public string? Reference { get; init; }
}

public class ProcessContributionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ProcessContributionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        ProcessContributionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Person
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Persona no encontrada."));

        var associate = await context.Associates.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PersonId == person.Id && !a.IsDeleted, cancellationToken);

        if (associate is null || associate.Status == "R")
            return Result.Failure<Guid>(new Error("Associate.NotActive",
                "La persona no es un asociado activo."));

        var personCode = person.LegacyCode ?? person.TaxId;

        // 2. If withdrawal, validate balance
        if (request.ContributionType == "R")
        {
            var currentBalance = await context.ContributionReductions
                .Where(cr => cr.PersonCode == personCode && !cr.IsDeleted)
                .SumAsync(cr => cr.OpeningBalance, cancellationToken);

            if (currentBalance < request.Amount)
                return Result.Failure<Guid>(new Error("Contribution.InsufficientBalance",
                    $"Saldo de aportes insuficiente. Disponible: {currentBalance:N0}"));
        }

        // 3. Create ContributionReduction entry
        var today = DateOnly.FromDateTime(dateTime.UtcNow);
        var period = today.Year * 100 + today.Month;

        var entry = new ContributionReduction
        {
            PersonCode = personCode,
            SavingsLineId = 0, // default contribution line
            Period = period,
            OpeningBalance = request.ContributionType == "A" ? request.Amount : -request.Amount,
            Average = request.Amount,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        // Set the day balance for today
        var day = today.Day;
        SetDayBalance(entry, day, request.ContributionType == "A" ? request.Amount : -request.Amount);

        context.ContributionReductions.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entry.PublicId);
    }

    private static void SetDayBalance(ContributionReduction entry, int day, decimal value)
    {
        switch (day)
        {
            case 1: entry.DayBalance1 = value; break;
            case 2: entry.DayBalance2 = value; break;
            case 3: entry.DayBalance3 = value; break;
            case 4: entry.DayBalance4 = value; break;
            case 5: entry.DayBalance5 = value; break;
            case 6: entry.DayBalance6 = value; break;
            case 7: entry.DayBalance7 = value; break;
            case 8: entry.DayBalance8 = value; break;
            case 9: entry.DayBalance9 = value; break;
            case 10: entry.DayBalance10 = value; break;
            case 11: entry.DayBalance11 = value; break;
            case 12: entry.DayBalance12 = value; break;
            case 13: entry.DayBalance13 = value; break;
            case 14: entry.DayBalance14 = value; break;
            case 15: entry.DayBalance15 = value; break;
            case 16: entry.DayBalance16 = value; break;
            case 17: entry.DayBalance17 = value; break;
            case 18: entry.DayBalance18 = value; break;
            case 19: entry.DayBalance19 = value; break;
            case 20: entry.DayBalance20 = value; break;
            case 21: entry.DayBalance21 = value; break;
            case 22: entry.DayBalance22 = value; break;
            case 23: entry.DayBalance23 = value; break;
            case 24: entry.DayBalance24 = value; break;
            case 25: entry.DayBalance25 = value; break;
            case 26: entry.DayBalance26 = value; break;
            case 27: entry.DayBalance27 = value; break;
            case 28: entry.DayBalance28 = value; break;
            case 29: entry.DayBalance29 = value; break;
            case 30: entry.DayBalance30 = value; break;
            case 31: entry.DayBalance31 = value; break;
        }
    }
}

public class ProcessContributionCommandValidator : AbstractValidator<ProcessContributionCommand>
{
    public ProcessContributionCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("El asociado es requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.ContributionType)
            .Must(t => t is "A" or "R")
            .WithMessage("Tipo invalido. Use 'A' para aporte o 'R' para retiro parcial.");
    }
}
