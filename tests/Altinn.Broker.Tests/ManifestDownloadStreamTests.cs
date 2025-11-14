using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.Tests.Helpers;

using Xunit;

namespace Altinn.Broker.Tests;
public class ManifestDownloadStreamTests
{

    [Fact]
    public async Task StreamWithoutManifest_AddManifest_ManifestIsAddedAsync()
    {
        var resource = new ResourceEntity
        {
            Id = "manifest-shim-resource",
            ServiceOwnerId = "someServiceOwnerId",
            UseManifestFileShim = true,
            ExternalServiceCodeLegacy = "someExternalServiceCode",
            ExternalServiceEditionCodeLegacy = 123
        };
        var stream = ReadFile("Data/ManifestFileTests/Payload.zip");
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        DateTime expectedSentDate = file.Created.ToLocalTime().DateTime;
        await stream.AddManifestFile(file, resource);
        Assert.True(stream.Length > originalFileLength);
        var brokerManifest = stream.GetBrokerManifest();
        Assert.NotNull(brokerManifest);
        Assert.Equal("991825827", brokerManifest.Reportee);
        Assert.Equal(brokerManifest.SendersReference, file.SendersFileTransferReference);
        Assert.Equal(brokerManifest.SentDate, expectedSentDate);
    }

    [Fact]
    public async Task StreamWithEmptyZip_AddManifest_ManifestIsAddedAsync()
    {
        var resource = new ResourceEntity
        {
            Id = "manifest-shim-resource",
            ServiceOwnerId = "someServiceOwnerId",
            UseManifestFileShim = true,
            ExternalServiceCodeLegacy = "someExternalServiceCode",
            ExternalServiceEditionCodeLegacy = 123
        };
        var stream = ReadFile("Data/ManifestFileTests/Empty.zip");
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        DateTime expectedSentDate = file.Created.ToLocalTime().DateTime;
        await stream.AddManifestFile(file, resource);
        Assert.True(stream.Length > originalFileLength);
        var brokerManifest = stream.GetBrokerManifest();
        Assert.NotNull(brokerManifest);
        Assert.Equal("991825827", brokerManifest.Reportee);
        Assert.Equal(brokerManifest.SendersReference, file.SendersFileTransferReference);
        Assert.Equal(brokerManifest.SentDate, expectedSentDate);
    }

    [Fact]
    public async Task StreamWithManifest_AddManifest_ExistingManifestIsReplacedAsync()
    {
        var resource = new ResourceEntity
        {
            Id = "manifest-shim-resource",
            ServiceOwnerId = "someServiceOwnerId",
            UseManifestFileShim = true,
            ExternalServiceCodeLegacy = "someExternalServiceCode",
            ExternalServiceEditionCodeLegacy = 123
        };
        var stream = ReadFile("Data/ManifestFileTests/PayloadWithExistingManifest.zip");
        var originalBrokerManifest = stream.GetBrokerManifest();
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        DateTime expectedSentDate = file.Created.ToLocalTime().DateTime;
        await stream.AddManifestFile(file, resource);
        var newBrokerManifest = stream.GetBrokerManifest();
        Assert.NotEqual(stream.Length, originalFileLength);
        Assert.NotEqual(originalBrokerManifest.Reportee, newBrokerManifest.Reportee);
        Assert.NotEqual(originalBrokerManifest.SendersReference, newBrokerManifest.SendersReference);
        Assert.NotEqual(originalBrokerManifest.SentDate, newBrokerManifest.SentDate);        
        Assert.Equal("991825827", newBrokerManifest.Reportee);
        Assert.Equal(newBrokerManifest.SendersReference, file.SendersFileTransferReference);
        Assert.Equal(newBrokerManifest.SentDate, expectedSentDate);
    }

    [Fact]
    public async Task StreamThatIsNotZip_AddManifest_FailsAsync()
    {
        var resource = new ResourceEntity
        {
            Id = "manifest-shim-resource",
            ServiceOwnerId = "someServiceOwnerId",
            UseManifestFileShim = true,
            ExternalServiceCodeLegacy = "someExternalServiceCode",
            ExternalServiceEditionCodeLegacy = 123
        };
        var stream = ReadFile("Data/ManifestFileTests/payload.txt");    
        var file = FileTransferEntityFactory.BasicFileTransfer();
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.AddManifestFile(file, resource));
    }

    private ManifestDownloadStream ReadFile(string path)
    {
        var fileStream = File.OpenRead(path);
        var fileBuffer = new byte[fileStream.Length];
        fileStream.Read(fileBuffer, 0, fileBuffer.Length);
        return new ManifestDownloadStream(fileBuffer);
    }
}
