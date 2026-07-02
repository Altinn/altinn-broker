using System.Collections.Concurrent;

namespace Altinn.Broker.Integrations.Tus;

public interface ITusPartialUploadRegistry
{
    void RegisterPartial(string partialFileId, Guid fileTransferId, long uploadLength);

    bool TryGetPartialInfo(string partialFileId, out Guid fileTransferId, out long uploadLength);

    bool IsPartial(string fileId);

    bool TryGetFileTransferId(string tusFileId, out Guid fileTransferId);

    void RemovePartial(string partialFileId);
}

public sealed class TusPartialUploadRegistry : ITusPartialUploadRegistry
{
    private readonly ConcurrentDictionary<string, PartialUploadInfo> _partials = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterPartial(string partialFileId, Guid fileTransferId, long uploadLength)
        => _partials[partialFileId] = new PartialUploadInfo(fileTransferId, uploadLength);

    public bool TryGetPartialInfo(string partialFileId, out Guid fileTransferId, out long uploadLength)
    {
        if (_partials.TryGetValue(partialFileId, out var info))
        {
            fileTransferId = info.FileTransferId;
            uploadLength = info.UploadLength;
            return true;
        }

        fileTransferId = default;
        uploadLength = 0;
        return false;
    }

    public bool IsPartial(string fileId) => _partials.ContainsKey(fileId);

    public bool TryGetFileTransferId(string tusFileId, out Guid fileTransferId)
    {
        if (_partials.TryGetValue(tusFileId, out var info))
        {
            fileTransferId = info.FileTransferId;
            return true;
        }

        fileTransferId = default;
        return false;
    }

    public void RemovePartial(string partialFileId) => _partials.TryRemove(partialFileId, out _);

    private sealed record PartialUploadInfo(Guid FileTransferId, long UploadLength);
}
