# Testing large file

In order to test upload of large files you can use the /tests/Altinn.Broker.Tests.LargeFile console application. Run it and follow the prompts. Use the username and password for [Altinn Test Tools](https://github.com/Altinn/AltinnTestTools).

If you want to test it from another environment use the Dockerfile to deploy it as a container somewhere (like as an Azure Container App Job) and set the correct environment variables (find the UPPERCASE_SNAKE_CASE variables in Program.cs).
