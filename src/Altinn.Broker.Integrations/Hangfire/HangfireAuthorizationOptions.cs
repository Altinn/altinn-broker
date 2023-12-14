namespace Altinn.Broker.Integrations.Hangfire;
public class HangfireAuthorizationOptions
{
    public string TenantId { get; set; }
    public string GroupId { get; set; }
    public string Audience { get; set; }

}
