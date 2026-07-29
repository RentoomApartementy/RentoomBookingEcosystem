using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBookingWeb.Services;

namespace RentoomBookingWeb.Components.Features.ReservationWorkflow.Components.ApartmentPage
{
    /// <summary>
    /// Availability-aware, guests-first booking widget for a single apartment.
    /// Replaces the generic (portfolio-wide) SearchBar picker inside the apartment
    /// page modal. Unavailable nights cannot be selected, so the "no offers" dead-end
    /// cannot arise on the mainline path (audit L7/L5). Emits the same
    /// startDate/endDate/adults/children contract as SearchBar via <see cref="OnSearch"/>.
    /// </summary>
    public partial class ApartmentBookingWidget : ComponentBase
    {
        [Parameter] public ApartmentObject? Apartment { get; set; }
        [Parameter] public string? StartDate { get; set; }
        [Parameter] public string? EndDate { get; set; }
        [Parameter] public string? Adults { get; set; }
        [Parameter] public string? Children { get; set; }
        [Parameter] public IStringLocalizer Localizer { get; set; } = default!;
        [Parameter] public EventCallback<Dictionary<string, string>> OnSearch { get; set; }
        [Parameter] public EventCallback<decimal?> FromPriceChanged { get; set; }

        [Inject] public IApartmentCalendarService CalendarService { get; set; } = default!;

        private const int InitialMonths = 3;
        private const int MoreMonths = 2;
        private const int MaxWindowMonths = 12;

        private DateOnly _today;
        private DateOnly _windowEnd;

        private int _adults = 2;
        private int _children = 0;

        private DateOnly? _selStart;
        private DateOnly? _selEnd;

        private ApartmentCalendarDto? _calendar;
        private bool _loading;
        private string? _occupancyNotice;
        private string? _dateNotice;

        private int MaxGuests => Apartment?.Capacity is int c && c > 0 ? c : 16;
        private int MinAdults => Apartment?.MinCapacity is int m && m > 0 ? Math.Min(m, MaxGuests) : 1;
        private int TotalGuests => _adults + _children;

        protected override async Task OnInitializedAsync()
        {
            _today = DateOnly.FromDateTime(DateTime.Today);
            _windowEnd = AddMonthsClamped(FirstOfMonth(_today), InitialMonths);

            ParseGuests();
            ParseIncomingRange();

            await LoadCalendarAsync();
        }

        private void ParseGuests()
        {
            _adults = int.TryParse(Adults, out var a) && a > 0 ? a : 2;
            _children = int.TryParse(Children, out var c) && c > 0 ? c : 0;
            ClampGuests();
        }

        private void ClampGuests()
        {
            if (_adults < MinAdults) _adults = MinAdults;
            if (_adults < 1) _adults = 1;
            if (TotalGuests > MaxGuests)
            {
                _children = Math.Max(0, MaxGuests - _adults);
                if (_adults > MaxGuests) _adults = MaxGuests;
            }
        }

        private void ParseIncomingRange()
        {
            if (TryParseDate(StartDate, out var s) && s >= _today)
            {
                _selStart = s;
                if (TryParseDate(EndDate, out var e) && e > s)
                {
                    _selEnd = e;
                    if (e > _windowEnd)
                    {
                        _windowEnd = AddMonthsClamped(FirstOfMonth(_today), MaxWindowMonths);
                    }
                }
            }
        }

        private async Task LoadCalendarAsync()
        {
            if (Apartment is null)
            {
                return;
            }

            _loading = true;
            StateHasChanged();
            try
            {
                _calendar = await CalendarService.GetCalendarAsync(
                    Apartment.Id,
                    _today,
                    LastVisibleDay(),
                    _adults,
                    _children);

                RevalidateSelection();
                await FromPriceChanged.InvokeAsync(_calendar?.FromPriceGross);
            }
            finally
            {
                _loading = false;
                StateHasChanged();
            }
        }

        // ---- Guests -------------------------------------------------------

        private async Task IncrementAdults()
        {
            _occupancyNotice = null;
            if (TotalGuests >= MaxGuests)
            {
                _occupancyNotice = Localizer["CalendarMaxOccupancy", MaxGuests];
                return;
            }
            _adults++;
            await LoadCalendarAsync();
        }

        private async Task DecrementAdults()
        {
            _occupancyNotice = null;
            if (_adults <= MinAdults) return;
            _adults--;
            await LoadCalendarAsync();
        }

        private async Task IncrementChildren()
        {
            _occupancyNotice = null;
            if (TotalGuests >= MaxGuests)
            {
                _occupancyNotice = Localizer["CalendarMaxOccupancy", MaxGuests];
                return;
            }
            _children++;
            await LoadCalendarAsync();
        }

        private async Task DecrementChildren()
        {
            _occupancyNotice = null;
            if (_children <= 0) return;
            _children--;
            await LoadCalendarAsync();
        }

        // ---- Calendar model ----------------------------------------------

        private IEnumerable<DateOnly> MonthStarts()
        {
            var cursor = FirstOfMonth(_today);
            var last = FirstOfMonth(_windowEnd);
            while (cursor <= last)
            {
                yield return cursor;
                cursor = cursor.AddMonths(1);
            }
        }

        /// <summary>Last calendar day currently rendered = end of the last visible month.</summary>
        private DateOnly LastVisibleDay() => FirstOfMonth(_windowEnd).AddMonths(1).AddDays(-1);

