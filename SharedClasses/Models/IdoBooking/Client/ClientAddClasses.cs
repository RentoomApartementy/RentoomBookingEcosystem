using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.Client
{
    public class ClientAddRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public ClientAddParams Params { get; set; } = new();
    }

    public class ClientAddParams
    {
        public List<ClientAddRequestClient> Clients { get; set; } = new();
    }

    public class ClientAddRequestClient
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Zipcode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public ClientGuest GuestData { get; set; } = new();
        public ClientInvoiceData? InvoiceData { get; set; }
        public string? Notification { get; set; }
        public string? SendNewsletter { get; set; }
        public string? Note { get; set; }
        public float? DiscountForItemsInPromotion { get; set; }
        public float? DiscountForItemsNotInPromotion { get; set; }
    }


    public class ClientAddResponseType
    {
        public ClientAddResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class ClientAddResponse
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public GateErrorType? Errors { get; set; }
        public List<ClientAddResult>? Clients { get; set; }
    }

    public class ClientAddResult
    {
        public bool Success { get; set; }
        public GateErrorType? Error { get; set; }
        public int? ClientId { get; set; }
        public string? ClientLogin { get; set; }
    }
}
