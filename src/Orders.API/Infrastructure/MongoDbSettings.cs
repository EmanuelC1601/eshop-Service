namespace Orders.API.Infrastructure;

public class MongoDbSettings
{
    public const string SectionName = "MongoDbSettings";
    public string ConnectionString { get; set; } = default!;
    public string DatabaseName { get; set; } = "OrdersDb";
    public string CollectionName { get; set; } = "Orders";
}
