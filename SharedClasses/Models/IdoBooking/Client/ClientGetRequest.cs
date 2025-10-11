using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking
{
    public class ClientGetRequest
    {
        public AuthenticateType Authenticate { get; set; } = new AuthenticateType();
        public ResultRequestPaging? Settings { get; set; }
        public ClientGetParams? Params { get; set; }
    }

    public class ClientGetParams
    {
        public int? Id { get; set; }
        public string? Login { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }

    public class ClientGetResponseType
    {
        public ClientGetResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class ClientGetResponse
    {
        public AuthenticateType? Authenticate { get; set; }
        public List<GateErrorType>? Errors { get; set; }
        public List<ClientWithGuest>? Clients { get; set; }
        public ResultResponseType? Result { get; set; }
    }


}
