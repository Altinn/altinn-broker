using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Models;
using Altinn.Broker.Models.Maskinporten;

namespace Altinn.Broker.Mappers;

internal static class InitializeFileMapper
{
    internal static InitializeFileCommandRequest MapToRequest(FileInitalizeExt fileInitializeExt, MaskinportenToken token)
    {
        return new InitializeFileCommandRequest()
        {
            Consumer = token.Consumer,
            Supplier = token.Supplier,
            Filename = fileInitializeExt.FileName,
            SenderExternalId = fileInitializeExt.Sender,
            SendersFileReference = fileInitializeExt.SendersFileReference,
            PropertyList = fileInitializeExt.PropertyList,
            RecipientIds = fileInitializeExt.Recipients,
            Checksum = fileInitializeExt.Checksum,
        };
    }
}
