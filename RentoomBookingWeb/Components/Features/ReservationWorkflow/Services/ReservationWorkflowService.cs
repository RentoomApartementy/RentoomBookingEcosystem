namespace RentoomBookingWeb.Components.Features.ReservationWorkflow.Services;

interface IUIReservationWorkflowService
{
    public Guid ReservationTokenGuid { get; }
    public Guid TpayGuid { get; }
}

public class UIReservationWorkflowService:  IUIReservationWorkflowService
{
    public Guid ReservationTokenGuid { get; private set; } = Guid.NewGuid();
    public Guid TpayGuid { get; } = Guid.NewGuid();
}