        private bool CanLoadMore() => FirstOfMonth(_windowEnd) < AddMonthsClamped(FirstOfMonth(_today), MaxWindowMonths);

        private async Task LoadMoreMonths()
        {
            if (!CanLoadMore()) return;
            _windowEnd = AddMonthsClamped(_windowEnd, MoreMonths);
            await LoadCalendarAsync();
        }

        /// <summary>Monday-first leading blank count for a month's first day.</summary>
        private static int LeadingBlanks(DateOnly monthStart)
            => ((int)monthStart.DayOfWeek + 6) % 7;

        private static int DaysInMonth(DateOnly monthStart)
            => DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

        private bool IsAvailable(DateOnly date)
            => _calendar is not null
               && _calendar.Days.TryGetValue(date, out var cell)
               && cell.Available;

        private decimal? PriceFor(DateOnly date)
            => _calendar is not null && _calendar.Days.TryGetValue(date, out var cell) ? cell.PriceGross : null;

        private int MinStayFor(DateOnly date)
            => _calendar is not null && _calendar.Days.TryGetValue(date, out var cell) && cell.MinStay is int m && m > 0 ? m : 1;

        private bool IsSelectable(DateOnly date)
            => date >= _today && IsAvailable(date);

        private bool IsSelectedStart(DateOnly date) => _selStart == date;
        private bool IsSelectedEnd(DateOnly date) => _selEnd == date;

        private bool IsInRange(DateOnly date)
            => _selStart is DateOnly s && _selEnd is DateOnly e && date > s && date < e;

        // ---- Selection ----------------------------------------------------

        private void OnDayClicked(DateOnly date)
        {
            _dateNotice = null;

            if (!IsSelectable(date))
            {
                return;
            }

            // Start a fresh range when nothing/complete selected, or clicking on/before current start.
            if (_selStart is null || _selEnd is not null || date <= _selStart)
            {
                _selStart = date;
                _selEnd = null;
                return;
            }

            // Selecting the check-out date.
            var nights = date.DayNumber - _selStart.Value.DayNumber;
            var minStay = MinStayFor(_selStart.Value);

            if (nights < minStay)
            {
                _dateNotice = Localizer["CalendarMinStayNotice", minStay];
                return;
            }

            if (!IsRangeAllNightsAvailable(_selStart.Value, date))
            {
                // A gap of unavailable nights — restart the range at the clicked date.
                _selStart = date;
                _selEnd = null;
                return;
            }

            _selEnd = date;
        }

        /// <summary>Every night in [start, end) must be available (end is the checkout day).</summary>
        private bool IsRangeAllNightsAvailable(DateOnly start, DateOnly end)
        {
            for (var night = start; night < end; night = night.AddDays(1))
            {
                if (!IsAvailable(night))
                {
                    return false;
                }
            }
            return true;
        }

        private void RevalidateSelection()
        {
            if (_selStart is DateOnly s && !IsAvailable(s))
            {
                _selStart = null;
                _selEnd = null;
                return;
            }

            if (_selStart is DateOnly ss && _selEnd is DateOnly ee &&
                (!IsRangeAllNightsAvailable(ss, ee) || (ee.DayNumber - ss.DayNumber) < MinStayFor(ss)))
            {
                _selEnd = null;
            }
        }

        private bool HasCompleteRange => _selStart is not null && _selEnd is not null;

        private int SelectedNights =>
            _selStart is DateOnly s && _selEnd is DateOnly e ? e.DayNumber - s.DayNumber : 0;

        private decimal SelectedTotal()
        {
            if (_selStart is not DateOnly s || _selEnd is not DateOnly e)
            {
                return 0m;
            }

            decimal total = 0m;
            for (var night = s; night < e; night = night.AddDays(1))
            {
                total += PriceFor(night) ?? 0m;
            }
            return total;
        }

        private async Task Confirm()
        {
            if (!HasCompleteRange || !OnSearch.HasDelegate)
            {
                return;
            }

            var query = new Dictionary<string, string>
            {
                ["startDate"] = _selStart!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["endDate"] = _selEnd!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["adults"] = _adults.ToString(CultureInfo.InvariantCulture),
                ["children"] = _children.ToString(CultureInfo.InvariantCulture)
            };

            await OnSearch.InvokeAsync(query);
        }

        // ---- Formatting helpers ------------------------------------------

        private string FormatPrice(decimal value)
            => $"{Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentUICulture)} zł";

        private static IEnumerable<string> WeekdayHeaders()
        {
            var names = CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames; // Sunday-first
            for (var i = 0; i < 7; i++)
            {
                yield return names[(i + 1) % 7]; // Monday-first
            }
        }

        private string MonthTitle(DateOnly monthStart)
            => CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(
                monthStart.ToString("MMMM yyyy", CultureInfo.CurrentUICulture));

        private string CheckInPrompt => Localizer[_selStart is null ? "CalendarSelectCheckIn" : "CalendarSelectCheckOut"];

        private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

        private static DateOnly AddMonthsClamped(DateOnly monthStart, int months)
            => FirstOfMonth(monthStart).AddMonths(months);

        private static bool TryParseDate(string? value, out DateOnly date)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }
            date = default;
            return false;
        }
    }
}
