using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Orders.API.Domain;

public class Order
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public string BasketId { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
}

public class OrderItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
