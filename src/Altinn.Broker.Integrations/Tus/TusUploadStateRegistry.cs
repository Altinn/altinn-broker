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
    private readonly ConcurrentDictionary<string, TusUploadState> _states = new(StringComparer.OrdinalIgnoreCase);

    public TusUploadState GetOrAdd(string fileId, Func<TusUploadState> factory)
        => _states.GetOrAdd(fileId, _ => factory());

    public bool TryGet(string fileId, out TusUploadState? state)
        => _states.TryGetValue(fileId, out state);

    public void Remove(string fileId)
    {
        if (_states.TryRemove(fileId, out var state))
        {
            state.Dispose();
        }
    }
}
