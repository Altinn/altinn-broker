using Altinn.Broker.Application.InitializeFileTransferCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;
using Altinn.Broker.Models.Maskinporten;

namespace Altinn.Broker.Mappers;

internal static class InitializeFileTransferMapper
{
    internal static InitializeFileTransferCommandRequest MapToRequest(FileTransferInitalizeExt fileTransferInitializeExt, CallerIdentity token)
    {
        return new InitializeFileTransferCommandRequest()
        {
            Token = token,
            ResourceId = fileTransferInitializeExt.ResourceId,
            FileName = fileTransferInitializeExt.FileName,
            SenderExternalId = fileTransferInitializeExt.Sender,
            SendersFileTransferReference = fileTransferInitializeExt.SendersFileTransferReference,
            PropertyList = fileTransferInitializeExt.PropertyList,
            RecipientExternalIds = fileTransferInitializeExt.Recipients,
            Checksum = fileTransferInitializeExt.Checksum
        };
    }
}
