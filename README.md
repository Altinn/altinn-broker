# Altinn Broker ("Formidlingstjenesten")

Altinn Broker is a Managed File Transfer (MFT) service for secure file transfer between organizations in Norway. 

[![CI/CD](https://github.com/Altinn/altinn-broker/actions/workflows/ci-cd.yaml/badge.svg)](https://github.com/Altinn/altinn-broker/actions/workflows/ci-cd.yaml)

## Getting started

Altinn Broker is currently available in Altinn's staging environment at https://platform.tt02.altinn.no. In order to get started integrating to the API, follow our [guide on getting started](./docs/get-started.md) and implement according to our [Swagger specification](https://docs.altinn.studio/api/broker/spec/).

## Postman

<a id="postman"></a>

Example requests using postman can be found in [altinn3-broker-postman-collection.json](/altinn3-broker-postman-collection.json). In order to use it, you need to [register a Maskinporten integration](https://sjolvbetjening.test.samarbeid.digdir.no/auth/login) with the scope "altinn:testtools/tokengenerator/enterprise" and use it to fill out the Postman variables "client_id", "client_kid" and "client_jwk". Also set the variable "serviceowner_orgnumber". After that, run all the requests in the folder Authenticator in order. This will authenticate you to to run all the other requests in the collection.

The first time you start testing in an environment, you need to register a service owner in Broker so that we can provision the necessary storage resources for you. Use the "Register Service Owner" request in the Postman collection to do this. Make sure you have run the Authenticator/"Authenticate as service owner (tjeneste-eier)" request first. 

Finally, you need to register a resource in the Resource Registry. First set the Postman variable resource_id to some unique ID. You can then use the requests in the Resource Registry folder with [the test policy](/Test/Altinn.Broker.Tests/Data/BasePolicy.xml) to create and manage the resource. As with registering a service owner, you need to have authenticated as a service owner to make these requests.

## Local Development

The start.ps1 script runs all neccassary commands to run the project. If you want to run the commands seperate, you can follow the steps below: 

The services required to support local development are run using docker compose:
```docker compose up -d```

To support features like hot reload etc, the app itself is run directly. Either in IDE like Visual Studio or by running:
```dotnet watch --project ./src/Altinn.Broker.API/Altinn.Broker.API.csproj```

Installing Dotnet 8.0 is a pre-requisite.

### Azurite

When running tests or when running locally, we use the Azurite storage emulator to emulate an Azure Storage account locally. You can use Azure Storage Explorer to inspect the blob contents.

### Migrations

The solution uses Flyway to run migrations. The migration scripts can be found in /src/Altinn.Broker.Persistence/Migrations. Script naming must follow the convention "V${four-digit-version-number}__${name}".
If you need to re-initialize the database during local development, you can delete the database container and re-run docker compose.

When running locally for development, you can use any Maskinporten token. It is not validated.

### Formatting

Formatting of the code base is handled by Dotnet format. [See how to configure it to format-on-save in Visual Studio here.](https://learn.microsoft.com/en-us/community/content/how-to-enforce-dotnet-format-using-editorconfig-github-actions#3---formatting-your-code-locally)

## Deploy

The build and push workflow produces a docker image that is pushed to Github packages. This image is then used by the release action found in the [altinn-broker-infra repository](https://github.com/Altinn/altinn-broker-infra).


## Load testing with k6
Before running tests you should mock the following:
- AltinnAuthorization by setting the function CheckUserAccess to return true
- AltinnRegisterService to return a string 
- AltinnResourceRegister to return a ResourceEntity
- Use the ConsoleLogEventBus 

Constants: 
- BASE_URL; enviroment to test. 
- TOKENS: tokens for a service owner(TOKENS.DUMMY_SERVICE_OWNER_TOKEN) and a sender(TOKENS.DUMMY_SENDER_TOKEN), which can be found in postman(Authenticate as Sender/serviceOwner) in the Authenticator folder. 

k6 option variables: 
- VUs: How many virtual users running tests at the same time. 
- iterations: how many tests TOTAL should be completed. vus/iterations=test per vus. 0 means infinite iterations for as long as the test will run. 
- httpDebug: full/summary. Outputs infomration about http requests and responses
- duration: How long the test should be running. The test also adds a 30 seconds gracefull stop period on top of this. 

We run load tests using k6. To run without installing k6 you can use docker-compose(base url has to be http://host.docker.internal:5096):
```docker-compose -f docker-compose-test.yml up k6-test``` 

if you have k6 installed locally, you can run it by using the following command: 
```"k6 run test.js"```
