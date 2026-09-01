using System.Security.Cryptography;

namespace Altinn.Broker.Integrations.Tus;

public sealed class TusUploadState : IDisposable
{
    public TusUploadState(long uploadLength, long initialOffset, int maxParallelBlockUploads)
    {
        UploadLength = uploadLength;
        AcceptedOffset = initialOffset;
        CommittedOffset = initialOffset;
        ProgressSignal = NewProgressSignal();
        ConcurrentUploader = new SemaphoreSlim(Math.Max(maxParallelBlockUploads, 1));
        UploadMd5 = MD5.Create();
    }

    public object SyncRoot { get; } = new();

    public long UploadLength { get; }

    public long AcceptedOffset { get; set; }

    public long CommittedOffset { get; set; }

    public int PendingUploads { get; set; }

    public long NextBlockIndex { get; set; }

    public Exception? Fault { get; set; }

    public TaskCompletionSource<long> ProgressSignal { get; set; }

    public SemaphoreSlim ConcurrentUploader { get; }

    public MD5 UploadMd5 { get; }

    public void Dispose()
    {
        UploadMd5.Dispose();
        ConcurrentUploader.Dispose();
    }

    private static TaskCompletionSource<long> NewProgressSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
