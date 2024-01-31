using Altinn.Broker.Application.InitializeFileCommand;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class LegacyInitializeFileMapper
{
    internal static InitializeFileCommandRequest MapToRequest(LegacyFileInitalizeExt fileInitializeExt, CallerIdentity token)
    {
        return new InitializeFileCommandRequest()
        {
            Token = token,
            ResourceId = fileInitializeExt.ResourceId,
            Filename = fileInitializeExt.FileName,
            SenderExternalId = fileInitializeExt.Sender,
            SendersFileReference = fileInitializeExt.SendersFileReference,
            PropertyList = fileInitializeExt.PropertyList,
            RecipientExternalIds = fileInitializeExt.Recipients,
            Checksum = fileInitializeExt.Checksum,
            IsLegacy = true
        };
    }
}
