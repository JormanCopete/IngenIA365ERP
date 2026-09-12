using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Agreements.Commands.CreateAgreement;

public record CreateAgreementCommand : IRequest<Result<Guid>>
{
    /// <summary>Código alfanumérico de la cooperativa (hasta 10); se guarda en LegacyCode.</summary>
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AccountNumber { get; init; }
    public string? EntityCode { get; init; }
    public short Currency { get; init; }
    public string? SavingsCode { get; init; }
    public string? CheckingCode { get; init; }
    public string? BlockCode { get; init; }

    // Availability config
    public short AvailabilityOption { get; init; }
    public decimal AvailabilityLimit { get; init; }
    public decimal AvailabilityRate { get; init; }

    // ATM config
    public short AtmOption { get; init; }
    public decimal AtmLimit { get; init; }
    public decimal AtmRate { get; init; }
    public short AtmTransactions { get; init; }

    // POS config
    public short PosOption { get; init; }
    public decimal PosLimit { get; init; }
    public decimal PosRate { get; init; }
    public short PosTransactions { get; init; }

    // Balances and limits
    public short ShowBalances { get; init; }
    public int Bin { get; init; }
    public decimal AvailableLimit { get; init; }
    public decimal CashLimit { get; init; }

    // File paths
    public string? OutputPath { get; init; }
    public string? InputPath { get; init; }

    // Additional config
    public int AverageDays { get; init; }
    public int FreeTransactions { get; init; }
    public int HandlingFee { get; init; }
    public decimal AvailableLimit2 { get; init; }
    public decimal CashLimit2 { get; init; }
    public int ServiceType { get; init; }
}

public class CreateAgreementCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAgreementCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateAgreementCommand request,
        CancellationToken cancellationToken)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Agreements.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && !e.IsDeleted, cancellationToken);
            if (repetido is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un convenio", codigo, repetido.Name));
        }

        var entity = new Agreement
        {
            LegacyCode = codigo,
            Name = request.Name,
            AccountNumber = request.AccountNumber,
            EntityCode = request.EntityCode,
            Currency = request.Currency,
            SavingsCode = request.SavingsCode,
            CheckingCode = request.CheckingCode,
            BlockCode = request.BlockCode,
            AvailabilityOption = request.AvailabilityOption,
            AvailabilityLimit = request.AvailabilityLimit,
            AvailabilityRate = request.AvailabilityRate,
            AtmOption = request.AtmOption,
            AtmLimit = request.AtmLimit,
            AtmRate = request.AtmRate,
            AtmTransactions = request.AtmTransactions,
            PosOption = request.PosOption,
            PosLimit = request.PosLimit,
            PosRate = request.PosRate,
            PosTransactions = request.PosTransactions,
            ShowBalances = request.ShowBalances,
            Bin = request.Bin,
            AvailableLimit = request.AvailableLimit,
            CashLimit = request.CashLimit,
            OutputPath = request.OutputPath,
            InputPath = request.InputPath,
            AverageDays = request.AverageDays,
            FreeTransactions = request.FreeTransactions,
            HandlingFee = request.HandlingFee,
            AvailableLimit2 = request.AvailableLimit2,
            CashLimit2 = request.CashLimit2,
            ServiceType = request.ServiceType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Agreements.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateAgreementCommandValidator : AbstractValidator<CreateAgreementCommand>
{
    public CreateAgreementCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.AccountNumber)
            .MaximumLength(60).WithMessage("Account number must not exceed 60 characters.");

        RuleFor(x => x.EntityCode)
            .MaximumLength(10).WithMessage("Entity code must not exceed 10 characters.");

        RuleFor(x => x.SavingsCode)
            .MaximumLength(4).WithMessage("Savings code must not exceed 4 characters.");

        RuleFor(x => x.CheckingCode)
            .MaximumLength(4).WithMessage("Checking code must not exceed 4 characters.");

        RuleFor(x => x.BlockCode)
            .MaximumLength(4).WithMessage("Block code must not exceed 4 characters.");

        RuleFor(x => x.OutputPath)
            .MaximumLength(200).WithMessage("Output path must not exceed 200 characters.");

        RuleFor(x => x.InputPath)
            .MaximumLength(200).WithMessage("Input path must not exceed 200 characters.");

        RuleFor(x => x.AvailabilityLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Availability limit must be non-negative.");

        RuleFor(x => x.AtmLimit)
            .GreaterThanOrEqualTo(0).WithMessage("ATM limit must be non-negative.");

        RuleFor(x => x.PosLimit)
            .GreaterThanOrEqualTo(0).WithMessage("POS limit must be non-negative.");

        RuleFor(x => x.AvailableLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Available limit must be non-negative.");

        RuleFor(x => x.CashLimit)
            .GreaterThanOrEqualTo(0).WithMessage("Cash limit must be non-negative.");
    }
}
