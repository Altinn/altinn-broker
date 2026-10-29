using Altinn.Broker.Integrations.Tus;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Shared switch for injecting Azure staging failures into TUS uploads during integration tests.
/// </summary>
public sealed class TusStagingFailureController
{
    private int _remainingFailures;

    public void FailNextStages(int count)
        => Interlocked.Exchange(ref _remainingFailures, Math.Max(count, 0));

    public void Clear()
        => Interlocked.Exchange(ref _remainingFailures, 0);

    public bool TryConsumeFailure()
    {
        while (true)
        {
            var remaining = Volatile.Read(ref _remainingFailures);
            if (remaining <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _remainingFailures, remaining - 1, remaining) == remaining)
            {
                return true;
            }
        }
    }
}
