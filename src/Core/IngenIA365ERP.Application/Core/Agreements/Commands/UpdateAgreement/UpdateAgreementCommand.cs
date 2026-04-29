using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Agreements.Commands.UpdateAgreement;

public record UpdateAgreementCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
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

public class UpdateAgreementCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAgreementCommand, Result>
{
    public async Task<Result> Handle(
        UpdateAgreementCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Agreements
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Name = request.Name;
        entity.AccountNumber = request.AccountNumber;
        entity.EntityCode = request.EntityCode;
        entity.Currency = request.Currency;
        entity.SavingsCode = request.SavingsCode;
        entity.CheckingCode = request.CheckingCode;
        entity.BlockCode = request.BlockCode;
        entity.AvailabilityOption = request.AvailabilityOption;
        entity.AvailabilityLimit = request.AvailabilityLimit;
        entity.AvailabilityRate = request.AvailabilityRate;
        entity.AtmOption = request.AtmOption;
        entity.AtmLimit = request.AtmLimit;
        entity.AtmRate = request.AtmRate;
        entity.AtmTransactions = request.AtmTransactions;
        entity.PosOption = request.PosOption;
        entity.PosLimit = request.PosLimit;
        entity.PosRate = request.PosRate;
        entity.PosTransactions = request.PosTransactions;
        entity.ShowBalances = request.ShowBalances;
        entity.Bin = request.Bin;
        entity.AvailableLimit = request.AvailableLimit;
        entity.CashLimit = request.CashLimit;
        entity.OutputPath = request.OutputPath;
        entity.InputPath = request.InputPath;
        entity.AverageDays = request.AverageDays;
        entity.FreeTransactions = request.FreeTransactions;
        entity.HandlingFee = request.HandlingFee;
        entity.AvailableLimit2 = request.AvailableLimit2;
        entity.CashLimit2 = request.CashLimit2;
        entity.ServiceType = request.ServiceType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
