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

    internal static FileTransferInitalizeExt BasicFileTransfer2() => new FileTransferInitalizeExt()
    {
        ResourceId = TestConstants.RESOURCE_FOR_TEST,
        Checksum = null,
        FileName = "input2.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:991825827" },
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data-2"
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

    internal static FileTransferInitalizeExt BasicFileTransfer_RequiredParty_NotPartOfTransaction() => new FileTransferInitalizeExt()
    {
        ResourceId = TestConstants.RESOURCE_FOR_TEST_REQUIREDPARTY,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932" },
        Sender = "0192:312195771",
        SendersFileTransferReference = "test-data"
    };

    internal static FileTransferInitalizeExt BasicFileTransfer_RequiredParty_SenderIsRequiredParty() => new FileTransferInitalizeExt()
    {
        ResourceId = TestConstants.RESOURCE_FOR_TEST_REQUIREDPARTY,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932" },
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data"
    };

    internal static FileTransferInitalizeExt BasicFileTransfer_With_AccessList_Resource_And_No_Recipients() => new FileTransferInitalizeExt()
    {
        ResourceId = TestConstants.RESOURCE_WITH_ACCESS_LIST,
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Sender = "0192:991825827",
        SendersFileTransferReference = "test-data"
    };

    internal static FileTransferInitalizeExt BasicFileTransfer_ManifestShim() {
        var basicFileTransfer = BasicFileTransfer();
        basicFileTransfer.ResourceId = TestConstants.RESOURCE_WITH_MANIFEST_SHIM;
        return basicFileTransfer;
    } 
}
