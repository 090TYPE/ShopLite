using Contracts.Events;
using MassTransit;

namespace ShopLite.E2ETests;

/// <summary>
/// Подписывается на то же событие, что и NotificationService, и делает факт
/// доставки наблюдаемым для теста.
/// </summary>
/// <remarks>
/// Одноразовый: <see cref="Received"/> статичен и живёт на всю сборку, а
/// TrySetResult защёлкивает только первое сообщение. Второй тест в этом
/// проекте получил бы уже готовый результат от первого — то есть ложный
/// зелёный. Прежде чем добавлять тесты, замените статику на инстансную
/// фикстуру.
/// </remarks>
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
