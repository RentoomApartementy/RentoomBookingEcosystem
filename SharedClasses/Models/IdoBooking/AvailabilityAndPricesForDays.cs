using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{

    public class OfferAvailabilityAndPricesForDaysRequest
    {
        [JsonProperty("authenticate")]
        public AuthenticateType Authenticate { get; set; } = new();

        [JsonProperty("paramsSearch")]
        public OfferAvailabilityAndPricesParamsSearch? ParamsSearch { get; set; }

        [JsonProperty("result")]
        public ResultRequestPaging? Result { get; set; }
    }

    public class OfferAvailabilityAndPricesParamsSearch
    {
        [JsonProperty("dateFrom")]
        public string? DateFrom { get; set; }

        [JsonProperty("dateTo")]
        public string? DateTo { get; set; }

        [JsonProperty("adultsNumber")]
        public int AdultsNumber { get; set; }

        [JsonProperty("childrenNumber")]
        public int? ChildrenNumber { get; set; }

        [JsonProperty("language")]
        public string? Language { get; set; }

        [JsonProperty("currency")]
        public string? Currency { get; set; }

        //[JsonProperty("minStay")]
       // public bool MinStay { get; set; } = true;
    }

    public class OfferAvailabilityAndPricesParamsSearchInternal //to allow filtering results by object id.
    {
        [JsonProperty("objectIds")]
        public List<int>? ObjectIds { get; set; }

        [JsonProperty("paramsSearch")]
        public OfferAvailabilityAndPricesParamsSearch? ParamsSearch { get; set; }

    }


    public class OfferAvailabilityAndPricesForDaysResponseRoot
    {
        public OfferAvailabilityAndPricesForDaysResponse Result { get; set; } = new();
    }
    public class OfferAvailabilityAndPricesForDaysResponse
    {
        [JsonProperty("authenticate")]
        public AuthenticateType? Authenticate { get; set; }

        [JsonProperty("errors")]
        public GateErrorType? Errors { get; set; }

        [JsonProperty("offerObjects")]
        public List<OfferAvailabilityObject>? OfferObjects { get; set; }

        [JsonProperty("result")]
        public ResultResponseType? Result { get; set; }
    }

    public class OfferAvailabilityObject
    {
        [JsonProperty("objectId")]
        public int ObjectId { get; set; }

        [JsonProperty("objectName")]
        public string? ObjectName { get; set; }

        [JsonProperty("objectCapacity")]
        public int ObjectCapacity { get; set; }

        [JsonProperty("objectAvailability")]
        public List<OfferAvailabilityDate>? ObjectAvailability { get; set; }

        [JsonProperty("objectPricesDates")]
        public List<OfferPriceDate>? ObjectPricesDates { get; set; }
    }

    public class OfferAvailabilityDate
    {
        [JsonProperty("date")]
        public string? Date { get; set; }

        [JsonProperty("itemsNumber")]
        public int ItemsNumber { get; set; }

        [JsonProperty("minStay")]
        public int? MinStay { get; set; }

       // [JsonProperty("closedToArrival")]
      //  public bool? ClosedToArrival { get; set; }

      //  [JsonProperty("closedToDeparture")]
      //  public bool? ClosedToDeparture { get; set; }
    }

    public class OfferPriceDate
    {
        [JsonProperty("date")]
        public string? Date { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }
    }
}
