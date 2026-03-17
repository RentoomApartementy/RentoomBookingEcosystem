using System;

namespace RentoomBooking.SharedClasses.Models
{
    public class CustomerAgreedTermDto
    {
        public int TermsSourceId { get; set; }
        public string? Description { get; set; }
        public DateTime AgreedAt { get; set; }
        public bool IsRequired { get; set; }
        public bool IsAccepted { get; set; }
        public string? TermsSourceType { get; set; }
    }

    public class UpdateAgreedTermRequest
    {
        public int TermsSourceId { get; set; }
        public bool IsAccepted { get; set; }
    }
}