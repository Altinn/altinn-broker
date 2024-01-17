using Altinn.Broker.Models;

namespace Altinn.Broker.Tests.Factories;
internal static class FileInitializeExtTestFactory
{
    internal static FileInitalizeExt BasicFile() => new FileInitalizeExt()
    {
        ResourceId = "1",
        Checksum = null,
        FileName = "input.txt",
        PropertyList = [],
        Recipients = new List<string> { "0192:986252932" },
        Sender = "0192:991825827",
        SendersFileReference = "test-data"
    };
}
