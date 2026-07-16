using FluentAssertions;
using OrderService.Domain;
using Xunit;

namespace OrderService.UnitTests.Domain;

public class ResultTests
{
    [Fact]
    public void Success_exposes_value_and_no_error()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_exposes_error_and_no_value()
    {
        var error = new Error("orders.empty", "Order must contain at least one item");

        var result = Result<int>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Accessing_value_of_failure_throws()
    {
        var result = Result<int>.Failure(new Error("x", "y"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }
}
