using Altinn.Broker.Models;

namespace Altinn.Broker.Tests.Factories;
internal static class FileInitializeExtTestFactory
{
    internal static FileInitalizeExt BasicFile() => new FileInitalizeExt()
    {
        ResourceId = "altinn-broker-test-resource-1",
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932" },
        Sender = "0192:991825827",
        SendersFileReference = "test-data"
    };
    internal static FileInitalizeExt BasicFile_MultipleRecipients() => new FileInitalizeExt()
    {
        ResourceId = "altinn-broker-test-resource-2",
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932", "0192:910351192" },
        Sender = "0192:991825827",
        SendersFileReference = "test-data"
    };
}
