using Contracts.Events;
using MassTransit;

namespace NotificationService.Consumers;

// Этот класс — единственная точка входа. Никакого HTTP-контроллера, только очередь.
public class OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger) : IConsumer<OrderCreated>
{
    public Task Consume(ConsumeContext<OrderCreated> context)
    {
        var evt = context.Message;

        logger.LogInformation(
            "📧 [NOTIFICATION] Order #{OrderId} received — User: {UserId}, Total: {Total:C}. Sending confirmation...",
            evt.OrderId,
            evt.UserId,
            evt.Total);

        // Здесь можно подключить MailKit и реально отправить email:
        // await _mailService.SendOrderConfirmationAsync(evt.UserId, evt.OrderId, evt.Total);

        return Task.CompletedTask;
    }
}
