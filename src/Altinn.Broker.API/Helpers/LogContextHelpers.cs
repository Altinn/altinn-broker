using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

using Serilog.Context;

namespace Altinn.Broker.Helpers;

public static class LogContextHelpers
{
    public static void EnrichLogsWithInitializeFile(FileTransferInitalizeExt fileTransferInitalizeExt)
    {
        LogContext.PushProperty("sender", fileTransferInitalizeExt.Sender);
        LogContext.PushProperty("fileName", fileTransferInitalizeExt.FileName);
        LogContext.PushProperty("recipients", string.Join(',', fileTransferInitalizeExt.Recipients));
        LogContext.PushProperty("sendersFileTransferReference", fileTransferInitalizeExt.SendersFileTransferReference);
        LogContext.PushProperty("checksum", fileTransferInitalizeExt.Checksum);
    }
}
