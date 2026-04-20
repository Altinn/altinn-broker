using System.ComponentModel.DataAnnotations;

namespace Altinn.Broker.Application.CleanupUseCaseTests;
public class CleanupUseCaseTestsRequest
{
    [Range(0, 365)]
    public int? MinAgeDays { get; set; }
}