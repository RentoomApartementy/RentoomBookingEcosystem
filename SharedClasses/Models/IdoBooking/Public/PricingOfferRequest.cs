using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.Public
{
    public class PricingOffersRequest
    {
        [JsonProperty("objectIds")]
        public List<int>? ObjectIds { get; set; }

        [JsonProperty("dateFrom")]
        public string? DateFrom { get; set; }

        [JsonProperty("dateTo")]
        public string? DateTo { get; set; }

        [JsonProperty("currency")]
        public string? Currency { get; set; }

        [JsonProperty("numberOfAdults")]
        public int? NumberOfAdults { get; set; }

        [JsonProperty("numberOfBigChildren")]
        public int? NumberOfBigChildren { get; set; }

        [JsonProperty("language")]
        public string? Language { get; set; }
    }

    public class PricingOffersResponse
    {
        [JsonProperty("result")]
        public PricingOffersResult? Result { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("errors")]
        public GateErrorType? Errors { get; set; }
    }

    public class PricingOffersResult
    {
        [JsonProperty("pricingOffers")]
        public List<PricingOffer>? PricingOffers { get; set; }
    }

    public class PricingOffer
    {
        [JsonProperty("objectId")]
        public int ObjectId { get; set; }

        [JsonProperty("minimalPrice")]
        public decimal MinimalPrice { get; set; }

        [JsonProperty("offers")]
        public List<OfferItem>? Offers { get; set; }
    }

    public class OfferItem
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("offerType")]
        public string? OfferType { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("persons")]
        public int Persons { get; set; }

        [JsonProperty("promotionId")]
        public int? PromotionId { get; set; }

        [JsonProperty("includedRateplanIds")]
        public List<int>? IncludedRateplanIds { get; set; }
    }
}
