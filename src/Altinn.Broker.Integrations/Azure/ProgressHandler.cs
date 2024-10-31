namespace Altinn.Broker.Integrations.Azure;
internal class ProgressHandler() : IProgress<long>
{

    public void Report(long value)
    {
        Console.WriteLine($"Progress: {value.ToString("N0")}");
    }
}
