# altinn-broker (work-in-progress)
Formidlingstjenesten

[![Build and push](https://github.com/Altinn/altinn-broker/actions/workflows/build-and-push.yml/badge.svg)](https://github.com/Altinn/altinn-broker/actions/workflows/build-and-push.yml)

## Postman

Example requests using postman can be found in [altinn3-broker-postman-collection.json](/altinn3-broker-postman-collection.json). In order to use it, you need to [register a Maskinporten integration](https://sjolvbetjening.test.samarbeid.digdir.no/auth/login) with the scope "altinn:testtools/tokengenerator/enterprise" and use it to fill out the Postman variables "client_id", "client_kid" and "client_jwk". After that, run all the requests in the folder Authenticator in order. This will authenticate you to to run all the other requests in the collection.

You also need to register a resource in the Resource Registry, and use its id in the resource_id variable. You can use the requests in the Resource Registry folder with [the test policy](/Test/Altinn.Broker.Tests/Data/BasePolicy.xml) to do this. 

## Local Development

The services required to support local development are run using docker compose:
```docker compose up -d```

To support features like hot reload etc, the app itself is run directly. Either in IDE like Visual Studio or by running:
```dotnet watch --project ./src/Altinn.Broker.API/Altinn.Broker.API.csproj```

### Azurite

When running tests or when running locally, we use the Azurite storage emulator to emulate an Azure Storage account locally. You can use Azure Storage Explorer to inspect the blob contents.

### Migrations

The solution uses Flyway to run migrations. The migration scripts can be found in /src/Altinn.Broker.Persistence/Migrations. Script naming must follow the convention "V${four-digit-version-number}__${name}".
If you need to re-initialize the database during local development, you can delete the database container and re-run docker compose.

### Authorization

To get access to the Broker API in staging/production, a consumer needs to use a Maskinporten integration. Recipients should use the scope altinn:broker.read and senders should use the scope altinn:broker.write. Tokens with both scopes also work. You can create a Maskinporten integration here:
https://selvbetjening-samarbeid-ver2.difi.no/integrations

For more on Maskinporten tokens see:
https://docs.digdir.no/docs/Maskinporten/maskinporten_guide_apikonsument

When running locally for development, you can use any Maskinporten token. It is not validated.

### Formatting

Formatting of the code base is handled by Dotnet format. [See how to configure it to format-on-save in Visual Studio here.](https://learn.microsoft.com/en-us/community/content/how-to-enforce-dotnet-format-using-editorconfig-github-actions#3---formatting-your-code-locally)

## Deploy

The build and push workflow produces a docker image that is pushed to Github packages. This image is then used by the release action found in the [altinn-broker-infra repository](https://github.com/Altinn/altinn-broker-infra).
