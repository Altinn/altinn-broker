using System.Text.Json.Serialization;

namespace Altinn.Broker.Application.GenerateReport;

/// <summary>
/// Parquet-friendly model for daily summary data.
/// All properties are simple types optimized for ParquetSerializer.
/// </summary>
public class ParquetDailySummaryData
{
    /// <summary>
    /// Date in YYYY-MM-DD format (as string for Parquet compatibility)
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
    
    /// <summary>
    /// Year (YYYY)
    /// </summary>
    [JsonPropertyName("year")]
    public int Year { get; set; }
    
    /// <summary>
    /// Month (MM)
    /// </summary>
    [JsonPropertyName("month")]
    public int Month { get; set; }
    
    /// <summary>
    /// Day (DD)
    /// </summary>
    [JsonPropertyName("day")]
    public int Day { get; set; }
    
    /// <summary>
    /// Service Owner ID (organization number)
    /// </summary>
    [JsonPropertyName("serviceownerorgnr")]
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name, e.g. digdir, brreg, kv, etc.
    /// </summary>
    [JsonPropertyName("serviceownercode")]
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID
    /// </summary>
    [JsonPropertyName("serviceresourceid")]
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource title in Norwegian (from Resource Registry)
    /// </summary>
    [JsonPropertyName("serviceresourcetitle")]
    public string ResourceTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient type (Organization or Person)
    /// </summary>
    [JsonPropertyName("recipienttype")]
    public string RecipientType { get; set; } = string.Empty;
    
    /// <summary>
    /// Altinn version (Altinn2, Altinn3)
    /// </summary>
    [JsonPropertyName("costcenter")]
    public string AltinnVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of file transfers for this service owner on this date
    /// </summary>
    [JsonPropertyName("messagecount")]
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Total database storage used (metadata) in bytes
    /// </summary>
    [JsonPropertyName("databasestoragebytes")]
    public long DatabaseStorageBytes { get; set; }
    
    /// <summary>
    /// Total file storage used in bytes
    /// </summary>
    [JsonPropertyName("attachmentstoragebytes")]
    public long AttachmentStorageBytes { get; set; }

}

