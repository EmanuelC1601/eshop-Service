using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);
var databaseConnectionString = ConnectionStringNormalizer.NormalizePostgres(
    builder.Configuration.GetConnectionString("Database")!);
var redisConnectionString = ConnectionStringNormalizer.NormalizeRedis(
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddCarter();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
            return;
        }

        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(databaseConnectionString);
}).UseLightweightSessions();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "basket:";
});

builder.Services.AddScoped<BasketRepository>();
builder.Services.AddScoped<IBasketRepository>(provider =>
{
    var repository = provider.GetRequiredService<BasketRepository>();
    var cache = provider.GetRequiredService<IDistributedCache>();
    return new CachedBasketRepository(repository, cache);
});

builder.Services.AddHealthChecks()
    .AddNpgSql(databaseConnectionString)
    .AddRedis(redisConnectionString);

var app = builder.Build();

app.UseExceptionHandler(_ => { });
app.UseCors("AllowAll");
app.MapCarter();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
