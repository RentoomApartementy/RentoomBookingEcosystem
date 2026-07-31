using Newtonsoft.Json;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class OfferAvailabilityForDaysRequest
    {
        [JsonProperty("authenticate")]
        public AuthenticateType Authenticate { get; set; } = new();

        [JsonProperty("paramsSearch")]
        public OfferAvailabilityForDaysParamsSearch? ParamsSearch { get; set; }

        [JsonProperty("result")]
        public ResultRequestPaging? Result { get; set; }
    }

    /// <summary>Mirrors WSDL paramsSearchType for getAvailabilityForDays — dateFrom/dateTo/personsNumber/language.
    /// Deliberately narrower than OfferAvailabilityAndPricesParamsSearch: one combined personsNumber instead of
    /// separate adultsNumber/childrenNumber, and no currency (this endpoint returns no prices).</summary>
    public class OfferAvailabilityForDaysParamsSearch
    {
        [JsonProperty("dateFrom")]
        public string? DateFrom { get; set; }

        [JsonProperty("dateTo")]
        public string? DateTo { get; set; }

        [JsonProperty("personsNumber")]
        public int? PersonsNumber { get; set; }

        [JsonProperty("language")]
        public string? Language { get; set; }
    }

    /// <summary>Mirrors the *AndPrices* Internal wrapper — allows local objectIds filtering, same as today
    /// (IdoBooking does not filter server-side by objectIds for this family of endpoints).</summary>
    public class OfferAvailabilityForDaysParamsSearchInternal
    {
        [JsonProperty("objectIds")]
        public List<int>? ObjectIds { get; set; }

        [JsonProperty("paramsSearch")]
        public OfferAvailabilityForDaysParamsSearch? ParamsSearch { get; set; }
    }

    public class OfferAvailabilityForDaysResponseRoot
    {
        public OfferAvailabilityForDaysResponse Result { get; set; } = new();
    }

    public class OfferAvailabilityForDaysResponse
    {
        [JsonProperty("authenticate")]
        public AuthenticateType? Authenticate { get; set; }

        [JsonProperty("errors")]
        public GateErrorType? Errors { get; set; }

        [JsonProperty("offerObjects")]
        public List<OfferAvailabilityForDaysObject>? OfferObjects { get; set; }

        [JsonProperty("result")]
        public ResultResponseType? Result { get; set; }
    }

    public class OfferAvailabilityForDaysObject
    {
        [JsonProperty("objectId")]
        public int ObjectId { get; set; }

        [JsonProperty("objectName")]
        public string? ObjectName { get; set; }

        [JsonProperty("objectCapacity")]
        public int ObjectCapacity { get; set; }

        [JsonProperty("objectAvailability")]
        public List<OfferAvailabilityForDaysDate>? ObjectAvailability { get; set; }
    }

    /// <summary>WSDL's availabilityDate for this endpoint has only date + itemsNumber — no minStay
    /// (unlike OfferAvailabilityDate used by the AndPrices endpoint).</summary>
    public class OfferAvailabilityForDaysDate
    {
        [JsonProperty("date")]
        public string? Date { get; set; }

        [JsonProperty("itemsNumber")]
        public int ItemsNumber { get; set; }
    }
}
