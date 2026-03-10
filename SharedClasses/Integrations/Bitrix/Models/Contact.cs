using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Bitrix.Models
{
    public class CreateContactRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public int? AssignedById { get; set; }
        public int? ReservationId { get; set; }
        public string? TaxNumber { get; set; }
        public string? CompanyAddress { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyEmail { get; set; }
    }
}
