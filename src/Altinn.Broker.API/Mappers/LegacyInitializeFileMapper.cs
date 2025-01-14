﻿using Altinn.Broker.Application.InitializeFileTransfer;
using Altinn.Broker.Models;

namespace Altinn.Broker.Mappers;

internal static class LegacyInitializeFileMapper
{
    internal static InitializeFileTransferRequest MapToRequest(LegacyFileInitalizeExt fileInitializeExt)
    {
        return new InitializeFileTransferRequest()
        {
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
