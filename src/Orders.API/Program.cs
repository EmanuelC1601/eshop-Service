using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Orders.API.Application;
using Orders.API.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(MongoDbSettings.SectionName));
var mongoSettings = builder.Configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>()
    ?? throw new InvalidOperationException("MongoDbSettings no está configurado.");
if (string.IsNullOrWhiteSpace(mongoSettings.ConnectionString))
    throw new InvalidOperationException("MongoDbSettings:ConnectionString no está configurado.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddHttpClient<IBasketClient, BasketClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:BasketUrl"]
        ?? throw new InvalidOperationException("Services:BasketUrl no está configurado."));
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddCors(options => options.AddPolicy("AllowFrontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    if (origins is { Length: > 0 }) policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
    else policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
}));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddMongoDb(
    serviceProvider => serviceProvider.GetRequiredService<IMongoClient>(),
    name: "mongodb",
    tags: ["ready"]);

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowFrontend");

app.MapPost("/api/orders", async (CreateOrderRequest request, CreateOrderHandler handler, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        return Results.Created($"/api/orders/{result.OrderId}", result);
    }
    catch (OrderValidationException exception)
    {
        return Results.BadRequest(new { message = exception.Message });
    }
    catch (HttpRequestException)
    {
        return Results.Problem("No fue posible consultar el carrito.", statusCode: StatusCodes.Status502BadGateway);
    }
    catch (MongoException)
    {
        return Results.Problem("No fue posible guardar la orden.", statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("CreateOrder")
.Produces<CreateOrderResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status500InternalServerError)
.WithSummary("Genera una orden usando el carrito existente del cliente");

// Render calls /health while starting the container. It must represent the
// liveness of this API, not the availability of an external managed database.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions());
app.Run();
