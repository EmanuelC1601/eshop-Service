using MongoDB.Bson;
using Orders.API.Domain;
using Orders.API.Infrastructure;

namespace Orders.API.Application;

public sealed record CreateOrderRequest(string CustomerId, string? BasketId);
public sealed record CreateOrderResponse(string OrderId);

public sealed class CreateOrderHandler(IBasketClient basketClient, IOrderRepository repository)
{
    public async Task<CreateOrderResponse> HandleAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new OrderValidationException("customerId es obligatorio.");

        var basket = await basketClient.GetBasketAsync(request.CustomerId.Trim(), cancellationToken);
        if (basket is null || !string.Equals(basket.UserName, request.CustomerId.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new OrderValidationException("No existe un carrito para el cliente indicado.");

        if (!string.IsNullOrWhiteSpace(request.BasketId) && !string.Equals(basket.Id, request.BasketId, StringComparison.Ordinal))
            throw new OrderValidationException("El basketId no corresponde al cliente indicado.");

        if (basket.Items is null || basket.Items.Count == 0)
            throw new OrderValidationException("El carrito está vacío.");

        if (basket.Items.Any(item => item.Quantity <= 0 || item.Price < 0 || string.IsNullOrWhiteSpace(item.ProductName)))
            throw new OrderValidationException("El carrito contiene productos inválidos.");

        var items = basket.Items.Select(item => new OrderItem
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Quantity = item.Quantity,
            UnitPrice = item.Price,
            LineTotal = item.Price * item.Quantity
        }).ToList();
        var subtotal = items.Sum(item => item.LineTotal);
        var order = new Order
        {
            Id = ObjectId.GenerateNewId().ToString(),
            CustomerId = basket.UserName,
            BasketId = basket.Id,
            CreatedAt = DateTime.UtcNow,
            Items = items,
            Subtotal = subtotal,
            Total = subtotal
        };

        await repository.CreateAsync(order, cancellationToken);
        return new CreateOrderResponse(order.Id);
    }
}

public sealed class OrderValidationException(string message) : Exception(message);
