using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{



    //REQUEST
    public class ReservationRequestIDOSellAPI
    {
        public ResultSetup result { get; set; }
        public AuthenticateType? authenticate { get; set; }
        public ReservationsParamsSearch paramsSearch { get; set; }
    }
    public class ResultSetup
    {
        public int page { get; set; }
        public int number { get; set; }
    }

    public class ReservationsParamsSearch
    {
        public FromToDateRange? fromDateRange { get; set; }
        public FromToDateRange? toDateRange { get; set; }
        public FromToDateRange? betweenDateRange { get; set; }
        public FromToDateRange? addDateRange { get; set; }
        public string[]? status { get; set; }
        public string? modificationStatus { get; set; }
        public int[]? objectsIds { get; set; }
        public int[]?ids { get; set; }


    }

    public class FromToDateRange
    {
        public string startDate { get; set; }
        public string endDate { get; set; }
    }


    //RESPONSE

    public class ReservationResponseFromIdoSellAPI
    {
        public ReservationsResult result { get; set; }
        public string? id { get; set; }
    }



    public class ReservationsResult
    {
        public IdoPaginationData? Result { get; set; }
        public AuthenticateType Authenticate { get; set; }
        public List<Reservation> Reservations { get; set; }
        public GateErrorType? errors { get; set; }


    }

    public class Reservation
    {
        public int id { get; set; }
        public Guid? RentoomReservationId { get; set; } = null; //lokalne id rezerwacji w Rentoom, które będzie przypisywane do rezerwacji z IdoSell, jeśli uda się znaleźć rezerwację w Rentoom na podstawie tokena z IdoSell (ResToken) - wtedy będzie można łatwo powiązać rezerwację z IdoSell z rezerwacją w Rentoom
        public ReservationDetails? ReservationDetails { get; set; }
        public List<ReservationItem> Items { get; set; }
        public ClientModel Client { get; set; }
        // public decimal? TotalPaymentsAsInAPI { get; set; }

    }

    public class ReservationDetails
    {
        private const string NormalizedDateFormat = "yyyy-MM-dd HH:mm";
        private static readonly string[] SupportedDateFormats = new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
        private static readonly TimeOnly DefaultCheckInTime = new(15, 0);
        private static readonly TimeOnly DefaultCheckOutTime = new(11, 0);

        private string _idbDateFrom = string.Empty;
        private string _idbDateTo = string.Empty;
        private string _dateFrom = string.Empty;
        private string _dateTo = string.Empty;

        public float price { get; set; }
        public float advance { get; set; }
        public string currency { get; set; } = string.Empty;
        public string dateAdd { get; set; } = string.Empty;


        /*Surowe wartości z IdoBooking trafiają teraz do idbDateFrom i idbDateTo, a robocze dateFrom i dateTo są automatycznie normalizowane do 15:00 i 11:00. Normalizacja jest zrobiona w NormalizeIdoDateTime, więc wszystkie obecne miejsca używające ReservationDetails.dateFrom/dateTo i getDateFrom()/getDateTo() dostają już poprawione wartości bez zmian w serwisach.
         * */
        [JsonProperty("dateFrom")]
        public string idbDateFrom
        {
            get => _idbDateFrom;
            set
            {
                _idbDateFrom = value ?? string.Empty;
                _dateFrom = NormalizeIdoDateTime(_idbDateFrom, DefaultCheckInTime);
            }
        }

        [JsonProperty("dateTo")]
        public string idbDateTo
        {
            get => _idbDateTo;
            set
            {
                _idbDateTo = value ?? string.Empty;
                _dateTo = NormalizeIdoDateTime(_idbDateTo, DefaultCheckOutTime);
            }
        }
        
        [JsonProperty("rentoomDateFrom")]
        public string dateFrom
        {
            get => string.IsNullOrWhiteSpace(_dateFrom)
                ? NormalizeIdoDateTime(_idbDateFrom, DefaultCheckInTime)
                : _dateFrom;
            set => _dateFrom = value ?? string.Empty;
        }
        
        [JsonProperty("rentoomDateTo")]
        public string dateTo
        {
            get => string.IsNullOrWhiteSpace(_dateTo)
                ? NormalizeIdoDateTime(_idbDateTo, DefaultCheckOutTime)
                : _dateTo;
            set => _dateTo = value ?? string.Empty;
        }
        public string status { get; set; } = string.Empty;
        public string internalNote { get; set; } = string.Empty;
        public string clientNote { get; set; } = string.Empty;
        public string externalNote { get; set; } = string.Empty;
        public string apiNote { get; set; } = string.Empty;
        public string modificationStatus { get; set; } = string.Empty;
        public string modificationDate { get; set; } = string.Empty;
        public string clientId { get; set; } = string.Empty;
        public float balance { get; set; }
        public string discount { get; set; } = string.Empty;
        public string internalSourceId { get; set; } = string.Empty;
        public int reservationSourceTypeId { get; set; }
        public string reservationSourceId { get; set; } = string.Empty;

        public int getDuration()
        {
            DateTime dateStart = DateTime.ParseExact(dateFrom, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            DateTime dateEnd = DateTime.ParseExact(dateTo, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var days = dateEnd.Subtract(dateStart).Days;
            return days + 1;


        }

        public DateTime getDateFrom()
        {
            return DateTime.ParseExact(dateFrom, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            
        }

        public DateTime getDateTo()
        {
            return DateTime.ParseExact(dateTo, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);


        }

        private static string NormalizeIdoDateTime(string? value, TimeOnly targetTime)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (DateTime.TryParseExact(
                value,
                SupportedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            {
                return new DateTime(
                    parsed.Year,
                    parsed.Month,
                    parsed.Day,
                    targetTime.Hour,
                    targetTime.Minute,
                    0,
                    DateTimeKind.Unspecified)
                    .ToString(NormalizedDateFormat, CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return new DateTime(
                    parsed.Year,
                    parsed.Month,
                    parsed.Day,
                    targetTime.Hour,
                    targetTime.Minute,
                    0,
                    DateTimeKind.Unspecified)
                    .ToString(NormalizedDateFormat, CultureInfo.InvariantCulture);
            }

            return value;
        }
    }

        public class ClientModel
    {
        public string ClientType { get; set; }
        public string Status { get; set; }
        public int Id { get; set; }
        public string Login { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Street { get; set; }
        public string Zipcode { get; set; }
        public string City { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Language { get; set; }
        public string Currency { get; set; }
        public string CountryCode { get; set; }
        public string Notification { get; set; }
        public string SendNewsletter { get; set; }
        public string ClientNote { get; set; }
        public int DiscountForItemsInGroup { get; set; }
        public int DiscountForItemsNotInGroup { get; set; }
        public List<Guest> Guests { get; set; }
        public InvoiceData InvoiceData { get; set; }
    }

    public class InvoiceData
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Street { get; set; }
        public string Zipcode { get; set; }
        public string City { get; set; }
        public string CountryCode { get; set; }
    }

    public class Guest: ClientGuest
    {
        public int Id { get; set; }
     
    }

    public class ReservationItem
    {
        public int objectItemId { get; set; }
        public int itemId { get; set; }
        public string objectName { get; set; }
        public string itemCode { get; set; }
        public int objectId { get; set; }
        public float priceCorrection { get; set; }
        public float price { get; set; }
        public float vat { get; set; }
        public int? numberOfAdults { get; set; }
        public string numberOfSmallChildren { get; set; }
        public string isSurplus { get; set; }
        public List<ReservationAddon> addons { get; set; }
    }

    public class ReservationAddon
    {
        public string addonId { get; set; }
        public int? persons { get; set; }
        public string addonName { get; set; }
        public string price { get; set; }
        public float vat { get; set; }
        public int? nights { get; set; }
        public int? quantity { get; set; }

        public int getAmount()
        {
            int defaultamount = 1;
            if (nights != null) return nights.Value;
            if (quantity != null) return quantity.Value;
            return defaultamount;
        }
    }
}
