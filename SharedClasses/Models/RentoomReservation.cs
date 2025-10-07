using RentoomBooking.SharedClasses.Models.IdoBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models
{
    public class RentoomReservation
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; } = default!; //id from IdoSell

        [Newtonsoft.Json.JsonProperty("resToken")]
        public string ResToken { get; set; } = default!; //partition key = token

        [Newtonsoft.Json.JsonProperty("reservation")]
        public Reservation Reservation { get; set; } = new(); //reservation details from IdoSell
    }
}
