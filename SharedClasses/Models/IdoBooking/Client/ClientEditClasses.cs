using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.Client
{
    public class ClientEditRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public ClientEditParams Params { get; set; } = new();
    }

    public class ClientEditParams
    {
        public List<ClientEditRequestClient> Clients { get; set; } = new();
    }

    public class ClientEditRequestClient
    {
        public string? Login { get; set; }
        public int? Id { get; set; }
        public string? Password { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Street { get; set; }
        public string? Zipcode { get; set; }
        public string? City { get; set; }
        public string? CountryCode { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Language { get; set; }
        public string? Currency { get; set; }
        public ClientGuest? GuestData { get; set; }
        public ClientInvoiceData? InvoiceData { get; set; }
        public string? Notification { get; set; }
        public string? SendNewsletter { get; set; }
        public string? Note { get; set; }
        public float? DiscountForItemsInPromotion { get; set; }
        public float? DiscountForItemsNotInPromotion { get; set; }
    }

    public class ClientEditResponseType
    {
        public ClientEditResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class ClientEditResponse
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public GateErrorType? Errors { get; set; }
        public List<ClientEditResult>? Clients { get; set; }
    }

    public class ClientEditResult
    {
        public bool Success { get; set; }
        public GateErrorType? Error { get; set; }
        public int? ClientId { get; set; }
        public string? ClientLogin { get; set; }
    }
}
