using Azure.Core;
using Azure.Identity;
using Altinn.Broker.SlackNotifier.External.AppInsights;
using Altinn.Broker.SlackNotifier.External.Slack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = await new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(x => x.AddUserSecrets<Program>(optional: true, reloadOnChange: false))
    .ConfigureServices((hostContext, services) =>
      {
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
        services.AddHttpClient<ISlackClient, SlackClient>();
        services.AddHttpClient<IAppInsightsClient, AppInsightsClient>();
        services.Configure<SlackOptions>(hostContext.Configuration.GetSection(SlackOptions.ConfigurationSection));
      })
      .StartAsync();
