using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orders.API.Domain;

namespace Orders.API.Infrastructure;

public interface IOrderRepository
{
    Task CreateAsync(Order order, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken cancellationToken);
    Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken);
}

public class OrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> _orders;

    public OrderRepository(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        var configuration = settings.Value;
        _orders = client.GetDatabase(configuration.DatabaseName)
            .GetCollection<Order>(configuration.CollectionName);
    }

    public Task CreateAsync(Order order, CancellationToken cancellationToken) =>
        _orders.InsertOneAsync(order, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(string customerId, CancellationToken cancellationToken) =>
        await _orders.Find(order => order.CustomerId == customerId)
            .SortByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken) =>
        await _orders.Find(order => order.Id == orderId).FirstOrDefaultAsync(cancellationToken);
}
