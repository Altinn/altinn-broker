namespace Altinn.Broker.Models
{
    public class BrokerServiceDescription {
        public string ServiceCode { get; set; }
        public int ServiceEditionCode { get; set; }
        public string SendersReference { get; set; }
        public List<string> Recipients { get; set; }
        public Dictionary<string, string> Properties { get; set; }
        public List<BrokerServiceDescriptionFileEntry> FileList { get; set; }
    }

    public class BrokerServiceDescriptionFileEntry {
        public string FileName { get; set; }
        public string Checksum { get; set; }
    }
}