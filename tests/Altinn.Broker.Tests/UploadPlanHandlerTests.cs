using Altinn.Broker.Application;
using Altinn.Broker.Application.UploadFile.Tus;
using Altinn.Broker.Application.UploadPlans;
using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Altinn.Broker.Tests;

public class UploadPlanHandlerTests
{
    private const string SenderExternalId = "0192:991825827";
    private const string ResourceId = "resource-a";

    private readonly FileTransferEntity _fileTransfer = CreateFileTransfer();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IFileTransferRepository> _fileTransferRepository = new();
    private readonly Mock<IUploadPlanStore> _uploadPlanStore = new();
    private readonly Mock<ITusPartialUploadLookup> _partialUploadLookup = new();
    private readonly Mock<ITusUploadProgressReader> _progressReader = new();
    private readonly List<TusPartialUpload> _partialUploads = [];

    public UploadPlanHandlerTests()
    {
        _fileTransferRepository
            .Setup(repository => repository.GetFileTransfer(_fileTransfer.FileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fileTransfer);
        _authorizationService
            .Setup(service => service.CheckAccessAsSender(null, ResourceId, SenderExternalId, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _partialUploadLookup
            .Setup(lookup => lookup.GetPartialUploadsAsync(_fileTransfer.FileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _partialUploads);
    }

    [Fact]
    public async Task Save_SplitFileWithKnownParts_SavesPlan()
    {
        PartialUpload("part-a", 60);
        PartialUpload("part-b", 40);

        var result = await CreateSaveHandler().Process(
            SaveRequest(("part-a", 0, 60), ("part-b", 60, 40)), null, CancellationToken.None);

        Assert.True(result.IsT0);
        _uploadPlanStore.Verify(store => store.SaveAsync(
            _fileTransfer.FileTransferId,
            It.Is<UploadPlan>(plan =>
                plan.Fingerprint == "fingerprint"
                && plan.Parts.Count == 2
                && plan.Parts[1].UploadId == "part-b"
                && plan.Parts[1].Start == 60
                && plan.Parts[1].Length == 40),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Save_PartialLeftBehindByARetriedCreate_IsNoConcernOfThePlan()
    {
        PartialUpload("part-a", 60);
        PartialUpload("orphan", 40);
        PartialUpload("part-b", 40);

        var result = await CreateSaveHandler().Process(
            SaveRequest(("part-a", 0, 60), ("part-b", 60, 40)), null, CancellationToken.None);

        Assert.True(result.IsT0);
    }

    [Fact]
    public async Task Save_SingleUploadUnderTransferId_SavesPlanWithoutLookingUpPartials()
    {
        var result = await CreateSaveHandler().Process(
            SaveRequest((_fileTransfer.FileTransferId.ToString(), 0, 100)), null, CancellationToken.None);

        Assert.True(result.IsT0);
        _partialUploadLookup.Verify(
            lookup => lookup.GetPartialUploadsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public static TheoryData<(string UploadId, long Start, long Length)[]> InvalidShapes => new()
    {
        new[] { ("part-a", 0L, 60L), ("part-b", 61L, 39L) },  // gap
        new[] { ("part-a", 0L, 60L), ("part-b", 50L, 50L) },  // overlap
        new[] { ("part-a", 0L, 60L), ("part-a", 60L, 40L) },  // the same upload twice
        new[] { ("part-a", 10L, 90L) },                       // does not start at the first byte
        new[] { ("part-a", 0L, 0L) },                         // empty
        Array.Empty<(string, long, long)>(),
    };

    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public async Task Save_PartsNotFollowingEachOther_ReturnsInvalidUploadPlan((string UploadId, long Start, long Length)[] parts)
    {
        PartialUpload("part-a", 60);
        PartialUpload("part-b", 40);

        var result = await CreateSaveHandler().Process(SaveRequest(parts), null, CancellationToken.None);

        Assert.Equal(Errors.InvalidUploadPlan, result.AsT1);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Save_MissingFingerprint_ReturnsInvalidUploadPlan()
    {
        PartialUpload("part-a", 100);
        var request = SaveRequest(("part-a", 0, 100));
        request.Fingerprint = "";

        var result = await CreateSaveHandler().Process(request, null, CancellationToken.None);

        Assert.Equal(Errors.InvalidUploadPlan, result.AsT1);
    }

    [Fact]
    public async Task Save_PartNotAPartialOfTheTransfer_ReturnsInvalidUploadPlan()
    {
        PartialUpload("part-a", 60);

        var result = await CreateSaveHandler().Process(
            SaveRequest(("part-a", 0, 60), ("part-b", 60, 40)), null, CancellationToken.None);

        Assert.Equal(Errors.InvalidUploadPlan, result.AsT1);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task Save_PartLengthDiffersFromThePartial_ReturnsInvalidUploadPlan()
    {
        PartialUpload("part-a", 60);
        PartialUpload("part-b", 41);

        var result = await CreateSaveHandler().Process(
            SaveRequest(("part-a", 0, 60), ("part-b", 60, 40)), null, CancellationToken.None);

        Assert.Equal(Errors.InvalidUploadPlan, result.AsT1);
    }

    [Fact]
    public async Task Save_SplitFileUsingTheTransferId_ReturnsInvalidUploadPlan()
    {
        PartialUpload("part-b", 40);

        var result = await CreateSaveHandler().Process(
            SaveRequest((_fileTransfer.FileTransferId.ToString(), 0, 60), ("part-b", 60, 40)), null, CancellationToken.None);

        Assert.Equal(Errors.InvalidUploadPlan, result.AsT1);
    }

    [Fact]
    public async Task Save_NoSenderAccess_ReturnsNoAccess()
    {
        _authorizationService
            .Setup(service => service.CheckAccessAsSender(null, ResourceId, SenderExternalId, It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        var result = await CreateSaveHandler().Process(SaveRequest(("part-a", 0, 100)), null, CancellationToken.None);

        Assert.Equal(Errors.NoAccessToResource, result.AsT1);
        VerifyNothingSaved();
    }

    [Theory]
    [InlineData(FileTransferStatus.UploadProcessing)]
    [InlineData(FileTransferStatus.Published)]
    [InlineData(FileTransferStatus.Failed)]
    public async Task Save_TransferPastUpload_ReturnsAlreadyUploaded(FileTransferStatus status)
    {
        _fileTransfer.FileTransferStatusEntity.Status = status;

        var result = await CreateSaveHandler().Process(SaveRequest(("part-a", 0, 100)), null, CancellationToken.None);

        Assert.Equal(Errors.FileTransferAlreadyUploaded, result.AsT1);
    }

    [Fact]
    public async Task Get_SavedPlan_ReturnsPartsWithProgress()
    {
        StoredPlan(("part-a", 0, 60), ("part-b", 60, 40));
        Uploaded("part-a", 60);
        Uploaded("part-b", 15);

        var result = await CreateGetHandler().Process(
            new GetUploadPlanRequest { FileTransferId = _fileTransfer.FileTransferId }, null, CancellationToken.None);

        var plan = result.AsT0;
        Assert.Equal("fingerprint", plan.Fingerprint);
        Assert.Equal(75, plan.UploadedBytes);
        Assert.Equal(["part-a", "part-b"], plan.Parts.Select(part => part.UploadId));
        Assert.Equal([0L, 60L], plan.Parts.Select(part => part.Start));
        Assert.Equal([60L, 15L], plan.Parts.Select(part => part.UploadedBytes));
    }

    [Fact]
    public async Task Get_ProgressBeyondThePart_IsCappedAtItsLength()
    {
        StoredPlan(("part-a", 0, 60));
        Uploaded("part-a", 80);

        var result = await CreateGetHandler().Process(
            new GetUploadPlanRequest { FileTransferId = _fileTransfer.FileTransferId }, null, CancellationToken.None);

        Assert.Equal(60, result.AsT0.UploadedBytes);
    }

    [Fact]
    public async Task Get_NoSavedPlan_ReturnsUploadPlanNotFound()
    {
        var result = await CreateGetHandler().Process(
            new GetUploadPlanRequest { FileTransferId = _fileTransfer.FileTransferId }, null, CancellationToken.None);

        Assert.Equal(Errors.UploadPlanNotFound, result.AsT1);
    }

    [Fact]
    public async Task Get_TransferPastUpload_ReturnsUploadPlanNotFound()
    {
        StoredPlan(("part-a", 0, 100));
        _fileTransfer.FileTransferStatusEntity.Status = FileTransferStatus.Published;

        var result = await CreateGetHandler().Process(
            new GetUploadPlanRequest { FileTransferId = _fileTransfer.FileTransferId }, null, CancellationToken.None);

        Assert.Equal(Errors.UploadPlanNotFound, result.AsT1);
    }

    private SaveUploadPlanHandler CreateSaveHandler()
        => new(CreateAccess(), _uploadPlanStore.Object, _partialUploadLookup.Object, NullLogger<SaveUploadPlanHandler>.Instance);

    private GetUploadPlanHandler CreateGetHandler()
        => new(CreateAccess(), _uploadPlanStore.Object, _progressReader.Object);

    private UploadPlanAccess CreateAccess()
        => new(_authorizationService.Object, _fileTransferRepository.Object);

    private SaveUploadPlanRequest SaveRequest(params (string UploadId, long Start, long Length)[] parts)
        => new()
        {
            FileTransferId = _fileTransfer.FileTransferId,
            Fingerprint = "fingerprint",
            FingerprintVersion = 1,
            Parts = parts.Select(part => new UploadPlanPartRequest { UploadId = part.UploadId, Start = part.Start, Length = part.Length }).ToList(),
        };

    private void PartialUpload(string partialUploadId, long length)
        => _partialUploads.Add(new TusPartialUpload(partialUploadId, length));

    private void StoredPlan(params (string UploadId, long Start, long Length)[] parts)
        => _uploadPlanStore
            .Setup(store => store.TryGetAsync(_fileTransfer.FileTransferId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadPlan(
                "fingerprint",
                1,
                parts.Select(part => new UploadPlanPart(part.UploadId, part.Start, part.Length)).ToList(),
                DateTimeOffset.UtcNow));

    private void Uploaded(string uploadId, long uploadedBytes)
        => _progressReader
            .Setup(reader => reader.GetUploadedBytesAsync(_fileTransfer.FileTransferId, uploadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedBytes);

    private void VerifyNothingSaved()
        => _uploadPlanStore.Verify(
            store => store.SaveAsync(It.IsAny<Guid>(), It.IsAny<UploadPlan>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static FileTransferEntity CreateFileTransfer()
        => new()
        {
            FileTransferId = Guid.NewGuid(),
            ResourceId = ResourceId,
            Sender = new ActorEntity { ActorId = 3, ActorExternalId = SenderExternalId },
            FileTransferStatusEntity = new FileTransferStatusEntity
            {
                Status = FileTransferStatus.UploadStarted,
                Date = DateTimeOffset.UtcNow,
            },
            Created = DateTimeOffset.UtcNow,
            ExpirationTime = DateTimeOffset.UtcNow.AddDays(30),
            RecipientCurrentStatuses = [],
            FileName = "report.zip",
            UseVirusScan = false,
            PropertyList = [],
        };
}
