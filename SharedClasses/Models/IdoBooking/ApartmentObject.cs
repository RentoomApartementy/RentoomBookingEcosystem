using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    
    public class ApartmentObject
    {
        public string Id { get; set; }
        public string? Name { get; set; }
        public int? Capacity { get; set; }
        public string? Area { get; set; }
        public int? MinCapacity { get; set; }
        public bool? PricesPerPersons { get; set; }
        public int? BedroomsCount { get; set; }
        public bool? PriceRules { get; set; }
        public List<BedConfigurationArray>? BedsConfiguration { get; set; }
        public List<CategoryType>? Categories { get; set; }
        public List<ItemType>? Items { get; set; }
        public List<AddonType>? Addons { get; set; }
        public ObjectLocation? ObjectLocation { get; set; }
    }


    public class CategoryType
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? IsActive { get; set; }
    }
    public class BedConfigurationArray
    {
        public int Count { get; set; }
        public string? BedType { get; set; } // BedTypeType
    }
    public class IsActiveType
    {
        public const string N = "n";
        public const string Y = "y";
    }

    public class ItemType
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
    }

    public class AddonType
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public bool? Costless { get; set; }
        public bool? Optional { get; set; }
        public string? Type { get; set; }
        public string? PersonType { get; set; }
    }


   

   
}
