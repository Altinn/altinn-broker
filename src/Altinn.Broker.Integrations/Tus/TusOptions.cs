namespace Altinn.Broker.Integrations.Tus;

public class TusOptions
{
    public const string SectionName = "TusOptions";

    /// <summary>
    /// How long an incomplete TUS upload may remain resumable before expiration.
    /// </summary>
    public TimeSpan UploadExpiration { get; set; } = TimeSpan.FromHours(24);
}
