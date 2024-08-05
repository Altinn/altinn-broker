using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

using Serilog.Context;

namespace Altinn.Broker.Helpers;

public static class LogContextHelpers
{
    public static void EnrichLogsWithInitializeFile(FileTransferInitalizeExt fileTransferInitalizeExt)
    {
        LogContext.PushProperty("sender", fileTransferInitalizeExt.Sender);
        LogContext.PushProperty("FileName", fileTransferInitalizeExt.FileName);
        LogContext.PushProperty("recipients", string.Join(',', fileTransferInitalizeExt.Recipients));
        LogContext.PushProperty("sendersFileTransferReference", fileTransferInitalizeExt.SendersFileTransferReference);
        LogContext.PushProperty("checksum", fileTransferInitalizeExt.Checksum);
    }

    public static void EnrichLogsWithLegacyInitializeFile(LegacyFileInitalizeExt fileInitalizeExt)
    {
        LogContext.PushProperty("sender", fileInitalizeExt.Sender);
        LogContext.PushProperty("FileName", fileInitalizeExt.FileName);
        LogContext.PushProperty("recipients", string.Join(',', fileInitalizeExt.Recipients));
        LogContext.PushProperty("sendersFileTransferReference", fileInitalizeExt.SendersFileTransferReference);
        LogContext.PushProperty("checksum", fileInitalizeExt.Checksum);
    }

    public static void EnrichLogsWithToken(CallerIdentity token)
    {
        LogContext.PushProperty("consumer", token.Consumer);
        LogContext.PushProperty("scope", token.Scope);
        LogContext.PushProperty("clientId", token.ClientId);
    }

    public static void EnrichLogsWithFileTransferId(Guid fileTransferId)
    {
        LogContext.PushProperty("fileTransferId", fileTransferId);
    }   
}
