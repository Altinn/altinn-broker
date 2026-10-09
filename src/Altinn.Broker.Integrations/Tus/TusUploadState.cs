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
        InflightChangedSignal = NewInflightChangedSignal();
        ConcurrentUploader = new SemaphoreSlim(Math.Max(maxParallelBlockUploads, 1));
        UploadMd5 = MD5.Create();
    }

    public object SyncRoot { get; } = new();

    public long UploadLength { get; }

    public long AcceptedOffset { get; set; }

    public long CommittedOffset { get; set; }

    public int PendingUploads { get; set; }

    /// <summary>
    /// Number of accepted block uploads that have not finished their full UploadBlockAsync work,
    /// including <c>IncrementCommittedOffsetAsync</c> after Azure staging. Unlike
    /// <see cref="PendingUploads"/>, this stays elevated until cache updates complete.
    /// </summary>
    public int InflightBlockOperations { get; set; }

    public long NextBlockIndex { get; set; }

    public Exception? Fault { get; set; }

    /// <summary>
    /// True while one failed <c>UploadBlockAsync</c> owns draining siblings and reconciling
    /// AcceptedOffset. Prevents concurrent failures from all waiting on the inflight count.
    /// </summary>
    public bool OffsetReconcileElected { get; set; }

    public TaskCompletionSource<long> ProgressSignal { get; set; }

    public TaskCompletionSource InflightChangedSignal { get; set; }

    public SemaphoreSlim ConcurrentUploader { get; }

    public MD5 UploadMd5 { get; }

    public void Dispose()
    {
        UploadMd5.Dispose();
        ConcurrentUploader.Dispose();
    }

    private static TaskCompletionSource<long> NewProgressSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource NewInflightChangedSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
