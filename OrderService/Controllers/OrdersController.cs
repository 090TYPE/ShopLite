using Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Domain;
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

        var items = req.Items
            .Select(i => new NewOrderItem(i.ProductName, i.Quantity, i.Price))
            .ToList();

        var result = Order.Create(userId, items);
        if (!result.IsSuccess)
        {
            var error = result.Error!.Value;
            ModelState.AddModelError(error.Code, error.Message);
            return ValidationProblem(ModelState);
        }

        var order = result.Value;

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
