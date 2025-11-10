using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Application.GenerateReport;

/// <summary>
/// Aggregated daily summary data for cost allocation and reporting.
/// Each row represents one day's usage for one service owner.
/// </summary>
public class DailySummaryData
{
    /// <summary>
    /// Date in YYYY-MM-DD format
    /// </summary>
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Year (YYYY)
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Month (MM)
    /// </summary>
    public int Month { get; set; }
    
    /// <summary>
    /// Day (DD)
    /// </summary>
    public int Day { get; set; }
    
    /// <summary>
    /// Service Owner ID (organization number)
    /// </summary>
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name (for readability)
    /// </summary>
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Message sender (sender actor external ID)
    /// </summary>
    public string MessageSender { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource title in Norwegian (from Resource Registry)
    /// </summary>
    public string ResourceTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient type (Organization or Person)
    /// </summary>
    public RecipientType RecipientType { get; set; }
    
    /// <summary>
    /// Altinn version (Altinn2 or Altinn3)
    /// Note: Broker only supports Altinn3
    /// </summary>
    public AltinnVersion AltinnVersion { get; set; }
    
    /// <summary>
    /// Number of file transfers for this service owner on this date
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Total database storage used (metadata) in bytes
    /// </summary>
    public long DatabaseStorageBytes { get; set; }
    
    /// <summary>
    /// Total file storage used in bytes
    /// </summary>
    public long AttachmentStorageBytes { get; set; }

}

