using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.RentoomBooking
{
    public class AmenitiesFilterDocument
    {
        public string id { get; set; } = "amenities-filter";
        public int[] amenities { get; set; } = Array.Empty<int>();
    }
}
