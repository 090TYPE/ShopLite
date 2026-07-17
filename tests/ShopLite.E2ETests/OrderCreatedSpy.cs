using Contracts.Events;
using MassTransit;

namespace ShopLite.E2ETests;

/// <summary>
/// Подписывается на то же событие, что и NotificationService, и делает факт
/// доставки наблюдаемым для теста.
/// </summary>
public class OrderCreatedSpy : IConsumer<OrderCreated>
{
    private static readonly TaskCompletionSource<OrderCreated> Received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static Task<OrderCreated> Delivered => Received.Task;

    public Task Consume(ConsumeContext<OrderCreated> context)
    {
        Received.TrySetResult(context.Message);
        return Task.CompletedTask;
    }
}
