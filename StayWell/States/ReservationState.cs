using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.StayWell.Services;
using System.Net;
using WorkflowModels = RentoomBooking.SharedClasses.Models.ReservationWorkflow;

namespace RentoomBooking.StayWell.States
{
    public class ReservationState(BackendApi backendApi, ReservationTokenService tokenService)
    {
        private const int EarlyCheckInAddonId = 40;
        private const int LateCheckOutAddonId = 41;

        private static readonly TimeOnly DefaultCheckInTime = new(15, 0);
        private static readonly TimeOnly DefaultCheckOutTime = new(11, 0);
        private static readonly TimeOnly EarlyCheckInTime = new(14, 0);   // 15:00 - 1h
        private static readonly TimeOnly LateCheckOutTime = new(12, 0);   // 11:00 + 1h

        private RentoomReservation? _reservation;
        private WorkflowModels.StayWellReservationRecordDto? _reservationRecord;
        private readonly BackendApi _backendApi = backendApi;
        private readonly ReservationTokenService _tokenService = tokenService;
        private string? _currentToken;
        private HttpStatusCode? _currentStatus;

        public bool IsValidReservation => CurrentReservation != null && CurrentStatus == HttpStatusCode.OK;
        public bool IsActiveReservation
        {
            get
            {
                var details = CurrentReservation?.Reservation?.ReservationDetails;
                if (details is null)
                {
                    return false;
                }

                var now = DateTime.Now;
                var from = details.getDateFrom().Date + CheckInTime.ToTimeSpan();
                var to = details.getDateTo().Date + CheckOutTime.ToTimeSpan();

                return from <= now && now <= to && details.status == ReservationStatusType.Accepted;
            }
        }

        public RentoomReservation? CurrentReservation
        {
            get => _reservation;
            private set
            {
                _reservation = value;
                NotifyStateChanged();
            }
        }

        public string? CurrentToken => _currentToken;
        public HttpStatusCode? CurrentStatus => _currentStatus;
        public WorkflowModels.StayWellReservationRecordDto? CurrentReservationRecord => _reservationRecord;
        public event Action? OnChange;
        public bool IsLoading { get; private set; }

        public bool HasEarlyCheckIn => HasAddon(EarlyCheckInAddonId);

        public bool HasLateCheckOut => HasAddon(LateCheckOutAddonId);

        public TimeOnly CheckInTime => HasEarlyCheckIn ? EarlyCheckInTime : DefaultCheckInTime;

        public TimeOnly CheckOutTime => HasLateCheckOut ? LateCheckOutTime : DefaultCheckOutTime;

        private bool HasAddon(int addonId)
        {
            var addons = CurrentReservation?.Reservation?.Items
                ?.SelectMany(item => item.addons ?? []);

            return addons?.Any(a =>
                int.TryParse(a.addonId, out var id) && id == addonId) ?? false;
        }

        public async Task<RentoomReservation?> GetReservationAsync(string token)
        {
            if (_currentToken == token && _reservation != null) return CurrentReservation;

            SetLoading(true);
            try
            {
                if (_backendApi == null)
                {
                    ClearReservation();
                    return null;
                }

                var response = await _backendApi.GetReservationByTokenAsync(token);

                _currentStatus = response?.StatusCode;
                _currentToken = token;

                if (response?.StatusCode == HttpStatusCode.Gone)
                {
                    _reservation = null;
                    _reservationRecord = null;
                }
                else
                {
                    _reservation = response?.Reservation;
                    _reservationRecord = response?.ReservationRecord;
                }

                if (response?.StatusCode == HttpStatusCode.OK && response.Reservation is not null)
                {
                    await _tokenService.SaveTokenAsync(token);
                }
                else if (response?.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    await _tokenService.ClearTokenAsync();
                }

                NotifyStateChanged();
                return _reservation;
            }
            catch
            {
                ClearReservation();
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public void SetReservation(RentoomReservation? reservation)
        {
            _reservation = reservation;
            _reservationRecord = null;
            IsLoading = false;
            NotifyStateChanged();
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearReservation()
        {
            _currentToken = null;
            _currentStatus = null;
            _reservation = null;
            _reservationRecord = null;
            IsLoading = false;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
