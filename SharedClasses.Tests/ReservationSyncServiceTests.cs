using Microsoft.Extensions.Logging;
using Moq;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using Xunit;

namespace SharedClasses.Tests;

public class ReservationSyncServiceTests
{
    [Fact]
    public async Task PreviewSync_KeepsInitiatedPaymentStatus_WithoutWritingStore()
    {
        var reservationGuid = Guid.NewGuid();
        var record = CreateRecord(reservationGuid, PaymentStatuses.Initiated, ReservationStatusType.WaitingForPayment, 10, 100);
        var idoReservation = CreateReservation(10, 100, ReservationStatusType.WaitingForPayment);

        var storeMock = new Mock<IReservationStore>(MockBehavior.Strict);
        var syncOpsMock = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOpsMock
            .Setup(x => x.FetchIdoReservationAsync(record, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idoReservation);

        var service = new ReservationSyncService(
            storeMock.Object,
            syncOpsMock.Object,
            Mock.Of<ILogger<ReservationSyncService>>());

        var result = await service.PreviewReservationStatusSyncAsync(record, idoReservation);

        Assert.Equal(PaymentStatuses.Initiated, result.CurrentPaymentStatus);
        Assert.Equal(ReservationStatusType.WaitingForPayment, result.CurrentIdoStatus);
        Assert.False(result.BitrixUpdated);
        storeMock.Verify(
            x => x.UpdateAsync(It.IsAny<ReservationRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncStatus_PreservesInitiatedPaymentStatus_WhenOnlyIdoStatusChanges()
    {
        var reservationGuid = Guid.NewGuid();
        var record = CreateRecord(reservationGuid, PaymentStatuses.Initiated, ReservationStatusType.WaitingForPayment, 10, 100);
        var idoReservation = CreateReservation(10, 100, ReservationStatusType.Accepted);

        var updatedRecord = CreateRecord(reservationGuid, PaymentStatuses.Initiated, ReservationStatusType.Accepted, 10, 100);
        updatedRecord.SyncChangeSummary = "IdoStatus: waitingForPayment -> accepted";

        var storeMock = new Mock<IReservationStore>(MockBehavior.Strict);
        storeMock
            .Setup(x => x.UpdateAsync(It.IsAny<ReservationRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var syncOpsMock = new Mock<IReservationWorkflowSyncOperations>(MockBehavior.Strict);
        syncOpsMock
            .Setup(x => x.FetchIdoReservationAsync(record, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idoReservation);
        syncOpsMock
            .Setup(x => x.RequireReservationAsync(reservationGuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedRecord);

        var service = new ReservationSyncService(
            storeMock.Object,
            syncOpsMock.Object,
            Mock.Of<ILogger<ReservationSyncService>>());

        var result = await service.SyncReservationStatusAsync(record, idoReservation);

        Assert.Equal(PaymentStatuses.Initiated, result.CurrentPaymentStatus);
        Assert.Equal(ReservationStatusType.Accepted, result.CurrentIdoStatus);
        storeMock.Verify(
            x => x.UpdateAsync(It.Is<ReservationRecord>(r => r.PaymentStatus == PaymentStatuses.Initiated), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private static ReservationRecord CreateRecord(
        Guid reservationGuid,
        string paymentStatus,
        string idoStatus,
        int objectId,
        int objectItemId)
    {
        return new ReservationRecord
        {
            ReservationGuid = reservationGuid,
            IdoReservationId = 123,
            IdoStatus = idoStatus,
            PaymentStatus = paymentStatus,
            State = new ReservationState
            {
                StartRequest = new StartReservationRequest
                {
                    ObjectId = objectId,
                    ObjectItemId = objectItemId
                }
            }
        };
    }

    private static Reservation CreateReservation(int objectId, int objectItemId, string status)
    {
        return new Reservation
        {
            id = 123,
            ReservationDetails = new ReservationDetails
            {
                status = status
            },
            Items =
            [
                new ReservationItem
                {
                    objectId = objectId,
                    objectItemId = objectItemId,
                    itemId = objectItemId
                }
            ],
            Client = new ClientModel()
        };
    }
}
