using Altinn.Broker.Models;
using Altinn.Broker.Models.Maskinporten;

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

    public static void EnrichLogsWithMaskinporten(MaskinportenToken token)
    {
        LogContext.PushProperty("consumer", token.Consumer);
        LogContext.PushProperty("supplier", token.Supplier);
        LogContext.PushProperty("scope", token.Scope);
    }
}
