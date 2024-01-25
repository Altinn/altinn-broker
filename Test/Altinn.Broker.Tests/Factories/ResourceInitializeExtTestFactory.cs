using Altinn.Broker.Models.Service;

namespace Altinn.Broker.Tests.Factories;
internal class ResourceInitializeExtTestFactory
{
    internal static ResourceInitializeExt BasicResource() => new ResourceInitializeExt()
    {
        ResourceId = Guid.NewGuid().ToString(),
        OrganizationId = "0192:991825827",
        PermittedMaskinportenUsers = [
            new MaskinportenUser()
            {
                ClientId = "042fbe5d-bbfb-41cf-bada-9b9b52073a9b",
                AccessLevel = AccessLevel.Write,
                OrganizationNumber = "0192:991825827"
            },
            new MaskinportenUser()
            {
                ClientId = "626c0754-dfae-4549-9343-c6ed47feea9a",
                AccessLevel = AccessLevel.Read,
                OrganizationNumber = "0192:986252932"
            }
        ]
    };
}
