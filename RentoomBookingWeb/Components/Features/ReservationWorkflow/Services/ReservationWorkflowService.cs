namespace RentoomBookingWeb.Components.Features.ReservationWorkflow.Services;

interface IReservationWorkflowService
{
    public Guid CurrentGuid { get; }
    public Guid TpayGuid { get; }
}

public class ReservationWorkflowService:  IReservationWorkflowService
{
    public Guid CurrentGuid { get; private set; } = Guid.NewGuid();
    public Guid TpayGuid { get; } = Guid.NewGuid();
}