namespace Altinn.Broker.Core.Helpers;
public static class SanitizerMethods
{
    public static string SanitizeForLogs(this string input) => input.Replace(Environment.NewLine, null);
}
