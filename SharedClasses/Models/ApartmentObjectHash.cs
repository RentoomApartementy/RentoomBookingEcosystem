using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models
{
    public class ApartmentObjectHash
    {
        public string id { get; set; } = "all-object-hashes"; // Unique ID for this document
        public List<ItemHash> Hashes { get; set; } = new List<ItemHash>();
        public DateTime lastUpdated { get; set; }
    }

    public class ItemHash
    {
        public string objectId { get; set; } = string.Empty; 
        public string hash { get; set; }  =string.Empty;
    }
}
