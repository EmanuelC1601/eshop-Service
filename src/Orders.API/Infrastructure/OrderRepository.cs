using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orders.API.Domain;

namespace Orders.API.Infrastructure;

public interface IOrderRepository
{
    Task CreateAsync(Order order, CancellationToken cancellationToken);
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
}
