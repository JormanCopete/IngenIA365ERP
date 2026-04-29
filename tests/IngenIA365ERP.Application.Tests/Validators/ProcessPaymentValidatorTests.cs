using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Lending.Payments.Commands.ProcessPayment;

namespace IngenIA365ERP.Application.Tests.Validators;

public class ProcessPaymentValidatorTests
{
    private readonly ProcessPaymentCommandValidator _validator = new();

    private static ProcessPaymentCommand CreateValidCommand() => new()
    {
        PortfolioPublicId = Guid.NewGuid(),
        Amount = 500_000m,
        PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        PaymentMethod = "EF",
        Reference = "REC-001"
    };

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyPortfolioPublicId_ShouldFail()
    {
        var command = CreateValidCommand() with { PortfolioPublicId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PortfolioPublicId);
    }

    [Fact]
    public void ZeroAmount_ShouldFail()
    {
        var command = CreateValidCommand() with { Amount = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void NegativeAmount_ShouldFail()
    {
        var command = CreateValidCommand() with { Amount = -1000 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void EmptyPaymentMethod_ShouldFail()
    {
        var command = CreateValidCommand() with { PaymentMethod = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    public void LargeValidAmount_ShouldPass()
    {
        var command = CreateValidCommand() with { Amount = 999_999_999.99m };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void SmallValidAmount_ShouldPass()
    {
        var command = CreateValidCommand() with { Amount = 0.01m };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }
}
