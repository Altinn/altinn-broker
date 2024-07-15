# Infrastructure
Infrastructure-as-code (IaC) for Altinn Broker (formidlingstjenesten)

# Environments

* Test (https://altinn-dev-api.azure-api.net) - Used for testing during development and performance tests.
* Staging (https://platform.tt02.altinn.no) - Used for external testers and for final manual tests before putting into production.
* Production (https://platform.altinn.no) - Should run the latest image from the main branch of altinn-broker repo.

# Versioning

Versioning is handled with a route parameter. Changes made should generally be backwards compatible, and bumping version should only be necessary when making breaking changes after we have gone live in production.

# Development workflow
1. Code repo action produces a docker image as an artifact and places it into Github packages
2. Infrastructure is deployed using a Github action. The deployment uses the last Github build from code repo.

## Github action

* The Github Action uses a service principal in order to authenticate to manage Azure resources, as when deploying resources. To create a service principal, do
```
az login
az ad sp create-for-rbac --name broker_sp --role Owner --scopes /subscriptions/<subscription_id>
```

* Federated credentials are used to authorize the pipeline based on the repo and branch. This is the reason why we do not need client secret in the pipeline. Because of this, we need one federated credential for the main branch and one for pull requests. Generate these by running:
```
az ad app federated-credential create --id <APPLICATION-OBJECT-ID> --parameters credential-pr.json
az ad app federated-credential create --id <APPLICATION-OBJECT-ID> --parameters credential-main.json
```

# FAQ

## How to get access to deployed database

The database uses IP blocking for security reasons so to get access you need to add a firewall rule on the database server for your IP. You also need to set yourself as an AD administrator with access to the database.

1. Go to the database server in the Azure Portal (name ends in "-dbserver")
2. Go Settings > Networking and click "Add current client IP address"
3. Go Security > Authentication and use "Add Microsoft Entra Admins" to add yourself.
4. After you have added yourself, you will see your AD user in the list of admins. Use the username from here and use an Azure Access token for password that can be generated using the CLI:
```
az account get-access-token --resource=https://ossrdbms-aad.database.windows.net/.default --query accessToken --output tsv
```

* Note: There is some time delay (~3 min) from when your IP address is added successfully to the firewall and when it actually has access.

## Where/how is APIM configured?

We run on Platform's shared APIM. It is configured in [Azure Devops/altinn-studio-ops](https://dev.azure.com/brreg/altinn-studio-ops/_git/altinn-studio-ops) See:

https://pedia.altinn.cloud/altinn-3/ops/release-and-deploy/api-management/

## Create release notes
If the version in version.txt is bumped, a new release in github will automaticly be created the next time the production environment is deployed. 
