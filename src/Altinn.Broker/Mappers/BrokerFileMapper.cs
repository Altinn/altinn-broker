using Altinn.Broker.Core.Models;
using Altinn.Broker.Enums;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers
{
    public static class BrokerFileMapper
    {
        public static List<BrokerFileStatusDetailsExt> MapToExternal(this List<BrokerFileStatusDetails> i)
        {
            return i.Select(fsd => fsd.MapToExternal()).ToList();
        }
        public static BrokerFileStatusDetailsExt MapToExternal(this BrokerFileStatusDetails ibfd)
        {
            return new BrokerFileStatusDetailsExt()
            {
                Checksum = ibfd.Checksum,
                FileId = ibfd.FileId,
                FileName = ibfd.FileName,
                FileStatus = (BrokerFileStatusExt)ibfd.FileStatus,
                FileStatusChanged = ibfd.FileStatusChanged,
                FileStatusHistory = ibfd.FileStatusHistory.MapToExternal(),
                FileStatusText = ibfd.FileStatusText,
                SendersFileReference = ibfd.SendersFileReference
            };
        }
        public static List<BrokerFileStatusOverviewExt> MapToExternal(this List<BrokerFileStatusOverview> i)
        {
            return i.Select(o => o.MapToExternal()).ToList();
        }
        public static BrokerFileStatusOverviewExt MapToExternal(this BrokerFileStatusOverview i)
        {
            return new BrokerFileStatusOverviewExt()
            {
                Checksum = i.Checksum,
                FileId = i.FileId,
                FileName = i.FileName,
                FileStatus = (BrokerFileStatusExt)i.FileStatus,
                FileStatusChanged = i.FileStatusChanged,
                FileStatusText = i.FileStatusText,
                SendersFileReference = i.SendersFileReference
            };
        }
        public static List<BrokerFileInitalize> MapToInternal(this List<BrokerFileInitalizeExt> extList)
        {
            return extList.Select(f => f.MapToInternal()).ToList();
        }
        public static BrokerFileInitalize MapToInternal(this BrokerFileInitalizeExt extObj)
        {
            return new BrokerFileInitalize()
            {
                Checksum = extObj.Checksum,
                FileName = extObj.FileName,
                SendersFileReference = extObj.SendersFileReference
            };
        }
        public static List<BrokerFileStatusEventExt> MapToExternal(this List<FileStatusEvent> i)
        {
            return i.Select(fse => fse.MapToExternal()).ToList();
        }
        public static BrokerFileStatusEventExt MapToExternal(this FileStatusEvent ifse)
        {
            return new BrokerFileStatusEventExt()
            {
                FileStatus = ifse.FileStatus,
                FileStatusChanged = ifse.FileStatusChanged,
                FileStatusText = ifse.FileStatusText
            };
        }
    }
}