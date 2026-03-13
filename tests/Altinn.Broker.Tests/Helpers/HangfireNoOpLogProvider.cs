using Hangfire.Logging;

namespace Altinn.Broker.Tests.Helpers;

/// <summary>
/// Test-only Hangfire log provider that ignores all log messages.
/// This avoids using the default AspNetCoreLogProvider, which depends on
/// the ASP.NET Core LoggerFactory lifetime and can be disposed between tests.
/// </summary>
public sealed class HangfireNoOpLogProvider : ILogProvider
{
    private sealed class NoOpLogger : ILog
    {
        public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception = null)
        {
            // Returning true indicates the message has been "logged".
            return true;
        }
    }

    private static readonly ILog _logger = new NoOpLogger();

    public ILog GetLogger(string name) => _logger;
}

