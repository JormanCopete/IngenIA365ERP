using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldReturnSuccessResult()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ShouldReturnFailureResult()
    {
        var error = new Error("Test.Error", "Something went wrong");
        var result = Result.Failure(error);
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Something went wrong");
    }

    [Fact]
    public void GenericSuccess_ShouldContainValue()
    {
        var result = Result.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GenericFailure_ShouldHaveDefaultValue()
    {
        var result = Result.Failure<int>(Error.NotFound);
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default);
        result.Error.Should().Be(Error.NotFound);
    }
}
