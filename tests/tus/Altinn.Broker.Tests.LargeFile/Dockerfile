FROM mcr.microsoft.com/dotnet/aspnet:9.0.2-alpine3.20 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0.200-alpine3.20 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["tests/tus/Altinn.Broker.Tests.LargeFile/Altinn.Broker.Tests.LargeFile.csproj", "tests/tus/Altinn.Broker.Tests.LargeFile/"]
COPY ["src/Altinn.Broker.API/Altinn.Broker.API.csproj", "src/Altinn.Broker.API/"]
COPY ["src/Altinn.Broker.Application/Altinn.Broker.Application.csproj", "src/Altinn.Broker.Application/"]
COPY ["src/Altinn.Broker.Core/Altinn.Broker.Core.csproj", "src/Altinn.Broker.Core/"]
COPY ["src/Altinn.Broker.Integrations/Altinn.Broker.Integrations.csproj", "src/Altinn.Broker.Integrations/"]
COPY ["src/Altinn.Broker.Persistence/Altinn.Broker.Persistence.csproj", "src/Altinn.Broker.Persistence/"]
RUN dotnet restore "./tests/tus/Altinn.Broker.Tests.LargeFile/Altinn.Broker.Tests.LargeFile.csproj"
COPY . .
WORKDIR "/src/tests/tus/Altinn.Broker.Tests.LargeFile"
RUN dotnet build "./Altinn.Broker.Tests.LargeFile.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Altinn.Broker.Tests.LargeFile.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Altinn.Broker.Tests.LargeFile.dll"]