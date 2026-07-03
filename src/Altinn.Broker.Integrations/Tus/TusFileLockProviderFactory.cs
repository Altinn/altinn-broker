using StackExchange.Redis;

using tusdotnet.FileLocks;
using tusdotnet.Interfaces;

namespace Altinn.Broker.Integrations.Tus;

public static class TusFileLockProviderFactory
{
    public static ITusFileLockProvider Create(IConnectionMultiplexer? redis)
        => redis is not null
            ? new RedisTusFileLockProvider(redis)
            : InMemoryFileLockProvider.Instance;
}
