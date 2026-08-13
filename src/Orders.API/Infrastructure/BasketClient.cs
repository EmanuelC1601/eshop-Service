using System.Net;
using System.Net.Http.Json;

namespace Orders.API.Infrastructure;

public interface IBasketClient
{
    Task<BasketDto?> GetBasketAsync(string customerId, CancellationToken cancellationToken);
}

public sealed class BasketClient(HttpClient httpClient) : IBasketClient
{
    public async Task<BasketDto?> GetBasketAsync(string customerId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/basket/{Uri.EscapeDataString(customerId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BasketResponseDto>(cancellationToken: cancellationToken).ConfigureAwait(false) is { } result
            ? result.Cart : null;
    }
}

public sealed record BasketResponseDto(BasketDto Cart);
public sealed record BasketDto(string Id, string UserName, List<BasketItemDto> Items);
public sealed record BasketItemDto(Guid ProductId, string ProductName, int Quantity, string Color, decimal Price);
