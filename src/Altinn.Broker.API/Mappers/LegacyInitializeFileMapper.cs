using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class LegacyInitializeFileMapper
{
    internal static InitializeFileTransferRequest MapToRequest(LegacyFileInitalizeExt fileInitializeExt, CallerIdentity token)
    {
        return new InitializeFileTransferRequest()
        {
            Token = token,
            ResourceId = fileInitializeExt.ResourceId,
            FileName = fileInitializeExt.FileName,
            SenderExternalId = fileInitializeExt.Sender,
            SendersFileTransferReference = fileInitializeExt.SendersFileTransferReference,
            PropertyList = fileInitializeExt.PropertyList,
            RecipientExternalIds = fileInitializeExt.Recipients,
            Checksum = fileInitializeExt.Checksum,
            IsLegacy = true
        };
    }
}
