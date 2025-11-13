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
    /// Source: service_owner.service_owner_id_pk
    /// </summary>
    [JsonPropertyName("service_owner_id_pk")]
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name, e.g. digdir, brreg, kv, etc.
    /// Source: service_owner.service_owner_name
    /// </summary>
    [JsonPropertyName("service_owner_name")]
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID
    /// Source: file_transfer.resource_id
    /// </summary>
    [JsonPropertyName("resource_id")]
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource title (service owner name from Resource Registry)
    /// Source: AltinnResourceService (Resource Registry API)
    /// </summary>
    [JsonPropertyName("altinn_resource_service")]
    public string ResourceTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient type (Organization, Person, or Unknown)
    /// </summary>
    [JsonPropertyName("recipient_type")]
    public string RecipientType { get; set; } = string.Empty;
    
    /// <summary>
    /// Altinn version (always Altinn3 for Broker)
    /// </summary>
    [JsonPropertyName("altinn_version")]
    public string AltinnVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of file transfers for this service owner on this date
    /// </summary>
    [JsonPropertyName("filetransfercount")]
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Total database storage used (metadata) in bytes
    /// </summary>
    [JsonPropertyName("databasestoragebytes")]
    public long DatabaseStorageBytes { get; set; }
    
    /// <summary>
    /// Total file storage used in bytes
    /// </summary>
    [JsonPropertyName("filestoragebytes")]
    public long AttachmentStorageBytes { get; set; }

}

