namespace Altinn.Broker.Core.Domain;

/// <summary>
/// Result of a file download from storage. When a range was requested, <see cref="Content"/> contains only
/// the requested segment while <see cref="TotalLength"/> is the full size of the stored file.
/// </summary>
public record BrokerFileDownload(
    Stream Content,
    long TotalLength,
    long SegmentLength,
    string? ETag
);
