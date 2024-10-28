using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Altinn.Broker.Core.Helpers;

namespace Altinn.Broker.Tests.Helpers;
internal static class ManifestDownloadStreamHelpers
{
    internal static BrokerServiceManifest? GetBrokerManifest(this ManifestDownloadStream downloadStream)
    {
        using (var archive = new ZipArchive(downloadStream, ZipArchiveMode.Read, true))
        {
            var manifestEntry = archive.GetEntry("Manifest.xml");
            if (manifestEntry is null)
            {
                return null;
            }
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
