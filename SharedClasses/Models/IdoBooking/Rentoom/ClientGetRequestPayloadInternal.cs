using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom
{
    public class ClientGetRequestPayloadInternal
    {
        public ClientGetParams? Params { get; set; }
        public ResultRequestPaging? Settings { get; set; }
    }
}
