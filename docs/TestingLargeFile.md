# Testing large file

In order to test upload of large files you can use the `tests/tus/Altinn.Broker.Tests.LargeFile` console application. Configure Maskinporten credentials (`CLIENT_ID`, `CLIENT_KID`, `CLIENT_PEM_FILE`), `ORG_NO`, and `RESOURCE_ID` as described in `tests/tus/README.md`. Ensure you have [set up a system user for your organisation](https://docs.altinn.studio/en/authorization/getting-started/systemuser/) linked to your Maskinporten client, then run the project.

If you want to test it from another environment use the Dockerfile to deploy it as a container somewhere (like as an Azure Container App Job) and set the correct environment variables (find the UPPERCASE_SNAKE_CASE variables in Program.cs).
