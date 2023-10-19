using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;

namespace Altinn.Broker.Models
{
    /// <summary>
    /// Entity containing Broker Service Instance metadata
    /// This describes the initiation of a Broker Service and is used in conjunction with a file sender uploading a file.
    /// </summary>
    public class BrokerFileStatusOverviewExt
    {
        public Guid FileId {get;set;}
        public string FileName {get;set;}=string.Empty;
        public string SendersFileReference {get;set;}=string.Empty;
        public string Checksum{get;set;}=string.Empty;
        public BrokerFileStatusExt FileStatus {get;set;}
        public string FileStatusText {get;set;} = string.Empty;
        public DateTime FileStatusChanged{get;set;}
    }
}