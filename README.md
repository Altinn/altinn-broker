# altinn-broker
Formidlingstjenesten

## Altinn broker Upload PoC
This is a PoC of Altinn-broker setting up a basic docker container with a simple service for uploading a file.
It is very much subject to change and should not be considered an example of the finished service.

## Running the PoC
The PoC can be run by either opening it in Visual Studio Code and running it, or by deploying it to a local Docker container using the command:

```bash
docker-compose up -d --build
```

Example requests using postman can be found in [Altinn3 Broker.postman_collection_examples.json](Altinn3 Broker.postman_collection_examples.json). 

## Local Development

The services required to support local development are run using docker compose:
```docker compose up -d```

### Azurite

When running tests or when running locally, we use the Azurite storage emulator to emulate an Azure Storage account locally. You can use Azure Storage Explorer to inspect the blob contents.

### Migrations

The solution uses Flyway to run migrations. The migration scripts can be found in /src/Altinn.Broker.Persistence/Migrations. Script naming must follow the convention "V${four-digit-version-number}__${name}".
If you need to re-initialize the database during local development, you can delete the database container and re-run docker compose.