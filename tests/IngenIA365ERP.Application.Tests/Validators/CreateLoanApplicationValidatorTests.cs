using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Lending.LoanApplications.Commands.CreateLoanApplication;

namespace IngenIA365ERP.Application.Tests.Validators;

public class CreateLoanApplicationValidatorTests
{
    private readonly CreateLoanApplicationCommandValidator _validator = new();

    private static CreateLoanApplicationCommand CreateValidCommand() => new()
    {
        PersonPublicId = Guid.NewGuid(),
        CreditLinePublicId = Guid.NewGuid(),
        RequestedAmount = 5_000_000m,
        RequestedTerm = 36,
        Purpose = "Capital de trabajo",
        GuaranteeType = "P"
    };

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyPersonPublicId_ShouldFail()
    {
        var command = CreateValidCommand() with { PersonPublicId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PersonPublicId);
    }

    [Fact]
    public void EmptyCreditLinePublicId_ShouldFail()
    {
        var command = CreateValidCommand() with { CreditLinePublicId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.CreditLinePublicId);
    }

    [Fact]
    public void ZeroAmount_ShouldFail()
    {
        var command = CreateValidCommand() with { RequestedAmount = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }

    [Fact]
    public void NegativeAmount_ShouldFail()
    {
        var command = CreateValidCommand() with { RequestedAmount = -100 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestedAmount);
    }

    [Fact]
    public void ZeroTerm_ShouldFail()
    {
        var command = CreateValidCommand() with { RequestedTerm = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestedTerm);
    }

    [Fact]
    public void TermOver360_ShouldFail()
    {
        var command = CreateValidCommand() with { RequestedTerm = 361 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequestedTerm);
    }

    [Fact]
    public void EmptyGuaranteeType_ShouldFail()
    {
        var command = CreateValidCommand() with { GuaranteeType = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GuaranteeType);
    }

    [Fact]
    public void ValidTermBoundary_1Month_ShouldPass()
    {
        var command = CreateValidCommand() with { RequestedTerm = 1 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.RequestedTerm);
    }

    [Fact]
    public void ValidTermBoundary_360Months_ShouldPass()
    {
        var command = CreateValidCommand() with { RequestedTerm = 360 };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.RequestedTerm);
    }
}
