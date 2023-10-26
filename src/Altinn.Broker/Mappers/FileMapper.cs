using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers
{
    public static class FileMapper
    {
        public static List<FileStatusDetailsExt> MapToExternal(this List<BrokerFileStatusDetails> i)
        {
            return i.Select(fsd => fsd.MapToExternal()).ToList();
        }
        public static FileStatusDetailsExt MapToExternal(this BrokerFileStatusDetails ibfd)
        {
            return new FileStatusDetailsExt()
            {
                Checksum = ibfd.Checksum,
                FileId = ibfd.FileId,
                FileName = ibfd.FileName,
                FileStatus =  (FileStatusExt)ibfd.FileStatus,
                FileStatusChanged = ibfd.FileStatusChanged,
                FileStatusHistory = ibfd.FileStatusHistory.MapToExternal(),
                FileStatusText = ibfd.FileStatusText,
                SendersFileReference = ibfd.SendersFileReference
            };
        }        
       public static List<FileStatusOverviewExt> MapToExternal(this List<BrokerFileStatusOverview> i)
        {
            return i.Select(o => o.MapToExternal()).ToList();
        }
        public static FileStatusOverviewExt MapToExternal(this BrokerFileStatusOverview i)
        {
            return new FileStatusOverviewExt()
            {
                Checksum = i.Checksum,
                FileId = i.FileId,
                FileName = i.FileName,
                FileStatus = (FileStatusExt)i.FileStatus,
                FileStatusChanged = i.FileStatusChanged,
                FileStatusText = i.FileStatusText,
                SendersFileReference = i.SendersFileReference
            };
        }
        public static List<BrokerFileInitalize> MapToInternal(this List<FileInitalizeExt> extList)
        {
            return extList.Select(f => f.MapToInternal()).ToList();
        }
       public static BrokerFileInitalize MapToInternal(this FileInitalizeExt extObj)
        {
            return new BrokerFileInitalize()
            {
                Checksum = extObj.Checksum,
                FileName = extObj.FileName,
                SendersFileReference = extObj.SendersFileReference
            };
        }
        public static List<FileStatusEventExt> MapToExternal(this List<FileStatusEvent> i)
        {
            return i.Select(fse => fse.MapToExternal()).ToList();
        }
        public static FileStatusEventExt MapToExternal(this FileStatusEvent ifse)
        {
            return new FileStatusEventExt ()
            {
                FileStatus = ifse.FileStatus,
                FileStatusChanged = ifse.FileStatusChanged,
                FileStatusText = ifse.FileStatusText
            };
        }
    }
}