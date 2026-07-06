using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Altinn.Broker.Integrations.Tus;

/// <summary>
/// Emits LogDebug step timings for TUS upload diagnostics.
/// StepMs is the delta since the previous step; TotalMs is elapsed since Start.
/// </summary>
internal sealed class TusUploadDebugTiming : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly string _fileId;
    private readonly int? _chunkBytes;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastCheckpointMs;

    private TusUploadDebugTiming(
        ILogger logger,
        string operation,
        string fileId,
        int? chunkBytes)
    {
        _logger = logger;
        _operation = operation;
        _fileId = fileId;
        _chunkBytes = chunkBytes;
    }

    public static TusUploadDebugTiming Start(
        ILogger logger,
        string operation,
        string fileId,
        int? chunkBytes = null)
    {
        logger.LogDebug(
            "TUS timing {Operation} started fileId={FileId} chunkBytes={ChunkBytes}",
            operation,
            fileId,
            chunkBytes);

        return new TusUploadDebugTiming(logger, operation, fileId, chunkBytes);
    }

    public void Step(string step, object? detail = null)
    {
        var totalMs = _stopwatch.ElapsedMilliseconds;
        var stepMs = totalMs - _lastCheckpointMs;
        _lastCheckpointMs = totalMs;

        if (detail is null)
        {
            _logger.LogDebug(
                "TUS timing {Operation} {Step} +{StepMs}ms total {TotalMs}ms fileId={FileId}",
                _operation,
                step,
                stepMs,
                totalMs,
                _fileId);
            return;
        }

        _logger.LogDebug(
            "TUS timing {Operation} {Step} +{StepMs}ms total {TotalMs}ms fileId={FileId} detail={Detail}",
            _operation,
            step,
            stepMs,
            totalMs,
            _fileId,
            detail);
    }

    public void Dispose()
    {
        _logger.LogDebug(
            "TUS timing {Operation} complete {TotalMs}ms fileId={FileId} chunkBytes={ChunkBytes}",
            _operation,
            _stopwatch.ElapsedMilliseconds,
            _fileId,
            _chunkBytes);
    }
}
