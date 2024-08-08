using Altinn.Broker.Models;
using Altinn.Broker.Tests.Helpers;

namespace Altinn.Broker.Tests.Factories;
internal static class FileTransferInitializeExtTestFactory
{
    internal static FileTransferInitalizeExt BasicFileTransfer() => new FileTransferInitalizeExt()
    {
        ResourceId = TestConstants.RESOURCE_FOR_TEST,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932" },
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data"
    };
    internal static FileTransferInitalizeExt BasicFileTransfer_MultipleRecipients() => new FileTransferInitalizeExt()
    {
        ResourceId = TestConstants.RESOURCE_FOR_TEST,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932", "0192:910351192" },
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data"
    };
}
