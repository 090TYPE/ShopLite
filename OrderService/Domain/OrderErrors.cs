namespace OrderService.Domain;

public static class OrderErrors
{
    public static readonly Error NoItems =
        new("orders.no_items", "Order must contain at least one item.");

    public static readonly Error InvalidQuantity =
        new("orders.invalid_quantity", "Item quantity must be greater than zero.");

    public static readonly Error NegativePrice =
        new("orders.negative_price", "Item price cannot be negative.");

    public static readonly Error BlankProductName =
        new("orders.blank_product_name", "Item product name is required.");

    public static readonly Error UnknownUser =
        new("orders.unknown_user", "Order must belong to a user.");

    public static readonly Error InvalidTransition =
        new("orders.invalid_transition", "Order status transition is not allowed.");

    public static readonly Error CannotCancelDelivered =
        new("orders.cannot_cancel_delivered", "A delivered order cannot be cancelled.");
}
