using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(OrderDbContext db, IPublishEndpoint publisher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var order = new Order
        {
            UserId = userId,
            Items = req.Items.Select(i => new OrderItem
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList()
        };
        order.Total = order.Items.Sum(i => i.Price * i.Quantity);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Публикуем событие — NotificationService получит его асинхронно
        await publisher.Publish(new OrderCreated(order.Id, order.UserId, order.Total, order.CreatedAt));

        return Created($"/api/orders/{order.Id}", new { order.Id, order.Total, order.Status });
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        return order is null ? NotFound() : Ok(order);
    }
}

public record CreateOrderRequest(List<OrderItemRequest> Items);
public record OrderItemRequest(string ProductName, int Quantity, decimal Price);
