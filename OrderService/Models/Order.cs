using OrderService.Domain;

namespace OrderService.Models;

public class Order
{
    private readonly List<OrderItem> _items = [];

    // Приватный конструктор для EF Core — материализация из БД минует фабрику.
    private Order() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public static Result<Order> Create(Guid userId, IReadOnlyCollection<NewOrderItem> items)
    {
        if (userId == Guid.Empty)
            return Result<Order>.Failure(OrderErrors.UnknownUser);

        if (items.Count == 0)
            return Result<Order>.Failure(OrderErrors.NoItems);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
                return Result<Order>.Failure(OrderErrors.BlankProductName);

            if (item.Quantity <= 0)
                return Result<Order>.Failure(OrderErrors.InvalidQuantity);

            if (item.Price < 0)
                return Result<Order>.Failure(OrderErrors.NegativePrice);
        }

        var order = new Order { UserId = userId };

        foreach (var item in items)
        {
            order._items.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price
            });
        }

        order.Total = order._items.Sum(i => i.Price * i.Quantity);

        return Result<Order>.Success(order);
    }
}

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }
