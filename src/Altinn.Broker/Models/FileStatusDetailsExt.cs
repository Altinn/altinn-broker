using Altinn.Broker.Models;

namespace Altinn.Broker.Core.Models
{
    public class FileStatusDetailsExt : FileStatusOverviewExt
    {
        public List<FileStatusEventExt> FileStatusHistory {get;set;} = new List<FileStatusEventExt>();
    }
}