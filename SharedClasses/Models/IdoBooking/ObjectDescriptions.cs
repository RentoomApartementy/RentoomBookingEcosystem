using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{

    public class ObjectDescriptionsRequestType
    {
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public ObjectDescriptionParamsSearch ParamsSearch { get; set; } = new();

    }

    public class ObjectDescriptionParamsSearch
    {
        public int ObjectId { get; set; }
        public string Language { get; set; } = "pol"; //ISO-639-3
    }


    public class ObjectDescriptionsResponseType
    {
        public ObjectDescriptionsResponse Result { get; set; } = new();
        public string? Id { get; set; }

    }


    public class ObjectDescriptionsResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public List<GateErrorType>? Errors { get; set; }
        public bool? Success { get; set; }
        public List<ObjectDescription>? ObjectDescriptions { get; set; }
    }

    public class ObjectDescription
    {
        public int ObjectId { get; set; }
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
       
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public string? NameInPanel {  get; set; }
        public string? Language { get; set; }
    }


}
