using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Accounting.Documents.Commands.CreateDocument;

namespace IngenIA365ERP.Application.Tests.Validators;

public class CreateDocumentValidatorTests
{
    private readonly CreateDocumentCommandValidator _validator = new();

    private static CreateDocumentCommand CreateValidCommand() => new()
    {
        VoucherTypePublicId = Guid.NewGuid(),
        DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Description = "Comprobante de prueba",
        Lines =
        [
            new JournalEntryLineDto(
                Guid.NewGuid(), null, null, null,
                100_000m, 0m, "Debito", "REF001"),
            new JournalEntryLineDto(
                Guid.NewGuid(), null, null, null,
                0m, 100_000m, "Credito", "REF001")
        ]
    };

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = _validator.TestValidate(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyVoucherType_ShouldFail()
    {
        var command = CreateValidCommand() with { VoucherTypePublicId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.VoucherTypePublicId);
    }

    [Fact]
    public void NullDescription_ShouldPass()
    {
        var command = CreateValidCommand() with { Description = null };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void LessThanTwoLines_ShouldFail()
    {
        var command = CreateValidCommand() with
        {
            Lines =
            [
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 100m, 0m, "Solo", "R1")
            ]
        };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Lines);
    }

    [Fact]
    public void UnbalancedLines_DebitGreaterThanCredit_ShouldFail()
    {
        var command = CreateValidCommand() with
        {
            Lines =
            [
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 200_000m, 0m, "Debito", "R1"),
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 0m, 100_000m, "Credito", "R1")
            ]
        };
        var result = _validator.TestValidate(command);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void BalancedThreeLines_ShouldPass()
    {
        var command = CreateValidCommand() with
        {
            Lines =
            [
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 50_000m, 0m, "D1", "R1"),
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 50_000m, 0m, "D2", "R1"),
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 0m, 100_000m, "C1", "R1")
            ]
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void LineWithEmptyAccountPublicId_ShouldFail()
    {
        var command = CreateValidCommand() with
        {
            Lines =
            [
                new JournalEntryLineDto(Guid.Empty, null, null, null, 100m, 0m, "D", "R"),
                new JournalEntryLineDto(Guid.NewGuid(), null, null, null, 0m, 100m, "C", "R")
            ]
        };
        var result = _validator.TestValidate(command);
        result.ShouldHaveAnyValidationError();
    }
}
