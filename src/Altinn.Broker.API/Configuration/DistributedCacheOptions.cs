namespace Altinn.Broker.API.Configuration;

public class DistributedCacheOptions
{
    public const string SectionName = "DistributedCacheOptions";

    public string? RedisConnectionString { get; set; }
}
