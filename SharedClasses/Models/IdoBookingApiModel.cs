
namespace RentoomBooking.SharedClasses.Models
{
  
    public class ApartmentResponseType
    {
        public GetAllObjectsResponseType? Result { get; set; }
        public string? Id { get; set; }
    }
    public class ResultResponseType
    {
        public int Page { get; set; }
        public int CountOnPage {get;set; }
        public int PageAll { get; set; }
        public int CountAll { get; set; }
    }

    public class  GateErrorType
    {
        public int FaultCode { get; set; }
        public string FaultString { get; set; }
    }

    public class AuthenticateType
    {
        public string SystemKey { get; set; } = string.Empty;
        public string SystemLogin { get; set; } = string.Empty;
        public string? Lang { get; set; } = "pol";
    }

    public class ContainerRequestType { 
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public ResultRequestType Result { get; set; } = new ResultRequestType();

    }

    public class GetAllObjectsResponseType
    {
        public AuthenticateType? Authenticate { get; set; }
        public List<GateErrorType>? Errors { get; set; }
        public List<ApartmentObject>? Objects { get; set; }
        public ResultResponseType? Result { get; set; }
        public bool? Success { get; set; }
    }

    public class ResultRequestType
    {
        public int Page { get; set; }
     
        public int Number { get; set; }
    }


}
