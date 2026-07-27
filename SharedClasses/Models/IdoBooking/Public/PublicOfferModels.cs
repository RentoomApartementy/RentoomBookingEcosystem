using Newtonsoft.Json;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.Public
{
    // Request body for public/offer/34/json: { "offerId": 256 }.
    // offerId is the apartment id (ApartmentId / objectId).
    public class PublicOfferRequest
    {
        [JsonProperty("offerId")]
        public int OfferId { get; set; }
    }

    public class PublicOfferResponse
    {
        [JsonProperty("result")]
        public PublicOfferResult? Result { get; set; }

        [JsonProperty("id")]
        public string? Id { get; set; }

        // IdoBooking is inconsistent: some endpoints report errors at the root,
        // others inside "result". We check both (see PublicOfferResult.Errors).
        [JsonProperty("errors")]
        public GateErrorType? Errors { get; set; }
    }

    public class PublicOfferResult
    {
        [JsonProperty("images")]
        public List<PublicOfferImage>? Images { get; set; }

        [JsonProperty("minimalPrice")]
        public decimal? MinimalPrice { get; set; }

        [JsonProperty("currency")]
        public string? Currency { get; set; }

        [JsonProperty("errors")]
        public GateErrorType? Errors { get; set; }
    }

    public class PublicOfferImage
    {
        [JsonProperty("url")]
        public string? Url { get; set; }
    }

    // Domain projection consumed by the blog apartments listing card.
    // Deliberately minimal - only what the fallback price + image need.
    public class PublicApartmentOffer
    {
        public int ApartmentId { get; init; }
        public decimal MinimalPrice { get; init; }
        public string? Currency { get; init; }
        public string? ImageUrl { get; init; }
    }
}
