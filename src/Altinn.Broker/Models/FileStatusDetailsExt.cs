using Altinn.Broker.Models;

namespace Altinn.Broker.Core.Models
{
    public class FileStatusDetailsExt : FileOverviewExt
    {
        public List<FileStatusEventExt> FileStatusHistory { get; set; } = new List<FileStatusEventExt>();
        public List<RecipientFileStatusEventExt> RecipientFileStatusHistory { get; set; } = new List<RecipientFileStatusEventExt>();
    }
}