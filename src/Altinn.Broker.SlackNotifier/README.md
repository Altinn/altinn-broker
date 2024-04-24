# Slack notifier
This function app is designed to convert Azure log alert v2 to a Slack message formatted as an ASCII table. 

When a Azure log alert triggers it notifies every consumer of the configured Azure action group. One of the consumers is this Azure function app which receives a HTTP Post request with the [log alert v2 format](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-common-schema#sample-log-alert-when-the-monitoringservice--log-alerts-v2). See [AzureAlertDto.cs](./Features/AzureAlertToSlackForwarder/AzureAlertDto.cs) for the format this function app expects. 

The log alert v2 format does not include the actual query data which triggered the alert. Therefore the function app must fetch it by calling application insight. The data is then transformed to an ASCII table and pushed to the configured Slack webhook URL through the field `exceptionReport`. It will also include a link to the application insight log with the following predefined query in the field named `link`:
```KQL
exceptions
| order by timestamp desc
```

The configured Slack webhook will receive the following request:
```HTTP
HTTP POST [Slack_Webhook_Url]
{
    "exceptionReport": "Ascii_table_as_string",
    "link": "Link_to_application_insight",
}
```

## Local development
1. [Login to azure](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/?tabs=command-line#exploring-the-sequence-of-defaultazurecredential-authentication-methods)
2. Configure the Slack webhook URL
    ```powerhell
    dotnet user-secrets set -p .\src\Altinn.Broker.SlackNotifier\ "Slack:WebhookUrl" "SLACK_WEBHOOK_URL_HERE"
    ```
3. Start the function app
4. Send a log alert v2 format to the app

The configured URL doesn't have to be an actual Slack workflow webhook URL. It could point to an online webhook tester like https://webhook.site or a homemade webhook tester on your local machine.

### Get a valid log alert v2 request
This function app uses the links in the incoming alerts request to fetch data. Therefore the requests are app instance and time specific. The provided example request is most likely to be invalid by the time this article is read. Do the following to get a valid request: 
1. Go to https://webhook.site and copy your unique URL
2. Add the URL as a webhook action of the azure action group 
3. Trigger the alert
4. Copy the request from https://webhook.site into Postman. It may take several minutes for the alert to produce a request to the webhook.
5. Delete the webhook action from the azure action group

Example log alert v2 request:
```jsonc
{
  "schemaId": "azureMonitorCommonAlertSchema",
  "data": {
    "essentials": {
      "alertId": "/subscriptions/<subscription ID>/providers/Microsoft.AlertsManagement/alerts/b9569717-bc32-442f-add5-83a997729330",
      "alertRule": "WCUS-R2-Gen2",
      "severity": "Sev3",
      "signalType": "Metric",
      "monitorCondition": "Resolved",
      "monitoringService": "Platform",
      "alertTargetIDs": [
        "/subscriptions/<subscription ID>/resourcegroups/pipelinealertrg/providers/microsoft.compute/virtualmachines/wcus-r2-gen2"
      ],
      "configurationItems": [
        "wcus-r2-gen2"
      ],
      "originAlertId": "3f2d4487-b0fc-4125-8bd5-7ad17384221e_PipeLineAlertRG_microsoft.insights_metricAlerts_WCUS-R2-Gen2_-117781227",
      "firedDateTime": "2019-03-22T13:58:24.3713213Z",
      "resolvedDateTime": "2019-03-22T14:03:16.2246313Z",
      "description": "",
      "essentialsVersion": "1.0",
      "alertContextVersion": "1.0"
    },
    "alertContext": {
      "properties": null,
      "conditionType": "SingleResourceMultipleMetricCriteria",
      "condition": {
        "windowSize": "PT5M",
        "allOf": [
          {
            "metricName": "Percentage CPU",
            "metricNamespace": "Microsoft.Compute/virtualMachines",
            "operator": "GreaterThan",
            "threshold": "25",
            "timeAggregation": "Average",
            "dimensions": [
              {
                "name": "ResourceId",
                "value": "3efad9dc-3d50-4eac-9c87-8b3fd6f97e4e"
              }
            ],
            "metricValue": 7.727
          }
        ]
      }
    },
    "customProperties": {
      "Key1": "Value1",
      "Key2": "Value2"
    }
  }
}

```