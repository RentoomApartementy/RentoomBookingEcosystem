using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.RentoomBooking
{
   
    //todo: depracate - sprawdzic kod i usunąć klasę. nie powinna być już używana.
    public class AmenitiesFilterDocument
    {
        public string id { get; set; } = string.Empty;
        public int[] amenities { get; set; } = Array.Empty<int>();
        public List<SearchFilter> filters { get; set; } = new();
    }

    public class SearchFilterDocument
    {

        public string id { get; set; } = string.Empty;
        public Dictionary<string, List<SearchFilter>> filtersDictionary { get; set; } = new();
    }

    
    public class SearchFilter
    {
        public string id { get; set; }
        public string name { get;set; }
        public string icon_materialui_name { get; set; }
    }
}
