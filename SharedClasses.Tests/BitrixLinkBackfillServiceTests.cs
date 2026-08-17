using Microsoft.Extensions.Logging;
using Moq;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using Xunit;

namespace SharedClasses.Tests;

public sealed class BitrixLinkBackfillServiceTests
{
    private static int _nextIdoReservationId = 1000;

    [Fact]
    public async Task DryRun_ReportsPlannedActions_WithoutCallingMutationOperations()
    {
        var record = CreateValidRecord();
        var store = CreateStoreForGuid(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid]
        });

        var item = Assert.Single(result.Results);
        Assert.True(result.DryRun);
        Assert.Equal(BitrixLinkBackfillStatuses.Planned, item.Status);
        Assert.Equal(["EnsureClientBitrixLink", "EnsureDealBitrixLink"], item.Actions);
        syncOperations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Execute_WithBothLinksMissing_EnsuresAndReturnsBothIdentifiers()
    {
        var record = CreateValidRecord();
        var store = CreateStoreForGuid(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOperations
            .Setup(operation => operation.EnsureBitrixContactAndDealAsync(record))
            .ReturnsAsync(() =>
            {
                record.ClientBitrixId = 701;
                record.DealBitrixId = 801;
                return record;
            });
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid],
            DryRun = false
        });

        var item = Assert.Single(result.Results);
        Assert.Equal(BitrixLinkBackfillStatuses.Updated, item.Status);
        Assert.Null(item.PreviousClientBitrixId);
        Assert.Null(item.PreviousDealBitrixId);
        Assert.Equal(701, item.ClientBitrixId);
        Assert.Equal(801, item.DealBitrixId);
        Assert.Equal(1, result.UpdatedCount);
        syncOperations.VerifyAll();
    }

    [Fact]
    public async Task Execute_WithOnlyClientLinkMissing_UpdatesExistingDealContactLink()
    {
        var record = CreateValidRecord();
        record.DealBitrixId = 801;
        var store = CreateStoreForGuid(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOperations
            .Setup(operation => operation.EnsureBitrixContactAndDealAsync(record))
            .ReturnsAsync(() =>
            {
                record.ClientBitrixId = 701;
                return record;
            });
        syncOperations
            .Setup(operation => operation.UpdateBitrixDealAsync(
                record,
                "Controlled Bitrix reservation link backfill",
                null))
            .Returns(Task.CompletedTask);
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid],
            DryRun = false
        });

        var item = Assert.Single(result.Results);
        Assert.Equal(BitrixLinkBackfillStatuses.Updated, item.Status);
        Assert.Equal(701, item.ClientBitrixId);
        Assert.Equal(801, item.DealBitrixId);
        Assert.Contains("UpdateDealContactLink", item.Actions);
        syncOperations.VerifyAll();
    }

    [Fact]
    public async Task Execute_WithOnlyDealLinkMissing_DoesNotRunSeparateDealUpdate()
    {
        var record = CreateValidRecord();
        record.ClientBitrixId = 701;
        var store = CreateStoreForGuid(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOperations
            .Setup(operation => operation.EnsureBitrixContactAndDealAsync(record))
            .ReturnsAsync(() =>
            {
                record.DealBitrixId = 801;
                return record;
            });
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid],
            DryRun = false
        });

        Assert.Equal(BitrixLinkBackfillStatuses.Updated, Assert.Single(result.Results).Status);
        syncOperations.VerifyAll();
    }

    [Fact]
    public async Task Execute_WhenBothLinksExist_SkipsRecord()
    {
        var record = CreateValidRecord();
        record.ClientBitrixId = 701;
        record.DealBitrixId = 801;
        var store = CreateStoreForGuid(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid],
            DryRun = false
        });

        Assert.Equal(BitrixLinkBackfillStatuses.Skipped, Assert.Single(result.Results).Status);
        syncOperations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidReservationStates_AreSkippedWithoutMutation()
    {
        var missingIdo = CreateValidRecord();
        missingIdo.IdoReservationId = null;
        var missingClient = CreateValidRecord();
        missingClient.State.Client = null;
        var missingStartRequest = CreateValidRecord();
        missingStartRequest.State.StartRequest = null;

        var store = new Mock<IReservationStore>(MockBehavior.Strict);
        store.Setup(value => value.GetAsync(missingIdo.ReservationGuid, It.IsAny<CancellationToken>())).ReturnsAsync(missingIdo);
        store.Setup(value => value.GetAsync(missingClient.ReservationGuid, It.IsAny<CancellationToken>())).ReturnsAsync(missingClient);
        store.Setup(value => value.GetAsync(missingStartRequest.ReservationGuid, It.IsAny<CancellationToken>())).ReturnsAsync(missingStartRequest);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [missingIdo.ReservationGuid, missingClient.ReservationGuid, missingStartRequest.ReservationGuid],
            DryRun = false
        });

        Assert.Equal(3, result.SkippedCount);
        Assert.All(result.Results, item => Assert.Equal(BitrixLinkBackfillStatuses.Skipped, item.Status));
        syncOperations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GuidAndIdoIdentifiers_ForSameRecord_AreDeduplicated()
    {
        var record = CreateValidRecord();
        var store = new Mock<IReservationStore>(MockBehavior.Strict);
        store.Setup(value => value.GetAsync(record.ReservationGuid, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        store.Setup(value => value.GetByIdoReservationIdAsync(record.IdoReservationId.GetValueOrDefault(), It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid],
            IdoReservationIds = [record.IdoReservationId.GetValueOrDefault()]
        });

        Assert.Equal(2, result.RequestedIdentifierCount);
        Assert.Equal(1, result.ResolvedRecordCount);
        Assert.Single(result.Results);
    }

    [Fact]
    public async Task FailureForOneRecord_DoesNotStopFollowingRecords()
    {
        var failingRecord = CreateValidRecord();
        var successfulRecord = CreateValidRecord();
        var store = new Mock<IReservationStore>(MockBehavior.Strict);
        store.Setup(value => value.GetAsync(failingRecord.ReservationGuid, It.IsAny<CancellationToken>())).ReturnsAsync(failingRecord);
        store.Setup(value => value.GetAsync(successfulRecord.ReservationGuid, It.IsAny<CancellationToken>())).ReturnsAsync(successfulRecord);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOperations
            .Setup(operation => operation.EnsureBitrixContactAndDealAsync(failingRecord))
            .ThrowsAsync(new InvalidOperationException("Bitrix unavailable"));
        syncOperations
            .Setup(operation => operation.EnsureBitrixContactAndDealAsync(successfulRecord))
            .ReturnsAsync(() =>
            {
                successfulRecord.ClientBitrixId = 702;
                successfulRecord.DealBitrixId = 802;
                return successfulRecord;
            });
        var service = CreateService(store.Object, syncOperations.Object);

        var result = await service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [failingRecord.ReservationGuid, successfulRecord.ReservationGuid],
            DryRun = false
        });

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(2, result.ProcessedCount);
        syncOperations.VerifyAll();
    }

    [Fact]
    public async Task RepeatedExecution_SkipsAlreadyBackfilledRecord()
    {
        var record = CreateValidRecord();
        var store = CreateStoreForGuid(record);
        var syncOperations = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOperations
            .Setup(operation => operation.EnsureBitrixContactAndDealAsync(record))
            .ReturnsAsync(() =>
            {
                record.ClientBitrixId = 701;
                record.DealBitrixId = 801;
                return record;
            });
        var service = CreateService(store.Object, syncOperations.Object);
        var request = new BitrixLinkBackfillRequestDto
        {
            ReservationGuids = [record.ReservationGuid],
            DryRun = false
        };

        var firstResult = await service.BackfillBitrixLinksAsync(request);
        var secondResult = await service.BackfillBitrixLinksAsync(request);

        Assert.Equal(BitrixLinkBackfillStatuses.Updated, Assert.Single(firstResult.Results).Status);
        Assert.Equal(BitrixLinkBackfillStatuses.Skipped, Assert.Single(secondResult.Results).Status);
        syncOperations.Verify(operation => operation.EnsureBitrixContactAndDealAsync(record), Times.Once);
        syncOperations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MoreThanMaximumIdentifiers_IsRejected()
    {
        var service = CreateService(
            Mock.Of<IReservationStore>(),
            Mock.Of<IReservationWorkflowSyncOperations>());
        var request = new BitrixLinkBackfillRequestDto
        {
            IdoReservationIds = Enumerable.Range(1, ReservationSyncService.MaxBitrixLinkBackfillIdentifiers + 1).ToList()
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.BackfillBitrixLinksAsync(request));

        Assert.Contains("at most 100", exception.Message);
    }

    [Fact]
    public async Task RequestWithoutValidIdentifiers_IsRejected()
    {
        var service = CreateService(
            Mock.Of<IReservationStore>(),
            Mock.Of<IReservationWorkflowSyncOperations>());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.BackfillBitrixLinksAsync(new BitrixLinkBackfillRequestDto()));

        Assert.Contains("at least one valid identifier", exception.Message);
    }

    private static Mock<IReservationStore> CreateStoreForGuid(ReservationRecord record)
    {
        var store = new Mock<IReservationStore>(MockBehavior.Strict);
        store
            .Setup(value => value.GetAsync(record.ReservationGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => record);
        return store;
    }

    private static ReservationSyncService CreateService(
        IReservationStore store,
        IReservationWorkflowSyncOperations syncOperations)
    {
        return new ReservationSyncService(
            store,
            syncOperations,
            Mock.Of<ILogger<ReservationSyncService>>());
    }

    private static ReservationRecord CreateValidRecord()
    {
        return new ReservationRecord
        {
            ReservationGuid = Guid.NewGuid(),
            IdoReservationId = Interlocked.Increment(ref _nextIdoReservationId),
            State = new ReservationState
            {
                Client = new ClientInfoDto
                {
                    FirstName = "Jan",
                    LastName = "Kowalski",
                    Email = $"jan.{Guid.NewGuid():N}@example.com"
                },
                StartRequest = new StartReservationRequest
                {
                    ObjectId = 10,
                    ObjectItemId = 20,
                    StartDate = new DateOnly(2026, 9, 1),
                    EndDate = new DateOnly(2026, 9, 3)
                }
            }
        };
    }
}
