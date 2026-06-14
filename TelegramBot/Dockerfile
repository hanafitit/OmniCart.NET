FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["TelegramBot/OmniCart.TelegramBot.csproj", "TelegramBot/"]
COPY ["Infrastructure/OmniCart.Infrastructure.csproj", "Infrastructure/"]
COPY ["domain/OmniCart.Domain.csproj", "domain/"]

RUN dotnet restore "TelegramBot/OmniCart.TelegramBot.csproj"

COPY . .
WORKDIR "/src/TelegramBot"
RUN dotnet build "OmniCart.TelegramBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OmniCart.TelegramBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OmniCart.TelegramBot.dll"]