FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OmniCart.BlazorAdmin/OmniCart.BlazorAdmin.csproj", "OmniCart.BlazorAdmin/"]
COPY ["Infrastructure/OmniCart.Infrastructure.csproj", "Infrastructure/"]
COPY ["domain/OmniCart.Domain.csproj", "domain/"]

RUN dotnet restore "OmniCart.BlazorAdmin/OmniCart.BlazorAdmin.csproj"

COPY . .
WORKDIR "/src/OmniCart.BlazorAdmin"
RUN dotnet build "OmniCart.BlazorAdmin.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OmniCart.BlazorAdmin.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OmniCart.BlazorAdmin.dll"]