using Altinn.Broker.Core.Enums;

namespace Altinn.Broker.Core.Models
{
    public class BrokerShipmentStatusOverview
    {
        public Guid BrokerResourceId {get;set;}
        public Guid ShipmentId {get;set;}
        public string Sender{get;set;} = string.Empty;
        public string SendersShipmentReference {get;set;} = string.Empty;
        public List<RecipientShipmentStatusOverview> RecipientStatusList {get;set;} = new List<RecipientShipmentStatusOverview>();
        public Dictionary<string, string> Metadata{get;set;} = new Dictionary<string, string>();
        public DateTime ShipmentInitialized {get;set;}
        public BrokerShipmentStatus CurrentShipmentStatus {get;set;}
        public string CurrentShipmentStatusText{get;set;} = string.Empty;
        public DateTime CurrentShipmentStatusChanged {get;set;}
        public List<BrokerFileStatusOverview> FileList {get;set;} = new List<BrokerFileStatusOverview>();
    }

    public class BrokerFileStatusOverview
    {
        public Guid FileId {get;set;}
        public string FileName {get;set;}=string.Empty;
        public string SendersFileReference {get;set;}=string.Empty;
        public string Checksum{get;set;}=string.Empty;
        public BrokerFileStatus FileStatus {get;set;}
        public string FileStatusText {get;set;} = string.Empty;
        public DateTime FileStatusChanged{get;set;}
    }

    public class BrokerShipmentStatusDetails : BrokerShipmentStatusOverview
    {
        public new List<BrokerFileStatusDetails> FileList {get;set;} = new List<BrokerFileStatusDetails>();
        public List<BrokerShipmentStatusEvent> ShipmentStatusHistory {get;set;} = new List<BrokerShipmentStatusEvent>();
    }

    public class BrokerShipmentStatusEvent
    {
        public BrokerShipmentStatus ShipmentStatus {get;set;}
        public string ShipmentStatusText {get;set;}=string.Empty;
        public DateTime ShipmentStatusChanged{get;set;}
    }

    public class BrokerFileStatusDetails : BrokerFileStatusOverview
    {
        public List<FileStatusEvent> FileStatusHistory{get;set;} = new List<FileStatusEvent>();
    }

    public class BrokerFileStatusDetailsExt : BrokerFileStatusDetails
    {
        public new List<FileStatusEventExt> FileStatusHistory {get;set;} = new List<FileStatusEventExt>();
    }

    public class FileStatusEventExt : FileStatusEvent
    {
    }

    public class FileStatusEvent
    {
        public BrokerFileStatus FileStatus {get;set;}
        public string FileStatusText{get;set;}=string.Empty;
        public DateTime FileStatusChanged{get;set;}
    }

    public class RecipientShipmentStatusOverview
    {
        public string Recipient {get;set;} = string.Empty;
        public RecipientShipmentStatus CurrentRecipientShipmentStatusCode {get;set; }
        public string CurrentrecipientShipmentStatusText {get;set;} = string.Empty;
        public DateTime CurrentRecipientShipmentStatusChanged {get;set;}
    }

    public enum RecipientShipmentStatus
    {
        Initialized,
        Published,
        Downloaded,
        ConfirmedAllDownloaded,
        Cancelled
    }
}