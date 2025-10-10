using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.Public
{
    public class PublicParametersResult
    {
        public PublicParametersResponseType Result { get; set; } = new();
        public string? Id { get; set; } = "parameters";
    }

   /* public class PublicParametersResponseResult
    {
        
        public bool Success { get; set; }

      
        public GateErrorType? Errors { get; set; }

       
        public PublicParametersResponseType? Result { get; set; }
    }
   */

    public class PublicParametersResponseType
    {
       
      //  public List<RoomTypeItem>? RoomTypes { get; set; }

      
      //  public List<CurrencyItem>? Currencies { get; set; }

      
      //  public List<LanguageItem>? Languages { get; set; }

      
        public List<LocalizationItem>? Locations { get; set; }
    }

    public class LocalizationItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("street")]
        public string? Street { get; set; }

        [JsonProperty("zipcode")]
        public string? ZipCode { get; set; }

        [JsonProperty("city")]
        public string? City { get; set; }

        [JsonProperty("region")]
        public string? Region { get; set; }

        [JsonProperty("country")]
        public string? Country { get; set; }

        [JsonProperty("geolocation_lat")]
        public float? GeoLocationLat { get; set; }

        [JsonProperty("geolocation_lng")]
        public float? GeoLocationLng { get; set; }

        [JsonProperty("phones")]
        public string[]? Phones { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("reception_info")]
        public string? ReceptionInfo { get; set; }

        [JsonProperty("directions_info")]
        public string? DirectionsInfo { get; set; }

        [JsonProperty("directions_info_plain_text")]
        public string? DirectionsInfoPlainText { get; set; }

        [JsonProperty("checkin_hours")]
        public CheckInHoursRange? CheckInHours { get; set; }

        [JsonProperty("checkout_hours")]
        public CheckOutHoursRange? CheckOutHours { get; set; }

        [JsonProperty("reception_hours")]
        public ReceptionsHoursRange? ReceptionHours { get; set; }
    }

  /*  public class PhoneType
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("number")]
        public string? Number { get; set; }
    }
  */
    public class CheckInHoursRange
    {
        [JsonProperty("from")]
        public string? From { get; set; }

        [JsonProperty("to")]
        public string? To { get; set; }
    }

    public class CheckOutHoursRange
    {
        [JsonProperty("from")]
        public string? From { get; set; }

        [JsonProperty("to")]
        public string? To { get; set; }
    }

    public class ReceptionsHoursRange
    {
        [JsonProperty("from")]
        public string? From { get; set; }

        [JsonProperty("to")]
        public string? To { get; set; }
    }

}
