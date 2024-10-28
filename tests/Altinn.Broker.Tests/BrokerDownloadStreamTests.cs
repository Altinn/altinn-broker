using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Altinn.Broker.Core.Helpers;
using Altinn.Broker.Tests.Factories;

using Xunit;

namespace Altinn.Broker.Tests;
public class BrokerDownloadStreamTests
{

    [Fact]
    public async Task StreamWithoutManifest_AddManifest_ManifestIsAddedAsync()
    {
        var stream = ReadFile("Data/ManifestFileTests/Payload.zip");
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        await stream.AddManifestFile(file);
        Assert.True(stream.Length > originalFileLength);
        var brokerManifest = GetBrokerManifest(stream);
        Assert.Equal(brokerManifest.Reportee, file.RecipientCurrentStatuses.First().Actor.ActorExternalId);
        Assert.Equal(brokerManifest.SendersReference, file.SendersFileTransferReference);
    }

    [Fact]
    public async Task StreamWithManifest_AddManifest_ExistingManifestIsReplacedAsync()
    {
        var stream = ReadFile("Data/ManifestFileTests/PayloadWithExistingManifest.zip");
        var originalBrokerManifest = GetBrokerManifest(stream);
        var originalFileLength = stream.Length;
        var file = FileTransferEntityFactory.BasicFileTransfer();
        await stream.AddManifestFile(file);
        var newBrokerManifest = GetBrokerManifest(stream);
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

    private DownloadStream ReadFile(string path)
    {
        var fileStream = File.OpenRead(path);
        var fileBuffer = new byte[fileStream.Length];
        fileStream.Read(fileBuffer, 0, fileBuffer.Length);
        return new DownloadStream(fileBuffer);
    }

    private BrokerServiceManifest GetBrokerManifest(DownloadStream downloadStream)
    {
        using (var archive = new ZipArchive(downloadStream, ZipArchiveMode.Read, true))
        {
            var manifestEntry = archive.GetEntry("Manifest.xml");
            using (var manifestStream = manifestEntry.Open())
            using (var memoryStream = new MemoryStream())
            {
                manifestStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                using (var reader = new StreamReader(memoryStream, Encoding.Unicode))
                {
                    var xmlContent = reader.ReadToEnd();
                    xmlContent = xmlContent.Substring(xmlContent.IndexOf("<BrokerServiceManifest"));
                    using (var cleanStream = new MemoryStream(Encoding.Unicode.GetBytes(xmlContent)))
                    {
                        var serializer = new XmlSerializer(
                            typeof(BrokerServiceManifest),
                            new XmlRootAttribute
                            {
                                ElementName = "BrokerServiceManifest",
                                Namespace = "http://schema.altinn.no/services/ServiceEngine/Broker/2015/06"
                            });

                        return serializer.Deserialize(cleanStream) as BrokerServiceManifest;
                    }
                }
            }
        }
    }
}
