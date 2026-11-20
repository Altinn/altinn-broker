using Altinn.Broker.Application.SyncAccessLists;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Core.Services;
using Altinn.Platform.Register.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class SyncAccessListsHandlerTests
{
    private const string ResourceId = "resource-1";
    private readonly Mock<IAltinnResourceRepository> _altinnResourceRepository = new();
    private readonly Mock<IEligiblePartyStore> _eligiblePartyStore = new();
    private readonly Mock<IResourceRepository> _resourceRepository = new();
    private readonly Mock<IAltinnRegisterService> _registerService = new();

    public SyncAccessListsHandlerTests()
    {
        _resourceRepository
            .Setup(x => x.GetResources(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ResourceEntity { Id = ResourceId, ServiceOwnerId = "0192:310417947" }]);
        _eligiblePartyStore
            .Setup(x => x.GetKnownPartyMappings(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
    }

    [Fact]
    public async Task Process_AccessListsHaveNoMembers_ClearsEligibleParties()
    {
        SetupAccessList();

        await CreateHandler().Process(CancellationToken.None);

        _eligiblePartyStore.Verify(x => x.ReplaceAll(
            It.Is<IReadOnlyDictionary<string, string>>(m => m.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_NewMember_IsResolvedInRegisterAndStored()
    {
        SetupAccessList("uuid-1");
        _registerService
            .Setup(x => x.LookupPartyByUuid("uuid-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Party { OrgNumber = "310417947" });

        await CreateHandler().Process(CancellationToken.None);

        _eligiblePartyStore.Verify(x => x.ReplaceAll(
            It.Is<IReadOnlyDictionary<string, string>>(m => m.Count == 1 && m["uuid-1"] == "310417947"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_NoNewMembers_KeepsSameEligibleParties()
    {
        var known = SetupKnownMembersOnly();

        await CreateHandler().Process(CancellationToken.None);

        _eligiblePartyStore.Verify(x => x.ReplaceAll(
            It.Is<IReadOnlyDictionary<string, string>>(m => m.Count == known.Count && known.All(k => m[k.Key] == k.Value)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_NoNewMembers_DoesNotLookUpRegister()
    {
        SetupKnownMembersOnly();

        await CreateHandler().Process(CancellationToken.None);

        _registerService.Verify(
            x => x.LookupPartyByUuid(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_ResourceRegistryFails_KeepsEligibleParties()
    {
        _altinnResourceRepository
            .Setup(x => x.GetAccessListMembersOfResource(ResourceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadHttpRequestException("Failed to get access lists from Altinn Resource Registry"));

        await Assert.ThrowsAsync<BadHttpRequestException>(() => CreateHandler().Process(CancellationToken.None));

        VerifyNotReplaced();
    }

    [Fact]
    public async Task Process_RegisterLookupFails_KeepsEligibleParties()
    {
        SetupAccessList("uuid-1");
        _registerService
            .Setup(x => x.LookupPartyByUuid("uuid-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Register unavailable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().Process(CancellationToken.None));

        VerifyNotReplaced();
    }

    private void SetupAccessList(params string[] partyUuids) =>
        _altinnResourceRepository
            .Setup(x => x.GetAccessListMembersOfResource(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceAccessList { Restricted = true, PartyUuids = [.. partyUuids] });

    private Dictionary<string, string> SetupKnownMembersOnly()
    {
        var known = new Dictionary<string, string>
        {
            ["uuid-1"] = "310417947",
            ["uuid-2"] = "310630829",
        };
        SetupAccessList([.. known.Keys]);
        _eligiblePartyStore
            .Setup(x => x.GetKnownPartyMappings(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(known);
        return known;
    }

    private void VerifyNotReplaced() =>
        _eligiblePartyStore.Verify(x => x.ReplaceAll(
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);

    private SyncAccessListsHandler CreateHandler() => new(
        _altinnResourceRepository.Object,
        _eligiblePartyStore.Object,
        _resourceRepository.Object,
        _registerService.Object,
        NullLogger<SyncAccessListsHandler>.Instance);
}
