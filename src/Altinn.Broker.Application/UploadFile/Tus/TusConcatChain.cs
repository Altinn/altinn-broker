namespace Altinn.Broker.Application.UploadFile.Tus;

public enum TusConcatChainStep
{
    ValidatePartials = 0,
    PrepareCommit = 1,
    CommitDestination = 2,
    Cleanup = 3
}

/// <summary>
/// Progress through the concatenation chain, persisted so a retry resumes rather than restarts.
/// Losing it costs time, not correctness: the chain re-detects committed stripes by their length.
/// New fields must be appended, so checkpoints written by an earlier version stay readable.
/// </summary>
public sealed record TusConcatCheckpoint(
    TusConcatChainStep NextStep = TusConcatChainStep.ValidatePartials,
    int ValidatedPartialCount = 0,
    long TotalValidatedLength = 0,
    int BlockCount = 0,
    long StagedLength = 0,
    long StripeSizeBytes = 0,
    int StripeCount = 0,
    int PreparedStripeCount = 0,
    int CommittedStripeCount = 0);

public sealed record TusConcatChainStepResult(
    bool StepCompleted,
    bool ChainComplete,
    bool ShouldRetryStep,
    TusConcatChainStep? NextStep = null);
