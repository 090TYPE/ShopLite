namespace Contracts.Events;

public record OrderCreated(Guid OrderId, Guid UserId, decimal Total, DateTime CreatedAt);
