docker compose up -d
$ENV:MAX_FILE_UPLOAD_SIZE = "2147483648"
dotnet watch --project ./src/Altinn.Broker.API/Altinn.Broker.API.csproj
