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
        var stream = ReadFile("Data/ManifestFileTests/Payload.zip");
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        await stream.AddManifestFile(file);
        Assert.True(stream.Length > originalFileLength);
        var brokerManifest = stream.GetBrokerManifest();
        Assert.Equal(brokerManifest.Reportee, file.RecipientCurrentStatuses.First().Actor.ActorExternalId);
        Assert.Equal(brokerManifest.SendersReference, file.SendersFileTransferReference);
    }

    [Fact]
    public async Task StreamWithManifest_AddManifest_ExistingManifestIsReplacedAsync()
    {
        var stream = ReadFile("Data/ManifestFileTests/PayloadWithExistingManifest.zip");
        var originalBrokerManifest = stream.GetBrokerManifest();
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        await stream.AddManifestFile(file);
        var newBrokerManifest = stream.GetBrokerManifest();
        Assert.NotEqual(stream.Length, originalFileLength);
        Assert.NotEqual(originalBrokerManifest.Reportee, newBrokerManifest.Reportee);
        Assert.NotEqual(originalBrokerManifest.SendersReference, newBrokerManifest.SendersReference);
        Assert.NotEqual(originalBrokerManifest.SentDate, newBrokerManifest.SentDate);
        Assert.Equal(newBrokerManifest.Reportee, file.RecipientCurrentStatuses.First().Actor.ActorExternalId);
        Assert.Equal(newBrokerManifest.SendersReference, file.SendersFileTransferReference);
    }

    [Fact]
    public async Task StreamThatIsNotZip_AddManifest_FailsAsync()
    {
        var stream = ReadFile("Data/ManifestFileTests/payload.txt");    
        var file = FileTransferEntityFactory.BasicFileTransfer();
        await Assert.ThrowsAsync<InvalidOperationException>(() => stream.AddManifestFile(file));
    }

    private ManifestDownloadStream ReadFile(string path)
    {
        var fileStream = File.OpenRead(path);
        var fileBuffer = new byte[fileStream.Length];
        fileStream.Read(fileBuffer, 0, fileBuffer.Length);
        return new ManifestDownloadStream(fileBuffer);
    }
}
