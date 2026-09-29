FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/NetCalendy.Api/NetCalendy.Api.csproj", "src/NetCalendy.Api/"]
COPY ["src/NetCalendy.Application/NetCalendy.Application.csproj", "src/NetCalendy.Application/"]
COPY ["src/NetCalendy.Domain/NetCalendy.Domain.csproj", "src/NetCalendy.Domain/"]
COPY ["src/NetCalendy.Infrastructure/NetCalendy.Infrastructure.csproj", "src/NetCalendy.Infrastructure/"]
RUN dotnet restore "src/NetCalendy.Api/NetCalendy.Api.csproj"

COPY . .
WORKDIR "/src/src/NetCalendy.Api"
RUN dotnet build "NetCalendy.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NetCalendy.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NetCalendy.Api.dll"]
