using Newtonsoft.Json;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class OfferPricesForDaysRequest
    {
        [JsonProperty("authenticate")]
        public AuthenticateType Authenticate { get; set; } = new();

        [JsonProperty("paramsSearch")]
        public OfferPricesForDaysParamsSearch? ParamsSearch { get; set; }

        // No "result"/paging field - this endpoint always returns the full list (confirmed against real API behavior).
    }

    public class OfferPricesForDaysParamsSearch
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
    }

    /// <summary>Mirrors the Internal wrapper convention of sibling endpoints — allows local objectIds
    /// filtering, same as today (IdoBooking does not filter server-side by objectIds for this family).</summary>
    public class OfferPricesForDaysParamsSearchInternal
    {
        [JsonProperty("objectIds")]
        public List<int>? ObjectIds { get; set; }

        [JsonProperty("paramsSearch")]
        public OfferPricesForDaysParamsSearch? ParamsSearch { get; set; }
    }

    public class OfferPricesForDaysResponseRoot
    {
        public OfferPricesForDaysResponse Result { get; set; } = new();
    }

    public class OfferPricesForDaysResponse
    {
        [JsonProperty("authenticate")]
        public AuthenticateType? Authenticate { get; set; }

        [JsonProperty("errors")]
        public GateErrorType? Errors { get; set; }

        [JsonProperty("offerObjects")]
        public List<OfferPricesForDaysObject>? OfferObjects { get; set; }

        // No paging field - confirmed the real API never returns partial pages for this endpoint.
    }

    public class OfferPricesForDaysObject
    {
        [JsonProperty("objectId")]
        public int ObjectId { get; set; }

        [JsonProperty("objectName")]
        public string? ObjectName { get; set; }

        [JsonProperty("objectCapacity")]
        public int ObjectCapacity { get; set; }

        [JsonProperty("objectPricesDates")]
        public List<OfferPriceDate>? ObjectPricesDates { get; set; }
    }
}
