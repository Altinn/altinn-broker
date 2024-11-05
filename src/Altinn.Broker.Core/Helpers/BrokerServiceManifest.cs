using Altinn.Broker.Core.Domain;
using System.Xml.Serialization;

namespace Altinn.Broker.Core.Helpers;
[XmlRoot("BrokerServiceManifest", Namespace = "http://schema.altinn.no/services/ServiceEngine/Broker/2015/06")]
public class BrokerServiceManifest
{
    public BrokerServiceManifest()
    {
        FileList = new List<FileEntry>();
        PropertyList = new List<PropertyEntry>();
    }

    [XmlElement("ExternalServiceCode")]
    public string ExternalServiceCode { get; set; }

    [XmlElement("ExternalServiceEditionCode")]
    public string ExternalServiceEditionCode { get; set; }

    [XmlElement("SendersReference")]
    public string SendersReference { get; set; }

    [XmlElement("Reportee")]
    public string Reportee { get; set; }

    [XmlElement("SentDate")]
    public DateTime SentDate { get; set; }

    [XmlArray("FileList")]
    [XmlArrayItem("File")]
    public List<FileEntry> FileList { get; set; }

    [XmlArray("PropertyList")]
    [XmlArrayItem("Property")]
    public List<PropertyEntry> PropertyList { get; set; }
}

[XmlType(Namespace = "http://schema.altinn.no/services/ServiceEngine/Broker/2015/06")]
public class FileEntry
{
    [XmlElement("FileName")]
    public string FileName { get; set; }
}

[XmlType(Namespace = "http://schema.altinn.no/services/ServiceEngine/Broker/2015/06")]
public class PropertyEntry
{
    [XmlElement("PropertyKey")]
    public string PropertyKey { get; set; }

    [XmlElement("PropertyValue")]
    public string PropertyValue { get; set; }
}

public static class BrokerServiceManifestExtensions
{
    public static BrokerServiceManifest CreateManifest(this FileTransferEntity entity, ResourceEntity resource)
    {
        var manifest = new BrokerServiceManifest
        {
            
            ExternalServiceCode = (bool)resource.UseManifestFileShim ? resource.ExternalServiceCodeLegacy : null,
            ExternalServiceEditionCode = (bool)resource.UseManifestFileShim ? resource.ExternalServiceEditionCodeLegacy : null,
            SendersReference = entity.SendersFileTransferReference,
            Reportee = entity.RecipientCurrentStatuses.First().Actor.ActorExternalId,
            SentDate = DateTime.UtcNow,
            FileList = new List<FileEntry>
                {
                    new FileEntry { FileName = entity.FileName }
                }
        };

        if (entity.PropertyList != null)
        {
            manifest.PropertyList = entity.PropertyList.Select(p => new PropertyEntry
            {
                PropertyKey = p.Key,
                PropertyValue = p.Value
            }).ToList();
        }

        return manifest;
    }
}
