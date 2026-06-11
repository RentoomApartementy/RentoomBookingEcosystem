using Newtonsoft.Json; 
using Newtonsoft.Json.Linq; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization; 
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class SingleOrArrayConverter<T> : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>();
            }
            return new List<T> { token.ToObject<T>() };
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public class ObjectAmenitiesRequestType : ObjectDefaultRequestType
    {

    }

    public class ObjectAmenitiesResponseType
    {
        public ObjectAmenitiesResponse Result { get; set; } = new();
        public string Id { get; set; } = string.Empty;
    }

    public class ObjectAmenitiesResponse
    {
        public AuthenticateType? Authenticate { get; set; }

        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter<GateErrorType>))]
        public List<GateErrorType>? Errors { get; set; }

        public bool? Success { get; set; }
        public List<ObjectAmenity>? ObjectAmenities { get; set; }
    }

    public class ObjectAmenity
    {
        [JsonPropertyName("ObjectId")]
        private int objectId;


        [JsonPropertyName("Id")]
        public int Id { get; set; }

        public int ObjectId { 
            
            get => objectId; 
            set
                { 
                int x = Convert.ToInt32(value);
                objectId = x; 
                }  
        }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("iconName")]
        public string? IconName { get; set; }
    }
}