namespace Basket.API.Basket.StoreBasket
{
    public record StoreBasketResponse(string UserName);

    public class StoreBasketEndPoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("/basket", async (ShoppingCart cart, ISender sender) =>
            {
                if (string.IsNullOrWhiteSpace(cart.UserName))
                    return Results.BadRequest(new { message = "El nombre del cliente es obligatorio." });

                cart.Items ??= [];
                var result = await sender.Send(new StoreBasketCommand(cart));

                return Results.Created($"/basket/{result.UserName}", new StoreBasketResponse(result.UserName));
            })
            .WithName("StoreBasket")
            .Produces<StoreBasketResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Guardar carrito")
            .WithDescription("Crea o actualiza el carrito de compras de un usuario.");
        }
    }
}
