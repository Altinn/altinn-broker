using Altinn.Broker.Integrations.Tus;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Xunit;

namespace Altinn.Broker.Tests.Tus;

public class TusPartialUploadRegistryTests
{
    private const string FileTransferId = "c28803a5-c986-4bb6-ac28-b47aafffad16";
    private const string PartialUploadId = "43b05d2efb1c444eaff5c53e1cbee918";

    private static TusPartialUploadRegistry CreateRegistry()
        => new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task TryGetFileTransferIdAsync_UnknownPartialId_DoesNotReturnPartialIdAsFileTransferId()
    {
        var registry = CreateRegistry();

        var fileTransferId = await registry.TryGetFileTransferIdAsync(PartialUploadId, CancellationToken.None);

        Assert.Null(fileTransferId);
    }

    [Fact]
    public async Task TryGetFileTransferIdAsync_RegisteredPartial_ReturnsParentFileTransferId()
    {
        var registry = CreateRegistry();
        await registry.RegisterPartialAsync(
            PartialUploadId,
            Guid.Parse(FileTransferId),
            uploadLength: 1024,
            CancellationToken.None);

        var fileTransferId = await registry.TryGetFileTransferIdAsync(PartialUploadId, CancellationToken.None);

        Assert.Equal(Guid.Parse(FileTransferId), fileTransferId);
    }
}
