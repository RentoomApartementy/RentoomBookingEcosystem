using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class ClientInvoiceData :InvoiceData
    {
      //  public string? FirstName { get; set; }
      //  public string? LastName { get; set; }
        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
      //  public string Street { get; set; } = string.Empty;
      //  public string Zipcode { get; set; } = string.Empty;
      //  public string City { get; set; } = string.Empty;
     //   public string CountryCode { get; set; } = string.Empty;
    }
}
