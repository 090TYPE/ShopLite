namespace OrderService.Domain;

/// <summary>Позиция, поданная на вход фабрике. Не EF-сущность: у неё нет identity.</summary>
public readonly record struct NewOrderItem(string ProductName, int Quantity, decimal Price);
