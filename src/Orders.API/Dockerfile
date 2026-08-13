FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Orders.API/Orders.API.csproj", "src/Orders.API/"]
RUN dotnet restore "src/Orders.API/Orders.API.csproj"
COPY . .
WORKDIR "/src/src/Orders.API"
RUN dotnet publish "Orders.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Orders.API.dll"]
