using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class ClientWithGuest : Client
    {
      //  public string Login { get; set; } = string.Empty;
     //   public int Id { get; set; }
      //  public string ClientType { get; set; } = string.Empty;
      //  public string Status { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
      //  public string FirstName { get; set; } = string.Empty;
     //   public string LastName { get; set; } = string.Empty;
     //   public string Street { get; set; } = string.Empty;
     //   public string Zipcode { get; set; } = string.Empty;
      //  public string City { get; set; } = string.Empty;
     //   public string CountryCode { get; set; } = string.Empty;
     //   public string? Phone { get; set; }
     //   public string? Email { get; set; }
     //   public string Language { get; set; } = string.Empty;
     //   public string Currency { get; set; } = string.Empty;
        public List<ClientGuest> Guests { get; set; } = new();
        public ClientInvoiceData? InvoiceData { get; set; }
    //    public string? Notification { get; set; }
    //    public string? SendNewsletter { get; set; }
        public string? Note { get; set; }
        public float? DiscountForItemsInPromotion { get; set; }
        public float? DiscountForItemsNotInPromotion { get; set; }
    }

    public class ClientGuest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Zipcode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string Language { get; set; } = string.Empty;
    }
}
