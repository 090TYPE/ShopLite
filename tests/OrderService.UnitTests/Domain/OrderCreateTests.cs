using FluentAssertions;
using OrderService.Domain;
using OrderService.Models;
using Xunit;

namespace OrderService.UnitTests.Domain;

public class OrderCreateTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static NewOrderItem ValidItem(string name = "Keyboard", int qty = 2, decimal price = 49.50m)
        => new(name, qty, price);

    [Fact]
    public void Create_with_valid_items_succeeds()
    {
        var result = Order.Create(UserId, [ValidItem()]);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(UserId);
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Create_computes_total_across_items()
    {
        var result = Order.Create(UserId,
        [
            ValidItem("Keyboard", qty: 2, price: 49.50m),
            ValidItem("Mouse", qty: 3, price: 10.00m)
        ]);

        // 2 * 49.50 + 3 * 10.00
        result.Value.Total.Should().Be(129.00m);
    }

    [Fact]
    public void Create_starts_order_as_pending()
    {
        var result = Order.Create(UserId, [ValidItem()]);

        result.Value.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Create_with_no_items_fails()
    {
        var result = Order.Create(UserId, []);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.NoItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_quantity_fails(int quantity)
    {
        var result = Order.Create(UserId, [ValidItem(qty: quantity)]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.InvalidQuantity);
    }

    [Fact]
    public void Create_with_negative_price_fails()
    {
        var result = Order.Create(UserId, [ValidItem(price: -0.01m)]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.NegativePrice);
    }

    [Fact]
    public void Create_with_zero_price_succeeds()
    {
        // Бесплатная позиция допустима — правило Price >= 0, а не > 0
        var result = Order.Create(UserId, [ValidItem(price: 0m)]);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_product_name_fails(string name)
    {
        var result = Order.Create(UserId, [ValidItem(name: name)]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.BlankProductName);
    }

    [Fact]
    public void Create_with_empty_user_fails()
    {
        var result = Order.Create(Guid.Empty, [ValidItem()]);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.UnknownUser);
    }
}
