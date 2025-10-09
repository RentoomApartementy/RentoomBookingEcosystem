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
        public ReservationDetails? ReservationDetails { get; set; }
        public List<ReservationItem> Items { get; set; }
        public Client Client { get; set; }
        public decimal? TotalPaymentsAsInAPI { get; set; }

    }

    public class ReservationDetails
    {
        public float price { get; set; }
        public float advance { get; set; }
        public string currency { get; set; }
        public string dateAdd { get; set; }
        public string dateFrom { get; set; }
        public string dateTo { get; set; }
        public string status { get; set; }
        public string internalNote { get; set; }
        public string clientNote { get; set; }
        public string externalNote { get; set; }
        public string apiNote { get; set; }
        public string modificationStatus { get; set; }
        public string modificationDate { get; set; }
        public string clientId { get; set; }
        public float balance { get; set; }
        public string discount { get; set; }
        public string internalSourceId { get; set; }
        public int reservationSourceTypeId { get; set; }
        public string reservationSourceId { get; set; }

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
    }

        public class Client
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
