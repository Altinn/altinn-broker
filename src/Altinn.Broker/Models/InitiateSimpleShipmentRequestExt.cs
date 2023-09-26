using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Instance metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class InitiateSimpleShipmentRequestExt
    {
        public InitiateSimpleShipmentRequestExt()
        {
            Metadata = new InitiateBrokerShipmentRequestExt();
        }
        public IFormFile File {get;set;}
        public InitiateBrokerShipmentRequestExt Metadata {get;set;}
    }
}