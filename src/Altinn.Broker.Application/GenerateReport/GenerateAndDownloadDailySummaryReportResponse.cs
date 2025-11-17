namespace Altinn.Broker.Application.GenerateReport;

public class GenerateAndDownloadDailySummaryReportResponse
{
    /// <summary>
    /// The parquet file stream
    /// </summary>
    public required Stream FileStream { get; set; }
    
    /// <summary>
    /// The filename for the download
    /// </summary>
    public required string FileName { get; set; }
    
    /// <summary>
    /// MD5 hash of the file
    /// </summary>
    public required string FileHash { get; set; }
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Number of service owners included in the report
    /// </summary>
    public int ServiceOwnerCount { get; set; }
    
    /// <summary>
    /// Total number of file transfers included in the report
    /// </summary>
    public int TotalFileTransferCount { get; set; }
    
    /// <summary>
    /// When the report was generated (UTC)
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }
    
    /// <summary>
    /// Environment where the report was generated
    /// </summary>
    public required string Environment { get; set; }
    
    /// <summary>
    /// Whether Altinn2 file transfers were included
    /// Note: Broker only supports Altinn3, so this will always be false.
    /// </summary>
    public bool Altinn2Included { get; set; }
}

