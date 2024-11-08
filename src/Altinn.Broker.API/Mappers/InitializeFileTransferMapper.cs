using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class InitializeFileTransferMapper
{
    internal static InitializeFileTransferRequest MapToRequest(FileTransferInitalizeExt fileTransferInitializeExt, CallerIdentity token)
    {
        return new InitializeFileTransferRequest()
        {
            Token = token,
            ResourceId = fileTransferInitializeExt.ResourceId,
            FileName = fileTransferInitializeExt.FileName,
            SenderExternalId = fileTransferInitializeExt.Sender,
            SendersFileTransferReference = fileTransferInitializeExt.SendersFileTransferReference,
            PropertyList = fileTransferInitializeExt.PropertyList,
            RecipientExternalIds = fileTransferInitializeExt.Recipients,
            Checksum = fileTransferInitializeExt.Checksum,
            DisableVirusScan = fileTransferInitializeExt.DisableVirusScan ?? false
        };
    }
}
