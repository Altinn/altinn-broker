using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

using Serilog.Context;

namespace Altinn.Broker.Helpers;

public static class LogContextHelpers
{
    public static void EnrichLogsWithInitializeFile(FileInitalizeExt fileInitalizeExt)
    {
        LogContext.PushProperty("sender", fileInitalizeExt.Sender);
        LogContext.PushProperty("filename", fileInitalizeExt.FileName);
        LogContext.PushProperty("recipients", string.Join(',', fileInitalizeExt.Recipients));
        LogContext.PushProperty("sendersFileReference", fileInitalizeExt.SendersFileReference);
        LogContext.PushProperty("checksum", fileInitalizeExt.Checksum);
    }

    public static void EnrichLogsWithToken(CallerIdentity token)
    {
        LogContext.PushProperty("consumer", token.Consumer);
        LogContext.PushProperty("supplier", token.Supplier);
        LogContext.PushProperty("scope", token.Scope);
        LogContext.PushProperty("clientId", token.ClientId);
    }
}
