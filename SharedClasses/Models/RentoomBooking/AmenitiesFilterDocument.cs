using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.RentoomBooking
{
    public class AmenitiesFilterDocument
    {
        public string id { get; set; } = string.Empty;
        public int[] amenities { get; set; } = Array.Empty<int>();
        public List<SearchFilter> filters { get; set; } = new();
    }

    public class SearchFilterDocument : AmenitiesFilterDocument
    {
        public Dictionary<string, List<SearchFilter>> filtersDictionary { get; set; } = new();
    }

    
    public class SearchFilter
    {
        public string id { get; set; }
        public string name { get;set; }

    }
}
