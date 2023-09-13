
namespace Altinn.Broker.Models;

public class ShipmentMetadata
    {
        public string ServiceCode { get; set; }
        public int ServiceEdtionCode { get; set; }
        public string FileName { get; set; }
        public string FileReference { get; set; }
        public int FileSize { get; set; }
        public string FileStatus { get; set; }
        public int ReceiptID { get; set; }
        public string Sender { get; set; }
        public string SendersReference { get; set; }
        public string SentDate { get; set; }
    }