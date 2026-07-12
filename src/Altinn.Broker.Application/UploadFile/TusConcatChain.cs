namespace Altinn.Broker.Application.UploadFile;

public enum TusConcatChainStep
{
    ValidatePartials = 0,
    PrepareCommit = 1,
    CommitDestination = 2,
    Cleanup = 3
}

public sealed record TusConcatCheckpoint(
    TusConcatChainStep NextStep = TusConcatChainStep.ValidatePartials,
    int ValidatedPartialCount = 0,
    long TotalValidatedLength = 0,
    int BlockCount = 0,
    long StagedLength = 0);

public sealed record TusConcatChainStepResult(
    bool StepCompleted,
    bool ChainComplete,
    bool ShouldRetryStep,
    TusConcatChainStep? NextStep = null);
