FROM mcr.microsoft.com/dotnet/sdk:9.0.200-alpine3.20 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.Broker.API/*.csproj ./src/Altinn.Broker.API/
COPY src/Altinn.Broker.Core/*.csproj ./src/Altinn.Broker.Core/
COPY src/Altinn.Broker.Integrations/*.csproj ./src/Altinn.Broker.Integrations/
COPY src/Altinn.Broker.Persistence/*.csproj ./src/Altinn.Broker.Persistence/
RUN dotnet restore ./src/Altinn.Broker.API/Altinn.Broker.API.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet publish -c Release -o out ./src/Altinn.Broker.API/Altinn.Broker.API.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.2-alpine3.20 AS final
WORKDIR /app
EXPOSE 2525
ENV ASPNETCORE_URLS=http://+:2525

COPY --from=build /app/out .
#COPY src/Altinn.Broker.Persistence/Migration ./Migration

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet

RUN mkdir -p /mnt/storage
run chown -R dotnet:dotnet /mnt/storage
USER dotnet
ENTRYPOINT [ "dotnet", "Altinn.Broker.API.dll" ]
