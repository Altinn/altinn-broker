namespace Altinn.Broker.Application.CleanupUseCaseTests;

public class CleanupUseCaseTestsResponse
{
    public required string ResourceId { get; set; }
    public required string TestTag { get; set; }
    public required int FileTransfersFound { get; set; }
    public required string DeleteFileTransfersJobId { get; set; } 
}