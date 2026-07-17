using FluentAssertions;
using OrderService.Domain;
using OrderService.Models;
using Xunit;

namespace OrderService.UnitTests.Domain;

public class OrderStatusTests
{
    private static Order NewOrder()
        => Order.Create(Guid.NewGuid(), [new NewOrderItem("Keyboard", 1, 10m)]).Value;

    [Fact]
    public void Pending_order_can_start_processing()
    {
        var order = NewOrder();

        var result = order.MarkProcessing();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Processing);
    }

    [Fact]
    public void Processing_order_can_ship()
    {
        var order = NewOrder();
        order.MarkProcessing();

        var result = order.MarkShipped();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void Pending_order_cannot_ship_without_processing()
    {
        var order = NewOrder();

        var result = order.MarkShipped();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.InvalidTransition);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public void Cancelled_order_cannot_ship()
    {
        var order = NewOrder();
        order.Cancel();

        var result = order.MarkShipped();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.InvalidTransition);
    }

    [Fact]
    public void Pending_order_can_be_cancelled()
    {
        var order = NewOrder();

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Delivered_order_cannot_be_cancelled()
    {
        var order = NewOrder();
        order.MarkProcessing();
        order.MarkShipped();
        order.MarkDelivered();

        var result = order.Cancel();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(OrderErrors.CannotCancelDelivered);
        order.Status.Should().Be(OrderStatus.Delivered);
    }
}
