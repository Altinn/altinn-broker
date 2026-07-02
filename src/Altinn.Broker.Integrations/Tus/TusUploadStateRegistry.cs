using System.Collections.Concurrent;

namespace Altinn.Broker.Integrations.Tus;

public interface ITusUploadStateRegistry
{
    TusUploadState GetOrAdd(string fileId, Func<TusUploadState> factory);

    bool TryGet(string fileId, out TusUploadState? state);

    void Remove(string fileId);
}

public sealed class TusUploadStateRegistry : ITusUploadStateRegistry
{
    private readonly ConcurrentDictionary<string, Lazy<TusUploadState>> _states = new(StringComparer.OrdinalIgnoreCase);

    public TusUploadState GetOrAdd(string fileId, Func<TusUploadState> factory)
        => _states.GetOrAdd(fileId, _ => new Lazy<TusUploadState>(factory)).Value;

    public bool TryGet(string fileId, out TusUploadState? state)
    {
        if (_states.TryGetValue(fileId, out var lazy) && lazy.IsValueCreated)
        {
            state = lazy.Value;
            return true;
        }

        state = null;
        return false;
    }

    public void Remove(string fileId)
    {
        if (_states.TryRemove(fileId, out var lazy) && lazy.IsValueCreated)
        {
            lazy.Value.Dispose();
        }
    }
}
